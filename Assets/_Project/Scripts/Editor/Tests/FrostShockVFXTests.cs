using System.Linq;
using NUnit.Framework;
using RPGClone.Abilities;
using RPGClone.Vfx;
using RPGClone.Vfx.Shaman;
using UnityEditor;
using UnityEngine;

namespace RPGClone.Tests
{
    public sealed class FrostShockVFXTests
    {
        private const string Root = "Assets/_Project/VFX/FrostShock";
        private const string DefinitionPath = "Assets/_Project/VFX/Definitions/Shaman_Frost_Shock_VFX.asset";
        private const string AbilityPath = "Assets/_Project/Configs/Abilities/Shaman_Frost_Shock.asset";

        [Test]
        public void Package_ContainsEveryPhaseAndUsesMeshVolumesAsPrimaryArt()
        {
            FrostShockVFXProfile profile = AssetDatabase.LoadAssetAtPath<FrostShockVFXProfile>(Root + "/Profiles/FrostShockVFX_Default.asset");
            GameObject cast = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/FrostShockCastVFX.prefab");
            GameObject projectile = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/FrostShockProjectileVFX.prefab");
            GameObject impact = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/FrostShockImpactVFX.prefab");
            GameObject slow = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/FrostShockSlowDebuffVFX.prefab");
            GameObject expiration = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/FrostShockExpirationVFX.prefab");
            GameObject complete = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/FrostShockVFX.prefab");

            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.ProjectileSpeed, Is.GreaterThanOrEqualTo(50f));
            Assert.That(profile.DebuffDuration, Is.EqualTo(6f).Within(0.01f));
            Assert.That(cast, Is.Not.Null);
            Assert.That(projectile.GetComponent<FrostShockProjectileVFX>(), Is.Not.Null);
            Assert.That(projectile.GetComponent<MMOAbilityVfxPoolable>(), Is.Not.Null);
            Assert.That(impact.GetComponent<FrostShockImpactVFX>(), Is.Not.Null);
            Assert.That(impact.GetComponentInChildren<FrostShockSlowDebuffVFX>(true), Is.Not.Null);
            Assert.That(slow.GetComponent<FrostShockSlowDebuffVFX>(), Is.Not.Null);
            Assert.That(expiration, Is.Not.Null);
            Assert.That(complete.GetComponent<FrostShockVFX>(), Is.Not.Null);
            Assert.That(projectile.GetComponentsInChildren<MeshRenderer>(true).Length, Is.GreaterThanOrEqualTo(6));
            Assert.That(slow.GetComponentsInChildren<MeshRenderer>(true).Length, Is.GreaterThanOrEqualTo(16));
            Assert.That(slow.GetComponentsInChildren<MeshFilter>(true).All(filter => filter.sharedMesh != null && filter.sharedMesh.name != "Quad"), Is.True);
        }

        [Test]
        public void AbilityDefinition_WiresReplicatedReleaseAndAuthoritativeBuffDuration()
        {
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(DefinitionPath);
            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AbilityPath);
            MMOAbilityEffectDefinition slow = ability.Effects.First(effect => effect.EffectType == MMOAbilityEffectType.TemporaryStatModifier);

            Assert.That(ability.VisualEffects, Is.SameAs(definition));
            Assert.That(definition.CastPrefab.GetComponent<FrostShockProjectileVFX>(), Is.Not.Null);
            Assert.That(definition.HitPrefab.GetComponent<FrostShockImpactVFX>(), Is.Not.Null);
            Assert.That(definition.CastPrefabControlsHitTiming, Is.True);
            Assert.That(definition.AttachHitToTarget, Is.True);
            Assert.That(slow.DurationSeconds, Is.EqualTo(6f).Within(0.01f));
            Assert.That(slow.MovementSpeedMultiplier, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(FrostShockSlowDebuffVFX.FrostShockBuffId, Is.EqualTo(ability.AbilityId));
        }

        [Test]
        public void DetachedTrailsAndMist_UseWorldSpaceSimulation()
        {
            GameObject projectile = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/FrostShockProjectileVFX.prefab");
            GameObject impact = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/FrostShockImpactVFX.prefab");
            foreach (ParticleSystem particles in projectile.GetComponentsInChildren<ParticleSystem>(true).Concat(impact.GetComponentsInChildren<ParticleSystem>(true)))
            {
                if (particles.name.Contains("World Space") || particles.name.Contains("Mist") || particles.name.Contains("Snow") || particles.name.Contains("Fragment"))
                {
                    Assert.That(particles.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World), particles.name);
                }
            }
        }

        [Test]
        public void GeneratedAtlases_AreImportedWithAlpha()
        {
            foreach (string name in new[] { "EnergyBurst", "IceShard", "CrackGroundPatch", "MistSnowTrail", "MeshSurface" })
            {
                string path = $"{Root}/Textures/FrostShock_{name}Atlas.png";
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.That(texture, Is.Not.Null, path);
                Assert.That(importer, Is.Not.Null, path);
                Assert.That(importer.alphaSource, Is.EqualTo(TextureImporterAlphaSource.FromInput), path);
                Assert.That(importer.alphaIsTransparency, Is.True, path);
            }
        }

    }
}
