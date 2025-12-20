#!/usr/bin/env python3
# -*- coding: utf-8 -*-

"""
从 DXF 中“识别（通过图例文字关联）”绿色图例符号，并导出为独立的 BLOCK。

思路（针对你给的图例页非常快）：
- 黄色中文 TEXT（默认图层 V-TXT1、ACI=2）作为“图例名称”
- 在图例区域内提取绿色几何（默认 ACI=3），按 2D 外包框相交聚类为若干符号簇
- 将每个图例名称匹配到最近的符号簇，导出为一个 block（block 名称 = 图例文字）

输出：
- 一个新的 DXF：包含每个图例符号对应的 BLOCK，并在 ModelSpace 中按网格插入一遍便于查看。
"""

from __future__ import annotations

import argparse
import math
import re
from collections import defaultdict
from dataclasses import dataclass
from typing import Iterable, Optional

import ezdxf
from ezdxf.addons.importer import Importer

HAN_RE = re.compile(r"[\u4e00-\u9fff]")


def resolved_aci(entity, doc: ezdxf.EzdxfDocument, fallback: int = 7) -> int:
	"""解析实体的最终 ACI 颜色（简化版：处理 BYLAYER/BYBLOCK）。"""
	try:
		true_color = getattr(entity.dxf, "true_color", None)
	except Exception:
		true_color = None

	# 这里主要用 ACI 做筛选；如你后续需要 TrueColor，可扩展为 RGB 判断
	color = getattr(entity.dxf, "color", 256)
	if color in (None, 0, 256):
		try:
			layer = doc.layers.get(entity.dxf.layer)
			layer_color = getattr(layer.dxf, "color", fallback)
			if layer_color in (None, 0, 256):
				return fallback
			return int(layer_color)
		except Exception:
			return fallback
	return int(color)


def bbox2d(entity) -> Optional[tuple[float, float, float, float]]:
	"""计算 2D 外包框（只覆盖图例常见实体类型）。返回 (minx,miny,maxx,maxy)。"""
	t = entity.dxftype()
	try:
		if t == "LINE":
			s = entity.dxf.start
			e = entity.dxf.end
			return (min(s.x, e.x), min(s.y, e.y), max(s.x, e.x), max(s.y, e.y))
		if t == "CIRCLE":
			c = entity.dxf.center
			r = float(entity.dxf.radius)
			return (c.x - r, c.y - r, c.x + r, c.y + r)
		if t == "POINT":
			p = entity.dxf.location
			return (p.x, p.y, p.x, p.y)
		if t in ("TEXT", "MTEXT"):
			p = entity.dxf.insert
			return (p.x, p.y, p.x, p.y)
		if t == "INSERT":
			p = entity.dxf.insert
			return (p.x, p.y, p.x, p.y)
		if t == "LWPOLYLINE":
			pts = list(entity.get_points("xy"))
			if not pts:
				return None
			xs = [p[0] for p in pts]
			ys = [p[1] for p in pts]
			return (min(xs), min(ys), max(xs), max(ys))
		if t == "POLYLINE":
			pts = [v.dxf.location for v in entity.vertices]  # type: ignore[attr-defined]
			if not pts:
				return None
			xs = [p.x for p in pts]
			ys = [p.y for p in pts]
			return (min(xs), min(ys), max(xs), max(ys))
	except Exception:
		return None
	return None


def overlap(a: tuple[float, float, float, float], b: tuple[float, float, float, float], tol: float) -> bool:
	ax0, ay0, ax1, ay1 = a
	bx0, by0, bx1, by1 = b
	return not (ax1 + tol < bx0 or bx1 + tol < ax0 or ay1 + tol < by0 or by1 + tol < ay0)


class UnionFind:
	def __init__(self, n: int) -> None:
		self.parent = list(range(n))
		self.rank = [0] * n

	def find(self, i: int) -> int:
		while self.parent[i] != i:
			self.parent[i] = self.parent[self.parent[i]]
			i = self.parent[i]
		return i

	def union(self, a: int, b: int) -> None:
		ra = self.find(a)
		rb = self.find(b)
		if ra == rb:
			return
		if self.rank[ra] < self.rank[rb]:
			self.parent[ra] = rb
		elif self.rank[ra] > self.rank[rb]:
			self.parent[rb] = ra
		else:
			self.parent[rb] = ra
			self.rank[ra] += 1


@dataclass(frozen=True)
class Label:
	text: str
	x: float
	y: float


@dataclass
class Cluster:
	id: int
	entity_indices: list[int]
	minx: float
	miny: float
	maxx: float
	maxy: float

	@property
	def cx(self) -> float:
		return (self.minx + self.maxx) * 0.5

	@property
	def cy(self) -> float:
		return (self.miny + self.maxy) * 0.5

	@property
	def width(self) -> float:
		return self.maxx - self.minx

	@property
	def height(self) -> float:
		return self.maxy - self.miny


def sanitize_block_name(name: str, max_len: int = 80) -> str:
	# DXF block 名对字符集要求各实现不同；这里尽量保留中文，但替换常见非法字符
	n = name.strip()
	n = n.replace("\u3001", "_")  # 顿号
	n = re.sub(r"\s+", "_", n)
	n = re.sub(r'[<>/\\\\:;\"?*|=,]', "_", n)
	n = re.sub(r"_+", "_", n).strip("_")
	if not n:
		n = "SYM"
	if len(n) > max_len:
		n = n[:max_len]
	return n


def extract_labels(msp, doc: ezdxf.EzdxfDocument, label_layer: Optional[str], label_aci: int) -> list[Label]:
	labels: list[Label] = []
	for e in msp.query("TEXT"):
		if label_layer and e.dxf.layer != label_layer:
			continue
		if resolved_aci(e, doc) != label_aci:
			continue
		text = (e.dxf.text or "").strip()
		if not text or not HAN_RE.search(text):
			continue
		labels.append(Label(text=text, x=float(e.dxf.insert.x), y=float(e.dxf.insert.y)))
	return labels


def build_green_clusters(
	msp,
	doc: ezdxf.EzdxfDocument,
	labels: list[Label],
	green_aci: int,
	region_margin_x: float,
	region_margin_y: float,
	cluster_tol: float,
) -> tuple[list, list[tuple[float, float, float, float]], list[Cluster]]:
	# 仅在“图例区域”附近聚类，速度快且不容易把管线等大结构纳入
	lx = [l.x for l in labels]
	ly = [l.y for l in labels]
	minx, maxx = min(lx), max(lx)
	miny, maxy = min(ly), max(ly)

	rx0 = minx - region_margin_x
	rx1 = maxx + region_margin_x
	ry0 = miny - region_margin_y
	ry1 = maxy + region_margin_y

	entities = []
	boxes: list[tuple[float, float, float, float]] = []
	for e in msp:
		if resolved_aci(e, doc) != green_aci:
			continue
		b = bbox2d(e)
		if b is None:
			continue
		cx = (b[0] + b[2]) * 0.5
		cy = (b[1] + b[3]) * 0.5
		if not (rx0 <= cx <= rx1 and ry0 <= cy <= ry1):
			continue
		entities.append(e)
		boxes.append(b)

	uf = UnionFind(len(entities))
	for i in range(len(entities)):
		bi = boxes[i]
		for j in range(i + 1, len(entities)):
			if overlap(bi, boxes[j], tol=cluster_tol):
				uf.union(i, j)

	cluster_map: dict[int, list[int]] = defaultdict(list)
	for i in range(len(entities)):
		cluster_map[uf.find(i)].append(i)

	clusters: list[Cluster] = []
	for cid, idxs in cluster_map.items():
		minx2 = min(boxes[i][0] for i in idxs)
		miny2 = min(boxes[i][1] for i in idxs)
		maxx2 = max(boxes[i][2] for i in idxs)
		maxy2 = max(boxes[i][3] for i in idxs)
		clusters.append(Cluster(id=cid, entity_indices=idxs, minx=minx2, miny=miny2, maxx=maxx2, maxy=maxy2))

	return entities, boxes, clusters


def assign_labels_to_clusters(labels: list[Label], clusters: list[Cluster], y_expand: float = 30.0) -> dict[Label, Cluster]:
	# 贪心分配：按 y 从上到下，每个 label 选一个“未使用”的最优 cluster
	labels_sorted = sorted(labels, key=lambda l: l.y, reverse=True)
	available = {c.id: c for c in clusters}
	result: dict[Label, Cluster] = {}

	for label in labels_sorted:
		best: Optional[Cluster] = None
		best_score = float("inf")
		for c in available.values():
			if c.maxx <= label.x:
				continue
			if not (c.miny - y_expand <= label.y <= c.maxy + y_expand):
				continue
			dx = max(0.0, c.minx - label.x)
			dy = abs(c.cy - label.y)
			score = dx + dy * 5.0  # y 更重要，避免同一行左右符号误匹配
			if score < best_score:
				best_score = score
				best = c

		if best is None:
			continue
		result[label] = best
		available.pop(best.id, None)

	return result


def main() -> int:
	parser = argparse.ArgumentParser(description="从 DXF 图例中提取绿色符号并导出为 blocks")
	parser.add_argument("input", help="输入 DXF 路径")
	parser.add_argument("-o", "--output", required=True, help="输出 DXF 路径（包含 blocks）")
	parser.add_argument("--label-layer", default=None, help="图例文字所在图层（默认：若存在 V-TXT1 则使用，否则不过滤）")
	parser.add_argument("--label-aci", type=int, default=2, help="图例文字颜色（ACI，默认 2=黄）")
	parser.add_argument("--green-aci", type=int, default=3, help="目标符号颜色（ACI，默认 3=绿）")
	parser.add_argument("--region-margin-x", type=float, default=700.0, help="图例区域 X 方向扩展（用于限定搜索范围）")
	parser.add_argument("--region-margin-y", type=float, default=60.0, help="图例区域 Y 方向扩展（用于限定搜索范围）")
	parser.add_argument("--cluster-tol", type=float, default=1.0, help="聚类判定的外包框容差（越大越容易粘连）")
	parser.add_argument(
		"--base-point",
		choices=("center", "min"),
		default="center",
		help="生成 block 的基点（默认 center：适合后续旋转/缩放匹配；min：兼容旧逻辑）",
	)
	parser.add_argument("--preview-grid", action="store_true", help="在输出 DXF 的 ModelSpace 中按网格插入 blocks 便于查看")
	args = parser.parse_args()

	doc = ezdxf.readfile(args.input)
	msp = doc.modelspace()

	label_layer = args.label_layer
	if label_layer is None and "V-TXT1" in doc.layers:
		label_layer = "V-TXT1"

	labels = extract_labels(msp, doc, label_layer=label_layer, label_aci=args.label_aci)
	if not labels:
		raise SystemExit(f"未找到图例文字（layer={label_layer!r}, aci={args.label_aci}），请调整 --label-layer/--label-aci")

	entities, _, clusters = build_green_clusters(
		msp,
		doc,
		labels=labels,
		green_aci=args.green_aci,
		region_margin_x=args.region_margin_x,
		region_margin_y=args.region_margin_y,
		cluster_tol=args.cluster_tol,
	)

	assign = assign_labels_to_clusters(labels, clusters)
	if not assign:
		raise SystemExit("未能将任何图例文字匹配到绿色符号簇，请调大 --region-margin 或调整 --cluster-tol")

	out_doc = ezdxf.new(dxfversion=doc.dxfversion)
	out_msp = out_doc.modelspace()
	imp = Importer(doc, out_doc)

	used_names: dict[str, int] = {}

	grid_x = 0.0
	grid_y = 0.0
	row_h = 0.0
	grid_gap = 50.0
	max_row_w = 2000.0

	for label in sorted(assign.keys(), key=lambda l: l.y, reverse=True):
		cluster = assign[label]
		name = sanitize_block_name(label.text)
		if name in used_names:
			used_names[name] += 1
			name = f"{name}_{used_names[name]}"
		else:
			used_names[name] = 1

		blk = out_doc.blocks.new(name=name, base_point=(0, 0, 0))
		src_entities = [entities[i] for i in cluster.entity_indices]
		imp.import_entities(src_entities, blk)

		# 把符号平移到 block 基点（建议用 bbox center，便于后续旋转/缩放）
		if args.base_point == "center":
			base_x = (cluster.minx + cluster.maxx) * 0.5
			base_y = (cluster.miny + cluster.maxy) * 0.5
		else:
			base_x = cluster.minx
			base_y = cluster.miny

		dx = -base_x
		dy = -base_y
		for e in blk:
			try:
				e.translate(dx, dy, 0)  # type: ignore[attr-defined]
			except Exception:
				pass

		if args.preview_grid:
			# 网格排版预览
			if grid_x + cluster.width > max_row_w and grid_x > 0:
				grid_x = 0
				grid_y -= (row_h + grid_gap)
				row_h = 0

			out_msp.add_blockref(name, (grid_x, grid_y))
			out_msp.add_text(label.text, dxfattribs={"height": 10}).set_placement((grid_x, grid_y - 15))  # type: ignore[attr-defined]

			grid_x += (cluster.width + grid_gap)
			row_h = max(row_h, cluster.height)

	imp.finalize()
	out_doc.saveas(args.output)

	print(f"已导出 blocks：{len(assign)} 个，输出：{args.output}")
	return 0


if __name__ == "__main__":
	raise SystemExit(main())
