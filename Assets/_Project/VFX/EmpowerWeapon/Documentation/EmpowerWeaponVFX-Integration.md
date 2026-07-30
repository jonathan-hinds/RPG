# Empower Weapon VFX Integration

Empower Weapon uses the standard `MMOAbilityVfxDefinition` release path. The activation prefab is spawned for both local and replicated ability releases, while the persistent presentation listens to the receiver-local replicated `shaman_empower_weapon` buff.

## Runtime ownership

- `EmpowerWeaponVFX` owns presentation lifecycle only. It does not apply damage, spend Mana, or maintain a second buff timer.
- `EmpowerWeaponPersistentVFX` attaches to the active `MainHand` equipment visual marker and rebuilds its overlay when equipment presentation changes between ready, combat movement, and stowed states.
- `MMOPlayerEquipmentVisuals.VisualsRebuilt` is a presentation event used to transfer the enchantment safely after weapon swaps or attachment-state rebuilds.
- Surface energy duplicates the actual equipped weapon mesh as a tightly lifted overlay. Generated grayscale alpha masks drive mesh-conforming nature veins, directional mint cores, golden runic bands, elemental breakup, and a travelling handle-to-tip pulse. Renderer bounds choose the flow axis, shared materials plus `MaterialPropertyBlock` values supply per-weapon mapping, and the base weapon materials are never replaced.
- Persistent billboards are deliberately restrained to small glints, motes, and intermittent arcs; the weapon-surface material is the primary read.
- Attack trails are enabled only by the existing replicated melee ability-release event.
- Compact impacts are triggered by the existing confirmed `MMOCombatant.DamageDealt` presentation event while the authoritative Empower Weapon buff is active.

## Lifecycle

The effect observes the actual buff after activation and stays alive while that buff exists. Buff expiration, dispel, cancellation, or death starts the profile-controlled fade. Removing the weapon removes the weapon child effect without ending the buff; equipping a replacement reattaches it and plays the small transfer flash.

## Mana cost

Both gameplay and tooltips call `MMOAbilityDefinition.CalculateManaCost`. Empower Weapon is configured with `MaximumManaPercentage` at `0.2`, and the shared method uses `Mathf.CeilToInt`. The primary tooltip already refreshes live; the legacy ability tooltip now uses the same calculation and refreshes its displayed value while open.

## Authoring

Use `Tools/RPG Clone/VFX/Build Empower Weapon VFX` to regenerate runtime textures, materials, meshes, prefabs, profile, and ability wiring from the checked-in source atlases. Use `Tools/RPG Clone/VFX/Validate Empower Weapon VFX` for structural and multiplayer-path validation.
