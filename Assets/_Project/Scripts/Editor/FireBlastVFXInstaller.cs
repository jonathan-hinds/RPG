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
    public static class FireBlastVFXInstaller
    {
        private const string RootFolder = "Assets/_Project/VFX/FireBlast";
        private const string TextureFolder = RootFolder + "/Textures";
        private const string ShaderFolder = RootFolder + "/Shaders";
        private const string MaterialFolder = RootFolder + "/Materials";
        private const string ProfileFolder = RootFolder + "/Profiles";
        private const string PrefabFolder = RootFolder + "/Prefabs";
        private const string ProfilePath = ProfileFolder + "/FireBlastVFX_Default.asset";
        private const string PrefabPath = PrefabFolder + "/FireBlastVFX.prefab";
        private const string DefinitionPath = "Assets/_Project/VFX/Definitions/Mage_Fire_Blast_VFX.asset";
        private const string AbilityPath = "Assets/_Project/Configs/Abilities/Mage_Fire_Blast.asset";

        private static readonly string[] RequiredTextureNames =
        {
            "FireBlast_Core.png",
            "FireBlast_FlameAtlas.png",
            "FireBlast_Streak.png",
            "FireBlast_HeatRing.png",
            "FireBlast_EmberAtlas.png",
            "FireBlast_SparkAtlas.png",
            "FireBlast_SmokeAtlas.png"
        };

        [MenuItem("Tools/RPG Clone/VFX/Build Fire Blast VFX")]
        public static void Build()
        {
            EnsureFolders();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureTextureImporters();

            FireBlastVFXProfile profile = LoadOrCreateProfile();
            profile.UpgradePolishDefaults();
            EditorUtility.SetDirty(profile);
            Dictionary<string, Material> materials = CreateMaterials();
            GameObject prefab = CreatePrefab(profile, materials);
            WireIntoMageFireBlast(prefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log($"Built non-projectile FireBlastVFX and wired Mage Fire Blast at '{PrefabPath}'.", prefab);
        }

        [MenuItem("Tools/RPG Clone/VFX/Validate Fire Blast VFX")]
        public static void ValidateBuild()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                throw new MissingReferenceException($"Fire Blast prefab is missing at {PrefabPath}.");
            }

            foreach (string section in new[] { "Casting Flash", "Instant Fire Streak", "Target Combustion", "Target Combustion/Aftermath" })
            {
                if (prefab.transform.Find(section) == null)
                {
                    throw new MissingReferenceException($"FireBlastVFX is missing its '{section}' section.");
                }
            }

            FireBlastVFX controller = prefab.GetComponent<FireBlastVFX>();
            if (controller == null || controller.Profile == null)
            {
                throw new MissingReferenceException("FireBlastVFX controller or profile reference is missing.");
            }

            if (prefab.GetComponent<MMOAbilityVfxProjectile>() != null
                || prefab.GetComponentsInChildren<MMOAbilityVfxProjectile>(true).Length != 0)
            {
                throw new UnityException("Fire Blast is target combustion and must never contain a projectile component.");
            }

            if (prefab.GetComponentsInChildren<Light>(true).Length != 0
                || prefab.GetComponentsInChildren<Animator>(true).Length != 0
                || prefab.GetComponentsInChildren<UnityEngine.Animation>(true).Length != 0)
            {
                throw new UnityException("FireBlastVFX must remain procedural and light-free.");
            }

            if (prefab.GetComponentsInChildren<LineRenderer>(true).Length != 1
                || prefab.GetComponentsInChildren<ParticleSystem>(true).Length != 7)
            {
                throw new UnityException("FireBlastVFX must contain one instant ribbon and seven budgeted particle systems.");
            }

            if (AssetDatabase.FindAssets("t:Material", new[] { MaterialFolder }).Length != 11)
            {
                throw new UnityException("FireBlastVFX must contain exactly eleven independently tunable materials.");
            }

            foreach (string layerPath in new[]
                     {
                         "Target Combustion/Compression Flash",
                         "Target Combustion/Outer Combustion",
                         "Target Combustion/Secondary Heat Ring",
                         "Target Combustion/Aftermath/Lingering Flame Bloom"
                     })
            {
                if (prefab.transform.Find(layerPath) == null)
                {
                    throw new MissingReferenceException($"Polished FireBlastVFX layer is missing: {layerPath}");
                }
            }

            foreach (string textureName in RequiredTextureNames)
            {
                if (AssetDatabase.LoadAssetAtPath<Texture>($"{TextureFolder}/{textureName}") == null)
                {
                    throw new MissingReferenceException($"Required Fire Blast texture is missing: {textureName}");
                }
            }

            ValidateShader();
            ValidateWiring(prefab);
            ValidateLifecycle(prefab);
            Debug.Log("FireBlastVFX validation passed: single non-projectile sequence, procedural layers, assets, lifecycle, and Mage Fire Blast wiring are valid.", prefab);
        }

        [MenuItem("Tools/RPG Clone/VFX/Stage Fire Blast Play Mode Preview")]
        public static void StagePlayModePreview()
        {
            StagePlayModePreview(0.085f, "impact");
        }

        [MenuItem("Tools/RPG Clone/VFX/Stage Fire Blast Lingering Preview")]
        public static void StageLingeringPlayModePreview()
        {
            StagePlayModePreview(0.32f, "lingering-burn");
        }

        private static void StagePlayModePreview(float previewTime, string frameName)
        {
            if (!EditorApplication.isPlaying)
            {
                throw new UnityException("Enter and pause Play Mode before staging the Fire Blast preview.");
            }

            Camera camera = Camera.main;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (camera == null || prefab == null)
            {
                throw new MissingReferenceException("A Main Camera and the FireBlastVFX prefab are required for preview staging.");
            }

            GameObject existing = GameObject.Find("FireBlastVFX Play Mode Preview");
            if (existing != null) Object.DestroyImmediate(existing);
            GameObject instance = Object.Instantiate(prefab);
            instance.name = "FireBlastVFX Play Mode Preview";
            FireBlastVFX controller = instance.GetComponent<FireBlastVFX>();
            Vector3 center = camera.transform.position + camera.transform.forward * 8f;
            Vector3 source = center - camera.transform.right * 2.2f;
            Vector3 target = center + camera.transform.right * 1.2f;
            controller.Play(source, target);

            foreach (ParticleSystem particles in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                particles.Simulate(previewTime, true, true, true);
            }

            System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(FireBlastVFX).GetField("cachedCamera", flags)?.SetValue(controller, camera);
            typeof(FireBlastVFX).GetMethod("UpdateAttachmentPositions", flags)?.Invoke(controller, null);
            typeof(FireBlastVFX).GetMethod("Animate", flags)?.Invoke(controller, new object[] { previewTime });
            EditorApplication.isPaused = true;
            Selection.activeObject = instance;
            Debug.Log($"Staged FireBlastVFX on its {frameName} frame in front of the Main Camera.", instance);
        }

        private static void ValidateShader()
        {
            string path = $"{ShaderFolder}/FireBlastSpriteUnlit.shader";
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (shader == null || !shader.isSupported)
            {
                throw new MissingReferenceException($"Fire Blast VFX shader is missing or unsupported: {path}");
            }

            foreach (ShaderMessage message in ShaderUtil.GetShaderMessages(shader))
            {
                if (message.severity == ShaderCompilerMessageSeverity.Error)
                {
                    throw new UnityException($"Shader error in {path}: {message.message}");
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
                || definition.CastPrefab != prefab
                || definition.HitPrefab != null
                || definition.CastPrefabControlsHitTiming)
            {
                throw new MissingReferenceException("Mage Fire Blast is not wired to the single complete target-combustion prefab.");
            }
        }

        private static void ValidateLifecycle(GameObject prefab)
        {
            GameObject instance = null;
            try
            {
                instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                instance.hideFlags = HideFlags.HideAndDontSave;
                FireBlastVFX controller = instance.GetComponent<FireBlastVFX>();
                System.Reflection.MethodInfo awake = typeof(FireBlastVFX).GetMethod(
                    "Awake",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                awake?.Invoke(controller, null);
                controller.Play(Vector3.zero, new Vector3(0f, 1f, 8f));

                LineRenderer line = instance.GetComponentInChildren<LineRenderer>(true);
                if (!controller.IsPlaying || controller.ReadyForPool || line.positionCount != 2)
                {
                    throw new UnityException("FireBlastVFX did not start its immediate source-to-target sequence correctly.");
                }

                controller.StopImmediate();
                if (controller.IsPlaying || !controller.ReadyForPool)
                {
                    throw new UnityException("FireBlastVFX did not reset to a pool-ready state.");
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
            EnsureFolder("Assets/_Project/VFX", "FireBlast");
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
                    throw new MissingReferenceException($"Required Fire Blast texture is missing: {path}");
                }

                bool atlas = textureName.EndsWith("Atlas.png");
                importer.textureType = TextureImporterType.Default;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = textureName == "FireBlast_Streak.png" ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = atlas ? 512 : textureName == "FireBlast_Streak.png" ? 1024 : 512;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
        }

        private static FireBlastVFXProfile LoadOrCreateProfile()
        {
            FireBlastVFXProfile profile = AssetDatabase.LoadAssetAtPath<FireBlastVFXProfile>(ProfilePath);
            if (profile != null)
            {
                return profile;
            }

            profile = ScriptableObject.CreateInstance<FireBlastVFXProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
            return profile;
        }

        private static Dictionary<string, Material> CreateMaterials()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>($"{ShaderFolder}/FireBlastSpriteUnlit.shader");
            if (shader == null)
            {
                throw new MissingReferenceException("Fire Blast shader must compile before building materials.");
            }

            return new Dictionary<string, Material>
            {
                ["Streak"] = CreateMaterial("FireBlast_Streak", shader, "FireBlast_Streak.png", true, 1.3f),
                ["Core"] = CreateMaterial("FireBlast_Core", shader, "FireBlast_Core.png", true, 1.5f),
                ["Compression"] = CreateMaterial("FireBlast_CompressionFlash", shader, "FireBlast_Core.png", true, 2f),
                ["OuterCombustion"] = CreateMaterial("FireBlast_OuterCombustion", shader, "FireBlast_Core.png", true, 0.95f),
                ["Flame"] = CreateMaterial("FireBlast_FlameBurst", shader, "FireBlast_FlameAtlas.png", true, 1.15f),
                ["LingeringFlame"] = CreateMaterial("FireBlast_LingeringFlames", shader, "FireBlast_FlameAtlas.png", true, 0.95f),
                ["Ring"] = CreateMaterial("FireBlast_HeatRing", shader, "FireBlast_HeatRing.png", true, 1.05f),
                ["SecondaryRing"] = CreateMaterial("FireBlast_SecondaryHeatRing", shader, "FireBlast_HeatRing.png", true, 0.85f),
                ["Ember"] = CreateMaterial("FireBlast_Embers", shader, "FireBlast_EmberAtlas.png", true, 1.15f),
                ["Spark"] = CreateMaterial("FireBlast_Sparks", shader, "FireBlast_SparkAtlas.png", true, 1.25f),
                ["Smoke"] = CreateMaterial("FireBlast_Smoke", shader, "FireBlast_SmokeAtlas.png", false, 0.8f)
            };
        }

        private static Material CreateMaterial(string name, Shader shader, string textureName, bool additive, float brightness)
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

            Texture texture = AssetDatabase.LoadAssetAtPath<Texture>($"{TextureFolder}/{textureName}");
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_Tint", Color.white);
            material.SetFloat("_Opacity", 1f);
            material.SetFloat("_Brightness", brightness);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            material.enableInstancing = true;
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreatePrefab(FireBlastVFXProfile profile, IReadOnlyDictionary<string, Material> materials)
        {
            GameObject root = new("FireBlastVFX");
            try
            {
                FireBlastVFX controller = root.AddComponent<FireBlastVFX>();

                Transform casterRoot = CreateSection("Casting Flash", root.transform);
                MeshRenderer casterGlow = CreateQuad("Hand Ignition Glow", casterRoot, materials["Core"], 8);
                ParticleSystem castingEmbers = CreateBurstParticles(
                    "Swirling Casting Embers", casterRoot, materials["Ember"], 0f, 0.23f, 0.45f, 0.075f, 6, 7, ParticleSystemShapeType.Sphere);
                ConfigureMotion(castingEmbers, 0.12f, 0.28f, 0.12f, true);

                Transform streakRoot = CreateSection("Instant Fire Streak", root.transform);
                LineRenderer streak = CreateStreak(streakRoot, materials["Streak"]);

                Transform impactRoot = CreateSection("Target Combustion", root.transform);
                MeshRenderer compression = CreateQuad("Compression Flash", impactRoot, materials["Compression"], 13);
                MeshRenderer outerCombustion = CreateQuad("Outer Combustion", impactRoot, materials["OuterCombustion"], 6);
                MeshRenderer core = CreateQuad("White Hot Explosion Core", impactRoot, materials["Core"], 10);
                ParticleSystem flames = CreateBurstParticles(
                    "Chunky Flame Lobes", impactRoot, materials["Flame"], 0.035f, 0.42f, 1.35f, 1.12f, 12, 9, ParticleSystemShapeType.Sphere);
                ConfigureMotion(flames, 0.22f, 0.22f, 0.05f, true);
                MeshRenderer ring = CreateQuad("Broken Heat Ring", impactRoot, materials["Ring"], 8);
                MeshRenderer secondaryRing = CreateQuad("Secondary Heat Ring", impactRoot, materials["SecondaryRing"], 7);
                ParticleSystem embers = CreateBurstParticles(
                    "Controlled Ember Burst", impactRoot, materials["Ember"], 0.045f, 0.62f, 2.65f, 0.14f, 18, 10, ParticleSystemShapeType.Sphere);
                ConfigureMotion(embers, 0.14f, -0.7f, 0f, true);
                ParticleSystem sparks = CreateBurstParticles(
                    "Hot Fragment Sparks", impactRoot, materials["Spark"], 0.04f, 0.34f, 3.5f, 0.18f, 14, 12, ParticleSystemShapeType.Sphere);
                ConfigureMotion(sparks, 0.08f, -0.4f, 0f, true);

                Transform aftermathRoot = CreateSection("Aftermath", impactRoot);
                MeshRenderer lingeringGlow = CreateQuad("Lingering Warm Glow", aftermathRoot, materials["Core"], 3);
                ParticleSystem lingeringFlames = CreateBurstParticles(
                    "Lingering Flame Bloom", aftermathRoot, materials["LingeringFlame"], 0.15f, 0.78f, 0.55f, 0.8f, 6, 6, ParticleSystemShapeType.Sphere);
                ConfigureMotion(lingeringFlames, 0.45f, -0.04f, 0.32f, true);
                ParticleSystem smoke = CreateBurstParticles(
                    "Subtle Smoke Bloom", aftermathRoot, materials["Smoke"], 0.24f, 0.68f, 0.22f, 0.9f, 5, 2, ParticleSystemShapeType.Sphere);
                ConfigureMotion(smoke, 0.32f, 0.12f, 0.42f, false, true);
                ParticleSystem residualEmbers = CreateBurstParticles(
                    "Drifting Final Embers", aftermathRoot, materials["Ember"], 0.2f, 0.62f, 0.38f, 0.07f, 6, 5, ParticleSystemShapeType.Sphere);
                ConfigureMotion(residualEmbers, 0.18f, 0.15f, 0.35f, true);

                controller.ConfigureAuthoring(
                    profile,
                    true,
                    casterRoot,
                    casterGlow,
                    castingEmbers,
                    streak,
                    impactRoot,
                    compression,
                    outerCombustion,
                    core,
                    flames,
                    ring,
                    secondaryRing,
                    embers,
                    sparks,
                    lingeringGlow,
                    lingeringFlames,
                    smoke,
                    residualEmbers);

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (prefab == null)
                {
                    throw new UnityException($"Failed to save FireBlastVFX at {PrefabPath}.");
                }

                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void WireIntoMageFireBlast(GameObject prefab)
        {
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(DefinitionPath);
            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AbilityPath);
            if (definition == null || ability == null)
            {
                throw new MissingReferenceException("Mage Fire Blast ability or VFX definition is missing.");
            }

            definition.Configure(
                null,
                prefab,
                null,
                true,
                true,
                false,
                true,
                new Vector3(0f, 1.15f, 0.42f),
                Vector3.zero,
                new Vector3(0f, 1.18f, 0.48f),
                new Vector3(0f, 0.9f, 0f),
                0.02f,
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

        private static MeshRenderer CreateQuad(string name, Transform parent, Material material, int sortingOrder)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            MeshFilter filter = child.AddComponent<MeshFilter>();
            filter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            MeshRenderer renderer = child.AddComponent<MeshRenderer>();
            ConfigureRenderer(renderer, material, sortingOrder);
            return renderer;
        }

        private static LineRenderer CreateStreak(Transform parent, Material material)
        {
            GameObject child = new("Few Frame Fire Ribbon");
            child.transform.SetParent(parent, false);
            LineRenderer line = child.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.textureMode = LineTextureMode.Tile;
            line.alignment = LineAlignment.View;
            line.widthMultiplier = 0.32f;
            line.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0.38f),
                new Keyframe(0.12f, 1f),
                new Keyframe(0.88f, 1f),
                new Keyframe(1f, 0.32f));
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.generateLightingData = false;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.allowOcclusionWhenDynamic = false;
            line.sortingOrder = 9;
            return line;
        }

        private static ParticleSystem CreateBurstParticles(
            string name,
            Transform parent,
            Material material,
            float delay,
            float lifetime,
            float speed,
            float size,
            int count,
            int sortingOrder,
            ParticleSystemShapeType shapeType)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            ParticleSystem particles = child.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.duration = Mathf.Max(0.1f, lifetime);
            main.loop = false;
            main.playOnAwake = false;
            main.startDelay = delay;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.82f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.72f, speed * 1.12f);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.72f, size * 1.16f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            main.startColor = Color.white;
            main.maxParticles = Mathf.Max(1, count);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.stopAction = ParticleSystemStopAction.None;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = shapeType;
            shape.radius = 0.16f;

            ParticleSystemRenderer renderer = child.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            ConfigureRenderer(renderer, material, sortingOrder);

            ParticleSystem.TextureSheetAnimationModule sheet = particles.textureSheetAnimation;
            if (material.mainTexture != null && material.mainTexture.name.EndsWith("Atlas"))
            {
                sheet.enabled = true;
                sheet.mode = ParticleSystemAnimationMode.Grid;
                sheet.numTilesX = 2;
                sheet.numTilesY = 2;
                sheet.animation = ParticleSystemAnimationType.WholeSheet;
                sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 0.999f);
                sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
                sheet.cycleCount = 1;
            }

            return particles;
        }

        private static void ConfigureMotion(
            ParticleSystem particles,
            float radius,
            float gravity,
            float upwardVelocity,
            bool rotate,
            bool smoke = false)
        {
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.radius = radius;
            ParticleSystem.MainModule main = particles.main;
            main.gravityModifier = gravity;

            if (upwardVelocity != 0f)
            {
                ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
                velocity.enabled = true;
                velocity.y = new ParticleSystem.MinMaxCurve(upwardVelocity * 0.72f, upwardVelocity * 1.25f);
                velocity.x = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
                velocity.z = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
            }

            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = CreateAlphaGradient(smoke ? 0f : 0.25f, 1f, 0f, smoke ? 0.2f : 0.08f);

            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, smoke ? 0.55f : 0.45f),
                new Keyframe(smoke ? 0.42f : 0.22f, 1f),
                new Keyframe(1f, smoke ? 1.55f : 0.08f)));

            if (rotate)
            {
                ParticleSystem.RotationOverLifetimeModule rotation = particles.rotationOverLifetime;
                rotation.enabled = true;
                rotation.z = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);
            }

            ParticleSystem.LimitVelocityOverLifetimeModule limit = particles.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = smoke ? 0.55f : 3.5f;
            limit.dampen = smoke ? 0.32f : 0.18f;
        }

        private static Gradient CreateAlphaGradient(float start, float middle, float end, float peakTime)
        {
            Gradient gradient = new();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(start, 0f),
                    new GradientAlphaKey(middle, peakTime),
                    new GradientAlphaKey(end, 1f)
                });
            return gradient;
        }

        private static void ConfigureRenderer(Renderer renderer, Material material, int sortingOrder)
        {
            if (renderer is ParticleSystemRenderer particles)
            {
                particles.sharedMaterial = material;
            }
            else if (renderer is MeshRenderer mesh)
            {
                mesh.sharedMaterial = material;
                mesh.lightProbeUsage = LightProbeUsage.Off;
                mesh.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }

            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.allowOcclusionWhenDynamic = false;
            renderer.sortingOrder = sortingOrder;
        }
    }
}
