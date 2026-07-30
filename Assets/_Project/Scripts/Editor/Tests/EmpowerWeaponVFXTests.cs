#if UNITY_EDITOR
using NUnit.Framework;
using RPGClone.Abilities;
using RPGClone.Characters;
using RPGClone.Inventory;
using RPGClone.Player;
using RPGClone.Vfx;
using RPGClone.Vfx.Shaman;
using UnityEditor;
using UnityEngine;

namespace RPGClone.EditorTests
{
    public sealed class EmpowerWeaponVFXTests
    {
        private const string AbilityPath = "Assets/_Project/Configs/Abilities/Shaman_Empower_Weapon.asset";
        private const string DefinitionPath = "Assets/_Project/VFX/Definitions/Shaman_Empower_Weapon_VFX.asset";
        private const string ActivationPath = "Assets/_Project/VFX/EmpowerWeapon/Prefabs/EmpowerWeaponActivationVFX.prefab";
        private const string PersistentPath = "Assets/_Project/VFX/EmpowerWeapon/Prefabs/EmpowerWeaponPersistentVFX.prefab";
        private const string ImpactPath = "Assets/_Project/VFX/EmpowerWeapon/Prefabs/EmpowerWeaponImpactVFX.prefab";
        private const string ProfilePath = "Assets/_Project/VFX/EmpowerWeapon/Profiles/EmpowerWeaponVFX_Default.asset";
        private const string SurfaceMaterialPath = "Assets/_Project/VFX/EmpowerWeapon/Materials/EmpowerWeapon_SurfaceOverlay.mat";

        [Test]
        public void EmpowerWeapon_UsesSharedDynamicManaCostAndReplicatedReleaseDefinition()
        {
            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AbilityPath);
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(DefinitionPath);
            GameObject activation = AssetDatabase.LoadAssetAtPath<GameObject>(ActivationPath);
            Assert.That(ability, Is.Not.Null);
            Assert.That(definition, Is.Not.Null);
            Assert.That(activation, Is.Not.Null);
            Assert.That(ability.ManaCostSource, Is.EqualTo(MMOAbilityManaCostSource.MaximumManaPercentage));
            Assert.That(ability.MaximumManaCostPercent, Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(ability.VisualEffects, Is.SameAs(definition));
            Assert.That(definition.CastPrefab, Is.SameAs(activation));
            Assert.That(activation.GetComponent<EmpowerWeaponVFX>(), Is.Not.Null);

            GameObject casterObject = new("Empower Weapon Mana Test");
            try
            {
                MMOCharacterIdentity caster = casterObject.AddComponent<MMOCharacterIdentity>();
                caster.Configure("Tester", 8, null, Color.white, MMOEntityFaction.Player, true, new MMOCharacterStats(), 500, 500);
                Assert.That(ability.CalculateManaCost(caster), Is.EqualTo(100));
            }
            finally
            {
                Object.DestroyImmediate(casterObject);
            }
        }

        [Test]
        public void PersistentPrefab_IsLayeredBoundedAndAttackTrailsStartDisabled()
        {
            GameObject persistent = AssetDatabase.LoadAssetAtPath<GameObject>(PersistentPath);
            Assert.That(persistent, Is.Not.Null);
            Assert.That(persistent.GetComponent<EmpowerWeaponPersistentVFX>(), Is.Not.Null);
            Assert.That(persistent.transform.Find("Surface Overlay"), Is.Not.Null);
            Assert.That(persistent.transform.Find("Aura"), Is.Not.Null);
            Assert.That(persistent.transform.Find("Attack Trail Integration"), Is.Not.Null);
            Assert.That(persistent.GetComponentsInChildren<ParticleSystem>(true), Has.Length.EqualTo(3));
            Assert.That(persistent.GetComponentsInChildren<TrailRenderer>(true), Has.Length.EqualTo(2));
            foreach (TrailRenderer trail in persistent.GetComponentsInChildren<TrailRenderer>(true))
            {
                Assert.That(trail.emitting, Is.False);
            }

            foreach (ParticleSystem particles in persistent.GetComponentsInChildren<ParticleSystem>(true))
            {
                Assert.That(particles.main.maxParticles, Is.LessThanOrEqualTo(12));
                Assert.That(particles.collision.enabled, Is.False);
            }
        }

        [Test]
        public void ActivationAndImpact_ArePresentationOnlyProceduralPrefabs()
        {
            GameObject activation = AssetDatabase.LoadAssetAtPath<GameObject>(ActivationPath);
            GameObject impact = AssetDatabase.LoadAssetAtPath<GameObject>(ImpactPath);
            Assert.That(activation, Is.Not.Null);
            Assert.That(impact, Is.Not.Null);
            Assert.That(activation.GetComponentInChildren<Animator>(true), Is.Null);
            Assert.That(activation.GetComponentInChildren<Collider>(true), Is.Null);
            Assert.That(impact.GetComponentInChildren<Collider>(true), Is.Null);
            Assert.That(impact.GetComponent<EmpowerWeaponOneShotVFX>(), Is.Not.Null);
            Assert.That(impact.GetComponent<MMOAbilityVfxPoolable>(), Is.Not.Null);
        }

        [Test]
        public void SurfaceOverlay_UsesGeneratedMeshConformingMaskLayers()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(SurfaceMaterialPath);
            Assert.That(material, Is.Not.Null);
            Assert.That(material.shader.name, Is.EqualTo("RPG Clone/VFX/Empower Weapon Surface Overlay"));
            Assert.That(material.GetTexture("_VeinMask"), Is.Not.Null);
            Assert.That(material.GetTexture("_FlowMask"), Is.Not.Null);
            Assert.That(material.GetTexture("_RuneMask"), Is.Not.Null);
            Assert.That(material.GetTexture("_BreakupMask"), Is.Not.Null);
            Assert.That(material.GetFloat("_SurfaceExtrusion"), Is.GreaterThan(0f));
            Assert.That(material.GetFloat("_RuneIntensity"), Is.GreaterThan(0f));
            foreach (var message in ShaderUtil.GetShaderMessages(material.shader))
            {
                Assert.That(
                    message.severity.ToString(),
                    Is.Not.EqualTo("Error"),
                    message.message);
            }
        }

        [Test]
        public void EquipmentVisualMarker_CarriesMainHandAndPresentationMetadata()
        {
            MMOEquipmentVisualDefinition visual = ScriptableObject.CreateInstance<MMOEquipmentVisualDefinition>();
            GameObject markerObject = new("Weapon Marker");
            try
            {
                visual.ConfigureAttachment(
                    MMOEquipmentSlotType.MainHand,
                    "cc_weapon_r",
                    "cc_weapon_stowed",
                    "cc_weapon_r",
                    null,
                    null,
                    null,
                    Vector3.zero,
                    Vector3.zero,
                    Vector3.one);
                MMOEquipmentVisualInstanceMarker marker = markerObject.AddComponent<MMOEquipmentVisualInstanceMarker>();
                marker.Configure(visual, MMOEquipmentAttachmentPresentationState.Stowed);
                Assert.That(marker.EquipmentSlot, Is.EqualTo(MMOEquipmentSlotType.MainHand));
                Assert.That(marker.PresentationState, Is.EqualTo(MMOEquipmentAttachmentPresentationState.Stowed));
                Assert.That(marker.VisualDefinition, Is.SameAs(visual));
            }
            finally
            {
                Object.DestroyImmediate(markerObject);
                Object.DestroyImmediate(visual);
            }
        }

        [Test]
        public void DefaultProfile_ExposesProductionSafePersistentBudget()
        {
            EmpowerWeaponVFXProfile profile = AssetDatabase.LoadAssetAtPath<EmpowerWeaponVFXProfile>(ProfilePath);
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.ParticleAmount, Is.InRange(1, 24));
            Assert.That(profile.FadeOutDuration, Is.InRange(0.2f, 0.4f));
            Assert.That(profile.SheathedIntensity, Is.InRange(0f, 1f));
            Assert.That(profile.ActivationDuration, Is.InRange(0.8f, 1.3f));
        }
    }
}
#endif
