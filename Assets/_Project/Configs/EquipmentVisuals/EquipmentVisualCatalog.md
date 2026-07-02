# Equipment Visual Catalog

This folder contains designer-authored `MMOEquipmentVisualDefinition` assets for gear that changes player appearance.

## Designer Workflow

1. Create a new asset from `Create > RPG Clone > Characters > Equipment Visual`.
2. For armor, keep `Binding Mode` as `Body Part`, set `Equipment Slot` to the item slot, and set `Body Part` to the visible character part it should affect.
3. For color-only gear, leave `Hide Base Body Part` off, turn `Use Color Override` on, and pick the color.
4. For one-handed weapons, set `Binding Mode` to `Attachment Socket`, set `Equipment Slot` to `Main Hand`, set `Socket Name` to `cc_weapon_r`, and assign a wrapper prefab whose root is the socket point.
5. Assign the Equipment Visual asset to the item's `Equipment > Equipment Visual` field.

Current visible mappings:

| Equipment slot | Body part |
| --- | --- |
| Head | Head |
| Chest | Torso |
| Legs | Legs |
| Hands | Hands |
| Feet | Feet |

## Weapon Attachment Workflow

Weapon wrapper prefabs live under `Assets/_Project/Prefabs/Equipment/Weapons`.

Open the wrapper prefab, keep the prefab root at the intended socket origin, and move/rotate/scale the mesh child until the weapon handle sits between the editor-only guide points:

- `EditorOnly_HandleBottom_AlignToWeaponBone`
- `EditorOnly_HandleTop_AlignToWeaponBone`

The guide points and line are tagged `EditorOnly`; they are visible while authoring the prefab and stripped from spawned equipment visuals at runtime. Designers should move the weapon mesh child, not the guide points, unless deliberately changing the grip convention for that weapon family.

## Authored Gear

| Item | Slot | Binding | Target | Equipment visual asset | Visual |
| --- | --- | --- | --- | --- | --- |
| Ashguard Vest (Cloth) | Chest | Body Part | Torso | `EV_Ashguard_Vest_Cloth` | Color `#345D9D` |
| Ashguard Vest (Leather) | Chest | Body Part | Torso | `EV_Ashguard_Vest_Leather` | Color `#6B3F22` |
| Ashguard Vest (Mail) | Chest | Body Part | Torso | `EV_Ashguard_Vest_Mail` | Color `#9EA7B3` |
| Razorcrag Grips (Cloth) | Hands | Body Part | Hands | `EV_Razorcrag_Grips_Cloth` | Color `#E6B33D` |
| Razorcrag Grips (Leather) | Hands | Body Part | Hands | `EV_Razorcrag_Grips_Leather` | Color `#5B2F1A` |
| Razorcrag Grips (Mail) | Hands | Body Part | Hands | `EV_Razorcrag_Grips_Mail` | Color `#7E8B95` |
| Valley Watch Leggings (Cloth) | Legs | Body Part | Legs | `EV_Valley_Watch_Leggings_Cloth` | Color `#4868A8` |
| Valley Watch Leggings (Leather) | Legs | Body Part | Legs | `EV_Valley_Watch_Leggings_Leather` | Color `#3E6B3A` |
| Valley Watch Leggings (Mail) | Legs | Body Part | Legs | `EV_Valley_Watch_Leggings_Mail` | Color `#65727F` |
| Trailbreaker's Boots (Cloth) | Feet | Body Part | Feet | `EV_Trailbreakers_Boots_Cloth` | Color `#6F5E9E` |
| Trailbreaker's Boots (Leather) | Feet | Body Part | Feet | `EV_Trailbreakers_Boots_Leather` | Color `#4A2F1E` |
| Trailbreaker's Boots (Mail) | Feet | Body Part | Feet | `EV_Trailbreakers_Boots_Mail` | Color `#535F68` |
| Recruit's Shortsword | Main Hand | Attachment Socket | `cc_weapon_r` | `EV_Recruits_Shortsword_1H` | Prefab `PF_Recruits_Shortsword_Attachment` |

No current item assets use the Head slot, so no head visuals were authored in this pass.
