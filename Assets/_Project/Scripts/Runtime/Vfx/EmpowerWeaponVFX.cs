using RPGClone.Abilities;
using RPGClone.Buffs;
using RPGClone.Combat;
using RPGClone.Inventory;
using RPGClone.Player;
using UnityEngine;

namespace RPGClone.Vfx.Shaman
{
    /// <summary>
    /// Presentation-only bridge from replicated ability/buff events to the equipped weapon visuals.
    /// Gameplay timing, hit resolution, and damage remain owned by their existing authoritative systems.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EmpowerWeaponVFX : MonoBehaviour, IMMOAbilityVfxInstance, IMMOAbilityVfxPoolReset
    {
        public const string BuffId = "shaman_empower_weapon";

        [SerializeField] private EmpowerWeaponVFXProfile profile;
        [SerializeField] private GameObject persistentPrefab;
        [SerializeField] private GameObject impactPrefab;
        [SerializeField] private GameObject transferPrefab;
        [SerializeField] private ParticleSystem[] activationParticles = System.Array.Empty<ParticleSystem>();
        [SerializeField] private Light activationLight;

        private MMOAbilityVfxContext context;
        private MMOCharacterBuffController buffs;
        private MMOCombatant combatant;
        private MMOAbilitySystem abilitySystem;
        private MMOPlayerEquipmentVisuals equipmentVisuals;
        private EmpowerWeaponPersistentVFX persistent;
        private Transform caster;
        private float startedAt;
        private float activationLightBaseIntensity;
        private bool playing;
        private bool ending;
        private bool buffWasObserved;
        private bool pendingVisualRefresh;
        private bool infusionPlayed;

        private void LateUpdate()
        {
            if (!playing || profile == null || caster == null)
            {
                return;
            }

            TryResolveRuntimeReferences();
            if (pendingVisualRefresh)
            {
                pendingVisualRefresh = false;
                TryAttachPersistent(true);
            }

            if (!infusionPlayed && persistent != null && Time.time - startedAt >= 0.25f)
            {
                infusionPlayed = true;
                SpawnTransfer(persistent.WeaponMarker, 0.42f);
            }

            UpdateActivationLight();
            CheckBuffState();
            if (ending && (persistent == null || persistent.FadeComplete))
            {
                Release();
            }
        }

        public void Initialize(MMOAbilityVfxContext newContext)
        {
            context = newContext;
            caster = newContext.Source;
            if (caster == null || profile == null)
            {
                MMOAbilityVfxPool.Release(gameObject);
                return;
            }

            foreach (EmpowerWeaponVFX existing in caster.GetComponentsInChildren<EmpowerWeaponVFX>(true))
            {
                if (existing != null && existing != this && existing.playing)
                {
                    existing.BeginEnding(true);
                }
            }

            ResolveReferences();
            startedAt = Time.time;
            playing = true;
            ending = false;
            infusionPlayed = false;
            buffWasObserved = HasBuff();
            activationLightBaseIntensity = activationLight != null ? activationLight.intensity : 0f;
            transform.localScale = Vector3.one * profile.ActivationScale;
            Subscribe();
            PlayActivation();
            TryAttachPersistent(false);
        }

        public void ConfigureAuthoring(
            EmpowerWeaponVFXProfile newProfile,
            GameObject newPersistentPrefab,
            GameObject newImpactPrefab,
            GameObject newTransferPrefab,
            ParticleSystem[] newActivationParticles,
            Light newActivationLight)
        {
            profile = newProfile;
            persistentPrefab = newPersistentPrefab;
            impactPrefab = newImpactPrefab;
            transferPrefab = newTransferPrefab;
            activationParticles = newActivationParticles ?? System.Array.Empty<ParticleSystem>();
            activationLight = newActivationLight;
        }

        public void ResetForPool()
        {
            Unsubscribe();
            playing = false;
            ending = false;
            pendingVisualRefresh = false;
            infusionPlayed = false;
            if (persistent != null)
            {
                Destroy(persistent.gameObject);
                persistent = null;
            }

            foreach (ParticleSystem particles in activationParticles)
            {
                particles?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            if (activationLight != null)
            {
                activationLight.enabled = false;
            }

            context = default;
            caster = null;
            buffs = null;
            combatant = null;
            abilitySystem = null;
            equipmentVisuals = null;
        }

        private void ResolveReferences()
        {
            if (caster == null) return;
            buffs = caster.GetComponent<MMOCharacterBuffController>();
            combatant = caster.GetComponent<MMOCombatant>();
            abilitySystem = caster.GetComponent<MMOAbilitySystem>();
            equipmentVisuals = caster.GetComponent<MMOPlayerEquipmentVisuals>();
        }

        private void TryResolveRuntimeReferences()
        {
            if (caster == null) return;
            if (buffs == null)
            {
                buffs = caster.GetComponent<MMOCharacterBuffController>();
                if (buffs != null)
                {
                    buffs.BuffsChanged -= OnBuffsChanged;
                    buffs.BuffsChanged += OnBuffsChanged;
                }
            }

            if (equipmentVisuals == null)
            {
                equipmentVisuals = caster.GetComponent<MMOPlayerEquipmentVisuals>();
                if (equipmentVisuals != null)
                {
                    equipmentVisuals.VisualsRebuilt -= OnEquipmentVisualsRebuilt;
                    equipmentVisuals.VisualsRebuilt += OnEquipmentVisualsRebuilt;
                    pendingVisualRefresh = true;
                }
            }
        }

        private void Subscribe()
        {
            if (buffs != null)
            {
                buffs.BuffsChanged -= OnBuffsChanged;
                buffs.BuffsChanged += OnBuffsChanged;
            }

            if (combatant != null)
            {
                combatant.DamageDealt -= OnDamageDealt;
                combatant.DamageDealt += OnDamageDealt;
                combatant.Died -= OnCasterDied;
                combatant.Died += OnCasterDied;
            }

            if (abilitySystem != null)
            {
                abilitySystem.AbilityReleased -= OnAbilityReleased;
                abilitySystem.AbilityReleased += OnAbilityReleased;
            }

            if (equipmentVisuals != null)
            {
                equipmentVisuals.VisualsRebuilt -= OnEquipmentVisualsRebuilt;
                equipmentVisuals.VisualsRebuilt += OnEquipmentVisualsRebuilt;
            }
        }

        private void Unsubscribe()
        {
            if (buffs != null) buffs.BuffsChanged -= OnBuffsChanged;
            if (combatant != null)
            {
                combatant.DamageDealt -= OnDamageDealt;
                combatant.Died -= OnCasterDied;
            }

            if (abilitySystem != null) abilitySystem.AbilityReleased -= OnAbilityReleased;
            if (equipmentVisuals != null) equipmentVisuals.VisualsRebuilt -= OnEquipmentVisualsRebuilt;
        }

        private void PlayActivation()
        {
            foreach (ParticleSystem particles in activationParticles)
            {
                if (particles == null) continue;
                particles.Clear(true);
                particles.Play(true);
            }

            if (activationLight != null)
            {
                activationLight.enabled = true;
                activationLight.intensity = activationLightBaseIntensity;
            }
        }

        private void UpdateActivationLight()
        {
            if (activationLight == null || !activationLight.enabled)
            {
                return;
            }

            float elapsed = Time.time - startedAt;
            float normalized = elapsed / Mathf.Max(0.01f, profile.ActivationDuration * 0.42f);
            activationLight.intensity = activationLightBaseIntensity * (1f - Smooth01(normalized));
            if (normalized >= 1f)
            {
                activationLight.enabled = false;
            }
        }

        private void CheckBuffState()
        {
            bool hasBuff = HasBuff();
            if (hasBuff)
            {
                buffWasObserved = true;
                return;
            }

            if ((buffWasObserved && Time.time - startedAt > 0.12f)
                || (!buffWasObserved && Time.time - startedAt > 1.5f))
            {
                BeginEnding(false);
            }
        }

        private bool HasBuff()
        {
            return buffs != null && buffs.FindBuff(BuffId) != null;
        }

        private void TryAttachPersistent(bool playTransfer)
        {
            if (!playing || ending || persistentPrefab == null || caster == null)
            {
                return;
            }

            MMOEquipmentVisualInstanceMarker marker = FindMainHandMarker();
            if (persistent != null && persistent.WeaponMarker == marker)
            {
                return;
            }

            if (persistent != null)
            {
                Destroy(persistent.gameObject);
                persistent = null;
            }

            if (marker == null)
            {
                return;
            }

            GameObject instance = Instantiate(persistentPrefab, marker.transform);
            instance.name = persistentPrefab.name;
            persistent = instance.GetComponent<EmpowerWeaponPersistentVFX>();
            if (persistent == null)
            {
                Destroy(instance);
                return;
            }

            persistent.Attach(marker);
            if (playTransfer && transferPrefab != null)
            {
                SpawnTransfer(marker, 0.35f);
            }
        }

        private void SpawnTransfer(MMOEquipmentVisualInstanceMarker marker, float duration)
        {
            if (marker == null || transferPrefab == null)
            {
                return;
            }

            GameObject transfer = MMOAbilityVfxPool.Spawn(
                transferPrefab,
                marker.transform.position,
                marker.transform.rotation,
                marker.transform);
            transfer?.GetComponent<EmpowerWeaponOneShotVFX>()?.Play(duration);
        }

        private MMOEquipmentVisualInstanceMarker FindMainHandMarker()
        {
            MMOEquipmentVisualInstanceMarker activeMarker =
                equipmentVisuals != null
                    ? equipmentVisuals.FindActiveEquipmentVisual(MMOEquipmentSlotType.MainHand)
                    : null;
            if (activeMarker != null && activeMarker.GetComponentInChildren<Renderer>(true) != null)
            {
                return activeMarker;
            }

            foreach (MMOEquipmentVisualInstanceMarker marker in caster.GetComponentsInChildren<MMOEquipmentVisualInstanceMarker>(true))
            {
                if (marker != null
                    && marker.EquipmentSlot == MMOEquipmentSlotType.MainHand
                    && marker.GetComponentInChildren<Renderer>(true) != null)
                {
                    return marker;
                }
            }

            return null;
        }

        private void BeginEnding(bool immediate)
        {
            if (!playing || ending)
            {
                return;
            }

            ending = true;
            Unsubscribe();
            foreach (ParticleSystem particles in activationParticles)
            {
                particles?.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            if (immediate || persistent == null)
            {
                Release();
                return;
            }

            persistent.FadeOut();
        }

        private void Release()
        {
            if (!playing)
            {
                return;
            }

            playing = false;
            Unsubscribe();
            if (persistent != null)
            {
                Destroy(persistent.gameObject);
                persistent = null;
            }

            MMOAbilityVfxPool.Release(gameObject);
        }

        private void OnBuffsChanged(MMOCharacterBuffController source)
        {
            if (source == buffs) CheckBuffState();
        }

        private void OnCasterDied(MMOCombatant source)
        {
            if (source == combatant) BeginEnding(false);
        }

        private void OnEquipmentVisualsRebuilt(MMOPlayerEquipmentVisuals source)
        {
            if (source == equipmentVisuals)
            {
                pendingVisualRefresh = true;
            }
        }

        private void OnAbilityReleased(
            MMOAbilitySystem source,
            MMOAbilityDefinition ability,
            RPGClone.Characters.MMOCharacterIdentity target,
            Vector3 targetPosition,
            bool hasGroundTarget)
        {
            if (source == abilitySystem && IsMeleeAbility(ability))
            {
                persistent?.TriggerAttackTrail();
            }
        }

        private void OnDamageDealt(
            MMOCombatant source,
            MMOCombatant target,
            MMOAbilityDefinition ability,
            int amount)
        {
            if (!playing || ending || source != combatant || target == null || amount <= 0
                || !HasBuff() || !IsMeleeAbility(ability) || impactPrefab == null)
            {
                return;
            }

            Vector3 position = target.transform.position + Vector3.up * 1.05f;
            Vector3 direction = caster != null ? target.transform.position - caster.position : Vector3.forward;
            Quaternion rotation = direction.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(direction.normalized, Vector3.up)
                : Quaternion.identity;
            GameObject impact = MMOAbilityVfxPool.Spawn(impactPrefab, position, rotation, null);
            if (impact != null)
            {
                impact.transform.localScale = Vector3.one * profile.MeleeImpactScale;
                impact.GetComponent<EmpowerWeaponOneShotVFX>()?.Play(profile.ImpactDuration);
            }
        }

        private static bool IsMeleeAbility(MMOAbilityDefinition ability)
        {
            if (ability == null)
            {
                return false;
            }

            if (ability.IsAutoAttack)
            {
                return true;
            }

            foreach (MMOAbilityEffectDefinition effect in ability.Effects)
            {
                if (effect != null
                    && effect.EffectType == MMOAbilityEffectType.Damage
                    && effect.DamageSchool == MMODamageSchool.Physical
                    && effect.AmountSource == MMOAbilityAmountSource.WeaponDamage)
                {
                    return true;
                }
            }

            return false;
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }
    }
}
