#if UNITY_EDITOR
using System.IO;
using System.Reflection;
using System.Linq;
using NUnit.Framework;
using RPGClone.Abilities;
using RPGClone.Buffs;
using RPGClone.Characters;
using RPGClone.Vfx;
using UnityEditor;
using UnityEngine;

namespace RPGClone.EditorTests
{
    public sealed class PressTheAttackVFXTests
    {
        private const string AbilityPath = "Assets/_Project/Configs/Abilities/Warrior_Press_The_Attack.asset";
        private const string DefinitionPath = "Assets/_Project/VFX/PressTheAttack/PressTheAttack_VFX.asset";
        private const string CombinedPrefabPath = "Assets/_Project/VFX/PressTheAttack/Prefabs/PressTheAttackVFX.prefab";
        private const string ProfilePath = "Assets/_Project/VFX/PressTheAttack/Profiles/PressTheAttackVFX_Default.asset";

        [Test]
        public void Ability_UsesInstalledPressTheAttackDefinition()
        {
            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AbilityPath);
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(DefinitionPath);

            Assert.That(ability, Is.Not.Null);
            Assert.That(definition, Is.Not.Null);
            Assert.That(ability.AbilityId, Is.EqualTo("warrior_press_the_attack"));
            Assert.That(ability.VisualEffects, Is.SameAs(definition));
            Assert.That(definition.CastPrefab, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(definition.CastPrefab), Is.EqualTo(CombinedPrefabPath));
        }

        [Test]
        public void CombinedPrefab_IsPooledAndContainsLayeredPresentation()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CombinedPrefabPath);

            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<PressTheAttackVFX>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<MMOAbilityVfxPoolable>(), Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<ParticleSystem>(true).Length, Is.GreaterThanOrEqualTo(8));
            Assert.That(prefab.GetComponentsInChildren<Renderer>(true).Length, Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public void Profile_UsesPersistentBuffDrivenTimingDefaults()
        {
            PressTheAttackVFXProfile profile = AssetDatabase.LoadAssetAtPath<PressTheAttackVFXProfile>(ProfilePath);

            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.ActivationDuration, Is.InRange(0.75f, 1.2f));
            Assert.That(profile.FadeOutDuration, Is.InRange(0.15f, 0.4f));
            Assert.That(profile.AuthoritativeBuffHandshakeTimeout, Is.LessThan(3f));
            Assert.That(profile.SurfaceSparkAmount, Is.InRange(1, 16));
            Assert.That(profile.PersistentOverlayIntensity, Is.GreaterThanOrEqualTo(4f));
            Assert.That(profile.SurfacePatternScale, Is.GreaterThanOrEqualTo(1.5f));
            Assert.That(profile.RageUndercoatIntensity, Is.InRange(0.2f, 0.8f));
            Assert.That(profile.OptionalCameraImpulse, Is.False);
        }

        [Test]
        public void GeneratedTextureSet_ContainsStandaloneMasksAndAnimatedAtlases()
        {
            string[] allTextures = AssetDatabase.FindAssets(
                "t:Texture2D PressTheAttack_",
                new[] { "Assets/_Project/VFX/PressTheAttack/Textures" });
            string[] atlases = AssetDatabase.FindAssets(
                "t:Texture2D PressTheAttack_",
                new[] { "Assets/_Project/VFX/PressTheAttack/Textures/Atlases" });

            Assert.That(allTextures.Length, Is.GreaterThanOrEqualTo(39));
            Assert.That(atlases.Length, Is.EqualTo(7));
        }

        [Test]
        public void PersistentMaterials_UseStandaloneV2SurfaceTextures()
        {
            Material rage = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Project/VFX/PressTheAttack/Materials/PressTheAttack_CharacterRageOverlay.mat");
            Material lightning = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Project/VFX/PressTheAttack/Materials/PressTheAttack_ShrinkWrappedLightning.mat");

            Assert.That(rage, Is.Not.Null);
            Assert.That(lightning, Is.Not.Null);
            Assert.That(
                AssetDatabase.GetAssetPath(rage.GetTexture("_VeinMask")),
                Does.EndWith("PressTheAttack_RageVeinNetwork_V2.png"));
            Assert.That(
                AssetDatabase.GetAssetPath(rage.GetTexture("_FlowMask")),
                Does.Contain("/Textures/SurfaceV2/"));
            Assert.That(
                AssetDatabase.GetAssetPath(lightning.GetTexture("_LightningMaskA")),
                Does.EndWith("PressTheAttack_CrawlingLightning_V2.png"));
            Assert.That(lightning.shader.name, Is.EqualTo("RPG Clone/VFX/Press the Attack/Surface Lightning"));
        }

        [Test]
        public void PersistentSurfaceShaders_UseSharedMultiAxisProjection()
        {
            const string shaderFolder = "Assets/_Project/VFX/PressTheAttack/Shaders/";
            string projection = File.ReadAllText(shaderFolder + "PressTheAttackSurfaceProjection.hlsl");
            string rage = File.ReadAllText(shaderFolder + "PressTheAttackRageOverlay.shader");
            string lightning = File.ReadAllText(shaderFolder + "PressTheAttackSurfaceLightning.shader");
            string edge = File.ReadAllText(shaderFolder + "PressTheAttackEdgeStreak.shader");

            Assert.That(projection, Does.Contain("PTATriplanarWeights"));
            Assert.That(projection, Does.Contain("normalizedPosition.zy"));
            Assert.That(projection, Does.Contain("normalizedPosition.xz"));
            Assert.That(projection, Does.Contain("normalizedPosition.xy"));
            Assert.That(rage, Does.Contain("PressTheAttackSurfaceProjection.hlsl"));
            Assert.That(lightning, Does.Contain("PressTheAttackSurfaceProjection.hlsl"));
            Assert.That(edge, Does.Contain("PressTheAttackSurfaceProjection.hlsl"));
            Assert.That(rage, Does.Contain("_ProjectionWorldToLocal"));
            Assert.That(lightning, Does.Contain("_ProjectionWorldToLocal"));
            Assert.That(edge, Does.Contain("_ProjectionWorldToLocal"));
        }

        [Test]
        public void ModularCharacter_UsesSharedProjectionAndRejectsTransparentAbilityCards()
        {
            MMOAbilityDefinition pressTheAttack = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AbilityPath);
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(DefinitionPath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CombinedPrefabPath);
            Material berzerkitisCard = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Project/VFX/Berzerkitis/Materials/Berzerkitis_BodyRageEnvelope.mat");
            GameObject caster = new("Modular Press the Attack Caster");
            GameObject instance = null;

            try
            {
                GameObject torso = GameObject.CreatePrimitive(PrimitiveType.Cube);
                torso.name = "Torso Armor";
                torso.transform.SetParent(caster.transform, false);
                torso.transform.localPosition = new Vector3(0f, 1.15f, 0f);
                torso.transform.localScale = new Vector3(0.9f, 1.25f, 0.52f);

                GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                head.name = "Helmet";
                head.transform.SetParent(caster.transform, false);
                head.transform.localPosition = new Vector3(0f, 2.15f, 0f);
                head.transform.localScale = Vector3.one * 0.55f;

                GameObject abilityCard = GameObject.CreatePrimitive(PrimitiveType.Quad);
                abilityCard.name = "Body Rage Envelope";
                abilityCard.transform.SetParent(caster.transform, false);
                abilityCard.transform.localPosition = new Vector3(0f, 1.2f, -0.4f);
                abilityCard.transform.localScale = new Vector3(2f, 3f, 1f);
                abilityCard.GetComponent<MeshRenderer>().sharedMaterial = berzerkitisCard;

                MMOCharacterIdentity identity = caster.AddComponent<MMOCharacterIdentity>();
                MMOAbilitySystem abilitySystem = caster.AddComponent<MMOAbilitySystem>();
                MMOCharacterBuffController buffs = caster.AddComponent<MMOCharacterBuffController>();
                instance = Object.Instantiate(prefab);
                PressTheAttackVFX vfx = instance.GetComponent<PressTheAttackVFX>();
                MethodInfo lateUpdate = typeof(PressTheAttackVFX).GetMethod(
                    "LateUpdate",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                vfx.Initialize(new MMOAbilityVfxContext(
                    abilitySystem,
                    pressTheAttack,
                    definition,
                    caster.transform,
                    identity.transform,
                    caster.transform.position,
                    caster.transform.position,
                    false,
                    null));
                Assert.That(buffs.ApplyTemporaryModifiers(pressTheAttack, null), Is.True);
                lateUpdate.Invoke(vfx, null);

                Renderer[] overlays = caster.GetComponentsInChildren<Renderer>(true)
                    .Where(renderer => renderer.name.Contains("Overlay"))
                    .ToArray();
                Assert.That(overlays, Has.Length.EqualTo(6));
                Assert.That(overlays.Any(renderer => renderer.transform.IsChildOf(abilityCard.transform)), Is.False);

                MaterialPropertyBlock properties = new();
                overlays[0].GetPropertyBlock(properties);
                Vector4 sharedBoundsMin = properties.GetVector("_BoundsMin");
                Vector4 sharedBoundsSize = properties.GetVector("_BoundsSize");
                foreach (Renderer overlay in overlays.Skip(1))
                {
                    properties.Clear();
                    overlay.GetPropertyBlock(properties);
                    Assert.That(properties.GetVector("_BoundsMin"), Is.EqualTo(sharedBoundsMin));
                    Assert.That(properties.GetVector("_BoundsSize"), Is.EqualTo(sharedBoundsSize));
                }

                caster.transform.SetPositionAndRotation(new Vector3(7f, 0f, -4f), Quaternion.Euler(0f, 63f, 0f));
                lateUpdate.Invoke(vfx, null);
                properties.Clear();
                overlays[0].GetPropertyBlock(properties);
                Assert.That(properties.GetMatrix("_ProjectionWorldToLocal"), Is.EqualTo(caster.transform.worldToLocalMatrix));
            }
            finally
            {
                if (instance != null)
                {
                    Object.DestroyImmediate(instance);
                }

                Object.DestroyImmediate(caster);
            }
        }

        [Test]
        public void ActualPlayerModel_UsesOneProjectionAcrossAllSkinnedBodyParts()
        {
            MMOAbilityDefinition pressTheAttack = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AbilityPath);
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(DefinitionPath);
            GameObject effectPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CombinedPrefabPath);
            GameObject playerModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Models/Idle.fbx");
            GameObject berzerkitisPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/VFX/Berzerkitis/Prefabs/BerzerkitisVFX.prefab");
            GameObject caster = new("Actual Modular Player VFX Test");
            GameObject effectInstance = null;

            try
            {
                Assert.That(playerModel, Is.Not.Null);
                Assert.That(berzerkitisPrefab, Is.Not.Null);
                GameObject visual = Object.Instantiate(playerModel, caster.transform);
                visual.transform.SetLocalPositionAndRotation(new Vector3(0f, -0.13f, 0f), Quaternion.identity);
                SkinnedMeshRenderer[] bodyParts = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Where(renderer => renderer.enabled)
                    .ToArray();
                Assert.That(bodyParts.Length, Is.GreaterThanOrEqualTo(5));

                GameObject stackedBerzerkitis = Object.Instantiate(berzerkitisPrefab, caster.transform);
                stackedBerzerkitis.name = "Active Berzerkitis Stack";
                foreach (MeshRenderer cardRenderer in stackedBerzerkitis.GetComponentsInChildren<MeshRenderer>(true))
                {
                    cardRenderer.enabled = true;
                    cardRenderer.gameObject.SetActive(true);
                }

                MMOCharacterIdentity identity = caster.AddComponent<MMOCharacterIdentity>();
                MMOAbilitySystem abilitySystem = caster.AddComponent<MMOAbilitySystem>();
                MMOCharacterBuffController buffs = caster.AddComponent<MMOCharacterBuffController>();
                effectInstance = Object.Instantiate(effectPrefab);
                PressTheAttackVFX vfx = effectInstance.GetComponent<PressTheAttackVFX>();
                MethodInfo lateUpdate = typeof(PressTheAttackVFX).GetMethod(
                    "LateUpdate",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                vfx.Initialize(new MMOAbilityVfxContext(
                    abilitySystem,
                    pressTheAttack,
                    definition,
                    caster.transform,
                    identity.transform,
                    caster.transform.position,
                    caster.transform.position,
                    false,
                    null));
                Assert.That(buffs.ApplyTemporaryModifiers(pressTheAttack, null), Is.True);
                lateUpdate.Invoke(vfx, null);

                Renderer[] overlays = caster.GetComponentsInChildren<Renderer>(true)
                    .Where(renderer => renderer.name.Contains("Overlay"))
                    .ToArray();
                Assert.That(overlays.Length, Is.EqualTo(bodyParts.Length * 3));
                Assert.That(
                    overlays.Any(renderer => renderer.transform.IsChildOf(stackedBerzerkitis.transform)),
                    Is.False);

                MaterialPropertyBlock properties = new();
                overlays[0].GetPropertyBlock(properties);
                Vector4 projectionMin = properties.GetVector("_BoundsMin");
                Vector4 projectionSize = properties.GetVector("_BoundsSize");
                Assert.That(projectionSize.y, Is.GreaterThan(projectionSize.x));
                foreach (Renderer overlay in overlays.Skip(1))
                {
                    properties.Clear();
                    overlay.GetPropertyBlock(properties);
                    Assert.That(properties.GetVector("_BoundsMin"), Is.EqualTo(projectionMin));
                    Assert.That(properties.GetVector("_BoundsSize"), Is.EqualTo(projectionSize));
                }
            }
            finally
            {
                if (effectInstance != null)
                {
                    Object.DestroyImmediate(effectInstance);
                }

                Object.DestroyImmediate(caster);
            }
        }

        [Test]
        public void ReplicatedRuntimeSignals_DriveBuffLifetimeAndAttackResponse()
        {
            MMOAbilityDefinition pressTheAttack = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AbilityPath);
            MMOAbilityDefinition autoAttack = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(
                "Assets/_Project/Configs/Abilities/Auto_Attack.asset");
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(DefinitionPath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CombinedPrefabPath);
            GameObject caster = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            caster.name = "Replicated Press the Attack Caster";
            GameObject instance = null;

            try
            {
                MMOCharacterIdentity identity = caster.AddComponent<MMOCharacterIdentity>();
                MMOAbilitySystem abilitySystem = caster.AddComponent<MMOAbilitySystem>();
                MMOCharacterBuffController buffs = caster.AddComponent<MMOCharacterBuffController>();
                instance = Object.Instantiate(prefab);
                PressTheAttackVFX vfx = instance.GetComponent<PressTheAttackVFX>();
                FieldInfo buffObserved = typeof(PressTheAttackVFX).GetField(
                    "buffObserved",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo attackResponse = typeof(PressTheAttackVFX).GetField(
                    "attackResponse",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo fadingOut = typeof(PressTheAttackVFX).GetField(
                    "fadingOut",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo lateUpdate = typeof(PressTheAttackVFX).GetMethod(
                    "LateUpdate",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                vfx.Initialize(new MMOAbilityVfxContext(
                    abilitySystem,
                    pressTheAttack,
                    definition,
                    caster.transform,
                    identity.transform,
                    caster.transform.position,
                    caster.transform.position,
                    false,
                    null));

                Assert.That(buffObserved, Is.Not.Null);
                Assert.That(attackResponse, Is.Not.Null);
                Assert.That(fadingOut, Is.Not.Null);
                Assert.That(lateUpdate, Is.Not.Null);
                Assert.That(buffObserved.GetValue(vfx), Is.False);

                Assert.That(buffs.ApplyTemporaryModifiers(pressTheAttack, null), Is.True);
                Assert.That(buffObserved.GetValue(vfx), Is.True);
                Assert.That(buffs.FindBuff(pressTheAttack.AbilityId), Is.Not.Null);
                lateUpdate.Invoke(vfx, null);

                Renderer[] conformingLayers = caster.GetComponentsInChildren<Renderer>(true)
                    .Where(renderer => renderer.name.Contains("Overlay"))
                    .ToArray();
                Assert.That(conformingLayers, Has.Length.EqualTo(3));
                float[] surfaceLifts = conformingLayers.Select(renderer =>
                {
                    MaterialPropertyBlock properties = new();
                    renderer.GetPropertyBlock(properties);
                    return properties.GetFloat("_SurfaceLift");
                }).ToArray();
                Assert.That(surfaceLifts.Distinct().Count(), Is.EqualTo(3));

                abilitySystem.PlayReplicatedAbilityReleased(
                    autoAttack,
                    identity,
                    caster.transform.position,
                    false);
                Assert.That((float)attackResponse.GetValue(vfx), Is.EqualTo(1f));

                buffs.RemoveBuff(pressTheAttack.AbilityId);
                Assert.That(fadingOut.GetValue(vfx), Is.True);
            }
            finally
            {
                if (instance != null)
                {
                    Object.DestroyImmediate(instance);
                }

                Object.DestroyImmediate(caster);
            }
        }
    }
}
#endif
