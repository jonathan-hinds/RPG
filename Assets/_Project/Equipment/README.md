# Equipment Content Catalog

This folder is the source of truth for equippable item content. Each gameplay item has its own folder, armor is grouped by weight, and weapons are grouped by weapon type.

## Armor Asset Layout

Armor content lives under `Assets/_Project/Equipment/Armor`:

- `<Armor Weight>/<Item>` contains the item definition, equipment visual definition, skinned model, texture, and material.

Every armor mesh must be remapped to the material in its own item folder, and that material must reference the matching item texture. Do not use a cross-item base material.

## Designer Workflow

1. Create a new asset from `Create > RPG Clone > Characters > Equipment Visual`.
2. For armor, keep `Binding Mode` as `Body Part`, set `Equipment Slot` to the item slot, and set `Body Part` to the visible character part it should affect.
3. For color-only gear, leave `Hide Base Body Part` off, turn `Use Color Override` on, and pick the color.
4. For weapons and shields, set `Binding Mode` to `Attachment Socket`, set the item slot, set the ready and stowed socket names from the socket table below, assign the in-combat wrapper prefab to `Model Prefab`, and optionally assign separate out-of-combat and in-combat locomotion wrappers to `Stowed Model Prefab` and `Combat Movement Model Prefab`.
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

Weapon content lives under `Assets/_Project/Equipment/Weapons/<Weapon Type>/<Item>`. Each item folder owns its equipment visual definition, wrapper prefabs, mesh, texture, and material. Items may begin with identical-looking copies, but their asset references must remain independent so one item's art can change without affecting another.

Open the wrapper prefab, keep the prefab root at the intended socket origin, and move/rotate/scale the mesh child until the weapon handle sits between the editor-only guide points:

- `EditorOnly_HandleBottom_AlignToWeaponBone`
- `EditorOnly_HandleTop_AlignToWeaponBone`

The guide points and line are tagged `EditorOnly`; they are visible while authoring the prefab and stripped from spawned equipment visuals at runtime. Designers should move the weapon mesh child, not the guide points, unless deliberately changing the grip convention for that weapon family.

At runtime, attachment visuals use `Socket Name` and `Model Prefab` while the character is stationary in combat. Walking, running, backpedaling, and jumping in combat use `Combat Movement Socket Name` and `Combat Movement Model Prefab`. Out of combat they use `Stowed Socket Name` and `Stowed Model Prefab`. Optional state-specific sockets and prefabs fall back to the ready socket and prefab when empty.

| Equipment family | Slot | Ready socket | Stowed socket |
| --- | --- | --- | --- |
| 1H weapon | Main Hand | `cc_weapon_r` | `cc_hip.l` |
| 2H weapon or staff | Main Hand | `cc_weapon_r` | `cc_back_x` |
| Shield | Off Hand | `cc_shield.l` | `cc_back_center.x` |

## Authored Gear

| Item | Slot | Binding | Target | Equipment visual asset | Visual |
| --- | --- | --- | --- | --- | --- |
| Ashguard Vest (Cloth) | Chest | Body Part | Torso | `EV_Ashguard_Vest_Cloth` | Item-specific cloth model, material, and texture |
| Ashguard Vest (Leather) | Chest | Body Part | Torso | `EV_Ashguard_Vest_Leather` | Item-specific leather skinned model and texture |
| Ashguard Vest (Mail) | Chest | Body Part | Torso | `EV_Ashguard_Vest_Mail` | Item-specific mail skinned model and texture |
| Razorcrag Grips (Cloth) | Hands | Body Part | Hands | `EV_Razorcrag_Grips_Cloth` | Item-specific cloth model, material, and texture |
| Razorcrag Grips (Leather) | Hands | Body Part | Hands | `EV_Razorcrag_Grips_Leather` | Item-specific leather skinned model and texture |
| Razorcrag Grips (Mail) | Hands | Body Part | Hands | `EV_Razorcrag_Grips_Mail` | Item-specific mail skinned model and texture |
| Valley Watch Leggings (Cloth) | Legs | Body Part | Legs | `EV_Valley_Watch_Leggings_Cloth` | Item-specific cloth model, material, and texture |
| Valley Watch Leggings (Leather) | Legs | Body Part | Legs | `EV_Valley_Watch_Leggings_Leather` | Item-specific leather skinned model and texture |
| Valley Watch Leggings (Mail) | Legs | Body Part | Legs | `EV_Valley_Watch_Leggings_Mail` | Item-specific mail skinned model and texture |
| Trailbreaker's Boots (Cloth) | Feet | Body Part | Feet | `EV_Trailbreakers_Boots_Cloth` | Item-specific cloth model, material, and texture |
| Trailbreaker's Boots (Leather) | Feet | Body Part | Feet | `EV_Trailbreakers_Boots_Leather` | Item-specific leather skinned model and texture |
| Trailbreaker's Boots (Mail) | Feet | Body Part | Feet | `EV_Trailbreakers_Boots_Mail` | Item-specific mail skinned model and texture |
| Tribal Seer's Vestments (Cloth) | Chest | Body Part | Torso | `EV_Tribal_Seers_Vestments` | Item-specific Tribal skinned model, material, texture, and icon |
| Tribal Seer's Legwraps (Cloth) | Legs | Body Part | Legs | `EV_Tribal_Seers_Legwraps` | Item-specific Tribal skinned model, material, texture, and icon |
| Tribal Mystic's Grips (Leather) | Hands | Body Part | Hands | `EV_Tribal_Mystics_Grips` | Item-specific Tribal skinned model, material, texture, and icon |
| Tribal Mystic's Treads (Leather) | Feet | Body Part | Feet | `EV_Tribal_Mystics_Treads` | Item-specific Tribal skinned model, material, texture, and icon |
| Scalehunter Grips (Leather) | Hands | Body Part | Hands | `EV_Scalehunter_Grips` | Item-specific Scale skinned model, material, texture, and icon |
| Scalehunter Treads (Leather) | Feet | Body Part | Feet | `EV_Scalehunter_Treads` | Item-specific Scale skinned model, material, texture, and icon |
| Scaleguard Legguards (Mail) | Legs | Body Part | Legs | `EV_Scaleguard_Legguards` | Item-specific Scale skinned model, material, texture, and icon |
| Scaleguard Hauberk (Mail) | Chest | Body Part | Torso | `EV_Scaleguard_Hauberk` | Item-specific Scale skinned model, material, texture, and icon |

## Weapon Coverage

Every weapon has an independent visual bundle. Some currently have identical geometry and textures, but no gameplay item references another item's visual assets.

| Item | Weapon type | Slot | Visual status | Equipment visual asset | Visual note |
| --- | --- | --- | --- | --- | --- |
| Apprentice Staff | Staff | Main Hand | Implemented | `EV_Apprentice_Staff` | Item-owned visual bundle |
| Butcher's Crook | Staff | Main Hand | Implemented | `EV_Butchers_Crook` | Item-owned visual bundle |
| Cleaver's Stone Maul | TwoHandMace | Main Hand | Implemented | `EV_Cleavers_Stone_Maul` | Item-owned visual bundle |
| Initiate's Cudgel | OneHandMace | Main Hand | Implemented | `EV_Initiates_Cudgel` | Item-owned visual bundle |
| Millguard Saber | OneHandSword | Main Hand | Implemented | `EV_Millguard_Saber` | Item-owned visual bundle |
| Millwright's Hammer | OneHandMace | Main Hand | Implemented | `EV_Millwrights_Hammer` | Item-owned visual bundle |
| Raincaller's Ward | Shield | Off Hand | Implemented | `EV_Raincallers_Ward` | Item-owned visual bundle |
| Recruit's Buckler | Shield | Off Hand | Implemented | `EV_Recruits_Buckler` | Item-owned visual bundle |
| Recruit's Greatsword | TwoHandSword | Main Hand | Implemented | `EV_Recruits_Greatsword` | Item-owned visual bundle |
| Recruit's Shield | Shield | Off Hand | Implemented | `EV_Recruits_Shield` | Item-owned visual bundle |
| Recruit's Shortsword | OneHandSword | Main Hand | Implemented | `EV_Recruits_Shortsword` | Item-owned visual bundle |
| Recruit's Staff | Staff | Main Hand | Implemented | `EV_Recruits_Staff` | Item-owned visual bundle |
| Reinforced Mill Buckler | Shield | Off Hand | Implemented | `EV_Reinforced_Mill_Buckler` | Item-owned visual bundle |
| Sailsong Staff | Staff | Main Hand | Implemented | `EV_Sailsong_Staff` | Item-owned visual bundle |
| Valley Greatsword | TwoHandSword | Main Hand | Implemented | `EV_Valley_Greatsword` | Item-owned visual bundle |
| Watchman's Tower Shield | Shield | Off Hand | Implemented | `EV_Watchmans_Tower_Shield` | Item-owned visual bundle |

No current item assets use the Head slot, so no head visuals were authored in this pass.
