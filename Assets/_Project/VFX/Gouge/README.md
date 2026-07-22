# Gouge VFX

`GougeVFXInstaller` is the source-of-truth authoring workflow for this package. Run **Tools > RPG Clone > VFX > Build Gouge VFX** after changing textures, shader defaults, profile defaults, or prefab composition, then run **Validate Gouge VFX**.

The package uses two pooled prefabs:

- `GougeCastVFX` presents the 0.08-second physical anticipation, main hooked weapon trail, delayed crimson tearing trail, weapon glints, arm dust, and abstract motion fragments at the replicated caster.
- `GougeVFX` resolves a stable attacker-facing collider surface at the supplied hit height, follows that surface in target-local space, and contains named reusable sections for impact, grounded force reaction, persistent bleed, bleed ticks, stack increases/refreshes, critical cooldown-reset feedback, and wound expiration.

Tune `Profiles/GougeVFX_Default.asset`. The inspector exposes attack motion, impact, persistent wound, tick, three-stack scaling, critical reset, expiration, master palette, gore intensity, and particle quality. The VFX never calculates damage, critical hits, cooldowns, stack rules, ticks, or target selection.

## Multiplayer presentation

The existing `MMOAbilitySystem.AbilityReleased` event is already replicated for player and enemy casters. It creates both Gouge prefabs on every receiver. `GougeVFXEventRelay` listens to the same `MMOCombatEventStream` that receives replicated `DamageResolved` results, so normal hits, critical hits, and periodic bleed ticks produce the same presentation for local and remote observers.

The relay stores receiver-local arrival time only. It never compares a short-lived event with a timestamp from another machine. A tiny receiver-local recent-result cache covers release/damage reordering without changing authority or gameplay state. The nine-second presentation duration refreshes when the replicated ability release arrives; the host remains authoritative for actual damage, stacks, cooldown reset, and buff expiration.

Reapplications reuse the existing target-attached wound, select one of three progressively more severe wound silhouettes, reset the receiver-local presentation duration, and play the stack-increase or max-stack refresh reaction. Periodic `DamageResolved` results after the release window play varied tick reactions. Critical results add the second tear, larger blood burst, physical sparks, and the contracting reset ring at the replicated caster's weapon hand.

## Generated textures

The package's original source textures and the v2 wound-state atlas were created with the built-in image-generation workflow as original hand-painted fantasy MMORPG VFX on a flat chroma-key background, then converted to alpha PNGs with soft matte and despill. The grounded reaction deliberately reuses Bash's proven dust-puff and ring masks so physical abilities share a consistent environmental language.

Prompt set:

1. A tightly padded 2x2 atlas of compact, hooked, crossed, and triple-channel wound states with near-black maroon cavities and crimson inner edges; the particle sheet module selects one cell, never the full sheet.
2. A broad tapered hooked physical weapon trail with a white-yellow leading edge, metallic-gray/brown center, and broken rear brushwork.
3. A narrow jagged crimson/maroon tearing trail with torn edges and droplet fragments.
4. A padded 2x2 atlas of warm-white/pale-yellow physical contact flashes, including a crimson-centered critical variation.
5. A padded 4x2 atlas of directional splashes, long streaks, thick and small droplets, painterly mist, and an irregular blood pulse.
6. A 4x2 atlas of torn cloth, leather, armor chips, metallic streaks, glints, and warm-white fragments.
7. A broken pale-yellow physical critical-reset ring with white glints and restrained crimson accents, without runes or symbols.
8. A 2x2 atlas of compact dark-red painterly wound-mist accents with broken edges.

Every prompt prohibited text, watermarks, scenes, characters, photorealistic gore, magical runes, fire, and blue/purple supernatural effects. The generated files live in `Textures/` and are imported as clamped, mipmapped, compressed alpha textures. The v2 wound, blood, and contact sheets add per-cell transparent gutters so no visible alpha reaches a tile edge. Persistent wounds use the same simple camera-facing convention as Fireball: retain the target-local hit height, offset toward the active camera, and explicitly face that camera every frame so the mark remains readable from oblique views.

## Material and performance model

All visual layers use Unity `ParticleSystem` components and the same simple `_BaseMap` URP sprite-material contract used by the existing physical VFX packages. Gouge directly reuses Charge's ground-burst, heavy-dust, fine-dust, and dirt materials plus its layered hemisphere/noise treatment for the environmental kick-up. Color, opacity, scale, atlas selection, and fades are driven by particle modules and shared materials rather than per-renderer property blocks. Impact, tick, stack, and critical bursts use bounded particle counts; persistent seepage/mist is sparse; world-space particles detach naturally; both root prefabs use `MMOAbilityVfxPool`.

The generic **Install Ability VFX Content** workflow preserves these Gouge prefabs when they exist, so running the shared installer does not replace the package with the legacy physical-impact burst.
