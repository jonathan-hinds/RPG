# Arcane Missiles VFX integration

## Installed package

- `Prefabs/ArcaneMissilesVFX.prefab` is the caster-attached channel package. It contains both hand glows, the central core, the restrained channel circle, three independently layered fabrication orbs, and four energy ribbons.
- `Prefabs/ArcaneMissileProjectileVFX.prefab` is the pooled layered homing missile with four braided trail layers and world-space fragments, vapor, and motes.
- `Prefabs/ArcaneMissilesImpactVFX.prefab` is the pooled compact impact with contact flash, rounded explosion, target wrap, particles, and the optional second final-missile shock ring.
- `Prefabs/ArcaneMissilesInterruptVFX.prefab` is the reusable pooled interruption collapse.
- `Profiles/ArcaneMissilesVFX_Default.asset` contains all channel, fabricator, projectile, trail, impact, final-missile, interruption, palette, scale, brightness, quality, and firing-order controls.
- `Materials` contains independently animated URP materials for cores, orb layers, shell, runes, rings, connections, projectile layers, trails, fragments, vapor, impact layers, channel circle, and distortion.
- `Textures/Sources/ArcaneMissiles_SourceAtlas.png` is the original generated hand-painted 5x4 source atlas. `Textures` contains the twenty alpha-ready runtime textures derived by the installer.

## Multiplayer-safe runtime flow

Arcane Missiles VFX is presentation-only. It never applies damage, chooses targets, advances combat ticks, detects movement, or decides interruption behavior.

1. `MMOAbilitySystem.CastStarted` is published by the existing authority/session path and is replayed for remote participants by `PlayReplicatedCastStarted`.
2. `MMOAbilityVfxController` spawns the dedicated caster prefab from the ability's `MMOAbilityVfxDefinition`; editor and player builds use the same path.
3. The VFX predicts each launch from the ability's authored channel duration and periodic tick interval so the missile has readable travel time. Prediction affects presentation only.
4. Each target `MMOCombatant.Damaged` event for this caster and ability confirms one impact. Replicated `DamageResolved` records call `ApplyResolvedDamage`, which raises the same presentation event for remote observers. The impact therefore follows authoritative damage rather than a local VFX timer.
5. `CastCompleted` runs the sequential normal cleanup. `CastInterrupted` runs the reusable collapse, stops future launches, snaps connections, shatters the fabricators, and dissolves unconfirmed missiles.

The fifth missile uses the final scale, brightness, trail, fragment, and impact multipliers. The default configurable firing order is left, right, upper, rebuilt left, upper.

## Spatial behavior and pooling

Hand glows, central core, fabrication orbs, rings, ribbons, and channel circle remain caster attached. Projectiles home independently toward the resolved target anchor. Trail particles, fragments, vapor, sparks, impact fragments, and interruption debris use world simulation space.

The casting package, missile, impact, and interruption prefabs all use `MMOAbilityVfxPoolable`. Runtime state implements `IMMOAbilityVfxPoolReset`; shared materials are animated with `MaterialPropertyBlock` rather than cloned per caster.

## Authoring and regeneration

Use **Tools > RPG Clone > VFX > Install Arcane Missiles VFX** to regenerate runtime textures, materials, all four prefabs, the default profile, and the ability binding. Use **Validate Arcane Missiles VFX** to verify source/runtime textures, shader support, layered prefab structure, world-space particles, five-second channel settings, movement interruption, and ability wiring.

The general ability VFX installer preserves the dedicated package when it exists and only falls back to the legacy generic arcane beam when this package is absent.

## Source-art generation

The checked-in source atlas was created with the built-in image-generation path using this production prompt summary: a fully original, hand-painted, chunky, classic fantasy MMORPG-style 5x4 atlas on pure black, with isolated orb, core, rune, ring, fragment, projectile, flare, shell, trail, vapor, impact, spike, spark, distortion, hand-glow, connection, and channel-circle cells in a cyan-blue, royal-blue, violet, purple, magenta, white, and indigo palette. No text, logos, characters, scene, or watermark were requested.
