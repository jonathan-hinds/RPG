# Lightning VFX integration

`LightningVFX` is presentation-only. Damage, target validation, cast authority, interruption, cooldowns, and replication remain owned by the existing ability/session runtime.

## Shared player and Trog path

Both `Shaman_Lightning_Bolt.asset` and `Trog_Lightning_Bolt.asset` reference `Shaman_Lightning_Bolt_VFX.asset`. The ability VFX controller receives the same replicated `CastStarted`, `CastInterrupted`, and `AbilityReleased` events used by player builds and enemy casters. No editor-only or local substitute path is used.

The shared definition maps phases as follows:

- Casting prefab: `LightningCastVFX.prefab`
- Cast/release prefab: `LightningBeamVFX.prefab`
- Hit prefab: `LightningImpactVFX.prefab`
- Standalone reusable aftermath: `LightningAftermathVFX.prefab`
- Package/catalog prefab: `LightningVFX.prefab`

## Attachment and world-space behavior

- `LightningCastVFX` resolves both hands through `MMOAbilityVfxAnchors`, with humanoid/name-based fallbacks already supplied by the project.
- Hand arcs, charge core, sparks, wrist electricity, and the seamless three-ring wind mesh follow the caster. The wind is closed torus geometry, so it does not camera-billboard, expose plane edges, or clip as a flat sprite scales. `Lightning_ChargeWindRibbon.png` tiles around the closed UV loop; per-ring UV offsets crawl in opposing directions with high-frequency electrical flicker.
- The wind rings contract from a tight `1.325` maximum radius to the original `0.48` inner radius. Their widened torus profile keeps more of the animated lightning ribbon visible during the full charge.
- Beam layers resolve the current cast-origin and target hit anchors, regenerate several readable jagged paths, and request the gameplay-timed hit through `MMOAbilityVfxContext.RequestHit`.
- Contact/body arcs follow the target while their controller is active.
- Dust, dirt, ground bursts, impact dust, and smoke use `ParticleSystemSimulationSpace.World`, so emitted particles remain planted while characters move.

## Bash and Charge dust reuse

The live environmental layers intentionally reuse the existing hand-painted assets:

- `Charge_HeavyDustAtlas.png`
- `Charge_FineDustAtlas.png`
- `Charge_DirtChunksAtlas.png`
- `Charge_GroundBurstAtlas.png`
- The wind motion follows the Charge air-compression timing, but uses a generated seamless torus mesh instead of the flat `Charge_AirCompressionAtlas.png` billboard.
- `Bash_DustRing.png`

They use the same atlas selection, world-space simulation, noise, fade, scale-over-lifetime, drag, and bounded-particle principles as Bash and Charge. Lightning changes the motion field: charge particles spawn around a ring and receive inward radial velocity plus a tangential spiral component, while the reused air-compression shapes pulse inward around the caster. On release, the field reverses into a compact outward ground burst.

`Lightning_DustAtlas_Optional.png` is retained as generated package art but is not used by the live default, preserving the established Warrior dust family requested for this effect.

## Tuning

Edit `LightningVFX_Default.asset`. It exposes charge electricity, environmental pull, dust density/radius/speed, beam widths and complexity, path refresh, branches and particles, impact layers, aftermath, palette, brightness, scale, and quality.

All temporary line layers are pre-authored in each prefab and reused for every path refresh; procedural playback does not instantiate per-branch objects. Particle counts are bounded and scaled by the profile quality level.

## Authoring commands

- `Tools > RPG Clone > VFX > Build Lightning VFX`
- `Tools > RPG Clone > VFX > Validate Lightning VFX`

The build command configures alpha texture import, creates reusable URP unlit materials and the profile, authors all phase prefabs, and updates the existing shared definition. The validation command checks package completeness, world-space environmental systems, definition timing ownership, and both Shaman/Trog ability references.
