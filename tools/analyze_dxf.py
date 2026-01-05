#!/usr/bin/env python3
# -*- coding: utf-8 -*-

"""分析 DXF 文件中的图层、文字和实体。"""

import sys
import re
from collections import Counter
import ezdxf

HAN_RE = re.compile(r"[\u4e00-\u9fff]")

def main():
    if len(sys.argv) < 2:
        print("用法: python analyze_dxf.py <dxf_file>")
        return 1

    dxf_path = sys.argv[1]
    doc = ezdxf.readfile(dxf_path)
    msp = doc.modelspace()

    print("=" * 60)
    print("图层信息：")
    print("=" * 60)
    for layer in doc.layers:
        print(f"  - {layer.dxf.name}: color={layer.dxf.color}, linetype={layer.dxf.linetype}")

    print("\n" + "=" * 60)
    print("块定义（含中文名称的）：")
    print("=" * 60)
    han_blocks = []
    for blk in doc.blocks:
        if HAN_RE.search(blk.name):
            han_blocks.append(blk.name)
    print(f"  总数量：{len(han_blocks)}")
    for name in han_blocks[:50]:
        print(f"  - {name}")
    if len(han_blocks) > 50:
        print(f"  ... 还有 {len(han_blocks) - 50} 个")

    print("\n" + "=" * 60)
    print("文字实体分析（ModelSpace）：")
    print("=" * 60)
    text_layers = Counter()
    text_colors = Counter()
    han_texts = []

    for e in msp.query("TEXT MTEXT"):
        layer = e.dxf.layer
        color = e.dxf.color if hasattr(e.dxf, 'color') else 256
        text_layers[layer] += 1
        text_colors[color] += 1

        if e.dxftype() == "TEXT":
            text = e.dxf.text or ""
        else:
            text = e.text or ""

        if HAN_RE.search(text):
            han_texts.append((text.strip()[:30], layer, color))

    print("按图层统计：")
    for layer, cnt in text_layers.most_common(10):
        print(f"  - {layer}: {cnt}")

    print("\n按颜色统计：")
    for color, cnt in text_colors.most_common(10):
        print(f"  - ACI {color}: {cnt}")

    print(f"\n含中文的文字 (共 {len(han_texts)} 条)：")
    for text, layer, color in han_texts[:50]:
        print(f"  - \"{text}\" | layer={layer} | color={color}")
    if len(han_texts) > 50:
        print(f"  ... 还有 {len(han_texts) - 50} 条")

    print("\n" + "=" * 60)
    print("实体类型统计（ModelSpace）：")
    print("=" * 60)
    entity_types = Counter()
    for e in msp:
        entity_types[e.dxftype()] += 1
    for et, cnt in entity_types.most_common():
        print(f"  - {et}: {cnt}")

    return 0

if __name__ == "__main__":
    raise SystemExit(main())
