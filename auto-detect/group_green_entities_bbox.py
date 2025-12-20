import argparse
import csv
import hashlib
import json
import math
import os
from collections import defaultdict

import ezdxf
from ezdxf.math import Matrix44, bulge_to_arc


class UnionFind:
    def __init__(self, n: int):
        self.parent = list(range(n))
        self.rank = [0] * n

    def find(self, x: int) -> int:
        while self.parent[x] != x:
            self.parent[x] = self.parent[self.parent[x]]
            x = self.parent[x]
        return x

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


def _abs_aci(color: int) -> int:
    try:
        c = int(color)
    except Exception:
        return 256
    return -c if c < 0 else c


def effective_aci_color(doc: ezdxf.EzDxf, entity) -> int:
    color = _abs_aci(getattr(entity.dxf, "color", 256))
    if color == 256:
        layer_name = getattr(entity.dxf, "layer", "0")
        try:
            layer = doc.layers.get(layer_name)
            return _abs_aci(getattr(layer.dxf, "color", 256))
        except Exception:
            return 256
    return color


def effective_linetype_name(doc: ezdxf.EzDxf, entity) -> str:
    lt = str(getattr(entity.dxf, "linetype", "ByLayer"))
    if lt.strip().lower() == "bylayer":
        layer_name = getattr(entity.dxf, "layer", "0")
        try:
            layer = doc.layers.get(layer_name)
            lt = str(getattr(layer.dxf, "linetype", "Continuous"))
        except Exception:
            lt = "Continuous"
    return lt


def is_continuous_linetype(doc: ezdxf.EzDxf, entity) -> bool:
    lt = effective_linetype_name(doc, entity)
    return lt.strip().upper() == "CONTINUOUS"


def qkey(x: float, y: float, tol: float) -> tuple[int, int]:
    if tol <= 0:
        raise ValueError("tol must be > 0")
    return (int(round(x / tol)), int(round(y / tol)))


def line_endpoints(e):
    s = e.dxf.start
    t = e.dxf.end
    return [(float(s[0]), float(s[1])), (float(t[0]), float(t[1]))]


def arc_endpoints(e):
    c = e.dxf.center
    r = float(e.dxf.radius)
    a0 = math.radians(float(e.dxf.start_angle))
    a1 = math.radians(float(e.dxf.end_angle))
    cx, cy = float(c[0]), float(c[1])
    p0 = (cx + r * math.cos(a0), cy + r * math.sin(a0))
    p1 = (cx + r * math.cos(a1), cy + r * math.sin(a1))
    return [p0, p1]


def lwpolyline_vertices(e):
    pts = []
    try:
        for p in e.get_points():
            pts.append((float(p[0]), float(p[1])))
    except Exception:
        for x, y, *_ in e:
            pts.append((float(x), float(y)))
    return pts


def polyline_vertices(e):
    pts = []
    try:
        for v in e.vertices():
            p = v.dxf.location
            pts.append((float(p[0]), float(p[1])))
    except Exception:
        pass
    return pts


def entity_connection_points(entity):
    t = entity.dxftype()
    if t == "LINE":
        return line_endpoints(entity)
    if t == "ARC":
        return arc_endpoints(entity)
    if t == "LWPOLYLINE":
        pts = lwpolyline_vertices(entity)
        if not pts:
            return []
        if bool(getattr(entity, "closed", False)) or bool(getattr(entity.dxf, "closed", False)):
            return pts
        return [pts[0], pts[-1]]
    if t == "POLYLINE":
        pts = polyline_vertices(entity)
        if not pts:
            return []
        try:
            is_closed = bool(entity.is_closed)
        except Exception:
            is_closed = False
        if is_closed:
            return pts
        return [pts[0], pts[-1]]
    if t == "CIRCLE":
        return []
    return []


def arc_bbox(entity):
    c = entity.dxf.center
    r = float(entity.dxf.radius)
    cx, cy = float(c[0]), float(c[1])

    start = float(entity.dxf.start_angle) % 360.0
    end = float(entity.dxf.end_angle) % 360.0

    def in_sweep(a: float) -> bool:
        if start <= end:
            return start <= a <= end
        return a >= start or a <= end

    angles = [start, end]
    for a in (0.0, 90.0, 180.0, 270.0):
        if in_sweep(a):
            angles.append(a)

    xs = []
    ys = []
    for a in angles:
        rad = math.radians(a)
        xs.append(cx + r * math.cos(rad))
        ys.append(cy + r * math.sin(rad))

    return min(xs), min(ys), max(xs), max(ys)


def circle_bbox(entity):
    c = entity.dxf.center
    r = float(entity.dxf.radius)
    cx, cy = float(c[0]), float(c[1])
    return cx - r, cy - r, cx + r, cy + r


def entity_bbox(doc: ezdxf.EzDxf, entity, insert_depth: int = 4, _visited: set[str] | None = None):
    t = entity.dxftype()
    if not is_continuous_linetype(doc, entity):
        return None
    if t == "INSERT":
        if insert_depth <= 0:
            return None
        try:
            name = str(entity.dxf.name)
        except Exception:
            return None
        if _visited is None:
            _visited = set()
        if name in _visited:
            return None
        _visited.add(name)
        try:
            try:
                block = doc.blocks.get(name)
            except Exception:
                return None
            try:
                m = entity.matrix44()
            except Exception:
                return None

            bb_total = None
            for child in block:
                bb_child = entity_bbox(doc, child, insert_depth=insert_depth - 1, _visited=_visited)
                if bb_child is None:
                    continue
                bb_child_t = bbox_transform(m, bb_child)
                bb_total = bb_child_t if bb_total is None else bbox_union(bb_total, bb_child_t)
            return bb_total
        finally:
            try:
                _visited.remove(name)
            except Exception:
                pass
    if t == "LINE":
        (x0, y0), (x1, y1) = line_endpoints(entity)
        return min(x0, x1), min(y0, y1), max(x0, x1), max(y0, y1)
    if t == "ARC":
        return arc_bbox(entity)
    if t == "CIRCLE":
        return circle_bbox(entity)
    if t == "POINT":
        p = entity.dxf.location
        x, y = float(p[0]), float(p[1])
        return x, y, x, y
    if t == "LWPOLYLINE":
        pts = lwpolyline_vertices(entity)
        if not pts:
            return None
        xs = [p[0] for p in pts]
        ys = [p[1] for p in pts]
        return min(xs), min(ys), max(xs), max(ys)
    if t == "POLYLINE":
        pts = polyline_vertices(entity)
        if not pts:
            return None
        xs = [p[0] for p in pts]
        ys = [p[1] for p in pts]
        return min(xs), min(ys), max(xs), max(ys)
    return None


def entity_bbox_loose(doc: ezdxf.EzDxf, entity):
    if not is_continuous_linetype(doc, entity):
        return None
    bb = entity_bbox(doc, entity)
    if bb is not None:
        return bb

    for attr in ("insert", "location", "center", "start", "end"):
        try:
            p = getattr(entity.dxf, attr)
        except Exception:
            p = None
        if p is None:
            continue
        try:
            x, y = float(p[0]), float(p[1])
            return x, y, x, y
        except Exception:
            continue

    return None


def _sample_arc_points(cx: float, cy: float, r: float, a0: float, a1: float, n: int) -> list[tuple[float, float]]:
    try:
        a0f = float(a0)
        a1f = float(a1)
    except Exception:
        return []
    if n <= 1:
        angs = [a0f]
    else:
        if a1f < a0f:
            a1f += math.tau
        step = (a1f - a0f) / float(n - 1)
        angs = [a0f + step * i for i in range(n)]
    pts: list[tuple[float, float]] = []
    for a in angs:
        pts.append((float(cx) + float(r) * math.cos(a), float(cy) + float(r) * math.sin(a)))
    return pts


def _entity_points_and_primitives(doc: ezdxf.EzDxf, entity) -> tuple[list[tuple[float, float]], list[dict]]:
    return _entity_points_and_primitives_xf(doc, entity, Matrix44(), insert_depth=4, _visited=None)


def _m44_transform_xy(m: Matrix44, x: float, y: float) -> tuple[float, float]:
    try:
        tx, ty, _ = m.transform((float(x), float(y), 0.0))
        return float(tx), float(ty)
    except Exception:
        return float(x), float(y)


def _entity_points_and_primitives_xf(
    doc: ezdxf.EzDxf,
    entity,
    m: Matrix44,
    insert_depth: int,
    _visited: set[str] | None,
) -> tuple[list[tuple[float, float]], list[dict]]:
    if not is_continuous_linetype(doc, entity):
        return [], []
    t = entity.dxftype()
    if t == "LINE":
        pts = line_endpoints(entity)
        if len(pts) != 2:
            return [], []
        (x0, y0), (x1, y1) = pts[0], pts[1]
        tx0, ty0 = _m44_transform_xy(m, x0, y0)
        tx1, ty1 = _m44_transform_xy(m, x1, y1)
        return [(tx0, ty0), (tx1, ty1)], [
            {"type": "LINE", "p0": [tx0, ty0], "p1": [tx1, ty1]},
        ]
    if t == "ARC":
        try:
            c = entity.dxf.center
            r = float(entity.dxf.radius)
            cx, cy = float(c[0]), float(c[1])
            a0 = math.radians(float(entity.dxf.start_angle))
            a1 = math.radians(float(entity.dxf.end_angle))
        except Exception:
            return [], []
        pts = _sample_arc_points(cx, cy, r, a0, a1, n=7)
        if not pts:
            return [], []
        pts_t = [_m44_transform_xy(m, x, y) for x, y in pts]
        p0x, p0y = pts_t[0]
        p1x, p1y = pts_t[-1]
        tcx, tcy = _m44_transform_xy(m, cx, cy)
        rx, ry = _m44_transform_xy(m, cx + r, cy)
        tr = math.hypot(rx - tcx, ry - tcy)
        return pts_t, [
            {"type": "ARC", "center": [tcx, tcy], "radius": tr, "p0": [p0x, p0y], "p1": [p1x, p1y]},
        ]
    if t == "CIRCLE":
        try:
            c = entity.dxf.center
            r = float(entity.dxf.radius)
            cx, cy = float(c[0]), float(c[1])
        except Exception:
            return [], []
        pts = []
        for i in range(8):
            a = math.tau * float(i) / 8.0
            pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
        pts_t = [_m44_transform_xy(m, x, y) for x, y in pts]
        tcx, tcy = _m44_transform_xy(m, cx, cy)
        rx, ry = _m44_transform_xy(m, cx + r, cy)
        tr = math.hypot(rx - tcx, ry - tcy)
        return pts_t, [
            {"type": "CIRCLE", "center": [tcx, tcy], "radius": tr},
        ]
    if t == "LWPOLYLINE":
        try:
            raw = list(entity.get_points("xyb"))
            verts = [(float(p[0]), float(p[1]), float(p[2]) if len(p) > 2 else 0.0) for p in raw]
        except Exception:
            verts = []
        if len(verts) < 2:
            return [], []
        try:
            is_closed = bool(getattr(entity, "closed", False)) or bool(getattr(entity.dxf, "closed", False))
        except Exception:
            is_closed = False
        pts_all: list[tuple[float, float]] = []
        prims: list[dict] = []
        nverts = len(verts)
        seg_count = nverts if is_closed else (nverts - 1)
        for i in range(seg_count):
            x0, y0, b = verts[i]
            x1, y1, _ = verts[(i + 1) % nverts]
            if abs(float(b)) < 1e-9:
                tx0, ty0 = _m44_transform_xy(m, x0, y0)
                tx1, ty1 = _m44_transform_xy(m, x1, y1)
                pts_all.append((tx0, ty0))
                pts_all.append((tx1, ty1))
                prims.append({"type": "LINE", "p0": [tx0, ty0], "p1": [tx1, ty1]})
                continue
            try:
                center, a0, a1, rr = bulge_to_arc((x0, y0), (x1, y1), float(b))
                cx, cy = float(center[0]), float(center[1])
                r = float(rr)
            except Exception:
                tx0, ty0 = _m44_transform_xy(m, x0, y0)
                tx1, ty1 = _m44_transform_xy(m, x1, y1)
                pts_all.append((tx0, ty0))
                pts_all.append((tx1, ty1))
                prims.append({"type": "LINE", "p0": [tx0, ty0], "p1": [tx1, ty1]})
                continue
            arc_pts = _sample_arc_points(cx, cy, r, float(a0), float(a1), n=7)
            arc_pts_t = [_m44_transform_xy(m, x, y) for x, y in arc_pts]
            pts_all.extend(arc_pts_t)
            tcx, tcy = _m44_transform_xy(m, cx, cy)
            tp0x, tp0y = _m44_transform_xy(m, x0, y0)
            tp1x, tp1y = _m44_transform_xy(m, x1, y1)
            tr = math.hypot(tp0x - tcx, tp0y - tcy)
            prims.append({"type": "ARC", "center": [tcx, tcy], "radius": tr, "p0": [tp0x, tp0y], "p1": [tp1x, tp1y]})
        return pts_all, prims
    if t == "POLYLINE":
        verts = []
        try:
            for v in entity.vertices():
                p = v.dxf.location
                b = float(getattr(v.dxf, "bulge", 0.0))
                verts.append((float(p[0]), float(p[1]), b))
        except Exception:
            verts = []
        if len(verts) < 2:
            return [], []
        try:
            is_closed = bool(entity.is_closed)
        except Exception:
            is_closed = False
        pts_all: list[tuple[float, float]] = []
        prims: list[dict] = []
        nverts = len(verts)
        seg_count = nverts if is_closed else (nverts - 1)
        for i in range(seg_count):
            x0, y0, b = verts[i]
            x1, y1, _ = verts[(i + 1) % nverts]
            if abs(float(b)) < 1e-9:
                tx0, ty0 = _m44_transform_xy(m, x0, y0)
                tx1, ty1 = _m44_transform_xy(m, x1, y1)
                pts_all.append((tx0, ty0))
                pts_all.append((tx1, ty1))
                prims.append({"type": "LINE", "p0": [tx0, ty0], "p1": [tx1, ty1]})
                continue
            try:
                center, a0, a1, rr = bulge_to_arc((x0, y0), (x1, y1), float(b))
                cx, cy = float(center[0]), float(center[1])
                r = float(rr)
            except Exception:
                tx0, ty0 = _m44_transform_xy(m, x0, y0)
                tx1, ty1 = _m44_transform_xy(m, x1, y1)
                pts_all.append((tx0, ty0))
                pts_all.append((tx1, ty1))
                prims.append({"type": "LINE", "p0": [tx0, ty0], "p1": [tx1, ty1]})
                continue
            arc_pts = _sample_arc_points(cx, cy, r, float(a0), float(a1), n=7)
            arc_pts_t = [_m44_transform_xy(m, x, y) for x, y in arc_pts]
            pts_all.extend(arc_pts_t)
            tcx, tcy = _m44_transform_xy(m, cx, cy)
            tp0x, tp0y = _m44_transform_xy(m, x0, y0)
            tp1x, tp1y = _m44_transform_xy(m, x1, y1)
            tr = math.hypot(tp0x - tcx, tp0y - tcy)
            prims.append({"type": "ARC", "center": [tcx, tcy], "radius": tr, "p0": [tp0x, tp0y], "p1": [tp1x, tp1y]})
        return pts_all, prims
    if t == "INSERT":
        if insert_depth <= 0:
            return [], []
        try:
            name = str(entity.dxf.name)
        except Exception:
            return [], []
        if _visited is None:
            _visited = set()
        if name in _visited:
            return [], []
        _visited.add(name)
        try:
            try:
                block = doc.blocks.get(name)
            except Exception:
                return [], []
            try:
                mi = entity.matrix44()
            except Exception:
                mi = Matrix44()
            m2 = m @ mi

            pts_all: list[tuple[float, float]] = []
            prims_all: list[dict] = []
            for child in block:
                pts, prims = _entity_points_and_primitives_xf(doc, child, m2, insert_depth=insert_depth - 1, _visited=_visited)
                if pts:
                    pts_all.extend(pts)
                if prims:
                    prims_all.extend(prims)
            return pts_all, prims_all
        finally:
            try:
                _visited.remove(name)
            except Exception:
                pass
    return [], []


def _normalize_points(points: list[tuple[float, float]]) -> tuple[list[tuple[float, float]], tuple[float, float], float]:
    if not points:
        return [], (0.0, 0.0), 1.0
    sx = 0.0
    sy = 0.0
    n = 0
    for x, y in points:
        sx += float(x)
        sy += float(y)
        n += 1
    if n <= 0:
        return [], (0.0, 0.0), 1.0
    cx = sx / float(n)
    cy = sy / float(n)
    shifted = [(float(x) - cx, float(y) - cy) for x, y in points]
    max_r = 0.0
    for x, y in shifted:
        r = math.hypot(float(x), float(y))
        if r > max_r:
            max_r = r
    if max_r <= 0.0:
        max_r = 1.0
    norm = [(float(x) / max_r, float(y) / max_r) for x, y in shifted]
    return norm, (cx, cy), float(max_r)


def _qf(v: float, grid: float) -> int:
    if grid <= 0.0:
        grid = 0.01
    try:
        return int(round(float(v) / float(grid)))
    except Exception:
        return 0


def _quantize_points(points: list[tuple[float, float]], grid: float) -> list[list[int]]:
    out: list[list[int]] = []
    for x, y in points:
        out.append([_qf(float(x), grid), _qf(float(y), grid)])
    out.sort()
    return out


def _load_legend_refs_from_info_json(info_json_path: str, match_desc: str) -> list[dict]:
    if not info_json_path:
        return []
    try:
        with open(info_json_path, "r", encoding="utf-8") as f:
            data = json.load(f)
    except Exception:
        return []
    items = data.get("items")
    if not isinstance(items, list):
        return []

    want = str(match_desc or "").strip()
    out: list[dict] = []
    for it in items:
        if not isinstance(it, dict):
            continue
        desc = str(it.get("desc") or "").strip()
        if not desc or desc != want:
            continue
        ph = it.get("phash")
        st = it.get("stats")
        if not ph:
            continue
        ref_gid = it.get("ref_gid")
        if ref_gid is None:
            ref_gid = it.get("gid")
        out.append({"phash": str(ph), "stats": st if isinstance(st, dict) else {}, "ref_gid": int(ref_gid or 0)})
    return out


def _load_legend_refs_all_from_info_json(info_json_path: str) -> list[dict]:
    if not info_json_path:
        return []
    try:
        with open(info_json_path, "r", encoding="utf-8") as f:
            data = json.load(f)
    except Exception:
        return []
    items = data.get("items")
    if not isinstance(items, list):
        return []

    out: list[dict] = []
    for it in items:
        if not isinstance(it, dict):
            continue
        desc = str(it.get("desc") or "").strip()
        ph = it.get("phash")
        st = it.get("stats")
        if not desc or not ph:
            continue
        ref_gid = it.get("ref_gid")
        if ref_gid is None:
            ref_gid = it.get("gid")
        out.append(
            {
                "phash": str(ph),
                "stats": st if isinstance(st, dict) else {},
                "ref_gid": int(ref_gid or 0),
                "desc": desc,
            }
        )
    return out


def _normalize_and_quantize_primitives(prims: list[dict], center: tuple[float, float], scale: float, grid: float) -> list[dict]:
    cx, cy = float(center[0]), float(center[1])
    s = float(scale) if float(scale) != 0.0 else 1.0
    out: list[dict] = []
    for p in prims:
        t = str(p.get("type", ""))
        if t == "LINE":
            p0 = p.get("p0")
            p1 = p.get("p1")
            if not p0 or not p1:
                continue
            x0 = (float(p0[0]) - cx) / s
            y0 = (float(p0[1]) - cy) / s
            x1 = (float(p1[0]) - cx) / s
            y1 = (float(p1[1]) - cy) / s
            out.append({"type": "LINE", "p0": [_qf(x0, grid), _qf(y0, grid)], "p1": [_qf(x1, grid), _qf(y1, grid)]})
        elif t == "ARC":
            c0 = p.get("center")
            r0 = p.get("radius")
            p0 = p.get("p0")
            p1 = p.get("p1")
            if not c0 or r0 is None or not p0 or not p1:
                continue
            ccx = (float(c0[0]) - cx) / s
            ccy = (float(c0[1]) - cy) / s
            rr = float(r0) / s
            x0 = (float(p0[0]) - cx) / s
            y0 = (float(p0[1]) - cy) / s
            x1 = (float(p1[0]) - cx) / s
            y1 = (float(p1[1]) - cy) / s
            out.append(
                {
                    "type": "ARC",
                    "center": [_qf(ccx, grid), _qf(ccy, grid)],
                    "radius": _qf(rr, grid),
                    "p0": [_qf(x0, grid), _qf(y0, grid)],
                    "p1": [_qf(x1, grid), _qf(y1, grid)],
                }
            )
        elif t == "CIRCLE":
            c0 = p.get("center")
            r0 = p.get("radius")
            if not c0 or r0 is None:
                continue
            ccx = (float(c0[0]) - cx) / s
            ccy = (float(c0[1]) - cy) / s
            rr = float(r0) / s
            out.append({"type": "CIRCLE", "center": [_qf(ccx, grid), _qf(ccy, grid)], "radius": _qf(rr, grid)})
    return out


def _radial_hist(points: list[tuple[float, float]], bins: int) -> list[float]:
    if bins <= 0:
        bins = 1
    hist = [0.0] * int(bins)
    if not points:
        return hist
    for x, y in points:
        r = math.hypot(float(x), float(y))
        if r < 0.0:
            r = 0.0
        if r > 1.0:
            r = 1.0
        idx = int(r * float(bins))
        if idx >= bins:
            idx = bins - 1
        hist[idx] += 1.0
    total = float(len(points))
    if total > 0.0:
        hist = [h / total for h in hist]
    return hist


def _pairwise_dist_hist(points: list[tuple[float, float]], bins: int, max_points: int = 80) -> list[float]:
    if bins <= 0:
        bins = 1
    hist = [0.0] * int(bins)
    if len(points) < 2:
        return hist
    pts = sorted([(float(x), float(y)) for x, y in points])
    if max_points > 0 and len(pts) > max_points:
        step = max(1, int(math.ceil(float(len(pts)) / float(max_points))))
        pts = pts[::step]
    n = len(pts)
    if n < 2:
        return hist
    pair_count = 0
    for i in range(n):
        x0, y0 = pts[i]
        for j in range(i + 1, n):
            x1, y1 = pts[j]
            d = math.hypot(x1 - x0, y1 - y0)
            if d < 0.0:
                d = 0.0
            if d > 2.0:
                d = 2.0
            idx = int((d / 2.0) * float(bins))
            if idx >= bins:
                idx = bins - 1
            hist[idx] += 1.0
            pair_count += 1
    if pair_count > 0:
        inv = 1.0 / float(pair_count)
        hist = [h * inv for h in hist]
    return hist


def _phash_from_vector(vec: list[float]) -> str:
    if not vec:
        return "0" * 16
    v = [float(x) for x in vec]
    if len(v) < 64:
        v = v + [0.0] * (64 - len(v))
    elif len(v) > 64:
        v = v[:64]
    med = sorted(v)[len(v) // 2]
    bits = 0
    for i, x in enumerate(v):
        if float(x) > float(med):
            bits |= 1 << (63 - i)
    return f"{bits:016x}"


def _phash_hamming(a: str, b: str) -> int:
    try:
        ia = int(str(a), 16)
        ib = int(str(b), 16)
    except Exception:
        return 10**9
    return int((ia ^ ib).bit_count())


def _stats_similarity_score(ref_stats, cand_stats) -> int:
    if not isinstance(ref_stats, dict) or not isinstance(cand_stats, dict):
        return 0
    score = 0
    for k in ("n_primitives", "n_line", "n_arc", "n_circle"):
        rv = ref_stats.get(k)
        cv = cand_stats.get(k)
        if isinstance(rv, int) and isinstance(cv, int):
            score += abs(int(cv) - int(rv))
    return int(score)


def _compute_geom_from_entity(doc: ezdxf.EzDxf, entity, quant_grid: float):
    pts, prims = _entity_points_and_primitives(doc, entity)
    if not pts and not prims:
        return None

    norm_pts, center, scale = _normalize_points(pts)
    q_pts = _quantize_points(norm_pts, quant_grid)
    q_pts_unique: list[list[int]] = []
    last = None
    for p in q_pts:
        if last is None or p != last:
            q_pts_unique.append(p)
            last = p

    q_prims = _normalize_and_quantize_primitives(prims, center=center, scale=scale, grid=quant_grid)
    q_prims_sorted = sorted(q_prims, key=lambda d: json.dumps(d, sort_keys=True, separators=(",", ":")))

    canon = json.dumps({"points": q_pts_unique, "prims": q_prims_sorted}, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
    geom_hash = hashlib.sha1(canon.encode("utf-8")).hexdigest()

    radial = _radial_hist(norm_pts, bins=16)
    dist = _pairwise_dist_hist(norm_pts, bins=48)
    phash = _phash_from_vector(radial + dist)

    n_line = 0
    n_arc = 0
    n_circle = 0
    for p in prims:
        tt = str(p.get("type", ""))
        if tt == "LINE":
            n_line += 1
        elif tt == "ARC":
            n_arc += 1
        elif tt == "CIRCLE":
            n_circle += 1

    stats = {
        "n_points": int(len(norm_pts)),
        "n_primitives": int(len(prims)),
        "n_line": int(n_line),
        "n_arc": int(n_arc),
        "n_circle": int(n_circle),
    }

    geom = {
        "geom_id": geom_hash,
        "geom_hash": geom_hash,
        "phash": phash,
        "quant_grid": float(quant_grid),
        "points_q": q_pts_unique,
        "primitives_q": q_prims_sorted,
        "radial_hist": radial,
        "dist_hist": dist,
        "stats": stats,
    }
    return geom


def _load_legend_refs_from_index(index_json_path: str, geom_dir: str | None, match_desc: str) -> list[dict]:
    if not index_json_path:
        return []
    try:
        with open(index_json_path, "r", encoding="utf-8") as f:
            data = json.load(f)
    except Exception:
        return []
    items = data.get("items")
    if not isinstance(items, list):
        return []
    want = str(match_desc).strip()
    out: list[dict] = []
    for it in items:
        if not isinstance(it, dict):
            continue
        descs = it.get("descs")
        if not isinstance(descs, list):
            continue
        norm_descs = [str(d).strip() for d in descs if d is not None]
        if want not in norm_descs:
            continue

        cur = dict(it)
        if (not cur.get("phash") or not cur.get("stats")) and geom_dir:
            gid = str(cur.get("geom_id") or "")
            if gid:
                geom_path = os.path.join(str(geom_dir), f"{gid}.json")
                if os.path.exists(geom_path):
                    try:
                        with open(geom_path, "r", encoding="utf-8") as gf:
                            geom = json.load(gf)
                        if not cur.get("phash") and geom.get("phash"):
                            cur["phash"] = geom.get("phash")
                        if not cur.get("stats") and geom.get("stats"):
                            cur["stats"] = geom.get("stats")
                    except Exception:
                        pass

        out.append(cur)
    return out


def _load_legend_refs_all_from_index(index_json_path: str, geom_dir: str | None) -> list[dict]:
    if not index_json_path:
        return []
    try:
        with open(index_json_path, "r", encoding="utf-8") as f:
            data = json.load(f)
    except Exception:
        return []
    items = data.get("items")
    if not isinstance(items, list):
        return []

    out: list[dict] = []
    for it in items:
        if not isinstance(it, dict):
            continue
        descs = it.get("descs")
        if not isinstance(descs, list) or not descs:
            continue
        norm_descs = [str(d).strip() for d in descs if d is not None and str(d).strip()]
        if not norm_descs:
            continue

        cur = dict(it)
        if (not cur.get("phash") or not cur.get("stats")) and geom_dir:
            gid = str(cur.get("geom_id") or "")
            if gid:
                geom_path = os.path.join(str(geom_dir), f"{gid}.json")
                if os.path.exists(geom_path):
                    try:
                        with open(geom_path, "r", encoding="utf-8") as gf:
                            geom = json.load(gf)
                        if not cur.get("phash") and geom.get("phash"):
                            cur["phash"] = geom.get("phash")
                        if not cur.get("stats") and geom.get("stats"):
                            cur["stats"] = geom.get("stats")
                    except Exception:
                        pass

        examples = cur.get("examples")
        ref_gid = 0
        if isinstance(examples, list) and examples:
            try:
                ref_gid = int(examples[0].get("gid", 0))
            except Exception:
                ref_gid = 0

        for d in norm_descs:
            item = dict(cur)
            item["desc"] = str(d)
            if ref_gid:
                item["ref_gid"] = int(ref_gid)
            out.append(item)
    return out


def _load_legend_ref_geoms_from_csv_and_dxf_all(
    ref_dxf_path: str,
    ref_csv_path: str,
    target_acis: set[int],
    max_entity_diag: float | None,
    quant_grid: float,
):
    if not ref_dxf_path or not ref_csv_path:
        return []
    if not os.path.exists(ref_dxf_path) or not os.path.exists(ref_csv_path):
        return []

    ref_bbs: list[tuple[int, str, tuple[float, float, float, float]]] = []
    try:
        with open(ref_csv_path, "r", encoding="utf-8-sig", newline="") as f:
            reader = csv.reader(f)
            header = next(reader, None)
            for row in reader:
                if not row or len(row) < 6:
                    continue
                desc = str(row[1] if len(row) > 1 else "").strip()
                if not desc:
                    continue
                try:
                    gid = int(str(row[0]).strip())
                    x0 = float(row[2])
                    y0 = float(row[3])
                    x1 = float(row[4])
                    y1 = float(row[5])
                except Exception:
                    continue
                ref_bbs.append((gid, desc, (x0, y0, x1, y1)))
    except Exception:
        ref_bbs = []

    if not ref_bbs:
        return []

    try:
        ref_doc = ezdxf.readfile(ref_dxf_path, encoding="latin1")
    except Exception:
        return []

    out = []
    for gid, desc, bb in ref_bbs:
        geom = _compute_geom_from_bbox(
            ref_doc,
            bb,
            target_acis=target_acis,
            exclude_handles=None,
            max_entity_diag=max_entity_diag,
            quant_grid=float(quant_grid),
        )
        if geom is None:
            continue
        geom["ref_gid"] = int(gid)
        geom["desc"] = str(desc)
        out.append(geom)
    return out


def _load_legend_ref_geoms_from_csv_and_dxf(
    ref_dxf_path: str,
    ref_csv_path: str,
    match_desc: str,
    target_acis: set[int],
    max_entity_diag: float | None,
    quant_grid: float,
):
    if not ref_dxf_path or not ref_csv_path:
        return []
    if not os.path.exists(ref_dxf_path) or not os.path.exists(ref_csv_path):
        return []

    want = str(match_desc).strip()
    ref_bbs: list[tuple[int, tuple[float, float, float, float]]] = []
    try:
        with open(ref_csv_path, "r", encoding="utf-8-sig", newline="") as f:
            reader = csv.reader(f)
            header = next(reader, None)
            for row in reader:
                if not row or len(row) < 6:
                    continue
                desc = str(row[1] if len(row) > 1 else "").strip()
                if desc != want:
                    continue
                try:
                    gid = int(str(row[0]).strip())
                    x0 = float(row[2])
                    y0 = float(row[3])
                    x1 = float(row[4])
                    y1 = float(row[5])
                except Exception:
                    continue
                ref_bbs.append((gid, (x0, y0, x1, y1)))
    except Exception:
        ref_bbs = []

    if not ref_bbs:
        return []

    try:
        ref_doc = ezdxf.readfile(ref_dxf_path, encoding="latin1")
    except Exception:
        return []

    out = []
    for gid, bb in ref_bbs:
        geom = _compute_geom_from_bbox(
            ref_doc,
            bb,
            target_acis=target_acis,
            exclude_handles=None,
            max_entity_diag=max_entity_diag,
            quant_grid=float(quant_grid),
        )
        if geom is None:
            continue
        geom["ref_gid"] = int(gid)
        out.append(geom)
    return out


def _wave_bbox_points(
    bbox: tuple[float, float, float, float],
    pad: float = 0.5,
    amplitude: float = 1.0,
    wavelength: float = 6.0,
    step: float | None = None,
):
    x0, y0, x1, y1 = bbox
    x0 = float(x0) - float(pad)
    y0 = float(y0) - float(pad)
    x1 = float(x1) + float(pad)
    y1 = float(y1) + float(pad)

    w = float(x1) - float(x0)
    h = float(y1) - float(y0)
    if w <= 0.0 or h <= 0.0:
        return []

    amp = float(amplitude)
    amp = max(0.0, amp)
    amp = min(amp, max(0.1, min(w, h) * 0.25))

    wl = float(wavelength)
    if wl <= 0.0:
        wl = max(1e-6, min(w, h))

    per = 2.0 * (w + h)
    if step is None:
        step = max(0.5, wl / 8.0)
    st = max(0.1, float(step))
    n = int(math.ceil(per / st))
    n = max(16, n)

    pts = []
    for i in range(n):
        s = per * (float(i) / float(n))
        if s < w:
            x = x0 + s
            y = y0
            nx, ny = 0.0, -1.0
        elif s < w + h:
            t = s - w
            x = x1
            y = y0 + t
            nx, ny = 1.0, 0.0
        elif s < 2.0 * w + h:
            t = s - (w + h)
            x = x1 - t
            y = y1
            nx, ny = 0.0, 1.0
        else:
            t = s - (2.0 * w + h)
            x = x0
            y = y1 - t
            nx, ny = -1.0, 0.0

        off = amp * math.sin(2.0 * math.pi * (s / wl))
        pts.append((x + nx * off, y + ny * off))
    return pts


def _extract_entities_in_bbox(
    doc: ezdxf.EzDxf,
    bbox,
    target_acis: set[int],
    exclude_handles: set[str] | None,
    max_entity_diag: float | None,
):
    msp = doc.modelspace()
    supported = {"LINE", "ARC", "CIRCLE", "LWPOLYLINE", "POLYLINE", "INSERT"}
    x0, y0, x1, y1 = bbox
    eps = 0.001
    bb = (float(x0) - eps, float(y0) - eps, float(x1) + eps, float(y1) + eps)
    out = []
    for e in msp:
        t = e.dxftype()
        if t not in supported:
            continue
        if exclude_handles is not None:
            try:
                if str(e.dxf.handle) in exclude_handles:
                    continue
            except Exception:
                pass
        if effective_aci_color(doc, e) not in target_acis:
            continue
        if not is_continuous_linetype(doc, e):
            continue
        eb = entity_bbox(doc, e)
        if eb is None:
            continue
        if max_entity_diag is not None:
            try:
                md = float(max_entity_diag)
            except Exception:
                md = None
            if md is not None and md > 0.0:
                if bbox_diag(eb) > md:
                    continue
        if not bbox_intersects(bb, eb):
            continue
        out.append(e)
    return out


def _compute_geom_from_bbox(
    doc: ezdxf.EzDxf,
    bbox,
    target_acis: set[int],
    exclude_handles: set[str] | None,
    max_entity_diag: float | None,
    quant_grid: float,
):
    entities = _extract_entities_in_bbox(doc, bbox, target_acis=target_acis, exclude_handles=exclude_handles, max_entity_diag=max_entity_diag)
    if not entities:
        return None
    all_pts: list[tuple[float, float]] = []
    all_prims: list[dict] = []
    for e in entities:
        pts, prims = _entity_points_and_primitives(doc, e)
        if pts:
            all_pts.extend(pts)
        if prims:
            all_prims.extend(prims)
    if not all_pts and not all_prims:
        return None

    norm_pts, center, scale = _normalize_points(all_pts)
    q_pts = _quantize_points(norm_pts, quant_grid)
    q_pts_unique: list[list[int]] = []
    last = None
    for p in q_pts:
        if last is None or p != last:
            q_pts_unique.append(p)
            last = p

    q_prims = _normalize_and_quantize_primitives(all_prims, center=center, scale=scale, grid=quant_grid)
    q_prims_sorted = sorted(q_prims, key=lambda d: json.dumps(d, sort_keys=True, separators=(",", ":")))

    canon = json.dumps({"points": q_pts_unique, "prims": q_prims_sorted}, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
    geom_hash = hashlib.sha1(canon.encode("utf-8")).hexdigest()

    radial = _radial_hist(norm_pts, bins=16)
    dist = _pairwise_dist_hist(norm_pts, bins=48)
    phash = _phash_from_vector(radial + dist)

    n_line = 0
    n_arc = 0
    n_circle = 0
    for p in all_prims:
        tt = str(p.get("type", ""))
        if tt == "LINE":
            n_line += 1
        elif tt == "ARC":
            n_arc += 1
        elif tt == "CIRCLE":
            n_circle += 1

    stats = {
        "n_points": int(len(norm_pts)),
        "n_primitives": int(len(all_prims)),
        "n_line": int(n_line),
        "n_arc": int(n_arc),
        "n_circle": int(n_circle),
    }

    geom = {
        "geom_id": geom_hash,
        "geom_hash": geom_hash,
        "phash": phash,
        "quant_grid": float(quant_grid),
        "points_q": q_pts_unique,
        "primitives_q": q_prims_sorted,
        "radial_hist": radial,
        "dist_hist": dist,
        "stats": stats,
    }
    return geom


def export_legend_geometry_library(
    doc: ezdxf.EzDxf,
    in_path: str,
    legend_results,
    target_acis: set[int],
    index_json_path: str,
    geom_dir: str,
    exclude_handles: set[str] | None,
    max_entity_diag: float | None,
    quant_grid: float = 0.02,
):
    if not index_json_path or not geom_dir:
        return
    os.makedirs(geom_dir, exist_ok=True)

    index_data = {"version": 1, "items": []}
    if os.path.exists(index_json_path):
        try:
            with open(index_json_path, "r", encoding="utf-8") as f:
                loaded = json.load(f)
            if isinstance(loaded, dict) and isinstance(loaded.get("items"), list):
                index_data = loaded
        except Exception:
            index_data = {"version": 1, "items": []}

    items_by_id: dict[str, dict] = {}
    for it in index_data.get("items", []):
        try:
            gid = str(it.get("geom_id", ""))
        except Exception:
            gid = ""
        if gid:
            items_by_id[gid] = it

    src_name = os.path.basename(str(in_path))
    for gid, bb, desc in legend_results:
        if not desc:
            continue
        geom = _compute_geom_from_bbox(
            doc,
            bb,
            target_acis=target_acis,
            exclude_handles=exclude_handles,
            max_entity_diag=max_entity_diag,
            quant_grid=float(quant_grid),
        )
        if geom is None:
            continue
        geom_id = str(geom.get("geom_id"))
        geom_path = os.path.join(geom_dir, f"{geom_id}.json")
        if not os.path.exists(geom_path):
            try:
                with open(geom_path, "w", encoding="utf-8") as f:
                    json.dump(geom, f, ensure_ascii=False, indent=2)
            except Exception:
                pass

        item = items_by_id.get(geom_id)
        if item is None:
            item = {
                "geom_id": geom_id,
                "geom_hash": geom.get("geom_hash"),
                "phash": geom.get("phash"),
                "radial_hist": geom.get("radial_hist"),
                "dist_hist": geom.get("dist_hist"),
                "stats": geom.get("stats"),
                "descs": [],
                "examples": [],
            }
            items_by_id[geom_id] = item

        descs = item.get("descs")
        if not isinstance(descs, list):
            descs = []
            item["descs"] = descs
        if str(desc) not in descs:
            descs.append(str(desc))

        examples = item.get("examples")
        if not isinstance(examples, list):
            examples = []
            item["examples"] = examples
        examples.append({"input": src_name, "gid": int(gid), "bbox": [float(bb[0]), float(bb[1]), float(bb[2]), float(bb[3])]})
        if len(examples) > 20:
            item["examples"] = examples[-20:]

    index_out = {"version": index_data.get("version", 1), "items": [items_by_id[k] for k in sorted(items_by_id.keys())]}
    try:
        with open(index_json_path, "w", encoding="utf-8") as f:
            json.dump(index_out, f, ensure_ascii=False, indent=2)
    except Exception:
        pass


def export_legend_info_json(
    doc: ezdxf.EzDxf,
    in_path: str,
    legend_results,
    target_acis: set[int],
    info_json_path: str,
    exclude_handles: set[str] | None,
    max_entity_diag: float | None,
    quant_grid: float,
):
    if not info_json_path:
        return

    items: list[dict] = []
    for gid, bb, desc in legend_results:
        d = str(desc or "").strip()
        if not d:
            continue
        text_sig = _extract_internal_single_letter_sig_from_bbox(
            doc,
            bb,
            target_acis=target_acis,
            exclude_handles=exclude_handles,
            max_entity_diag=max_entity_diag,
        )
        geom = _compute_geom_from_bbox(
            doc,
            bb,
            target_acis=target_acis,
            exclude_handles=exclude_handles,
            max_entity_diag=max_entity_diag,
            quant_grid=float(quant_grid),
        )
        if geom is None:
            continue
        items.append(
            {
                "gid": int(gid),
                "ref_gid": int(gid),
                "desc": d,
                "bbox": [float(bb[0]), float(bb[1]), float(bb[2]), float(bb[3])],
                "geom_id": geom.get("geom_id"),
                "geom_hash": geom.get("geom_hash"),
                "phash": geom.get("phash"),
                "stats": geom.get("stats"),
                "text_sig": str(text_sig or ""),
            }
        )

    out = {
        "version": 1,
        "src_dxf": os.path.basename(str(in_path)),
        "quant_grid": float(quant_grid),
        "items": items,
    }
    try:
        with open(info_json_path, "w", encoding="utf-8") as f:
            json.dump(out, f, ensure_ascii=False, indent=2)
    except Exception:
        pass


def _decode_dxf_text(raw_text: str) -> str:
    """尝试修复DXF文本编码（有些文件声明gb2312但实际是UTF-8）"""
    if not raw_text:
        return raw_text
    try:
        raw_bytes = raw_text.encode("latin1")
        return raw_bytes.decode("utf-8")
    except Exception:
        return raw_text


def _normalize_single_letter_sig(raw_text: str) -> str | None:
    if raw_text is None:
        return None
    t = _decode_dxf_text(str(raw_text)).strip().upper()
    if not t:
        return None
    filtered = "".join(ch for ch in t if ch.isalnum())
    if len(filtered) != 1:
        return None
    ch = filtered
    if "A" <= ch <= "Z":
        return ch
    return None


def _is_point_in_sig_region(symbol_bbox, x: float, y: float) -> bool:
    try:
        x0, y0, x1, y1 = symbol_bbox
        w = float(x1) - float(x0)
        h = float(y1) - float(y0)
    except Exception:
        return False
    if w <= 0.0 or h <= 0.0:
        return False
    rx0 = float(x0) + 0.55 * w
    rx1 = float(x1) + 1e-6
    ry0 = float(y0) - 1e-6
    ry1 = float(y0) + 0.45 * h
    return rx0 <= float(x) <= rx1 and ry0 <= float(y) <= ry1


def _entity_text_items_xf(
    doc: ezdxf.EzDxf,
    entity,
    m: Matrix44,
    insert_depth: int,
    _visited: set[str] | None,
):
    t = entity.dxftype()
    if t in ("TEXT", "MTEXT", "ATTRIB", "ATTDEF"):
        try:
            p = getattr(entity.dxf, "insert", None)
            if p is None:
                p = getattr(entity.dxf, "location", None)
            x, y = float(p[0]), float(p[1])
        except Exception:
            return []
        try:
            tx, ty, _ = m.transform((float(x), float(y), 0.0))
        except Exception:
            tx, ty = float(x), float(y)

        text = ""
        try:
            if t == "TEXT":
                text = str(getattr(entity.dxf, "text", ""))
            elif t == "MTEXT":
                text = str(getattr(entity, "text", ""))
                if not text:
                    text = str(getattr(entity.dxf, "text", ""))
            else:
                text = str(getattr(entity.dxf, "text", ""))
        except Exception:
            text = ""
        return [(text, (float(tx), float(ty)))]

    if t == "INSERT":
        if insert_depth <= 0:
            return []
        try:
            name = str(entity.dxf.name)
        except Exception:
            return []
        if _visited is None:
            _visited = set()
        if name in _visited:
            return []
        _visited.add(name)
        try:
            try:
                block = doc.blocks.get(name)
            except Exception:
                return []
            try:
                mi = entity.matrix44()
            except Exception:
                mi = Matrix44()
            m2 = m @ mi

            out = []
            for child in block:
                out.extend(_entity_text_items_xf(doc, child, m2, insert_depth=insert_depth - 1, _visited=_visited))
            try:
                for a in getattr(entity, "attribs", []) or []:
                    out.extend(_entity_text_items_xf(doc, a, m2, insert_depth=insert_depth - 1, _visited=_visited))
            except Exception:
                pass
            return out
        finally:
            try:
                _visited.remove(name)
            except Exception:
                pass
    return []


def _extract_internal_single_letter_sig_from_insert(doc: ezdxf.EzDxf, insert_entity, symbol_bbox, target_acis: set[int]) -> str | None:
    letters: list[str] = []
    for raw_text, (tx, ty) in _entity_text_items_xf(doc, insert_entity, Matrix44(), insert_depth=4, _visited=None):
        if not _is_point_in_sig_region(symbol_bbox, tx, ty):
            continue
        sig = _normalize_single_letter_sig(raw_text)
        if not sig:
            continue
        try:
            if effective_aci_color(doc, insert_entity) not in target_acis:
                # 若整个INSERT颜色不在目标色，仍可能子文字在目标色；这里不强行过滤
                pass
        except Exception:
            pass
        letters.append(sig)
    uniq = sorted(set(letters))
    if len(uniq) == 1:
        return uniq[0]
    return None


def _extract_internal_single_letter_sig_from_bbox(
    doc: ezdxf.EzDxf,
    symbol_bbox,
    target_acis: set[int],
    exclude_handles: set[str] | None,
    max_entity_diag: float | None,
):
    msp = doc.modelspace()
    letters: list[str] = []
    for e in msp:
        t = e.dxftype()
        if t not in ("TEXT", "MTEXT", "INSERT"):
            continue
        if exclude_handles is not None:
            try:
                if str(e.dxf.handle) in exclude_handles:
                    continue
            except Exception:
                pass
        if t in ("TEXT", "MTEXT"):
            try:
                if effective_aci_color(doc, e) not in target_acis:
                    continue
            except Exception:
                continue
            bb, txt = text_entity_bbox_and_content(doc, e)
            if bb is None:
                continue
            cx = (float(bb[0]) + float(bb[2])) * 0.5
            cy = (float(bb[1]) + float(bb[3])) * 0.5
            if not bbox_contains(symbol_bbox, (cx, cy, cx, cy)):
                continue
            if not _is_point_in_sig_region(symbol_bbox, cx, cy):
                continue
            sig = _normalize_single_letter_sig(txt)
            if sig:
                letters.append(sig)
            continue

        if t == "INSERT":
            try:
                if effective_aci_color(doc, e) not in target_acis:
                    continue
            except Exception:
                continue
            ibb = entity_bbox(doc, e)
            if ibb is None:
                continue
            if max_entity_diag is not None:
                try:
                    md = float(max_entity_diag)
                except Exception:
                    md = None
                if md is not None and md > 0.0:
                    if bbox_diag(ibb) > md:
                        continue
            if not bbox_intersects(symbol_bbox, ibb):
                continue
            sig = _extract_internal_single_letter_sig_from_insert(doc, e, symbol_bbox, target_acis=target_acis)
            if sig:
                letters.append(sig)

    uniq = sorted(set(letters))
    if len(uniq) == 1:
        return uniq[0]
    return None


def text_entity_bbox_and_content(doc, entity):
    """获取TEXT/MTEXT实体的bbox和文本内容，返回 (bbox, text) 或 (None, None)"""
    t = entity.dxftype()
    if t == "TEXT":
        try:
            insert = entity.dxf.insert
            x, y = float(insert[0]), float(insert[1])
            h = float(getattr(entity.dxf, "height", 2.5))
            text = _decode_dxf_text(str(getattr(entity.dxf, "text", "")))
            w = len(text) * h * 0.8
            bb = (x, y, x + w, y + h)
            return bb, text
        except Exception:
            return None, None
    if t == "MTEXT":
        try:
            insert = entity.dxf.insert
            x, y = float(insert[0]), float(insert[1])
            h = float(getattr(entity.dxf, "char_height", 2.5))
            text = str(getattr(entity, "text", ""))
            if not text:
                text = str(getattr(entity.dxf, "text", ""))
            text = _decode_dxf_text(text)
            lines = text.replace("\\P", "\n").split("\n")
            max_len = max((len(ln) for ln in lines), default=1)
            w = max_len * h * 0.8
            th = h * len(lines)
            bb = (x, y - th, x + w, y)
            return bb, text.replace("\\P", "")
        except Exception:
            return None, None
    return None, None


def find_legend_description(
    doc,
    symbol_bbox,
    text_entities,
    obstacle_entities,
    corridor_width: float = 100.0,
    y_pad: float = 10.0,
    text_color: int = 2,
):
    """
    在符号bbox左侧走廊内搜索黄色文字作为图例描述。
    如果走廊内有任何非文字实体（障碍物）与之相交，则返回None（严格模式）。
    返回: (description_text, text_bboxes) 或 (None, None)
    """
    x0, y0, x1, y1 = symbol_bbox
    corridor = (x0 - corridor_width, y0 - y_pad, x0, y1 + y_pad)

    candidates = []
    for te, tbb, tcontent in text_entities:
        if effective_aci_color(doc, te) != text_color:
            continue
        if tbb is None:
            continue
        tx_center = (tbb[0] + tbb[2]) * 0.5
        ty_center = (tbb[1] + tbb[3]) * 0.5
        if corridor[0] <= tx_center <= corridor[2] and corridor[1] <= ty_center <= corridor[3]:
            candidates.append((te, tbb, tcontent, ty_center, tx_center))

    if not candidates:
        return None, None

    anchor = max(candidates, key=lambda c: c[1][2])
    anchor_bb = anchor[1]
    check_left = float(anchor_bb[2])
    if check_left > x0:
        check_left = x0
    corridor_check = (check_left, corridor[1], x0, corridor[3])

    for obs_e, obs_bb in obstacle_entities:
        if bbox_intersects(corridor_check, obs_bb):
            return None, None

    anchor_x = float(anchor_bb[0])
    anchor_h = float(anchor_bb[3]) - float(anchor_bb[1])
    x_tol = max(2.0, anchor_h * 0.5)
    candidates = [c for c in candidates if abs(float(c[1][0]) - anchor_x) <= x_tol]
    if not candidates:
        candidates = [anchor]

    candidates.sort(key=lambda c: (-c[3], c[4]))
    description = "".join(c[2] for c in candidates)
    text_bbs = [c[1] for c in candidates]
    return description, text_bbs


def bbox_union(a, b):
    ax0, ay0, ax1, ay1 = a
    bx0, by0, bx1, by1 = b
    return min(ax0, bx0), min(ay0, by0), max(ax1, bx1), max(ay1, by1)


def bbox_intersects(a, b) -> bool:
    ax0, ay0, ax1, ay1 = a
    bx0, by0, bx1, by1 = b
    return not (ax1 < bx0 or bx1 < ax0 or ay1 < by0 or by1 < ay0)


def bbox_contains(outer, inner) -> bool:
    ox0, oy0, ox1, oy1 = outer
    ix0, iy0, ix1, iy1 = inner
    return ox0 <= ix0 and oy0 <= iy0 and ox1 >= ix1 and oy1 >= iy1


def bbox_diag(bb) -> float:
    return math.hypot(float(bb[2]) - float(bb[0]), float(bb[3]) - float(bb[1]))


def detect_frame_and_titleblock(doc: ezdxf.EzDxf, tol: float, frame_tol: float, title_ratio: float):
    msp = doc.modelspace()
    line_types = {"LINE", "LWPOLYLINE", "POLYLINE"}
    items = []

    all_bbs = []
    for e in msp:
        if not is_continuous_linetype(doc, e):
            continue
        bb = entity_bbox_loose(doc, e)
        if bb is None:
            continue
        all_bbs.append(bb)
        if e.dxftype() in line_types:
            bb2 = entity_bbox(doc, e)
            if bb2 is not None:
                items.append((e, bb2))

    if not all_bbs:
        return set(), None

    minx = min(bb[0] for bb in all_bbs)
    miny = min(bb[1] for bb in all_bbs)
    maxx = max(bb[2] for bb in all_bbs)
    maxy = max(bb[3] for bb in all_bbs)
    w = float(maxx) - float(minx)
    h = float(maxy) - float(miny)
    if w <= 0.0 or h <= 0.0:
        return set(), None

    edge_eps = max(float(frame_tol) * 2.0, float(tol) * 5.0)
    seed_ratio = 0.8

    seeds = []
    for i, (_, bb) in enumerate(items):
        bw = float(bb[2]) - float(bb[0])
        bh = float(bb[3]) - float(bb[1])
        touches_h = abs(float(bb[1]) - float(miny)) < edge_eps or abs(float(bb[3]) - float(maxy)) < edge_eps
        touches_v = abs(float(bb[0]) - float(minx)) < edge_eps or abs(float(bb[2]) - float(maxx)) < edge_eps
        if (touches_h and bw > w * seed_ratio) or (touches_v and bh > h * seed_ratio):
            seeds.append(i)
            continue
        if (
            abs(float(bb[0]) - float(minx)) < edge_eps
            and abs(float(bb[1]) - float(miny)) < edge_eps
            and abs(float(bb[2]) - float(maxx)) < edge_eps
            and abs(float(bb[3]) - float(maxy)) < edge_eps
        ):
            seeds.append(i)

    buckets: dict[tuple[int, int], list[int]] = defaultdict(list)
    for i, (e, _) in enumerate(items):
        for x, y in entity_connection_points(e):
            try:
                k = qkey(float(x), float(y), float(frame_tol))
            except Exception:
                continue
            buckets[k].append(i)

    frame_set = set()
    if seeds:
        q = list(dict.fromkeys(seeds))
        frame_set = set(q)
        head = 0
        while head < len(q):
            i = q[head]
            head += 1
            e, _ = items[i]
            for x, y in entity_connection_points(e):
                try:
                    k = qkey(float(x), float(y), float(frame_tol))
                except Exception:
                    continue
                for j in buckets.get(k, []):
                    if j in frame_set:
                        continue
                    frame_set.add(j)
                    q.append(j)

    outer_border = set()
    for i in frame_set:
        bb = items[i][1]
        bw = float(bb[2]) - float(bb[0])
        bh = float(bb[3]) - float(bb[1])
        if bw > w * seed_ratio and abs(float(bb[1]) - float(miny)) < edge_eps and abs(float(bb[3]) - float(miny)) < edge_eps:
            outer_border.add(i)
            continue
        if bw > w * seed_ratio and abs(float(bb[1]) - float(maxy)) < edge_eps and abs(float(bb[3]) - float(maxy)) < edge_eps:
            outer_border.add(i)
            continue
        if bh > h * seed_ratio and abs(float(bb[0]) - float(minx)) < edge_eps and abs(float(bb[2]) - float(minx)) < edge_eps:
            outer_border.add(i)
            continue
        if bh > h * seed_ratio and abs(float(bb[0]) - float(maxx)) < edge_eps and abs(float(bb[2]) - float(maxx)) < edge_eps:
            outer_border.add(i)
            continue
        if (
            abs(float(bb[0]) - float(minx)) < edge_eps
            and abs(float(bb[1]) - float(miny)) < edge_eps
            and abs(float(bb[2]) - float(maxx)) < edge_eps
            and abs(float(bb[3]) - float(maxy)) < edge_eps
        ):
            outer_border.add(i)

    inner = set(frame_set) - set(outer_border) if frame_set else set(range(len(items)))

    tx0 = float(maxx) - float(w) * float(title_ratio)
    ty1 = float(miny) + float(h) * float(title_ratio)
    zone_x0 = tx0 - edge_eps
    zone_y1 = ty1 + edge_eps

    def in_title_zone(bb) -> bool:
        cx = (float(bb[0]) + float(bb[2])) * 0.5
        cy = (float(bb[1]) + float(bb[3])) * 0.5
        return cx >= zone_x0 and cy <= zone_y1

    tb_seeds = []
    for i in inner:
        bb = items[i][1]
        if in_title_zone(bb):
            tb_seeds.append(i)

    title_set = set()
    if tb_seeds:
        q2 = list(dict.fromkeys(tb_seeds))
        title_set = set(q2)
        head = 0
        while head < len(q2):
            i = q2[head]
            head += 1
            e, _ = items[i]
            for x, y in entity_connection_points(e):
                try:
                    k = qkey(float(x), float(y), float(frame_tol))
                except Exception:
                    continue
                for j in buckets.get(k, []):
                    if j not in inner:
                        continue
                    if j in title_set:
                        continue
                    if not in_title_zone(items[j][1]):
                        continue
                    title_set.add(j)
                    q2.append(j)

    title_bbox = None
    if title_set:
        it = iter(title_set)
        first = next(it)
        bb = items[first][1]
        for i in it:
            bb = bbox_union(bb, items[i][1])
        title_bbox = bb

    if title_bbox is not None:
        tw = float(title_bbox[2]) - float(title_bbox[0])
        th = float(title_bbox[3]) - float(title_bbox[1])
        if tw > w * 0.8 or th > h * 0.8:
            title_bbox = None

    exclude_handles = set()
    for i in outer_border:
        try:
            exclude_handles.add(str(items[i][0].dxf.handle))
        except Exception:
            pass
    for i in title_set:
        try:
            exclude_handles.add(str(items[i][0].dxf.handle))
        except Exception:
            pass

    for e in msp:
        if not is_continuous_linetype(doc, e):
            continue
        bb = entity_bbox(doc, e)
        if bb is None:
            continue
        if (
            abs(float(bb[0]) - float(minx)) < edge_eps
            and abs(float(bb[1]) - float(miny)) < edge_eps
            and abs(float(bb[2]) - float(maxx)) < edge_eps
            and abs(float(bb[3]) - float(maxy)) < edge_eps
            and bbox_diag(bb) > max(w, h) * 0.8
        ):
            try:
                exclude_handles.add(str(e.dxf.handle))
            except Exception:
                pass

    return exclude_handles, title_bbox


def bbox_min_distance(a, b) -> float:
    ax0, ay0, ax1, ay1 = a
    bx0, by0, bx1, by1 = b

    dx = 0.0
    if ax1 < bx0:
        dx = bx0 - ax1
    elif bx1 < ax0:
        dx = ax0 - bx1

    dy = 0.0
    if ay1 < by0:
        dy = by0 - ay1
    elif by1 < ay0:
        dy = ay0 - by1

    return math.hypot(dx, dy)


def bbox_should_merge(a, b, merge_gap: float, size_ratio: float = 0.0) -> bool:
    """
    判断两个bbox是否应该合并。
    size_ratio > 0 时，只有当两个bbox面积比 >= size_ratio 时才合并（一大一小）。
    面积比接近1说明大小相似，是两个独立图例，不应合并。
    """
    # 先检查大小差异（如果启用）
    if size_ratio > 0:
        area_a = max((a[2] - a[0]) * (a[3] - a[1]), 1e-9)
        area_b = max((b[2] - b[0]) * (b[3] - b[1]), 1e-9)
        ratio = max(area_a, area_b) / min(area_a, area_b)
        if ratio < size_ratio:
            # 大小相似，不合并
            return False

    if bbox_intersects(a, b):
        return True
    try:
        g = float(merge_gap)
    except Exception:
        return False
    if g <= 0.0:
        return False
    return bbox_min_distance(a, b) < g


def bbox_transform(m, bb):
    x0, y0, x1, y1 = bb
    pts = [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]
    xs = []
    ys = []
    for x, y in pts:
        p = m.transform((float(x), float(y), 0.0))
        xs.append(float(p[0]))
        ys.append(float(p[1]))
    return min(xs), min(ys), max(xs), max(ys)


def compute_bbox_for_indices(doc: ezdxf.EzDxf, entities, idxs, pad: float):
    minx = float("inf")
    miny = float("inf")
    maxx = float("-inf")
    maxy = float("-inf")

    for i in idxs:
        bb = entity_bbox(doc, entities[i])
        if bb is None:
            continue
        x0, y0, x1, y1 = bb
        minx = min(minx, x0)
        miny = min(miny, y0)
        maxx = max(maxx, x1)
        maxy = max(maxy, y1)

    if not math.isfinite(minx) or not math.isfinite(miny):
        return None

    return minx - pad, miny - pad, maxx + pad, maxy + pad


def compute_bbox_for_entity_bboxes(entity_bboxes, pad: float):
    minx = float("inf")
    miny = float("inf")
    maxx = float("-inf")
    maxy = float("-inf")

    for bb in entity_bboxes:
        x0, y0, x1, y1 = bb
        minx = min(minx, x0)
        miny = min(miny, y0)
        maxx = max(maxx, x1)
        maxy = max(maxy, y1)

    if not math.isfinite(minx) or not math.isfinite(miny):
        return None
    return minx - pad, miny - pad, maxx + pad, maxy + pad


def _bbox_close(a, b, eps: float = 1e-6) -> bool:
    return (
        math.isclose(a[0], b[0], abs_tol=eps)
        and math.isclose(a[1], b[1], abs_tol=eps)
        and math.isclose(a[2], b[2], abs_tol=eps)
        and math.isclose(a[3], b[3], abs_tol=eps)
    )


def refine_and_merge_bboxes(initial_bboxes, all_entity_bboxes, pad: float, merge_gap: float):
    cur_bboxes = sorted(list(initial_bboxes), key=lambda b: (b[0], b[1], b[2], b[3]))

    max_iters = 50
    it = 0
    while True:
        it += 1
        if it > max_iters:
            return cur_bboxes
        dummy_groups = [[i] for i in range(len(cur_bboxes))]
        _, merged_bboxes = merge_by_bbox_iterative(dummy_groups, cur_bboxes, merge_gap=merge_gap)

        refined_bboxes = []
        for bb in merged_bboxes:
            inside = [ebb for _, ebb in all_entity_bboxes if bbox_contains(bb, ebb)]
            new_bb = compute_bbox_for_entity_bboxes(inside, pad=pad)
            refined_bboxes.append(new_bb if new_bb is not None else bb)

        dummy_groups2 = [[i] for i in range(len(refined_bboxes))]
        _, stabilized_bboxes = merge_by_bbox_iterative(dummy_groups2, refined_bboxes, merge_gap=merge_gap)
        stabilized_bboxes = sorted(stabilized_bboxes, key=lambda b: (b[0], b[1], b[2], b[3]))

        if len(stabilized_bboxes) == len(cur_bboxes) and all(
            _bbox_close(stabilized_bboxes[i], cur_bboxes[i]) for i in range(len(cur_bboxes))
        ):
            return stabilized_bboxes

        cur_bboxes = stabilized_bboxes


def merge_by_bbox_iterative(groups, bboxes, merge_gap: float = 0.0, size_ratio: float = 0.0):
    cur_groups = [list(g) for g in groups]
    cur_bboxes = list(bboxes)

    while True:
        n = len(cur_bboxes)
        uf = UnionFind(n)
        for i in range(n):
            bi = cur_bboxes[i]
            for j in range(i + 1, n):
                if bbox_should_merge(bi, cur_bboxes[j], merge_gap=merge_gap, size_ratio=size_ratio):
                    uf.union(i, j)

        merged: dict[int, list[int]] = defaultdict(list)
        for i in range(n):
            merged[uf.find(i)].append(i)

        if len(merged) == n:
            return cur_groups, cur_bboxes

        new_groups = []
        new_bboxes = []
        roots = sorted(merged.keys(), key=lambda r: min(merged[r]))
        for r in roots:
            members = sorted(merged[r])
            g = []
            bb = cur_bboxes[members[0]]
            for m in members:
                g.extend(cur_groups[m])
            for m in members[1:]:
                bb = bbox_union(bb, cur_bboxes[m])
            new_groups.append(g)
            new_bboxes.append(bb)

        cur_groups = new_groups
        cur_bboxes = new_bboxes


def ensure_layer(doc, name: str, color: int):
    if name in doc.layers:
        return
    doc.layers.new(name, dxfattribs={"color": int(color)})


def group_green_entities(
    doc: ezdxf.EzDxf,
    tol: float,
    target_acis: set[int],
    max_entity_diag: float | None = None,
    exclude_handles: set[str] | None = None,
    exclude_bbox=None,
):
    msp = doc.modelspace()

    supported = {"LINE", "ARC", "CIRCLE", "LWPOLYLINE", "POLYLINE", "INSERT"}

    entities = []
    for e in msp:
        t = e.dxftype()
        if t not in supported:
            continue
        if exclude_handles is not None:
            try:
                if str(e.dxf.handle) in exclude_handles:
                    continue
            except Exception:
                pass
        if effective_aci_color(doc, e) not in target_acis:
            continue
        if not is_continuous_linetype(doc, e):
            continue
        bb = None
        if max_entity_diag is not None:
            try:
                md = float(max_entity_diag)
            except Exception:
                md = None
            if md is not None and md > 0.0:
                bb = entity_bbox(doc, e)
                if bb is None:
                    continue
                if bbox_diag(bb) > md:
                    continue
        if exclude_bbox is not None:
            if bb is None:
                bb = entity_bbox(doc, e)
            if bb is not None and bbox_contains(exclude_bbox, bb):
                continue
        entities.append(e)

    uf = UnionFind(len(entities))

    point_buckets: dict[tuple[int, int], list[tuple[float, float, int]]] = defaultdict(list)
    first_owner: dict[tuple[int, int], int] = {}

    circle_indices = []

    for idx, e in enumerate(entities):
        if e.dxftype() == "CIRCLE":
            circle_indices.append(idx)
            continue
        for (x, y) in entity_connection_points(e):
            k = qkey(x, y, tol)
            if k in first_owner:
                uf.union(idx, first_owner[k])
            else:
                first_owner[k] = idx
            point_buckets[k].append((x, y, idx))

    for idx in circle_indices:
        e = entities[idx]
        c = e.dxf.center
        r = float(e.dxf.radius)
        cx, cy = float(c[0]), float(c[1])
        minx, miny = cx - r - tol, cy - r - tol
        maxx, maxy = cx + r + tol, cy + r + tol

        ix0, iy0 = qkey(minx, miny, tol)
        ix1, iy1 = qkey(maxx, maxy, tol)

        for ix in range(min(ix0, ix1), max(ix0, ix1) + 1):
            for iy in range(min(iy0, iy1), max(iy0, iy1) + 1):
                k = (ix, iy)
                if k not in point_buckets:
                    continue
                for px, py, other_idx in point_buckets[k]:
                    d = math.hypot(px - cx, py - cy)
                    if abs(d - r) <= tol:
                        uf.union(idx, other_idx)

    groups: dict[int, list[int]] = defaultdict(list)
    for i in range(len(entities)):
        groups[uf.find(i)].append(i)

    return entities, list(groups.values())


def read_dwgcodepage_from_text_dxf(path: str) -> str | None:
    try:
        with open(path, "r", encoding="latin1", errors="ignore") as f:
            lines = []
            for _ in range(5000):
                line = f.readline()
                if not line:
                    break
                lines.append(line.rstrip("\n"))
    except Exception:
        return None

    for i in range(len(lines) - 3):
        if lines[i].strip() == "9" and lines[i + 1].strip() == "$DWGCODEPAGE":
            if lines[i + 2].strip() == "3":
                return lines[i + 3].strip()
    return None


def codepage_to_python_encoding(raw_codepage: str | None) -> str | None:
    if not raw_codepage:
        return None
    cp = str(raw_codepage).strip().lower()
    if cp in {"gb2312", "gbk", "gb18030"}:
        return "cp936"
    if cp == "ansi_936":
        return "cp936"
    if cp == "utf-8" or cp == "utf8":
        return "utf-8"
    return None


def read_dxf_auto_encoding(path: str, raw_codepage: str | None) -> ezdxf.EzDxf:
    cp_enc = codepage_to_python_encoding(raw_codepage)
    encs: list[str] = []
    for enc in ("utf-8", cp_enc, "latin1"):
        if not enc:
            continue
        if enc not in encs:
            encs.append(enc)

    for enc in encs:
        try:
            return ezdxf.readfile(path, encoding=enc)
        except UnicodeDecodeError:
            continue

    return ezdxf.readfile(path, encoding="latin1")


def apply_output_codepage(doc: ezdxf.EzDxf, raw_codepage: str | None) -> None:
    if not raw_codepage:
        return
    cp = str(raw_codepage).strip()
    if cp.lower() in {"gb2312", "gbk", "gb18030"}:
        cp = "ANSI_936"

    try:
        doc.header["$DWGCODEPAGE"] = cp
    except Exception:
        pass

    if cp.upper() == "ANSI_936":
        for enc in ("cp936", "gb2312"):
            try:
                doc.encoding = enc
                break
            except Exception:
                continue


def patch_dwgcodepage_in_text_dxf(path: str, new_value: str) -> None:
    try:
        with open(path, "r", encoding="latin1", errors="ignore") as f:
            lines = f.readlines()
    except Exception:
        return

    replaced = False
    for i in range(len(lines) - 3):
        if lines[i].strip() == "9" and lines[i + 1].strip() == "$DWGCODEPAGE" and lines[i + 2].strip() == "3":
            lines[i + 3] = str(new_value).strip() + "\n"
            replaced = True
            break

    if not replaced:
        return

    with open(path, "w", encoding="latin1", errors="ignore", newline="") as f:
        f.writelines(lines)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("input", help="input dxf path")
    ap.add_argument("-o", "--output", default=None, help="output dxf path")
    ap.add_argument("--tol", type=float, default=0.1)
    ap.add_argument("--color", action="append", default=None)
    ap.add_argument("--merge-gap", type=float, default=5.0)
    ap.add_argument("--cross-merge-gap", type=float, default=0.0)
    ap.add_argument("--max-entity-diag", type=float, default=100.0)
    ap.add_argument("--keep-frame", action="store_true")
    ap.add_argument("--frame-tol", type=float, default=None)
    ap.add_argument("--title-ratio", type=float, default=0.3)
    ap.add_argument("--bbox-layer", default="GREEN_GROUP_BBOX")
    ap.add_argument("--bbox-color", type=int, default=2)
    ap.add_argument("--label-height", type=float, default=2.5)
    ap.add_argument("--pad", type=float, default=None)
    ap.add_argument("--corridor-width", type=float, default=100.0, help="左侧走廊宽度，用于搜索图例描述文字")
    ap.add_argument("--corridor-y-pad", type=float, default=10.0, help="走廊上下扩展量")
    ap.add_argument("--text-color", type=int, default=2, help="图例描述文字颜色(ACI)，默认2=黄色")
    ap.add_argument("--merge-size-ratio", type=float, default=2.0, help="合并时要求的面积比阈值，>=此值才合并(一大一小)，<此值不合并(大小相似)")
    ap.add_argument("--export-csv", type=str, default=None, help="导出图例信息到CSV文件")
    ap.add_argument("--export-legend-info-json", type=str, default=None, help="导出图例信息JSON，例如 图例信息.json")
    ap.add_argument("--export-legend-index-json", type=str, default=None, help="导出图例几何索引JSON(方案2)，例如 legend_index.json")
    ap.add_argument("--export-legend-geom-dir", type=str, default=None, help="导出图例几何文件目录(方案2)，例如 legend_geoms")
    ap.add_argument("--legend-quant-grid", type=float, default=0.02, help="图例几何量化网格(归一化后)，越小越敏感")

    ap.add_argument("--match-legend-desc", type=str, default=None, help="匹配模式：要匹配的图例描述，例如 手动阀")
    ap.add_argument("--match-all-legends", action="store_true", help="匹配模式：放开类型，匹配参考CSV中的全部图例描述")
    ap.add_argument("--legend-index-json", type=str, default=None, help="匹配模式：图例几何索引JSON，例如 legend_index.json")
    ap.add_argument("--legend-geom-dir", type=str, default=None, help="匹配模式：图例几何目录，例如 legend_geoms")
    ap.add_argument("--match-ref-dxf", type=str, default=None, help="匹配模式(方案A)：参考图例DXF，例如 2244原理图图例.dxf")
    ap.add_argument("--match-ref-csv", type=str, default=None, help="匹配模式(方案A)：参考图例CSV，例如 legend_output_b.csv")
    ap.add_argument("--legend-info-json", type=str, default=None, help="匹配模式：图例信息JSON，例如 图例信息.json")
    ap.add_argument("--match-phash-max-dist", type=int, default=12, help="匹配模式：pHash最大汉明距离阈值，越小越严格")
    ap.add_argument("--match-top-k", type=int, default=0, help="匹配模式：若阈值下无命中，取最相似的前K个进行标注(0=不启用)")
    ap.add_argument("--match-bbox-layer", default="LEGEND_MATCH", help="匹配模式：标注图层")
    ap.add_argument("--match-bbox-color", type=int, default=4, help="匹配模式：标注颜色(ACI)，默认4=青色/浅蓝")
    ap.add_argument("--match-label-height", type=float, default=2.5, help="匹配模式：标注文字高度")

    args = ap.parse_args()

    in_path = args.input
    out_path = args.output
    if out_path is None:
        if args.match_legend_desc:
            base_dir = os.path.dirname(str(in_path))
            out_path = os.path.join(base_dir, f"{str(args.match_legend_desc)}_标注.dxf")
        elif args.match_all_legends:
            base_dir = os.path.dirname(str(in_path))
            base_name = os.path.splitext(os.path.basename(str(in_path)))[0]
            out_path = os.path.join(base_dir, f"{base_name}_全部图例_标注.dxf")
        elif in_path.lower().endswith(".dxf"):
            out_path = in_path[:-4] + "_green_groups_bbox_merged.dxf"
        else:
            out_path = in_path + "_green_groups_bbox_merged.dxf"

    tol = float(args.tol)
    pad = float(args.pad) if args.pad is not None else tol

    raw_codepage = read_dwgcodepage_from_text_dxf(in_path)

    doc = read_dxf_auto_encoding(in_path, raw_codepage)

    target_acis = set()
    if args.color is None:
        target_acis.add(3)
    else:
        for item in args.color:
            for part in str(item).split(","):
                s = part.strip()
                if not s:
                    continue
                try:
                    target_acis.add(int(s))
                except Exception:
                    continue

    if args.match_legend_desc or args.match_all_legends:
        ensure_layer(doc, str(args.match_bbox_layer), int(args.match_bbox_color))
    else:
        ensure_layer(doc, args.bbox_layer, int(args.bbox_color))

    msp = doc.modelspace()

    exclude_handles = set()
    title_bbox = None
    if not bool(args.keep_frame):
        frame_tol = float(args.frame_tol) if args.frame_tol is not None else max(tol * 2.0, 0.5)
        exclude_handles, title_bbox = detect_frame_and_titleblock(
            doc,
            tol=tol,
            frame_tol=frame_tol,
            title_ratio=float(args.title_ratio),
        )

    if args.match_legend_desc:
        want_desc = str(args.match_legend_desc).strip()
        base_dir = os.path.dirname(str(in_path))
        ref_dxf_required = os.path.join(base_dir, "图例.dxf")
        if args.match_ref_dxf and os.path.abspath(str(args.match_ref_dxf)) != os.path.abspath(ref_dxf_required):
            raise SystemExit("匹配模式：强制只允许使用同目录 图例.dxf 作为参考图例DXF")
        if not os.path.exists(ref_dxf_required):
            raise SystemExit(f"匹配模式：未找到参考图例DXF: {ref_dxf_required}")

        refs: list[dict] = []
        info_path = str(args.legend_info_json or os.path.join(base_dir, "图例信息.json"))
        if info_path and os.path.exists(info_path):
            refs = _load_legend_refs_from_info_json(info_path, want_desc)

        if not refs:
            index_path = str(args.legend_index_json or "")
            geom_dir = str(args.legend_geom_dir or "")
            if index_path and os.path.exists(index_path):
                refs = _load_legend_refs_from_index(index_path, geom_dir if geom_dir else None, want_desc)

        if not refs:
            print(f"匹配模式：未在 图例信息.json/索引 中找到图例描述: {want_desc}，请先从 图例.dxf 生成 图例信息.json")
        else:
            ref_phashes: list[str] = []
            ref_stats: list[dict] = []
            ref_gids: list[int] = []
            ref_sigs: list[str] = []
            for it in refs:
                ph = it.get("phash")
                st = it.get("stats")
                sig = str(it.get("text_sig") or "").strip().upper()
                if ph:
                    ref_phashes.append(str(ph))
                    ref_stats.append(st if isinstance(st, dict) else {})
                    try:
                        ref_gids.append(int(it.get("ref_gid", 0)))
                    except Exception:
                        ref_gids.append(0)
                    ref_sigs.append(sig)
            if not ref_phashes:
                print(f"匹配模式：参考图例缺少phash，无法匹配: {want_desc}")
            else:
                candidates = []
                scanned = 0
                for e in msp:
                    if e.dxftype() != "INSERT":
                        continue
                    if exclude_handles:
                        try:
                            if str(e.dxf.handle) in exclude_handles:
                                continue
                        except Exception:
                            pass
                    if effective_aci_color(doc, e) not in target_acis:
                        continue
                    if not is_continuous_linetype(doc, e):
                        continue
                    bb = entity_bbox(doc, e)
                    if bb is None:
                        continue
                    if title_bbox is not None and bbox_contains(title_bbox, bb):
                        continue
                    try:
                        md = float(args.max_entity_diag)
                    except Exception:
                        md = None
                    if md is not None and md > 0.0:
                        if bbox_diag(bb) > md:
                            continue

                    scanned += 1
                    geom = _compute_geom_from_entity(doc, e, quant_grid=float(args.legend_quant_grid))
                    if geom is None:
                        continue
                    cand_phash = str(geom.get("phash") or "")
                    cand_stats = geom.get("stats")
                    cand_sig = _extract_internal_single_letter_sig_from_insert(doc, e, bb, target_acis=target_acis)
                    best = None
                    best_key = None
                    for ridx, (rph, rst, rgid, rsig) in enumerate(zip(ref_phashes, ref_stats, ref_gids, ref_sigs), start=1):
                        if rsig and cand_sig != rsig:
                            continue
                        d = _phash_hamming(cand_phash, rph)
                        sscore = _stats_similarity_score(rst, cand_stats)
                        try:
                            rn = int(rst.get("n_primitives", 0))
                            cn = int((cand_stats or {}).get("n_primitives", 0))
                        except Exception:
                            rn = 0
                            cn = 0
                        if rn > 0 and cn > 0:
                            ratio = float(cn) / float(rn)
                            if ratio < 0.5 or ratio > 2.0:
                                continue
                        key = (int(d), int(sscore))
                        if best_key is None or key < best_key:
                            best_key = key
                            best = {
                                "entity": e,
                                "bbox": bb,
                                "dist": int(d),
                                "stats_score": int(sscore),
                                "ref_idx": int(ridx),
                                "ref_gid": int(rgid),
                            }
                    if best is not None:
                        candidates.append(best)

                candidates_sorted = sorted(candidates, key=lambda r: (int(r.get("dist", 10**9)), int(r.get("stats_score", 10**9))))
                try:
                    max_d = int(args.match_phash_max_dist)
                except Exception:
                    max_d = 10
                matches = [r for r in candidates_sorted if int(r.get("dist", 10**9)) <= max_d]
                if not matches:
                    try:
                        top_k = int(args.match_top_k)
                    except Exception:
                        top_k = 0
                    if top_k > 0:
                        matches = candidates_sorted[:top_k]

                print(f"匹配模式：候选INSERT={scanned} 计算指纹={len(candidates_sorted)} 命中={len(matches)}")

                for i, r in enumerate(matches, start=1):
                    x0, y0, x1, y1 = r["bbox"]
                    pad_w = max(0.5, float(args.match_label_height) * 0.4)
                    amp = min(5.0, max(0.8, pad_w))
                    wl = max(6.0, amp * 6.0)
                    wave_pts = _wave_bbox_points((x0, y0, x1, y1), pad=pad_w, amplitude=amp, wavelength=wl)
                    if wave_pts:
                        msp.add_lwpolyline(
                            wave_pts,
                            format="xy",
                            close=True,
                            dxfattribs={"layer": str(args.match_bbox_layer), "color": int(args.match_bbox_color)},
                        )
                    else:
                        msp.add_lwpolyline(
                            [(x0, y0), (x1, y0), (x1, y1), (x0, y1)],
                            format="xy",
                            close=True,
                            dxfattribs={"layer": str(args.match_bbox_layer), "color": int(args.match_bbox_color)},
                        )
                    rgid = int(r.get("ref_gid", 0))
                    if rgid:
                        label_text = f"{i}: {want_desc} d={int(r.get('dist', 0))} r{int(r.get('ref_idx', 0))}[{rgid}]"
                    else:
                        label_text = f"{i}: {want_desc} d={int(r.get('dist', 0))} r{int(r.get('ref_idx', 0))}"
                    t = msp.add_text(
                        label_text,
                        dxfattribs={
                            "layer": str(args.match_bbox_layer),
                            "height": float(args.match_label_height),
                            "color": int(args.match_bbox_color),
                        },
                    )
                    t.dxf.insert = (x0 - pad_w, y1 + pad_w)

        apply_output_codepage(doc, raw_codepage)
        doc.saveas(out_path, encoding="utf-8")
        patch_dwgcodepage_in_text_dxf(out_path, "UTF-8")
        return

    if args.match_all_legends:
        base_dir = os.path.dirname(str(in_path))
        ref_dxf_required = os.path.join(base_dir, "图例.dxf")
        if args.match_ref_dxf and os.path.abspath(str(args.match_ref_dxf)) != os.path.abspath(ref_dxf_required):
            raise SystemExit("匹配模式：强制只允许使用同目录 图例.dxf 作为参考图例DXF")
        if not os.path.exists(ref_dxf_required):
            raise SystemExit(f"匹配模式：未找到参考图例DXF: {ref_dxf_required}")

        refs: list[dict] = []
        info_path = str(args.legend_info_json or os.path.join(base_dir, "图例信息.json"))
        if info_path and os.path.exists(info_path):
            refs = _load_legend_refs_all_from_info_json(info_path)

        if not refs:
            index_path = str(args.legend_index_json or "")
            geom_dir = str(args.legend_geom_dir or "")
            if index_path and os.path.exists(index_path):
                refs = _load_legend_refs_all_from_index(index_path, geom_dir if geom_dir else None)

        if not refs:
            print("匹配模式：未加载到任何参考图例（请先从 图例.dxf 生成 图例信息.json）")
        else:
            ref_phashes: list[str] = []
            ref_stats: list[dict] = []
            ref_descs: list[str] = []
            ref_gids: list[int] = []
            ref_sigs: list[str] = []
            for it in refs:
                ph = it.get("phash")
                st = it.get("stats")
                desc = str(it.get("desc") or "").strip()
                sig = str(it.get("text_sig") or "").strip().upper()
                if not ph or not desc:
                    continue
                ref_phashes.append(str(ph))
                ref_stats.append(st if isinstance(st, dict) else {})
                ref_descs.append(desc)
                try:
                    ref_gids.append(int(it.get("ref_gid", 0)))
                except Exception:
                    ref_gids.append(0)
                ref_sigs.append(sig)

            if not ref_phashes:
                print("匹配模式：参考图例缺少phash，无法匹配")
            else:
                candidates = []
                scanned = 0
                for e in msp:
                    if e.dxftype() != "INSERT":
                        continue
                    if exclude_handles:
                        try:
                            if str(e.dxf.handle) in exclude_handles:
                                continue
                        except Exception:
                            pass
                    if effective_aci_color(doc, e) not in target_acis:
                        continue
                    if not is_continuous_linetype(doc, e):
                        continue
                    bb = entity_bbox(doc, e)
                    if bb is None:
                        continue
                    if title_bbox is not None and bbox_contains(title_bbox, bb):
                        continue
                    try:
                        md = float(args.max_entity_diag)
                    except Exception:
                        md = None
                    if md is not None and md > 0.0:
                        if bbox_diag(bb) > md:
                            continue

                    scanned += 1
                    geom = _compute_geom_from_entity(doc, e, quant_grid=float(args.legend_quant_grid))
                    if geom is None:
                        continue
                    cand_phash = str(geom.get("phash") or "")
                    cand_stats = geom.get("stats")
                    cand_sig = _extract_internal_single_letter_sig_from_insert(doc, e, bb, target_acis=target_acis)

                    best = None
                    best_key = None
                    for ridx, (rph, rst, rdesc, rgid, rsig) in enumerate(
                        zip(ref_phashes, ref_stats, ref_descs, ref_gids, ref_sigs), start=1
                    ):
                        if rsig and cand_sig != rsig:
                            continue
                        d = _phash_hamming(cand_phash, rph)
                        sscore = _stats_similarity_score(rst, cand_stats)
                        try:
                            rn = int(rst.get("n_primitives", 0))
                            cn = int((cand_stats or {}).get("n_primitives", 0))
                        except Exception:
                            rn = 0
                            cn = 0
                        if rn > 0 and cn > 0:
                            ratio = float(cn) / float(rn)
                            if ratio < 0.5 or ratio > 2.0:
                                continue
                        key = (int(d), int(sscore))
                        if best_key is None or key < best_key:
                            best_key = key
                            best = {
                                "entity": e,
                                "bbox": bb,
                                "dist": int(d),
                                "stats_score": int(sscore),
                                "ref_idx": int(ridx),
                                "ref_gid": int(rgid),
                                "desc": str(rdesc),
                            }
                    if best is not None:
                        candidates.append(best)

                candidates_sorted = sorted(
                    candidates,
                    key=lambda r: (
                        int(r.get("dist", 10**9)),
                        int(r.get("stats_score", 10**9)),
                    ),
                )
                try:
                    max_d = int(args.match_phash_max_dist)
                except Exception:
                    max_d = 10
                matches = [r for r in candidates_sorted if int(r.get("dist", 10**9)) <= max_d]
                if not matches:
                    try:
                        top_k = int(args.match_top_k)
                    except Exception:
                        top_k = 0
                    if top_k > 0:
                        matches = candidates_sorted[:top_k]

                print(
                    f"匹配模式(全类型)：参考={len(ref_phashes)} 候选INSERT={scanned} 计算指纹={len(candidates_sorted)} 命中={len(matches)}"
                )

                for i, r in enumerate(matches, start=1):
                    x0, y0, x1, y1 = r["bbox"]
                    pad_w = max(0.5, float(args.match_label_height) * 0.4)
                    amp = min(5.0, max(0.8, pad_w))
                    wl = max(6.0, amp * 6.0)
                    wave_pts = _wave_bbox_points((x0, y0, x1, y1), pad=pad_w, amplitude=amp, wavelength=wl)
                    if wave_pts:
                        msp.add_lwpolyline(
                            wave_pts,
                            format="xy",
                            close=True,
                            dxfattribs={"layer": str(args.match_bbox_layer), "color": int(args.match_bbox_color)},
                        )
                    else:
                        msp.add_lwpolyline(
                            [(x0, y0), (x1, y0), (x1, y1), (x0, y1)],
                            format="xy",
                            close=True,
                            dxfattribs={"layer": str(args.match_bbox_layer), "color": int(args.match_bbox_color)},
                        )

                    desc = str(r.get("desc") or "")
                    rgid = int(r.get("ref_gid", 0))
                    if rgid:
                        label_text = f"{i}: {desc} d={int(r.get('dist', 0))} r{int(r.get('ref_idx', 0))}[{rgid}]"
                    else:
                        label_text = f"{i}: {desc} d={int(r.get('dist', 0))} r{int(r.get('ref_idx', 0))}"
                    t = msp.add_text(
                        label_text,
                        dxfattribs={
                            "layer": str(args.match_bbox_layer),
                            "height": float(args.match_label_height),
                            "color": int(args.match_bbox_color),
                        },
                    )
                    t.dxf.insert = (x0 - pad_w, y1 + pad_w)

        apply_output_codepage(doc, raw_codepage)
        doc.saveas(out_path, encoding="utf-8")
        patch_dwgcodepage_in_text_dxf(out_path, "UTF-8")
        return

    all_entities = list(msp)
    all_entity_bboxes = []
    text_entities = []
    obstacle_entities = []
    text_types = {"TEXT", "MTEXT"}

    for e in all_entities:
        if exclude_handles:
            try:
                if str(e.dxf.handle) in exclude_handles:
                    continue
            except Exception:
                pass
        ebb = entity_bbox_loose(doc, e)
        if ebb is None:
            pass
        elif title_bbox is not None and bbox_contains(title_bbox, ebb):
            pass
        else:
            all_entity_bboxes.append((e, ebb))

        t = e.dxftype()
        if t in text_types:
            tbb, tcontent = text_entity_bbox_and_content(doc, e)
            if tbb is not None and tcontent:
                if title_bbox is None or not bbox_contains(title_bbox, tbb):
                    text_entities.append((e, tbb, tcontent))
        else:
            e_color = effective_aci_color(doc, e)
            if e_color not in target_acis:
                obs_bb = entity_bbox_loose(doc, e)
                if obs_bb is not None:
                    if bbox_diag(obs_bb) > float(args.max_entity_diag):
                        continue
                    if title_bbox is None or not bbox_contains(title_bbox, obs_bb):
                        obstacle_entities.append((e, obs_bb))

    colors = sorted(target_acis)
    per_color_bboxes = []
    for c in colors:
        entities, groups = group_green_entities(
            doc,
            tol=tol,
            target_acis={c},
            max_entity_diag=float(args.max_entity_diag),
            exclude_handles=exclude_handles if exclude_handles else None,
            exclude_bbox=title_bbox,
        )

        group_bboxes = []
        group_entities = []
        for idxs in groups:
            bb = compute_bbox_for_indices(doc, entities, idxs, pad=pad)
            if bb is None:
                continue
            group_entities.append(idxs)
            group_bboxes.append(bb)

        _, merged_bboxes = merge_by_bbox_iterative(group_entities, group_bboxes, merge_gap=float(args.merge_gap), size_ratio=float(args.merge_size_ratio))

        final_bboxes_c = refine_and_merge_bboxes(
            merged_bboxes,
            all_entity_bboxes,
            pad=pad,
            merge_gap=float(args.merge_gap),
        )
        per_color_bboxes.extend(final_bboxes_c)

    final_bboxes = per_color_bboxes

    legend_results = []
    for gid, bb in enumerate(final_bboxes, start=1):
        x0, y0, x1, y1 = bb

        desc, desc_bbs = find_legend_description(
            doc,
            bb,
            text_entities,
            obstacle_entities,
            corridor_width=float(args.corridor_width),
            y_pad=float(args.corridor_y_pad),
            text_color=int(args.text_color),
        )

        legend_results.append((gid, bb, desc))

        msp.add_lwpolyline(
            [(x0, y0), (x1, y0), (x1, y1), (x0, y1)],
            format="xy",
            close=True,
            dxfattribs={"layer": args.bbox_layer, "color": int(args.bbox_color)},
        )

        if desc:
            label_text = f"{gid}: {desc}"
        else:
            label_text = str(gid)

        t = msp.add_text(
            label_text,
            dxfattribs={
                "layer": args.bbox_layer,
                "height": float(args.label_height),
                "color": int(args.bbox_color),
            },
        )
        t.dxf.insert = (x0, y1)

    print(f"\n=== 图例提取结果 ({len(final_bboxes)} 个符号) ===")
    valid_count = 0
    for gid, bb, desc in legend_results:
        if desc:
            valid_count += 1
            print(f"  [{gid}] {desc}  bbox={bb}")
        else:
            print(f"  [{gid}] (无描述/被阻挡)  bbox={bb}")
    print(f"=== 有效图例: {valid_count} / {len(final_bboxes)} ===\n")

    if args.export_csv:
        import csv
        csv_path = args.export_csv
        with open(csv_path, "w", newline="", encoding="utf-8-sig") as csvfile:
            writer = csv.writer(csvfile)
            writer.writerow(["序号", "描述", "x0", "y0", "x1", "y1"])
            for gid, bb, desc in legend_results:
                writer.writerow([gid, desc if desc else "", bb[0], bb[1], bb[2], bb[3]])
        print(f"已导出CSV: {csv_path}")

    if args.export_legend_index_json and args.export_legend_geom_dir:
        export_legend_geometry_library(
            doc,
            in_path=in_path,
            legend_results=legend_results,
            target_acis=target_acis,
            index_json_path=str(args.export_legend_index_json),
            geom_dir=str(args.export_legend_geom_dir),
            exclude_handles=exclude_handles if exclude_handles else None,
            max_entity_diag=float(args.max_entity_diag),
            quant_grid=float(args.legend_quant_grid),
        )
        print(f"已导出图例几何库: {args.export_legend_index_json} + {args.export_legend_geom_dir}")

    if args.export_legend_info_json:
        export_legend_info_json(
            doc,
            in_path=in_path,
            legend_results=legend_results,
            target_acis=target_acis,
            info_json_path=str(args.export_legend_info_json),
            exclude_handles=exclude_handles if exclude_handles else None,
            max_entity_diag=float(args.max_entity_diag),
            quant_grid=float(args.legend_quant_grid),
        )
        print(f"已导出图例信息JSON: {args.export_legend_info_json}")

    apply_output_codepage(doc, raw_codepage)

    doc.saveas(out_path, encoding="utf-8")

    patch_dwgcodepage_in_text_dxf(out_path, "UTF-8")


if __name__ == "__main__":
    main()
