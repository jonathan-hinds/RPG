using System;
using System.Collections.Generic;
using RPGClone.Abilities;
using RPGClone.Vfx;
using RPGClone.Vfx.Mage;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RPGClone.EditorTools
{
    public static class FrostWaveVFXInstaller
    {
        private const string Root = "Assets/_Project/VFX/FrostWave";
        private const string TextureFolder = Root + "/Textures";
        private const string MaterialFolder = Root + "/Materials";
        private const string MeshFolder = Root + "/Meshes";
        private const string ProfileFolder = Root + "/Profiles";
        private const string PrefabFolder = Root + "/Prefabs";
        private const string ShaderFolder = Root + "/Shaders";
        private const string DocumentationFolder = Root + "/Documentation";
        private const string ProfilePath = ProfileFolder + "/FrostWaveVFX_Default.asset";
        private const string RingPath = PrefabFolder + "/FrostWaveExpandingRingVFX.prefab";
        private const string GroundPath = PrefabFolder + "/FrostWaveGroundFrostVFX.prefab";
        private const string RootIndicatorPath = PrefabFolder + "/FrostWaveRootIndicatorVFX.prefab";
        private const string EnemyImpactPath = PrefabFolder + "/FrostWaveEnemyImpactVFX.prefab";
        private const string CasterPath = PrefabFolder + "/FrostWaveCasterVFX.prefab";
        private const string DefinitionPath = "Assets/_Project/VFX/Definitions/Mage_Frost_Wave_VFX.asset";
        private const string AbilityPath = "Assets/_Project/Configs/Abilities/Mage_Frost_Wave.asset";
        private const string RingAtlasPath = TextureFolder + "/FrostWave_RingsGroundRuneAtlas.png";
        private const string MistAtlasPath = TextureFolder + "/FrostWave_MistVaporNoiseAtlas.png";
        private const string ParticleAtlasPath = TextureFolder + "/FrostWave_ParticlesGlowStreakAtlas.png";
        private const string ShardAtlasPath = TextureFolder + "/FrostWave_IceShardAtlas.png";
        private const string RadialCloudAtlasPath = TextureFolder + "/FrostWave_RadialCloudAtlas.png";
        private const string HeroIceAtlasPath = TextureFolder + "/FrostWave_HeroIceAtlas.png";
        private const string DistortionNoisePath = TextureFolder + "/FrostWave_DistortionNoise.png";
        private const string ErosionNoisePath = TextureFolder + "/FrostWave_ErosionNoise.png";

        [MenuItem("Tools/RPG Clone/VFX/Build Frost Wave VFX")]
        public static void Build()
        {
            EnsureFolders();
            RemoveObsoleteAssets();
            ConfigureTextures();
            FrostWaveVFXProfile profile = GetOrCreateProfile();
            Dictionary<string, Mesh> meshes = CreateMeshes();
            Dictionary<string, Material> materials = CreateMaterials();
            GameObject ring = CreateRingPrefab(meshes, materials);
            GameObject ground = CreateGroundPrefab(meshes, materials);
            GameObject rootIndicator = CreateRootIndicatorPrefab(meshes, materials);
            GameObject enemyImpact = CreateEnemyImpactPrefab(profile, rootIndicator, meshes, materials);
            GameObject caster = CreateCasterPrefab(profile, ring, ground, enemyImpact, meshes, materials);
            ConnectAbility(caster);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Validate();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(CasterPath);
            Debug.Log("Frost Wave VFX built and connected to Mage_Frost_Wave.");
        }

        [MenuItem("Tools/RPG Clone/VFX/Validate Frost Wave VFX")]
        public static void Validate()
        {
            FrostWaveVFXProfile profile = AssetDatabase.LoadAssetAtPath<FrostWaveVFXProfile>(ProfilePath);
            GameObject ring = AssetDatabase.LoadAssetAtPath<GameObject>(RingPath);
            GameObject ground = AssetDatabase.LoadAssetAtPath<GameObject>(GroundPath);
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(RootIndicatorPath);
            GameObject impact = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyImpactPath);
            GameObject caster = AssetDatabase.LoadAssetAtPath<GameObject>(CasterPath);
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(DefinitionPath);
            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AbilityPath);
            if (profile == null || ring == null || ground == null || root == null || impact == null || caster == null)
            {
                throw new InvalidOperationException("Frost Wave is missing one or more required profile/prefab assets.");
            }
            if (ring.GetComponent<FrostWaveRingVFX>() == null
                || ground.GetComponent<FrostWaveGroundFrostVFX>() == null
                || impact.GetComponent<FrostWaveEnemyImpactVFX>() == null
                || caster.GetComponent<FrostWaveVFX>() == null
                || caster.GetComponent<FrostWaveRadialFrontVFX>() == null)
            {
                throw new InvalidOperationException("Frost Wave prefab controllers are incomplete.");
            }
            if (caster.GetComponentsInChildren<ParticleSystem>(true).Length < 6
                || impact.GetComponentsInChildren<ParticleSystem>(true).Length < 6)
            {
                throw new InvalidOperationException("Frost Wave particle layering or target reaction pool is incomplete.");
            }
            if (definition == null || ability == null || ability.VisualEffects != definition
                || definition.CastPrefab != caster || definition.HitPrefab != null
                || !definition.CastPrefabControlsHitTiming || definition.AttachCastingToCaster)
            {
                throw new InvalidOperationException("Frost Wave ability VFX definition is not connected correctly.");
            }
            if (!Mathf.Approximately(profile.ResolveRadius(ability), ability.AreaRadius))
            {
                throw new InvalidOperationException("Frost Wave VFX does not resolve its radius from the gameplay ability.");
            }
            foreach (string path in new[]
                     {
                         RingAtlasPath, MistAtlasPath, ParticleAtlasPath, ShardAtlasPath,
                         RadialCloudAtlasPath, HeroIceAtlasPath, DistortionNoisePath, ErosionNoisePath
                     })
            {
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture == null)
                {
                    throw new InvalidOperationException($"Missing Frost Wave texture: {path}");
                }
            }
            Debug.Log("Frost Wave VFX validation passed.");
        }

        [MenuItem("Tools/RPG Clone/VFX/Preview Frost Wave VFX In Play Mode")]
        public static void PreviewInPlayMode()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Enter Play Mode before using the Frost Wave presentation preview.");
                return;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CasterPath);
            FrostWaveVFXProfile profile = AssetDatabase.LoadAssetAtPath<FrostWaveVFXProfile>(ProfilePath);
            Camera camera = Camera.main;
            if (prefab == null || profile == null || camera == null)
            {
                Debug.LogError("Frost Wave preview requires its caster prefab, profile, and a Main Camera.");
                return;
            }

            const string previewName = "[VFX Preview] Frost Wave";
            GameObject existing = GameObject.Find(previewName);
            if (existing != null)
            {
                UnityEngine.Object.Destroy(existing);
            }

            Vector3 forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f)
            {
                forward = camera.transform.forward;
            }
            Vector3 position = camera.transform.position + forward * 7f;
            if (Physics.Raycast(position + Vector3.up * profile.GroundProbeHeight, Vector3.down, out RaycastHit hit, profile.GroundProbeDistance))
            {
                position = hit.point + Vector3.up * profile.GroundOffset;
            }

            GameObject instance = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);
            instance.name = previewName;
            instance.GetComponent<FrostWaveRadialFrontVFX>()?.Play(profile, profile.EffectRadius);
            instance.GetComponentInChildren<FrostWaveRingVFX>(true)?.Play(profile, profile.EffectRadius);
            instance.GetComponentInChildren<FrostWaveGroundFrostVFX>(true)?.Play(profile, profile.EffectRadius);
            Selection.activeGameObject = instance;
            Debug.Log("Frost Wave presentation preview spawned. This is a visual diagnostic only; multiplayer validation still uses the replicated runtime cast path.");
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/_Project/VFX");
            EnsureFolder(Root);
            foreach (string folder in new[] { "Textures", "Materials", "Meshes", "Profiles", "Prefabs", "Shaders", "Documentation" })
            {
                EnsureFolder(Root + "/" + folder);
            }
            EnsureFolder("Assets/_Project/VFX/Definitions");
        }

        private static void RemoveObsoleteAssets()
        {
            foreach (string path in new[]
                     {
                         MaterialFolder + "/FrostWave_MistRing.mat",
                         MaterialFolder + "/FrostWave_AlphaFrostMistA.mat"
                     })
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                {
                    AssetDatabase.DeleteAsset(path);
                }
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            string child = path.Substring(slash + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, child);
        }

        private static void ConfigureTextures()
        {
            ConfigureTexture(RingAtlasPath, TextureWrapMode.Clamp, 2048);
            ConfigureTexture(MistAtlasPath, TextureWrapMode.Clamp, 2048);
            ConfigureTexture(ParticleAtlasPath, TextureWrapMode.Clamp, 2048);
            ConfigureTexture(ShardAtlasPath, TextureWrapMode.Clamp, 2048);
            ConfigureTexture(RadialCloudAtlasPath, TextureWrapMode.Clamp, 2048);
            ConfigureTexture(HeroIceAtlasPath, TextureWrapMode.Clamp, 2048);
            ConfigureTexture(DistortionNoisePath, TextureWrapMode.Repeat, 512);
            ConfigureTexture(ErosionNoisePath, TextureWrapMode.Repeat, 512);
        }

        private static void ConfigureTexture(string path, TextureWrapMode wrap, int maxSize)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"Frost Wave source texture has not imported: {path}");
            }
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = wrap;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = maxSize;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        private static FrostWaveVFXProfile GetOrCreateProfile()
        {
            FrostWaveVFXProfile profile = AssetDatabase.LoadAssetAtPath<FrostWaveVFXProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<FrostWaveVFXProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            profile.ResetToProductionDefaults();
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static Dictionary<string, Material> CreateMaterials()
        {
            Shader layered = AssetDatabase.LoadAssetAtPath<Shader>(ShaderFolder + "/FrostWaveLayered.shader");
            Shader ground = AssetDatabase.LoadAssetAtPath<Shader>(ShaderFolder + "/FrostWaveGround.shader");
            Shader ice = AssetDatabase.LoadAssetAtPath<Shader>(ShaderFolder + "/FrostWaveIce.shader");
            if (layered == null || ground == null || ice == null)
            {
                throw new InvalidOperationException("Frost Wave shaders are missing or failed to import.");
            }

            Texture2D rings = LoadTexture(RingAtlasPath);
            Texture2D mist = LoadTexture(MistAtlasPath);
            Texture2D particles = LoadTexture(ParticleAtlasPath);
            Texture2D shards = LoadTexture(ShardAtlasPath);
            Texture2D radialCloud = LoadTexture(RadialCloudAtlasPath);
            Texture2D heroIce = LoadTexture(HeroIceAtlasPath);
            Texture2D distortion = LoadTexture(DistortionNoisePath);
            Texture2D erosion = LoadTexture(ErosionNoisePath);
            Dictionary<string, Material> result = new();

            AddLayered("AdditiveFrostGlow", particles, AtlasRect(0, 0), new Color(0.55f, 1f, 1.2f, 1f), true, 0.01f, 0f);
            AddLayered("AdditiveExpandingFrostRing", rings, AtlasRect(0, 0), new Color(0.16f, 0.72f, 1.2f, 1f), true, 0.035f, 0f);
            AddLayered("AdditiveThinFrostRing", rings, AtlasRect(2, 0), new Color(0.72f, 1.25f, 1.4f, 1f), true, 0.015f, 0f);
            AddLayered("AdditiveFrostStreak", particles, AtlasRect(3, 1), new Color(0.48f, 0.95f, 1.25f, 1f), true, 0.02f, 0f);
            AddLayered("AdditiveSparkle", particles, AtlasRect(0, 1), new Color(0.8f, 1.2f, 1.35f, 1f), true, 0.005f, 0f);
            AddLayered("AlphaFrostMistB", mist, AtlasRect(2, 0), new Color(0.34f, 0.72f, 1f, 0.62f), false, 0.055f, 1.8f);
            AddLayered("AlphaColdVapor", mist, AtlasRect(1, 1), new Color(0.62f, 0.9f, 1f, 0.62f), false, 0.04f, 2.1f);
            AddLayered("RadialCloudFront", radialCloud, new Vector4(1f, 1f, 0f, 0f), new Color(0.68f, 0.95f, 1.08f, 0.92f), false, 0.018f, 1.05f);
            AddLayered("AlphaSnow", particles, AtlasRect(1, 0), new Color(0.82f, 1f, 1.12f, 0.92f), false, 0f, 0f);
            AddLayered("FrostSpecks", particles, AtlasRect(1, 3), new Color(0.68f, 0.98f, 1.15f, 0.9f), true, 0f, 0f);
            AddLayered("FrostRune", rings, AtlasRect(3, 1), new Color(0.42f, 0.85f, 1.15f, 0.65f), true, 0.012f, 0f);
            AddGround("GroundFrostPrimary", rings, AtlasRect(1, 1), new Color(0.08f, 0.48f, 1f, 0.72f));
            AddGround("GroundFrostSecondary", rings, AtlasRect(2, 1), new Color(0.42f, 0.9f, 1.2f, 0.6f));
            AddGround("RootGroundMark", rings, AtlasRect(1, 2), new Color(0.1f, 0.52f, 1f, 0.58f));
            AddIce("IceShardMain", shards, AtlasRect(0, 0), new Color(0.08f, 0.5f, 1f, 0.96f), new Color(0.72f, 1.35f, 1.7f, 1f));
            AddIce("IceShardDeep", shards, AtlasRect(2, 0), new Color(0.02f, 0.24f, 0.75f, 0.96f), new Color(0.38f, 1.05f, 1.45f, 1f));
            AddIce("IceShardHighlight", shards, AtlasRect(3, 0), new Color(0.25f, 0.78f, 1.15f, 0.96f), new Color(0.9f, 1.45f, 1.75f, 1f));
            AddIce("HeroIceBreaker", heroIce, new Vector4(1f, 1f, 0f, 0f), new Color(0.58f, 0.92f, 1.18f, 1f), new Color(1.1f, 1.55f, 1.9f, 1f));
            return result;

            void AddLayered(string name, Texture texture, Vector4 rect, Color tint, bool additive, float distortionStrength, float softIntersection)
            {
                Material material = GetOrCreateMaterial(name, layered);
                material.SetTexture("_BaseMap", texture);
                material.SetTexture("_NoiseMap", distortion);
                material.SetVector("_AtlasRect", rect);
                material.SetColor("_Tint", tint);
                material.SetFloat("_Opacity", 1f);
                material.SetFloat("_Brightness", 1f);
                material.SetFloat("_Reveal", 1.5f);
                material.SetFloat("_RevealSoftness", 0.12f);
                material.SetFloat("_Dissolve", 0f);
                material.SetFloat("_EdgeSoftness", 0.08f);
                material.SetFloat("_DistortionStrength", distortionStrength);
                material.SetFloat("_SoftIntersection", softIntersection);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
                material.SetFloat("_ZWrite", 0f);
                EditorUtility.SetDirty(material);
                result[name] = material;
            }

            void AddGround(string name, Texture texture, Vector4 rect, Color tint)
            {
                Material material = GetOrCreateMaterial(name, ground);
                material.SetTexture("_BaseMap", texture);
                material.SetTexture("_NoiseMap", erosion);
                material.SetVector("_AtlasRect", rect);
                material.SetColor("_Tint", tint);
                material.SetFloat("_Opacity", 1f);
                material.SetFloat("_Brightness", 1f);
                material.SetFloat("_Reveal", 0f);
                material.SetFloat("_RevealSoftness", 0.11f);
                material.SetFloat("_Dissolve", 0f);
                material.SetFloat("_EdgeSoftness", 0.09f);
                material.SetFloat("_DistortionStrength", 0.025f);
                EditorUtility.SetDirty(material);
                result[name] = material;
            }

            void AddIce(string name, Texture texture, Vector4 rect, Color tint, Color edge)
            {
                Material material = GetOrCreateMaterial(name, ice);
                material.SetTexture("_BaseMap", texture);
                material.SetTexture("_NoiseMap", erosion);
                material.SetVector("_AtlasRect", rect);
                material.SetColor("_Tint", tint);
                material.SetColor("_EdgeTint", edge);
                material.SetFloat("_Opacity", 1f);
                material.SetFloat("_Brightness", 1f);
                material.SetFloat("_Dissolve", 0f);
                material.SetFloat("_EdgeSoftness", 0.08f);
                material.SetFloat("_FresnelStrength", 1.15f);
                EditorUtility.SetDirty(material);
                result[name] = material;
            }
        }

        private static Material GetOrCreateMaterial(string name, Shader shader)
        {
            string path = $"{MaterialFolder}/FrostWave_{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = "FrostWave_" + name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            return material;
        }

        private static Dictionary<string, Mesh> CreateMeshes()
        {
            return new Dictionary<string, Mesh>
            {
                ["GroundQuad"] = CreateOrUpdateMesh("FrostWave_GroundQuad", CreateGroundQuad),
                ["IceShard"] = CreateOrUpdateMesh("FrostWave_IceShard", CreateIceShard)
            };
        }

        private static Mesh CreateOrUpdateMesh(string name, Func<Mesh> factory)
        {
            string path = $"{MeshFolder}/{name}.asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            Mesh generated = factory();
            generated.name = name;
            if (existing == null)
            {
                AssetDatabase.CreateAsset(generated, path);
                return generated;
            }
            EditorUtility.CopySerialized(generated, existing);
            UnityEngine.Object.DestroyImmediate(generated);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static GameObject CreateRingPrefab(IReadOnlyDictionary<string, Mesh> meshes, IReadOnlyDictionary<string, Material> materials)
        {
            GameObject root = new("Frost Wave Expanding Ring VFX");
            FrostWaveRingVFX controller = root.AddComponent<FrostWaveRingVFX>();
            Renderer primary = CreateRenderer("Primary Irregular Frost Ring", root.transform, meshes["GroundQuad"], materials["AdditiveExpandingFrostRing"], Vector3.up * 0.025f, Vector3.one, Quaternion.identity, 31);
            Renderer secondary = CreateRenderer("Thin White Hot Secondary Ring", root.transform, meshes["GroundQuad"], materials["AdditiveThinFrostRing"], Vector3.up * 0.035f, Vector3.one, Quaternion.Euler(0f, 19f, 0f), 33);
            controller.ConfigureAuthoring(primary, secondary, null);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, RingPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateGroundPrefab(IReadOnlyDictionary<string, Mesh> meshes, IReadOnlyDictionary<string, Material> materials)
        {
            GameObject root = new("Frost Wave Ground Frost VFX");
            FrostWaveGroundFrostVFX controller = root.AddComponent<FrostWaveGroundFrostVFX>();
            Renderer primary = CreateRenderer("Branching Frost Pattern", root.transform, meshes["GroundQuad"], materials["GroundFrostPrimary"], Vector3.zero, Vector3.one, Quaternion.Euler(0f, 13f, 0f), 3);
            Renderer secondary = CreateRenderer("Broken Frozen Patch Overlay", root.transform, meshes["GroundQuad"], materials["GroundFrostSecondary"], Vector3.up * 0.012f, Vector3.one, Quaternion.Euler(0f, -41f, 0f), 4);
            Renderer rune = CreateRenderer("Faint Caster Frost Rune", root.transform, meshes["GroundQuad"], materials["FrostRune"], Vector3.up * 0.025f, Vector3.one, Quaternion.Euler(0f, 7f, 0f), 12);
            controller.ConfigureAuthoring(primary, secondary, rune);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, GroundPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateRootIndicatorPrefab(IReadOnlyDictionary<string, Mesh> meshes, IReadOnlyDictionary<string, Material> materials)
        {
            GameObject root = new("Frost Wave Root Indicator VFX");
            Transform visuals = CreateChild("Persistent Root Indicator", root.transform).transform;
            Renderer ground = CreateRenderer("Subtle Frozen Ground Mark", visuals, meshes["GroundQuad"], materials["RootGroundMark"], Vector3.zero, new Vector3(1.85f, 1f, 1.85f), Quaternion.identity, 8);
            List<Renderer> formations = new();
            const int rootFormationCount = 10;
            for (int i = 0; i < rootFormationCount; i++)
            {
                float angle = i * Mathf.PI * 2f / rootFormationCount + (i % 2) * 0.1f;
                float distance = 0.5f + (i % 3) * 0.065f;
                Vector3 position = new(Mathf.Cos(angle) * distance, 0.28f, Mathf.Sin(angle) * distance);
                formations.Add(CreateRenderer(
                    $"Foot Ice Formation {i + 1:00}",
                    visuals,
                    meshes["IceShard"],
                    materials[i % 3 == 0 ? "IceShardDeep" : i % 3 == 1 ? "IceShardMain" : "IceShardHighlight"],
                    position,
                    new Vector3(0.28f + (i % 2) * 0.07f, 0.68f + (i % 3) * 0.14f, 0.28f),
                    Quaternion.Euler(4f + (i % 2) * 8f, -angle * Mathf.Rad2Deg, (i % 3 - 1) * 15f),
                    14 + i % 3));
            }
            CreateParticles("Faint Persistent Cold Vapor", visuals, materials["AlphaColdVapor"], 0, 1.4f, 0.22f, 0.32f, 0.48f, false, false, null, 10, 0f, true);
            CreateParticles("Occasional Root Frost Sparkles", visuals, materials["AdditiveSparkle"], 0, 1.2f, 0.35f, 0.07f, 0.45f, false, false, null, 18, 0f, true);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, RootIndicatorPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateEnemyImpactPrefab(
            FrostWaveVFXProfile profile,
            GameObject rootIndicatorPrefab,
            IReadOnlyDictionary<string, Mesh> meshes,
            IReadOnlyDictionary<string, Material> materials)
        {
            GameObject root = new("Frost Wave Enemy Impact VFX");
            FrostWaveEnemyImpactVFX controller = root.AddComponent<FrostWaveEnemyImpactVFX>();
            Transform impact = CreateChild("Initial Freeze Impact", root.transform).transform;
            Renderer flash = CreateRenderer("Compact Blue White Frost Flash", impact, meshes["GroundQuad"], materials["AdditiveFrostGlow"], Vector3.up * 0.08f, new Vector3(1.4f, 1f, 1.4f), Quaternion.identity, 42);
            Renderer ground = CreateRenderer("Enemy Frost Contact Mark", impact, meshes["GroundQuad"], materials["RootGroundMark"], Vector3.zero, new Vector3(1.65f, 1f, 1.65f), Quaternion.Euler(0f, 31f, 0f), 12);
            List<Renderer> shards = new();
            const int impactFormationCount = 10;
            for (int i = 0; i < impactFormationCount; i++)
            {
                float angle = i * Mathf.PI * 2f / impactFormationCount;
                shards.Add(CreateRenderer(
                    $"Rising Impact Shard {i + 1:00}",
                    impact,
                    meshes["IceShard"],
                    materials[i % 3 == 0 ? "IceShardHighlight" : i % 3 == 1 ? "IceShardMain" : "IceShardDeep"],
                    new Vector3(Mathf.Cos(angle) * 0.53f, 0.24f, Mathf.Sin(angle) * 0.53f),
                    new Vector3(0.28f + (i % 2) * 0.06f, 0.72f + (i % 3) * 0.14f, 0.29f),
                    Quaternion.Euler(7f + (i % 2) * 12f, -angle * Mathf.Rad2Deg, (i % 3 - 1) * 16f),
                    34 + i % 3));
            }
            ParticleSystem mist = CreateParticles("Compact Impact Cold Mist", impact, materials["AlphaFrostMistB"], 7, 0.72f, 0.8f, 0.34f, 0.35f, true, true, null, 30, 0f);
            ParticleSystem snow = CreateParticles("Impact Snow And Frost Specks", impact, materials["AlphaSnow"], 15, 0.82f, 1.6f, 0.075f, 0.38f, true, true, null, 43, 0f);
            ParticleSystem particleShards = CreateParticles("Impact Ice Mesh Fragments", impact, materials["IceShardMain"], 8, 0.78f, 2.4f, 0.11f, 0.28f, true, true, meshes["IceShard"], 38, 0f);
            ParticleSystem streaks = CreateParticles("Upward Frost Streaks", impact, materials["AdditiveFrostStreak"], 5, 0.52f, 2.2f, 0.18f, 0.3f, true, true, null, 41, 0f, false, true);

            GameObject rootIndicator = (GameObject)PrefabUtility.InstantiatePrefab(rootIndicatorPrefab, root.transform);
            rootIndicator.name = "Persistent Root Indicator";
            Transform rootVisuals = rootIndicator.transform.Find("Persistent Root Indicator");
            Renderer rootGround = rootVisuals.Find("Subtle Frozen Ground Mark").GetComponent<Renderer>();
            List<Renderer> rootFormations = new();
            const int rootFormationCount = 10;
            for (int i = 0; i < rootFormationCount; i++)
            {
                rootFormations.Add(rootVisuals.Find($"Foot Ice Formation {i + 1:00}").GetComponent<Renderer>());
            }
            ParticleSystem rootVapor = rootVisuals.Find("Faint Persistent Cold Vapor").GetComponent<ParticleSystem>();
            ParticleSystem rootSparkles = rootVisuals.Find("Occasional Root Frost Sparkles").GetComponent<ParticleSystem>();
            controller.ConfigureAuthoring(
                impact,
                flash,
                ground,
                shards.ToArray(),
                mist,
                snow,
                particleShards,
                streaks,
                rootVisuals,
                rootGround,
                rootFormations.ToArray(),
                rootVapor,
                rootSparkles);
            root.AddComponent<MMOAbilityVfxPoolable>().ConfigureAuthoring(profile.TargetReactionPoolSize);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, EnemyImpactPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateCasterPrefab(
            FrostWaveVFXProfile profile,
            GameObject ringPrefab,
            GameObject groundPrefab,
            GameObject enemyImpactPrefab,
            IReadOnlyDictionary<string, Mesh> meshes,
            IReadOnlyDictionary<string, Material> materials)
        {
            GameObject root = new("Frost Wave Caster VFX");
            FrostWaveVFX controller = root.AddComponent<FrostWaveVFX>();
            FrostWaveRadialFrontVFX radialFront = root.AddComponent<FrostWaveRadialFrontVFX>();
            Renderer centerGlow = CreateRenderer("Blue White Center Flash", root.transform, meshes["GroundQuad"], materials["AdditiveFrostGlow"], Vector3.up * 0.04f, Vector3.one, Quaternion.identity, 50);
            GameObject ringObject = (GameObject)PrefabUtility.InstantiatePrefab(ringPrefab, root.transform);
            ringObject.name = "Expanding Frost Wave";
            GameObject groundObject = (GameObject)PrefabUtility.InstantiatePrefab(groundPrefab, root.transform);
            groundObject.name = "Temporary Ground Frost";
            FrostWaveRingVFX ring = ringObject.GetComponent<FrostWaveRingVFX>();
            FrostWaveGroundFrostVFX ground = groundObject.GetComponent<FrostWaveGroundFrostVFX>();

            ParticleSystem openingSnow = CreateParticles("Opening Snow Burst", root.transform, materials["AlphaSnow"], profile.OpeningSnowAmount, 0.72f, 2.8f, 0.075f, 0.34f, true, true, null, 46, 0f);
            ParticleSystem outwardShards = CreateParticles("Outward Faceted Ice Fragments", root.transform, materials["IceShardMain"], profile.OutwardShardAmount, 0.82f, 10.5f, 0.24f, 0.2f, true, true, meshes["IceShard"], 44, 0.06f);
            ParticleSystem waveSnow = CreateParticles("Outward Wave Snow", root.transform, materials["AlphaSnow"], profile.WaveSnowAmount, 0.85f, 16.5f, 0.065f, 0.2f, true, true, null, 40, 0.08f);
            ParticleSystem streaks = CreateParticles("Directional Frost Streaks", root.transform, materials["AdditiveFrostStreak"], profile.FrostStreakAmount, 0.52f, 18.5f, 0.18f, 0.16f, true, true, null, 47, 0.08f, false, true);
            ParticleSystem cloudFront = CreateRadialFrontParticles(
                "Upright Traveling Frost Cloud Front",
                root.transform,
                materials["RadialCloudFront"],
                profile.RadialCloudLifetime,
                160,
                38,
                false);
            ParticleSystem iceBreakers = CreateRadialFrontParticles(
                "Hero Ice Breakers Riding Wave",
                root.transform,
                materials["HeroIceBreaker"],
                profile.IceBreakerLifetime,
                64,
                45,
                true);
            radialFront.ConfigureAuthoring(cloudFront, iceBreakers);

            GameObject lightObject = CreateChild("Brief Blue White Light Pulse", root.transform);
            lightObject.transform.localPosition = Vector3.up * 0.28f;
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = profile.PaleCyan;
            light.intensity = 0f;
            light.range = profile.LightRadius;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.Auto;
            light.enabled = false;

            controller.ConfigureAuthoring(
                profile,
                centerGlow,
                ring,
                ground,
                radialFront,
                openingSnow,
                null,
                outwardShards,
                waveSnow,
                streaks,
                null,
                light,
                enemyImpactPrefab);
            root.AddComponent<MMOAbilityVfxPoolable>().ConfigureAuthoring(16);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, CasterPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static void ConnectAbility(GameObject casterPrefab)
        {
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(DefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<MMOAbilityVfxDefinition>();
                AssetDatabase.CreateAsset(definition, DefinitionPath);
            }
            definition.Configure(
                null,
                casterPrefab,
                null,
                false,
                false,
                false,
                false,
                Vector3.zero,
                Vector3.zero,
                Vector3.zero,
                Vector3.zero,
                0f,
                true);
            definition.ConfigureCasterBounce(0f, 0f);
            EditorUtility.SetDirty(definition);

            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AbilityPath);
            if (ability == null)
            {
                throw new InvalidOperationException($"Frost Wave ability not found at {AbilityPath}.");
            }
            ability.SetVisualEffects(definition);
            EditorUtility.SetDirty(ability);
        }

        private static MeshRenderer CreateRenderer(
            string name,
            Transform parent,
            Mesh mesh,
            Material material,
            Vector3 position,
            Vector3 scale,
            Quaternion rotation,
            int sortingOrder)
        {
            GameObject child = CreateChild(name, parent);
            child.transform.localPosition = position;
            child.transform.localRotation = rotation;
            child.transform.localScale = scale;
            MeshFilter filter = child.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private static ParticleSystem CreateParticles(
            string name,
            Transform parent,
            Material material,
            int burst,
            float lifetime,
            float speed,
            float size,
            float radius,
            bool radial,
            bool worldSpace,
            Mesh mesh,
            int sortingOrder,
            float delay,
            bool loop = false,
            bool stretched = false)
        {
            GameObject child = CreateChild(name, parent);
            if (radial)
            {
                child.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            ParticleSystem system = child.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = loop;
            main.duration = Mathf.Max(0.1f, lifetime);
            main.startDelay = delay;
            main.simulationSpace = worldSpace ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.72f, lifetime * 1.18f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.72f, speed * 1.16f);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.7f, size * 1.3f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            main.maxParticles = Mathf.Max(24, burst * 2 + (loop ? 32 : 0));
            if (mesh != null)
            {
                main.startRotation3D = true;
                main.startRotationX = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
                main.startRotationY = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
                main.startRotationZ = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
                main.gravityModifier = new ParticleSystem.MinMaxCurve(0.18f, 0.42f);
            }
            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = loop ? Mathf.Max(1f, burst) : 0f;
            emission.SetBursts(!loop && burst > 0 ? new[] { new ParticleSystem.Burst(0f, (short)burst) } : Array.Empty<ParticleSystem.Burst>());
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = radial ? ParticleSystemShapeType.Circle : ParticleSystemShapeType.Sphere;
            shape.radius = radius;
            shape.radiusThickness = 1f;
            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(size * 0.35f, size * 0.9f);
            noise.frequency = 0.65f;
            noise.scrollSpeed = 0.24f;
            ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
            color.enabled = true;
            Gradient alpha = new();
            alpha.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.82f, 0.62f), new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(alpha);
            ParticleSystem.SizeOverLifetimeModule sizeModule = system.sizeOverLifetime;
            sizeModule.enabled = true;
            AnimationCurve sizeCurve = new(
                new Keyframe(0f, mesh != null ? 0.45f : 0.25f),
                new Keyframe(0.22f, 1f),
                new Keyframe(0.78f, mesh != null ? 0.82f : 1.25f),
                new Keyframe(1f, 0f));
            sizeModule.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);
            ParticleSystem.RotationOverLifetimeModule rotation = system.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-2.8f, 2.8f);
            ParticleSystem.LimitVelocityOverLifetimeModule drag = system.limitVelocityOverLifetime;
            drag.enabled = mesh != null;
            drag.limit = new ParticleSystem.MinMaxCurve(Mathf.Max(0.5f, speed * 0.62f));
            drag.dampen = 0.28f;
            ParticleSystemRenderer renderer = child.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = sortingOrder;
            if (mesh != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Mesh;
                renderer.mesh = mesh;
                renderer.alignment = ParticleSystemRenderSpace.World;
            }
            else if (stretched)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = 0.2f;
                renderer.lengthScale = 2.2f;
            }
            else
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.alignment = ParticleSystemRenderSpace.View;
            }
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return system;
        }

        private static ParticleSystem CreateRadialFrontParticles(
            string name,
            Transform parent,
            Material material,
            float lifetime,
            int maxParticles,
            int sortingOrder,
            bool ice)
        {
            GameObject child = CreateChild(name, parent);
            ParticleSystem system = child.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 2f;
            main.startLifetime = Mathf.Max(0.1f, lifetime);
            main.startSpeed = 0f;
            main.startSize = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Max(16, maxParticles);
            main.stopAction = ParticleSystemStopAction.None;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;

            ParticleSystem.TextureSheetAnimationModule sheet = system.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.numTilesX = 4;
            sheet.numTilesY = 4;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 0.999f);
            sheet.cycleCount = 1;

            ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
            color.enabled = true;
            Gradient alpha = new();
            alpha.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                ice
                    ? new[]
                    {
                        new GradientAlphaKey(0f, 0f),
                        new GradientAlphaKey(1f, 0.12f),
                        new GradientAlphaKey(1f, 0.68f),
                        new GradientAlphaKey(0f, 1f)
                    }
                    : new[]
                    {
                        new GradientAlphaKey(0f, 0f),
                        new GradientAlphaKey(1f, 0.08f),
                        new GradientAlphaKey(0.9f, 0.58f),
                        new GradientAlphaKey(0f, 1f)
                    });
            color.color = new ParticleSystem.MinMaxGradient(alpha);

            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(
                1f,
                ice
                    ? new AnimationCurve(
                        new Keyframe(0f, 0.08f),
                        new Keyframe(0.16f, 1f),
                        new Keyframe(0.72f, 0.94f),
                        new Keyframe(1f, 0f))
                    : new AnimationCurve(
                        new Keyframe(0f, 0.44f),
                        new Keyframe(0.18f, 1f),
                        new Keyframe(0.68f, 1.18f),
                        new Keyframe(1f, 0f)));

            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = !ice;
            noise.quality = ParticleSystemNoiseQuality.Low;
            noise.strength = 0.18f;
            noise.frequency = 0.55f;
            noise.scrollSpeed = 0.22f;

            ParticleSystem.LimitVelocityOverLifetimeModule drag = system.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.limit = ice ? 1.2f : 3.5f;
            drag.dampen = ice ? 0.52f : 0.18f;

            ParticleSystemRenderer renderer = child.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.VerticalBillboard;
            renderer.alignment = ParticleSystemRenderSpace.World;
            renderer.allowRoll = false;
            renderer.sortingOrder = sortingOrder;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return system;
        }

        private static GameObject CreateChild(string name, Transform parent)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            return child;
        }

        private static Texture2D LoadTexture(string path)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                throw new InvalidOperationException($"Texture not found: {path}");
            }
            return texture;
        }

        private static Vector4 AtlasRect(int column, int rowFromTop)
        {
            const float size = 0.25f;
            return new Vector4(size, size, Mathf.Clamp(column, 0, 3) * size, (3 - Mathf.Clamp(rowFromTop, 0, 3)) * size);
        }

        private static Mesh CreateGroundQuad()
        {
            Mesh mesh = new();
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, 0f, -0.5f),
                new Vector3(-0.5f, 0f, 0.5f),
                new Vector3(0.5f, 0f, 0.5f),
                new Vector3(0.5f, 0f, -0.5f)
            };
            mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateIceShard()
        {
            Vector3[] vertices =
            {
                new(-0.42f, 0f, -0.34f), new(0.38f, 0f, -0.3f), new(0.32f, 0f, 0.36f), new(-0.34f, 0f, 0.3f),
                new(-0.11f, 1f, -0.07f), new(0.09f, 0.86f, 0.08f)
            };
            Vector2[] uv =
            {
                new(0.08f, 0.08f), new(0.92f, 0.08f), new(0.9f, 0.42f), new(0.1f, 0.42f),
                new(0.38f, 0.98f), new(0.62f, 0.88f)
            };
            int[] triangles =
            {
                0, 2, 1, 0, 3, 2,
                0, 1, 4, 1, 5, 4,
                1, 2, 5,
                2, 3, 5, 3, 4, 5,
                3, 0, 4
            };
            Mesh mesh = new();
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
