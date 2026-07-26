#!/usr/bin/env python3
"""Normalize a square icon master and export standard game-ready sizes."""

from __future__ import annotations

import argparse
from pathlib import Path
from PIL import Image, ImageOps

DEFAULT_SIZES = (256, 64, 32)


def parse_sizes(value: str) -> tuple[int, ...]:
    try:
        sizes = tuple(sorted({int(part.strip()) for part in value.split(",")}, reverse=True))
    except ValueError as exc:
        raise argparse.ArgumentTypeError("sizes must be comma-separated integers") from exc
    if not sizes or any(size <= 0 for size in sizes):
        raise argparse.ArgumentTypeError("sizes must contain positive integers")
    return sizes


def square_crop(image: Image.Image) -> Image.Image:
    width, height = image.size
    side = min(width, height)
    left = (width - side) // 2
    top = (height - side) // 2
    return image.crop((left, top, left + side, top + side))


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("input", type=Path, help="Path to the approved master PNG")
    parser.add_argument("--output-dir", type=Path, required=True, help="Directory for exports")
    parser.add_argument("--name", required=True, help="Base filename without extension")
    parser.add_argument("--sizes", type=parse_sizes, default=DEFAULT_SIZES, help="Comma-separated sizes")
    parser.add_argument(
        "--fit",
        choices=("crop", "contain"),
        default="crop",
        help="crop=center-crop to square; contain=pad to square with transparent pixels",
    )
    args = parser.parse_args()

    if not args.input.is_file():
        parser.error(f"input file does not exist: {args.input}")

    args.output_dir.mkdir(parents=True, exist_ok=True)

    with Image.open(args.input) as source:
        image = source.convert("RGBA")
        if args.fit == "crop":
            normalized = square_crop(image)
        else:
            side = max(image.size)
            normalized = ImageOps.pad(image, (side, side), method=Image.Resampling.LANCZOS, color=(0, 0, 0, 0))

        master_path = args.output_dir / f"{args.name}_master.png"
        normalized.save(master_path, optimize=True)

        for size in args.sizes:
            exported = normalized.resize((size, size), Image.Resampling.LANCZOS)
            exported.save(args.output_dir / f"{args.name}_{size}.png", optimize=True)

    print(f"Created normalized master and {len(args.sizes)} exports in {args.output_dir}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
