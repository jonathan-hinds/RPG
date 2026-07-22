# Gouge VFX integration

`Warrior_Gouge.asset` references `Warrior_Gouge_VFX.asset`, whose cast prefab is `GougeCastVFX.prefab` and hit prefab is `GougeVFX.prefab`. Hit delay is zero so the target-attached listener exists before a same-frame critical result is published.

No gameplay call-site change is required. The package consumes:

- replicated `AbilityReleased` context for caster, target, hit attachment, strike direction, stack application/refresh presentation, and the nine-second visual duration;
- replicated `DamageResolved` combat events for authoritative critical confirmation and each periodic bleed-tick reaction;
- the target combatant's death event for early wound expiration.

It does not apply or inspect damage formulas, roll critical chance, reset cooldowns, schedule ticks, or calculate stacks. `GougeVFXEventRelay` is presentation-only and caches events by the receiver's resolved target object using receiver-local arrival time.

For content rebuilds:

1. Run **Tools > RPG Clone > VFX > Build Gouge VFX**.
2. Run **Tools > RPG Clone > VFX > Validate Gouge VFX**.
3. Run the `GougeVFXTests` EditMode group.
4. Validate a host and remote player build: normal hit, critical reset, three reapplications, all three periodic ticks, target movement, target death, and natural expiration.
