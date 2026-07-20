#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using RPGClone.Abilities;
using RPGClone.Vfx;
using RPGClone.Vfx.Water;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace RPGClone.EditorTools
{
    public static class WaterShieldVFXInstaller
    {
        private const int CurrentProfileAuthoringVersion = 3;
        private const string RootFolder = "Assets/_Project/VFX/WaterShield";
        private const string TextureFolder = RootFolder + "/Textures";
        private const string SourceFolder = TextureFolder + "/Sources";
        private const string MaterialFolder = RootFolder + "/Materials";
        private const string ShaderFolder = RootFolder + "/Shaders";
        private const string ProfileFolder = RootFolder + "/Profiles";
        private const string PrefabFolder = RootFolder + "/Prefabs";
        private const string ProfilePath = ProfileFolder + "/WaterShieldVFX_Default.asset";
        private const string OrbPrefabPath = PrefabFolder + "/WaterShieldOrbVFX.prefab";
        private const string ActivationPrefabPath = PrefabFolder + "/WaterShieldActivationVFX.prefab";
        private const string AbsorbPrefabPath = PrefabFolder + "/WaterShieldAbsorbReactionVFX.prefab";
        private const string ManaPrefabPath = PrefabFolder + "/WaterShieldManaRestoreVFX.prefab";
        private const string ExpirationPrefabPath = PrefabFolder + "/WaterShieldExpirationVFX.prefab";
        private const string CombinedPrefabPath = PrefabFolder + "/WaterShieldVFX.prefab";
        private const string DefinitionPath = "Assets/_Project/VFX/Definitions/Shaman_Water_Shield_VFX.asset";
        private const string AbilityPath = "Assets/_Project/Configs/Abilities/Shaman_Water_Shield.asset";

        private static readonly string[] RequiredRuntimeTextures =
        {
            "WaterShield_MainWaterPatternA.png",
            "WaterShield_MainWaterPatternB.png",
            "WaterShield_DeepWaterPattern.png",
            "WaterShield_DistortionPattern.png",
            "WaterShield_TrailRibbon.png",
            "WaterShield_SurfaceHighlights.png",
            "WaterShield_Droplets.png",
            "WaterShield_Splashes.png",
            "WaterShield_Mist.png",
            "WaterShield_FineSpray.png",
            "WaterShield_ManaEnergy.png",
            "WaterShield_ProtectiveArc.png",
            "WaterShield_ActivationRing.png",
            "WaterShield_CondensationStream.png",
            "WaterShield_ImpactSparkle.png"
        };

        private static readonly string[] RequiredMaterials =
        {
            "WaterShield_InnerWaterCore.mat",
            "WaterShield_MainWaterBody.mat",
            "WaterShield_SecondaryWaterBody.mat",
            "WaterShield_OuterWaterShell.mat",
            "WaterShield_WhiteWaterHighlights.mat",
            "WaterShield_DeepWaterShadow.mat",
            "WaterShield_WaterDistortion.mat",
            "WaterShield_MainTrailRibbon.mat",
            "WaterShield_TrailHighlight.mat",
            "WaterShield_WaterMist.mat",
            "WaterShield_Droplets.mat",
            "WaterShield_Splashes.mat",
            "WaterShield_ManaEnergy.mat",
            "WaterShield_ProtectiveWaterArc.mat"
        };

        [InitializeOnLoadMethod]
        private static void QueueInitialInstall()
        {
            if (Application.isBatchMode
                || (AssetDatabase.LoadAssetAtPath<GameObject>(CombinedPrefabPath) != null
                    && AssetDatabase.LoadAssetAtPath<GameObject>(OrbPrefabPath) != null
                    && AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/WaterShield_MainWaterBody.mat") != null))
            {
                return;
            }

            EditorApplication.delayCall += TryRunInitialInstall;
        }

        private static void TryRunInitialInstall()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryRunInitialInstall;
                return;
            }

            try
            {
                Install();
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        [MenuItem("Tools/RPG Clone/VFX/Install Water Shield VFX")]
        public static void Install()
        {
            EnsureFolders();
            DeleteLegacyPlaceholderAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureSourceAndBaseTextures();
            CreateDerivedTextures();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureDerivedTextures();

            WaterShieldVFXProfile profile = GetOrCreateProfile();
            Dictionary<string, Material> materials = CreateMaterials(profile);
            GameObject orb = CreateOrbPrefab(profile, materials);
            GameObject activation = CreateActivationPrefab(profile, materials);
            GameObject absorb = CreateReactionPrefab(profile, materials, WaterShieldReactionMode.Absorb, AbsorbPrefabPath);
            GameObject mana = CreateReactionPrefab(profile, materials, WaterShieldReactionMode.ManaRestore, ManaPrefabPath);
            GameObject expiration = CreateExpirationPrefab(profile, materials);
            GameObject combined = CreateCombinedPrefab(profile, orb, activation, absorb, mana, expiration);
            MMOAbilityVfxDefinition definition = ConfigureDefinition(combined);
            ConfigureAbility(definition);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = combined;
            EditorGUIUtility.PingObject(combined);
            Debug.Log("Installed the complete layered Water Shield VFX package and wired the ability.", combined);
        }

        [MenuItem("Tools/RPG Clone/VFX/Validate Water Shield VFX")]
        public static void ValidateBuild()
        {
            GameObject combined = AssetDatabase.LoadAssetAtPath<GameObject>(CombinedPrefabPath);
            GameObject orb = AssetDatabase.LoadAssetAtPath<GameObject>(OrbPrefabPath);
            if (combined == null || orb == null)
            {
                throw new MissingReferenceException("Water Shield combined or orb prefab is missing.");
            }

            WaterShieldVFX controller = combined.GetComponent<WaterShieldVFX>();
            WaterShieldOrbVFX orbController = orb.GetComponent<WaterShieldOrbVFX>();
            if (controller == null || controller.Profile == null || orbController == null)
            {
                throw new MissingReferenceException("Water Shield controllers or profile references are incomplete.");
            }

            WaterShieldVFXProfile profile = controller.Profile;
            float finalOrbFormationTime = profile.FirstOrbDelay + profile.OrbFormationInterval * 2f + profile.GatherDuration;
            if (profile.ActivationDuration > 0.7f || finalOrbFormationTime > 0.4f)
            {
                throw new UnityException("Water Shield activation must remain immediate: the full cast is capped at 0.7 seconds and all three orbs must form by 0.4 seconds.");
            }

            if (profile.OrbitHeight < 1f || profile.FormationPopScale < 0.2f
                || profile.FormationPopBrightness < 1f || profile.FormationSplashAmount < 10)
            {
                throw new UnityException("Water Shield must retain its chest-height orbit and authored formation impact pulse.");
            }

            foreach (string section in new[]
                     {
                         "Inner Water Core", "Main Water Body", "Secondary Water Body", "Outer Water Surface",
                         "White Water Highlights", "Deep Water Shadow", "Mana Energy Layer", "Water Refraction Shell"
                     })
            {
                if (orb.transform.Find($"Layered Orb/{section}") == null)
                {
                    throw new MissingReferenceException($"WaterShieldOrbVFX is missing its '{section}' layer.");
                }
            }

            if (orb.GetComponentsInChildren<TrailRenderer>(true).Length != 2
                || orb.GetComponentsInChildren<ParticleSystem>(true).Length != 5)
            {
                throw new UnityException("WaterShieldOrbVFX must contain two trail layers and five budgeted particle systems.");
            }

            foreach (ParticleSystem particles in orb.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (particles.main.simulationSpace != ParticleSystemSimulationSpace.World)
                {
                    throw new UnityException($"Orb particle layer '{particles.name}' must use world-space simulation.");
                }
            }

            foreach (string texture in RequiredRuntimeTextures)
            {
                if (AssetDatabase.LoadAssetAtPath<Texture>($"{TextureFolder}/{texture}") == null)
                {
                    throw new FileNotFoundException($"Required Water Shield texture is missing: {texture}");
                }
            }

            foreach (string material in RequiredMaterials)
            {
                if (AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/{material}") == null)
                {
                    throw new FileNotFoundException($"Required Water Shield material is missing: {material}");
                }
            }

            foreach (string prefabPath in new[] { ActivationPrefabPath, AbsorbPrefabPath, ManaPrefabPath, ExpirationPrefabPath })
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
                {
                    throw new FileNotFoundException($"Required Water Shield prefab is missing: {prefabPath}");
                }
            }

            if (combined.GetComponentsInChildren<Light>(true).Length != 0
                || combined.GetComponentsInChildren<Animator>(true).Length != 0
                || combined.GetComponentsInChildren<UnityEngine.Animation>(true).Length != 0)
            {
                throw new UnityException("Water Shield must remain procedural and light-free.");
            }

            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AbilityPath);
            if (ability == null || ability.VisualEffects == null || ability.VisualEffects.HitPrefab != combined)
            {
                throw new MissingReferenceException("Water Shield ability is not wired to WaterShieldVFX.prefab.");
            }

            ValidateShader("RPG Clone/VFX/Water Shield Layered Unlit");
            ValidateShader("RPG Clone/VFX/Water Shield Refraction");
            Debug.Log("Water Shield VFX validation passed: fast impact formation, chest-height orbit, generated source art, fifteen runtime textures, layered materials, reusable prefabs, persistent buff lifecycle, world-space particles, reactions, and ability wiring are valid.", combined);
        }

        private static WaterShieldVFXProfile GetOrCreateProfile()
        {
            WaterShieldVFXProfile profile = AssetDatabase.LoadAssetAtPath<WaterShieldVFXProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<WaterShieldVFXProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            ApplyProfileAuthoringMigration(profile);

            return profile;
        }

        private static void ApplyProfileAuthoringMigration(WaterShieldVFXProfile profile)
        {
            SerializedObject serialized = new(profile);
            SerializedProperty version = serialized.FindProperty("authoringVersion");
            if (version == null || version.intValue >= CurrentProfileAuthoringVersion)
            {
                return;
            }

            serialized.FindProperty("activationDuration").floatValue = 0.62f;
            serialized.FindProperty("firstOrbDelay").floatValue = 0f;
            serialized.FindProperty("orbFormationInterval").floatValue = 0.055f;
            serialized.FindProperty("gatherDuration").floatValue = 0.24f;
            serialized.FindProperty("activationFlashBrightness").floatValue = 2.65f;
            serialized.FindProperty("activationRingSize").floatValue = 2.35f;
            serialized.FindProperty("activationSweepDegrees").floatValue = 210f;
            serialized.FindProperty("formationPopScale").floatValue = 0.28f;
            serialized.FindProperty("formationPopBrightness").floatValue = 1.4f;
            serialized.FindProperty("formationSplashAmount").intValue = 14;
            serialized.FindProperty("orbitHeight").floatValue = 1.05f;
            serialized.FindProperty("orbitReactionDuration").floatValue = 0.48f;
            serialized.FindProperty("orbitReactionSpinDegrees").floatValue = 360f;
            version.intValue = CurrentProfileAuthoringVersion;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
        }

        private static Dictionary<string, Material> CreateMaterials(WaterShieldVFXProfile profile)
        {
            Shader layered = Shader.Find("RPG Clone/VFX/Water Shield Layered Unlit");
            Shader refraction = Shader.Find("RPG Clone/VFX/Water Shield Refraction");
            if (layered == null || refraction == null)
            {
                throw new System.InvalidOperationException("Water Shield shaders did not import. Resolve shader compilation errors before installing.");
            }

            Texture2D patternA = LoadTexture("WaterShield_MainWaterPatternA.png");
            Texture2D patternB = LoadTexture("WaterShield_MainWaterPatternB.png");
            Texture2D deep = LoadTexture("WaterShield_DeepWaterPattern.png");
            Texture2D distortion = LoadTexture("WaterShield_DistortionPattern.png");
            Texture2D highlights = LoadTexture("WaterShield_SurfaceHighlights.png");
            Texture2D trail = LoadTexture("WaterShield_TrailRibbon.png");
            Texture2D droplets = LoadTexture("WaterShield_Droplets.png");
            Texture2D splashes = LoadTexture("WaterShield_Splashes.png");
            Texture2D mist = LoadTexture("WaterShield_Mist.png");
            Texture2D spray = LoadTexture("WaterShield_FineSpray.png");
            Texture2D mana = LoadTexture("WaterShield_ManaEnergy.png");
            Texture2D arc = LoadTexture("WaterShield_ProtectiveArc.png");
            Texture2D ring = LoadTexture("WaterShield_ActivationRing.png");
            Texture2D stream = LoadTexture("WaterShield_CondensationStream.png");
            Texture2D sparkle = LoadTexture("WaterShield_ImpactSparkle.png");

            return new Dictionary<string, Material>
            {
                ["InnerCore"] = CreateMaterial("WaterShield_InnerWaterCore", layered, patternA, patternB, distortion, profile.Colors.PaleCyan, profile.Colors.Aqua, true, 1.8f, 0.82f, 0.7f),
                ["MainBody"] = CreateMaterial("WaterShield_MainWaterBody", layered, patternA, patternB, distortion, profile.Colors.ClearBlue, profile.Colors.Aqua, false, 1.05f, 0.72f, 0.55f),
                ["SecondaryBody"] = CreateMaterial("WaterShield_SecondaryWaterBody", layered, patternB, patternA, distortion, profile.Colors.Teal, profile.Colors.ClearBlue, true, 0.85f, 0.45f, 0.62f),
                ["OuterShell"] = CreateMaterial("WaterShield_OuterWaterShell", layered, patternB, highlights, distortion, profile.Colors.Aqua, profile.Colors.PaleCyan, true, 1.2f, 0.42f, 0.58f),
                ["Highlights"] = CreateMaterial("WaterShield_WhiteWaterHighlights", layered, highlights, highlights, distortion, profile.Colors.WhiteHighlight, profile.Colors.PaleCyan, true, 1.7f, 0.72f, 0.38f),
                ["DeepShadow"] = CreateMaterial("WaterShield_DeepWaterShadow", layered, deep, patternA, distortion, profile.Colors.DeepBlue, profile.Colors.ClearBlue, false, 0.72f, 0.58f, 0.25f),
                ["Distortion"] = CreateMaterial("WaterShield_WaterDistortion", refraction, patternA, patternB, distortion, Color.white, Color.white, false, 1f, 0.28f, 0.2f),
                ["MainTrail"] = CreateMaterial("WaterShield_MainTrailRibbon", layered, trail, trail, distortion, Color.white, profile.Colors.Aqua, false, 1.15f, 0.78f, 0.18f),
                ["TrailHighlight"] = CreateMaterial("WaterShield_TrailHighlight", layered, trail, highlights, distortion, profile.Colors.WhiteHighlight, profile.Colors.PaleCyan, true, 1.65f, 0.9f, 0.35f),
                ["Mist"] = CreateMaterial("WaterShield_WaterMist", layered, mist, mist, distortion, profile.Colors.Mist, profile.Colors.PaleCyan, false, 0.72f, 0.48f, 0.1f),
                ["Droplets"] = CreateMaterial("WaterShield_Droplets", layered, droplets, droplets, distortion, Color.white, profile.Colors.Aqua, true, 1.25f, 0.92f, 0.2f),
                ["Splashes"] = CreateMaterial("WaterShield_Splashes", layered, splashes, splashes, distortion, Color.white, profile.Colors.PaleCyan, true, 1.35f, 0.9f, 0.22f),
                ["FineSpray"] = CreateMaterial("WaterShield_FineSpray", layered, spray, spray, distortion, profile.Colors.PaleCyan, Color.white, true, 1.2f, 0.78f, 0.16f),
                ["Mana"] = CreateMaterial("WaterShield_ManaEnergy", layered, mana, mana, distortion, profile.Colors.Aqua, profile.Colors.ManaViolet, true, 1.65f, 0.9f, 0.35f),
                ["ProtectiveArc"] = CreateMaterial("WaterShield_ProtectiveWaterArc", layered, arc, arc, distortion, Color.white, profile.Colors.Aqua, true, 1.55f, 0.92f, 0.28f),
                ["ActivationRing"] = CreateMaterial("WaterShield_ActivationRing", layered, ring, ring, distortion, Color.white, profile.Colors.Aqua, true, 1.45f, 0.9f, 0.22f),
                ["Condensation"] = CreateMaterial("WaterShield_CondensationStream", layered, stream, stream, distortion, Color.white, profile.Colors.PaleCyan, true, 1.35f, 0.88f, 0.22f),
                ["Sparkle"] = CreateMaterial("WaterShield_ImpactSparkle", layered, sparkle, sparkle, distortion, Color.white, profile.Colors.Aqua, true, 1.9f, 1f, 0.2f)
            };
        }

        private static Material CreateMaterial(string name, Shader shader, Texture primary, Texture secondary, Texture distortion, Color tint, Color secondaryTint, bool additive, float brightness, float opacity, float secondaryMix)
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

            SetTexture(material, "_BaseMap", primary);
            SetTexture(material, "_SecondaryMap", secondary != null ? secondary : primary);
            SetTexture(material, "_DistortionMap", distortion != null ? distortion : primary);
            SetColor(material, "_Tint", tint);
            SetColor(material, "_SecondaryTint", secondaryTint);
            SetFloat(material, "_Opacity", opacity);
            SetFloat(material, "_Brightness", brightness);
            SetFloat(material, "_SecondaryMix", secondaryMix);
            SetFloat(material, "_DistortionStrength", 0.025f);
            SetFloat(material, "_WobbleAmount", 0.035f);
            SetFloat(material, "_WobbleSpeed", 2.4f);
            SetFloat(material, "_Dissolve", 0f);
            SetFloat(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
            SetFloat(material, "_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreateOrbPrefab(WaterShieldVFXProfile profile, IReadOnlyDictionary<string, Material> materials)
        {
            GameObject root = new("WaterShieldOrbVFX");
            WaterShieldOrbVFX controller = root.AddComponent<WaterShieldOrbVFX>();
            Transform layeredRoot = CreateChild("Layered Orb", root.transform);
            Renderer deep = CreateSphere("Deep Water Shadow", layeredRoot, materials["DeepShadow"], Vector3.one * 0.84f);
            Renderer mana = CreateSphere("Mana Energy Layer", layeredRoot, materials["Mana"], Vector3.one * 0.68f);
            Renderer core = CreateSphere("Inner Water Core", layeredRoot, materials["InnerCore"], Vector3.one * 0.58f);
            Renderer secondary = CreateSphere("Secondary Water Body", layeredRoot, materials["SecondaryBody"], Vector3.one * 0.96f);
            Renderer body = CreateSphere("Main Water Body", layeredRoot, materials["MainBody"], Vector3.one);
            Renderer outer = CreateSphere("Outer Water Surface", layeredRoot, materials["OuterShell"], Vector3.one * 1.055f);
            Renderer highlights = CreateSphere("White Water Highlights", layeredRoot, materials["Highlights"], Vector3.one * 1.075f);
            Renderer distortion = CreateSphere("Water Refraction Shell", layeredRoot, materials["Distortion"], Vector3.one * 1.095f);

            TrailRenderer mainTrail = CreateTrail("Main Water Wake", root.transform, materials["MainTrail"], profile.TrailLength, profile.TrailWidth, profile.TrailOpacity, profile.Colors.ClearBlue);
            TrailRenderer highlightTrail = CreateTrail("White Cyan Wake Highlight", root.transform, materials["TrailHighlight"], profile.TrailLength * 0.78f, profile.HighlightTrailWidth, 0.9f, profile.Colors.WhiteHighlight);
            ParticleSystem droplets = CreateParticleSystem("World Space Water Droplets", root.transform, materials["Droplets"], true, true, 0.55f, profile.DropletSpawnRate, 0.16f, profile.DropletSize, profile.DropletSpeed, ParticleSystemShapeType.Sphere, 2, 1);
            ParticleSystem spray = CreateParticleSystem("World Space Fine Spray", root.transform, materials["FineSpray"], true, true, 0.38f, profile.FineSprayAmount, 0.17f, 0.045f, 0.36f, ParticleSystemShapeType.Sphere, 1, 1);
            ParticleSystem mist = CreateParticleSystem("World Space Wake Mist", root.transform, materials["Mist"], true, true, 0.62f, profile.MistAmount, 0.12f, 0.13f, 0.12f, ParticleSystemShapeType.Sphere, 1, 1);
            ParticleSystem motes = CreateParticleSystem("World Space Mana Motes", root.transform, materials["Mana"], true, true, 0.9f, profile.WaterMoteCount * 0.55f, 0.2f, 0.055f, 0.08f, ParticleSystemShapeType.Sphere, 2, 1);
            ParticleSystem splashes = CreateParticleSystem("Surface Splash Accents", root.transform, materials["Splashes"], true, true, 0.38f, profile.SplashFrequency, 0.13f, profile.SplashSize, 0.32f, ParticleSystemShapeType.Sphere, 2, 2);

            controller.ConfigureAuthoring(profile, core, body, secondary, outer, highlights, deep, mana, distortion, mainTrail, highlightTrail, droplets, spray, mist, motes, splashes);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, OrbPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateActivationPrefab(WaterShieldVFXProfile profile, IReadOnlyDictionary<string, Material> materials)
        {
            GameObject root = new("WaterShieldActivationVFX");
            WaterShieldActivationVFX controller = root.AddComponent<WaterShieldActivationVFX>();
            Renderer flash = CreateQuad("Bright Cyan Torso Flash", root.transform, materials["Sparkle"], new Vector3(0f, 0.25f, 0.2f), Quaternion.identity, Vector3.one);
            Renderer ring = CreateQuad("Expanding Watery Ring", root.transform, materials["ActivationRing"], new Vector3(0f, -0.38f, 0f), Quaternion.Euler(90f, 0f, 0f), Vector3.one);
            ParticleSystem splash = CreateParticleSystem("Circular Summoning Splash", root.transform, materials["Splashes"], false, true, 0.55f, 0f, 0.48f, 0.2f, 0.82f, ParticleSystemShapeType.Circle, 2, 2);
            ParticleSystem mist = CreateParticleSystem("Activation Mist", root.transform, materials["Mist"], false, true, 0.72f, 0f, 0.55f, 0.32f, 0.18f, ParticleSystemShapeType.Sphere, 1, 1);
            ParticleSystem sparkles = CreateParticleSystem("Blue White Sparkles", root.transform, materials["Sparkle"], false, true, 0.45f, 0f, 0.62f, 0.12f, 0.52f, ParticleSystemShapeType.Sphere, 1, 1);
            ParticleSystem atmosphere = CreateParticleSystem("Atmospheric Water Collection", root.transform, materials["Droplets"], true, true, 10f, 0f, 0.1f, profile.DropletSize, 0f, ParticleSystemShapeType.Sphere, 2, 1);
            ParticleSystem.EmissionModule atmosphereEmission = atmosphere.emission;
            atmosphereEmission.enabled = false;
            ParticleSystem.MainModule atmosphereMain = atmosphere.main;
            atmosphereMain.maxParticles = profile.DropletsPerOrb * 3 + 8;
            atmosphereMain.startLifetime = 10f;

            LineRenderer[] streams = new LineRenderer[3];
            for (int i = 0; i < streams.Length; i++)
            {
                streams[i] = CreateLine($"Condensation Stream {i + 1}", root.transform, materials["Condensation"], 11, 0.075f, true);
                streams[i].enabled = false;
            }

            ConfigureBurst(splash, 18, 0.82f, 0.2f);
            ConfigureBurst(mist, 12, 0.16f, 0.32f);
            ConfigureBurst(sparkles, 15, 0.52f, 0.12f);
            controller.ConfigureAuthoring(profile, true, flash, ring, splash, mist, sparkles, atmosphere, streams);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ActivationPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateReactionPrefab(WaterShieldVFXProfile profile, IReadOnlyDictionary<string, Material> materials, WaterShieldReactionMode mode, string path)
        {
            string name = mode == WaterShieldReactionMode.Absorb ? "WaterShieldAbsorbReactionVFX" : "WaterShieldManaRestoreVFX";
            GameObject root = new(name);
            WaterShieldReactionVFX controller = root.AddComponent<WaterShieldReactionVFX>();
            Renderer arc = null;
            Renderer chest = null;
            LineRenderer stream = null;
            ParticleSystem splash = null;
            ParticleSystem droplets = null;
            ParticleSystem motes = null;

            if (mode == WaterShieldReactionMode.Absorb)
            {
                arc = CreateQuad("Protective Water Arc", root.transform, materials["ProtectiveArc"], Vector3.zero, Quaternion.identity, Vector3.one);
                splash = CreateParticleSystem("Reactive Water Splash", root.transform, materials["Splashes"], false, true, 0.42f, 0f, 0.22f, 0.24f, 0.82f, ParticleSystemShapeType.Hemisphere, 2, 2);
                droplets = CreateParticleSystem("Detached Impact Droplets", root.transform, materials["Droplets"], false, true, 0.52f, 0f, 0.28f, 0.07f, 1.05f, ParticleSystemShapeType.Hemisphere, 2, 1);
                ConfigureBurst(splash, profile.ReactiveSplashAmount, 0.82f, 0.24f);
                ConfigureBurst(droplets, Mathf.Max(6, profile.ReactiveSplashAmount), 1.05f, 0.07f);
            }
            else
            {
                chest = CreateQuad("Caster Chest Mana Pulse", root.transform, materials["Mana"], Vector3.zero, Quaternion.identity, Vector3.one * 0.1f);
                stream = CreateLine("Curved Mana Transfer", root.transform, materials["Mana"], 13, 0.065f, true);
                motes = CreateParticleSystem("Inward Mana Motes", root.transform, materials["Mana"], false, true, 0.62f, 0f, 0.22f, 0.075f, 0.65f, ParticleSystemShapeType.Sphere, 2, 1);
                ConfigureBurst(motes, 11, 0.65f, 0.075f);
            }

            controller.ConfigureAuthoring(profile, mode, false, arc, chest, stream, splash, droplets, motes);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateExpirationPrefab(WaterShieldVFXProfile profile, IReadOnlyDictionary<string, Material> materials)
        {
            GameObject root = new("WaterShieldExpirationVFX");
            ParticleSystem collapse = CreateParticleSystem("Collapsing Water Streams", root.transform, materials["Condensation"], false, true, profile.ExpirationDuration, 0f, 0.48f, 0.22f, 0.42f, ParticleSystemShapeType.Sphere, 1, 1);
            ParticleSystem droplets = CreateParticleSystem("Final Water Droplets", root.transform, materials["Droplets"], false, true, profile.ExpirationDuration, 0f, 0.6f, 0.08f, 0.62f, ParticleSystemShapeType.Sphere, 2, 1);
            ParticleSystem mana = CreateParticleSystem("Blue White Dissolve", root.transform, materials["Mana"], false, true, profile.ExpirationDuration, 0f, 0.45f, 0.11f, 0.28f, ParticleSystemShapeType.Sphere, 2, 1);
            ConfigureBurst(collapse, 12, 0.42f, 0.22f);
            ConfigureBurst(droplets, 18, 0.62f, 0.08f);
            ConfigureBurst(mana, 14, 0.28f, 0.11f);
            MMOAbilityVfxLifetime lifetime = root.AddComponent<MMOAbilityVfxLifetime>();
            lifetime.Configure(profile.ExpirationDuration + 0.3f, true, true);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ExpirationPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateCombinedPrefab(WaterShieldVFXProfile profile, GameObject orb, GameObject activation, GameObject absorb, GameObject mana, GameObject expiration)
        {
            GameObject root = new("WaterShieldVFX");
            WaterShieldVFX controller = root.AddComponent<WaterShieldVFX>();
            Transform formation = CreateChild("Persistent Three Orb Formation", root.transform);
            controller.ConfigureAuthoring(profile, orb, activation, absorb, mana, expiration, true, formation);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, CombinedPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static MMOAbilityVfxDefinition ConfigureDefinition(GameObject combined)
        {
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(DefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<MMOAbilityVfxDefinition>();
                AssetDatabase.CreateAsset(definition, DefinitionPath);
            }

            definition.Configure(null, null, combined, true, false, true, false, Vector3.zero, Vector3.zero, new Vector3(0f, 1.05f, 0f), Vector3.zero, 0.02f, false);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static void ConfigureAbility(MMOAbilityVfxDefinition definition)
        {
            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AbilityPath);
            if (ability == null)
            {
                Debug.LogWarning($"Water Shield ability was not found at {AbilityPath}; the VFX definition is ready for manual assignment.");
                return;
            }

            ability.SetVisualEffects(definition);
            EditorUtility.SetDirty(ability);
        }

        private static Renderer CreateSphere(string name, Transform parent, Material material, Vector3 scale)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = name;
            sphere.transform.SetParent(parent, false);
            sphere.transform.localScale = scale;
            Collider collider = sphere.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
            MeshRenderer renderer = sphere.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        private static Renderer CreateQuad(string name, Transform parent, Material material, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = position;
            quad.transform.localRotation = rotation;
            quad.transform.localScale = scale;
            Collider collider = quad.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
            MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        private static TrailRenderer CreateTrail(string name, Transform parent, Material material, float time, float width, float opacity, Color endColor)
        {
            Transform child = CreateChild(name, parent);
            TrailRenderer trail = child.gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = material;
            trail.time = time;
            trail.widthMultiplier = width;
            trail.minVertexDistance = 0.022f;
            trail.numCornerVertices = 3;
            trail.numCapVertices = 2;
            trail.textureMode = LineTextureMode.Tile;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.emitting = false;
            Color start = Color.white;
            start.a = opacity;
            endColor.a = 0f;
            trail.colorGradient = CreateFadeGradient(start, endColor);
            return trail;
        }

        private static LineRenderer CreateLine(string name, Transform parent, Material material, int points, float width, bool worldSpace)
        {
            Transform child = CreateChild(name, parent);
            LineRenderer line = child.gameObject.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = worldSpace;
            line.positionCount = points;
            line.widthMultiplier = width;
            line.textureMode = LineTextureMode.Tile;
            line.numCornerVertices = 3;
            line.numCapVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private static ParticleSystem CreateParticleSystem(string name, Transform parent, Material material, bool loop, bool worldSpace, float lifetime, float rate, float radius, float size, float speed, ParticleSystemShapeType shapeType, int tilesX, int tilesY)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            ParticleSystem particles = child.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = loop;
            main.duration = Mathf.Max(0.1f, lifetime);
            main.playOnAwake = false;
            main.simulationSpace = worldSpace ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.62f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.55f, speed * 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.58f, size * 1.2f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            main.maxParticles = Mathf.Max(48, Mathf.CeilToInt(Mathf.Max(1f, rate) * lifetime * 2.5f));
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = loop ? rate : 0f;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = shapeType;
            shape.radius = radius;
            shape.radiusThickness = 0.45f;
            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = CreateFadeGradient(Color.white, new Color(0.15f, 0.65f, 1f, 0f));
            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.35f), new Keyframe(0.16f, 1f), new Keyframe(0.75f, 0.72f), new Keyframe(1f, 0.05f)));
            ParticleSystem.RotationOverLifetimeModule rotation = particles.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-1.6f, 1.6f);
            if (tilesX > 1 || tilesY > 1)
            {
                ParticleSystem.TextureSheetAnimationModule sheet = particles.textureSheetAnimation;
                sheet.enabled = true;
                sheet.mode = ParticleSystemAnimationMode.Grid;
                sheet.numTilesX = tilesX;
                sheet.numTilesY = tilesY;
                sheet.animation = ParticleSystemAnimationType.WholeSheet;
                sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f, 1f);
                sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 0.999f);
                sheet.cycleCount = 1;
            }

            ParticleSystemRenderer renderer = child.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = name.Contains("Droplet") || name.Contains("Spray") ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            renderer.velocityScale = 0.1f;
            renderer.lengthScale = 1.4f;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.minParticleSize = 0f;
            renderer.maxParticleSize = 0.2f;
            return particles;
        }

        private static void ConfigureBurst(ParticleSystem particles, int count, float speed, float size)
        {
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(count, 0, short.MaxValue)) });
            ParticleSystem.MainModule main = particles.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.55f, speed * 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.58f, size * 1.2f);
        }

        private static Gradient CreateFadeGradient(Color start, Color end)
        {
            Gradient gradient = new();
            gradient.SetKeys(
                new[] { new GradientColorKey(start, 0f), new GradientColorKey(start, 0.38f), new GradientColorKey(end, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(start.a, 0.1f), new GradientAlphaKey(end.a, 0.74f), new GradientAlphaKey(0f, 1f) });
            return gradient;
        }

        private static Transform CreateChild(string name, Transform parent)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static void ConfigureSourceAndBaseTextures()
        {
            foreach (string file in Directory.GetFiles(SourceFolder, "*.png"))
            {
                ConfigureTextureImporter(file.Replace("\\", "/"), false, false, file.Contains("Pattern") || file.Contains("Trail"));
            }

            foreach (string name in new[]
                     {
                         "WaterShield_MainWaterPatternA.png", "WaterShield_MainWaterPatternB.png", "WaterShield_DeepWaterPattern.png",
                         "WaterShield_DistortionPattern.png", "WaterShield_TrailRibbon.png", "WaterShield_SpriteAtlas.png"
                     })
            {
                bool repeat = name.Contains("Pattern") || name.Contains("Trail");
                ConfigureTextureImporter($"{TextureFolder}/{name}", false, false, repeat);
            }
        }

        private static void ConfigureDerivedTextures()
        {
            foreach (string name in RequiredRuntimeTextures)
            {
                bool repeat = name.Contains("Pattern") || name.Contains("TrailRibbon");
                ConfigureTextureImporter($"{TextureFolder}/{name}", false, false, repeat);
            }
        }

        private static void ConfigureTextureImporter(string path, bool readable, bool sprite, bool repeat)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                throw new FileNotFoundException($"Required Water Shield texture is missing: {path}");
            }

            importer.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            importer.spriteImportMode = sprite ? SpriteImportMode.Single : SpriteImportMode.None;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.isReadable = readable;
            importer.mipmapEnabled = true;
            importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            // Alpha-block compression creates visible square/diamond ghosts around the small
            // atlas cutouts. Keep clamped VFX masks lossless; only tileable body/trail art is
            // large enough to benefit materially from runtime texture compression.
            importer.textureCompression = repeat
                ? TextureImporterCompression.CompressedHQ
                : TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        private static void CreateDerivedTextures()
        {
            string atlasPath = $"{TextureFolder}/WaterShield_SpriteAtlas.png";
            if (!File.Exists(atlasPath))
            {
                throw new FileNotFoundException("WaterShield_SpriteAtlas is missing before deriving runtime textures.", atlasPath);
            }

            // Read the authored PNG bytes directly. Reading back Unity's imported NPOT texture
            // resamples the chroma-key alpha and creates obvious geometric garbage in the
            // cutouts, especially around droplets and highlights.
            Texture2D atlas = new(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!atlas.LoadImage(File.ReadAllBytes(atlasPath), false))
                {
                    throw new System.InvalidOperationException("WaterShield_SpriteAtlas could not be decoded as PNG data.");
                }

                WriteTexture("WaterShield_SurfaceHighlights.png", CombineCells(atlas, new[] { new Cell(0, 0), new Cell(1, 0) }, 2, 1));
                WriteTexture("WaterShield_Droplets.png", CombineCells(atlas, new[] { new Cell(2, 0), new Cell(3, 0) }, 2, 1));
                WriteTexture("WaterShield_Splashes.png", CombineCells(atlas, new[] { new Cell(0, 1), new Cell(1, 1), new Cell(2, 1), new Cell(3, 1) }, 2, 2));
                WriteTexture("WaterShield_Mist.png", CombineCells(atlas, new[] { new Cell(0, 2) }, 1, 1));
                WriteTexture("WaterShield_FineSpray.png", CombineCells(atlas, new[] { new Cell(1, 2) }, 1, 1));
                WriteTexture("WaterShield_ManaEnergy.png", CombineCells(atlas, new[] { new Cell(2, 2), new Cell(3, 2) }, 2, 1));
                WriteTexture("WaterShield_ProtectiveArc.png", CombineCells(atlas, new[] { new Cell(0, 3) }, 1, 1));
                WriteTexture("WaterShield_ActivationRing.png", CombineCells(atlas, new[] { new Cell(1, 3) }, 1, 1));
                WriteTexture("WaterShield_CondensationStream.png", CombineCells(atlas, new[] { new Cell(2, 3) }, 1, 1));
                WriteTexture("WaterShield_ImpactSparkle.png", CombineCells(atlas, new[] { new Cell(3, 3) }, 1, 1));
            }
            finally
            {
                Object.DestroyImmediate(atlas);
            }
        }

        private static Texture2D CombineCells(Texture2D source, IReadOnlyList<Cell> cells, int columns, int rows)
        {
            int cellWidth = source.width / 4;
            int cellHeight = source.height / 4;
            Texture2D output = new(cellWidth * columns, cellHeight * rows, TextureFormat.RGBA32, false);
            Color32[] outputPixels = new Color32[output.width * output.height];
            for (int index = 0; index < cells.Count; index++)
            {
                Cell cell = cells[index];
                int sourceY = (3 - cell.RowFromTop) * cellHeight;
                Color32[] pixels = source.GetPixels32(0);
                int destinationColumn = index % columns;
                int destinationRowFromTop = index / columns;
                int destinationY = (rows - 1 - destinationRowFromTop) * cellHeight;
                for (int y = 0; y < cellHeight; y++)
                {
                    for (int x = 0; x < cellWidth; x++)
                    {
                        int sourceIndex = (sourceY + y) * source.width + cell.Column * cellWidth + x;
                        int destinationIndex = (destinationY + y) * output.width + destinationColumn * cellWidth + x;
                        outputPixels[destinationIndex] = pixels[sourceIndex];
                    }
                }
            }

            output.SetPixels32(outputPixels);
            output.Apply(false, false);
            return output;
        }

        private static void WriteTexture(string fileName, Texture2D texture)
        {
            File.WriteAllBytes($"{TextureFolder}/{fileName}", texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        private static Texture2D LoadTexture(string fileName)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureFolder}/{fileName}");
            if (texture == null) throw new FileNotFoundException($"Required Water Shield texture is missing: {fileName}");
            return texture;
        }

        private static void ValidateShader(string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null || !shader.isSupported)
            {
                throw new UnityException($"Water Shield shader is missing or has compile errors: {shaderName}");
            }

            foreach (ShaderMessage message in ShaderUtil.GetShaderMessages(shader))
            {
                if (message.severity == ShaderCompilerMessageSeverity.Error)
                {
                    throw new UnityException($"Shader error in {shaderName}: {message.message}");
                }
            }
        }

        private static void DeleteLegacyPlaceholderAssets()
        {
            foreach (string path in new[]
                     {
                         MaterialFolder + "/WaterShield_AtmosphericDroplets.mat",
                         MaterialFolder + "/WaterShield_FoamTrail.mat",
                         MaterialFolder + "/WaterShield_OrbBody.mat",
                         MaterialFolder + "/WaterShield_OrbCore.mat"
                     })
            {
                if (AssetDatabase.LoadAssetAtPath<Object>(path) != null) AssetDatabase.DeleteAsset(path);
            }
        }

        private static void EnsureFolders()
        {
            foreach (string path in new[] { RootFolder, TextureFolder, SourceFolder, MaterialFolder, ShaderFolder, ProfileFolder, PrefabFolder, RootFolder + "/Documentation" })
            {
                EnsureFolder(path);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            string folder = Path.GetFileName(path);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, folder);
            }
        }

        private static void SetTexture(Material material, string property, Texture value)
        {
            if (material.HasProperty(property)) material.SetTexture(property, value);
        }

        private static void SetColor(Material material, string property, Color value)
        {
            if (material.HasProperty(property)) material.SetColor(property, value);
        }

        private static void SetFloat(Material material, string property, float value)
        {
            if (material.HasProperty(property)) material.SetFloat(property, value);
        }

        private readonly struct Cell
        {
            public Cell(int column, int rowFromTop)
            {
                Column = column;
                RowFromTop = rowFromTop;
            }

            public int Column { get; }
            public int RowFromTop { get; }
        }
    }
}
#endif
