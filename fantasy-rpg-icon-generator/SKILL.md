---
name: fantasy-rpg-icon-generator
description: Generate and prepare original classic-MMORPG-style icon artwork for inventory items, gear, consumables, resources, abilities, buffs, debuffs, passives, quest items, and status effects. Use when creating compact hand-painted game UI icons, designing item art by rarity, producing icon variations, evaluating tiny-size readability, organizing exports into rarity folders, or converting approved masters into Unity-ready PNG sizes.
---

# Fantasy RPG Icon Generator

Generate original compact game-interface artwork that prioritizes the visual readability of classic MMORPG inventory icons. Do not create cinematic product renders or copy existing game icons.

## Required workflow

1. Read `references/icon-style.md`.
2. Read the relevant category in `references/icon-categories.md`.
3. For items or gear, read `references/rarity-direction.md` and assign a rarity.
4. Inspect `assets/style-references/target_wow_inventory_reference.png` for scale, crop, simplification, and UI readability.
5. Inspect `assets/style-references/rejected_overrendered_reference.png` only as a negative example.
6. Build the prompt with `references/prompt-templates.md`.
7. Generate borderless artwork with a soft painted vignette. Never bake an inventory-slot frame into the image unless the user explicitly requests one for that specific icon.
8. Review against `references/quality-checklist.md` at 64x64 and 32x32.
9. Use `scripts/process_icon.py` to export the approved master into the correct rarity and category folders.
10. Use `scripts/create_contact_sheet.py` to compare batches.

## Default assumptions

- Master: 1024x1024 PNG used as a source, but painted with intentionally broad simplified detail.
- Exports: 256x256, 64x64, and 32x32 PNG.
- Tight square crop with the subject covering 75-95% of the canvas.
- One dominant object, symbol, or action.
- Chunky hand-painted forms, limited micro-detail, and a quiet abstract background with a soft edge vignette.
- No permanent UI frame, metallic border, bevel, slot edge, text, labels, cooldown treatment, or rarity border.
- Item rarity controls art complexity using `references/rarity-direction.md`.

## Intake

Extract or infer:

- Icon name
- Category
- Item type or gameplay effect
- Rarity for gear/items
- Primary silhouette or visual symbol
- Signature feature
- Materials, magic school, or damage type
- Palette
- Elements to avoid

When rarity is not specified for an item, ask for it only when the result materially depends on rarity. Otherwise default to `common` and state the assumption briefly.

## Core art-direction rules

- Design backward from 32x32 readability.
- Favor a tight symbolic crop over displaying the full object at a distance.
- Use broad painted shapes and two or three major value groups.
- Simplify chainmail, engraving, cracks, grain, stitching, and surface damage.
- Use selective highlights; do not render every edge.
- Keep the background subordinate and abstract; use a soft painted vignette that darkens the outer edges without forming a hard frame.
- Treat photorealism, PBR rendering, studio lighting, and high-frequency material texture as failures.
- Do not confuse more rarity with more noise.
- Preserve clear differences between icons in the same family.

## Rarity output structure

Export item icons to:

```text
output/<rarity>/<category>/<icon_name>/
```

Supported rarity values:

```text
poor
common
uncommon
rare
epic
legendary
```

Suggested categories include `weapons`, `armor`, `consumables`, `resources`, `quest-items`, and `misc`.

Ability, buff, debuff, passive, and status icons may use `common` as the organizational default unless the project assigns them a rarity tier.

## Naming

Use lowercase snake_case:

- `<category>_<icon_name>_master.png`
- `<category>_<icon_name>_256.png`
- `<category>_<icon_name>_64.png`
- `<category>_<icon_name>_32.png`

## Process an approved icon

```bash
python scripts/process_icon.py path/to/master.png \
  --output-root output \
  --rarity uncommon \
  --category armor \
  --name bonebound_chainmail
```

This creates:

```text
output/uncommon/armor/bonebound_chainmail/
```

## Concept batches

For uncertain art direction, generate three or four substantially different silhouettes, then select the clearest at 32x32. For a rarity progression, generate all six rarities as related but independently designed items; do not just recolor one base image.

## Approved references

The initial target screenshot is sufficient to begin. As original icons are approved, add only the strongest ones to `assets/approved-references/`. Treat approved project icons as stronger references than broad genre labels, but never copy their exact composition into unrelated icons.
