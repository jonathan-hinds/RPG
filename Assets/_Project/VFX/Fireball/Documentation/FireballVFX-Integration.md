# FireballVFX integration

`FireballVFX` is visual-only. Targeting, damage, projectile movement, collision, spell timing, and networking remain owned by the existing ability runtime. The installer wires lightweight phase wrappers into `Mage_Fireball_VFX.asset`; the projectile wrapper retains the project's existing `MMOAbilityVfxProjectile` movement component and only attaches the visual to it.

## Existing spell wiring

- `FireballVFX_Casting.prefab` starts the buildup at the resolved casting anchor and releases its launch flash when the cast completes.
- `FireballVFX_Projectile.prefab` starts the core, layered flame shell, short trails, embers, and smoke. `MMOAbilityVfxProjectile` remains responsible for travel and hit timing.
- `FireballVFX_Impact.prefab` plays the flash, burst, shockwave, embers, smoke, and optional scorch at the resolved hit point.
- All three wrappers contain the reusable `FireballVFX.prefab`, whose five top-level visual sections remain available for direct integration and pooling.

## Direct or pooled use

```csharp
using RPGClone.Vfx.Fire;

FireballVFX effect = pooledInstance.GetComponent<FireballVFX>();

effect.SetCastPoint(castSocket);
effect.PlayCasting();
effect.ReleaseCasting();

effect.AttachToProjectile(existingGameplayProjectile.transform);
effect.PlayProjectile();

// Before destroying a directly attached pooled instance, detach/reposition it or
// acquire a second pooled instance for the impact phase.
effect.TriggerImpact(hitPoint, hitNormal);

effect.StopImmediate();
effect.ResetForPool();
```

Subscribe to `Completed` or poll `ReadyForPool` before returning a normally completed instance. Reproduce these calls from the same replicated spell events already used by gameplay; do not network the VFX object itself.

## Authoring

- Edit `FireballVFX_Default.asset` for projectile/core/flame size, trail lifetime/width/brightness, HDR fire colors, flicker, distortion, particle budgets, impact/shockwave scale, durations, scorch, and master intensity.
- Re-run **Tools > RPG Clone > VFX > Build Fireball VFX** after changing textures, shaders, or material construction.
- Disable `Enable Scorch` in the profile for targets or environments where a flat temporary mark is unsuitable.
- The effect uses no lights, Timeline, animation clips, runtime texture generation, editor-only runtime fallback, or gameplay/network branching.
