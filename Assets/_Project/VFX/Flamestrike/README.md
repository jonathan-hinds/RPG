# Flamestrike VFX

`FlamestrikeVFXInstaller` is the source-of-truth authoring workflow for this package. Run **Tools > RPG Clone > VFX > Build Flamestrike VFX** after changing textures, shader defaults, or prefab composition, then run **Validate Flamestrike VFX**.

## Runtime integration

- `MMOGroundTargetingController` uses `MMOAbilityVfxDefinition.TargetingPrefab` when one is assigned. The Flamestrike preview receives terrain position, surface normal, gameplay radius, and valid-range state through `IMMOGroundTargetingVfx`.
- `FlamestrikeCastVFX` is one composed caster effect. It resolves both hand anchors through `MMOAbilityVfxAnchors`, follows authoritative cast progress, and keeps the selected ground buildup fixed at the cast target.
- `FlamestrikeVFX` is spawned by the existing `AbilityReleased` presentation event at the replicated world-space target. It owns initial impact, the persistent eight-second field, damage pulses, target reactions, and expiration.
- Damage pulses and enemy reactions listen to `MMOCombatEventStream.CombatEventResolved`. Events are filtered by ability, caster, and field radius. Same-tick multi-target records are grouped into one area pulse while each affected target receives at most one pooled reaction.
- No damage, target selection, radius query, or tick timer is implemented by the VFX. Host-authoritative gameplay and remote clients use the same `MMOAbilitySystem`/combat-event presentation path.

## Visual construction

The dominant visuals are mesh based: procedural circular discs for ground layers, toruses for thin boundaries and pulses, four large slightly offset vertical tube shells for the centered fire vortex, and layered spheres for compressed cores. The hit prefab contains no atlas cards. `FlamestrikeTubeShellVFX` expands each shell once and receives a monotonic lifetime fade; only the shader mask moves continuously, so visibility never loops or bobs. Three delayed `FlamestrikeExpandingRingVFX` layers also use tall tube meshes: each rotates, expands from the center to the full ground perimeter, collapses its entire height to zero, and reduces opacity continuously to zero over the same travel time. The Charge heavy-dust smoke crown emits for the full burn duration and stops with the other continuous systems at expiration.

The targeting preview is a dedicated blue radial-gradient shader on circular meshes. Invalid placement changes the same indicator to red without creating a second prefab or material.

The package uses these generated, tintable textures:

- `Flamestrike_FlameAtlas_Polished.png`: retained for non-card radial accents and compatibility with the earlier authored package.
- `Flamestrike_GroundScorch_Polished.png`: a circular scorched footprint with a compressed center and branching cracks.
- `Flamestrike_TubeFlowMask.png`: a mirrored-wrap grayscale flow mask used only as procedural shader data; shader properties provide color, motion, erosion, and lifetime fading.
- `Flamestrike_UtilityAtlas.png`: lightweight embers, sparks, debris, and distortion utilities.

Chroma-key source images are retained under `Textures/Sources`. Seventeen shared URP materials select atlas cells through `_AtlasRect`; runtime variation uses material property blocks, so overlapping casts do not duplicate materials. Smoke, ash, expiration plumes, and target smoke reuse Charge's `Charge_HeavyDust` and `Charge_FineDust` materials and 4x2 particle animation workflow.

## Tuning

Tune `Profiles/FlamestrikeVFX_Default.asset`. The inspector groups targeting, cast, impact, persistent ground, pulse, reaction, expiration, global color, quality, and particle-budget controls. The default radius is five units, cast duration is two seconds, and persistent duration is eight seconds to match `Mage_Flamestrike.asset`.

The polished source prompts requested a padded grayscale 2x2 hand-painted flame atlas and a single top-down scorched footprint on a uniform `#ff00ff` background. Both were generated with the built-in image tool and converted to alpha locally with the image-generation skill's chroma-key helper. Earlier atlases remain in place for history and utility compatibility, but the rebuilt prefabs use the polished textures for their primary shapes.
