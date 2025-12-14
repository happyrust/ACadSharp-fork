import argparse
import math
from collections import defaultdict

import ezdxf


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


def bbox_should_merge(a, b, merge_gap: float) -> bool:
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


def merge_by_bbox_iterative(groups, bboxes, merge_gap: float = 0.0):
    cur_groups = [list(g) for g in groups]
    cur_bboxes = list(bboxes)

    while True:
        n = len(cur_bboxes)
        uf = UnionFind(n)
        for i in range(n):
            bi = cur_bboxes[i]
            for j in range(i + 1, n):
                if bbox_should_merge(bi, cur_bboxes[j], merge_gap=merge_gap):
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

    args = ap.parse_args()

    in_path = args.input
    out_path = args.output
    if out_path is None:
        if in_path.lower().endswith(".dxf"):
            out_path = in_path[:-4] + "_green_groups_bbox_merged.dxf"
        else:
            out_path = in_path + "_green_groups_bbox_merged.dxf"

    tol = float(args.tol)
    pad = float(args.pad) if args.pad is not None else tol

    raw_codepage = read_dwgcodepage_from_text_dxf(in_path)

    input_encoding = codepage_to_python_encoding(raw_codepage)
    if input_encoding is not None:
        doc = ezdxf.readfile(in_path, encoding=input_encoding)
    else:
        doc = ezdxf.readfile(in_path)

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

    all_entities = list(msp)
    all_entity_bboxes = []
    for e in all_entities:
        if exclude_handles:
            try:
                if str(e.dxf.handle) in exclude_handles:
                    continue
            except Exception:
                pass
        ebb = entity_bbox_loose(doc, e)
        if ebb is None:
            continue
        if title_bbox is not None and bbox_contains(title_bbox, ebb):
            continue
        all_entity_bboxes.append((e, ebb))

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

        _, merged_bboxes = merge_by_bbox_iterative(group_entities, group_bboxes, merge_gap=float(args.merge_gap))

        final_bboxes_c = refine_and_merge_bboxes(
            merged_bboxes,
            all_entity_bboxes,
            pad=pad,
            merge_gap=float(args.merge_gap),
        )
        per_color_bboxes.extend(final_bboxes_c)

    final_bboxes = per_color_bboxes

    for gid, bb in enumerate(final_bboxes, start=1):
        x0, y0, x1, y1 = bb

        msp.add_lwpolyline(
            [(x0, y0), (x1, y0), (x1, y1), (x0, y1)],
            format="xy",
            close=True,
            dxfattribs={"layer": args.bbox_layer, "color": int(args.bbox_color)},
        )

        t = msp.add_text(
            str(gid),
            dxfattribs={
                "layer": args.bbox_layer,
                "height": float(args.label_height),
                "color": int(args.bbox_color),
            },
        )
        t.dxf.insert = (x0, y1)

    if input_encoding is not None:
        try:
            doc.encoding = input_encoding
        except Exception:
            pass

    apply_output_codepage(doc, raw_codepage)

    save_encoding = input_encoding
    if save_encoding is None and raw_codepage is not None:
        save_encoding = codepage_to_python_encoding(raw_codepage)

    if save_encoding is not None:
        doc.saveas(out_path, encoding=save_encoding)
    else:
        doc.saveas(out_path)

    patch_dwgcodepage_in_text_dxf(out_path, "UTF-8")


if __name__ == "__main__":
    main()
