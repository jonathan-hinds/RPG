# HealingBeamVFX integration

`HealingBeamVFX` is visual-only. The spell remains the authority for targeting, cast completion, healing, and networking. During casting, `Shaman_Healing_Beam_VFX.asset` uses the dedicated `HealingBeamChargeVFX.prefab`, which contains the new golden caster glow, origin flash, orbiting stars, and inward spiral orbs centered at the casting anchor between the hands. The beam prefab is separate and spawns from `AbilityReleased` only after the cast finishes. `HealingBeamAbilityVfxAdapter` mirrors the target's actual `Healed` event into the traveling pulse and target burst on both authoritative and replicated runtime paths, then releases the one-shot effect.

## Runtime hook

The existing Healing Beam spell requires no call-site changes. `CastStarted` spawns only the hand-charge effect; it never spawns the beam. A successful `AbilityReleased` launches the beam, and the matching `Healed` event drives its pulse and target burst. An interrupted cast removes the hand charge and never launches the beam.

For reuse by another spell or a custom pool, the direct API remains:

```csharp
using RPGClone.Vfx.Healing;

// Acquire/instantiate Assets/_Project/VFX/HealingBeam/Prefabs/HealingBeamVFX.prefab.
HealingBeamVFX beam = pooledObject.GetComponent<HealingBeamVFX>();
beam.SetAttachmentPoints(casterVfxAnchor, targetVfxAnchor);
beam.Play();

// Call only when the authoritative spell reports that the heal resolved.
beam.TriggerHealingTick();

// Normal one-shot completion: fades and lets particles finish.
beam.Stop();

// Despawn/disconnect/invalid target: clears immediately.
beam.StopImmediate();
```

Subscribe to `Completed` or poll `ReadyForPool` before returning a normally stopped instance to the pool. Call `ResetForPool()` when releasing it. Do not network the VFX object itself; reproduce these calls from the same replicated spell events already used by the game.

## Authoring

- Edit `HealingBeamVFX_Default.asset` for width, colors, sway, flow, pulses, particle budgets, scales, fades, ground-ring size, and master intensity.
- `Endpoint Orb Size Multiplier` and `Endpoint Sparkle Size Multiplier` are shared by the caster charge and target endpoint so their visual sizes remain matched.
- Keep the prefab hierarchy's four top-level visual sections intact: `Beam Effect`, `Caster Effect`, `Target Effect`, and `Heal-Tick Burst Effect`.
- Re-run **Tools > RPG Clone > VFX > Build Healing Beam VFX** after changing source textures, shaders, or material construction.
- The beam uses 12 CPU-authored points and three reusable unlit material layers. It creates no lights, runtime textures, timelines, animation clips, or gameplay dependencies.
