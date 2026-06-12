using System.IO;
using RPGClone.World.Atmosphere;
using RPGClone.World.Foliage;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace RPGClone.EditorTools
{
    public static class MMOOrcishValleyVisualPolishInstaller
    {
        private const string StarterScenePath = "Assets/Scenes/OrcishStarterValley.unity";
        private const string SettingsFolder = "Assets/Settings";
        private const string WorldConfigFolder = "Assets/_Project/Configs/World";
        private const string AtmosphereConfigFolder = WorldConfigFolder + "/Atmosphere";
        private const string GeneratedMaterialsFolder = "Assets/_Project/Generated/Materials";
        private const string PcRenderPipelineAssetPath = SettingsFolder + "/PC_RPAsset.asset";
        private const string PcRendererPath = SettingsFolder + "/PC_Renderer.asset";
        private const string PolishVolumeProfilePath = SettingsFolder + "/OrcishStarterValley_PolishProfile.asset";
        private const string DustProfilePath = AtmosphereConfigFolder + "/OrcishValley_DustProfile.asset";
        private const string DustMaterialPath = GeneratedMaterialsFolder + "/Ambient_Dust_Mote.mat";
        private const string GrassProfilePath = WorldConfigFolder + "/Foliage/ClassicGrassFoliageProfile.asset";

        [MenuItem("Tools/RPG Clone/Apply Orcish Valley Visual Polish")]
        public static void ApplyToActiveScene()
        {
            ApplyPolish(SceneManager.GetActiveScene());
        }

        [MenuItem("Tools/RPG Clone/Apply Orcish Valley Visual Polish To Starter Scene")]
        public static void ApplyToStarterScene()
        {
            Scene scene = EditorSceneManager.OpenScene(StarterScenePath, OpenSceneMode.Single);
            ApplyPolish(scene);
        }

        private static void ApplyPolish(Scene scene)
        {
            EnsureFolders();
            ConfigureRenderPipelineAsset();
            ConfigureScreenSpaceAmbientOcclusion();
            ConfigureLighting();
            ConfigureCamera();
            ConfigureGlobalVolume();
            ConfigureAmbientDust();
            ConfigureGrass();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Applied Orcish Starter Valley visual polish: long-range shadows, warm/cool lighting, restrained post processing, ambient dust, and shadow-aware grass.");
        }

        private static void ConfigureRenderPipelineAsset()
        {
            UniversalRenderPipelineAsset asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PcRenderPipelineAssetPath);
            if (asset == null)
            {
                Debug.LogWarning($"URP asset was not found at {PcRenderPipelineAssetPath}.");
                return;
            }

            SerializedObject serializedAsset = new(asset);
            SetSerializedFloat(serializedAsset, "m_ShadowDistance", 560f);
            SetSerializedInt(serializedAsset, "m_ShadowCascadeCount", 4);
            SetSerializedInt(serializedAsset, "m_MainLightShadowmapResolution", 4096);
            SetSerializedInt(serializedAsset, "m_AdditionalLightsShadowmapResolution", 2048);
            SetSerializedVector3(serializedAsset, "m_Cascade4Split", new Vector3(0.045f, 0.16f, 0.46f));
            SetSerializedFloat(serializedAsset, "m_CascadeBorder", 0.08f);
            SetSerializedFloat(serializedAsset, "m_ShadowDepthBias", 0.085f);
            SetSerializedFloat(serializedAsset, "m_ShadowNormalBias", 0.42f);
            SetSerializedInt(serializedAsset, "m_SoftShadowQuality", 3);
            SetSerializedInt(serializedAsset, "m_ColorGradingMode", 1);
            serializedAsset.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        private static void ConfigureScreenSpaceAmbientOcclusion()
        {
            ScriptableObject rendererData = AssetDatabase.LoadAssetAtPath<ScriptableObject>(PcRendererPath);
            if (rendererData == null)
            {
                Debug.LogWarning($"URP renderer data was not found at {PcRendererPath}.");
                return;
            }

            SerializedObject serializedRenderer = new(rendererData);
            SerializedProperty features = serializedRenderer.FindProperty("m_RendererFeatures");
            if (features == null || !features.isArray)
            {
                return;
            }

            for (int i = 0; i < features.arraySize; i++)
            {
                Object feature = features.GetArrayElementAtIndex(i).objectReferenceValue;
                if (feature == null || !feature.name.Contains("AmbientOcclusion"))
                {
                    continue;
                }

                SerializedObject serializedFeature = new(feature);
                SetSerializedFloat(serializedFeature, "m_Settings.Intensity", 1.05f);
                SetSerializedFloat(serializedFeature, "m_Settings.DirectLightingStrength", 0.18f);
                SetSerializedFloat(serializedFeature, "m_Settings.Radius", 0.42f);
                SetSerializedFloat(serializedFeature, "m_Settings.Falloff", 140f);
                SetSerializedBool(serializedFeature, "m_Settings.Downsample", true);
                SetSerializedInt(serializedFeature, "m_Settings.Samples", 2);
                SetSerializedInt(serializedFeature, "m_Settings.BlurQuality", 1);
                serializedFeature.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(feature);
            }
        }

        private static void ConfigureLighting()
        {
            Light sun = FindOrCreateSun();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.78f, 0.52f, 1f);
            sun.intensity = 1.24f;
            sun.bounceIntensity = 1.08f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.82f;
            sun.shadowBias = 0.04f;
            sun.shadowNormalBias = 0.32f;
            sun.transform.rotation = Quaternion.Euler(42f, 318f, 0f);
            RenderSettings.sun = sun;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.46f, 0.53f, 0.68f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.36f, 0.34f, 0.32f, 1f);
            RenderSettings.ambientGroundColor = new Color(0.16f, 0.14f, 0.13f, 1f);
            RenderSettings.ambientIntensity = 0.86f;
            RenderSettings.subtractiveShadowColor = new Color(0.34f, 0.42f, 0.58f, 1f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.62f, 0.48f, 0.34f, 1f);
            RenderSettings.fogStartDistance = 70f;
            RenderSettings.fogEndDistance = 560f;

            if (RenderSettings.skybox != null)
            {
                SetMaterialColor(RenderSettings.skybox, "_SkyTint", new Color(0.58f, 0.62f, 0.72f, 1f));
                SetMaterialColor(RenderSettings.skybox, "_GroundColor", new Color(0.42f, 0.32f, 0.24f, 1f));
                SetMaterialFloat(RenderSettings.skybox, "_AtmosphereThickness", 1.18f);
                SetMaterialFloat(RenderSettings.skybox, "_Exposure", 1.16f);
                SetMaterialFloat(RenderSettings.skybox, "_SunSize", 0.055f);
            }
        }

        private static void ConfigureCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            camera.farClipPlane = Mathf.Max(camera.farClipPlane, 900f);
            camera.allowHDR = true;
            camera.allowMSAA = false;

            UniversalAdditionalCameraData cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null)
            {
                cameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }

            cameraData.renderPostProcessing = true;
            cameraData.requiresDepthTexture = true;
            cameraData.requiresColorTexture = true;
            EditorUtility.SetDirty(camera);
            EditorUtility.SetDirty(cameraData);
        }

        private static void ConfigureGlobalVolume()
        {
            Volume volume = Object.FindAnyObjectByType<Volume>();
            if (volume == null)
            {
                GameObject volumeObject = new("Global Volume");
                volume = volumeObject.AddComponent<Volume>();
            }

            VolumeProfile profile = CreateOrResetVolumeProfile();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.weight = 1f;
            volume.sharedProfile = profile;
            EditorUtility.SetDirty(volume);
        }

        private static VolumeProfile CreateOrResetVolumeProfile()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PolishVolumeProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = Path.GetFileNameWithoutExtension(PolishVolumeProfilePath);
                AssetDatabase.CreateAsset(profile, PolishVolumeProfilePath);
            }

            Object[] existingSubAssets = AssetDatabase.LoadAllAssetsAtPath(PolishVolumeProfilePath);
            foreach (Object asset in existingSubAssets)
            {
                if (asset is VolumeComponent)
                {
                    Object.DestroyImmediate(asset, true);
                }
            }

            for (int i = profile.components.Count - 1; i >= 0; i--)
            {
                VolumeComponent component = profile.components[i];
                if (component != null)
                {
                    Object.DestroyImmediate(component, true);
                }
            }

            profile.components.Clear();

            Tonemapping tonemapping = AddPersistentVolumeComponent<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.Neutral);

            WhiteBalance whiteBalance = AddPersistentVolumeComponent<WhiteBalance>(profile);
            whiteBalance.temperature.Override(12f);
            whiteBalance.tint.Override(-3f);

            ColorAdjustments color = AddPersistentVolumeComponent<ColorAdjustments>(profile);
            color.postExposure.Override(-0.05f);
            color.contrast.Override(16f);
            color.saturation.Override(10f);
            color.colorFilter.Override(new Color(1f, 0.96f, 0.88f, 1f));

            ShadowsMidtonesHighlights grading = AddPersistentVolumeComponent<ShadowsMidtonesHighlights>(profile);
            grading.shadows.Override(new Vector4(0.88f, 0.94f, 1.08f, 0f));
            grading.midtones.Override(new Vector4(1.02f, 1f, 0.96f, 0f));
            grading.highlights.Override(new Vector4(1.08f, 1.02f, 0.9f, 0f));
            grading.shadowsStart.Override(0f);
            grading.shadowsEnd.Override(0.32f);
            grading.highlightsStart.Override(0.58f);
            grading.highlightsEnd.Override(1f);

            Bloom bloom = AddPersistentVolumeComponent<Bloom>(profile);
            bloom.threshold.Override(1.08f);
            bloom.intensity.Override(0.22f);
            bloom.scatter.Override(0.52f);
            bloom.clamp.Override(8f);
            bloom.tint.Override(new Color(1f, 0.82f, 0.58f, 1f));
            bloom.highQualityFiltering.Override(true);
            bloom.maxIterations.Override(5);

            Vignette vignette = AddPersistentVolumeComponent<Vignette>(profile);
            vignette.color.Override(new Color(0.05f, 0.035f, 0.025f, 1f));
            vignette.center.Override(new Vector2(0.5f, 0.5f));
            vignette.intensity.Override(0.095f);
            vignette.smoothness.Override(0.38f);
            vignette.rounded.Override(false);

            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static T AddPersistentVolumeComponent<T>(VolumeProfile profile)
            where T : VolumeComponent
        {
            T component = ScriptableObject.CreateInstance<T>();
            component.name = typeof(T).Name;
            component.active = true;
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            profile.components.Add(component);
            AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(component);
            return component;
        }

        private static void ConfigureAmbientDust()
        {
            MMOAmbientDustProfile dustProfile = CreateOrUpdateDustProfile();
            Material dustMaterial = CreateOrUpdateDustMaterial();

            GameObject dustObject = GameObject.Find("Ambient Dust - Starter Valley");
            if (dustObject == null)
            {
                dustObject = new GameObject("Ambient Dust - Starter Valley");
            }

            ParticleSystem particles = dustObject.GetComponent<ParticleSystem>();
            if (particles == null)
            {
                particles = dustObject.AddComponent<ParticleSystem>();
            }

            ParticleSystemRenderer renderer = dustObject.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = dustMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            MMOAmbientDustFollower follower = dustObject.GetComponent<MMOAmbientDustFollower>();
            if (follower == null)
            {
                follower = dustObject.AddComponent<MMOAmbientDustFollower>();
            }

            follower.Profile = dustProfile;
            follower.FollowTarget = Camera.main != null ? Camera.main.transform : null;
            dustObject.transform.position = follower.FollowTarget != null
                ? follower.FollowTarget.position + dustProfile.localOffset
                : dustProfile.localOffset;

            particles.Play();
            EditorUtility.SetDirty(dustObject);
        }

        private static MMOAmbientDustProfile CreateOrUpdateDustProfile()
        {
            MMOAmbientDustProfile profile = AssetDatabase.LoadAssetAtPath<MMOAmbientDustProfile>(DustProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<MMOAmbientDustProfile>();
                AssetDatabase.CreateAsset(profile, DustProfilePath);
            }

            profile.localOffset = new Vector3(0f, 7.5f, 0f);
            profile.emitterBoxSize = new Vector3(46f, 16f, 46f);
            profile.maxParticles = 260;
            profile.emissionRate = 22f;
            profile.lifetimeRange = new Vector2(7f, 13f);
            profile.speedRange = new Vector2(0.025f, 0.12f);
            profile.sizeRange = new Vector2(0.035f, 0.105f);
            profile.dustColor = new Color(1f, 0.73f, 0.42f, 0.2f);
            profile.driftVelocity = new Vector3(0.08f, 0.015f, -0.035f);
            profile.turbulenceStrength = 0.12f;
            profile.turbulenceFrequency = 0.045f;
            profile.turbulenceScrollSpeed = 0.08f;
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static Material CreateOrUpdateDustMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(DustMaterialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                    ?? Shader.Find("Particles/Standard Unlit")
                    ?? Shader.Find("Universal Render Pipeline/Unlit");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, DustMaterialPath);
            }

            SetMaterialColor(material, "_BaseColor", new Color(1f, 0.72f, 0.42f, 0.22f));
            SetMaterialColor(material, "_Color", new Color(1f, 0.72f, 0.42f, 0.22f));
            SetMaterialFloat(material, "_Surface", 1f);
            SetMaterialFloat(material, "_Blend", 0f);
            SetMaterialFloat(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
            SetMaterialFloat(material, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            SetMaterialFloat(material, "_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureGrass()
        {
            MMOClassicGrassFoliageProfile profile = AssetDatabase.LoadAssetAtPath<MMOClassicGrassFoliageProfile>(GrassProfilePath);
            if (profile != null)
            {
                profile.detailDrawDistance = 170f;
                profile.terrainDetailDensity = 0.36f;
                profile.opacity = 0.35f;
                profile.healthyColor = Color.white;
                profile.dryColor = Color.white;
                EditorUtility.SetDirty(profile);
            }

            MMOClassicGrassFoliageBuilder.RefreshClassicGrassMaterials();
        }

        private static Light FindOrCreateSun()
        {
            Light sun = RenderSettings.sun;
            if (sun != null)
            {
                return sun;
            }

            GameObject sunObject = GameObject.Find("Sun");
            if (sunObject == null)
            {
                sunObject = new GameObject("Sun");
            }

            Light light = sunObject.GetComponent<Light>();
            return light != null ? light : sunObject.AddComponent<Light>();
        }

        private static void EnsureFolders()
        {
            CreateFolderIfMissing(WorldConfigFolder);
            CreateFolderIfMissing(AtmosphereConfigFolder);
            CreateFolderIfMissing(GeneratedMaterialsFolder);
        }

        private static void CreateFolderIfMissing(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                return;
            }

            string parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            string folderName = Path.GetFileName(assetPath);
            if (!string.IsNullOrEmpty(parent))
            {
                CreateFolderIfMissing(parent);
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }

        private static void SetSerializedFloat(SerializedObject serializedObject, string propertyName, float value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.floatValue = value;
            }
        }

        private static void SetSerializedInt(SerializedObject serializedObject, string propertyName, int value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.intValue = value;
            }
        }

        private static void SetSerializedBool(SerializedObject serializedObject, string propertyName, bool value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.boolValue = value;
            }
        }

        private static void SetSerializedVector3(SerializedObject serializedObject, string propertyName, Vector3 value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.vector3Value = value;
            }
        }

        private static void SetMaterialColor(Material material, string propertyName, Color value)
        {
            if (material != null && material.HasProperty(propertyName))
            {
                material.SetColor(propertyName, value);
            }
        }

        private static void SetMaterialFloat(Material material, string propertyName, float value)
        {
            if (material != null && material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }
    }
}
