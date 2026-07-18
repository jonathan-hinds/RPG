# ChargeVFX integration

`ChargeVFX.prefab` is the complete presentation prefab for Warrior Charge. It is wired as the ability definition's cast prefab; there is intentionally no separate hit prefab. `MMOAbilityVfxController` creates it from the existing `ChargeStarted` event, and `ChargeVFX` listens to the same ability system's `ChargeImpactStarted` and `ChargeCompleted` events. It never changes movement, targeting, damage, timing, or network authority.

## Space ownership

- **World space:** launch dust, launch dirt and rocks, launch ring, heavy/fine trail deposits, ground scrapes, impact dust, ground shards, debris, impact ring, and recovery dust all use `ParticleSystemSimulationSpace.World`. Once emitted, those particles retain their spawn positions while the Warrior moves away.
- **Character space:** speed streaks, compressed-air arcs, and armor glints use `ParticleSystemSimulationSpace.Local`. They travel with the Charge prefab and stop at the beginning of impact.

The prefab itself stays parented beneath the caster's existing `Ability VFX` root. This gives the three character layers the correct attachment for free while world-space particles remain independent after emission.

## Runtime sequence

1. `ChargeStarted` creates the prefab and `Initialize` emits the launch burst and starts the three attached layers.
2. The launch also seeds a small rearward momentum deposit and first-frame streak/air/glint particles, so the effect reads immediately without shortening any lifetime. While the Warrior moves, the controller deposits localized heavy dust, fine dust, dirt, and scrape particles by distance and profile rate. Existing particles are never repositioned.
3. `ChargeImpactStarted` stops attached motion layers and emits the collision stack at the resolved contact point.
4. `ChargeCompleted` begins recovery. World particles keep simulating for `Recovery Duration`, then the lightweight controller destroys itself.
5. A bounded `Maximum Travel Duration` is a visual-only cleanup guard for interrupted or invalidated charges.

## Authoring and inspector

Edit `ChargeVFX_Default.asset` rather than changing particle-system values in the prefab. It exposes launch dust, trail spawn rate, dust lifetimes, dirt frequency/size, scrape frequency, shockwave size/speed, streak intensity, air/glint rates, collision size/dust, recovery duration, overall brightness, and the reusable dust/dirt/rock/motion palette.

The polished default intentionally front-loads dust opacity and scale, increases launch/trail density, and doubles the layered shockwave emission. Heavy dust, fine dust, collision dust, shockwave, and recovery lifetimes remain at their original values so the battlefield settles for the same duration.

Regenerate or validate content from:

- `Tools > RPG Clone > VFX > Build Charge VFX`
- `Tools > RPG Clone > VFX > Validate Charge VFX`

The build command configures texture import, reusable URP unlit materials, the profile, prefab hierarchy, and the existing `Warrior_Charge_VFX` definition. Source art sheets are retained under `Textures/Sources`; runtime atlases contain alpha and multiple variations per required texture family.
