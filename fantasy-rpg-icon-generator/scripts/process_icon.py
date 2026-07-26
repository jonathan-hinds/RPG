#!/usr/bin/env python3
"""Normalize an icon master and export sizes into rarity/category folders."""

from __future__ import annotations

import argparse
import re
from pathlib import Path
from PIL import Image, ImageOps

DEFAULT_SIZES = (256, 64, 32)
RARITIES = ("poor", "common", "uncommon", "rare", "epic", "legendary")


def parse_sizes(value: str) -> tuple[int, ...]:
    try:
        sizes = tuple(sorted({int(part.strip()) for part in value.split(",")}, reverse=True))
    except ValueError as exc:
        raise argparse.ArgumentTypeError("sizes must be comma-separated integers") from exc
    if not sizes or any(size <= 0 for size in sizes):
        raise argparse.ArgumentTypeError("sizes must contain positive integers")
    return sizes


def slug(value: str) -> str:
    cleaned = re.sub(r"[^a-z0-9]+", "_", value.lower()).strip("_")
    if not cleaned:
        raise argparse.ArgumentTypeError("value must contain letters or numbers")
    return cleaned


def square_crop(image: Image.Image) -> Image.Image:
    width, height = image.size
    side = min(width, height)
    left = (width - side) // 2
    top = (height - side) // 2
    return image.crop((left, top, left + side, top + side))


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("input", type=Path, help="Approved source PNG")
    parser.add_argument("--output-root", type=Path, default=Path("output"))
    parser.add_argument("--rarity", choices=RARITIES, default="common")
    parser.add_argument("--category", type=slug, default="misc")
    parser.add_argument("--name", type=slug, required=True)
    parser.add_argument("--sizes", type=parse_sizes, default=DEFAULT_SIZES)
    parser.add_argument("--fit", choices=("crop", "contain"), default="crop")
    args = parser.parse_args()

    if not args.input.is_file():
        parser.error(f"input file does not exist: {args.input}")

    output_dir = args.output_root / args.rarity / args.category / args.name
    output_dir.mkdir(parents=True, exist_ok=True)
    base_name = f"{args.category}_{args.name}"

    with Image.open(args.input) as source:
        image = source.convert("RGBA")
        if args.fit == "crop":
            normalized = square_crop(image)
        else:
            side = max(image.size)
            normalized = ImageOps.pad(
                image,
                (side, side),
                method=Image.Resampling.LANCZOS,
                color=(0, 0, 0, 0),
            )

        normalized.save(output_dir / f"{base_name}_master.png", optimize=True)
        for size in args.sizes:
            exported = normalized.resize((size, size), Image.Resampling.LANCZOS)
            exported.save(output_dir / f"{base_name}_{size}.png", optimize=True)

    print(f"Created master and {len(args.sizes)} exports in {output_dir}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
