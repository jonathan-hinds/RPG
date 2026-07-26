#!/usr/bin/env python3
"""Create a labeled contact sheet from a directory of icon images."""

from __future__ import annotations

import argparse
import math
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

SUPPORTED = {".png", ".jpg", ".jpeg", ".webp"}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("input_dir", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--cell", type=int, default=160)
    parser.add_argument("--columns", type=int, default=5)
    args = parser.parse_args()

    if args.cell < 64 or args.columns < 1:
        parser.error("cell must be at least 64 and columns must be positive")

    files = sorted(path for path in args.input_dir.iterdir() if path.suffix.lower() in SUPPORTED)
    if not files:
        parser.error("no supported images found")

    label_height = 28
    rows = math.ceil(len(files) / args.columns)
    sheet = Image.new("RGB", (args.columns * args.cell, rows * (args.cell + label_height)), "#202020")
    draw = ImageDraw.Draw(sheet)
    font = ImageFont.load_default()

    for index, path in enumerate(files):
        row, column = divmod(index, args.columns)
        x = column * args.cell
        y = row * (args.cell + label_height)
        with Image.open(path) as source:
            icon = source.convert("RGBA")
            icon.thumbnail((args.cell - 12, args.cell - 12), Image.Resampling.LANCZOS)
            px = x + (args.cell - icon.width) // 2
            py = y + (args.cell - icon.height) // 2
            sheet.paste(icon, (px, py), icon)
        label = path.stem[:24]
        bbox = draw.textbbox((0, 0), label, font=font)
        tx = x + (args.cell - (bbox[2] - bbox[0])) // 2
        draw.text((tx, y + args.cell + 7), label, fill="white", font=font)

    args.output.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(args.output, optimize=True)
    print(f"Created contact sheet: {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
