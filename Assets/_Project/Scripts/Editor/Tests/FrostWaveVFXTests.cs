using NUnit.Framework;
using RPGClone.Abilities;
using RPGClone.Vfx;
using RPGClone.Vfx.Mage;
using UnityEditor;
using UnityEngine;

namespace RPGClone.EditorTests
{
    public sealed class FrostWaveVFXTests
    {
        private const string Root = "Assets/_Project/VFX/FrostWave";

        [Test]
        public void FrostWave_UsesGameplayRadiusAndReplicatedPresentationPrefab()
        {
            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(
                "Assets/_Project/Configs/Abilities/Mage_Frost_Wave.asset");
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(
                "Assets/_Project/VFX/Definitions/Mage_Frost_Wave_VFX.asset");
            FrostWaveVFXProfile profile = AssetDatabase.LoadAssetAtPath<FrostWaveVFXProfile>(
                Root + "/Profiles/FrostWaveVFX_Default.asset");

            Assert.That(ability, Is.Not.Null);
            Assert.That(definition, Is.Not.Null);
            Assert.That(profile, Is.Not.Null);
            Assert.That(ability.VisualEffects, Is.SameAs(definition));
            Assert.That(definition.CastPrefab, Is.Not.Null);
            Assert.That(definition.HitPrefab, Is.Null);
            Assert.That(definition.CastPrefabControlsHitTiming, Is.True);
            Assert.That(definition.AttachCastingToCaster, Is.False);
            Assert.That(profile.ResolveRadius(ability), Is.EqualTo(ability.AreaRadius).Within(0.001f));
            Assert.That(ability.AreaRadius, Is.EqualTo(8f).Within(0.001f));
        }

        [Test]
        public void FrostWave_PrefabsContainAllReusableLayers()
        {
            GameObject caster = LoadPrefab("FrostWaveCasterVFX");
            GameObject ring = LoadPrefab("FrostWaveExpandingRingVFX");
            GameObject ground = LoadPrefab("FrostWaveGroundFrostVFX");
            GameObject impact = LoadPrefab("FrostWaveEnemyImpactVFX");
            GameObject root = LoadPrefab("FrostWaveRootIndicatorVFX");

            Assert.That(caster.GetComponent<FrostWaveVFX>(), Is.Not.Null);
            Assert.That(caster.GetComponent<FrostWaveRadialFrontVFX>(), Is.Not.Null);
            Assert.That(ring.GetComponent<FrostWaveRingVFX>(), Is.Not.Null);
            Assert.That(ground.GetComponent<FrostWaveGroundFrostVFX>(), Is.Not.Null);
            Assert.That(impact.GetComponent<FrostWaveEnemyImpactVFX>(), Is.Not.Null);
            Assert.That(root.transform.Find("Persistent Root Indicator"), Is.Not.Null);
            Assert.That(caster.GetComponentsInChildren<FrostWaveEnemyImpactVFX>(true).Length, Is.Zero);
            Assert.That(caster.GetComponentsInChildren<ParticleSystem>(true).Length, Is.GreaterThanOrEqualTo(6));
            Transform cloudFront = caster.transform.Find("Upright Traveling Frost Cloud Front");
            Transform iceBreakers = caster.transform.Find("Hero Ice Breakers Riding Wave");
            Assert.That(cloudFront, Is.Not.Null, "The cloud must be an upright moving front, not a ground-projected mist decal.");
            Assert.That(iceBreakers, Is.Not.Null, "The freeze needs a readable hero-ice silhouette at the wave front.");
            Assert.That(cloudFront.GetComponent<ParticleSystemRenderer>().renderMode, Is.EqualTo(ParticleSystemRenderMode.VerticalBillboard));
            Assert.That(iceBreakers.GetComponent<ParticleSystemRenderer>().renderMode, Is.EqualTo(ParticleSystemRenderMode.VerticalBillboard));
            Assert.That(cloudFront.GetComponent<ParticleSystem>().textureSheetAnimation.numTilesX, Is.EqualTo(4));
            Assert.That(iceBreakers.GetComponent<ParticleSystem>().textureSheetAnimation.numTilesY, Is.EqualTo(4));
            Assert.That(
                ring.transform.Find("Low Ground Hugging Mist Ring"),
                Is.Null,
                "The obsolete painted-on mist disc must not return.");
            Assert.That(impact.GetComponentsInChildren<MeshRenderer>(true).Length, Is.GreaterThanOrEqualTo(12));
            Assert.That(impact.GetComponent<MMOAbilityVfxPoolable>(), Is.Not.Null);
        }

        [Test]
        public void FrostWave_FocusedShadersAndGeneratedTexturesAreImported()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<Shader>(Root + "/Shaders/FrostWaveLayered.shader"), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<Shader>(Root + "/Shaders/FrostWaveGround.shader"), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<Shader>(Root + "/Shaders/FrostWaveIce.shader"), Is.Not.Null);
            AssertTexture("FrostWave_RingsGroundRuneAtlas.png");
            AssertTexture("FrostWave_MistVaporNoiseAtlas.png");
            AssertTexture("FrostWave_ParticlesGlowStreakAtlas.png");
            AssertTexture("FrostWave_IceShardAtlas.png");
            AssertTexture("FrostWave_RadialCloudAtlas.png");
            AssertTexture("FrostWave_HeroIceAtlas.png");
            AssertTexture("FrostWave_DistortionNoise.png");
            AssertTexture("FrostWave_ErosionNoise.png");
        }

        private static GameObject LoadPrefab(string name)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Prefabs/{name}.prefab");
            Assert.That(prefab, Is.Not.Null, $"Missing prefab {name}");
            return prefab;
        }

        private static void AssertTexture(string name)
        {
            Assert.That(
                AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/Textures/{name}"),
                Is.Not.Null,
                $"Missing texture {name}");
        }
    }
}
