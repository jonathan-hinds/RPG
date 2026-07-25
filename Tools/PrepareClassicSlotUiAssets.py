from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw, ImageEnhance


ROOT = Path(__file__).resolve().parents[1]
GENERATED = ROOT / "ArtSource" / "UI" / "Classic" / "Generated"
ALPHA = ROOT / "ArtSource" / "UI" / "Classic" / "Alpha"
OUTPUT = ROOT / "Assets" / "Resources" / "RPGClone" / "UI" / "SlotFramework"
PREVIEW = ROOT / "ArtSource" / "UI" / "Classic" / "ClassicSlotSkinPreview.png"


def load_rgba(path: Path) -> Image.Image:
    return Image.open(path).convert("RGBA")


def crop_alpha(image: Image.Image, padding_ratio: float = 0.04) -> Image.Image:
    alpha = image.getchannel("A")
    bounds = alpha.getbbox()
    if bounds is None:
        raise ValueError("Image has no visible pixels.")

    left, top, right, bottom = bounds
    width = right - left
    height = bottom - top
    padding = max(2, round(max(width, height) * padding_ratio))
    left = max(0, left - padding)
    top = max(0, top - padding)
    right = min(image.width, right + padding)
    bottom = min(image.height, bottom + padding)
    return image.crop((left, top, right, bottom))


def crop_largest_alpha_component(image: Image.Image) -> Image.Image:
    """Remove disconnected generator artifacts before the normal alpha crop."""
    sample_size = 256
    alpha = image.getchannel("A").resize(
        (sample_size, sample_size),
        Image.Resampling.NEAREST,
    )
    mask = alpha.point(lambda value: 255 if value > 24 else 0)
    pixels = mask.load()
    visited: set[tuple[int, int]] = set()
    largest: tuple[int, int, int, int, int] | None = None

    for y in range(sample_size):
        for x in range(sample_size):
            if pixels[x, y] == 0 or (x, y) in visited:
                continue

            stack = [(x, y)]
            visited.add((x, y))
            count = 0
            left = right = x
            top = bottom = y
            while stack:
                current_x, current_y = stack.pop()
                count += 1
                left = min(left, current_x)
                right = max(right, current_x)
                top = min(top, current_y)
                bottom = max(bottom, current_y)
                for next_x, next_y in (
                    (current_x - 1, current_y),
                    (current_x + 1, current_y),
                    (current_x, current_y - 1),
                    (current_x, current_y + 1),
                ):
                    if (
                        next_x < 0
                        or next_x >= sample_size
                        or next_y < 0
                        or next_y >= sample_size
                        or pixels[next_x, next_y] == 0
                        or (next_x, next_y) in visited
                    ):
                        continue
                    visited.add((next_x, next_y))
                    stack.append((next_x, next_y))

            component = (count, left, top, right + 1, bottom + 1)
            if largest is None or component[0] > largest[0]:
                largest = component

    if largest is None:
        raise ValueError("Image has no visible alpha component.")

    _, left, top, right, bottom = largest
    scale_x = image.width / sample_size
    scale_y = image.height / sample_size
    pad = 8
    bounds = (
        max(0, round(left * scale_x) - pad),
        max(0, round(top * scale_y) - pad),
        min(image.width, round(right * scale_x) + pad),
        min(image.height, round(bottom * scale_y) + pad),
    )
    return image.crop(bounds)


def fit_transparent(
    image: Image.Image,
    size: tuple[int, int],
    padding_ratio: float = 0.04,
    largest_component_only: bool = False,
) -> Image.Image:
    if largest_component_only:
        image = crop_largest_alpha_component(image)
    cropped = crop_alpha(image, padding_ratio)
    target_width, target_height = size
    scale = min(target_width / cropped.width, target_height / cropped.height)
    resized = cropped.resize(
        (
            max(1, round(cropped.width * scale)),
            max(1, round(cropped.height * scale)),
        ),
        Image.Resampling.LANCZOS,
    )
    result = Image.new("RGBA", size, (0, 0, 0, 0))
    result.alpha_composite(
        resized,
        ((target_width - resized.width) // 2, (target_height - resized.height) // 2),
    )
    return result


def quiet_surface(
    image: Image.Image,
    size: tuple[int, int],
    saturation: float,
    contrast: float,
    brightness: float,
) -> Image.Image:
    square = min(image.width, image.height)
    left = (image.width - square) // 2
    top = (image.height - square) // 2
    image = image.crop((left, top, left + square, top + square))
    image = ImageEnhance.Color(image).enhance(saturation)
    image = ImageEnhance.Contrast(image).enhance(contrast)
    image = ImageEnhance.Brightness(image).enhance(brightness)
    return image.resize(size, Image.Resampling.LANCZOS)


def tone_transparent(
    image: Image.Image,
    saturation: float,
    brightness: float = 1.0,
    contrast: float = 1.0,
) -> Image.Image:
    alpha = image.getchannel("A")
    image = ImageEnhance.Color(image).enhance(saturation)
    image = ImageEnhance.Brightness(image).enhance(brightness)
    image = ImageEnhance.Contrast(image).enhance(contrast)
    image.putalpha(alpha)
    return image


def save(image: Image.Image, name: str) -> Path:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    path = OUTPUT / name
    image.save(path, optimize=True)
    return path


def composite_scaled(
    target: Image.Image,
    sprite: Image.Image,
    bounds: tuple[int, int, int, int],
    mirror: bool = False,
) -> None:
    x, y, width, height = bounds
    fitted = sprite.resize((width, height), Image.Resampling.LANCZOS)
    if mirror:
        fitted = fitted.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
    target.alpha_composite(fitted, (x, y))


def build_preview(paths: list[Path]) -> None:
    sprites = {path.stem: load_rgba(path) for path in paths}
    preview = Image.new("RGBA", (1280, 680), (25, 31, 36, 255))
    draw = ImageDraw.Draw(preview)
    draw.text((36, 18), "Classic slot construction preview", fill=(226, 213, 181, 255))

    bag = (38, 56, 334, 525)
    composite_scaled(preview, sprites["Panel_Background_Default"], bag)
    composite_scaled(preview, sprites["Panel_Header_Default"], (52, 65, 305, 50))
    composite_scaled(preview, sprites["BagPanel_CurrencyBar"], (54, 525, 302, 42))

    slot_size = 62
    slot_stride = 68
    for row in range(5):
        for column in range(4):
            x = 61 + column * slot_stride
            y = 132 + row * slot_stride
            composite_scaled(preview, sprites["Slot_Background_Empty"], (x, y, slot_size, slot_size))
            composite_scaled(preview, sprites["Slot_Frame_Normal"], (x, y, slot_size, slot_size))

    composite_scaled(preview, sprites["Panel_Frame_Default"], bag)
    composite_scaled(preview, sprites["BagPanel_IconMedallion"], (12, 42, 86, 86))
    composite_scaled(preview, sprites["Panel_Close_Normal"], (325, 68, 40, 40))
    draw.text((151, 80), "BACKPACK", fill=(239, 232, 216, 255), anchor="mm")
    draw.text((282, 535), "24   91   92", fill=(239, 232, 216, 255))

    bar_x = 460
    bar_y = 430
    composite_scaled(
        preview,
        sprites["ActionBar_Background_Center"],
        (bar_x, bar_y, 730, 86),
    )
    composite_scaled(
        preview,
        sprites["ActionBar_EndCap"],
        (bar_x - 92, bar_y + 8, 116, 78),
    )
    composite_scaled(
        preview,
        sprites["ActionBar_EndCap"],
        (bar_x + 706, bar_y + 8, 116, 78),
        mirror=True,
    )

    bar_slot_size = 54
    bar_stride = 59
    for index in range(12):
        x = bar_x + 14 + index * bar_stride
        y = bar_y + 15
        composite_scaled(
            preview,
            sprites["Slot_Background_Empty"],
            (x, y, bar_slot_size, bar_slot_size),
        )
        if index in (1, 4, 7, 10):
            inset = 7
            colors = (
                (48, 92, 116, 255),
                (112, 58, 91, 255),
                (62, 113, 74, 255),
                (121, 76, 41, 255),
            )
            color = colors[(index // 3) % len(colors)]
            draw.rounded_rectangle(
                (
                    x + inset,
                    y + inset,
                    x + bar_slot_size - inset,
                    y + bar_slot_size - inset,
                ),
                radius=5,
                fill=color,
            )
        composite_scaled(
            preview,
            sprites["Slot_Frame_Normal"],
            (x, y, bar_slot_size, bar_slot_size),
        )

    draw.text((460, 394), "ACTION BAR — 4 px visual gaps", fill=(200, 191, 171, 255))
    draw.text(
        (460, 548),
        "empty well  +  normal frame  +  one semantic state rim",
        fill=(184, 178, 165, 255),
    )

    state_x = 490
    for index, tint in enumerate(
        ((232, 224, 189, 255), (93, 184, 241, 255), (219, 166, 67, 255))
    ):
        x = state_x + index * 120
        composite_scaled(
            preview,
            sprites["Slot_Background_Empty"],
            (x, 584, 70, 70),
        )
        composite_scaled(
            preview,
            sprites["Slot_Frame_Normal"],
            (x, 584, 70, 70),
        )
        rim = sprites["Slot_State_Rim"].copy()
        tinted = Image.new("RGBA", rim.size, tint)
        tinted.putalpha(rim.getchannel("A"))
        composite_scaled(preview, tinted, (x, 584, 70, 70))

    PREVIEW.parent.mkdir(parents=True, exist_ok=True)
    preview.convert("RGB").save(PREVIEW, quality=94)


def main() -> None:
    outputs = [
        save(
            quiet_surface(
                load_rgba(GENERATED / "Slot_Background_Empty.png"),
                (128, 128),
                0.38,
                0.72,
                0.58,
            ),
            "Slot_Background_Empty.png",
        ),
        save(
            fit_transparent(
                tone_transparent(
                    load_rgba(ALPHA / "Slot_Frame_Normal.png"),
                    0.22,
                    0.86,
                ),
                (128, 128),
                0.015,
            ),
            "Slot_Frame_Normal.png",
        ),
        save(
            fit_transparent(load_rgba(ALPHA / "Slot_State_Rim.png"), (128, 128), 0.025),
            "Slot_State_Rim.png",
        ),
        save(
            fit_transparent(load_rgba(ALPHA / "Slot_Glow_Proc.png"), (128, 128), 0.06),
            "Slot_Glow_Proc.png",
        ),
        save(
            fit_transparent(
                load_rgba(ALPHA / "Slot_CategorySilhouette_Default.png"),
                (128, 128),
                0.10,
            ),
            "Slot_CategorySilhouette_Default.png",
        ),
        save(
            quiet_surface(
                load_rgba(GENERATED / "Panel_Background_Default.png"),
                (512, 512),
                0.42,
                0.60,
                0.48,
            ),
            "Panel_Background_Default.png",
        ),
        save(
            fit_transparent(
                tone_transparent(
                    load_rgba(ALPHA / "Panel_Frame_Default.png"),
                    0.18,
                    0.82,
                ),
                (512, 512),
                0.012,
                largest_component_only=True,
            ),
            "Panel_Frame_Default.png",
        ),
        save(
            fit_transparent(load_rgba(ALPHA / "Panel_Header_Default.png"), (1024, 160), 0.012),
            "Panel_Header_Default.png",
        ),
        save(
            fit_transparent(load_rgba(ALPHA / "Panel_Close_Normal.png"), (128, 128), 0.025),
            "Panel_Close_Normal.png",
        ),
        save(
            fit_transparent(load_rgba(ALPHA / "BagPanel_IconMedallion.png"), (256, 256), 0.025),
            "BagPanel_IconMedallion.png",
        ),
        save(
            fit_transparent(load_rgba(ALPHA / "BagPanel_CurrencyBar.png"), (1024, 160), 0.012),
            "BagPanel_CurrencyBar.png",
        ),
        save(
            fit_transparent(
                load_rgba(ALPHA / "ActionBar_Background_Center.png"),
                (1024, 128),
                0.012,
            ),
            "ActionBar_Background_Center.png",
        ),
        save(
            fit_transparent(load_rgba(ALPHA / "ActionBar_EndCap.png"), (384, 192), 0.025),
            "ActionBar_EndCap.png",
        ),
    ]

    build_preview(outputs)

    for path in outputs:
        image = load_rgba(path)
        alpha = image.getchannel("A")
        print(
            f"{path.relative_to(ROOT)} "
            f"{image.width}x{image.height} "
            f"alpha={alpha.getextrema()}"
        )
    print(f"Preview: {PREVIEW.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
