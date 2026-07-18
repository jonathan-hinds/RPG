# FireBlastVFX integration

`FireBlastVFX` is a visual-only, non-projectile target-combustion effect. The existing Fire Blast ability remains authoritative for targeting, damage, cooldowns, animation, and networking. The prefab never moves an object toward the target and contains no `MMOAbilityVfxProjectile`.

## Existing spell wiring

`Mage_Fire_Blast_VFX.asset` uses `FireBlastVFX.prefab` as its single release prefab. The shared ability VFX controller passes the replicated caster and target context to `FireBlastVFX.Initialize`. The cast still connects to the target in only a few frames, but the polished impact now lasts about 1.05 seconds: a compression flash, outer combustion disc, white-hot core, two heat rings, dense flame lobes, embers, sparks, a slower lingering flame bloom, smoke, and residual heat.

No Fire Blast gameplay call-site changes are required. Re-run **Tools > RPG Clone > VFX > Build Fire Blast VFX** if the generic ability VFX content installer is run again.

## Direct or pooled use

```csharp
using RPGClone.Vfx.Fire;

FireBlastVFX effect = pooledInstance.GetComponent<FireBlastVFX>();
effect.Play(casterHand.position, targetHitPoint);

// Return a non-destroying authored variant after completion, or stop it early.
effect.Completed += ReturnToPool;
effect.StopImmediate();
effect.ResetForPool();
```

The shipped integration instance destroys itself after the sequence. For pooling, author a variant with `Destroy On Complete` disabled and wait for `Completed`/`ReadyForPool`.

## Authoring

- Edit `FireBlastVFX_Default.asset` for overall scale, fixed-ribbon length and brightness, explosion size and brightness, flame count and size, lingering-fire duration, heat-ring size and speed, ember count and speed, spark count, smoke amount and duration, total duration, master brightness, and the full fire palette.
- The eleven URP materials are unlit, transparent, instancing-enabled, and reusable. Compression flash, outer combustion, primary fire, lingering fire, and both heat-ring layers have independent materials, so their intensity and tint can be tuned without affecting other layers. Fire uses soft additive blending; smoke uses standard alpha blending.
- The effect uses procedural scale, counter-rotation, opacity, UV scrolling, particle velocity, color/size over lifetime, and controlled burst delays. It uses no lights, Timeline, animation clips, runtime material instances, editor-only runtime fallbacks, or multiplayer/gameplay branching.

For deterministic visual review, enter and pause Play Mode, then use **Tools > RPG Clone > VFX > Stage Fire Blast Play Mode Preview** for the 0.085-second impact frame or **Stage Fire Blast Lingering Preview** for the 0.32-second target-burn frame.

## Generated texture prompts

The texture sources were generated with the built-in image generation workflow as original, hand-painted fantasy MMORPG VFX on a flat black extraction background, then converted to straight-alpha PNGs. The seven prompts requested, respectively: a white-hot circular combustion core; a 2x2 atlas of asymmetrical chunky flame tongues; a horizontally tileable compressed fire ribbon with a white-yellow center; a broken painterly heat ring; a 2x2 atlas of glowing orbs and burning fragments; a 2x2 atlas of elongated sparks; and a 2x2 atlas of subtle warm-charcoal smoke puffs. Every prompt prohibited text, watermarks, scenes, photorealism, and green/blue hues.
