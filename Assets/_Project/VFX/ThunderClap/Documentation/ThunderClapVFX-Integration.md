# Thunder Clap VFX integration

`ThunderClapVFX` is presentation-only. Damage, the six-unit target query, attack-speed reduction, cooldowns, combat authority, and replication remain owned by the existing ability and session systems.

## Live ability path

`Warrior_Thunderclap.asset` keeps its existing `Warrior_Thunderclap_VFX.asset` definition. The definition now maps the release/cast phase to `ThunderClapVFX.prefab`; it does not use a separate gameplay hit prefab.

The same `AbilityReleased` presentation event creates the effect for the caster and replicated observers. The complete effect listens to `MMOCombatEventStream.CombatEventResolved` for authoritative `DamageResolved` records with ability id `warrior_thunderclap`. It schedules one pooled reaction per resolved target according to shockwave travel time. It never performs damage, radius checks, target filtering, or buff application.

This is the same runtime path in Play Mode and player builds. There are no editor-only VFX fallbacks, fake peers, local-only radius queries, or clock comparisons between machines.

## Reusable prefabs

- `ThunderClapCastVFX.prefab`: attached 0.14-second dust, stone, and spark anticipation.
- `ThunderClapImpactVFX.prefab`: compression, compact flash, earth explosion, dirt, rocks, and sparks.
- `ThunderClapShockwaveVFX.prefab`: pressure, physical/dust rings, crawling electricity, secondary strikes, dirt wake, sparks, and distortion.
- `ThunderClapTargetReactionVFX.prefab`: torso flash, body arcs, foot burst, brief debuff bands, and break sparks.
- `ThunderClapAftermathVFX.prefab`: rolling/suspended dust, settling debris, ground flickers, and residual arcs.
- `ThunderClapVFX.prefab`: complete live sequence with 12 pre-authored target-reaction instances.

## Attachment and simulation

- Anticipation sparks and brief target confirmation layers follow their character.
- Pressure, dust, physical, and lightning rings exist only while expanding.
- Dust, dirt, rocks, wake particles, foot bursts, sparks, smoke, and debris simulate in world space and finish after characters move.
- All particle counts are bounded. The profile quality level scales emission counts without changing gameplay timing or radius.

## Tuning

Edit `Profiles/ThunderClapVFX_Default.asset`. Inspector groups expose timing, impact density and velocity, the six-unit shockwave, lightning complexity and brightness, target reaction styling, colors, global scale/brightness, and particle quality.

The default `Ring Radius` is `6` to match gameplay. If gameplay radius changes, update the profile as a presentation follow-up; the VFX does not read or modify gameplay configuration.

## Authoring commands

- `Tools > RPG Clone > VFX > Build Thunder Clap VFX`
- `Tools > RPG Clone > VFX > Validate Thunder Clap VFX`

The build command configures generated texture import, creates the 13 reusable URP materials, authors all phase and complete prefabs, and wires the existing ability definition. The validator checks package completeness, world-space environmental layers, bounded particles, the pooled reaction count, six-unit radius, shader/material assets, and ability wiring.

## Generated texture manifest

Runtime textures live in `Textures`; untouched built-in-generation outputs are retained in `Textures/Sources`. The set contains heavy dust, fine dust/smoke, dirt and rock debris, pressure/shockwave/dust/compression rings, electrical ring/flash/crawler/branch shapes, electrical sparks, a horizontally tileable lightning core, and a tileable grayscale distortion map.
