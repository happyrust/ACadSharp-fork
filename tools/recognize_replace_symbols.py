#!/usr/bin/env python3
# -*- coding: utf-8 -*-

from __future__ import annotations

import argparse
import json
import math
import re
from collections import defaultdict
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable, Optional

import ezdxf
from ezdxf.addons.importer import Importer

from symbol_lib import (
	SymbolDescriptor,
	bbox2d,
	bbox_of_entities,
	chamfer_best_rotation,
	compute_descriptor,
	decode_point_cloud,
	descriptor_distance,
	flatten_entities,
	load_index,
	merge_bbox,
	point_cloud_from_entities,
	resolved_aci,
)


def overlap(a: tuple[float, float, float, float], b: tuple[float, float, float, float], tol: float) -> bool:
	ax0, ay0, ax1, ay1 = a
	bx0, by0, bx1, by1 = b
	return not (ax1 + tol < bx0 or bx1 + tol < ax0 or ay1 + tol < by0 or by1 + tol < ay0)


def grid_cells_for_bbox(b: tuple[float, float, float, float], cell: float) -> Iterable[tuple[int, int]]:
	x0, y0, x1, y1 = b
	ix0 = int(x0 // cell)
	iy0 = int(y0 // cell)
	ix1 = int(x1 // cell)
	iy1 = int(y1 // cell)
	for ix in range(ix0, ix1 + 1):
		for iy in range(iy0, iy1 + 1):
			yield (ix, iy)


@dataclass
class Cluster:
	indices: list[int]
	bbox: tuple[float, float, float, float]


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


def cluster_entities(
	entities: list,
	boxes: list[tuple[float, float, float, float]],
	*,
	cell_size: float,
	tol: float,
) -> list[Cluster]:
	grid: dict[tuple[int, int], list[int]] = defaultdict(list)
	for i, b in enumerate(boxes):
		for key in grid_cells_for_bbox(b, cell_size):
			grid[key].append(i)

	visited = [False] * len(entities)
	clusters: list[Cluster] = []

	for i in range(len(entities)):
		if visited[i]:
			continue
		stack = [i]
		visited[i] = True
		idxs: list[int] = []
		bb_list: list[tuple[float, float, float, float]] = []
		while stack:
			cur = stack.pop()
			idxs.append(cur)
			bb = boxes[cur]
			bb_list.append(bb)

			# 找潜在邻居：查 bbox 覆盖的网格
			exp = (bb[0] - tol, bb[1] - tol, bb[2] + tol, bb[3] + tol)
			candidates: set[int] = set()
			for cell_key in grid_cells_for_bbox(exp, cell_size):
				candidates.update(grid.get(cell_key, []))
			for j in candidates:
				if visited[j]:
					continue
				if overlap(bb, boxes[j], tol):
					visited[j] = True
					stack.append(j)

		merged = merge_bbox(bb_list)
		if merged is None:
			continue
		clusters.append(Cluster(indices=idxs, bbox=merged))

	return clusters


def entity_key_points(entity) -> list[tuple[float, float]]:
	"""抽取用于“连通性聚类”的关键点（2D）。"""
	t = entity.dxftype()
	try:
		if t == "LINE":
			s = entity.dxf.start
			en = entity.dxf.end
			return [(float(s.x), float(s.y)), (float(en.x), float(en.y))]
		if t == "ARC":
			c = entity.dxf.center
			r = float(entity.dxf.radius)
			a0 = math.radians(float(entity.dxf.start_angle))
			a1 = math.radians(float(entity.dxf.end_angle))
			return [
				(float(c.x) + r * math.cos(a0), float(c.y) + r * math.sin(a0)),
				(float(c.x) + r * math.cos(a1), float(c.y) + r * math.sin(a1)),
			]
		if t == "CIRCLE":
			c = entity.dxf.center
			r = float(entity.dxf.radius)
			return [
				(float(c.x) + r, float(c.y)),
				(float(c.x) - r, float(c.y)),
				(float(c.x), float(c.y) + r),
				(float(c.x), float(c.y) - r),
			]
		if t == "POINT":
			p = entity.dxf.location
			return [(float(p.x), float(p.y))]
		if t == "INSERT":
			p = entity.dxf.insert
			return [(float(p.x), float(p.y))]
		if t == "LWPOLYLINE":
			pts = list(entity.get_points("xy"))
			return [(float(x), float(y)) for x, y in pts]
		if t == "POLYLINE":
			pts = [v.dxf.location for v in entity.vertices]  # type: ignore[attr-defined]
			return [(float(p.x), float(p.y)) for p in pts]
	except Exception:
		return []
	return []


def cluster_entities_connectivity(
	entities: list,
	boxes: list[tuple[float, float, float, float]],
	*,
	tol: float,
	cell_size: Optional[float] = None,
) -> list[Cluster]:
	"""
按“线段/顶点连通性”聚类：
- 抽取每个实体的关键点（端点/顶点等）
- 若两实体存在一对关键点距离 <= tol，则视为连通
"""
	if not entities:
		return []
	tol = float(tol)
	if not math.isfinite(tol) or tol <= 0:
		tol = 1.0

	cell = float(cell_size) if cell_size is not None else tol * 4.0
	if not math.isfinite(cell) or cell <= 1e-9:
		cell = tol * 4.0

	def key(x: float, y: float) -> tuple[int, int]:
		return (int(math.floor(x / cell)), int(math.floor(y / cell)))

	grid: dict[tuple[int, int], list[tuple[int, float, float]]] = defaultdict(list)
	uf = UnionFind(len(entities))

	tol2 = tol * tol
	for i, e in enumerate(entities):
		pts = entity_key_points(e)
		for x, y in pts:
			k = key(x, y)
			# 查邻域 3x3 cells 的点
			for dx in (-1, 0, 1):
				for dy in (-1, 0, 1):
					for j, ox, oy in grid.get((k[0] + dx, k[1] + dy), []):
						if i == j:
							continue
						if (x - ox) * (x - ox) + (y - oy) * (y - oy) <= tol2:
							uf.union(i, j)
			grid[k].append((i, x, y))

	groups: dict[int, list[int]] = defaultdict(list)
	for i in range(len(entities)):
		groups[uf.find(i)].append(i)

	clusters: list[Cluster] = []
	for _, idxs in groups.items():
		merged = merge_bbox(boxes[i] for i in idxs)
		if merged is None:
			continue
		clusters.append(Cluster(indices=idxs, bbox=merged))
	return clusters


def best_match(desc: SymbolDescriptor, library: list[tuple[str, SymbolDescriptor]]) -> tuple[Optional[str], float]:
	best_name: Optional[str] = None
	best_score = float("inf")
	for name, lib_desc in library:
		s = descriptor_distance(desc, lib_desc)
		if s < best_score:
			best_score = s
			best_name = name
	return best_name, best_score


def linear_length(entity) -> float:
	"""用于“管线过滤”的粗略长度估计（只覆盖常见线性实体）。"""
	t = entity.dxftype()
	try:
		if t == "LINE":
			s = entity.dxf.start
			en = entity.dxf.end
			return float(math.hypot(float(en.x - s.x), float(en.y - s.y)))
		if t == "LWPOLYLINE":
			pts = list(entity.get_points("xy"))
			if len(pts) < 2:
				return 0.0
			total = 0.0
			for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
				total += float(math.hypot(float(x1 - x0), float(y1 - y0)))
			if bool(getattr(entity, "closed", False)):
				x0, y0 = pts[-1]
				x1, y1 = pts[0]
				total += float(math.hypot(float(x1 - x0), float(y1 - y0)))
			return total
		if t == "POLYLINE":
			vs = [v.dxf.location for v in entity.vertices]  # type: ignore[attr-defined]
			if len(vs) < 2:
				return 0.0
			pts = [(float(p.x), float(p.y)) for p in vs]
			total = 0.0
			for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
				total += float(math.hypot(x1 - x0, y1 - y0))
			if bool(getattr(entity, "is_closed", False)):
				total += float(math.hypot(pts[0][0] - pts[-1][0], pts[0][1] - pts[-1][1]))
			return total
	except Exception:
		return 0.0
	return 0.0


def ensure_linetype(doc: ezdxf.EzdxfDocument, name: str) -> str:
	try:
		_ = doc.linetypes.get(name)
		return name
	except Exception:
		pass

	# 尝试创建一个简单虚线
	try:
		doc.linetypes.new(
			name,
			dxfattribs={
				"description": "Dashed",
				"pattern": [0.6, 0.3, -0.3],
			},
		)
		return name
	except Exception:
		return "CONTINUOUS"


def ensure_layer(doc: ezdxf.EzdxfDocument, name: str, *, color: int, linetype: str) -> str:
	try:
		_ = doc.layers.get(name)
		return name
	except Exception:
		pass
	try:
		doc.layers.new(name, dxfattribs={"color": int(color), "linetype": str(linetype)})
	except Exception:
		# 失败就用 0 层
		return "0"
	return name


def draw_bbox(
	msp,
	bbox: tuple[float, float, float, float],
	*,
	layer: str,
	color: int,
	linetype: str,
	ltscale: float,
) -> None:
	x0, y0, x1, y1 = bbox
	attribs = {"layer": layer, "color": int(color), "linetype": str(linetype), "ltscale": float(ltscale)}
	try:
		msp.add_line((x0, y0), (x1, y0), dxfattribs=attribs)
		msp.add_line((x1, y0), (x1, y1), dxfattribs=attribs)
		msp.add_line((x1, y1), (x0, y1), dxfattribs=attribs)
		msp.add_line((x0, y1), (x0, y0), dxfattribs=attribs)
	except Exception:
		return


def compile_regex_list(patterns: list[str]) -> list["re.Pattern[str]"]:
	out: list["re.Pattern[str]"] = []
	for p in patterns:
		p = str(p or "").strip()
		if not p:
			continue
		out.append(re.compile(p))
	return out


def name_allowed(
	name: str,
	*,
	include_names: set[str],
	include_regex: list["re.Pattern[str]"],
	exclude_names: set[str],
	exclude_regex: list["re.Pattern[str]"],
) -> bool:
	name = str(name)
	has_includes = bool(include_names) or bool(include_regex)
	if has_includes:
		ok = False
		if name in include_names:
			ok = True
		elif include_regex and any(rx.search(name) for rx in include_regex):
			ok = True
		if not ok:
			return False

	if name in exclude_names:
		return False
	if exclude_regex and any(rx.search(name) for rx in exclude_regex):
		return False
	return True


def main() -> int:
	parser = argparse.ArgumentParser(description="用符号库识别 DXF 中的绿色符号并替换为 blockref")
	parser.add_argument("input_dxf", help="输入 DXF")
	parser.add_argument("--lib-dir", required=True, help="符号库目录（包含 blocks.dxf 与 index.json）")
	parser.add_argument("-o", "--output", required=True, help="输出 DXF")
	parser.add_argument("--green-aci", type=int, default=3, help="目标颜色（ACI，默认 3=绿）")
	parser.add_argument(
		"--filter-pipes",
		action=argparse.BooleanOptionalAction,
		default=True,
		help="先过滤疑似管线（避免符号与长管线粘连导致聚类过大）；可用 --no-filter-pipes 关闭",
	)
	parser.add_argument("--pipe-threshold", type=float, default=None, help="直接指定管线长度阈值（优先生效；不走自动阈值）")
	parser.add_argument("--pipe-min-length", type=float, default=0.0, help="管线最小长度阈值（绝对值，下限）")
	parser.add_argument("--pipe-median-factor", type=float, default=25.0, help="管线阈值 = 中位长度 * factor 的下限")
	parser.add_argument("--pipe-length-quantile", type=float, default=0.995, help="管线阈值使用的长度分位数（例如 0.995）")
	parser.add_argument(
		"--cluster-method",
		choices=("bbox", "connect"),
		default="bbox",
		help="分割/聚类方法：bbox=外包框相交（更快），connect=线段连通性（更适合调试分割）",
	)
	parser.add_argument("--connect-tol", type=float, default=None, help="连通性聚类的点距阈值（默认使用 --cluster-tol）")
	parser.add_argument("--connect-cell-size", type=float, default=None, help="连通性聚类的网格尺寸（默认 tol*4）")
	parser.add_argument("--cluster-tol", type=float, default=1.0, help="聚类 bbox 相交容差")
	parser.add_argument("--cell-size", type=float, default=50.0, help="空间网格尺寸（越大越快但可能粘连）")
	parser.add_argument("--max-entities", type=int, default=200, help="单簇最大实体数（超过视为管线/大结构跳过）")
	parser.add_argument("--max-size", type=float, default=500.0, help="单簇最大尺寸（bbox 边长阈值）")
	parser.add_argument("--score-threshold", type=float, default=120.0, help="粗匹配阈值（descriptor_distance，越小越严格）")
	parser.add_argument("--coarse-topk", type=int, default=5, help="粗匹配保留 TopK 后再做精匹配")
	parser.add_argument("--sample-div", type=float, default=30.0, help="点云采样密度：max_dim / sample_div 作为步长")
	parser.add_argument("--max-points", type=int, default=600, help="点云最大点数（下采样）")
	parser.add_argument("--chamfer-threshold", type=float, default=0.06, help="精匹配阈值（Chamfer 距离，越小越严格）")
	parser.add_argument(
		"--include-symbol",
		action="append",
		default=[],
		help="仅识别指定符号名（可多次传入）；若设置了 include，则只在 include 集合中匹配",
	)
	parser.add_argument("--include-regex", action="append", default=[], help="仅识别匹配正则的符号名（可多次传入）")
	parser.add_argument("--exclude-symbol", action="append", default=[], help="排除指定符号名（可多次传入）")
	parser.add_argument("--exclude-regex", action="append", default=[], help="排除匹配正则的符号名（可多次传入）")
	parser.add_argument(
		"--replace",
		action=argparse.BooleanOptionalAction,
		default=True,
		help="是否将识别到的符号替换为 blockref；用 --no-replace 仅标注 bbox/输出报告",
	)
	parser.add_argument(
		"--draw-bbox",
		action=argparse.BooleanOptionalAction,
		default=True,
		help="是否在输出 DXF 中绘制识别结果的 BBOX（红色虚线）",
	)
	parser.add_argument("--bbox-layer", default="AI_BBOX", help="BBOX 所在图层名")
	parser.add_argument("--bbox-color", type=int, default=1, help="BBOX 颜色（ACI，默认 1=红）")
	parser.add_argument("--bbox-linetype", default="DASHED", help="BBOX 线型（默认 DASHED）")
	parser.add_argument("--bbox-ltscale", type=float, default=1.0, help="BBOX 线型比例（LTSCALE，默认 1.0）")
	parser.add_argument(
		"--draw-seg-bbox",
		action=argparse.BooleanOptionalAction,
		default=False,
		help="绘制“分割阶段”所有簇的 BBOX（中间调试输出）",
	)
	parser.add_argument(
		"--seg-debug-dxf",
		default=None,
		help="可选：输出一个“仅包含分割后待识别图元”的 DXF（用于降噪调试），例如 out/seg_only.dxf",
	)
	parser.add_argument(
		"--seg-debug-keep",
		choices=("eligible", "all"),
		default="eligible",
		help="seg-debug-dxf 保留的簇：eligible=仅保留满足 max_size/max_entities 的簇（默认更降噪），all=保留全部簇",
	)
	parser.add_argument("--seg-bbox-layer", default="AI_SEG_BBOX", help="分割阶段 BBOX 图层名")
	parser.add_argument("--seg-bbox-color", type=int, default=4, help="分割阶段 BBOX 颜色（ACI，默认 4=青）")
	parser.add_argument("--seg-bbox-linetype", default="DASHED", help="分割阶段 BBOX 线型（默认 DASHED）")
	parser.add_argument("--seg-bbox-ltscale", type=float, default=1.0, help="分割阶段 BBOX 线型比例（默认 1.0）")
	parser.add_argument(
		"--seg-label",
		action=argparse.BooleanOptionalAction,
		default=False,
		help="在分割阶段 BBOX 旁标注簇编号/大小（调试用）",
	)
	parser.add_argument("--seg-label-height", type=float, default=2.5, help="分割阶段标签文字高度")
	parser.add_argument("--report-json", default=None, help="可选：输出识别/替换报告 JSON")
	args = parser.parse_args()

	lib_dir = Path(args.lib_dir).expanduser().resolve()
	lib_blocks = lib_dir / "blocks.dxf"
	lib_index = lib_dir / "index.json"
	if not lib_blocks.exists() or not lib_index.exists():
		raise SystemExit(f"符号库目录不完整：需要 {lib_blocks} 和 {lib_index}")

	index = load_index(lib_index)
	lib_doc = ezdxf.readfile(str(lib_blocks))
	base_point_mode = str(index.get("base_point") or "center")

	include_names = {str(x).strip() for x in (args.include_symbol or []) if str(x).strip()}
	exclude_names = {str(x).strip() for x in (args.exclude_symbol or []) if str(x).strip()}
	include_regex = compile_regex_list(list(args.include_regex or []))
	exclude_regex = compile_regex_list(list(args.exclude_regex or []))

	library: list[tuple[str, SymbolDescriptor]] = []
	symbol_meta: dict[str, dict] = {}
	for sym in index.get("symbols", []):
		block_name = sym.get("block_name") or sym.get("name")
		if not name_allowed(
			str(block_name),
			include_names=include_names,
			include_regex=include_regex,
			exclude_names=exclude_names,
			exclude_regex=exclude_regex,
		):
			continue
		desc = SymbolDescriptor.from_dict(sym.get("descriptor") or {})
		library.append((block_name, desc))
		symbol_meta[str(block_name)] = dict(sym)

	if not library:
		raise SystemExit("符号库为空（index.json 未包含 symbols）")

	# 预计算模板点云（精匹配）
	template_points: dict[str, dict] = {}
	for block_name, desc in library:
		meta = symbol_meta.get(block_name) or {}

		# 优先复用符号库中存好的点云（更快，也便于“入库后复用”）
		if meta.get("point_cloud"):
			pts = decode_point_cloud(meta.get("point_cloud") or {})
			bb_list = meta.get("bbox") or None
			if isinstance(bb_list, list) and len(bb_list) == 4:
				bb = (float(bb_list[0]), float(bb_list[1]), float(bb_list[2]), float(bb_list[3]))
			else:
				bb = None
			norm = float(meta.get("norm") or (max(bb[2] - bb[0], bb[3] - bb[1]) if bb else 1.0) or 1.0)
		else:
			try:
				blk = lib_doc.blocks.get(block_name)
			except Exception:
				continue
			flat = flatten_entities(list(blk), lib_doc, max_depth=2)
			bb = bbox_of_entities(flat)
			if bb is None:
				continue
			pts = point_cloud_from_entities(flat, bb, sample_div=args.sample_div, max_points=args.max_points)
			norm = float(max(bb[2] - bb[0], bb[3] - bb[1]) or 1.0)

		if bb is None:
			# 没有 bbox 则无法换算比例
			continue
		template_points[block_name] = {
			"desc": desc,
			"bbox": bb,
			"norm": norm,
			"points": pts,
		}

	doc = ezdxf.readfile(args.input_dxf)
	msp = doc.modelspace()

	# 选取候选实体
	cand_entities: list = []
	cand_boxes: list[tuple[float, float, float, float]] = []
	cand_lengths: list[float] = []
	for e in msp:
		if e.dxftype() in ("TEXT", "MTEXT"):
			continue
		if resolved_aci(e, doc) != args.green_aci:
			continue
		b = bbox2d(e)
		if b is None:
			continue
		cand_entities.append(e)
		cand_boxes.append(b)
		cand_lengths.append(linear_length(e))

	print(f"候选绿色实体数：{len(cand_entities)}")

	# 先过滤“疑似管线”：把特别长的线性实体从聚类候选里剔除（但不会删除它们）
	pipe_filter_info: dict = {"enabled": bool(args.filter_pipes)}
	if args.filter_pipes:
		lengths = [l for l in cand_lengths if l > 1e-9]
		if lengths:
			import numpy as np

			arr = np.asarray(lengths, dtype=float)
			med = float(np.median(arr))
			q = float(np.quantile(arr, float(args.pipe_length_quantile)))
			auto_threshold = max(float(args.pipe_min_length), med * float(args.pipe_median_factor), q)
			threshold = float(args.pipe_threshold) if args.pipe_threshold is not None else float(auto_threshold)
			pipe_filter_info.update(
				{
					"median": med,
					"quantile": float(args.pipe_length_quantile),
					"q_value": q,
					"auto_threshold": float(auto_threshold),
					"threshold": float(threshold),
				}
			)

			keep_entities: list = []
			keep_boxes: list[tuple[float, float, float, float]] = []
			keep_lengths: list[float] = []
			removed = 0
			for e, b, ln in zip(cand_entities, cand_boxes, cand_lengths):
				if ln > 1e-9 and ln >= threshold and e.dxftype() in ("LINE", "LWPOLYLINE", "POLYLINE"):
					removed += 1
					continue
				keep_entities.append(e)
				keep_boxes.append(b)
				keep_lengths.append(ln)
			cand_entities, cand_boxes, cand_lengths = keep_entities, keep_boxes, keep_lengths
			pipe_filter_info["removed"] = removed
			print(f"管线过滤：阈值≈{threshold:.3f}，剔除 {removed} 条线性实体，剩余 {len(cand_entities)}")
		else:
			pipe_filter_info["note"] = "未找到线性实体长度样本"

	if args.cluster_method == "connect":
		tol = float(args.connect_tol) if args.connect_tol is not None else float(args.cluster_tol)
		clusters = cluster_entities_connectivity(
			cand_entities,
			cand_boxes,
			tol=tol,
			cell_size=(float(args.connect_cell_size) if args.connect_cell_size is not None else None),
		)
	else:
		clusters = cluster_entities(cand_entities, cand_boxes, cell_size=args.cell_size, tol=args.cluster_tol)

	print(f"聚类数量：{len(clusters)}（method={args.cluster_method}）")

	# 仅保留“可识别”的簇（避免大结构干扰）
	eligible_clusters: list[Cluster] = []
	for cl in clusters:
		if len(cl.indices) > args.max_entities:
			continue
		bb = cl.bbox
		size = max(bb[2] - bb[0], bb[3] - bb[1])
		if size > args.max_size:
			continue
		eligible_clusters.append(cl)

	# 中间调试：导出“仅包含待识别图元”的 DXF（更干净，便于检查分割效果）
	seg_doc: Optional[ezdxf.EzdxfDocument] = None
	seg_msp = None
	if args.seg_debug_dxf:
		seg_doc = ezdxf.new(dxfversion=doc.dxfversion)
		seg_msp = seg_doc.modelspace()
		keep_clusters = clusters if args.seg_debug_keep == "all" else eligible_clusters
		keep_idxs = sorted({i for cl in keep_clusters for i in cl.indices})
		keep_entities = [cand_entities[i] for i in keep_idxs]
		imp_seg = Importer(doc, seg_doc)
		imp_seg.import_entities(keep_entities, seg_msp)
		imp_seg.finalize()

		# 在 seg_doc 上画分割 bbox（默认画，便于调试）
		lt = ensure_linetype(seg_doc, str(args.seg_bbox_linetype))
		ensure_layer(seg_doc, str(args.seg_bbox_layer), color=int(args.seg_bbox_color), linetype=lt)
		for idx, cl in enumerate(keep_clusters, start=1):
			draw_bbox(
				seg_msp,
				cl.bbox,
				layer=str(args.seg_bbox_layer),
				color=int(args.seg_bbox_color),
				linetype=lt,
				ltscale=float(args.seg_bbox_ltscale),
			)
			if args.seg_label:
				try:
					x0, y0, x1, y1 = cl.bbox
					text = f"C{idx}:{len(cl.indices)}"
					t = seg_msp.add_text(
						text,
						dxfattribs={
							"height": float(args.seg_label_height),
							"layer": str(args.seg_bbox_layer),
							"color": int(args.seg_bbox_color),
						},
					)
					try:
						t.dxf.insert = (float(x0), float(y1), 0.0)
					except Exception:
						pass
				except Exception:
					pass

	importer = Importer(lib_doc, doc) if args.replace else None
	imported_map: dict[str, str] = {}  # 仅在 replace 时使用

	if args.draw_seg_bbox:
		lt = ensure_linetype(doc, str(args.seg_bbox_linetype))
		ensure_layer(doc, str(args.seg_bbox_layer), color=int(args.seg_bbox_color), linetype=lt)

	if args.draw_bbox:
		lt = ensure_linetype(doc, str(args.bbox_linetype))
		ensure_layer(doc, str(args.bbox_layer), color=int(args.bbox_color), linetype=lt)

	matched = 0
	replaced = 0
	seg_clusters_report = []
	if args.draw_seg_bbox or args.report_json:
		for idx, cl in enumerate(clusters, start=1):
			bb = cl.bbox
			seg_clusters_report.append(
				{
					"id": idx,
					"bbox": [bb[0], bb[1], bb[2], bb[3]],
					"entity_count": len(cl.indices),
					"eligible": bool(cl in eligible_clusters),
				}
			)

			if args.draw_seg_bbox:
				lt = ensure_linetype(doc, str(args.seg_bbox_linetype))
				draw_bbox(
					msp,
					bb,
					layer=str(args.seg_bbox_layer),
					color=int(args.seg_bbox_color),
					linetype=lt,
					ltscale=float(args.seg_bbox_ltscale),
				)
				if args.seg_label:
					try:
						x0, y0, x1, y1 = bb
						text = f"C{idx}:{len(cl.indices)}"
						t = msp.add_text(
							text,
							dxfattribs={
								"height": float(args.seg_label_height),
								"layer": str(args.seg_bbox_layer),
								"color": int(args.seg_bbox_color),
							},
						)
						try:
							t.dxf.insert = (float(x0), float(y1), 0.0)
						except Exception:
							pass
					except Exception:
						pass

	report: dict = {
		"input": str(Path(args.input_dxf).expanduser().resolve()),
		"output": str(Path(args.output).expanduser().resolve()),
		"lib_dir": str(lib_dir),
		"base_point": base_point_mode,
		"symbol_filter": {
			"include_symbol": sorted(include_names),
			"include_regex": list(args.include_regex or []),
			"exclude_symbol": sorted(exclude_names),
			"exclude_regex": list(args.exclude_regex or []),
		},
		"pipe_filter": pipe_filter_info,
		"segmentation": {
			"method": str(args.cluster_method),
			"cluster_tol": float(args.cluster_tol),
			"connect_tol": float(args.connect_tol) if args.connect_tol is not None else None,
			"connect_cell_size": float(args.connect_cell_size) if args.connect_cell_size is not None else None,
			"eligible_clusters_total": len(eligible_clusters),
			"seg_debug_dxf": str(Path(args.seg_debug_dxf).expanduser().resolve()) if args.seg_debug_dxf else None,
			"seg_debug_keep": str(args.seg_debug_keep),
			"clusters": seg_clusters_report,
		},
		"clusters_total": len(clusters),
		"replacements": [],
	}
	for cl in eligible_clusters:
		if len(cl.indices) > args.max_entities:
			continue

		ents = [cand_entities[i] for i in cl.indices]
		flat = flatten_entities(ents, doc, max_depth=2)
		bb = bbox_of_entities(flat)
		if bb is None:
			continue
		minx, miny, maxx, maxy = bb
		size = max(maxx - minx, maxy - miny)
		if size > args.max_size:
			continue

		desc = compute_descriptor(flat, bb)

		# 粗匹配：descriptor_distance TopK
		candidates = []
		for name, lib_desc in library:
			s = descriptor_distance(desc, lib_desc)
			if s <= args.score_threshold:
				candidates.append((s, name))
		if not candidates:
			continue
		candidates.sort(key=lambda x: x[0])
		candidates = candidates[: max(1, args.coarse_topk)]

		# 精匹配：Chamfer Distance + 旋转
		cand_pts = point_cloud_from_entities(flat, bb, sample_div=args.sample_div, max_points=args.max_points)
		best_name: Optional[str] = None
		best_coarse = float("inf")
		best_chamfer = float("inf")
		best_angle = 0.0
		for coarse_score, name in candidates:
			tpl = template_points.get(name)
			if tpl is None:
				continue
			ch_score, angle = chamfer_best_rotation(tpl["points"], cand_pts)
			if ch_score < best_chamfer:
				best_chamfer = ch_score
				best_name = name
				best_coarse = coarse_score
				best_angle = angle

		if best_name is None or best_chamfer > args.chamfer_threshold:
			continue

		matched += 1

		cx = (minx + maxx) * 0.5
		cy = (miny + maxy) * 0.5

		if base_point_mode == "center":
			insert_point = (cx, cy)
			tpl_norm = float(template_points[best_name]["norm"]) if best_name in template_points else 1.0
			scale = float(size / tpl_norm) if tpl_norm > 1e-9 else 1.0
			rotation = float(best_angle)
		else:
			# 旧库（min 基点）不建议旋转/缩放，避免插入点偏移
			insert_point = (minx, miny)
			scale = 1.0
			rotation = 0.0

		# 尽量保持原层
		try:
			from collections import Counter

			layer = Counter(getattr(e.dxf, "layer", "0") for e in ents).most_common(1)[0][0]
		except Exception:
			layer = "0"

		if args.replace and importer is not None:
			# 导入 block（避免名称冲突）
			if best_name in imported_map:
				target_block = imported_map[best_name]
			else:
				target_block = importer.import_block(best_name, rename=True)
				imported_map[best_name] = target_block

			# 删除原始实体并插入 blockref
			for e in ents:
				try:
					msp.delete_entity(e)
				except Exception:
					pass

			msp.add_blockref(
				target_block,
				insert_point,
				dxfattribs={
					"layer": layer,
					"xscale": scale,
					"yscale": scale,
					"rotation": rotation,
				},
			)
			replaced += 1

		if args.draw_bbox:
			lt = str(args.bbox_linetype)
			try:
				lt = ensure_linetype(doc, lt)
			except Exception:
				lt = "CONTINUOUS"
			draw_bbox(
				msp,
				(minx, miny, maxx, maxy),
				layer=str(args.bbox_layer),
				color=int(args.bbox_color),
				linetype=lt,
				ltscale=float(args.bbox_ltscale),
			)
			# 同步在 seg_doc 上画识别 bbox（如果启用）
			if seg_doc is not None and seg_msp is not None:
				try:
					lt2 = ensure_linetype(seg_doc, lt)
				except Exception:
					lt2 = "CONTINUOUS"
				ensure_layer(seg_doc, str(args.bbox_layer), color=int(args.bbox_color), linetype=lt2)
				draw_bbox(
					seg_msp,
					(minx, miny, maxx, maxy),
					layer=str(args.bbox_layer),
					color=int(args.bbox_color),
					linetype=lt2,
					ltscale=float(args.bbox_ltscale),
				)

		similarity = max(0.0, 1.0 - float(best_chamfer) / float(args.chamfer_threshold))

		report["replacements"].append(
			{
				"symbol": best_name,
				"coarse_score": best_coarse,
				"chamfer": best_chamfer,
				"similarity": similarity,
				"angle_deg": best_angle,
				"scale": scale,
				"insert_point": [insert_point[0], insert_point[1]],
				"bbox": [minx, miny, maxx, maxy],
				"entity_count": len(ents),
				"flat_entity_count": len(flat),
			}
		)

	if importer is not None:
		importer.finalize()
	out_path = Path(args.output).expanduser().resolve()
	out_path.parent.mkdir(parents=True, exist_ok=True)
	doc.saveas(str(out_path))

	if seg_doc is not None and args.seg_debug_dxf:
		seg_path = Path(args.seg_debug_dxf).expanduser().resolve()
		seg_path.parent.mkdir(parents=True, exist_ok=True)
		seg_doc.saveas(str(seg_path))

	if args.report_json:
		report_path = Path(args.report_json).expanduser().resolve()
		report_path.parent.mkdir(parents=True, exist_ok=True)
		with open(report_path, "w", encoding="utf-8") as f:
			json.dump(report, f, ensure_ascii=False, indent=2)

	# 列出相似度（按 similarity 降序）
	if report["replacements"]:
		items = sorted(report["replacements"], key=lambda r: float(r.get("similarity") or 0.0), reverse=True)
		print("识别结果（按相似度降序）：")
		for r in items:
			print(
				f"- {r.get('symbol')}  similarity={float(r.get('similarity') or 0.0):.3f}  "
				f"chamfer={float(r.get('chamfer') or 0.0):.4f}  coarse={float(r.get('coarse_score') or 0.0):.1f}"
			)

	if seg_doc is not None and args.seg_debug_dxf:
		print(f"分割调试 DXF：{Path(args.seg_debug_dxf).expanduser().resolve()}")
	print(f"识别到符号簇：{matched} 个；已替换：{replaced} 个；输出：{out_path}")
	return 0


if __name__ == "__main__":
	raise SystemExit(main())
