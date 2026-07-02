# Equipment Visual Catalog

This folder contains designer-authored `MMOEquipmentVisualDefinition` assets for gear that changes player body-part appearance.

## Designer Workflow

1. Create a new asset from `Create > RPG Clone > Characters > Equipment Visual`.
2. Set `Equipment Slot` to the item slot and `Body Part` to the visible character part it should affect.
3. For color-only gear, leave `Hide Base Body Part` off, turn `Use Color Override` on, and pick the color.
4. Assign the Equipment Visual asset to the item's `Equipment > Equipment Visual` field.

Current visible mappings:

| Equipment slot | Body part |
| --- | --- |
| Head | Head |
| Chest | Torso |
| Legs | Legs |
| Hands | Hands |
| Feet | Feet |

## Authored Gear

| Item | Slot | Body part | Equipment visual asset | Color |
| --- | --- | --- | --- | --- |
| Ashguard Vest (Cloth) | Chest | Torso | `EV_Ashguard_Vest_Cloth` | `#345D9D` |
| Ashguard Vest (Leather) | Chest | Torso | `EV_Ashguard_Vest_Leather` | `#6B3F22` |
| Ashguard Vest (Mail) | Chest | Torso | `EV_Ashguard_Vest_Mail` | `#9EA7B3` |
| Razorcrag Grips (Cloth) | Hands | Hands | `EV_Razorcrag_Grips_Cloth` | `#E6B33D` |
| Razorcrag Grips (Leather) | Hands | Hands | `EV_Razorcrag_Grips_Leather` | `#5B2F1A` |
| Razorcrag Grips (Mail) | Hands | Hands | `EV_Razorcrag_Grips_Mail` | `#7E8B95` |
| Valley Watch Leggings (Cloth) | Legs | Legs | `EV_Valley_Watch_Leggings_Cloth` | `#4868A8` |
| Valley Watch Leggings (Leather) | Legs | Legs | `EV_Valley_Watch_Leggings_Leather` | `#3E6B3A` |
| Valley Watch Leggings (Mail) | Legs | Legs | `EV_Valley_Watch_Leggings_Mail` | `#65727F` |
| Trailbreaker's Boots (Cloth) | Feet | Feet | `EV_Trailbreakers_Boots_Cloth` | `#6F5E9E` |
| Trailbreaker's Boots (Leather) | Feet | Feet | `EV_Trailbreakers_Boots_Leather` | `#4A2F1E` |
| Trailbreaker's Boots (Mail) | Feet | Feet | `EV_Trailbreakers_Boots_Mail` | `#535F68` |

No current item assets use the Head slot, so no head visuals were authored in this pass.
