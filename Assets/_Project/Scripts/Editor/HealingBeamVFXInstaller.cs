using System.Collections.Generic;
using RPGClone.Vfx;
using RPGClone.Vfx.Healing;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace RPGClone.EditorTools
{
    public static class HealingBeamVFXInstaller
    {
        private const string RootFolder = "Assets/_Project/VFX/HealingBeam";
        private const string TextureFolder = RootFolder + "/Textures";
        private const string ShaderFolder = RootFolder + "/Shaders";
        private const string MaterialFolder = RootFolder + "/Materials";
        private const string ProfileFolder = RootFolder + "/Profiles";
        private const string PrefabFolder = RootFolder + "/Prefabs";
        private const string ProfilePath = ProfileFolder + "/HealingBeamVFX_Default.asset";
        private const string PrefabPath = PrefabFolder + "/HealingBeamVFX.prefab";
        private const string CastingPrefabPath = PrefabFolder + "/HealingBeamChargeVFX.prefab";
        private const string SpellVfxDefinitionPath = "Assets/_Project/VFX/Definitions/Shaman_Healing_Beam_VFX.asset";

        [MenuItem("Tools/RPG Clone/VFX/Build Healing Beam VFX")]
        public static void Build()
        {
            EnsureFolders();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureTextureImporters();

            HealingBeamVFXProfile profile = LoadOrCreateProfile();
            EditorUtility.SetDirty(profile);
            Dictionary<string, Material> materials = CreateMaterials();
            GameObject castingPrefab = CreateCastingPrefab(profile, materials);
            GameObject prefab = CreatePrefab(profile, materials);
            WireIntoHealingSpell(castingPrefab, prefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log($"Built reusable healing beam VFX at '{PrefabPath}'.", prefab);
        }

        [MenuItem("Tools/RPG Clone/VFX/Validate Healing Beam VFX")]
        public static void ValidateBuild()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                throw new MissingReferenceException($"Healing beam prefab is missing at {PrefabPath}.");
            }

            string[] sections = { "Beam Effect", "Caster Effect", "Target Effect", "Heal-Tick Burst Effect" };
            foreach (string section in sections)
            {
                if (prefab.transform.Find(section) == null)
                {
                    throw new MissingReferenceException($"Healing beam prefab is missing its '{section}' section.");
                }
            }

            HealingBeamVFX prefabController = prefab.GetComponent<HealingBeamVFX>();
            HealingBeamAbilityVfxAdapter prefabAdapter = prefab.GetComponent<HealingBeamAbilityVfxAdapter>();
            if (prefabController == null || prefabController.Profile == null || prefabAdapter == null)
            {
                throw new MissingReferenceException("Healing beam controller, spell adapter, or profile reference is missing.");
            }

            if (prefab.GetComponentsInChildren<LineRenderer>(true).Length != 3)
            {
                throw new UnityException("Healing beam prefab must contain exactly three beam layers.");
            }

            if (prefab.GetComponentsInChildren<ParticleSystem>(true).Length != 7)
            {
                throw new UnityException("Healing beam prefab must contain exactly seven lightweight particle systems.");
            }

            GameObject castingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CastingPrefabPath);
            if (castingPrefab == null
                || castingPrefab.GetComponent<HealingBeamChargeVFX>() == null
                || castingPrefab.GetComponentsInChildren<ParticleSystem>(true).Length != 3
                || castingPrefab.GetComponentsInChildren<LineRenderer>(true).Length != 0)
            {
                throw new MissingReferenceException("Healing Beam's caster-only charge prefab is missing or contains beam/target visuals.");
            }

            ValidateShader($"{ShaderFolder}/HealingBeamUnlit.shader");
            ValidateShader($"{ShaderFolder}/HealingSpriteUnlit.shader");
            ValidateSpellWiring(prefab);
            ValidateLifecycle(prefab);
            Debug.Log("HealingBeamVFX validation passed: caster-only charge, post-cast beam wiring, assets, shaders, lifecycle hooks, and pooling reset are valid.", prefab);
        }

        private static void ValidateShader(string path)
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (shader == null || !shader.isSupported)
            {
                throw new MissingReferenceException($"Healing VFX shader is missing or unsupported: {path}");
            }

            foreach (ShaderMessage message in ShaderUtil.GetShaderMessages(shader))
            {
                if (message.severity == ShaderCompilerMessageSeverity.Error)
                {
                    throw new UnityException($"Shader error in {path}: {message.message}");
                }
            }
        }

        private static void ValidateSpellWiring(GameObject prefab)
        {
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(SpellVfxDefinitionPath);
            GameObject castingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CastingPrefabPath);
            if (definition == null
                || castingPrefab == null
                || definition.CastingPrefab != castingPrefab
                || definition.CastPrefab != prefab
                || definition.HitPrefab != null
                || definition.UseHandCastingAnchors
                || definition.CastPrefabControlsHitTiming)
            {
                throw new MissingReferenceException("Shaman Healing Beam must use its centered caster-only charge while casting and HealingBeamVFX only for its post-cast launch.");
            }
        }

        private static void ValidateLifecycle(GameObject prefab)
        {
            GameObject instance = null;
            GameObject caster = null;
            GameObject target = null;
            try
            {
                instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                caster = new GameObject("HealingBeamVFX Validation Caster") { hideFlags = HideFlags.HideAndDontSave };
                target = new GameObject("HealingBeamVFX Validation Target") { hideFlags = HideFlags.HideAndDontSave };
                instance.hideFlags = HideFlags.HideAndDontSave;
                caster.transform.position = new Vector3(-1f, 1.2f, 0f);
                target.transform.position = new Vector3(3f, 1f, 1f);

                HealingBeamVFX controller = instance.GetComponent<HealingBeamVFX>();
                System.Reflection.MethodInfo awake = typeof(HealingBeamVFX).GetMethod(
                    "Awake",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                awake?.Invoke(controller, null);
                controller.Play(caster.transform, target.transform);
                if (!controller.IsPlaying || controller.ReadyForPool)
                {
                    throw new UnityException("Healing beam did not enter its playing lifecycle state.");
                }

                System.Reflection.MethodInfo lateUpdate = typeof(HealingBeamVFX).GetMethod(
                    "LateUpdate",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                lateUpdate?.Invoke(controller, null);
                LineRenderer[] lines = instance.GetComponentsInChildren<LineRenderer>(true);
                foreach (LineRenderer line in lines)
                {
                    if (line.positionCount < 2
                        || Vector3.Distance(line.GetPosition(0), caster.transform.position) > 0.001f
                        || Vector3.Distance(line.GetPosition(line.positionCount - 1), target.transform.position) > 0.001f)
                    {
                        throw new UnityException("A healing beam layer is not firmly attached to both endpoints.");
                    }
                }

                Vector3 firstTargetPosition = lines[0].GetPosition(lines[0].positionCount - 1);
                target.transform.position += new Vector3(1.25f, 0.4f, -0.35f);
                lateUpdate?.Invoke(controller, null);
                if (Vector3.Distance(firstTargetPosition, lines[0].GetPosition(lines[0].positionCount - 1)) < 0.5f)
                {
                    throw new UnityException("Healing beam endpoint did not follow its moving target attachment.");
                }

                controller.TriggerHealingTick();
                controller.Stop();
                if (controller.IsPlaying || controller.ReadyForPool)
                {
                    throw new UnityException("Healing beam graceful-stop lifecycle state is invalid.");
                }

                controller.StopImmediate();
                if (!controller.ReadyForPool)
                {
                    throw new UnityException("Healing beam did not become ready for pooling after an immediate stop.");
                }

                controller.ResetForPool();
            }
            finally
            {
                if (instance != null)
                {
                    Object.DestroyImmediate(instance);
                }
                if (caster != null)
                {
                    Object.DestroyImmediate(caster);
                }
                if (target != null)
                {
                    Object.DestroyImmediate(target);
                }
            }
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/_Project/VFX", "HealingBeam");
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
            ConfigureTexture("HealingRibbon.png", TextureWrapMode.Repeat, 512, true, true);
            ConfigureTexture("HealingNoise.png", TextureWrapMode.Repeat, 256, false, false);
            ConfigureTexture("SoftGlow.png", TextureWrapMode.Clamp, 256, true, true);
            ConfigureTexture("HealingSpark.png", TextureWrapMode.Clamp, 256, true, true);
            ConfigureTexture("HealingOrb.png", TextureWrapMode.Clamp, 128, true, true);
            ConfigureTexture("HealingBurst.png", TextureWrapMode.Clamp, 256, true, true);
            ConfigureTexture("GroundRing.png", TextureWrapMode.Clamp, 256, true, true);
        }

        private static void ConfigureTexture(string fileName, TextureWrapMode wrapMode, int maxSize, bool hasAlpha, bool sRgb)
        {
            string path = $"{TextureFolder}/{fileName}";
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                Debug.LogError($"Required healing VFX texture is missing: {path}");
                return;
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

        private static HealingBeamVFXProfile LoadOrCreateProfile()
        {
            HealingBeamVFXProfile profile = AssetDatabase.LoadAssetAtPath<HealingBeamVFXProfile>(ProfilePath);
            if (profile != null)
            {
                return profile;
            }

            profile = ScriptableObject.CreateInstance<HealingBeamVFXProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
            return profile;
        }

        private static Dictionary<string, Material> CreateMaterials()
        {
            Shader beamShader = AssetDatabase.LoadAssetAtPath<Shader>($"{ShaderFolder}/HealingBeamUnlit.shader");
            Shader spriteShader = AssetDatabase.LoadAssetAtPath<Shader>($"{ShaderFolder}/HealingSpriteUnlit.shader");
            if (beamShader == null || spriteShader == null)
            {
                throw new MissingReferenceException("Healing beam shaders must compile before building the prefab.");
            }

            Texture ribbon = LoadTexture("HealingRibbon.png");
            Texture noise = LoadTexture("HealingNoise.png");
            Texture glow = LoadTexture("SoftGlow.png");
            Texture spark = LoadTexture("HealingSpark.png");
            Texture orb = LoadTexture("HealingOrb.png");
            Texture burst = LoadTexture("HealingBurst.png");
            Texture groundRing = LoadTexture("GroundRing.png");

            return new Dictionary<string, Material>
            {
                ["MainBeam"] = CreateBeamMaterial("HealingBeam_MainBeam", beamShader, ribbon, noise, new Color(1f, 0.82f, 0.34f, 0.82f)),
                ["BeamGlow"] = CreateBeamMaterial("HealingBeam_BeamGlow", beamShader, ribbon, noise, new Color(1f, 0.68f, 0.18f, 0.3f)),
                ["BeamCore"] = CreateBeamMaterial("HealingBeam_BeamCore", beamShader, ribbon, noise, new Color(1.3f, 1.15f, 0.8f, 0.92f)),
                ["CasterGlow"] = CreateSpriteMaterial("HealingBeam_CasterGlow", spriteShader, glow, new Color(1f, 0.78f, 0.25f, 0.5f), true),
                ["TargetGlow"] = CreateSpriteMaterial("HealingBeam_TargetGlow", spriteShader, glow, new Color(1f, 0.84f, 0.38f, 0.45f), true),
                ["GroundRing"] = CreateSpriteMaterial("HealingBeam_GroundRing", spriteShader, groundRing, new Color(1f, 0.72f, 0.2f, 0.38f), false),
                ["Sparks"] = CreateSpriteMaterial("HealingBeam_Sparks", spriteShader, spark, new Color(1f, 0.88f, 0.48f, 0.9f), true),
                ["Orbs"] = CreateSpriteMaterial("HealingBeam_Orbs", spriteShader, orb, new Color(0.82f, 1f, 0.72f, 0.76f), true),
                ["HealBurst"] = CreateSpriteMaterial("HealingBeam_HealBurst", spriteShader, burst, new Color(1f, 0.88f, 0.45f, 0.8f), true)
            };
        }

        private static Texture LoadTexture(string fileName)
        {
            Texture texture = AssetDatabase.LoadAssetAtPath<Texture>($"{TextureFolder}/{fileName}");
            if (texture == null)
            {
                throw new MissingReferenceException($"Missing healing VFX texture: {fileName}");
            }
            return texture;
        }

        private static Material CreateBeamMaterial(string name, Shader shader, Texture ribbon, Texture noise, Color tint)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Material material = LoadOrCreateMaterial(path, shader);
            material.SetTexture("_BaseMap", ribbon);
            material.SetTexture("_NoiseMap", noise);
            material.SetColor("_Tint", tint);
            material.SetFloat("_Opacity", 1f);
            material.enableInstancing = true;
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateSpriteMaterial(string name, Shader shader, Texture texture, Color tint, bool additive)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Material material = LoadOrCreateMaterial(path, shader);
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_Tint", tint);
            material.SetFloat("_Opacity", 1f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            material.enableInstancing = true;
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material LoadOrCreateMaterial(string path, Shader shader)
        {
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
            return material;
        }

        private static GameObject CreateCastingPrefab(HealingBeamVFXProfile profile, IReadOnlyDictionary<string, Material> materials)
        {
            GameObject root = new("HealingBeamChargeVFX");
            try
            {
                HealingBeamChargeVFX controller = root.AddComponent<HealingBeamChargeVFX>();
                CasterEffectReferences caster = CreateCasterEffect(root.transform, materials);
                controller.ConfigureAuthoring(
                    profile,
                    caster.Root,
                    caster.Glow,
                    caster.OriginFlash,
                    caster.OrbitingStars,
                    caster.InwardOrbs);

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, CastingPrefabPath);
                if (prefab == null)
                {
                    throw new UnityException($"Failed to save healing charge VFX prefab at {CastingPrefabPath}.");
                }

                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreatePrefab(HealingBeamVFXProfile profile, IReadOnlyDictionary<string, Material> materials)
        {
            GameObject root = new("HealingBeamVFX");
            try
            {
                HealingBeamVFX controller = root.AddComponent<HealingBeamVFX>();
                HealingBeamAbilityVfxAdapter adapter = root.AddComponent<HealingBeamAbilityVfxAdapter>();

                Transform beamRoot = CreateSection("Beam Effect", root.transform);
                LineRenderer outerGlow = CreateLine("Outer Glow", beamRoot, materials["BeamGlow"], 0);
                LineRenderer ribbon = CreateLine("Flowing Energy Ribbon", beamRoot, materials["MainBeam"], 1);
                LineRenderer core = CreateLine("Bright Inner Core + Traveling Pulse", beamRoot, materials["BeamCore"], 2);

                CasterEffectReferences caster = CreateCasterEffect(root.transform, materials);

                Transform targetRoot = CreateSection("Target Effect", root.transform);
                MeshRenderer targetGlow = CreateQuad("Torso Glow", targetRoot, materials["TargetGlow"], Vector3.zero, Vector3.one * 1.15f, Quaternion.identity, 0);
                MeshRenderer groundRing = CreateQuad("Soft Ground Ring", targetRoot, materials["GroundRing"], new Vector3(0f, -1.05f, 0f), Vector3.one * 2.2f, Quaternion.Euler(90f, 0f, 0f), -1);
                ParticleSystem risingOrbs = CreateRisingOrbs("Rising Orbs", targetRoot, materials["Orbs"]);
                ParticleSystem sparkles = CreateTargetSparkles("Target Sparkles", targetRoot, materials["Sparks"]);

                Transform tickRoot = CreateSection("Heal-Tick Burst Effect", root.transform);
                ParticleSystem burst = CreateHealBurst("Soft Petal Burst", tickRoot, materials["HealBurst"]);
                ParticleSystem tickSparks = CreateTickSparks("Tick Sparks", tickRoot, materials["Sparks"]);

                controller.ConfigureAuthoring(
                    profile,
                    beamRoot,
                    outerGlow,
                    ribbon,
                    core,
                    caster.Root,
                    caster.Glow,
                    caster.OriginFlash,
                    caster.OrbitingStars,
                    caster.InwardOrbs,
                    targetRoot,
                    targetGlow,
                    groundRing.transform,
                    groundRing,
                    risingOrbs,
                    sparkles,
                    tickRoot,
                    burst,
                    tickSparks);
                adapter.ConfigureAuthoring(controller);

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (prefab == null)
                {
                    throw new UnityException($"Failed to save healing VFX prefab at {PrefabPath}.");
                }
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static CasterEffectReferences CreateCasterEffect(Transform parent, IReadOnlyDictionary<string, Material> materials)
        {
            Transform root = CreateSection("Caster Effect", parent);
            MeshRenderer glow = CreateQuad("Caster Glow", root, materials["CasterGlow"], Vector3.zero, Vector3.one * 0.72f, Quaternion.identity, 0);
            ParticleSystem originFlash = CreateFlash("Origin Flash", root, materials["CasterGlow"]);
            ParticleSystem orbitingStars = CreateOrbitStars("Orbiting Stars", root, materials["Sparks"]);
            ParticleSystem inwardOrbs = CreateInwardOrbs("Inward Spiral Orbs", root, materials["Orbs"]);
            return new CasterEffectReferences(root, glow, originFlash, orbitingStars, inwardOrbs);
        }

        private static void WireIntoHealingSpell(GameObject castingPrefab, GameObject prefab)
        {
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(SpellVfxDefinitionPath);
            if (definition == null)
            {
                throw new MissingReferenceException($"Healing Beam VFX definition is missing: {SpellVfxDefinitionPath}");
            }

            if (castingPrefab == null)
            {
                throw new MissingReferenceException($"Healing Beam hand-charge prefab is missing: {CastingPrefabPath}");
            }

            definition.Configure(
                castingPrefab,
                prefab,
                null,
                true,
                false,
                true,
                false,
                new Vector3(0f, 1.15f, 0.42f),
                Vector3.zero,
                new Vector3(0f, 1.18f, 0.48f),
                new Vector3(0f, 0.85f, 0f),
                0f,
                false);
            EditorUtility.SetDirty(definition);
        }

        private static Transform CreateSection(string name, Transform parent)
        {
            GameObject section = new(name);
            section.transform.SetParent(parent, false);
            return section.transform;
        }

        private static LineRenderer CreateLine(string name, Transform parent, Material material, int sortingOrder)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            LineRenderer line = child.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.loop = false;
            line.positionCount = 12;
            // Keep UV.x normalized for endpoint fading and traveling pulses; the shader performs distance tiling.
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.numCornerVertices = 2;
            line.numCapVertices = 3;
            line.generateLightingData = false;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.allowOcclusionWhenDynamic = false;
            line.sortingOrder = sortingOrder;
            line.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0.05f),
                new Keyframe(0.12f, 0.76f),
                new Keyframe(0.5f, 1f),
                new Keyframe(0.88f, 0.76f),
                new Keyframe(1f, 0.05f));
            return line;
        }

        private static MeshRenderer CreateQuad(string name, Transform parent, Material material, Vector3 localPosition, Vector3 localScale, Quaternion localRotation, int sortingOrder)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            child.transform.localRotation = localRotation;
            child.transform.localScale = localScale;
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

        private static ParticleSystem CreateFlash(string name, Transform parent, Material material)
        {
            ParticleSystem particleSystem = CreateParticleSystem(name, parent, material, false, 0.28f, 0.22f, 0f, 0.42f, new Color(1f, 0.9f, 0.52f, 0.9f), 2);
            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)1) });
            ConfigureFadeAndScale(particleSystem, 0.2f, 1f, 1.75f);
            return particleSystem;
        }

        private static ParticleSystem CreateOrbitStars(string name, Transform parent, Material material)
        {
            ParticleSystem particleSystem = CreateParticleSystem(name, parent, material, true, 2f, 1.35f, 0f, 0.14f, new Color(1f, 0.86f, 0.36f, 0.9f), 8);
            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = 4f;
            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.3f;
            ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.orbitalY = 1.2f;
            ConfigureFadeAndScale(particleSystem, 0.1f, 1f, 0.25f);
            return particleSystem;
        }

        private static ParticleSystem CreateInwardOrbs(string name, Transform parent, Material material)
        {
            ParticleSystem particleSystem = CreateParticleSystem(name, parent, material, true, 1.6f, 1.1f, 0f, 0.1f, new Color(0.72f, 1f, 0.64f, 0.72f), 8);
            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = 4f;
            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.38f;
            shape.radiusThickness = 0.05f;
            ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.radial = -0.32f;
            velocity.orbitalY = 0.7f;
            ConfigureFadeAndScale(particleSystem, 0.25f, 1f, 0.15f);
            return particleSystem;
        }

        private static ParticleSystem CreateRisingOrbs(string name, Transform parent, Material material)
        {
            ParticleSystem particleSystem = CreateParticleSystem(name, parent, material, true, 2f, 1.65f, 0.12f, 0.12f, new Color(0.78f, 1f, 0.66f, 0.7f), 12);
            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = 5f;
            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.48f;
            ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.y = new ParticleSystem.MinMaxCurve(0.2f, 0.42f);
            velocity.orbitalY = 0.18f;
            ConfigureFadeAndScale(particleSystem, 0.15f, 1f, 0.35f);
            return particleSystem;
        }

        private static ParticleSystem CreateTargetSparkles(string name, Transform parent, Material material)
        {
            ParticleSystem particleSystem = CreateParticleSystem(name, parent, material, true, 2f, 1.25f, 0.08f, 0.13f, new Color(1f, 0.9f, 0.45f, 0.82f), 8);
            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = 2.5f;
            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.5f;
            ConfigureFadeAndScale(particleSystem, 0.05f, 1f, 0.05f);
            return particleSystem;
        }

        private static ParticleSystem CreateHealBurst(string name, Transform parent, Material material)
        {
            ParticleSystem particleSystem = CreateParticleSystem(name, parent, material, false, 0.55f, 0.46f, 0f, 0.72f, new Color(1f, 0.86f, 0.38f, 0.78f), 2);
            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)1) });
            ConfigureFadeAndScale(particleSystem, 0.2f, 1.65f, 1.9f);
            ParticleSystem.RotationOverLifetimeModule rotation = particleSystem.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = 0.9f;
            return particleSystem;
        }

        private static ParticleSystem CreateTickSparks(string name, Transform parent, Material material)
        {
            ParticleSystem particleSystem = CreateParticleSystem(name, parent, material, false, 0.9f, 0.75f, 0.62f, 0.15f, new Color(1f, 0.9f, 0.45f, 0.92f), 12);
            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)7) });
            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.28f;
            ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.y = new ParticleSystem.MinMaxCurve(0.25f, 0.65f);
            velocity.orbitalY = 0.22f;
            ConfigureFadeAndScale(particleSystem, 0.15f, 1f, 0.15f);
            return particleSystem;
        }

        private static ParticleSystem CreateParticleSystem(string name, Transform parent, Material material, bool loop, float duration, float lifetime, float speed, float size, Color color, int maxParticles)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            ParticleSystem particleSystem = child.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particleSystem.main;
            main.duration = duration;
            main.loop = loop;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = color;
            main.maxParticles = maxParticles;
            main.stopAction = ParticleSystemStopAction.None;
            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = false;
            ParticleSystemRenderer renderer = child.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = 3;
            return particleSystem;
        }

        private static void ConfigureFadeAndScale(ParticleSystem particleSystem, float startScale, float middleScale, float endScale)
        {
            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.18f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particleSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, startScale),
                new Keyframe(0.35f, middleScale),
                new Keyframe(1f, endScale)));
        }

        private readonly struct CasterEffectReferences
        {
            public CasterEffectReferences(
                Transform root,
                MeshRenderer glow,
                ParticleSystem originFlash,
                ParticleSystem orbitingStars,
                ParticleSystem inwardOrbs)
            {
                Root = root;
                Glow = glow;
                OriginFlash = originFlash;
                OrbitingStars = orbitingStars;
                InwardOrbs = inwardOrbs;
            }

            public Transform Root { get; }
            public MeshRenderer Glow { get; }
            public ParticleSystem OriginFlash { get; }
            public ParticleSystem OrbitingStars { get; }
            public ParticleSystem InwardOrbs { get; }
        }
    }
}
