# Ability Icon Generation Manifest

Generated with the built-in image generation tool using the project-local
`fantasy-rpg-icon-generator` skill.

## Shared prompt direction

Create one square, original fantasy MMORPG UI icon using compact,
hand-painted game-interface artwork rather than cinematic concept art. Use
one dominant chunky silhouette, simplified materials, two or three broad
value groups, selective highlights, a quiet abstract painted background, and
immediate readability at 32x32 pixels.

Keep every meaningful symbol and focal highlight inside the central 80% of
the square. Reserve the outer 10% on every side for subordinate background
and a soft irregular painted vignette so Unity action-slot, buff, and debuff
borders cannot crop important artwork. Fill the canvas to every edge with
painted background, but do not place essential subject detail in the outer
band.

Avoid photorealism, PBR materials, studio lighting, micro-detail, ornate
clutter, full scenes, text, letters, numbers, logos, portraits, graphic gore,
watermarks, metallic rims, bevels, slot borders, hard rectangles,
rarity-colored outlines, and copied game-icon compositions.

## Per-icon prompt concepts

| Ability ID | Category | Prompt concept |
|---|---|---|
| `auto_attack` | Active | A broad steel sword sweeps diagonally into dark armor, with one orange-white contact spark and a single curved motion trail. |
| `mage_arcane_missile` | Debuff/channel | Three violet-blue arcane bolts spiral toward a dark target, led by a concentrated blue-white core. |
| `mage_fire_blast` | Active | An open gloved hand releases a compact circular fire detonation with a white-yellow center and broad orange-red flame petals. |
| `mage_fireball` | Active | A cracked molten fireball flies diagonally with one broad tapered flame tail. |
| `mage_flamestrike` | Debuff/area | A vertical pillar of flame strikes cracked black stone and leaves a low ring of embers. |
| `mage_mage_armor` | Buff | Symmetrical blue-violet armor receives two upward arcane streams around a central mana gem. |
| `orc_blood_fury` | Buff | A powerful green clenched fist rises inside a stable crimson-orange aura. |
| `shaman_earthquake` | Active | A heavy stone fist slams cracked ground while four broad rock slabs rise in an amber shockwave. |
| `shaman_frost_shock` | Debuff | A jagged ice shard strikes an armored boot and locks the ankle in angular frost. |
| `shaman_healing_beam` | Active/heal | A warm open hand sends one turquoise-gold beam into a luminous ivory heart. |
| `shaman_lightning_bolt` | Active | A thick turquoise-white bolt descends from a compact storm spiral onto a dark stone totem. |
| `shaman_water_shield` | Buff | A symmetrical blue shield and central water droplet are encircled by three large water orbs. |
| `trog_lightning_bolt` | Active/NPC | A rough clawed hand throws a crooked yellow-green lightning sphere with three chunky forks. |
| `troll_regeneration` | Buff | A crimson heart wrapped in symmetrical green vines sprouts broad leaves around a healing pulse. |
| `warrior_bash` | Active | A reinforced round shield crashes into a dark iron helmet at one bright impact notch. |
| `warrior_berzerkitis` | Buff | Two broad crossed steel axes glow red-orange while strong speed streaks rise behind them. |
| `warrior_charge` | Active | A heavy armored shoulder and shield surge forward behind a broad white dust-and-speed trail. |
| `warrior_gouge` | Debuff | A hooked short blade cuts a dark leather target, leaving a bold crimson slash and three large drops. |
| `warrior_thunderclap` | Debuff/area | A massive steel gauntlet slams cracked stone and releases an amber shockwave that knocks weapon silhouettes backward. |

## Outputs

- Master source: 1024x1024 PNG
- Review exports: 64x64 and 32x32 PNG
- Unity runtime sprites: 256x256 PNG
- Unity import: single Sprite, mipmaps disabled, bilinear filtering, clamp wrap
