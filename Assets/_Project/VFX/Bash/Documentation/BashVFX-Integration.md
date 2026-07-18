# BashVFX integration

`BashVFX` is visual-only. Damage, hit validation, stun authority, cooldowns, animation, and networking remain owned by the existing ability runtime. The package replaces only `Warrior_Bash_VFX.asset`'s hit prefab, so editor and player builds continue to use the same replicated ability-release path.

## Existing ability wiring

Run **Tools > RPG Clone > VFX > Build Bash VFX** after changing source textures, the shared shader, material construction, or prefab authoring. The installer creates `BashVFX.prefab`, assigns it to `Warrior_Bash_VFX.asset`, and preserves the ability asset's existing reference to that definition.

The generic hit wrapper calls `Play(profile.ShowStunAccentByDefault)`. This matches Bash when its stun is guaranteed. If gameplay can report an immune, resisted, or otherwise unsuccessful stun, keep that decision in the authoritative gameplay layer and call the explicit presentation API with the replicated result:

```csharp
BashVFX effect = pooledInstance.GetComponent<BashVFX>();
effect.SetImpactDirection(targetPosition - sourcePosition);
effect.Play(stunApplied);
```

Do not infer or roll stun success inside the VFX. Reproduce the `stunApplied` presentation value from the same authoritative/replicated combat result used by gameplay.

## Tuning and pooling

- Edit `BashVFX_Default.asset` for overall scale, brightness/tint, flash intensity, burst size/count, directional follow-through, dust amount/size, radial dust-ring radius/opacity/duration, spark count/speed, stun stars/duration/orbit, and swing-arc size.
- Subscribe to `Completed` or poll `ReadyForPool` before returning a normally completed instance. Call `ResetForPool()` before reuse.
- Impact layers finish in under half a second. Only the optional stun accent remains longer.
- The prefab is procedural, unlit, light-free, animator-free, and uses bounded particle counts suitable for an elevated MMORPG camera.
