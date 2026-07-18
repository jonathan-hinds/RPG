# MageArmorApplyVFX integration

`MageArmorApplyVFX` is a one-shot, visual-only confirmation that Mage Armor was applied. Buff duration, mana regeneration, authority, replication, and the persistent buff icon remain owned by the existing ability and buff systems. The world-space effect completes in about one second and leaves no persistent aura.

## Existing ability wiring

The builder assigns `MageArmorApplyVFX.prefab` as the hit/application prefab in `Mage_Mage_Armor_VFX.asset`, attaches it to the resolved self target, and keeps the generic replicated ability-presentation path intact. It does not introduce editor-only or single-player behavior.

## Direct or pooled use

```csharp
using RPGClone.Vfx.Arcane;

MageArmorApplyVFX effect = pooledInstance.GetComponent<MageArmorApplyVFX>();
effect.Play(casterTransform, torsoSocket);

// Return after Completed or when ReadyForPool becomes true.
effect.StopImmediate();
effect.ResetForPool();
```

Passing a torso socket makes a directly pooled instance follow that socket. When spawned through `MMOAbilityVfxController`, the existing VFX definition resolves and parents the application effect to the caster.

## Authoring

- Edit `MageArmorVFX_Default.asset` for scale, brightness, duration, shell behavior, layer counts, timings, and the Arcane palette.
- Re-run **Tools > RPG Clone > VFX > Build Mage Armor VFX** after changing textures, shader construction, or material construction.
- Use **Tools > RPG Clone > VFX > Validate Mage Armor VFX** to verify the prefab sections, texture/material budget, procedural lifecycle, and ability wiring.
- The effect uses no lights, animation clips, Timeline, runtime texture generation, networking branches, or editor-only runtime fallbacks.
