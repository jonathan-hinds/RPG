# Equipment Visual Catalog

This folder contains designer-authored `MMOEquipmentVisualDefinition` assets for gear that changes player appearance.

## Designer Workflow

1. Create a new asset from `Create > RPG Clone > Characters > Equipment Visual`.
2. For armor, keep `Binding Mode` as `Body Part`, set `Equipment Slot` to the item slot, and set `Body Part` to the visible character part it should affect.
3. For color-only gear, leave `Hide Base Body Part` off, turn `Use Color Override` on, and pick the color.
4. For weapons and shields, set `Binding Mode` to `Attachment Socket`, set the item slot, set the ready and stowed socket names from the socket table below, assign the in-combat wrapper prefab to `Model Prefab`, and optionally assign a separate out-of-combat wrapper prefab to `Stowed Model Prefab`.
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

At runtime, attachment visuals use `Socket Name` and `Model Prefab` while the character is in combat. Out of combat they use `Stowed Socket Name` and `Stowed Model Prefab`; when `Stowed Model Prefab` is empty, the visual falls back to `Model Prefab`.

| Equipment family | Slot | Ready socket | Stowed socket |
| --- | --- | --- | --- |
| 1H weapon | Main Hand | `cc_weapon_r` | `cc_hip.l` |
| 2H weapon or staff | Main Hand | `cc_weapon_r` | `cc_back_x` |
| Shield | Off Hand | `cc_shield.l` | `cc_back_center.x` |

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
| Recruit's Shortsword | Main Hand | Attachment Socket | `cc_weapon_r` in combat, `cc_hip.l` stowed | `EV_Recruits_Shortsword_1H` | Prefab `PF_Recruits_Shortsword_Attachment` |
| Recruit's Mace | Main Hand | Attachment Socket | `cc_weapon_r` in combat, `cc_hip.l` stowed | `EV_Recruits_Mace_1H` | Prefab `PF_Recruits_Mace_Attachment` |
| Recruit's Greatsword | Main Hand | Attachment Socket | `cc_weapon_r` in combat, `cc_back_x` stowed | `EV_Recruits_Greatsword_2H` | Ready prefab `PF_Recruits_Greatsword_Attachment`, stowed prefab `PF_Recruits_Greatsword_StowedAttachment` |
| Recruit's Maul | Main Hand | Attachment Socket | `cc_weapon_r` in combat, `cc_back_x` stowed | `EV_Recruits_Maul_2H` | Ready prefab `PF_Recruits_Maul_Attachment`, stowed prefab `PF_Recruits_Maul_StowedAttachment` |
| Recruit's Staff | Main Hand | Attachment Socket | `cc_weapon_r` in combat, `cc_back_x` stowed | `EV_Recruits_Staff_2H` | Ready prefab `PF_Recruits_Staff_Attachment`, stowed prefab `PF_Recruits_Staff_StowedAttachment` |
| Recruit's Shield | Off Hand | Attachment Socket | `cc_shield.l` in combat, `cc_back_center.x` stowed | `EV_Recruits_Shield_OffHand` | Ready prefab `PF_Recruits_Shield_Attachment`, stowed prefab `PF_Recruits_Shield_StowedAttachment` |

## Weapon Coverage

Weapons in the same family intentionally share one visual until distinct art is authored.

| Item | Weapon type | Slot | Visual status | Equipment visual asset | Visual note |
| --- | --- | --- | --- | --- | --- |
| Apprentice Staff | Staff | Main Hand | Implemented | `EV_Recruits_Staff_2H` | Shares recruit staff visual |
| Butcher's Crook | Staff | Main Hand | Implemented | `EV_Recruits_Staff_2H` | Shares recruit staff visual |
| Cleaver's Stone Maul | TwoHandMace | Main Hand | Implemented | `EV_Recruits_Maul_2H` | Shares recruit maul visual |
| Initiate's Cudgel | OneHandMace | Main Hand | Implemented | `EV_Recruits_Mace_1H` | Shares recruit mace visual |
| Millguard Saber | OneHandSword | Main Hand | Implemented | `EV_Recruits_Shortsword_1H` | Shares recruit shortsword visual |
| Millwright's Hammer | OneHandMace | Main Hand | Implemented | `EV_Recruits_Mace_1H` | Shares recruit mace visual |
| Raincaller's Ward | Shield | Off Hand | Implemented | `EV_Recruits_Shield_OffHand` | Shares recruit shield visual |
| Recruit's Buckler | Shield | Off Hand | Implemented | `EV_Recruits_Shield_OffHand` | Shares recruit shield visual |
| Recruit's Greatsword | TwoHandSword | Main Hand | Implemented | `EV_Recruits_Greatsword_2H` | Uses recruit greatsword visual |
| Recruit's Shield | Shield | Off Hand | Implemented | `EV_Recruits_Shield_OffHand` | Uses recruit shield visual |
| Recruit's Shortsword | OneHandSword | Main Hand | Implemented | `EV_Recruits_Shortsword_1H` | Uses recruit shortsword visual |
| Recruit's Staff | Staff | Main Hand | Implemented | `EV_Recruits_Staff_2H` | Uses recruit staff visual |
| Reinforced Mill Buckler | Shield | Off Hand | Implemented | `EV_Recruits_Shield_OffHand` | Shares recruit shield visual |
| Sailsong Staff | Staff | Main Hand | Implemented | `EV_Recruits_Staff_2H` | Shares recruit staff visual |
| Valley Greatsword | TwoHandSword | Main Hand | Implemented | `EV_Recruits_Greatsword_2H` | Shares recruit greatsword visual |
| Watchman's Tower Shield | Shield | Off Hand | Implemented | `EV_Recruits_Shield_OffHand` | Shares recruit shield visual |

No current item assets use the Head slot, so no head visuals were authored in this pass.
