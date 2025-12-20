#!/usr/bin/env python3
# -*- coding: utf-8 -*-

from __future__ import annotations

import argparse
import json
import shutil
import sqlite3
from datetime import datetime, timezone
from pathlib import Path

import ezdxf

from symbol_lib import (
	HAN_RE,
	bbox_of_entities,
	compute_descriptor,
	encode_point_cloud,
	flatten_entities,
	save_index,
)


def has_han(text: str) -> bool:
	return bool(HAN_RE.search(text))


def build_index(
	doc: ezdxf.EzdxfDocument,
	*,
	only_han_blocks: bool,
	with_point_cloud: bool,
	sample_div: float,
	max_points: int,
	point_scale: int,
	max_depth: int,
) -> dict:
	symbols: list[dict] = []
	for blk in doc.blocks:
		name = blk.name
		if name.startswith("*"):
			continue
		if only_han_blocks and not has_han(name):
			continue

		raw_ents = list(blk)
		flat = flatten_entities(raw_ents, doc, max_depth=max_depth)
		mb = bbox_of_entities(flat)
		if mb is None:
			continue

		desc = compute_descriptor(flat, mb)
		norm = max(mb[2] - mb[0], mb[3] - mb[1]) or 1.0

		item: dict = {
			"name": name,
			"block_name": name,
			"bbox": [mb[0], mb[1], mb[2], mb[3]],
			"norm": float(norm),
			"descriptor": desc.to_dict(),
		}

		if with_point_cloud:
			from symbol_lib import point_cloud_from_entities

			pts = point_cloud_from_entities(flat, mb, sample_div=sample_div, max_points=max_points)
			item["point_cloud"] = encode_point_cloud(pts, scale=point_scale)

		symbols.append(item)

	return {
		"schema": 3 if with_point_cloud else 2,
		"created_at": datetime.now(timezone.utc).isoformat(),
		"symbols": symbols,
	}


def write_sqlite(db_path: Path, index: dict, blocks_dxf_path: Path) -> None:
	db_path.parent.mkdir(parents=True, exist_ok=True)
	conn = sqlite3.connect(str(db_path))
	try:
		cur = conn.cursor()
		cur.execute(
			"""
DROP TABLE IF EXISTS symbols;
"""
		)
		cur.execute(
			"""
CREATE TABLE symbols (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  name TEXT NOT NULL,
  block_name TEXT NOT NULL,
  bbox_minx REAL,
  bbox_miny REAL,
  bbox_maxx REAL,
  bbox_maxy REAL,
  norm REAL,
  descriptor_json TEXT NOT NULL,
  point_cloud_blob BLOB,
  point_cloud_n INTEGER,
  point_cloud_scale INTEGER,
  blocks_dxf_path TEXT NOT NULL,
  base_point TEXT NOT NULL,
  created_at TEXT NOT NULL
);
"""
		)
		for sym in index.get("symbols", []):
			bbox = sym.get("bbox") or [None, None, None, None]
			pc = sym.get("point_cloud") or {}
			blob = None
			if pc.get("data_b64"):
				import base64

				try:
					blob = base64.b64decode(str(pc.get("data_b64")).encode("ascii"))
				except Exception:
					blob = None
			cur.execute(
				"""
INSERT INTO symbols(
  name, block_name, bbox_minx, bbox_miny, bbox_maxx, bbox_maxy,
  norm, descriptor_json,
  point_cloud_blob, point_cloud_n, point_cloud_scale,
  blocks_dxf_path, base_point, created_at
) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?);
""",
				(
					sym.get("name"),
					sym.get("block_name"),
					bbox[0],
					bbox[1],
					bbox[2],
					bbox[3],
					sym.get("norm"),
					json.dumps(sym.get("descriptor"), ensure_ascii=False),
					blob,
					pc.get("n") if pc else None,
					pc.get("scale") if pc else None,
					str(blocks_dxf_path),
					index.get("base_point") or "center",
					index.get("created_at"),
				),
			)
		conn.commit()
	finally:
		conn.close()


def main() -> int:
	parser = argparse.ArgumentParser(description="从 blocks.dxf 构建符号库索引（JSON / SQLite）")
	parser.add_argument("blocks_dxf", help="包含符号 blocks 的 DXF（例如 legend_to_blocks 产物）")
	parser.add_argument("--out-dir", required=True, help="输出目录（将写入 index.json 和 blocks.dxf 副本）")
	parser.add_argument("--only-han-blocks", action="store_true", help="仅收录 block 名含中文的符号（默认启用）")
	parser.add_argument("--include-non-han", action="store_true", help="也收录非中文 block（用于包含英文符号名）")
	parser.add_argument(
		"--base-point",
		choices=("center", "min"),
		default="center",
		help="符号库中 block 的基点（用于识别/替换时决定插入点与旋转中心）",
	)
	parser.add_argument("--with-point-cloud", action="store_true", help="在 index.json/SQLite 中写入点云（用于精匹配与入库）")
	parser.add_argument("--sample-div", type=float, default=30.0, help="点云采样密度：max_dim / sample_div 作为步长")
	parser.add_argument("--max-points", type=int, default=600, help="点云最大点数（下采样）")
	parser.add_argument("--point-scale", type=int, default=32767, help="点云量化缩放（int16）")
	parser.add_argument("--max-depth", type=int, default=2, help="展开 INSERT 的最大递归深度")
	parser.add_argument("--sqlite", default=None, help="可选：输出 sqlite 数据库路径（例如 out/lib.sqlite）")
	args = parser.parse_args()

	blocks_dxf_path = Path(args.blocks_dxf).expanduser().resolve()
	out_dir = Path(args.out_dir).expanduser().resolve()
	out_dir.mkdir(parents=True, exist_ok=True)

	doc = ezdxf.readfile(str(blocks_dxf_path))
	only_han_blocks = bool(args.only_han_blocks) or not bool(args.include_non_han)
	index = build_index(
		doc,
		only_han_blocks=only_han_blocks,
		with_point_cloud=bool(args.with_point_cloud),
		sample_div=float(args.sample_div),
		max_points=int(args.max_points),
		point_scale=int(args.point_scale),
		max_depth=int(args.max_depth),
	)
	index["base_point"] = args.base_point
	if args.with_point_cloud:
		index["point_cloud"] = {"sample_div": float(args.sample_div), "max_points": int(args.max_points), "scale": int(args.point_scale)}

	# 固定输出文件名
	out_blocks = out_dir / "blocks.dxf"
	out_index = out_dir / "index.json"

	shutil.copy2(blocks_dxf_path, out_blocks)
	save_index(out_index, index)

	if args.sqlite:
		write_sqlite(Path(args.sqlite).expanduser().resolve(), index, out_blocks)

	print(f"符号库已生成：{out_dir}")
	print(f"- blocks: {out_blocks}")
	print(f"- index:  {out_index}")
	if args.sqlite:
		print(f"- sqlite: {Path(args.sqlite).expanduser().resolve()}")
	print(f"符号数量：{len(index.get('symbols', []))}")
	return 0


if __name__ == "__main__":
	raise SystemExit(main())
