using NUnit.Framework;
using RPGClone.Abilities;
using RPGClone.Vfx;
using RPGClone.Vfx.Shaman;
using UnityEditor;
using UnityEngine;

namespace RPGClone.EditorTests
{
    public sealed class EarthquakeVFXTests
    {
        private const string ProfilePath = "Assets/_Project/VFX/Earthquake/Profiles/EarthquakeVFX_Default.asset";
        private const string PrefabPath = "Assets/_Project/VFX/Earthquake/Prefabs/EarthquakeVFX.prefab";
        private const string DefinitionPath = "Assets/_Project/VFX/Definitions/Shaman_Earthquake_VFX.asset";
        private const string AbilityPath = "Assets/_Project/Configs/Abilities/Shaman_Earthquake.asset";

        [Test]
        public void Package_IsWiredThroughReplicatedAbilityReleasePath()
        {
            EarthquakeVFXProfile profile = AssetDatabase.LoadAssetAtPath<EarthquakeVFXProfile>(ProfilePath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(DefinitionPath);
            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AbilityPath);
            Assert.That(profile, Is.Not.Null);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(definition, Is.Not.Null);
            Assert.That(ability, Is.Not.Null);
            Assert.That(profile.Radius, Is.EqualTo(ability.AreaRadius).Within(0.01f));
            Assert.That(ability.VisualEffects, Is.SameAs(definition));
            Assert.That(definition.CastPrefab, Is.SameAs(prefab));
            Assert.That(definition.HitPrefab, Is.Null);
            Assert.That(definition.CastPrefabControlsHitTiming, Is.True);
            Assert.That(prefab.GetComponent<EarthquakeVFX>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<MMOAbilityVfxPoolable>(), Is.Not.Null);
        }

        [Test]
        public void Package_UsesBoundedWorldSpaceEnvironmentalLayersAndPooledReactions()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<EarthquakeTargetReactionVFX>(true), Has.Length.EqualTo(12));
            Assert.That(prefab.GetComponentsInChildren<MeshRenderer>(true).Length, Is.GreaterThanOrEqualTo(30));
            foreach (ParticleSystem particle in prefab.GetComponentsInChildren<ParticleSystem>(true))
            {
                Assert.That(particle.main.maxParticles, Is.LessThanOrEqualTo(384), particle.name);
                Assert.That(particle.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World), particle.name);
                Assert.That(particle.collision.enabled, Is.False, particle.name);
            }
            Assert.That(prefab.GetComponentInChildren<Collider>(true), Is.Null);
            Assert.That(prefab.GetComponentInChildren<Light>(true), Is.Null);
            Assert.That(prefab.GetComponentInChildren<Animator>(true), Is.Null);
        }

        [Test]
        public void GeneratedRuntimeTexturesAndReusableMeshes_AreImported()
        {
            string root = "Assets/_Project/VFX/Earthquake";
            string[] textures =
            {
                "Earthquake_GroundFractureAtlas.png", "Earthquake_DustHazeAtlas.png",
                "Earthquake_DebrisImpactAtlas.png", "Earthquake_TerrainSurfaceAtlas.png"
            };
            foreach (string texture in textures)
                Assert.That(AssetDatabase.LoadAssetAtPath<Texture2D>($"{root}/Textures/{texture}"), Is.Not.Null, texture);
            string[] meshes =
            {
                "GroundCubeSmall", "GroundCubeMedium", "GroundBlockBroad", "GroundSlabTilted",
                "AngularRock", "Pebble", "FlatRockChip", "PressureRingStrip", "GroundQuad"
            };
            foreach (string mesh in meshes)
                Assert.That(AssetDatabase.LoadAssetAtPath<Mesh>($"{root}/Meshes/{mesh}.asset"), Is.Not.Null, mesh);
        }

        [Test]
        public void Package_ReusesChargeEarthMaterialsWithoutCollision()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            string root = "World Space Environmental Layers/Charge Earth Library Layers/";
            string[] layers =
            {
                "Charge Heavy Dust Surge", "Charge Fine Dust Clouds", "Charge Ground Scrape Dust", "Charge Dirt Chunks",
                "Charge Scrape Debris", "Charge Rocks", "Charge Ground Burst", "Charge Ground Shockwave"
            };
            string[] materials =
            {
                "Charge_HeavyDust", "Charge_FineDust", "Charge_HeavyDust", "Charge_DirtDebris",
                "Charge_DirtDebris", "Charge_Rocks", "Charge_GroundBursts", "Charge_Shockwaves"
            };
            for (int i = 0; i < layers.Length; i++)
            {
                Transform layer = prefab.transform.Find(root + layers[i]);
                Assert.That(layer, Is.Not.Null, layers[i]);
                ParticleSystem system = layer.GetComponent<ParticleSystem>();
                Assert.That(system, Is.Not.Null, layers[i]);
                Assert.That(system.collision.enabled, Is.False, layers[i]);
                Material expected = AssetDatabase.LoadAssetAtPath<Material>($"Assets/_Project/VFX/Charge/Materials/{materials[i]}.mat");
                Assert.That(layer.GetComponent<ParticleSystemRenderer>().sharedMaterial, Is.SameAs(expected), layers[i]);
            }
        }

        [Test]
        public void GroundSampler_IgnoresCasterColliderAndFollowsSlopeNormal()
        {
            GameObject caster = new("Sampler Test Caster");
            GameObject slope = new("Sampler Test Slope");
            try
            {
                BoxCollider casterCollider = caster.AddComponent<BoxCollider>();
                casterCollider.center = new Vector3(0f, 1f, 0f);
                casterCollider.size = new Vector3(1f, 2f, 1f);

                slope.transform.position = new Vector3(0f, -0.18f, 0f);
                slope.transform.rotation = Quaternion.Euler(0f, 0f, 24f);
                BoxCollider slopeCollider = slope.AddComponent<BoxCollider>();
                slopeCollider.size = new Vector3(12f, 0.2f, 12f);
                Physics.SyncTransforms();

                EarthquakeTerrainSample sample = EarthquakeTerrainSampler.Sample(Vector3.zero, caster.transform);
                Assert.That(sample.Position.y, Is.LessThan(0.5f), "Ground probe anchored to the character instead of the ground.");
                Assert.That(Vector3.Angle(sample.Normal, slope.transform.up), Is.LessThan(1f));
            }
            finally
            {
                Object.DestroyImmediate(caster);
                Object.DestroyImmediate(slope);
            }
        }
    }
}
