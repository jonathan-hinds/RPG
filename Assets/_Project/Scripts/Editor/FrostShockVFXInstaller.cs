using System;
using System.Collections.Generic;
using RPGClone.Abilities;
using RPGClone.Vfx;
using RPGClone.Vfx.Shaman;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RPGClone.EditorTools
{
    public static class FrostShockVFXInstaller
    {
        private const string Root = "Assets/_Project/VFX/FrostShock";
        private const string TextureFolder = Root + "/Textures";
        private const string MaterialFolder = Root + "/Materials";
        private const string MeshFolder = Root + "/Meshes";
        private const string ProfileFolder = Root + "/Profiles";
        private const string PrefabFolder = Root + "/Prefabs";
        private const string ShaderFolder = Root + "/Shaders";
        private const string ProfilePath = ProfileFolder + "/FrostShockVFX_Default.asset";
        private const string CastPath = PrefabFolder + "/FrostShockCastVFX.prefab";
        private const string ProjectilePath = PrefabFolder + "/FrostShockProjectileVFX.prefab";
        private const string ImpactPath = PrefabFolder + "/FrostShockImpactVFX.prefab";
        private const string SlowPath = PrefabFolder + "/FrostShockSlowDebuffVFX.prefab";
        private const string ExpirationPath = PrefabFolder + "/FrostShockExpirationVFX.prefab";
        private const string CompletePath = PrefabFolder + "/FrostShockVFX.prefab";
        private const string DefinitionPath = "Assets/_Project/VFX/Definitions/Shaman_Frost_Shock_VFX.asset";
        private const string AbilityPath = "Assets/_Project/Configs/Abilities/Shaman_Frost_Shock.asset";
        private const string SurfaceAtlasPath = TextureFolder + "/FrostShock_MeshSurfaceAtlas.png";
        private const string EnergyAtlasPath = TextureFolder + "/FrostShock_EnergyBurstAtlas.png";
        private const string ShardAtlasPath = TextureFolder + "/FrostShock_IceShardAtlas.png";
        private const string CrackAtlasPath = TextureFolder + "/FrostShock_CrackGroundPatchAtlas.png";
        private const string MistAtlasPath = TextureFolder + "/FrostShock_MistSnowTrailAtlas.png";
        private const string NoisePath = "Assets/_Project/VFX/Fireball/Textures/Fireball_Noise.png";

        [MenuItem("Tools/RPG Clone/VFX/Build Frost Shock VFX")]
        public static void Build()
        {
            EnsureFolders();
            ConfigureTextures();
            FrostShockVFXProfile profile = GetOrCreateProfile();
            Dictionary<string, Mesh> meshes = CreateMeshes();
            Dictionary<string, Material> materials = CreateMaterials();
            GameObject cast = CreateCastPrefab(profile, meshes, materials);
            GameObject projectile = CreateProjectilePrefab(profile, meshes, materials);
            GameObject expiration = CreateExpirationPrefab(profile, meshes, materials);
            GameObject slow = CreateSlowPrefab(profile, meshes, materials);
            GameObject impact = CreateImpactPrefab(profile, meshes, materials, slow);
            CreateCompletePrefab(profile, projectile, impact, expiration);
            WireAbility(projectile, impact);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Validate();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(CompletePath);
            Debug.Log("Frost Shock VFX built: textured mesh volumes, layered projectile, impact, replicated-buff slow lifecycle, and secondary atlases are ready.");
        }

        [MenuItem("Tools/RPG Clone/VFX/Validate Frost Shock VFX")]
        public static void Validate()
        {
            FrostShockVFXProfile profile = AssetDatabase.LoadAssetAtPath<FrostShockVFXProfile>(ProfilePath);
            GameObject cast = AssetDatabase.LoadAssetAtPath<GameObject>(CastPath);
            GameObject projectile = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePath);
            GameObject impact = AssetDatabase.LoadAssetAtPath<GameObject>(ImpactPath);
            GameObject slow = AssetDatabase.LoadAssetAtPath<GameObject>(SlowPath);
            GameObject expiration = AssetDatabase.LoadAssetAtPath<GameObject>(ExpirationPath);
            GameObject complete = AssetDatabase.LoadAssetAtPath<GameObject>(CompletePath);
            if (profile == null || cast == null || projectile == null || impact == null || slow == null || expiration == null || complete == null)
            {
                throw new MissingReferenceException("Frost Shock profile or prefab deliverables are missing. Run Build Frost Shock VFX.");
            }

            if (projectile.GetComponent<FrostShockProjectileVFX>() == null || impact.GetComponent<FrostShockImpactVFX>() == null
                || impact.GetComponentInChildren<FrostShockSlowDebuffVFX>(true) == null || slow.GetComponent<FrostShockSlowDebuffVFX>() == null
                || complete.GetComponent<FrostShockVFX>() == null)
            {
                throw new MissingReferenceException("Frost Shock phase controllers are missing from their prefabs.");
            }

            foreach (string meshName in new[] { "FrostShock_Disc", "FrostShock_Torus", "FrostShock_Tube", "FrostShock_TaperedPrism", "FrostShock_IcePlate", "FrostShock_Sphere" })
            {
                if (AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshFolder}/{meshName}.asset") == null)
                {
                    throw new MissingReferenceException($"Frost Shock mesh is missing: {meshName}");
                }
            }

            if (projectile.GetComponentsInChildren<MeshRenderer>(true).Length < 6 || slow.GetComponentsInChildren<MeshRenderer>(true).Length < 16)
            {
                throw new UnityException("Frost Shock must retain its layered textured-mesh construction.");
            }

            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(DefinitionPath);
            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AbilityPath);
            if (definition == null || ability == null || ability.VisualEffects != definition || definition.CastPrefab != projectile
                || definition.HitPrefab != impact || !definition.CastPrefabControlsHitTiming || !definition.AttachHitToTarget)
            {
                throw new MissingReferenceException("Frost Shock is not wired through the shared network-facing ability VFX definition.");
            }

            Debug.Log("Frost Shock VFX validation passed: mesh-first art, phase prefabs, profile, materials, and multiplayer-facing wiring are valid.", complete);
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/_Project/VFX", "FrostShock");
            foreach (string folder in new[] { "Textures", "Materials", "Meshes", "Profiles", "Prefabs", "Shaders", "Documentation" })
            {
                EnsureFolder(Root, folder);
            }
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static void ConfigureTextures()
        {
            ConfigureTexture(SurfaceAtlasPath, TextureWrapMode.Repeat, 2048);
            ConfigureTexture(EnergyAtlasPath, TextureWrapMode.Clamp, 2048);
            ConfigureTexture(ShardAtlasPath, TextureWrapMode.Clamp, 2048);
            ConfigureTexture(CrackAtlasPath, TextureWrapMode.Clamp, 2048);
            ConfigureTexture(MistAtlasPath, TextureWrapMode.Clamp, 2048);
        }

        private static void ConfigureTexture(string path, TextureWrapMode wrap, int maxSize)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                throw new MissingReferenceException($"Required Frost Shock texture is missing: {path}");
            }

            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.wrapMode = wrap;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = maxSize;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        private static FrostShockVFXProfile GetOrCreateProfile()
        {
            FrostShockVFXProfile profile = AssetDatabase.LoadAssetAtPath<FrostShockVFXProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<FrostShockVFXProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static Dictionary<string, Material> CreateMaterials()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderFolder + "/FrostShockLayeredMesh.shader");
            Shader distortionShader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderFolder + "/FrostShockDistortion.shader");
            if (shader == null || distortionShader == null)
            {
                throw new MissingReferenceException("Frost Shock shaders could not be loaded.");
            }

            Texture2D surface = LoadTexture(SurfaceAtlasPath);
            Texture2D energy = LoadTexture(EnergyAtlasPath);
            Texture2D shards = LoadTexture(ShardAtlasPath);
            Texture2D cracks = LoadTexture(CrackAtlasPath);
            Texture2D mist = LoadTexture(MistAtlasPath);
            Texture2D noise = AssetDatabase.LoadAssetAtPath<Texture2D>(NoisePath) ?? surface;
            Dictionary<string, Material> result = new();

            Add("EnergyCore", energy, Cell4(0, 0), new Color(0.58f, 0.96f, 1f, 1f), new Color(1.7f, 2.2f, 2.6f, 1f), true, 2.2f, new Vector4(-0.1f, 0.08f, 0.05f, -0.07f));
            Add("FrostSpear", surface, Cell2(0, 0), new Color(0.08f, 0.48f, 1f, 1f), new Color(1.5f, 2.1f, 2.5f, 1f), false, 1.25f, new Vector4(-1.8f, 0.02f, 0.2f, -0.12f));
            Add("ProjectileIceBody", surface, Cell2(1, 0), new Color(0.06f, 0.34f, 0.92f, 0.96f), new Color(0.72f, 1.4f, 1.8f, 1f), false, 0.7f, new Vector4(-0.22f, 0.05f, 0.09f, -0.08f));
            Add("OuterGlow", surface, Cell2(0, 0), new Color(0.2f, 0.72f, 1f, 0.62f), new Color(1.2f, 2f, 2.4f, 1f), true, 1.4f, new Vector4(-0.7f, 0.03f, 0.14f, -0.11f));
            Add("IceShards", shards, Vector4.one, new Color(0.12f, 0.58f, 1f, 1f), new Color(1.4f, 2f, 2.3f, 1f), false, 0.55f, Vector4.zero);
            Add("FrostBursts", energy, Vector4.one, new Color(0.35f, 0.82f, 1f, 1f), new Color(1.6f, 2.2f, 2.6f, 1f), true, 1.1f, Vector4.zero);
            Add("FrostCracks", cracks, Vector4.one, new Color(0.1f, 0.54f, 1f, 0.95f), new Color(1.7f, 2.3f, 2.7f, 1f), true, 0.45f, Vector4.zero);
            Add("GroundFrost", cracks, Cell4(0, 1), new Color(0.12f, 0.45f, 0.92f, 0.82f), new Color(0.72f, 1.3f, 1.65f, 1f), false, 0.25f, new Vector4(0.012f, -0.008f, -0.01f, 0.01f));
            Add("FootIceDark", surface, Cell2(1, 0), new Color(0.02f, 0.1f, 0.42f, 1f), new Color(0.2f, 0.72f, 1.1f, 1f), false, 0.35f, new Vector4(-0.025f, 0.015f, 0.01f, -0.01f));
            Add("FootIceMain", surface, Cell2(1, 0), new Color(0.06f, 0.38f, 0.9f, 1f), new Color(0.72f, 1.45f, 1.8f, 1f), false, 0.58f, new Vector4(-0.04f, 0.02f, 0.015f, -0.012f));
            Add("IceHighlights", surface, Cell2(0, 0), new Color(0.55f, 0.94f, 1f, 0.82f), new Color(1.8f, 2.35f, 2.65f, 1f), true, 1.2f, new Vector4(-0.08f, 0.025f, 0.02f, -0.018f));
            Add("LowerLegFrost", surface, Cell2(1, 1), new Color(0.08f, 0.46f, 0.94f, 0.78f), new Color(0.72f, 1.4f, 1.75f, 1f), false, 0.5f, new Vector4(0.018f, 0.035f, -0.02f, 0.01f));
            Add("BodyFrostPatches", cracks, Cell4(0, 2), new Color(0.22f, 0.66f, 1f, 0.68f), new Color(0.82f, 1.55f, 1.9f, 1f), false, 0.42f, new Vector4(-0.01f, 0.016f, 0.01f, -0.012f));
            Add("ColdMist", mist, Vector4.one, new Color(0.58f, 0.84f, 1f, 0.52f), new Color(0.82f, 1.1f, 1.3f, 1f), false, 0.1f, Vector4.zero);
            Add("SnowParticles", mist, Vector4.one, new Color(0.75f, 0.95f, 1f, 1f), new Color(1.4f, 1.8f, 2f, 1f), true, 0.35f, Vector4.zero);
            Add("FrostTrails", mist, Vector4.one, new Color(0.3f, 0.72f, 1f, 0.72f), new Color(1.1f, 1.7f, 2f, 1f), false, 0.35f, Vector4.zero);
            Add("SlowEnergyBands", surface, Cell2(0, 1), new Color(0.22f, 0.72f, 1f, 0.72f), new Color(1.35f, 1.95f, 2.25f, 1f), true, 1f, new Vector4(-0.25f, 0f, 0.04f, -0.02f));

            string distortionPath = MaterialFolder + "/FrostShock_Distortion.mat";
            Material distortion = AssetDatabase.LoadAssetAtPath<Material>(distortionPath);
            if (distortion == null)
            {
                distortion = new Material(distortionShader) { name = "FrostShock_Distortion" };
                AssetDatabase.CreateAsset(distortion, distortionPath);
            }
            distortion.shader = distortionShader;
            distortion.SetTexture("_NoiseMap", noise);
            distortion.SetFloat("_Strength", 0.018f);
            result["Distortion"] = distortion;
            EditorUtility.SetDirty(distortion);
            return result;

            void Add(string name, Texture texture, Vector4 rect, Color tint, Color hot, bool additive, float fresnel, Vector4 scroll)
            {
                string path = $"{MaterialFolder}/FrostShock_{name}.mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(shader) { name = "FrostShock_" + name };
                    AssetDatabase.CreateAsset(material, path);
                }

                material.shader = shader;
                material.SetTexture("_BaseMap", texture);
                material.SetTexture("_NoiseMap", noise);
                material.SetVector("_AtlasRect", rect);
                material.SetColor("_Tint", tint);
                material.SetColor("_HotTint", hot);
                material.SetFloat("_Opacity", 1f);
                material.SetFloat("_Brightness", 1f);
                material.SetFloat("_Dissolve", 0f);
                material.SetFloat("_DistortionStrength", 0.025f);
                material.SetFloat("_FresnelStrength", fresnel);
                material.SetVector("_Scroll", scroll);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
                material.SetFloat("_ZWrite", 0f);
                EditorUtility.SetDirty(material);
                result[name] = material;
            }
        }

        private static Dictionary<string, Mesh> CreateMeshes()
        {
            return new Dictionary<string, Mesh>
            {
                ["Disc"] = CreateOrUpdateMesh("FrostShock_Disc", CreateDiscMesh),
                ["Torus"] = CreateOrUpdateMesh("FrostShock_Torus", CreateTorusMesh),
                ["Tube"] = CreateOrUpdateMesh("FrostShock_Tube", CreateTubeMesh),
                ["Prism"] = CreateOrUpdateMesh("FrostShock_TaperedPrism", CreateTaperedPrismMesh),
                ["Plate"] = CreateOrUpdateMesh("FrostShock_IcePlate", CreateIcePlateMesh),
                ["Sphere"] = CreateOrUpdateMesh("FrostShock_Sphere", CreateSphereMesh)
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

        private static GameObject CreateCastPrefab(FrostShockVFXProfile profile, IReadOnlyDictionary<string, Mesh> meshes, IReadOnlyDictionary<string, Material> materials)
        {
            GameObject root = new("FrostShockCastVFX");
            Transform visuals = Section("Instant Hand Ignition", root.transform);
            CreateRenderer("Palm Flash", visuals, meshes["Sphere"], materials["EnergyCore"], Vector3.zero, Vector3.one * 0.34f, Quaternion.identity, 30);
            CreateRenderer("Compressed Frost Core", visuals, meshes["Sphere"], materials["IceHighlights"], new Vector3(0f, 0f, 0.1f), Vector3.one * 0.22f, Quaternion.identity, 31);
            for (int i = 0; i < profile.WristRibbonCount; i++)
            {
                CreateRenderer($"Wrist Frost Ribbon {i + 1}", visuals, meshes["Torus"], materials["SlowEnergyBands"], new Vector3(0f, -0.08f - i * 0.06f, 0f), Vector3.one * (0.32f + i * 0.05f), Quaternion.Euler(90f, i * 43f, 0f), 28 + i);
            }
            root.AddComponent<MMOAbilityVfxLifetime>().Configure(profile.CastDuration + 0.2f, true, true);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, CastPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateProjectilePrefab(FrostShockVFXProfile profile, IReadOnlyDictionary<string, Mesh> meshes, IReadOnlyDictionary<string, Material> materials)
        {
            GameObject root = new("FrostShockProjectileVFX");
            Transform castRoot = Section("Caster Hand Release", root.transform);
            Renderer handFlash = CreateRenderer("White Blue Palm Flash", castRoot, meshes["Sphere"], materials["EnergyCore"], Vector3.zero, Vector3.one, Quaternion.identity, 40);
            Renderer handCore = CreateRenderer("Compressed Frost Core", castRoot, meshes["Sphere"], materials["IceHighlights"], Vector3.zero, Vector3.one, Quaternion.identity, 41);
            Renderer[] ribbons = new Renderer[profile.WristRibbonCount];
            for (int i = 0; i < ribbons.Length; i++)
            {
                ribbons[i] = CreateRenderer($"Collapsing Wrist Ribbon {i + 1}", castRoot, meshes["Torus"], materials["SlowEnergyBands"], new Vector3(0f, -0.06f - i * 0.055f, 0f), Vector3.one, Quaternion.Euler(90f, i * 52f, 0f), 38 + i);
            }

            ParticleSystem release = CreateParticles("Backward Ice Release", castRoot, materials["IceShards"], false, false, 0, 12, 0.45f, 2.8f, 0.09f, 0.18f, 4, 4, meshes["Prism"], 42);
            Transform projectileRoot = Section("Dynamic Layered Frost Projectile", root.transform);
            Renderer[] layers =
            {
                CreateRenderer("White Hot Central Spear", projectileRoot, meshes["Prism"], materials["FrostSpear"], Vector3.zero, Vector3.one, Quaternion.identity, 52),
                CreateRenderer("Jagged Painted Ice Body", projectileRoot, meshes["Prism"], materials["ProjectileIceBody"], Vector3.zero, Vector3.one, Quaternion.Euler(0f, 0f, 30f), 50),
                CreateRenderer("Outer Cyan Frost Glow", projectileRoot, meshes["Prism"], materials["OuterGlow"], Vector3.zero, Vector3.one, Quaternion.Euler(0f, 0f, -18f), 49)
            };
            CreateRenderer("Subtle Frost Distortion", projectileRoot, meshes["Sphere"], materials["Distortion"], Vector3.zero, new Vector3(0.45f, 0.45f, 1.3f), Quaternion.identity, 48);
            ParticleSystem vapor = CreateParticles("World Space Vapor Trail", projectileRoot, materials["ColdMist"], true, true, 18, 0, profile.TrailFadeDuration, 0.12f, profile.VaporTrailWidth, 0.14f, 4, 4, null, 46);
            ParticleSystem shards = CreateParticles("World Space Ice Fragment Trail", projectileRoot, materials["IceShards"], true, true, profile.IceFragmentCount, 0, 0.5f, 0.45f, 0.07f, 0.1f, 4, 4, meshes["Prism"], 47);
            ParticleSystem snow = CreateParticles("World Space Snow Trail", projectileRoot, materials["SnowParticles"], true, true, profile.SnowTrailAmount, 0, 0.7f, 0.22f, 0.065f, 0.16f, 4, 4, null, 45);
            FrostShockProjectileVFX controller = root.AddComponent<FrostShockProjectileVFX>();
            controller.ConfigureAuthoring(profile, castRoot, projectileRoot, handFlash, handCore, ribbons, layers, release, vapor, shards, snow, true);
            root.AddComponent<MMOAbilityVfxPoolable>().ConfigureAuthoring(24);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ProjectilePath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateSlowPrefab(FrostShockVFXProfile profile, IReadOnlyDictionary<string, Mesh> meshes, IReadOnlyDictionary<string, Material> materials)
        {
            GameObject root = new("FrostShockSlowDebuffVFX");
            Transform slowRoot = Section("Six Second Attached Slow", root.transform);
            List<Renderer> feet = new();
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 foot = new(side * 0.24f, -0.78f, 0.02f);
                feet.Add(CreateRenderer(side < 0 ? "Left Foot Deep Ice" : "Right Foot Deep Ice", slowRoot, meshes["Plate"], materials["FootIceDark"], foot, new Vector3(0.55f, 0.48f, 0.72f), Quaternion.Euler(-8f, side * 18f, side * 5f), 14));
                feet.Add(CreateRenderer(side < 0 ? "Left Foot Main Plate" : "Right Foot Main Plate", slowRoot, meshes["Plate"], materials["FootIceMain"], foot + new Vector3(0f, 0.04f, 0f), new Vector3(0.48f, 0.42f, 0.66f), Quaternion.Euler(4f, side * -27f, side * -8f), 15));
                feet.Add(CreateRenderer(side < 0 ? "Left Foot Cyan Streak" : "Right Foot Cyan Streak", slowRoot, meshes["Plate"], materials["IceHighlights"], foot + new Vector3(0f, 0.07f, -0.015f), new Vector3(0.37f, 0.33f, 0.57f), Quaternion.Euler(10f, side * 31f, side * 12f), 16));
                feet.Add(CreateRenderer(side < 0 ? "Left Ankle Grip Shard" : "Right Ankle Grip Shard", slowRoot, meshes["Prism"], materials["IceHighlights"], foot + new Vector3(side * 0.08f, 0.23f, 0f), new Vector3(0.18f, 0.18f, 0.44f), Quaternion.Euler(-68f, side * 35f, 0f), 17));
            }

            List<Renderer> legs = new();
            for (int i = 0; i < 6; i++)
            {
                int side = i % 2 == 0 ? -1 : 1;
                legs.Add(CreateRenderer($"Broken Shin Frost {i + 1}", slowRoot, meshes["Plate"], materials["LowerLegFrost"], new Vector3(side * (0.24f + (i / 2) * 0.025f), -0.48f + (i / 2) * 0.18f, (i % 3 - 1) * 0.08f), new Vector3(0.28f, 0.42f, 0.18f), Quaternion.Euler(0f, side * (38f + i * 13f), side * 12f), 12 + i));
            }

            List<Renderer> body = new();
            Vector3[] bodyPositions = { new(-0.34f, 0.2f, 0.05f), new(0.31f, 0.38f, 0.02f), new(-0.42f, 0.68f, 0.01f), new(0.4f, 0.82f, 0.02f) };
            for (int i = 0; i < bodyPositions.Length; i++)
            {
                body.Add(CreateRenderer($"Restrained Body Frost Patch {i + 1}", slowRoot, meshes["Plate"], materials["BodyFrostPatches"], bodyPositions[i], new Vector3(0.28f, 0.32f, 0.14f), Quaternion.Euler(i * 19f, i % 2 == 0 ? 72f : -68f, i * 27f), 9 + i));
            }

            Renderer[] bands = new Renderer[profile.EnergyBandCount];
            for (int i = 0; i < bands.Length; i++)
            {
                bands[i] = CreateRenderer($"Broken Slow Energy Band {i + 1}", slowRoot, meshes["Tube"], materials["SlowEnergyBands"], new Vector3(0f, -0.4f + i * 0.34f, 0f), new Vector3(0.72f + i * 0.08f, 0.08f, 0.72f + i * 0.08f), Quaternion.identity, 20 + i);
            }

            Renderer[] crackFlickers = new Renderer[4];
            for (int i = 0; i < crackFlickers.Length; i++)
            {
                crackFlickers[i] = CreateRenderer($"Irregular Frost Crack Flicker {i + 1}", slowRoot, meshes["Plate"], materials["FrostCracks"], bodyPositions[i], new Vector3(0.34f, 0.4f, 0.12f), Quaternion.Euler(i * 17f, i % 2 == 0 ? 78f : -74f, i * 31f), 24 + i);
            }

            ParticleSystem mist = CreateParticles("Persistent Low Cold Mist", slowRoot, materials["ColdMist"], true, true, 0, 0, 1.15f, 0.18f, 0.34f, 0.52f, 4, 4, null, 6);
            ParticleSystem snow = CreateParticles("Sparse Persistent Snow", slowRoot, materials["SnowParticles"], true, true, 0, 0, 1.5f, 0.16f, 0.07f, 0.65f, 4, 4, null, 18);
            ParticleSystem trail = CreateParticles("World Space Movement Frost Trail", slowRoot, materials["FrostTrails"], true, true, 0, 0, profile.MovementTrailLifetime, 0.08f, 0.18f, 0.18f, 4, 4, null, 5);
            ParticleSystem crackParticles = CreateParticles("Crack Release Snow", slowRoot, materials["SnowParticles"], false, true, 0, 0, 0.45f, 0.7f, 0.055f, 0.38f, 4, 4, null, 26);

            Transform expirationRoot = Section("Thaw And Shatter Expiration", root.transform);
            Renderer[] expirationFragments = new Renderer[10];
            for (int i = 0; i < expirationFragments.Length; i++)
            {
                float angle = i / (float)expirationFragments.Length * Mathf.PI * 2f;
                expirationFragments[i] = CreateRenderer($"Detached Expiration Plate {i + 1}", expirationRoot, meshes["Prism"], materials[i % 2 == 0 ? "FootIceMain" : "IceHighlights"], new Vector3(Mathf.Cos(angle) * 0.25f, -0.55f + (i % 3) * 0.12f, Mathf.Sin(angle) * 0.25f), new Vector3(0.1f, 0.1f, 0.28f), Quaternion.Euler(Mathf.Sin(angle) * 55f, angle * Mathf.Rad2Deg, Mathf.Cos(angle) * 35f), 28 + i);
            }
            ParticleSystem shatter = CreateParticles("Expiration Mesh Shards", expirationRoot, materials["IceShards"], false, true, 0, 0, 0.8f, profile.ShatterVelocity, 0.1f, 0.34f, 4, 4, meshes["Prism"], 34);
            ParticleSystem finalMist = CreateParticles("Final World Mist", expirationRoot, materials["ColdMist"], false, true, 0, 0, 1.1f, 0.32f, 0.42f, 0.55f, 4, 4, null, 7);
            ParticleSystem finalSnow = CreateParticles("Final Snow Release", expirationRoot, materials["SnowParticles"], false, true, 0, 0, 0.9f, 0.8f, 0.07f, 0.48f, 4, 4, null, 35);
            FrostShockSlowDebuffVFX controller = root.AddComponent<FrostShockSlowDebuffVFX>();
            controller.ConfigureAuthoring(profile, slowRoot, expirationRoot, feet.ToArray(), legs.ToArray(), body.ToArray(), bands, crackFlickers, expirationFragments, mist, snow, trail, crackParticles, shatter, finalMist, finalSnow);
            slowRoot.gameObject.SetActive(false);
            expirationRoot.gameObject.SetActive(false);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, SlowPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateImpactPrefab(FrostShockVFXProfile profile, IReadOnlyDictionary<string, Mesh> meshes, IReadOnlyDictionary<string, Material> materials, GameObject slowPrefab)
        {
            GameObject root = new("FrostShockImpactVFX");
            Transform impactRoot = Section("Sharp Crystalline Impact", root.transform);
            Renderer flash = CreateRenderer("Contact White Flash", impactRoot, meshes["Sphere"], materials["EnergyCore"], Vector3.zero, Vector3.one, Quaternion.identity, 60);
            Renderer freezeShell = CreateRenderer("Temporary Body Freeze Shell", impactRoot, meshes["Tube"], materials["ProjectileIceBody"], new Vector3(0f, -0.65f, 0f), new Vector3(0.62f, 1.35f, 0.62f), Quaternion.identity, 42);
            Renderer[] explosion =
            {
                CreateRenderer("White Frost Explosion Core", impactRoot, meshes["Sphere"], materials["FrostBursts"], Vector3.zero, Vector3.one, Quaternion.identity, 58),
                CreateRenderer("Cyan Frost Explosion Body", impactRoot, meshes["Sphere"], materials["OuterGlow"], Vector3.zero, Vector3.one, Quaternion.Euler(17f, 41f, 9f), 55),
                CreateRenderer("Deep Blue Jagged Breakup", impactRoot, meshes["Sphere"], materials["ProjectileIceBody"], Vector3.zero, Vector3.one, Quaternion.Euler(-13f, -31f, 22f), 50)
            };
            Renderer[] rings =
            {
                CreateRenderer("Expanding Torso Cold Shock Ring", impactRoot, meshes["Torus"], materials["SlowEnergyBands"], Vector3.zero, Vector3.one, Quaternion.Euler(90f, 0f, 0f), 54),
                CreateRenderer("Breaking Lower Cold Ring", impactRoot, meshes["Torus"], materials["OuterGlow"], new Vector3(0f, -0.48f, 0f), Vector3.one, Quaternion.Euler(90f, 31f, 0f), 53)
            };
            Renderer[] radialShards = new Renderer[profile.MainShardCount];
            for (int i = 0; i < radialShards.Length; i++)
            {
                float angle = i / (float)radialShards.Length * Mathf.PI * 2f;
                float elevation = Mathf.Lerp(-0.28f, 0.55f, (i % 4) / 3f);
                Quaternion rotation = Quaternion.LookRotation(new Vector3(Mathf.Cos(angle), elevation, Mathf.Sin(angle)).normalized, Vector3.up);
                radialShards[i] = CreateRenderer($"Layered Radial Ice Shard {i + 1:00}", impactRoot, meshes["Prism"], materials[i % 3 == 0 ? "IceHighlights" : "ProjectileIceBody"], Vector3.zero, Vector3.one, rotation, 48 + i % 4);
            }
            Renderer[] cracks = new Renderer[profile.FrostCrackDensity];
            for (int i = 0; i < cracks.Length; i++)
            {
                float angle = i / (float)Mathf.Max(1, cracks.Length) * Mathf.PI * 2f;
                cracks[i] = CreateRenderer($"Body Crack Propagation {i + 1}", impactRoot, meshes["Plate"], materials["FrostCracks"], new Vector3(Mathf.Cos(angle) * 0.43f, -0.25f + (i % 3) * 0.38f, Mathf.Sin(angle) * 0.43f), new Vector3(0.45f, 0.52f, 0.12f), Quaternion.LookRotation(new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle))), 57);
            }
            ParticleSystem fragments = CreateParticles("Secondary Ice Fragments", impactRoot, materials["IceShards"], false, true, 0, profile.SecondaryFragmentCount, 0.85f, 3.2f, 0.09f, 0.3f, 4, 4, meshes["Prism"], 56);
            ParticleSystem mist = CreateParticles("Dense Initial Impact Mist", impactRoot, materials["ColdMist"], false, true, 0, profile.ImpactMistAmount, 0.85f, 0.55f, 0.38f, 0.48f, 4, 4, null, 45);
            ParticleSystem snow = CreateParticles("Impact Snow Burst", impactRoot, materials["SnowParticles"], false, true, 0, profile.SnowBurstAmount, 0.9f, 1.25f, 0.065f, 0.5f, 4, 4, null, 59);

            Transform groundRoot = Section("Detached World Ground Frost", root.transform);
            Renderer ground = CreateRenderer("Short Lived Frost Patch", groundRoot, meshes["Disc"], materials["GroundFrost"], Vector3.zero, Vector3.one, Quaternion.identity, 8);
            for (int i = 0; i < profile.GroundSpikeCount; i++)
            {
                float angle = i / (float)profile.GroundSpikeCount * Mathf.PI * 2f;
                CreateRenderer($"Ground Ice Spike {i + 1}", groundRoot, meshes["Prism"], materials["FootIceMain"], new Vector3(Mathf.Cos(angle) * 0.55f, 0.08f, Mathf.Sin(angle) * 0.55f), new Vector3(0.1f, 0.1f, 0.32f), Quaternion.Euler(-70f, -angle * Mathf.Rad2Deg, 0f), 9);
            }

            FrostShockImpactVFX controller = root.AddComponent<FrostShockImpactVFX>();
            controller.ConfigureAuthoring(profile, impactRoot, groundRoot, flash, freezeShell, explosion, rings, ground, radialShards, cracks, fragments, mist, snow);
            GameObject slowInstance = (GameObject)PrefabUtility.InstantiatePrefab(slowPrefab, root.transform);
            slowInstance.name = "Persistent Replicated Buff Slow";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ImpactPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateExpirationPrefab(FrostShockVFXProfile profile, IReadOnlyDictionary<string, Mesh> meshes, IReadOnlyDictionary<string, Material> materials)
        {
            GameObject root = new("FrostShockExpirationVFX");
            for (int i = 0; i < 12; i++)
            {
                float angle = i / 12f * Mathf.PI * 2f;
                CreateRenderer($"Thaw Shard {i + 1:00}", root.transform, meshes["Prism"], materials[i % 2 == 0 ? "FootIceMain" : "IceHighlights"], new Vector3(Mathf.Cos(angle) * 0.28f, (i % 3) * 0.1f, Mathf.Sin(angle) * 0.28f), new Vector3(0.1f, 0.1f, 0.3f), Quaternion.Euler(-55f, angle * Mathf.Rad2Deg, 0f), 20 + i % 3);
            }
            CreateParticles("Thaw Mist", root.transform, materials["ColdMist"], false, true, 0, profile.FinalMistAmount, 1.1f, 0.4f, 0.36f, 0.48f, 4, 4, null, 18);
            CreateParticles("Thaw Snow", root.transform, materials["SnowParticles"], false, true, 0, profile.FinalSnowAmount, 0.9f, 0.8f, 0.065f, 0.45f, 4, 4, null, 24);
            root.AddComponent<MMOAbilityVfxLifetime>().Configure(profile.FractureDuration + profile.FrostDissolveDuration + 0.5f, true, true);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ExpirationPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static void CreateCompletePrefab(FrostShockVFXProfile profile, GameObject projectile, GameObject impact, GameObject expiration)
        {
            GameObject root = new("FrostShockVFX");
            FrostShockVFX package = root.AddComponent<FrostShockVFX>();
            package.ConfigureAuthoring(profile, projectile, impact, expiration);
            PrefabUtility.SaveAsPrefabAsset(root, CompletePath);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void WireAbility(GameObject projectile, GameObject impact)
        {
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(DefinitionPath);
            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AbilityPath);
            if (definition == null || ability == null)
            {
                throw new MissingReferenceException("Frost Shock ability or VFX definition is missing.");
            }

            definition.Configure(null, projectile, impact, true, true, true, true,
                new Vector3(0f, 1.15f, 0.42f), Vector3.zero, new Vector3(0f, 1.18f, 0.48f), new Vector3(0f, 0.85f, 0f), 0f, true);
            ability.SetVisualEffects(definition);
            EditorUtility.SetDirty(definition);
            EditorUtility.SetDirty(ability);
        }

        private static MeshRenderer CreateRenderer(string name, Transform parent, Mesh mesh, Material material, Vector3 position, Vector3 scale, Quaternion rotation, int sortingOrder)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
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
            renderer.allowOcclusionWhenDynamic = false;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private static ParticleSystem CreateParticles(string name, Transform parent, Material material, bool loop, bool worldSpace, int rate, int burst, float lifetime, float speed, float size, float radius, int tilesX, int tilesY, Mesh mesh, int sortingOrder)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            ParticleSystem system = child.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = system.main;
            main.loop = loop;
            main.playOnAwake = false;
            main.simulationSpace = worldSpace ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.76f, lifetime * 1.15f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.62f, speed * 1.18f);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.7f, size * 1.25f);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            main.startRotationY = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            main.maxParticles = Mathf.Max(24, rate * Mathf.CeilToInt(lifetime) + burst * 2);
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = loop ? rate : 0;
            if (burst > 0) emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burst) });
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.18f;
            noise.frequency = 0.55f;
            ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
            color.enabled = true;
            Gradient gradient = new();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.52f, 0.88f, 1f), 0.7f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            ParticleSystem.TextureSheetAnimationModule sheet = system.textureSheetAnimation;
            sheet.enabled = tilesX > 1 || tilesY > 1;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.numTilesX = tilesX;
            sheet.numTilesY = tilesY;
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 0.999f);
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
            ParticleSystemRenderer renderer = child.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = sortingOrder;
            if (mesh != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Mesh;
                renderer.mesh = mesh;
            }
            else
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.alignment = ParticleSystemRenderSpace.View;
            }
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return system;
        }

        private static Transform Section(string name, Transform parent)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static Texture2D LoadTexture(string path)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) throw new MissingReferenceException($"Required Frost Shock texture is missing: {path}");
            return texture;
        }

        private static Vector4 Cell4(int column, int rowFromTop) => new(0.25f, 0.25f, column * 0.25f, (3 - rowFromTop) * 0.25f);
        private static Vector4 Cell2(int column, int rowFromTop) => new(0.5f, 0.5f, column * 0.5f, (1 - rowFromTop) * 0.5f);

        private static Mesh CreateDiscMesh()
        {
            const int segments = 48;
            Vector3[] vertices = new Vector3[segments + 1];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[segments * 3];
            vertices[0] = Vector3.zero; uv[0] = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                vertices[i + 1] = new Vector3(Mathf.Cos(a) * 0.5f, 0f, Mathf.Sin(a) * 0.5f);
                uv[i + 1] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.5f + Vector2.one * 0.5f;
                int t = i * 3; triangles[t] = 0; triangles[t + 1] = i + 1; triangles[t + 2] = (i + 1) % segments + 1;
            }
            return Mesh(vertices, uv, triangles);
        }

        private static Mesh CreateTorusMesh()
        {
            const int radial = 40, tube = 8;
            int row = tube + 1;
            Vector3[] vertices = new Vector3[(radial + 1) * row];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[radial * tube * 6];
            for (int r = 0; r <= radial; r++) for (int t = 0; t <= tube; t++)
            {
                float a = r / (float)radial * Mathf.PI * 2f, b = t / (float)tube * Mathf.PI * 2f;
                float radius = 0.42f + Mathf.Cos(b) * 0.08f;
                int index = r * row + t;
                vertices[index] = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(b) * 0.08f, Mathf.Sin(a) * radius);
                uv[index] = new Vector2(r / (float)radial, t / (float)tube);
            }
            int k = 0;
            for (int r = 0; r < radial; r++) for (int t = 0; t < tube; t++)
            {
                int a = r * row + t, b = a + row;
                triangles[k++] = a; triangles[k++] = b; triangles[k++] = a + 1;
                triangles[k++] = a + 1; triangles[k++] = b; triangles[k++] = b + 1;
            }
            return Mesh(vertices, uv, triangles);
        }

        private static Mesh CreateTubeMesh()
        {
            const int segments = 32;
            Vector3[] vertices = new Vector3[(segments + 1) * 2];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float u = i / (float)segments, a = u * Mathf.PI * 2f;
                for (int y = 0; y < 2; y++)
                {
                    int index = i * 2 + y;
                    vertices[index] = new Vector3(Mathf.Cos(a) * 0.5f, y - 0.5f, Mathf.Sin(a) * 0.5f);
                    uv[index] = new Vector2(u, y);
                }
            }
            int k = 0;
            for (int i = 0; i < segments; i++)
            {
                int a = i * 2, b = a + 2;
                triangles[k++] = a; triangles[k++] = a + 1; triangles[k++] = b;
                triangles[k++] = b; triangles[k++] = a + 1; triangles[k++] = b + 1;
            }
            return Mesh(vertices, uv, triangles);
        }

        private static Mesh CreateTaperedPrismMesh()
        {
            const int sides = 6;
            List<Vector3> vertices = new(); List<Vector2> uv = new(); List<int> triangles = new();
            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2f, a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
                int start = vertices.Count;
                vertices.Add(new Vector3(Mathf.Cos(a0) * 0.5f, Mathf.Sin(a0) * 0.5f, -0.5f));
                vertices.Add(new Vector3(Mathf.Cos(a1) * 0.5f, Mathf.Sin(a1) * 0.5f, -0.5f));
                vertices.Add(new Vector3(Mathf.Cos(a1) * 0.24f, Mathf.Sin(a1) * 0.24f, 0.22f));
                vertices.Add(new Vector3(Mathf.Cos(a0) * 0.24f, Mathf.Sin(a0) * 0.24f, 0.22f));
                vertices.Add(new Vector3(0f, 0f, 0.5f));
                uv.AddRange(new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 0.72f), new Vector2(0, 0.72f), new Vector2(0.5f, 1) });
                triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3, start + 3, start + 2, start + 4 });
            }
            return Mesh(vertices.ToArray(), uv.ToArray(), triangles.ToArray());
        }

        private static Mesh CreateIcePlateMesh()
        {
            Vector3[] top = { new(-0.5f, 0f, -0.25f), new(-0.18f, 0.08f, -0.5f), new(0.42f, 0.02f, -0.36f), new(0.52f, 0.06f, 0.16f), new(0.08f, 0.12f, 0.5f), new(-0.46f, 0.04f, 0.3f) };
            List<Vector3> vertices = new(); List<Vector2> uv = new(); List<int> triangles = new();
            vertices.AddRange(top);
            for (int i = 0; i < top.Length; i++) vertices.Add(top[i] + Vector3.down * 0.16f);
            for (int i = 1; i < top.Length - 1; i++) triangles.AddRange(new[] { 0, i, i + 1, top.Length, top.Length + i + 1, top.Length + i });
            for (int i = 0; i < top.Length; i++)
            {
                int next = (i + 1) % top.Length;
                triangles.AddRange(new[] { i, next, top.Length + next, i, top.Length + next, top.Length + i });
            }
            for (int i = 0; i < vertices.Count; i++) uv.Add(new Vector2(vertices[i].x + 0.5f, vertices[i].z + 0.5f));
            return Mesh(vertices.ToArray(), uv.ToArray(), triangles.ToArray());
        }

        private static Mesh CreateSphereMesh()
        {
            const int longitude = 20, latitude = 12;
            Vector3[] vertices = new Vector3[(longitude + 1) * (latitude + 1)];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[longitude * latitude * 6];
            for (int y = 0; y <= latitude; y++) for (int x = 0; x <= longitude; x++)
            {
                float u = x / (float)longitude, v = y / (float)latitude;
                float phi = u * Mathf.PI * 2f, theta = v * Mathf.PI;
                int index = y * (longitude + 1) + x;
                vertices[index] = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi)) * 0.5f;
                uv[index] = new Vector2(u, v);
            }
            int k = 0;
            for (int y = 0; y < latitude; y++) for (int x = 0; x < longitude; x++)
            {
                int a = y * (longitude + 1) + x, b = a + longitude + 1;
                triangles[k++] = a; triangles[k++] = b; triangles[k++] = a + 1;
                triangles[k++] = a + 1; triangles[k++] = b; triangles[k++] = b + 1;
            }
            return Mesh(vertices, uv, triangles);
        }

        private static Mesh Mesh(Vector3[] vertices, Vector2[] uv, int[] triangles)
        {
            Mesh mesh = new(); mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = triangles; mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
    }
}
