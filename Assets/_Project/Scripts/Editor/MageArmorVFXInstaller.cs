using System.Collections.Generic;
using RPGClone.Abilities;
using RPGClone.Vfx;
using RPGClone.Vfx.Arcane;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace RPGClone.EditorTools
{
    public static class MageArmorVFXInstaller
    {
        private const string RootFolder = "Assets/_Project/VFX/MageArmor";
        private const string TextureFolder = RootFolder + "/Textures";
        private const string ShaderFolder = RootFolder + "/Shaders";
        private const string MaterialFolder = RootFolder + "/Materials";
        private const string ProfileFolder = RootFolder + "/Profiles";
        private const string PrefabFolder = RootFolder + "/Prefabs";
        private const string ProfilePath = ProfileFolder + "/MageArmorVFX_Default.asset";
        private const string PrefabPath = PrefabFolder + "/MageArmorApplyVFX.prefab";
        private const string AbilityWrapperPath = PrefabFolder + "/MageArmorApplyVFX_Ability.prefab";
        private const string ShaderPath = ShaderFolder + "/MageArmorUnlit.shader";
        private const string DefinitionPath = "Assets/_Project/VFX/Definitions/Mage_Mage_Armor_VFX.asset";
        private const string AbilityPath = "Assets/_Project/Configs/Abilities/Mage_Mage_Armor.asset";

        private static readonly string[] RequiredTextureNames =
        {
            "MageArmor_Glow.png",
            "MageArmor_ShellPattern.png",
            "MageArmor_FacetShield.png",
            "MageArmor_FacetWing.png",
            "MageArmor_FacetKite.png",
            "MageArmor_Ring.png",
            "MageArmor_Sparkle.png",
            "MageArmor_Orb.png",
            "MageArmor_Focus.png"
        };

        [MenuItem("Tools/RPG Clone/VFX/Build Mage Armor VFX")]
        public static void Build()
        {
            EnsureFolders();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureTextureImporters();

            MageArmorVFXProfile profile = LoadOrCreateProfile();
            Dictionary<string, Material> materials = CreateMaterials();
            GameObject reusablePrefab = CreateReusablePrefab(profile, materials);
            GameObject abilityWrapper = CreateAbilityWrapper(reusablePrefab);
            WireIntoMageArmor(abilityWrapper);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = reusablePrefab;
            EditorGUIUtility.PingObject(reusablePrefab);
            Debug.Log($"Built pooled MageArmorApplyVFX and wired the Mage Armor ability at '{PrefabPath}'.", reusablePrefab);
        }

        [MenuItem("Tools/RPG Clone/VFX/Validate Mage Armor VFX")]
        public static void ValidateBuild()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                throw new MissingReferenceException($"Mage Armor prefab is missing at {PrefabPath}.");
            }

            foreach (string section in new[]
                     {
                         "Central Application Flash",
                         "Temporary Protective Shell",
                         "Arcane Armor Facets",
                         "Rising Arcane Rings",
                         "Overhead Arcane Focus",
                         "Arcane Sparkles",
                         "Arcane Particles"
                     })
            {
                if (prefab.transform.Find($"Visual Root/{section}") == null)
                {
                    throw new MissingReferenceException($"MageArmorApplyVFX is missing its '{section}' section.");
                }
            }

            MageArmorApplyVFX controller = prefab.GetComponent<MageArmorApplyVFX>();
            if (controller == null || controller.Profile == null)
            {
                throw new MissingReferenceException("MageArmorApplyVFX controller or profile reference is missing.");
            }

            if (prefab.GetComponentsInChildren<ParticleSystem>(true).Length != 10
                || prefab.GetComponentsInChildren<Light>(true).Length != 0
                || prefab.GetComponentsInChildren<Animator>(true).Length != 0
                || prefab.GetComponentsInChildren<UnityEngine.Animation>(true).Length != 0)
            {
                throw new UnityException("MageArmorApplyVFX must contain ten budgeted particle systems and remain procedural and light-free.");
            }

            foreach (string textureName in RequiredTextureNames)
            {
                if (AssetDatabase.LoadAssetAtPath<Texture>($"{TextureFolder}/{textureName}") == null)
                {
                    throw new MissingReferenceException($"Required Mage Armor texture is missing: {textureName}");
                }
            }

            ValidateShader();
            ValidateAbilityWiring();
            ValidateLifecycle(prefab);
            Debug.Log("MageArmorApplyVFX validation passed: layered prefab, original textures, procedural lifecycle, pool-ready state, and Mage Armor wiring are valid.", prefab);
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/_Project/VFX", "MageArmor");
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
                    throw new MissingReferenceException($"Required Mage Armor VFX texture is missing: {path}");
                }

                bool shellPattern = textureName == "MageArmor_ShellPattern.png";
                bool smallParticle = textureName is "MageArmor_Sparkle.png" or "MageArmor_Orb.png";
                importer.textureType = TextureImporterType.Default;
                importer.wrapMode = shellPattern ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = shellPattern;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.sRGBTexture = true;
                importer.maxTextureSize = shellPattern ? 512 : smallParticle ? 128 : 256;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.SaveAndReimport();
            }
        }

        private static MageArmorVFXProfile LoadOrCreateProfile()
        {
            MageArmorVFXProfile profile = AssetDatabase.LoadAssetAtPath<MageArmorVFXProfile>(ProfilePath);
            if (profile != null)
            {
                return profile;
            }

            profile = ScriptableObject.CreateInstance<MageArmorVFXProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
            return profile;
        }

        private static Dictionary<string, Material> CreateMaterials()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null)
            {
                throw new MissingReferenceException($"Mage Armor shader must compile before materials are built: {ShaderPath}");
            }

            return new Dictionary<string, Material>
            {
                ["Flash"] = CreateMaterial("MageArmor_CentralFlash", shader, "MageArmor_Glow.png", true),
                ["Shell"] = CreateMaterial("MageArmor_ProtectiveShell", shader, "MageArmor_ShellPattern.png", false),
                ["FacetShield"] = CreateMaterial("MageArmor_FacetShield", shader, "MageArmor_FacetShield.png", true),
                ["FacetWing"] = CreateMaterial("MageArmor_FacetWing", shader, "MageArmor_FacetWing.png", true),
                ["FacetKite"] = CreateMaterial("MageArmor_FacetKite", shader, "MageArmor_FacetKite.png", true),
                ["Ring"] = CreateMaterial("MageArmor_RisingRing", shader, "MageArmor_Ring.png", true),
                ["Focus"] = CreateMaterial("MageArmor_OverheadFocus", shader, "MageArmor_Focus.png", true),
                ["Sparkle"] = CreateMaterial("MageArmor_Sparkles", shader, "MageArmor_Sparkle.png", true),
                ["Particle"] = CreateMaterial("MageArmor_ArcaneParticles", shader, "MageArmor_Orb.png", true)
            };
        }

        private static Material CreateMaterial(string name, Shader shader, string textureName, bool additive)
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
            if (texture == null)
            {
                throw new MissingReferenceException($"Missing Mage Armor texture: {textureName}");
            }

            material.SetTexture("_BaseMap", texture);
            material.SetColor("_Tint", Color.white);
            material.SetFloat("_Opacity", 1f);
            material.SetFloat("_DistortionStrength", 0f);
            material.SetFloat("_Dissolve", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            material.enableInstancing = true;
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreateReusablePrefab(MageArmorVFXProfile profile, IReadOnlyDictionary<string, Material> materials)
        {
            GameObject root = new("MageArmorApplyVFX");
            try
            {
                MageArmorApplyVFX controller = root.AddComponent<MageArmorApplyVFX>();
                Transform visualRoot = CreateSection("Visual Root", root.transform);

                Transform flashRoot = CreateSection("Central Application Flash", visualRoot);
                ParticleSystem flash = CreateOneShot("Torso Arcane Flash", flashRoot, materials["Flash"], Vector3.zero, ParticleSystemRenderMode.Billboard, 9);
                ConfigureFadeAndScale(flash, 0.18f, 1.18f, 0.08f);

                Transform shellSection = CreateSection("Temporary Protective Shell", visualRoot);
                MeshRenderer shell = CreateShell(shellSection, materials["Shell"]);

                Transform facetRoot = CreateSection("Arcane Armor Facets", visualRoot);
                ParticleSystem facetShield = CreateFacetSystem("Shield Facets", facetRoot, materials["FacetShield"], 5);
                ParticleSystem facetWing = CreateFacetSystem("Wing Facets", facetRoot, materials["FacetWing"], 4);
                ParticleSystem facetKite = CreateFacetSystem("Kite Facets", facetRoot, materials["FacetKite"], 6);

                Transform ringRoot = CreateSection("Rising Arcane Rings", visualRoot);
                ParticleSystem primaryRing = CreateRingSystem("Primary Enchantment Ring", ringRoot, materials["Ring"], 3);
                ParticleSystem secondaryRing = CreateRingSystem("Secondary Enchantment Ring", ringRoot, materials["Ring"], 2);
                secondaryRing.transform.localRotation = Quaternion.Euler(0f, 28f, 0f);

                Transform focusRoot = CreateSection("Overhead Arcane Focus", visualRoot);
                ParticleSystem focus = CreateOneShot("Arcane Focus Crest", focusRoot, materials["Focus"], new Vector3(0f, 1.28f, 0f), ParticleSystemRenderMode.HorizontalBillboard, 7);
                ConfigureFadeAndScale(focus, 0.12f, 1.12f, 0.02f);

                Transform sparkleRoot = CreateSection("Arcane Sparkles", visualRoot);
                ParticleSystem sparkles = CreateSparkleSystem(sparkleRoot, materials["Sparkle"]);

                Transform particleRoot = CreateSection("Arcane Particles", visualRoot);
                ParticleSystem outward = CreateArcaneParticles("Outward Arcane Burst", particleRoot, materials["Particle"], false, 3);
                ParticleSystem inward = CreateArcaneParticles("Absorbing Arcane Particles", particleRoot, materials["Particle"], true, 4);

                controller.ConfigureAuthoring(
                    profile,
                    visualRoot,
                    shellSection,
                    shell,
                    flash,
                    facetShield,
                    facetWing,
                    facetKite,
                    primaryRing,
                    secondaryRing,
                    focus,
                    sparkles,
                    outward,
                    inward);

                shell.enabled = false;
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (prefab == null)
                {
                    throw new UnityException($"Failed to save MageArmorApplyVFX at {PrefabPath}.");
                }

                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateAbilityWrapper(GameObject reusablePrefab)
        {
            GameObject root = new("MageArmorApplyVFX_Ability");
            try
            {
                GameObject nested = PrefabUtility.InstantiatePrefab(reusablePrefab, root.transform) as GameObject;
                if (nested == null)
                {
                    throw new UnityException("Failed to instantiate the reusable Mage Armor prefab for its ability wrapper.");
                }

                nested.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                nested.transform.localScale = Vector3.one;
                root.AddComponent<MMOAbilityVfxLifetime>().Configure(1.35f, true, true);
                GameObject wrapper = PrefabUtility.SaveAsPrefabAsset(root, AbilityWrapperPath);
                if (wrapper == null)
                {
                    throw new UnityException($"Failed to save Mage Armor ability wrapper at {AbilityWrapperPath}.");
                }

                return wrapper;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void WireIntoMageArmor(GameObject abilityWrapper)
        {
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(DefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<MMOAbilityVfxDefinition>();
                AssetDatabase.CreateAsset(definition, DefinitionPath);
            }

            definition.Configure(
                null,
                null,
                abilityWrapper,
                true,
                false,
                true,
                false,
                Vector3.zero,
                Vector3.zero,
                Vector3.zero,
                new Vector3(0f, 1.02f, 0f),
                0f,
                false);
            EditorUtility.SetDirty(definition);

            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AbilityPath);
            if (ability == null)
            {
                throw new MissingReferenceException($"Mage Armor ability is missing: {AbilityPath}");
            }

            ability.SetVisualEffects(definition);
            EditorUtility.SetDirty(ability);
        }

        private static Transform CreateSection(string name, Transform parent)
        {
            GameObject section = new(name);
            section.transform.SetParent(parent, false);
            return section.transform;
        }

        private static MeshRenderer CreateShell(Transform parent, Material material)
        {
            GameObject shellObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shellObject.name = "Painted Arcane Barrier";
            shellObject.transform.SetParent(parent, false);
            Object.DestroyImmediate(shellObject.GetComponent<Collider>());
            MeshRenderer renderer = shellObject.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            ConfigureRenderer(renderer, 0);
            return renderer;
        }

        private static ParticleSystem CreateFacetSystem(string name, Transform parent, Material material, int sortingOrder)
        {
            ParticleSystem system = CreateOneShot(name, parent, material, Vector3.zero, ParticleSystemRenderMode.Billboard, sortingOrder);
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.78f;
            shape.radiusThickness = 0.08f;
            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.orbitalY = 0.85f;
            ConfigureFadeAndScale(system, 1.16f, 1f, 0.25f);
            return system;
        }

        private static ParticleSystem CreateRingSystem(string name, Transform parent, Material material, int sortingOrder)
        {
            ParticleSystem system = CreateOneShot(name, parent, material, new Vector3(0f, -0.98f, 0f), ParticleSystemRenderMode.HorizontalBillboard, sortingOrder);
            ParticleSystem.RotationOverLifetimeModule rotation = system.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = 0.7f;
            ConfigureFadeAndScale(system, 0.68f, 1.04f, 1.2f);
            return system;
        }

        private static ParticleSystem CreateSparkleSystem(Transform parent, Material material)
        {
            ParticleSystem system = CreateOneShot("Upper Body Sparkle Crown", parent, material, new Vector3(0f, 0.28f, 0f), ParticleSystemRenderMode.Billboard, 8);
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.72f;
            shape.radiusThickness = 0.12f;
            ParticleSystem.RotationOverLifetimeModule rotation = system.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-1.4f, 1.4f);
            ConfigureFadeAndScale(system, 0.25f, 1f, 0.08f);
            return system;
        }

        private static ParticleSystem CreateArcaneParticles(string name, Transform parent, Material material, bool inward, int sortingOrder)
        {
            ParticleSystem system = CreateOneShot(name, parent, material, Vector3.zero, ParticleSystemRenderMode.Billboard, sortingOrder);
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = inward ? 1f : 0.24f;
            shape.radiusThickness = inward ? 0.18f : 1f;
            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.y = new ParticleSystem.MinMaxCurve(0.08f, 0.4f);
            velocity.orbitalY = inward ? -0.9f : 0.65f;
            ConfigureFadeAndScale(system, inward ? 0.55f : 0.9f, 1f, 0.04f);
            return system;
        }

        private static ParticleSystem CreateOneShot(
            string name,
            Transform parent,
            Material material,
            Vector3 localPosition,
            ParticleSystemRenderMode renderMode,
            int sortingOrder)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            ParticleSystem system = child.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = system.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startLifetime = 0.5f;
            main.startSpeed = 0f;
            main.startSize = 0.5f;
            main.startColor = Color.white;
            main.maxParticles = 64;
            main.stopAction = ParticleSystemStopAction.None;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;

            ParticleSystemRenderer renderer = child.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = renderMode;
            renderer.alignment = ParticleSystemRenderSpace.View;
            ConfigureRenderer(renderer, sortingOrder);
            return system;
        }

        private static void ConfigureRenderer(Renderer renderer, int sortingOrder)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.allowOcclusionWhenDynamic = false;
            renderer.sortingOrder = sortingOrder;
        }

        private static void ConfigureFadeAndScale(ParticleSystem system, float startScale, float middleScale, float endScale)
        {
            ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
            color.enabled = true;
            color.color = CreateAlphaGradient(0f, 1f, 0f);
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, startScale),
                new Keyframe(0.36f, middleScale),
                new Keyframe(1f, endScale)));
        }

        private static Gradient CreateAlphaGradient(float start, float middle, float end)
        {
            Gradient gradient = new();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(start, 0f), new GradientAlphaKey(middle, 0.16f), new GradientAlphaKey(end, 1f) });
            return gradient;
        }

        private static void ValidateShader()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null || !shader.isSupported)
            {
                throw new MissingReferenceException($"Mage Armor shader is missing or unsupported: {ShaderPath}");
            }

            foreach (ShaderMessage message in ShaderUtil.GetShaderMessages(shader))
            {
                if (message.severity == ShaderCompilerMessageSeverity.Error)
                {
                    throw new UnityException($"Shader error in {ShaderPath}: {message.message}");
                }
            }
        }

        private static void ValidateAbilityWiring()
        {
            GameObject wrapper = AssetDatabase.LoadAssetAtPath<GameObject>(AbilityWrapperPath);
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(DefinitionPath);
            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AbilityPath);
            if (wrapper == null
                || wrapper.GetComponentInChildren<MageArmorApplyVFX>(true) == null
                || wrapper.GetComponent<MMOAbilityVfxLifetime>() == null
                || definition == null
                || ability == null
                || ability.VisualEffects != definition
                || definition.CastingPrefab != null
                || definition.CastPrefab != null
                || definition.HitPrefab != wrapper
                || !definition.AttachHitToTarget)
            {
                throw new MissingReferenceException("Mage Armor is not wired to its one-shot self-application VFX wrapper.");
            }
        }

        private static void ValidateLifecycle(GameObject prefab)
        {
            GameObject instance = null;
            try
            {
                instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                instance.hideFlags = HideFlags.HideAndDontSave;
                MageArmorApplyVFX controller = instance.GetComponent<MageArmorApplyVFX>();
                System.Reflection.MethodInfo awake = typeof(MageArmorApplyVFX).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                awake?.Invoke(controller, null);
                controller.Play(null);
                if (!controller.IsPlaying || controller.ReadyForPool)
                {
                    throw new UnityException("MageArmorApplyVFX did not enter its playing state.");
                }

                controller.StopImmediate();
                if (controller.IsPlaying || !controller.ReadyForPool)
                {
                    throw new UnityException("MageArmorApplyVFX did not clear into a pool-ready state.");
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
    }
}
