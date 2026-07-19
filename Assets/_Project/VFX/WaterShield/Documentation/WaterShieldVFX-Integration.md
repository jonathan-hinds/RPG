# Water Shield VFX integration

## Installed package

- `Prefabs/WaterShieldVFX.prefab` is the combined activation, persistent three-orb formation, absorb response, mana transfer, and expiration setup used by the ability.
- `Prefabs/WaterShieldOrbVFX.prefab` is the reusable eight-layer living-water orb.
- `Prefabs/WaterShieldActivationVFX.prefab`, `WaterShieldAbsorbReactionVFX.prefab`, `WaterShieldManaRestoreVFX.prefab`, and `WaterShieldExpirationVFX.prefab` are independently reusable phase effects.
- `Profiles/WaterShieldVFX_Default.asset` contains the full orbit, orb, material-animation, trail, particle, reaction, palette, quality, brightness, scale, activation, and expiration controls.
- `Materials` contains separate URP materials for the inner core, two main-water layers, outer shell, foam highlights, deep-water shadow, refraction, two trails, mist, droplets, splashes, mana, protective arc, and supporting phase accents.
- `Textures/Sources` contains the original generated hand-painted source art. `Textures` contains the alpha-ready and installer-derived runtime textures.

## Runtime flow

No gameplay authority is replaced or simulated. `MMOAbilityVfxController` creates `WaterShieldVFX` through the same release/hit path used by player builds and replicated sessions.

1. The combined prefab receives the caster, resolved self target, and ability through `IMMOAbilityVfxInstance`.
2. The activation prefab gathers world-space droplets from the surrounding air into three sequential condensation streams. Each orb grows from its gathered water, then the formation performs a fast orbit sweep.
3. Three instances of the shared orb prefab remain attached to the caster while `shaman_water_shield` is present in `MMOCharacterBuffController`. If replicated buff state is temporarily unavailable, the authored ability duration supplies the player-build-safe fallback.
4. `MMOCharacterBuffController.DamageAbsorbedAsMana` exposes the already-resolved absorbed amount as a presentation event. The VFX pairs it with `MMOCombatant.Damaged` to select the orb nearest the incoming direction, disturb the orbit, splash, flash a protective arc, and transfer mana back to the chest.
5. Buff removal triggers the authored collapse and expiration sequence. Hard despawn uses `StopImmediate()`.

## Spatial behavior and pooling

The orbit, layered orb meshes, mana cores, and reaction paths are caster/orb attached. Droplets, fine spray, mist, detached splashes, and released mana motes simulate in world space and finish where emitted.

Temporary absorb and mana-reaction instances are retained and reused by `WaterShieldVFX`; they do not allocate a new effect for every absorbed hit. `WaterShieldVFX` exposes `ReadyForPool` and `ResetForPool` for an external prefab pool. Material animation uses shared materials plus `MaterialPropertyBlock`, avoiding per-caster material cloning.

## Authoring and regeneration

Use **Tools > RPG Clone > VFX > Install Water Shield VFX** to regenerate derived textures, materials, all prefabs, and ability bindings from the checked-in source art and scripts. Use **Validate Water Shield VFX** to verify the source/runtime texture set, material library, custom shaders, prefab layers, world-space particles, procedural lifecycle, and ability wiring.

Re-running the general ability VFX installer preserves `WaterShieldVFX.prefab` and only falls back to the legacy generic aura when the dedicated package is absent.

The refraction shell samples URP's camera opaque texture. Keep **Opaque Texture** enabled on the active URP asset for visible scene distortion; every other water layer remains readable when it is disabled.
