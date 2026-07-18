using System.Collections.Generic;
using RPGClone.Abilities;
using RPGClone.Vfx;
using RPGClone.Vfx.Fire;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace RPGClone.EditorTools
{
    public static class FireballVFXInstaller
    {
        private const string RootFolder = "Assets/_Project/VFX/Fireball";
        private const string TextureFolder = RootFolder + "/Textures";
        private const string ShaderFolder = RootFolder + "/Shaders";
        private const string MaterialFolder = RootFolder + "/Materials";
        private const string ProfileFolder = RootFolder + "/Profiles";
        private const string PrefabFolder = RootFolder + "/Prefabs";
        private const string ProfilePath = ProfileFolder + "/FireballVFX_Default.asset";
        private const string PrefabPath = PrefabFolder + "/FireballVFX.prefab";
        private const string CastingPrefabPath = PrefabFolder + "/FireballVFX_Casting.prefab";
        private const string ProjectilePrefabPath = PrefabFolder + "/FireballVFX_Projectile.prefab";
        private const string ImpactPrefabPath = PrefabFolder + "/FireballVFX_Impact.prefab";
        private const string SpellVfxDefinitionPath = "Assets/_Project/VFX/Definitions/Mage_Fireball_VFX.asset";
        private const string MageFireballAbilityPath = "Assets/_Project/Configs/Abilities/Mage_Fireball.asset";

        private static readonly string[] RequiredTextureNames =
        {
            "Fireball_Core.png",
            "Fireball_FlameBody.png",
            "Fireball_FlameRibbon.png",
            "Fireball_Noise.png",
            "Fireball_Ember.png",
            "Fireball_Burst.png",
            "Fireball_ImpactFlash.png",
            "Fireball_Shockwave.png",
            "Fireball_Smoke.png",
            "Fireball_Scorch.png",
            "Fireball_CometHead.png",
            "Fireball_FlameCorona.png",
            "Fireball_ImpactCrown.png",
            "Fireball_HeatRing.png"
        };

        [MenuItem("Tools/RPG Clone/VFX/Build Fireball VFX")]
        public static void Build()
        {
            EnsureFolders();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureTextureImporters();

            FireballVFXProfile profile = LoadOrCreateProfile();
            profile.UpgradePolishDefaults();
            EditorUtility.SetDirty(profile);
            Dictionary<string, Material> materials = CreateMaterials();
            GameObject completePrefab = CreateCompletePrefab(profile, materials);
            GameObject castingPrefab = CreatePhasePrefab(completePrefab, CastingPrefabPath, FireballVFXPhaseAdapter.Phase.Casting, false);
            GameObject projectilePrefab = CreatePhasePrefab(completePrefab, ProjectilePrefabPath, FireballVFXPhaseAdapter.Phase.Projectile, true);
            GameObject impactPrefab = CreatePhasePrefab(completePrefab, ImpactPrefabPath, FireballVFXPhaseAdapter.Phase.Impact, false);
            WireIntoMageFireball(castingPrefab, projectilePrefab, impactPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = completePrefab;
            EditorGUIUtility.PingObject(completePrefab);
            Debug.Log($"Built reusable FireballVFX and wired Mage Fireball at '{PrefabPath}'.", completePrefab);
        }

        [MenuItem("Tools/RPG Clone/VFX/Validate Fireball VFX")]
        public static void ValidateBuild()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                throw new MissingReferenceException($"Fireball prefab is missing at {PrefabPath}.");
            }

            foreach (string section in new[] { "Casting Effect", "Projectile Effect", "Trail Effect", "Impact Effect", "Aftermath Effect" })
            {
                if (prefab.transform.Find(section) == null)
                {
                    throw new MissingReferenceException($"FireballVFX is missing its '{section}' section.");
                }
            }

            FireballVFX controller = prefab.GetComponent<FireballVFX>();
            if (controller == null || controller.Profile == null)
            {
                throw new MissingReferenceException("FireballVFX controller or profile reference is missing.");
            }

            if (prefab.GetComponentsInChildren<Light>(true).Length != 0
                || prefab.GetComponentsInChildren<Animator>(true).Length != 0
                || prefab.GetComponentsInChildren<UnityEngine.Animation>(true).Length != 0)
            {
                throw new UnityException("FireballVFX must remain procedural and light-free.");
            }

            if (prefab.GetComponentsInChildren<TrailRenderer>(true).Length != 2
                || prefab.GetComponentsInChildren<ParticleSystem>(true).Length != 10)
            {
                throw new UnityException("FireballVFX must contain exactly two trail layers and ten budgeted particle systems.");
            }

            foreach (string layerPath in new[]
                     {
                         "Casting Effect/Launch Heat Ring",
                         "Projectile Effect/Asymmetric Comet Head",
                         "Projectile Effect/Rotating Flame Corona",
                         "Impact Effect/Heavy Impact Crown",
                         "Impact Effect/Expanding Heat Ring"
                     })
            {
                if (prefab.transform.Find(layerPath) == null)
                {
                    throw new MissingReferenceException($"Polished FireballVFX layer is missing: {layerPath}");
                }
            }

            if (AssetDatabase.FindAssets("t:Material", new[] { MaterialFolder }).Length != 14)
            {
                throw new UnityException("FireballVFX must contain exactly fourteen reusable materials after the polish pass.");
            }

            foreach (string textureName in RequiredTextureNames)
            {
                if (AssetDatabase.LoadAssetAtPath<Texture>($"{TextureFolder}/{textureName}") == null)
                {
                    throw new MissingReferenceException($"Required Fireball texture is missing: {textureName}");
                }
            }

            ValidateShader($"{ShaderFolder}/FireballSpriteUnlit.shader");
            ValidatePhasePrefab(CastingPrefabPath, FireballVFXPhaseAdapter.Phase.Casting, false);
            ValidatePhasePrefab(ProjectilePrefabPath, FireballVFXPhaseAdapter.Phase.Projectile, true);
            ValidatePhasePrefab(ImpactPrefabPath, FireballVFXPhaseAdapter.Phase.Impact, false);
            ValidateMageFireballWiring();
            ValidateLifecycle(prefab);
            Debug.Log("FireballVFX validation passed: five-section prefab, procedural assets, phase wrappers, lifecycle, and Mage Fireball wiring are valid.", prefab);
        }

        private static void ValidateShader(string path)
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (shader == null || !shader.isSupported)
            {
                throw new MissingReferenceException($"Fireball VFX shader is missing or unsupported: {path}");
            }

            foreach (ShaderMessage message in ShaderUtil.GetShaderMessages(shader))
            {
                if (message.severity == ShaderCompilerMessageSeverity.Error)
                {
                    throw new UnityException($"Shader error in {path}: {message.message}");
                }
            }
        }

        private static void ValidatePhasePrefab(string path, FireballVFXPhaseAdapter.Phase phase, bool requiresProjectile)
        {
            GameObject wrapper = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (wrapper == null
                || wrapper.GetComponent<FireballVFXPhaseAdapter>() == null
                || wrapper.GetComponentInChildren<FireballVFX>(true) == null
                || (wrapper.GetComponent<MMOAbilityVfxProjectile>() != null) != requiresProjectile)
            {
                throw new MissingReferenceException($"Fireball {phase} integration wrapper is invalid: {path}");
            }
        }

        private static void ValidateMageFireballWiring()
        {
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(SpellVfxDefinitionPath);
            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(MageFireballAbilityPath);
            if (definition == null
                || ability == null
                || ability.VisualEffects != definition
                || definition.CastingPrefab != AssetDatabase.LoadAssetAtPath<GameObject>(CastingPrefabPath)
                || definition.CastPrefab != AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePrefabPath)
                || definition.HitPrefab != AssetDatabase.LoadAssetAtPath<GameObject>(ImpactPrefabPath)
                || !definition.CastPrefabControlsHitTiming)
            {
                throw new MissingReferenceException("Mage Fireball ability is not wired through its VFX definition to the new casting, projectile, and impact wrappers.");
            }
        }

        private static void ValidateLifecycle(GameObject prefab)
        {
            GameObject instance = null;
            GameObject attachment = null;
            try
            {
                instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                attachment = new GameObject("FireballVFX Validation Attachment") { hideFlags = HideFlags.HideAndDontSave };
                instance.hideFlags = HideFlags.HideAndDontSave;
                attachment.transform.position = new Vector3(1f, 1.4f, -0.5f);
                FireballVFX controller = instance.GetComponent<FireballVFX>();
                System.Reflection.MethodInfo awake = typeof(FireballVFX).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                awake?.Invoke(controller, null);

                controller.SetCastPoint(attachment.transform);
                controller.PlayCasting();
                if (!controller.IsPlaying || controller.ReadyForPool || !instance.transform.Find("Casting Effect").gameObject.activeSelf)
                {
                    throw new UnityException("Fireball casting phase did not start correctly.");
                }

                controller.ReleaseCasting();
                controller.AttachToProjectile(attachment.transform);
                controller.PlayProjectile();
                if (!instance.transform.Find("Projectile Effect").gameObject.activeSelf
                    || !instance.transform.Find("Trail Effect").gameObject.activeSelf)
                {
                    throw new UnityException("Fireball projectile and trail phases did not start correctly.");
                }

                controller.TriggerImpact(Vector3.one, Vector3.up);
                if (!instance.transform.Find("Impact Effect").gameObject.activeSelf
                    || !instance.transform.Find("Aftermath Effect").gameObject.activeSelf)
                {
                    throw new UnityException("Fireball impact and aftermath phases did not start correctly.");
                }

                controller.StopImmediate();
                if (!controller.ReadyForPool || controller.IsPlaying)
                {
                    throw new UnityException("FireballVFX did not reset to a pool-ready state.");
                }
                controller.ResetForPool();
            }
            finally
            {
                if (instance != null) Object.DestroyImmediate(instance);
                if (attachment != null) Object.DestroyImmediate(attachment);
            }
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/_Project/VFX", "Fireball");
            EnsureFolder(RootFolder, "Textures");
            EnsureFolder(RootFolder, "Shaders");
            EnsureFolder(RootFolder, "Materials");
            EnsureFolder(RootFolder, "Profiles");
            EnsureFolder(RootFolder, "Prefabs");
            EnsureFolder(RootFolder, "Documentation");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static void ConfigureTextureImporters()
        {
            foreach (string textureName in RequiredTextureNames)
            {
                bool isNoise = textureName == "Fireball_Noise.png";
                bool repeat = isNoise || textureName == "Fireball_FlameRibbon.png";
                bool isPolishTexture = textureName is "Fireball_CometHead.png" or "Fireball_FlameCorona.png" or "Fireball_ImpactCrown.png" or "Fireball_HeatRing.png";
                int maxSize = textureName == "Fireball_FlameRibbon.png" || isPolishTexture ? 512 : textureName == "Fireball_Ember.png" ? 128 : 256;
                ConfigureTexture(textureName, repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp, maxSize, !isNoise, !isNoise);
            }

            ConfigureTexture("Fireball_SourceAtlas.png", TextureWrapMode.Clamp, 2048, false, true);
            ConfigureTexture("Fireball_PolishSourceAtlas.png", TextureWrapMode.Clamp, 2048, false, true);
        }

        private static void ConfigureTexture(string fileName, TextureWrapMode wrapMode, int maxSize, bool hasAlpha, bool sRgb)
        {
            string path = $"{TextureFolder}/{fileName}";
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                throw new MissingReferenceException($"Required Fireball VFX texture is missing: {path}");
            }

            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = wrapMode;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = hasAlpha;
            importer.sRGBTexture = sRgb;
            importer.maxTextureSize = maxSize;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
        }

        private static FireballVFXProfile LoadOrCreateProfile()
        {
            FireballVFXProfile profile = AssetDatabase.LoadAssetAtPath<FireballVFXProfile>(ProfilePath);
            if (profile != null) return profile;
            profile = ScriptableObject.CreateInstance<FireballVFXProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
            return profile;
        }

        private static Dictionary<string, Material> CreateMaterials()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>($"{ShaderFolder}/FireballSpriteUnlit.shader");
            if (shader == null)
            {
                throw new MissingReferenceException("Fireball shader must compile before building materials.");
            }

            Texture noise = LoadTexture("Fireball_Noise.png");
            return new Dictionary<string, Material>
            {
                ["Core"] = CreateMaterial("Fireball_Core", shader, LoadTexture("Fireball_Core.png"), noise, new Color(1.18f, 0.58f, 0.12f, 1f), new Color(1.9f, 1.55f, 0.88f, 1f), true),
                ["FlameBody"] = CreateMaterial("Fireball_FlameBody", shader, LoadTexture("Fireball_FlameBody.png"), noise, new Color(1.35f, 0.26f, 0.018f, 0.94f), new Color(1.72f, 0.92f, 0.26f, 1f), true),
                ["OuterShell"] = CreateMaterial("Fireball_OuterFlameShell", shader, LoadTexture("Fireball_FlameBody.png"), noise, new Color(0.78f, 0.06f, 0.005f, 0.66f), new Color(1.28f, 0.31f, 0.025f, 1f), true),
                ["CometHead"] = CreateMaterial("Fireball_CometHead", shader, LoadTexture("Fireball_CometHead.png"), noise, new Color(1.38f, 0.19f, 0.012f, 0.94f), new Color(1.82f, 1.08f, 0.38f, 1f), true),
                ["FlameCorona"] = CreateMaterial("Fireball_FlameCorona", shader, LoadTexture("Fireball_FlameCorona.png"), noise, new Color(0.92f, 0.07f, 0.004f, 0.68f), new Color(1.52f, 0.42f, 0.045f, 1f), true),
                ["FlameTrail"] = CreateMaterial("Fireball_FlameTrail", shader, LoadTexture("Fireball_FlameRibbon.png"), noise, new Color(1.23f, 0.18f, 0.008f, 0.86f), new Color(1.68f, 0.82f, 0.16f, 1f), true),
                ["Ember"] = CreateMaterial("Fireball_Embers", shader, LoadTexture("Fireball_Ember.png"), noise, new Color(1.36f, 0.25f, 0.02f, 0.94f), new Color(1.78f, 1.04f, 0.34f, 1f), true),
                ["Smoke"] = CreateMaterial("Fireball_Smoke", shader, LoadTexture("Fireball_Smoke.png"), noise, new Color(0.25f, 0.12f, 0.075f, 0.45f), new Color(0.38f, 0.2f, 0.11f, 1f), false, 0.7f, 1.05f),
                ["ImpactFlash"] = CreateMaterial("Fireball_ImpactFlash", shader, LoadTexture("Fireball_ImpactFlash.png"), noise, new Color(1.55f, 0.88f, 0.2f, 1f), new Color(2f, 1.72f, 1.1f, 1f), true, 0.5f, 0.72f),
                ["FireBurst"] = CreateMaterial("Fireball_FireBurst", shader, LoadTexture("Fireball_Burst.png"), noise, new Color(1.26f, 0.15f, 0.008f, 0.94f), new Color(1.76f, 0.78f, 0.15f, 1f), true),
                ["ImpactCrown"] = CreateMaterial("Fireball_ImpactCrown", shader, LoadTexture("Fireball_ImpactCrown.png"), noise, new Color(1.12f, 0.09f, 0.004f, 0.9f), new Color(1.82f, 0.92f, 0.24f, 1f), true),
                ["Shockwave"] = CreateMaterial("Fireball_Shockwave", shader, LoadTexture("Fireball_Shockwave.png"), noise, new Color(1.08f, 0.18f, 0.015f, 0.74f), new Color(1.55f, 0.58f, 0.08f, 1f), true),
                ["HeatRing"] = CreateMaterial("Fireball_HeatRing", shader, LoadTexture("Fireball_HeatRing.png"), noise, new Color(0.94f, 0.07f, 0.004f, 0.72f), new Color(1.48f, 0.38f, 0.035f, 1f), true),
                ["GroundScorch"] = CreateMaterial("Fireball_GroundScorch", shader, LoadTexture("Fireball_Scorch.png"), noise, new Color(0.18f, 0.07f, 0.025f, 0.58f), new Color(0.34f, 0.12f, 0.035f, 1f), false, 0.72f, 0.9f)
            };
        }

        private static Texture LoadTexture(string fileName)
        {
            Texture texture = AssetDatabase.LoadAssetAtPath<Texture>($"{TextureFolder}/{fileName}");
            if (texture == null) throw new MissingReferenceException($"Missing Fireball texture: {fileName}");
            return texture;
        }

        private static Material CreateMaterial(string name, Shader shader, Texture texture, Texture noise, Color tint, Color hotTint, bool additive, float hotThreshold = 0.58f, float alphaPower = 0.82f)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            material.SetTexture("_BaseMap", texture);
            material.SetTexture("_NoiseMap", noise);
            material.SetColor("_Tint", tint);
            material.SetColor("_HotTint", hotTint);
            material.SetFloat("_HotThreshold", hotThreshold);
            material.SetFloat("_AlphaPower", alphaPower);
            material.SetFloat("_Opacity", 1f);
            material.SetFloat("_DistortionStrength", 0.04f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            material.enableInstancing = true;
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreateCompletePrefab(FireballVFXProfile profile, IReadOnlyDictionary<string, Material> materials)
        {
            GameObject root = new("FireballVFX");
            try
            {
                FireballVFX controller = root.AddComponent<FireballVFX>();

                Transform castingRoot = CreateSection("Casting Effect", root.transform);
                MeshRenderer castGlow = CreateQuad("Warm Gathering Glow", castingRoot, materials["FlameBody"], Vector3.zero, Vector3.one, Quaternion.identity, 1);
                ParticleSystem gathering = CreateGatheringEmbers("Inward Gathering Embers", castingRoot, materials["Ember"]);
                ParticleSystem swirl = CreateCastSwirl("Compact Swirling Fire", castingRoot, materials["FlameBody"]);
                ParticleSystem launch = CreateOneShot("Launch Flash", castingRoot, materials["ImpactFlash"], 0.22f, 0f, 0.64f, 1, new Color(1f, 0.92f, 0.58f, 1f), 5);
                ConfigureFadeAndScale(launch, 0.2f, 1.45f, 1.8f);
                ParticleSystem residual = CreateBurstEmbers("Release Embers", castingRoot, materials["Ember"], 5, 0.45f, 0.8f, 0.1f);
                MeshRenderer launchRing = CreateQuad("Launch Heat Ring", castingRoot, materials["HeatRing"], Vector3.zero, Vector3.one, Quaternion.identity, 0);

                Transform projectileRoot = CreateSection("Projectile Effect", root.transform);
                MeshRenderer core = CreateQuad("Hot Inner Core", projectileRoot, materials["Core"], Vector3.zero, Vector3.one, Quaternion.identity, 7);
                MeshRenderer body = CreateQuad("Main Flame Body", projectileRoot, materials["FlameBody"], Vector3.zero, Vector3.one, Quaternion.identity, 4);
                MeshRenderer outer = CreateQuad("Outer Flame Shell", projectileRoot, materials["OuterShell"], Vector3.zero, Vector3.one, Quaternion.identity, 2);
                MeshRenderer cometHead = CreateQuad("Asymmetric Comet Head", projectileRoot, materials["CometHead"], Vector3.zero, Vector3.one, Quaternion.identity, 5);
                MeshRenderer corona = CreateQuad("Rotating Flame Corona", projectileRoot, materials["FlameCorona"], Vector3.zero, Vector3.one, Quaternion.identity, 1);
                ParticleSystem projectileEmbers = CreateLoopingTravelParticles("Projectile Embers", projectileRoot, materials["Ember"], 5f, 0.62f, 0.06f, new Color(1f, 0.54f, 0.12f, 0.9f), 8, false);

                Transform trailRoot = CreateSection("Trail Effect", root.transform);
                TrailRenderer brightTrail = CreateTrail("Bright Flame Trail", trailRoot, materials["FlameTrail"], 0.3f, 5);
                TrailRenderer outerTrail = CreateTrail("Outer Orange Trail", trailRoot, materials["FlameTrail"], 0.46f, 1);
                ParticleSystem trailEmbers = CreateLoopingTravelParticles("Ember Trail", trailRoot, materials["Ember"], 3f, 0.75f, 0.045f, new Color(1f, 0.42f, 0.08f, 0.78f), 5, true);
                ParticleSystem smokeTrail = CreateLoopingTravelParticles("Light Smoke Trail", trailRoot, materials["Smoke"], 1.8f, 0.72f, 0.24f, new Color(0.52f, 0.31f, 0.21f, 0.38f), 4, true);

                Transform impactRoot = CreateSection("Impact Effect", root.transform);
                MeshRenderer impactFlash = CreateQuad("Immediate Impact Flash", impactRoot, materials["ImpactFlash"], Vector3.zero, Vector3.one, Quaternion.identity, 10);
                MeshRenderer burst = CreateQuad("Chunky Fire Burst", impactRoot, materials["FireBurst"], Vector3.zero, Vector3.one, Quaternion.identity, 6);
                MeshRenderer shockwave = CreateQuad("Broken Painted Shockwave", impactRoot, materials["Shockwave"], Vector3.zero, Vector3.one, Quaternion.identity, 5);
                MeshRenderer impactCrown = CreateQuad("Heavy Impact Crown", impactRoot, materials["ImpactCrown"], Vector3.zero, Vector3.one, Quaternion.identity, 7);
                MeshRenderer heatRing = CreateQuad("Expanding Heat Ring", impactRoot, materials["HeatRing"], Vector3.zero, Vector3.one, Quaternion.identity, 4);
                ParticleSystem impactEmbers = CreateBurstEmbers("Impact Embers", impactRoot, materials["Ember"], 9, 0.9f, 1.65f, 0.1f);
                ParticleSystem impactFlames = CreateBurstParticles("Impact Flame Shapes", impactRoot, materials["FlameBody"], 7, 0.54f, 0.82f, 0.42f);

                Transform aftermathRoot = CreateSection("Aftermath Effect", root.transform);
                ParticleSystem impactSmoke = CreateImpactSmoke("Rising Smoke Bloom", aftermathRoot, materials["Smoke"]);
                MeshRenderer scorch = CreateQuad("Optional Ground Scorch", aftermathRoot, materials["GroundScorch"], new Vector3(0f, 0.02f, 0f), Vector3.one, Quaternion.Euler(90f, 0f, 0f), -2);

                controller.ConfigureAuthoring(
                    profile,
                    castingRoot, castGlow, gathering, swirl, launch, residual, launchRing,
                    projectileRoot, core, body, outer, cometHead, corona, projectileEmbers,
                    trailRoot, brightTrail, outerTrail, trailEmbers, smokeTrail,
                    impactRoot, impactFlash, burst, shockwave, impactCrown, heatRing, impactEmbers, impactFlames,
                    aftermathRoot, impactSmoke, scorch);

                castingRoot.gameObject.SetActive(false);
                projectileRoot.gameObject.SetActive(false);
                trailRoot.gameObject.SetActive(false);
                impactRoot.gameObject.SetActive(false);
                aftermathRoot.gameObject.SetActive(false);

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (prefab == null) throw new UnityException($"Failed to save FireballVFX at {PrefabPath}.");
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreatePhasePrefab(GameObject completePrefab, string path, FireballVFXPhaseAdapter.Phase phase, bool addProjectile)
        {
            GameObject root = new($"FireballVFX_{phase}");
            try
            {
                GameObject nested = PrefabUtility.InstantiatePrefab(completePrefab, root.transform) as GameObject;
                nested.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                FireballVFX fireball = nested.GetComponent<FireballVFX>();
                FireballVFXPhaseAdapter adapter = root.AddComponent<FireballVFXPhaseAdapter>();
                adapter.ConfigureAuthoring(phase, fireball);
                if (addProjectile)
                {
                    MMOAbilityVfxProjectile projectile = root.AddComponent<MMOAbilityVfxProjectile>();
                    projectile.Configure(18f, 0f, 4f, true);
                }

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                if (prefab == null) throw new UnityException($"Failed to save Fireball phase prefab at {path}.");
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void WireIntoMageFireball(GameObject castingPrefab, GameObject projectilePrefab, GameObject impactPrefab)
        {
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(SpellVfxDefinitionPath);
            if (definition == null)
            {
                throw new MissingReferenceException($"Mage Fireball VFX definition is missing: {SpellVfxDefinitionPath}");
            }

            definition.Configure(
                castingPrefab,
                projectilePrefab,
                impactPrefab,
                true,
                true,
                true,
                true,
                new Vector3(0f, 1.15f, 0.42f),
                Vector3.zero,
                new Vector3(0f, 1.18f, 0.48f),
                new Vector3(0f, 0.85f, 0f),
                0.18f,
                true);
            EditorUtility.SetDirty(definition);
        }

        private static Transform CreateSection(string name, Transform parent)
        {
            GameObject section = new(name);
            section.transform.SetParent(parent, false);
            return section.transform;
        }

        private static MeshRenderer CreateQuad(string name, Transform parent, Material material, Vector3 position, Vector3 scale, Quaternion rotation, int sortingOrder)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = position;
            child.transform.localRotation = rotation;
            child.transform.localScale = scale;
            MeshFilter filter = child.AddComponent<MeshFilter>();
            filter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            MeshRenderer renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.allowOcclusionWhenDynamic = false;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private static TrailRenderer CreateTrail(string name, Transform parent, Material material, float width, int sortingOrder)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            TrailRenderer trail = child.AddComponent<TrailRenderer>();
            trail.sharedMaterial = material;
            trail.time = 0.25f;
            trail.emitting = false;
            trail.minVertexDistance = 0.04f;
            trail.widthMultiplier = width;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.35f, 0.72f), new Keyframe(1f, 1f));
            trail.colorGradient = CreateAlphaGradient(0f, 0.84f, 1f);
            trail.textureMode = LineTextureMode.Tile;
            trail.alignment = LineAlignment.View;
            trail.numCornerVertices = 2;
            trail.numCapVertices = 2;
            trail.generateLightingData = false;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.allowOcclusionWhenDynamic = false;
            trail.sortingOrder = sortingOrder;
            return trail;
        }

        private static ParticleSystem CreateGatheringEmbers(string name, Transform parent, Material material)
        {
            ParticleSystem system = CreateParticleSystem(name, parent, material, true, ParticleSystemSimulationSpace.Local, 1.2f, 0f, 0.08f, Color.white, 10, 3);
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 8f;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.46f;
            shape.radiusThickness = 0.15f;
            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.radial = -0.42f;
            velocity.orbitalY = 1.15f;
            ConfigureFadeAndScale(system, 0.25f, 1f, 0.1f);
            return system;
        }

        private static ParticleSystem CreateCastSwirl(string name, Transform parent, Material material)
        {
            ParticleSystem system = CreateParticleSystem(name, parent, material, true, ParticleSystemSimulationSpace.Local, 0.72f, 0f, 0.18f, Color.white, 6, 2);
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 4f;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.25f;
            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.orbitalY = 1.35f;
            ConfigureFadeAndScale(system, 0.2f, 0.9f, 0.3f);
            return system;
        }

        private static ParticleSystem CreateOneShot(string name, Transform parent, Material material, float lifetime, float speed, float size, int count, Color color, int sortingOrder)
        {
            ParticleSystem system = CreateParticleSystem(name, parent, material, false, ParticleSystemSimulationSpace.Local, lifetime, speed, size, color, Mathf.Max(1, count), sortingOrder);
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            return system;
        }

        private static ParticleSystem CreateBurstEmbers(string name, Transform parent, Material material, int count, float lifetime, float speed, float size)
        {
            ParticleSystem system = CreateOneShot(name, parent, material, lifetime, speed, size, count, Color.white, 7);
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.16f;
            ParticleSystem.ForceOverLifetimeModule force = system.forceOverLifetime;
            force.enabled = true;
            force.y = -0.75f;
            ConfigureFadeAndScale(system, 0.85f, 1f, 0f);
            return system;
        }

        private static ParticleSystem CreateBurstParticles(string name, Transform parent, Material material, int count, float lifetime, float speed, float size)
        {
            ParticleSystem system = CreateOneShot(name, parent, material, lifetime, speed, size, count, Color.white, 4);
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.22f;
            ConfigureFadeAndScale(system, 0.45f, 1.3f, 0.15f);
            return system;
        }

        private static ParticleSystem CreateLoopingTravelParticles(string name, Transform parent, Material material, float rate, float lifetime, float size, Color color, int maxParticles, bool drift)
        {
            ParticleSystem system = CreateParticleSystem(name, parent, material, true, ParticleSystemSimulationSpace.World, lifetime, 0f, size, color, maxParticles, 4);
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = rate;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.18f;
            if (drift)
            {
                ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
                velocity.enabled = true;
                velocity.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
                velocity.y = new ParticleSystem.MinMaxCurve(0.04f, 0.2f);
                velocity.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            }
            ConfigureFadeAndScale(system, 0.85f, 1f, 0f);
            return system;
        }

        private static ParticleSystem CreateImpactSmoke(string name, Transform parent, Material material)
        {
            ParticleSystem system = CreateOneShot(name, parent, material, 1.45f, 0.18f, 0.42f, 4, new Color(0.56f, 0.36f, 0.27f, 0.48f), 1);
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.28f;
            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.y = new ParticleSystem.MinMaxCurve(0.18f, 0.42f);
            ConfigureFadeAndScale(system, 0.4f, 1.2f, 1.75f);
            return system;
        }

        private static ParticleSystem CreateParticleSystem(string name, Transform parent, Material material, bool loop, ParticleSystemSimulationSpace space, float lifetime, float speed, float size, Color color, int maxParticles, int sortingOrder)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            ParticleSystem system = child.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = system.main;
            main.duration = Mathf.Max(0.1f, lifetime);
            main.loop = loop;
            main.playOnAwake = false;
            main.simulationSpace = space;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = color;
            main.maxParticles = maxParticles;
            main.stopAction = ParticleSystemStopAction.None;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;
            ParticleSystemRenderer renderer = child.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = sortingOrder;
            return system;
        }

        private static void ConfigureFadeAndScale(ParticleSystem system, float startScale, float middleScale, float endScale)
        {
            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = CreateAlphaGradient(0f, 1f, 0f);
            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, startScale),
                new Keyframe(0.35f, middleScale),
                new Keyframe(1f, endScale)));
        }

        private static Gradient CreateAlphaGradient(float start, float middle, float end)
        {
            Gradient gradient = new();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(start, 0f), new GradientAlphaKey(middle, 0.18f), new GradientAlphaKey(end, 1f) });
            return gradient;
        }
    }
}
