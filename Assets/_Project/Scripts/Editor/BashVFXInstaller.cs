using System.Collections.Generic;
using RPGClone.Abilities;
using RPGClone.Vfx;
using RPGClone.Vfx.Warrior;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace RPGClone.EditorTools
{
    public static class BashVFXInstaller
    {
        private const string RootFolder = "Assets/_Project/VFX/Bash";
        private const string TextureFolder = RootFolder + "/Textures";
        private const string ShaderFolder = RootFolder + "/Shaders";
        private const string MaterialFolder = RootFolder + "/Materials";
        private const string ProfileFolder = RootFolder + "/Profiles";
        private const string PrefabFolder = RootFolder + "/Prefabs";
        private const string ProfilePath = ProfileFolder + "/BashVFX_Default.asset";
        private const string PrefabPath = PrefabFolder + "/BashVFX.prefab";
        private const string ShaderPath = ShaderFolder + "/BashSpriteUnlit.shader";
        private const string DefinitionPath = "Assets/_Project/VFX/Definitions/Warrior_Bash_VFX.asset";
        private const string AbilityPath = "Assets/_Project/Configs/Abilities/Warrior_Bash.asset";
        private const string ChargeHeavyDustTexturePath = "Assets/_Project/VFX/Charge/Textures/Charge_HeavyDustAtlas.png";
        private const string ChargeFineDustTexturePath = "Assets/_Project/VFX/Charge/Textures/Charge_FineDustAtlas.png";
        private const string ChargeGroundBurstTexturePath = "Assets/_Project/VFX/Charge/Textures/Charge_GroundBurstAtlas.png";

        private static readonly string[] RequiredTextureNames =
        {
            "Bash_BrushArc.png",
            "Bash_ImpactFlash.png",
            "Bash_ImpactBurstAtlas.png",
            "Bash_DustPuffAtlas.png",
            "Bash_DustRing.png",
            "Bash_MetallicSpark.png",
            "Bash_StunStar.png"
        };

        private static readonly string[] RequiredMaterialNames =
        {
            "Bash_SwingAccent",
            "Bash_ImpactBackplate",
            "Bash_ImpactFlash",
            "Bash_ImpactBurst",
            "Bash_SecondaryBurst",
            "Bash_Dust",
            "Bash_DustRing",
            "Bash_Debris",
            "Bash_EnvironmentalGroundBurst",
            "Bash_EnvironmentalHeavyDust",
            "Bash_EnvironmentalFineDust",
            "Bash_Sparks",
            "Bash_StunStars"
        };

        [MenuItem("Tools/RPG Clone/VFX/Build Bash VFX")]
        public static void Build()
        {
            EnsureFolders();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureTextureImporters();

            BashVFXProfile profile = LoadOrCreateProfile();
            EditorUtility.SetDirty(profile);
            Dictionary<string, Material> materials = CreateMaterials();
            GameObject prefab = CreatePrefab(profile, materials);
            WireIntoBash(prefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log($"Built reusable BashVFX and wired Warrior Bash at '{PrefabPath}'.", prefab);
        }

        [MenuItem("Tools/RPG Clone/VFX/Validate Bash VFX")]
        public static void ValidateBuild()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                throw new MissingReferenceException($"Bash prefab is missing at {PrefabPath}.");
            }

            foreach (string section in new[] { "Impact Layers", "Ground Reaction", "Stun Accent" })
            {
                if (prefab.transform.Find($"Visual Root/{section}") == null)
                {
                    throw new MissingReferenceException($"BashVFX is missing its '{section}' section.");
                }
            }

            foreach (string layerPath in new[]
            {
                "Visual Root/Impact Layers/Dark Impact Backplate",
                "Visual Root/Impact Layers/Secondary Orange Impact",
                "Visual Root/Impact Layers/Forward Momentum Streaks",
                "Visual Root/Ground Reaction/Ground Debris Chunks",
                "Visual Root/Ground Reaction/Radial Dust Ring",
                "Visual Root/Ground Reaction/Environmental Ground Burst",
                "Visual Root/Ground Reaction/Environmental Heavy Dust",
                "Visual Root/Ground Reaction/Environmental Fine Dust"
            })
            {
                if (prefab.transform.Find(layerPath) == null)
                {
                    throw new MissingReferenceException($"BashVFX polish layer is missing: {layerPath}");
                }
            }

            BashVFX controller = prefab.GetComponent<BashVFX>();
            if (controller == null || controller.Profile == null)
            {
                throw new MissingReferenceException("BashVFX controller or profile reference is missing.");
            }

            ParticleSystem[] particles = prefab.GetComponentsInChildren<ParticleSystem>(true);
            if (particles.Length != 15)
            {
                throw new UnityException($"BashVFX must contain exactly fifteen bounded particle layers; found {particles.Length}.");
            }

            foreach (string worldLayerPath in new[]
            {
                "Visual Root/Ground Reaction/Environmental Ground Burst",
                "Visual Root/Ground Reaction/Environmental Heavy Dust",
                "Visual Root/Ground Reaction/Environmental Fine Dust"
            })
            {
                ParticleSystem worldLayer = prefab.transform.Find(worldLayerPath)?.GetComponent<ParticleSystem>();
                if (worldLayer == null || worldLayer.main.simulationSpace != ParticleSystemSimulationSpace.World)
                {
                    throw new UnityException($"Bash environmental layer must remain in world space: {worldLayerPath}");
                }
            }

            if (prefab.GetComponentsInChildren<Light>(true).Length != 0
                || prefab.GetComponentsInChildren<Animator>(true).Length != 0
                || prefab.GetComponentsInChildren<UnityEngine.Animation>(true).Length != 0)
            {
                throw new UnityException("BashVFX must remain procedural, light-free, and animator-free.");
            }

            foreach (string textureName in RequiredTextureNames)
            {
                if (AssetDatabase.LoadAssetAtPath<Texture>($"{TextureFolder}/{textureName}") == null)
                {
                    throw new MissingReferenceException($"Required Bash texture is missing: {textureName}");
                }
            }

            foreach (string materialName in RequiredMaterialNames)
            {
                if (AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/{materialName}.mat") == null)
                {
                    throw new MissingReferenceException($"Required Bash material is missing: {materialName}");
                }
            }

            foreach (string sharedTexturePath in new[]
            {
                ChargeHeavyDustTexturePath,
                ChargeFineDustTexturePath,
                ChargeGroundBurstTexturePath
            })
            {
                if (AssetDatabase.LoadAssetAtPath<Texture>(sharedTexturePath) == null)
                {
                    throw new MissingReferenceException($"Shared Warrior environmental texture is missing: {sharedTexturePath}");
                }
            }

            ValidateShader();
            ValidateWiring(prefab);
            ValidateLifecycle(prefab);
            Debug.Log("BashVFX validation passed: layered prefab, radial dust ring, procedural lifecycle, authored assets, and Warrior Bash wiring are valid.", prefab);
        }

        private static void ValidateShader()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null || !shader.isSupported)
            {
                throw new MissingReferenceException($"Bash VFX shader is missing or unsupported: {ShaderPath}");
            }

            foreach (ShaderMessage message in ShaderUtil.GetShaderMessages(shader))
            {
                if (message.severity == ShaderCompilerMessageSeverity.Error)
                {
                    throw new UnityException($"Shader error in {ShaderPath}: {message.message}");
                }
            }
        }

        private static void ValidateWiring(GameObject prefab)
        {
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(DefinitionPath);
            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AbilityPath);
            if (definition == null
                || ability == null
                || ability.VisualEffects != definition
                || definition.CastingPrefab != null
                || definition.CastPrefab != null
                || definition.HitPrefab != prefab
                || !definition.AttachHitToTarget
                || definition.CastPrefabControlsHitTiming)
            {
                throw new MissingReferenceException("Warrior Bash is not wired through its existing VFX definition to BashVFX.");
            }
        }

        private static void ValidateLifecycle(GameObject prefab)
        {
            GameObject instance = null;
            try
            {
                instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                instance.hideFlags = HideFlags.HideAndDontSave;
                BashVFX controller = instance.GetComponent<BashVFX>();
                System.Reflection.MethodInfo awake = typeof(BashVFX).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                awake?.Invoke(controller, null);

                controller.SetImpactDirection(Vector3.forward);
                controller.Play(true);
                if (!controller.IsPlaying || controller.ReadyForPool)
                {
                    throw new UnityException("BashVFX did not enter its playback state.");
                }

                controller.StopImmediate();
                if (controller.IsPlaying || !controller.ReadyForPool)
                {
                    throw new UnityException("BashVFX did not reset to a pool-ready state.");
                }

                controller.ResetForPool();
            }
            finally
            {
                if (instance != null)
                {
                    Object.DestroyImmediate(instance);
                }
            }
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/_Project/VFX", "Bash");
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
                string path = $"{TextureFolder}/{textureName}";
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                {
                    throw new MissingReferenceException($"Required Bash texture is missing: {path}");
                }

                importer.textureType = TextureImporterType.Default;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.sRGBTexture = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = textureName.Contains("Atlas") ? 1024 : 512;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
        }

        private static BashVFXProfile LoadOrCreateProfile()
        {
            BashVFXProfile profile = AssetDatabase.LoadAssetAtPath<BashVFXProfile>(ProfilePath);
            if (profile != null)
            {
                return profile;
            }

            profile = ScriptableObject.CreateInstance<BashVFXProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
            return profile;
        }

        private static Dictionary<string, Material> CreateMaterials()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null)
            {
                throw new MissingReferenceException($"Bash shader is missing: {ShaderPath}");
            }

            return new Dictionary<string, Material>
            {
                ["Swing"] = CreateMaterial("Bash_SwingAccent", shader, "Bash_BrushArc.png", new Color(1f, 0.92f, 0.72f, 0.72f), true, 1.05f),
                ["Backplate"] = CreateMaterial("Bash_ImpactBackplate", shader, "Bash_ImpactBurstAtlas.png", new Color(0.22f, 0.1f, 0.03f, 0.76f), false, 0.82f),
                ["Flash"] = CreateMaterial("Bash_ImpactFlash", shader, "Bash_ImpactFlash.png", Color.white, true, 1.35f),
                ["Burst"] = CreateMaterial("Bash_ImpactBurst", shader, "Bash_ImpactBurstAtlas.png", Color.white, true, 1.05f),
                ["Secondary"] = CreateMaterial("Bash_SecondaryBurst", shader, "Bash_ImpactBurstAtlas.png", new Color(1f, 0.36f, 0.06f, 0.82f), true, 0.92f),
                ["Dust"] = CreateMaterial("Bash_Dust", shader, "Bash_DustPuffAtlas.png", new Color(0.72f, 0.68f, 0.6f, 0.72f), false, 0.92f),
                ["DustRing"] = CreateMaterial("Bash_DustRing", shader, "Bash_DustRing.png", new Color(0.72f, 0.68f, 0.6f, 0.72f), false, 0.9f),
                ["Debris"] = CreateMaterial("Bash_Debris", shader, "Bash_ImpactBurstAtlas.png", new Color(0.42f, 0.34f, 0.25f, 0.9f), false, 0.72f),
                ["EnvironmentalGroundBurst"] = CreateMaterialFromPath("Bash_EnvironmentalGroundBurst", shader, ChargeGroundBurstTexturePath, Color.white, false, 0.9f),
                ["EnvironmentalHeavyDust"] = CreateMaterialFromPath("Bash_EnvironmentalHeavyDust", shader, ChargeHeavyDustTexturePath, Color.white, false, 0.92f),
                ["EnvironmentalFineDust"] = CreateMaterialFromPath("Bash_EnvironmentalFineDust", shader, ChargeFineDustTexturePath, Color.white, false, 0.94f),
                ["Sparks"] = CreateMaterial("Bash_Sparks", shader, "Bash_MetallicSpark.png", Color.white, true, 1.25f),
                ["Stun"] = CreateMaterial("Bash_StunStars", shader, "Bash_StunStar.png", Color.white, true, 1.08f)
            };
        }

        private static Material CreateMaterial(string name, Shader shader, string textureName, Color tint, bool additive, float brightness)
        {
            return CreateMaterialFromPath(name, shader, $"{TextureFolder}/{textureName}", tint, additive, brightness);
        }

        private static Material CreateMaterialFromPath(string name, Shader shader, string texturePath, Color tint, bool additive, float brightness)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(texturePath);
            if (texture == null)
            {
                throw new MissingReferenceException($"Bash material source texture is missing: {texturePath}");
            }

            material.SetTexture("_BaseMap", texture);
            material.SetColor("_Tint", tint);
            material.SetFloat("_Brightness", brightness);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreatePrefab(BashVFXProfile profile, IReadOnlyDictionary<string, Material> materials)
        {
            GameObject root = new("BashVFX");
            try
            {
                BashVFX controller = root.AddComponent<BashVFX>();
                Transform visualRoot = CreateSection("Visual Root", root.transform);
                Transform impactRoot = CreateSection("Impact Layers", visualRoot);
                Transform groundRoot = CreateSection("Ground Reaction", visualRoot);
                groundRoot.localPosition = new Vector3(0f, -0.8f, 0f);
                Transform stunRoot = CreateSection("Stun Accent", visualRoot);
                stunRoot.localPosition = new Vector3(0f, 0.9f, 0f);

                ParticleSystem swing = CreateSwingAccent(impactRoot, materials["Swing"]);
                ParticleSystem backplate = CreateImpactBackplate(impactRoot, materials["Backplate"]);
                ParticleSystem flash = CreateImpactFlash(impactRoot, materials["Flash"]);
                ParticleSystem burst = CreateImpactBurst(impactRoot, materials["Burst"]);
                ParticleSystem secondary = CreateSecondaryImpactBurst(impactRoot, materials["Secondary"]);
                ParticleSystem directional = CreateDirectionalBurst(impactRoot, materials["Burst"]);
                ParticleSystem momentum = CreateMomentumStreaks(impactRoot, materials["Sparks"]);
                ParticleSystem sparks = CreateArmorSparks(impactRoot, materials["Sparks"]);
                ParticleSystem dust = CreateDustBurst(groundRoot, materials["Dust"]);
                ParticleSystem ring = CreateDustRing(groundRoot, materials["DustRing"]);
                ParticleSystem debris = CreateGroundDebris(groundRoot, materials["Debris"]);
                ParticleSystem environmentalGroundBurst = CreateEnvironmentalGroundBurst(groundRoot, materials["EnvironmentalGroundBurst"]);
                ParticleSystem environmentalHeavyDust = CreateEnvironmentalHeavyDust(groundRoot, materials["EnvironmentalHeavyDust"]);
                ParticleSystem environmentalFineDust = CreateEnvironmentalFineDust(groundRoot, materials["EnvironmentalFineDust"]);
                ParticleSystem stars = CreateStunStars(stunRoot, materials["Stun"]);

                controller.ConfigureAuthoring(
                    profile,
                    visualRoot,
                    impactRoot,
                    groundRoot,
                    stunRoot,
                    swing,
                    backplate,
                    flash,
                    burst,
                    secondary,
                    directional,
                    momentum,
                    sparks,
                    dust,
                    ring,
                    debris,
                    environmentalGroundBurst,
                    environmentalHeavyDust,
                    environmentalFineDust,
                    stars);

                root.AddComponent<MMOAbilityVfxLifetime>().Configure(2.6f, true, true);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (prefab == null)
                {
                    throw new UnityException($"Failed to save Bash VFX prefab at {PrefabPath}.");
                }

                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void WireIntoBash(GameObject prefab)
        {
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(DefinitionPath);
            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AbilityPath);
            if (definition == null || ability == null)
            {
                throw new MissingReferenceException("Warrior Bash ability or VFX definition is missing.");
            }

            definition.Configure(
                null,
                null,
                prefab,
                true,
                false,
                true,
                true,
                new Vector3(0f, 1.15f, 0.42f),
                Vector3.zero,
                new Vector3(0f, 1.18f, 0.48f),
                new Vector3(0f, 0.85f, 0f),
                0.04f,
                false);
            ability.SetVisualEffects(definition);
            EditorUtility.SetDirty(definition);
            EditorUtility.SetDirty(ability);
        }

        private static Transform CreateSection(string name, Transform parent)
        {
            GameObject section = new(name);
            section.transform.SetParent(parent, false);
            return section.transform;
        }

        private static ParticleSystem CreateSwingAccent(Transform parent, Material material)
        {
            ParticleSystem system = CreateOneShot("Swing Accent", parent, material, 0.16f, 0f, 1.15f, 1, new Color(1f, 0.88f, 0.54f, 0.64f), 4);
            system.transform.localPosition = new Vector3(-0.08f, 0.06f, -0.08f);
            ParticleSystem.MainModule main = system.main;
            main.startRotation = -18f * Mathf.Deg2Rad;
            ConfigureFadeAndScale(system, 0.35f, 1f, 1.18f, 0.04f, 0.38f);
            return system;
        }

        private static ParticleSystem CreateImpactFlash(Transform parent, Material material)
        {
            ParticleSystem system = CreateOneShot("Contact Flash", parent, material, 0.12f, 0f, 0.9f, 1, Color.white, 8);
            ConfigureFadeAndScale(system, 0.18f, 1f, 1.32f, 0.02f, 0.18f);
            return system;
        }

        private static ParticleSystem CreateImpactBackplate(Transform parent, Material material)
        {
            ParticleSystem system = CreateOneShot("Dark Impact Backplate", parent, material, 0.3f, 0.62f, 1.48f, 5, new Color(0.22f, 0.1f, 0.025f, 0.74f), 3);
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.055f;
            ParticleSystem.RotationOverLifetimeModule rotation = system.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-0.85f, 0.85f);
            ConfigureAtlas(system, 2, 2);
            ConfigureFadeAndScale(system, 0.5f, 1.08f, 1.2f, 0f, 0.34f);
            return system;
        }

        private static ParticleSystem CreateImpactBurst(Transform parent, Material material)
        {
            ParticleSystem system = CreateOneShot("Heavy Impact Burst", parent, material, 0.34f, 1.15f, 1.35f, 9, new Color(1f, 0.78f, 0.36f, 0.92f), 6);
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.08f;
            ParticleSystem.RotationOverLifetimeModule rotation = system.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-1.6f, 1.6f);
            ConfigureAtlas(system, 2, 2);
            ConfigureFadeAndScale(system, 0.42f, 1f, 1.14f, 0f, 0.28f);
            return system;
        }

        private static ParticleSystem CreateDirectionalBurst(Transform parent, Material material)
        {
            ParticleSystem system = CreateOneShot("Directional Force Burst", parent, material, 0.3f, 4.2f, 0.68f, 5, new Color(1f, 0.75f, 0.34f, 0.72f), 5);
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = 0.04f;
            shape.length = 0.08f;
            ParticleSystem.LimitVelocityOverLifetimeModule drag = system.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.dampen = 0.36f;
            ConfigureAtlas(system, 2, 2);
            ConfigureFadeAndScale(system, 0.32f, 0.9f, 0.18f, 0f, 0.25f);
            return system;
        }

        private static ParticleSystem CreateSecondaryImpactBurst(Transform parent, Material material)
        {
            ParticleSystem system = CreateOneShot("Secondary Orange Impact", parent, material, 0.3f, 1.45f, 1.62f, 5, new Color(1f, 0.4f, 0.08f, 0.8f), 5);
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;
            ParticleSystem.RotationOverLifetimeModule rotation = system.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-1.1f, 1.1f);
            ConfigureAtlas(system, 2, 2);
            ConfigureFadeAndScale(system, 0.34f, 1.08f, 1.24f, 0f, 0.3f);
            return system;
        }

        private static ParticleSystem CreateMomentumStreaks(Transform parent, Material material)
        {
            ParticleSystem system = CreateOneShot("Forward Momentum Streaks", parent, material, 0.24f, 7.2f, 0.115f, 6, Color.white, 8);
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 11f;
            shape.radius = 0.055f;
            shape.length = 0.04f;
            ParticleSystem.LimitVelocityOverLifetimeModule drag = system.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.dampen = 0.22f;
            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 2.6f;
            renderer.velocityScale = 0.16f;
            renderer.cameraVelocityScale = 0f;
            ConfigureFadeAndScale(system, 0.78f, 1f, 0f, 0f, 0.46f);
            return system;
        }

        private static ParticleSystem CreateArmorSparks(Transform parent, Material material)
        {
            ParticleSystem system = CreateOneShot("Armor Sparks", parent, material, 0.34f, 5.8f, 0.16f, 6, Color.white, 9);
            ParticleSystem.MainModule main = system.main;
            main.gravityModifier = 0.72f;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.06f;
            ParticleSystem.LimitVelocityOverLifetimeModule drag = system.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.dampen = 0.28f;
            ConfigureFadeAndScale(system, 1f, 0.82f, 0f, 0f, 0.42f);
            return system;
        }

        private static ParticleSystem CreateDustBurst(Transform parent, Material material)
        {
            ParticleSystem system = CreateOneShot("Dust Puff Burst", parent, material, 0.48f, 0.9f, 0.52f, 8, new Color(0.68f, 0.64f, 0.56f, 0.66f), 2);
            system.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 68f;
            shape.radius = 0.25f;
            shape.radiusThickness = 0.72f;
            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.z = new ParticleSystem.MinMaxCurve(0.05f, 0.28f);
            ConfigureAtlas(system, 2, 2);
            ConfigureFadeAndScale(system, 0.42f, 1.08f, 1.36f, 0f, 0.28f);
            return system;
        }

        private static ParticleSystem CreateDustRing(Transform parent, Material material)
        {
            ParticleSystem system = CreateOneShot("Radial Dust Ring", parent, material, 0.42f, 0f, 2.25f, 1, new Color(0.7f, 0.66f, 0.58f, 0.62f), 1);
            system.transform.localPosition = new Vector3(0f, 0.025f, 0f);
            system.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.alignment = ParticleSystemRenderSpace.Local;
            renderer.allowRoll = false;
            ConfigureFadeAndScale(system, 0.24f, 1f, 1.08f, 0f, 0.2f);
            return system;
        }

        private static ParticleSystem CreateGroundDebris(Transform parent, Material material)
        {
            ParticleSystem system = CreateOneShot("Ground Debris Chunks", parent, material, 0.44f, 2.35f, 0.18f, 6, new Color(0.4f, 0.33f, 0.24f, 0.86f), 3);
            system.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            ParticleSystem.MainModule main = system.main;
            main.gravityModifier = 1.45f;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 62f;
            shape.radius = 0.18f;
            shape.radiusThickness = 0.8f;
            ParticleSystem.RotationOverLifetimeModule rotation = system.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-3.2f, 3.2f);
            ConfigureAtlas(system, 2, 2);
            ConfigureFadeAndScale(system, 0.84f, 1f, 0.3f, 0f, 0.68f);
            return system;
        }

        private static ParticleSystem CreateEnvironmentalGroundBurst(Transform parent, Material material)
        {
            ParticleSystem system = CreateOneShot(
                "Environmental Ground Burst",
                parent,
                material,
                0.46f,
                2.4f,
                1.72f,
                8,
                Color.white,
                4);
            ParticleSystem.MainModule main = system.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.12f;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.14f;
            shape.scale = new Vector3(1f, 0.22f, 1f);
            ParticleSystem.RotationOverLifetimeModule rotation = system.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-1.4f, 1.4f);
            ConfigureAtlas(system, 4, 1);
            ConfigureFadeAndScale(system, 0.48f, 1f, 0.58f, 0f, 0.52f);
            return system;
        }

        private static ParticleSystem CreateEnvironmentalHeavyDust(Transform parent, Material material)
        {
            ParticleSystem system = CreateOneShot(
                "Environmental Heavy Dust",
                parent,
                material,
                1.65f,
                1.15f,
                0.9f,
                18,
                Color.white,
                2);
            ConfigureEnvironmentalDust(system, 4, 2, 0.3f, 0.12f, 0.62f);
            return system;
        }

        private static ParticleSystem CreateEnvironmentalFineDust(Transform parent, Material material)
        {
            ParticleSystem system = CreateOneShot(
                "Environmental Fine Dust",
                parent,
                material,
                2.25f,
                0.55f,
                0.55f,
                12,
                Color.white,
                3);
            ConfigureEnvironmentalDust(system, 4, 2, 0.24f, 0.58f, 0.7f);
            return system;
        }

        private static void ConfigureEnvironmentalDust(
            ParticleSystem system,
            int columns,
            int rows,
            float radius,
            float riseSpeed,
            float startScale)
        {
            ParticleSystem.MainModule main = system.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = radius;
            shape.scale = new Vector3(1f, 0.24f, 1f);
            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = riseSpeed;
            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.28f;
            noise.frequency = 0.42f;
            noise.scrollSpeed = 0.18f;
            ConfigureAtlas(system, columns, rows);
            ConfigureFadeAndScale(system, startScale, 1f, 1.2f, 0.012f, 0.62f);
        }

        private static ParticleSystem CreateStunStars(Transform parent, Material material)
        {
            ParticleSystem system = CreateOneShot("Orbiting Stun Stars", parent, material, 1.15f, 0f, 0.2f, 5, new Color(1f, 0.88f, 0.28f, 0.84f), 10);
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.38f;
            shape.radiusThickness = 0f;
            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.allowRoll = false;
            ConfigureFadeAndScale(system, 0.7f, 1f, 0.72f, 0.08f, 0.82f);
            return system;
        }

        private static ParticleSystem CreateOneShot(
            string name,
            Transform parent,
            Material material,
            float lifetime,
            float speed,
            float size,
            int count,
            Color color,
            int sortingOrder)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            ParticleSystem system = child.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = system.main;
            main.duration = Mathf.Max(0.1f, lifetime);
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.72f, size * 1.18f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            main.startColor = color;
            main.maxParticles = Mathf.Max(1, count);
            main.stopAction = ParticleSystemStopAction.None;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Max(0, count)) });
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;

            ParticleSystemRenderer renderer = child.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = sortingOrder;
            renderer.enableGPUInstancing = true;
            return system;
        }

        private static void ConfigureAtlas(ParticleSystem system, int columns, int rows)
        {
            ParticleSystem.TextureSheetAnimationModule sheet = system.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.numTilesX = columns;
            sheet.numTilesY = rows;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 0.999f);
            sheet.cycleCount = 1;
        }

        private static void ConfigureFadeAndScale(
            ParticleSystem system,
            float startScale,
            float middleScale,
            float endScale,
            float fadeInEnd,
            float fadeOutStart)
        {
            ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
            color.enabled = true;
            color.color = CreateAlphaGradient(fadeInEnd, fadeOutStart);

            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, startScale),
                new Keyframe(0.4f, middleScale),
                new Keyframe(1f, endScale)));
        }

        private static Gradient CreateAlphaGradient(float fadeInEnd, float fadeOutStart)
        {
            float inEnd = Mathf.Clamp(fadeInEnd, 0.01f, 0.45f);
            float outStart = Mathf.Clamp(fadeOutStart, inEnd + 0.01f, 0.98f);
            Gradient gradient = new();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, inEnd),
                    new GradientAlphaKey(1f, outStart),
                    new GradientAlphaKey(0f, 1f)
                });
            return gradient;
        }
    }
}
