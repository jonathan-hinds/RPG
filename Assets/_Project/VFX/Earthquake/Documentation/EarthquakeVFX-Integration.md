# Earthquake VFX integration

`EarthquakeVFX` is the complete reusable presentation package for `shaman_earthquake`. Its production profile, materials, meshes, generated texture atlases, complete prefab, and source images live together under `Assets/_Project/VFX/Earthquake`.

## Runtime ownership

- `MMOAbilitySystem` owns casting, target selection, range, damage, and combat timing.
- `MMOSharedSessionReplicator` publishes the authoritative `AbilityReleased` and `DamageResolved` records.
- Every client replays the release through `MMOAbilityVfxController`; the Earthquake definition points its cast phase at `EarthquakeVFX.prefab`.
- `EarthquakeVFX` only presents the replicated event. It does not detect enemies, deal damage, modify terrain, or own ability timing.
- Enemy reactions are driven by replicated `DamageResolved` records and delayed locally by distance / wave speed so they meet the visible ground front.

This is the same player-build runtime path used in Play Mode. No editor-only networking, simulated peer, fake transport, or local gameplay fallback is involved.

## Terrain matching

The effect samples the actual ground below the caster once per release. Terrain splat weights or the struck renderer's material classify the surface as dirt, stone, sand, or grass. A material property block selects the matching hand-painted atlas quadrant and blends the sampled surface tint onto chunk tops. Chunk sides keep an exposed dirt treatment. No permanent terrain deformation occurs.

Character colliders are explicitly rejected by the non-allocating ground probe, so the effect cannot anchor on the caster's head or body. Each crack and ground section is projected once onto its own ground point and normal; this keeps the presentation aligned across inclines, declines, and uneven terrain. When a dominant terrain or renderer texture is available, chunk tops use that actual texture through a property block and fall back to the hand-painted surface atlas otherwise.

## Performance and pooling

- The complete effect uses `MMOAbilityVfxPool`.
- Twelve enemy reaction objects and thirty ground sections are pre-authored and reused.
- Dust, dirt, haze, rocks, and reaction debris simulate in world space.
- Particle counts are profile quality-scaled and individually bounded below 384.
- Distance LOD reduces secondary particles and ground-section density while preserving the readable pressure ring.
- The package contains no lights or animators; motion is procedural.
- The complete VFX prefab contains no colliders, and every particle collision module is disabled. Ground pieces are visual-only and cannot affect navigation, characters, projectiles, or physics cost.

## Authoring commands

- `Tools > RPG Clone > VFX > Build Earthquake VFX` rebuilds meshes, materials, profile wiring, prefab contents, and the ability definition.
- `Tools > RPG Clone > VFX > Validate Earthquake VFX` verifies radius parity, asset completeness, pooling, world-space behavior, budgets, and ability wiring.

Generated source prompts produced four project-bound images:

1. 2x2 fracture, broken pressure ring, compression flash, and distortion/noise atlas.
2. 4x4 low dust, rolling haze, ground streak, and earth-smoke atlas.
3. 4x4 dirt clump, earth flake, rock, debris, and impact atlas.
4. 2x2 dirt, stone, sand, and dusty-grass terrain surface atlas.

The runtime images are under `Textures`; unchanged generated sources are preserved under `Textures/Sources`.

## Shared Charge earth library

Earthquake directly references the Charge package's heavy-dust, fine-dust, dirt-debris, rock, ground-burst, and shockwave materials. This keeps the game's earth effects visually related and avoids duplicated textures or materials. Earthquake owns its particle timing, world-space slope-aware motion, distance/quality scaling, and pooling; every reused layer has particle collision disabled and the prefab contains no Collider components.
