---
name: fantasy-rpg-icon-generator
description: Generate and prepare consistent hand-painted fantasy MMORPG icon artwork for abilities, buffs, debuffs, passives, items, resources, quest items, and status effects. Use when creating new game icons, turning gameplay mechanics into readable visual symbols, producing prompt sets or icon sheets, evaluating icon consistency, or exporting approved square artwork into standardized Unity-ready sizes.
---

# Fantasy RPG Icon Generator

Create original fantasy MMORPG icon artwork with the visual clarity, saturation, dramatic lighting, and hand-painted readability associated with classic early-2000s fantasy game interfaces. Do not copy specific copyrighted icons, logos, characters, or exact compositions.

## Required workflow

1. Read `references/icon-style.md`.
2. Read the relevant category in `references/icon-categories.md`.
3. Convert the gameplay concept into one dominant visual symbol.
4. Build the image prompt with `references/prompt-templates.md`.
5. Generate artwork without a permanent UI frame unless the user explicitly requests one.
6. Review the result against `references/quality-checklist.md`.
7. Inspect it at 64x64 and 32x32 before approval.
8. Use `scripts/process_icon.py` to create standardized exports from an approved master.
9. Use `scripts/create_contact_sheet.py` when comparing multiple icons or variants.

## Default assumptions

Use these defaults unless the user provides different requirements:

- Master artwork: 1024x1024 PNG.
- Final crop: square.
- Artwork only, without cooldown overlays, rarity borders, text, labels, or permanent frames.
- Exports: 256x256, 64x64, and 32x32 PNG.
- Subject coverage: approximately 70-90% of the canvas.
- One dominant focal subject.
- Dramatic but readable lighting.
- Darkened or simplified corners.
- Strong silhouette and value separation.
- No transparency inside the painted artwork unless explicitly requested.

## Intake format

Extract or infer the following fields:

- Icon name
- Category
- Gameplay effect
- Primary visual symbol
- Secondary visual cue
- Magic school, damage type, item material, or emotional tone
- Preferred palette
- Elements to avoid

When information is missing, choose the clearest conventional symbol for the mechanic and state the interpretation briefly.

## Design rules

- Design backward from 32x32 readability.
- Depict an action for active abilities rather than a static inventory object.
- Use a concrete symbol before abstract energy.
- Exaggerate perspective, gesture, and silhouette.
- Keep the focal subject large and immediately identifiable.
- Use broad painted shapes and controlled texture.
- Preserve material identity through distinct highlight behavior.
- Keep backgrounds subordinate to the focal subject.
- Avoid tiny decorative details, excessive particles, clutter, photographic realism, smooth generic 3D rendering, and muddy global color grading.
- Do not use text, letters, numbers, logos, interface labels, or accidental pseudo-lettering.
- Do not make every debuff green, every holy effect gold, or every harmful effect a skull. Match the symbol to the mechanic.
- Maintain meaningful visual differences between icons in the same family.

## Variant workflow

For a new icon family or an uncertain concept:

1. Produce three substantially different symbolic directions.
2. Compare them at thumbnail size.
3. Select the clearest direction, not merely the most detailed image.
4. Refine that direction into the final master.

For an established family, generate one primary direction plus one alternative only when useful.

## Consistency workflow

When approved project icons are available:

- Treat them as stronger guidance than broad genre labels.
- Match their crop, contrast, edge treatment, brush scale, saturation, background density, and lighting intensity.
- Do not duplicate their exact subject arrangement.
- Record recurring visual rules in `references/project-style-overrides.md`.

## Borders and UI treatment

Prefer separate UI borders applied in Unity or by a deterministic image-processing step. Keep source art borderless so the same artwork can support:

- Ability buttons
- Buff and debuff frames
- Item rarity frames
- Disabled states
- Cooldown overlays
- Proc highlights
- Selected and hovered states

If a user requests baked borders, create the artwork first and apply a consistent provided frame afterward. Never ask the image model to improvise a different frame for every icon.

## Output naming

Use lowercase snake_case:

- Master: `<category>_<icon_name>_master.png`
- 256 export: `<category>_<icon_name>_256.png`
- 64 export: `<category>_<icon_name>_64.png`
- 32 export: `<category>_<icon_name>_32.png`

Example:

`ability_press_the_attack_master.png`

## Script usage

Process an approved master:

```bash
python scripts/process_icon.py path/to/master.png --output-dir path/to/exports --name ability_press_the_attack
```

Create a contact sheet:

```bash
python scripts/create_contact_sheet.py path/to/icons --output path/to/contact_sheet.png
```

## Deliverables

For a single icon request, provide:

1. A concise visual concept.
2. A production-ready image-generation prompt.
3. An optional alternate concept when ambiguity is high.
4. A short avoidance list.
5. The generated image when image generation is available.
6. Exported sizes after the user approves the master and a local file is available.

For a batch, preserve visual variety while keeping shared family rules consistent. Use a contact sheet to review duplicate silhouettes, palette repetition, and inconsistent rendering.
