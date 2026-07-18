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
    public static class ChargeVFXInstaller
    {
        private const string RootFolder = "Assets/_Project/VFX/Charge";
        private const string TextureFolder = RootFolder + "/Textures";
        private const string ShaderFolder = RootFolder + "/Shaders";
        private const string MaterialFolder = RootFolder + "/Materials";
        private const string ProfileFolder = RootFolder + "/Profiles";
        private const string PrefabFolder = RootFolder + "/Prefabs";
        private const string ProfilePath = ProfileFolder + "/ChargeVFX_Default.asset";
        private const string PrefabPath = PrefabFolder + "/ChargeVFX.prefab";
        private const string ShaderPath = ShaderFolder + "/ChargeSpriteUnlit.shader";
        private const string DefinitionPath = "Assets/_Project/VFX/Definitions/Warrior_Charge_VFX.asset";
        private const string AbilityPath = "Assets/_Project/Configs/Abilities/Warrior_Charge.asset";

        private static readonly IReadOnlyDictionary<string, Vector2Int> RequiredTextures =
            new Dictionary<string, Vector2Int>
            {
                ["Charge_HeavyDustAtlas.png"] = new(4, 2),
                ["Charge_FineDustAtlas.png"] = new(4, 2),
                ["Charge_DirtChunksAtlas.png"] = new(4, 1),
                ["Charge_RocksAtlas.png"] = new(4, 1),
                ["Charge_ShockwavesAtlas.png"] = new(4, 1),
                ["Charge_AirCompressionAtlas.png"] = new(4, 1),
                ["Charge_SpeedStreaksAtlas.png"] = new(4, 1),
                ["Charge_ImpactShardsAtlas.png"] = new(4, 1),
                ["Charge_GroundBurstAtlas.png"] = new(4, 1),
                ["Charge_ImpactFlashAtlas.png"] = new(2, 1),
                ["Charge_MetallicGlintsAtlas.png"] = new(2, 1)
            };

        private static readonly string[] RequiredMaterials =
        {
            "Charge_HeavyDust", "Charge_FineDust", "Charge_DirtDebris", "Charge_Rocks",
            "Charge_Shockwaves", "Charge_AirCompression", "Charge_SpeedStreaks",
            "Charge_ImpactBursts", "Charge_GroundBursts", "Charge_MetallicGlints", "Charge_ContactFlash"
        };

        [MenuItem("Tools/RPG Clone/VFX/Build Charge VFX")]
        public static void Build()
        {
            EnsureFolders();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureTextureImporters();
            ChargeVFXProfile profile = LoadOrCreateProfile();
            Dictionary<string, Material> materials = CreateMaterials();
            GameObject prefab = CreatePrefab(profile, materials);
            WireAbility(prefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = prefab;
            Debug.Log($"Built reusable ChargeVFX and wired Warrior Charge at '{PrefabPath}'.", prefab);
        }

        [MenuItem("Tools/RPG Clone/VFX/Validate Charge VFX")]
        public static void ValidateBuild()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                throw new MissingReferenceException($"Charge prefab is missing at {PrefabPath}.");
            }

            ChargeVFX controller = prefab.GetComponent<ChargeVFX>();
            if (controller == null || controller.Profile == null)
            {
                throw new MissingReferenceException("ChargeVFX controller or profile is missing.");
            }

            foreach (string section in new[] { "Launch (World Space)", "Travel Trail (World Space)", "Motion (Character Space)", "Collision (World Space)" })
            {
                if (prefab.transform.Find(section) == null)
                {
                    throw new MissingReferenceException($"ChargeVFX section is missing: {section}");
                }
            }

            ParticleSystem[] particles = prefab.GetComponentsInChildren<ParticleSystem>(true);
            if (particles.Length != 22)
            {
                throw new UnityException($"ChargeVFX must contain 22 bounded particle layers; found {particles.Length}.");
            }

            int worldLayers = 0;
            int localLayers = 0;
            foreach (ParticleSystem particle in particles)
            {
                if (particle.main.simulationSpace == ParticleSystemSimulationSpace.World)
                {
                    worldLayers++;
                }
                else if (particle.main.simulationSpace == ParticleSystemSimulationSpace.Local)
                {
                    localLayers++;
                }
            }

            if (worldLayers != 19 || localLayers != 3)
            {
                throw new UnityException($"ChargeVFX space separation is invalid: expected 19 world layers and 3 local layers, found {worldLayers}/{localLayers}.");
            }

            if (prefab.GetComponentsInChildren<Light>(true).Length != 0
                || prefab.GetComponentsInChildren<Animator>(true).Length != 0
                || prefab.GetComponentsInChildren<UnityEngine.Animation>(true).Length != 0)
            {
                throw new UnityException("ChargeVFX must remain procedural, light-free, and animator-free.");
            }

            foreach (string texture in RequiredTextures.Keys)
            {
                if (AssetDatabase.LoadAssetAtPath<Texture>($"{TextureFolder}/{texture}") == null)
                {
                    throw new MissingReferenceException($"Charge texture is missing: {texture}");
                }
            }

            foreach (string material in RequiredMaterials)
            {
                if (AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/{material}.mat") == null)
                {
                    throw new MissingReferenceException($"Charge material is missing: {material}");
                }
            }

            ValidateShader();
            ValidateWiring(prefab);
            ValidateLifecycle(prefab);
            Debug.Log("ChargeVFX validation passed: assets, world/local simulation separation, procedural layers, prefab, profile, and ability wiring are valid.", prefab);
        }

        private static void ValidateShader()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null || !shader.isSupported)
            {
                throw new MissingReferenceException($"Charge shader is missing or unsupported: {ShaderPath}");
            }

            foreach (ShaderMessage message in ShaderUtil.GetShaderMessages(shader))
            {
                if (message.severity == ShaderCompilerMessageSeverity.Error)
                {
                    throw new UnityException($"Charge shader error: {message.message}");
                }
            }
        }

        private static void ValidateWiring(GameObject prefab)
        {
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(DefinitionPath);
            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AbilityPath);
            if (definition == null || ability == null || ability.VisualEffects != definition
                || definition.CastingPrefab != null || definition.CastPrefab != prefab || definition.HitPrefab != null
                || definition.AttachHitToTarget || !definition.AlignCastPrefabToTarget || definition.CastPrefabControlsHitTiming)
            {
                throw new MissingReferenceException("Warrior Charge is not wired to the unified ChargeVFX cast prefab.");
            }
        }

        private static void ValidateLifecycle(GameObject prefab)
        {
            GameObject instance = null;
            try
            {
                instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (instance == null)
                {
                    throw new UnityException("ChargeVFX could not be instantiated for lifecycle validation.");
                }

                instance.hideFlags = HideFlags.HideAndDontSave;
                ChargeVFX controller = instance.GetComponent<ChargeVFX>();
                System.Reflection.MethodInfo awake = typeof(ChargeVFX).GetMethod(
                    "Awake",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                awake?.Invoke(controller, null);
                controller.Initialize(new MMOAbilityVfxContext(
                    null,
                    null,
                    null,
                    instance.transform,
                    null,
                    Vector3.zero,
                    Vector3.forward,
                    false,
                    null));
                if (!controller.IsPlaying || controller.IsRecovering)
                {
                    throw new UnityException("ChargeVFX did not enter its travel playback state.");
                }

                controller.StopImmediate();
                if (controller.IsPlaying || controller.IsRecovering)
                {
                    throw new UnityException("ChargeVFX did not return to its stopped state.");
                }
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
            EnsureFolder("Assets/_Project/VFX", "Charge");
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
            foreach (KeyValuePair<string, Vector2Int> texture in RequiredTextures)
            {
                string path = $"{TextureFolder}/{texture.Key}";
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                {
                    throw new MissingReferenceException($"Required Charge texture is missing: {path}");
                }

                importer.textureType = TextureImporterType.Default;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.sRGBTexture = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = texture.Value.y > 1 ? 2048 : 1024;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
        }

        private static ChargeVFXProfile LoadOrCreateProfile()
        {
            ChargeVFXProfile profile = AssetDatabase.LoadAssetAtPath<ChargeVFXProfile>(ProfilePath);
            if (profile != null)
            {
                return profile;
            }

            profile = ScriptableObject.CreateInstance<ChargeVFXProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
            return profile;
        }

        private static Dictionary<string, Material> CreateMaterials()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null)
            {
                throw new MissingReferenceException($"Charge shader is missing: {ShaderPath}");
            }

            return new Dictionary<string, Material>
            {
                ["HeavyDust"] = CreateMaterial("Charge_HeavyDust", shader, "Charge_HeavyDustAtlas.png", new Color(0.68f, 0.53f, 0.36f, 0.76f), false, 0.9f),
                ["FineDust"] = CreateMaterial("Charge_FineDust", shader, "Charge_FineDustAtlas.png", new Color(0.86f, 0.75f, 0.56f, 0.52f), false, 0.92f),
                ["Dirt"] = CreateMaterial("Charge_DirtDebris", shader, "Charge_DirtChunksAtlas.png", new Color(0.38f, 0.25f, 0.14f, 0.94f), false, 0.82f),
                ["Rocks"] = CreateMaterial("Charge_Rocks", shader, "Charge_RocksAtlas.png", new Color(0.44f, 0.43f, 0.4f, 0.96f), false, 0.82f),
                ["Shockwave"] = CreateMaterial("Charge_Shockwaves", shader, "Charge_ShockwavesAtlas.png", new Color(0.95f, 0.82f, 0.62f, 0.58f), true, 0.92f),
                ["Air"] = CreateMaterial("Charge_AirCompression", shader, "Charge_AirCompressionAtlas.png", new Color(1f, 0.94f, 0.82f, 0.3f), true, 0.84f),
                ["Streak"] = CreateMaterial("Charge_SpeedStreaks", shader, "Charge_SpeedStreaksAtlas.png", new Color(1f, 0.9f, 0.7f, 0.5f), true, 1f),
                ["Impact"] = CreateMaterial("Charge_ImpactBursts", shader, "Charge_ImpactShardsAtlas.png", new Color(1f, 0.68f, 0.3f, 0.88f), true, 1.08f),
                ["GroundBurst"] = CreateMaterial("Charge_GroundBursts", shader, "Charge_GroundBurstAtlas.png", new Color(0.72f, 0.48f, 0.25f, 0.86f), false, 0.9f),
                ["Glint"] = CreateMaterial("Charge_MetallicGlints", shader, "Charge_MetallicGlintsAtlas.png", new Color(1f, 0.95f, 0.78f, 0.84f), true, 1.24f),
                ["Flash"] = CreateMaterial("Charge_ContactFlash", shader, "Charge_ImpactFlashAtlas.png", Color.white, true, 1.38f)
            };
        }

        private static Material CreateMaterial(string name, Shader shader, string textureName, Color tint, bool additive, float brightness)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            Texture texture = AssetDatabase.LoadAssetAtPath<Texture>($"{TextureFolder}/{textureName}");
            if (texture == null)
            {
                throw new MissingReferenceException($"Charge material source is missing: {textureName}");
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

        private static GameObject CreatePrefab(ChargeVFXProfile profile, IReadOnlyDictionary<string, Material> materials)
        {
            GameObject root = new("ChargeVFX");
            try
            {
                ChargeVFX controller = root.AddComponent<ChargeVFX>();
                Transform launchRoot = CreateSection("Launch (World Space)", root.transform);
                Transform trailRoot = CreateSection("Travel Trail (World Space)", root.transform);
                Transform motionRoot = CreateSection("Motion (Character Space)", root.transform);
                motionRoot.localPosition = new Vector3(0f, 1.05f, -0.12f);
                Transform impactRoot = CreateSection("Collision (World Space)", root.transform);

                ParticleSystem[] launch =
                {
                    CreateDust("Launch Heavy Dust", launchRoot, materials["HeavyDust"], true, 4, 2, 0),
                    CreateDust("Launch Fine Dust", launchRoot, materials["FineDust"], true, 4, 2, 1),
                    CreateDebris("Launch Dirt Chunks", launchRoot, materials["Dirt"], true, 4, 1, 2),
                    CreateDebris("Launch Rocks", launchRoot, materials["Rocks"], true, 4, 1, 3),
                    CreateShockwave("Launch Ground Shockwave", launchRoot, materials["Shockwave"], 4, 1, 4)
                };

                ParticleSystem[] trail =
                {
                    CreateDust("Heavy Dust Events", trailRoot, materials["HeavyDust"], true, 4, 2, 0),
                    CreateDust("Fine Dust Events", trailRoot, materials["FineDust"], true, 4, 2, 1),
                    CreateDebris("Trail Dirt Chunks", trailRoot, materials["Dirt"], true, 4, 1, 2),
                    CreateDust("Ground Scrape Dust", trailRoot, materials["HeavyDust"], true, 4, 2, 0),
                    CreateDebris("Ground Scrape Debris", trailRoot, materials["Dirt"], true, 4, 1, 2)
                };

                ParticleSystem[] attached =
                {
                    CreateAttached("Speed Streaks", motionRoot, materials["Streak"], 4, 1, ParticleSystemRenderMode.Stretch, 8),
                    CreateAttached("Air Compression", motionRoot, materials["Air"], 4, 1, ParticleSystemRenderMode.Billboard, 7),
                    CreateAttached("Armor Glints", motionRoot, materials["Glint"], 2, 1, ParticleSystemRenderMode.Billboard, 9)
                };
                attached[0].transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

                ParticleSystem[] impact =
                {
                    CreateImpact("Contact Flash", impactRoot, materials["Flash"], 2, 1, 10),
                    CreateDust("Impact Heavy Dust", impactRoot, materials["HeavyDust"], true, 4, 2, 1),
                    CreateDust("Impact Fine Dust", impactRoot, materials["FineDust"], true, 4, 2, 2),
                    CreateImpact("Broad Ground Burst", impactRoot, materials["GroundBurst"], 4, 1, 5),
                    CreateImpact("Painterly Impact Shards", impactRoot, materials["Impact"], 4, 1, 6),
                    CreateDebris("Impact Dirt Debris", impactRoot, materials["Dirt"], true, 4, 1, 4),
                    CreateDebris("Impact Rocks", impactRoot, materials["Rocks"], true, 4, 1, 5),
                    CreateShockwave("Impact Ground Shockwave", impactRoot, materials["Shockwave"], 4, 1, 3),
                    CreateDust("Recovery Dust", impactRoot, materials["FineDust"], true, 4, 2, 0)
                };

                controller.ConfigureAuthoring(profile, launch, trail, attached, impact);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (prefab == null)
                {
                    throw new UnityException($"Failed to save ChargeVFX at {PrefabPath}.");
                }

                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void WireAbility(GameObject prefab)
        {
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(DefinitionPath);
            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AbilityPath);
            if (definition == null || ability == null)
            {
                throw new MissingReferenceException("Warrior Charge ability or VFX definition is missing.");
            }

            definition.Configure(
                null,
                prefab,
                null,
                true,
                false,
                false,
                true,
                new Vector3(0f, 0.06f, 0f),
                Vector3.zero,
                new Vector3(0f, 0.06f, 0f),
                Vector3.zero,
                0f,
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

        private static ParticleSystem CreateDust(string name, Transform parent, Material material, bool worldSpace, int columns, int rows, int order)
        {
            ParticleSystem system = CreateBase(name, parent, material, worldSpace, order, 256);
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.28f;
            shape.scale = new Vector3(1f, 0.25f, 1f);
            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.28f;
            noise.frequency = 0.42f;
            noise.scrollSpeed = 0.18f;
            ConfigureAtlas(system, columns, rows);
            ConfigureFadeAndScale(system, 0.62f, 1f, 1.2f, 0.012f, 0.62f);
            return system;
        }

        private static ParticleSystem CreateDebris(string name, Transform parent, Material material, bool worldSpace, int columns, int rows, int order)
        {
            ParticleSystem system = CreateBase(name, parent, material, worldSpace, order, 128);
            ParticleSystem.MainModule main = system.main;
            main.gravityModifier = 1.55f;
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 58f;
            shape.radius = 0.16f;
            ParticleSystem.RotationOverLifetimeModule rotation = system.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
            ConfigureAtlas(system, columns, rows);
            ConfigureFadeAndScale(system, 0.8f, 1f, 0.72f, 0.02f, 0.82f);
            return system;
        }

        private static ParticleSystem CreateShockwave(string name, Transform parent, Material material, int columns, int rows, int order)
        {
            ParticleSystem system = CreateBase(name, parent, material, true, order, 8);
            ParticleSystem.MainModule main = system.main;
            main.startRotation3D = true;
            main.startRotationX = Mathf.PI * 0.5f;
            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            ConfigureAtlas(system, columns, rows);
            ConfigureFadeAndScale(system, 0.12f, 0.78f, 1f, 0.01f, 0.48f);
            return system;
        }

        private static ParticleSystem CreateAttached(string name, Transform parent, Material material, int columns, int rows, ParticleSystemRenderMode mode, int order)
        {
            ParticleSystem system = CreateBase(name, parent, material, false, order, 96);
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.duration = 1f;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = 8f;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.9f, 1.45f, 0.6f);
            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = mode;
            renderer.lengthScale = mode == ParticleSystemRenderMode.Stretch ? 2.2f : 1f;
            renderer.velocityScale = mode == ParticleSystemRenderMode.Stretch ? 0.22f : 0f;
            ConfigureAtlas(system, columns, rows);
            ConfigureFadeAndScale(system, 0.72f, 1f, 0.35f, 0.018f, 0.56f);
            return system;
        }

        private static ParticleSystem CreateImpact(string name, Transform parent, Material material, int columns, int rows, int order)
        {
            ParticleSystem system = CreateBase(name, parent, material, true, order, 128);
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.16f;
            ConfigureAtlas(system, columns, rows);
            ConfigureFadeAndScale(system, 0.48f, 1f, 0.58f, 0.008f, 0.52f);
            return system;
        }

        private static ParticleSystem CreateBase(string name, Transform parent, Material material, bool worldSpace, int order, int maxParticles)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            ParticleSystem system = child.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = system.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = worldSpace ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startLifetime = 1f;
            main.startSpeed = 1f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.72f, 1.18f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            main.startColor = Color.white;
            main.maxParticles = maxParticles;
            main.stopAction = ParticleSystemStopAction.None;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;
            emission.rateOverTime = 0f;
            ParticleSystemRenderer renderer = child.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = order;
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

        private static void ConfigureFadeAndScale(ParticleSystem system, float startScale, float middleScale, float endScale, float fadeInEnd, float fadeOutStart)
        {
            ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
            color.enabled = true;
            Gradient gradient = new();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(fadeInEnd > 0.02f ? 0f : 1f, 0f),
                    new GradientAlphaKey(1f, Mathf.Max(0.02f, fadeInEnd)),
                    new GradientAlphaKey(1f, fadeOutStart),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = gradient;

            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, startScale),
                new Keyframe(0.42f, middleScale),
                new Keyframe(1f, endScale)));
        }
    }
}
