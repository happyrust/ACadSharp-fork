#!/usr/bin/env python3
# -*- coding: utf-8 -*-

from __future__ import annotations

import base64
import json
import math
import re
import zlib
from collections import Counter
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable, Optional

import ezdxf

HAN_RE = re.compile(r"[\u4e00-\u9fff]")


def resolved_aci(entity, doc: ezdxf.EzdxfDocument, fallback: int = 7) -> int:
	"""解析实体的最终 ACI 颜色（简化版：处理 BYLAYER/BYBLOCK）。"""
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
	"""计算 2D 外包框（覆盖图例常见实体类型）。返回 (minx,miny,maxx,maxy)。"""
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
		if t == "ARC":
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


def merge_bbox(bboxes: Iterable[tuple[float, float, float, float]]) -> Optional[tuple[float, float, float, float]]:
	it = iter(bboxes)
	try:
		minx, miny, maxx, maxy = next(it)
	except StopIteration:
		return None
	for bx0, by0, bx1, by1 in it:
		minx = min(minx, bx0)
		miny = min(miny, by0)
		maxx = max(maxx, bx1)
		maxy = max(maxy, by1)
	return (minx, miny, maxx, maxy)


def quantize(value: float, step: float) -> int:
	if step <= 0:
		raise ValueError("step must be > 0")
	return int(round(value / step))


def normalize_angle_pi(angle: float) -> float:
	"""将角度归一到 [0, π)。"""
	angle = angle % math.pi
	if angle < 0:
		angle += math.pi
	return angle


def sanitize_name(name: str, max_len: int = 80) -> str:
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


@dataclass(frozen=True)
class SymbolDescriptor:
	# 计数特征
	counts: dict[str, int]
	# 几何特征（量化后的整数数组）
	lengths_q: list[int]
	angles_q: list[int]
	radii_q: list[int]
	# 归一化尺寸（量化）
	size_q: tuple[int, int]

	def to_dict(self) -> dict[str, Any]:
		return {
			"counts": self.counts,
			"lengths_q": self.lengths_q,
			"angles_q": self.angles_q,
			"radii_q": self.radii_q,
			"size_q": list(self.size_q),
		}

	@staticmethod
	def from_dict(d: dict[str, Any]) -> "SymbolDescriptor":
		return SymbolDescriptor(
			counts={str(k): int(v) for k, v in (d.get("counts") or {}).items()},
			lengths_q=[int(x) for x in (d.get("lengths_q") or [])],
			angles_q=[int(x) for x in (d.get("angles_q") or [])],
			radii_q=[int(x) for x in (d.get("radii_q") or [])],
			size_q=(int((d.get("size_q") or [0, 0])[0]), int((d.get("size_q") or [0, 0])[1])),
		)


def compute_descriptor(
	entities: Iterable,
	bbox: tuple[float, float, float, float],
	*,
	length_step: float = 0.01,
	radius_step: float = 0.01,
	angle_step_deg: float = 5.0,
) -> SymbolDescriptor:
	minx, miny, maxx, maxy = bbox
	width = maxx - minx
	height = maxy - miny
	norm = max(width, height)
	if not math.isfinite(norm) or norm <= 0:
		norm = 1.0

	counts: Counter[str] = Counter()
	lengths: list[tuple[float, float]] = []  # (len, angle)
	radii: list[float] = []

	# 收集原始特征
	for e in entities:
		t = e.dxftype()
		counts[t] += 1

		if t == "LINE":
			s = e.dxf.start
			en = e.dxf.end
			dx = float(en.x - s.x)
			dy = float(en.y - s.y)
			ln = math.hypot(dx, dy)
			if ln <= 1e-9:
				continue
			ang = normalize_angle_pi(math.atan2(dy, dx))
			lengths.append((ln / norm, ang))
		elif t == "CIRCLE":
			r = float(e.dxf.radius)
			if r > 0:
				radii.append(r / norm)
		elif t == "ARC":
			r = float(e.dxf.radius)
			if r > 0:
				radii.append(r / norm)
		elif t in ("LWPOLYLINE", "POLYLINE"):
			try:
				if t == "LWPOLYLINE":
					pts = list(e.get_points("xy"))
					pts = [(float(p[0]), float(p[1])) for p in pts]
					is_closed = bool(getattr(e, "closed", False))
				else:
					vs = [v.dxf.location for v in e.vertices]  # type: ignore[attr-defined]
					pts = [(float(p.x), float(p.y)) for p in vs]
					is_closed = bool(getattr(e, "is_closed", False))
			except Exception:
				continue

			if len(pts) < 2:
				continue
			seq = pts + ([pts[0]] if is_closed else [])
			for (x0, y0), (x1, y1) in zip(seq, seq[1:]):
				dx = x1 - x0
				dy = y1 - y0
				ln = math.hypot(dx, dy)
				if ln <= 1e-9:
					continue
				ang = normalize_angle_pi(math.atan2(dy, dx))
				lengths.append((ln / norm, ang))

	# 旋转归一：用“最长线段”的方向作为参考
	ref_angle = 0.0
	if lengths:
		ref_angle = max(lengths, key=lambda x: x[0])[1]

	angle_step = math.radians(angle_step_deg)
	lengths_q: list[int] = []
	angles_q: list[int] = []
	for ln, ang in lengths:
		lengths_q.append(quantize(ln, length_step))
		angles_q.append(quantize(normalize_angle_pi(ang - ref_angle), angle_step))

	radii_q = [quantize(r, radius_step) for r in radii]

	lengths_q.sort()
	angles_q.sort()
	radii_q.sort()

	size_q = (quantize(width / norm, 0.01), quantize(height / norm, 0.01))

	return SymbolDescriptor(
		counts=dict(counts),
		lengths_q=lengths_q,
		angles_q=angles_q,
		radii_q=radii_q,
		size_q=size_q,
	)


def descriptor_distance(a: SymbolDescriptor, b: SymbolDescriptor) -> float:
	# 计数作为硬约束的软惩罚：差 1 个就很大
	score = 0.0
	for k in ("LINE", "CIRCLE", "ARC", "LWPOLYLINE", "POLYLINE", "INSERT"):
		da = a.counts.get(k, 0)
		db = b.counts.get(k, 0)
		score += abs(da - db) * 50.0

	def list_dist(x: list[int], y: list[int], w: float) -> float:
		if not x and not y:
			return 0.0
		if not x or not y:
			return w * 200.0 + abs(len(x) - len(y)) * w * 10.0
		n = min(len(x), len(y))
		d = sum(abs(x[i] - y[i]) for i in range(n))
		d += abs(len(x) - len(y)) * 20
		return d * w

	score += list_dist(a.lengths_q, b.lengths_q, 1.0)
	score += list_dist(a.radii_q, b.radii_q, 2.0)
	score += list_dist(a.angles_q, b.angles_q, 0.5)

	# 尺寸差异（归一后仍可用于区分）
	score += (abs(a.size_q[0] - b.size_q[0]) + abs(a.size_q[1] - b.size_q[1])) * 5.0
	return score


def load_index(path: str | Path) -> dict[str, Any]:
	with open(path, "r", encoding="utf-8") as f:
		return json.load(f)


def save_index(path: str | Path, data: dict[str, Any]) -> None:
	with open(path, "w", encoding="utf-8") as f:
		json.dump(data, f, ensure_ascii=False, indent=2)


def _downsample_points(points, max_points: int):
	# 避免点云过大导致匹配过慢；使用等步长抽样保证确定性
	if max_points <= 0:
		return points
	try:
		n = len(points)
	except Exception:
		return points
	if n <= max_points:
		return points
	step = max(1, n // max_points)
	return points[::step]


def sample_points_from_entities(
	entities: Iterable,
	*,
	step: float,
	min_circle_points: int = 16,
	max_points: int = 600,
) -> "Any":
	"""
将实体采样为点云（2D），用于后续 Chamfer 距离匹配。

说明：
- 这里只做 2D（忽略 Z）
- 仅覆盖常见图例实体类型：LINE/CIRCLE/ARC/LWPOLYLINE/POLYLINE/POINT
- 返回 numpy.ndarray shape=(N,2)
"""
	import numpy as np

	points: list[tuple[float, float]] = []

	def add_line(x0: float, y0: float, x1: float, y1: float) -> None:
		dx = x1 - x0
		dy = y1 - y0
		ln = math.hypot(dx, dy)
		if not math.isfinite(ln) or ln <= 1e-9:
			points.append((x0, y0))
			return
		n = max(2, int(math.ceil(ln / step)) + 1)
		for i in range(n):
			t = i / (n - 1)
			points.append((x0 + dx * t, y0 + dy * t))

	for e in entities:
		t = e.dxftype()
		try:
			if t == "LINE":
				s = e.dxf.start
				en = e.dxf.end
				add_line(float(s.x), float(s.y), float(en.x), float(en.y))
			elif t == "CIRCLE":
				c = e.dxf.center
				r = float(e.dxf.radius)
				if not math.isfinite(r) or r <= 0:
					continue
				perimeter = 2.0 * math.pi * r
				n = max(min_circle_points, int(math.ceil(perimeter / step)))
				n = min(n, max_points)
				for i in range(n):
					ang = 2.0 * math.pi * (i / n)
					points.append((float(c.x) + r * math.cos(ang), float(c.y) + r * math.sin(ang)))
			elif t == "ARC":
				c = e.dxf.center
				r = float(e.dxf.radius)
				if not math.isfinite(r) or r <= 0:
					continue
				start = math.radians(float(e.dxf.start_angle))
				end = math.radians(float(e.dxf.end_angle))
				# 处理跨越 0 的情况
				if end < start:
					end += 2.0 * math.pi
				arc_len = r * (end - start)
				n = max(min_circle_points, int(math.ceil(arc_len / step)) + 1)
				n = min(n, max_points)
				for i in range(n):
					ang = start + (end - start) * (i / (n - 1))
					points.append((float(c.x) + r * math.cos(ang), float(c.y) + r * math.sin(ang)))
			elif t == "LWPOLYLINE":
				pts = list(e.get_points("xy"))
				if len(pts) < 2:
					continue
				for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
					add_line(float(x0), float(y0), float(x1), float(y1))
				if bool(getattr(e, "closed", False)):
					x0, y0 = pts[-1]
					x1, y1 = pts[0]
					add_line(float(x0), float(y0), float(x1), float(y1))
			elif t == "POLYLINE":
				vs = [v.dxf.location for v in e.vertices]  # type: ignore[attr-defined]
				if len(vs) < 2:
					continue
				pts = [(float(p.x), float(p.y)) for p in vs]
				for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
					add_line(x0, y0, x1, y1)
				if bool(getattr(e, "is_closed", False)):
					add_line(pts[-1][0], pts[-1][1], pts[0][0], pts[0][1])
			elif t == "POINT":
				p = e.dxf.location
				points.append((float(p.x), float(p.y)))
		except Exception:
			continue

	if not points:
		return np.zeros((0, 2), dtype=float)

	arr = np.asarray(points, dtype=float)
	arr = _downsample_points(arr, max_points)
	return np.asarray(arr, dtype=float)


def normalize_points(points: "Any", bbox: tuple[float, float, float, float]) -> "Any":
	"""按 bbox 做中心化 + 缩放归一（max(width,height)=1）。"""
	import numpy as np

	if points is None:
		return np.zeros((0, 2), dtype=float)
	if len(points) == 0:
		return np.zeros((0, 2), dtype=float)

	minx, miny, maxx, maxy = bbox
	cx = (minx + maxx) * 0.5
	cy = (miny + maxy) * 0.5
	norm = max(maxx - minx, maxy - miny)
	if not math.isfinite(norm) or norm <= 1e-9:
		norm = 1.0

	p = np.asarray(points, dtype=float)
	p = p - np.array([[cx, cy]], dtype=float)
	p = p / float(norm)
	return p


def rotate_points(points: "Any", angle_deg: float) -> "Any":
	import numpy as np

	if len(points) == 0:
		return points
	rad = math.radians(angle_deg)
	c = math.cos(rad)
	s = math.sin(rad)
	R = np.array([[c, -s], [s, c]], dtype=float)
	return np.asarray(points, dtype=float) @ R.T


def _mean_nn_distance(a: "Any", b: "Any") -> float:
	"""a->b 的平均最近邻距离。优先使用 scipy.cKDTree，否则回退到 numpy 暴力法（带分块）。"""
	import numpy as np

	a = np.asarray(a, dtype=float)
	b = np.asarray(b, dtype=float)
	if len(a) == 0 or len(b) == 0:
		return float("inf")

	try:
		from scipy.spatial import cKDTree  # type: ignore[import-not-found]

		tree = cKDTree(b)
		dists, _ = tree.query(a, k=1)
		return float(np.mean(dists))
	except Exception:
		# numpy 暴力法：分块避免大矩阵
		chunk = 512
		min_d2 = []
		for i in range(0, len(a), chunk):
			aa = a[i : i + chunk]
			# (na,1,2) - (1,nb,2) => (na,nb,2)
			diff = aa[:, None, :] - b[None, :, :]
			d2 = np.sum(diff * diff, axis=2)
			min_d2.append(np.min(d2, axis=1))
		d2_all = np.concatenate(min_d2, axis=0)
		return float(np.mean(np.sqrt(d2_all)))


def chamfer_distance(points_a: "Any", points_b: "Any", *, symmetric: bool = True) -> float:
	"""Chamfer 距离：平均最近邻距离（可选对称）。"""
	d1 = _mean_nn_distance(points_a, points_b)
	if not symmetric:
		return d1
	d2 = _mean_nn_distance(points_b, points_a)
	return 0.5 * (d1 + d2)


def chamfer_best_rotation(
	template_points: "Any",
	candidate_points: "Any",
	*,
	angles_deg: Iterable[float] = (0, 90, 180, 270),
	symmetric: bool = True,
) -> tuple[float, float]:
	"""尝试多个旋转角，返回 (best_score, best_angle_deg)。"""
	best_score = float("inf")
	best_angle = 0.0
	for ang in angles_deg:
		rot = rotate_points(template_points, float(ang))
		s = chamfer_distance(rot, candidate_points, symmetric=symmetric)
		if s < best_score:
			best_score = s
			best_angle = float(ang)
	return best_score, best_angle


def flatten_entities(entities: Iterable, doc: ezdxf.EzdxfDocument, *, max_depth: int = 2) -> list:
	"""尽量展开 INSERT，生成可用于 bbox/采样/匹配的“平铺实体”列表。"""
	from ezdxf.math import Matrix44

	identity = Matrix44()

	def transform_clone(entity, m: Matrix44):
		e = entity.copy()
		try:
			e.transform(m)
			return e
		except ZeroDivisionError:
			# 部分实体的 extrusion 变换会触发除零，尝试降级处理
			try:
				if e.dxf.hasattr("thickness"):
					e.dxf.discard("thickness")
			except Exception:
				pass
			try:
				if e.dxf.hasattr("extrusion"):
					e.dxf.discard("extrusion")
			except Exception:
				pass
			try:
				e.transform(m)
				return e
			except Exception:
				return None
		except Exception:
			return None

	def walk(entity, m: Matrix44, depth: int):
		if entity.dxftype() == "INSERT" and depth < max_depth:
			try:
				blk = doc.blocks.get(entity.dxf.name)
			except Exception:
				return
			m2 = m @ entity.matrix44()
			for child in blk:
				yield from walk(child, m2, depth + 1)
			return

		if m is identity:
			yield entity
			return

		cl = transform_clone(entity, m)
		if cl is not None:
			yield cl

	out: list = []
	for e in entities:
		out.extend(list(walk(e, identity, 0)))
	return out


def bbox_of_entities(entities: Iterable) -> Optional[tuple[float, float, float, float]]:
	bbs = []
	for e in entities:
		b = bbox2d(e)
		if b is None:
			continue
		bbs.append(b)
	return merge_bbox(bbs)


def point_cloud_from_entities(
	entities: Iterable,
	bbox: tuple[float, float, float, float],
	*,
	sample_div: float = 30.0,
	max_points: int = 600,
) -> "Any":
	"""按 bbox 归一化后的点云（用于 Chamfer 距离匹配）。"""
	minx, miny, maxx, maxy = bbox
	norm = max(maxx - minx, maxy - miny)
	if not math.isfinite(norm) or norm <= 1e-9:
		norm = 1.0
	step = norm / sample_div if sample_div > 0 else norm / 30.0
	step = max(step, 1e-6)
	pts = sample_points_from_entities(entities, step=step, max_points=max_points)
	return normalize_points(pts, bbox)


def encode_point_cloud(points: "Any", *, scale: int = 32767) -> dict[str, Any]:
	"""
将归一化点云编码为 JSON 友好的形式：
- float32/float64 -> int16 量化
- zlib 压缩
- base64 编码
"""
	import numpy as np

	p = np.asarray(points, dtype=float)
	if p.size == 0:
		return {"encoding": "zlib-base64-int16", "n": 0, "scale": int(scale), "data_b64": ""}
	p = np.clip(p, -1.0, 1.0)
	q = np.rint(p * float(scale)).astype(np.int16)
	raw = q.tobytes()
	comp = zlib.compress(raw, level=9)
	return {
		"encoding": "zlib-base64-int16",
		"n": int(q.shape[0]),
		"scale": int(scale),
		"data_b64": base64.b64encode(comp).decode("ascii"),
	}


def decode_point_cloud(data: dict[str, Any]) -> "Any":
	import numpy as np

	if not data:
		return np.zeros((0, 2), dtype=float)
	n = int(data.get("n") or 0)
	scale = int(data.get("scale") or 32767)
	b64 = str(data.get("data_b64") or "")
	if n <= 0 or not b64:
		return np.zeros((0, 2), dtype=float)
	comp = base64.b64decode(b64.encode("ascii"))
	raw = zlib.decompress(comp)
	q = np.frombuffer(raw, dtype=np.int16)
	try:
		q = q.reshape((n, 2))
	except Exception:
		# 兼容：当 n 不可信时，按长度推断
		q = q.reshape((-1, 2))
	return q.astype(float) / float(scale)
