# Berzerkitis VFX integration

## Installed assets

- `Prefabs/BerzerkitisVFX.prefab` is the combined activation and persistent-buff setup.
- `Prefabs/BerzerkitisActivationVFX.prefab` is the reusable one-shot activation-only setup.
- `Prefabs/BerzerkitisHandBuffVFX_Left.prefab` and `BerzerkitisHandBuffVFX_Right.prefab` are independently reusable hand effects.
- `Profiles/BerzerkitisVFX_Default.asset` contains all activation, emblem, hand, palette, quality, brightness, offset, pulse, and fade controls.
- `Warrior_Berzerkitis_VFX.asset` points its hit phase at the combined prefab. The ability asset references that definition.
- `Textures/Sources` contains the generated painted chroma-key source art. The alpha-ready runtime textures and derived emblem masks are in `Textures`.

## Runtime flow

No gameplay ability code is replaced or bypassed. `MMOAbilityVfxController` creates the combined effect through the same release/hit path used in player builds and replicated sessions.

1. The combined prefab receives the caster, target, and ability through `IMMOAbilityVfxInstance`.
2. The 1.25-second activation plays attached body layers and world-locked ground layers.
3. At concentration timing, the left and right hand prefabs attach to `MMOAbilityVfxAnchors.LeftHandAnchor` and `RightHandAnchor`.
4. The effect observes `MMOCharacterBuffController` for `warrior_berzerkitis`. When replicated buff state is not locally available, it uses the ability effect's authored duration so remote visuals retain parity.
5. Successful damage events from the buffed combatant pulse both hands through the shared combat-event stream.
6. Buff removal triggers the authored 0.3-0.5 second extinguish. `StopImmediate()` is available for despawn, pooling, or hard cancellation.

## Attachment requirements

Add or configure `MMOAbilityVfxAnchors` on character roots. Explicit left/right hand anchors give the best animation tracking. If an anchor is absent, the package falls back to the character root and conservative local offsets from the profile.

World-space dust, smoke, embers, sparks, shockwave, and trails use world simulation or a world-position lock. They remain at their emission position while the character moves. Body, emblem-formation, arm-transfer, bands, and hand layers remain character attached.

## Pooling

`BerzerkitisVFX` implements `IBerzerkitisVFX` with `ReadyForPool` and `ResetForPool`. Set `destroyOnComplete` off in a pooled variant, return it when `ReadyForPool` becomes true, and call `ResetForPool` before reuse. Hand particle systems and material instances are reused throughout a buff; animation uses material property blocks, avoiding per-caster material cloning.

## Authoring and regeneration

Use **Tools > RPG Clone > VFX > Install Berzerkitis VFX** to regenerate derived masks, materials, prefabs, and bindings from the checked-in source textures and scripts. Re-running the general ability VFX installer preserves the dedicated Berzerkitis prefab and only falls back to Blood Fury when the package is absent.

The heat layer samples URP's camera opaque texture. Keep **Opaque Texture** enabled on the active URP renderer/asset for visible scene refraction; all fire and buff layers continue to render if it is disabled.
