using NUnit.Framework;
using RPGClone.Abilities;
using RPGClone.Vfx;
using RPGClone.Vfx.Shaman;
using UnityEditor;
using UnityEngine;

namespace RPGClone.Tests
{
    public sealed class LightningVFXTests
    {
        private const string Root = "Assets/_Project/VFX/Lightning";
        private const string DefinitionPath = "Assets/_Project/VFX/Definitions/Shaman_Lightning_Bolt_VFX.asset";

        [Test]
        public void Package_ContainsProfileAndAllRuntimePhases()
        {
            LightningVFXProfile profile = AssetDatabase.LoadAssetAtPath<LightningVFXProfile>(Root + "/Profiles/LightningVFX_Default.asset");
            GameObject cast = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/LightningCastVFX.prefab");
            GameObject beam = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/LightningBeamVFX.prefab");
            GameObject impact = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/LightningImpactVFX.prefab");
            GameObject aftermath = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/LightningAftermathVFX.prefab");
            GameObject complete = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/LightningVFX.prefab");

            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.BeamDuration, Is.InRange(0.12f, 0.25f));
            Assert.That(profile.AftermathDuration, Is.InRange(0.3f, 0.5f));
            Assert.That(cast.GetComponent<LightningCastVFX>(), Is.Not.Null);
            LightningChargeWindMeshVFX windMesh = cast.GetComponentInChildren<LightningChargeWindMeshVFX>(true);
            Assert.That(windMesh, Is.Not.Null);
            Assert.That(windMesh.RingCount, Is.EqualTo(3));
            Assert.That(windMesh.GetComponentsInChildren<MeshFilter>(true), Has.Length.EqualTo(3));
            foreach (MeshFilter filter in windMesh.GetComponentsInChildren<MeshFilter>(true))
            {
                Assert.That(filter.sharedMesh, Is.Not.Null, filter.name);
                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                Assert.That(renderer, Is.Not.Null, filter.name);
                Assert.That(renderer.sharedMaterial, Is.Not.Null, filter.name);
                Assert.That(AssetDatabase.GetAssetPath(renderer.sharedMaterial.mainTexture), Does.EndWith("Lightning_ChargeWindRibbon.png"), filter.name);
            }
            foreach (ParticleSystem particle in cast.GetComponentsInChildren<ParticleSystem>(true))
            {
                Assert.That(particle.name, Is.Not.EqualTo("Pressure And Air Distortion"), "The clipped billboard pressure layer must not return.");
            }
            Assert.That(beam.GetComponent<LightningBeamVFX>(), Is.Not.Null);
            Assert.That(beam.GetComponent<LightningAftermathVFX>(), Is.Not.Null);
            Assert.That(impact.GetComponent<LightningImpactVFX>(), Is.Not.Null);
            Assert.That(aftermath.GetComponent<LightningAftermathVFX>(), Is.Not.Null);
            Assert.That(complete.GetComponent<LightningVFX>(), Is.Not.Null);
        }

        [Test]
        public void SharedDefinition_WiresShamanAndTrogToSameSpecializedPackage()
        {
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(DefinitionPath);
            MMOAbilityDefinition shaman = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>("Assets/_Project/Configs/Abilities/Shaman_Lightning_Bolt.asset");
            MMOAbilityDefinition trog = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>("Assets/_Project/Configs/Abilities/Trog_Lightning_Bolt.asset");

            Assert.That(definition, Is.Not.Null);
            Assert.That(shaman.VisualEffects, Is.SameAs(definition));
            Assert.That(trog.VisualEffects, Is.SameAs(definition));
            Assert.That(definition.CastingPrefab.GetComponent<LightningCastVFX>(), Is.Not.Null);
            Assert.That(definition.CastPrefab.GetComponent<LightningBeamVFX>(), Is.Not.Null);
            Assert.That(definition.HitPrefab.GetComponent<LightningImpactVFX>(), Is.Not.Null);
            Assert.That(definition.CastPrefabControlsHitTiming, Is.True);
            Assert.That(definition.UseHandCastingAnchors, Is.False, "The integrated cast prefab must spawn once and resolve both hands internally.");
        }

        [Test]
        public void EnvironmentalSystems_StayWorldSpaceAndReuseChargeBashArt()
        {
            GameObject cast = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/LightningCastVFX.prefab");
            int reusedSystems = 0;
            foreach (ParticleSystem particle in cast.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (!particle.name.Contains("Dust") && !particle.name.Contains("Dirt") && !particle.name.Contains("Ground"))
                {
                    continue;
                }

                Assert.That(particle.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World), particle.name);
                Material material = particle.GetComponent<ParticleSystemRenderer>().sharedMaterial;
                string texturePath = material != null && material.mainTexture != null ? AssetDatabase.GetAssetPath(material.mainTexture) : string.Empty;
                if (texturePath.Contains("/VFX/Charge/") || texturePath.Contains("/VFX/Bash/"))
                {
                    reusedSystems++;
                }
            }

            Assert.That(reusedSystems, Is.GreaterThanOrEqualTo(5));
        }
    }
}
