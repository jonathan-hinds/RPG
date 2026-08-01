# Press the Attack VFX integration

## Runtime ownership

`PressTheAttackVFX` is the single runtime owner for activation, persistent character overlays, movement response, attack accents, confirmed-hit accents, final-second instability, fade-out, and pooled cleanup. Tunable values live in `PressTheAttackVFX_Default.asset`; the ability references `PressTheAttack_VFX.asset` through the established `MMOAbilityVfxDefinition` workflow.

The persistent presentation does not run an independent six-second timer. It observes the replicated `warrior_press_the_attack` entry in `MMOCharacterBuffController`, uses that buff's remaining time for the final-second response, and fades only after the replicated buff disappears. The short handshake timeout only releases an unmatched activation presentation if no authoritative buff arrives.

## Multiplayer path

- Activation and attack-response pulses originate from the existing replicated `MMOAbilitySystem.AbilityReleased` path.
- Persistent lifetime follows the existing authoritative buff application/removal replication path.
- Confirmed-hit accents originate from replicated `MMOCombatEventStream.CombatEventResolved` damage events.
- Local and remote characters execute the same runtime code. No editor-only or single-player presentation path is used.

## Conforming character treatment

At activation, the controller discovers eligible body and equipped-armor `MeshRenderer` and `SkinnedMeshRenderer` components. It calculates one stable caster-root projection volume across the complete modular character, then gives every body and armor shell that same volume and live world-to-caster matrix through `MaterialPropertyBlock`. Texture coordinates therefore remain continuous across head, torso, hands, legs, feet, and replacement armor instead of restarting in each renderer's local bounds. The shells reuse the source meshes, bones, and root bone, so they continue to follow skinned animation.

Three deliberately separated layers provide a painted rage undercoat, dominant crawling crimson electricity, and a supporting momentum/silhouette corona. Each shell has a distinct surface offset to prevent depth competition. Transparent/additive materials, particle/trail/line renderers, ability-VFX-owned renderers, nameplates, shadows, weapons, and previously generated overlay shells are rejected before projection sources are collected. This prevents Press the Attack from cloning Berzerkitis alpha cards or recursively wrapping another presentation effect.

The persistent material uses five standalone 1024px full-surface textures under `Textures/SurfaceV2`, plus localized snapping phases, counter-scrolling texture layers, travelling charge bands, shared caster-space triplanar projection, and response-driven emission. Authored crawling-lightning art owns the visible bolt shapes; shader-generated paths are secondary motion detail. Flipbook atlases remain limited to short activation and impact punctuation; they do not define the lasting character state.

Equipment rebuilds are handled through `MMOPlayerEquipmentVisuals.VisualsRebuilt`. Weapon attachment slots are excluded because this ability is authored as a whole-character rage state, not a weapon enchant.

## Asset layout

- `Textures`: standalone masks/noise/sprite textures for mesh-conforming and modular particle layers.
- `Textures/Atlases`: seven 4x4 activation/response animation sheets.
- `Materials`: shared character-overlay, ground, activation, movement, attack, and hit materials.
- `Shaders`: focused URP HLSL shaders following this project's established VFX shader workflow.
- `Prefabs`: modular activation, persistent, movement, attack, hit, combined, and activation-only prefabs.
- `Profiles`: data-driven palette, intensity, timing, response, and particle configuration.

Run `Tools/RPG Clone/VFX/Install Press the Attack VFX` to idempotently rebuild the authored assets after changing installer defaults.

Run `Tools/RPG Clone/VFX/Press the Attack/Preview Modular Skinned Character (Play Mode)` for diagnostic projection QA on the project's five-part player model. This tool is visual QA only and is not evidence of multiplayer replication.
