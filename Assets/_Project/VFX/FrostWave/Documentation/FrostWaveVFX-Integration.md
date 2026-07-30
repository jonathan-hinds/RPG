# Frost Wave VFX Integration

## Runtime event path

`Mage_Frost_Wave` references `Mage_Frost_Wave_VFX`. Its cast prefab is spawned by the existing `MMOAbilityVfxController` when `MMOAbilitySystem.AbilityReleased` fires. That event is already replayed for remote participants by `PlayReplicatedAbilityReleased`, so local and remote players use the same prefab and runtime code.

The caster effect listens to `MMOCombatEventStream.CombatEventResolved` for authoritative `DamageResolved` records whose ability ID is `mage_frost_wave` and whose source matches the caster. Those results schedule the enemy impact at the same travel time as the expanding ring. No particle collision, physics overlap, or second targeting calculation is used.

The persistent root indicator waits for the target's replicated `mage_frost_wave` active buff. It reads `MMOActiveBuff.NormalizedRemaining` for presentation only and disappears immediately when the buff is removed, expires, or is dispelled. It does not own movement prevention or duration.

## Data controls

The effect uses `FrostWaveVFX_Default.asset`. The gameplay radius always comes from `MMOAbilityDefinition.AreaRadius`; the profile's `effectRadius` is only a safe preview fallback.

Primary production controls include ring expansion duration, radial-cloud density/size/lifetime/drift/lift, hero ice-breaker density/size/lifetime, overall intensity, ground-frost duration, particle amount and quality, light intensity/radius, enemy impact scale, root-indicator fallback duration, ground probing, reaction-pool size, and distance reduction.

`FrostWaveRadialFrontVFX` follows the established Earthquake/Thunderclap wake pattern: it continuously seeds particles at the moving gameplay-radius front instead of projecting mist onto the floor. The generated cloud sprites use vertical billboards and the hero-ice formations erupt on the same front. The former ground-hugging mist renderer was intentionally removed. Target impacts use a larger ten-point faceted ice clamp to communicate a hard freeze rather than Frost Shock's smaller slow treatment.

## Asset workflow

Generated chroma-key source atlases are retained under `Textures/Sources`. Clean alpha atlases and two derived noise crops live under `Textures`. The editor installer configures import settings, creates focused shared materials, low-poly meshes, reusable prefabs, a profile, and the ability VFX definition.

Run:

- `Tools/RPG Clone/VFX/Build Frost Wave VFX`
- `Tools/RPG Clone/VFX/Validate Frost Wave VFX`
- `Tools/RPG Clone/VFX/Preview Frost Wave VFX In Play Mode` (presentation diagnostic only)

The installer is idempotent and updates the existing assets in place.

## Performance notes

The caster and enemy-impact prefabs use the existing `MMOAbilityVfxPool`. Enemy reactions are acquired lazily from their shared prefab pool rather than duplicated inside every caster instance. Shared materials are animated with `MaterialPropertyBlock`, particle counts scale by profile quality, the ground probe uses a fixed non-allocating hit buffer, and no temporary effect continues updating after its authoritative/presentation lifetime.

Camera impulse is exposed as an optional profile preference but intentionally not dispatched because the project has no existing camera impulse or shake service. No new camera framework was added solely for this spell.
