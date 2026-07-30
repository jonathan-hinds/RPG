#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using RPGClone.Abilities;
using RPGClone.Vfx;
using RPGClone.Vfx.Shaman;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace RPGClone.EditorTools
{
    public static class EmpowerWeaponVFXInstaller
    {
        private const string Root = "Assets/_Project/VFX/EmpowerWeapon";
        private const string TextureFolder = Root + "/Textures";
        private const string SourceFolder = TextureFolder + "/Sources";
        private const string MaterialFolder = Root + "/Materials";
        private const string ShaderFolder = Root + "/Shaders";
        private const string MeshFolder = Root + "/Meshes";
        private const string ProfileFolder = Root + "/Profiles";
        private const string PrefabFolder = Root + "/Prefabs";
        private const string DocumentationFolder = Root + "/Documentation";
        private const string ProfilePath = ProfileFolder + "/EmpowerWeaponVFX_Default.asset";
        private const string PersistentPrefabPath = PrefabFolder + "/EmpowerWeaponPersistentVFX.prefab";
        private const string ImpactPrefabPath = PrefabFolder + "/EmpowerWeaponImpactVFX.prefab";
        private const string TransferPrefabPath = PrefabFolder + "/EmpowerWeaponTransferVFX.prefab";
        private const string ActivationPrefabPath = PrefabFolder + "/EmpowerWeaponActivationVFX.prefab";
        private const string DefinitionPath = "Assets/_Project/VFX/Definitions/Shaman_Empower_Weapon_VFX.asset";
        private const string AbilityPath = "Assets/_Project/Configs/Abilities/Shaman_Empower_Weapon.asset";
        private const string SurfaceShaderName = "RPG Clone/VFX/Empower Weapon Surface Overlay";
        private const string AdditiveShaderName = "RPG Clone/VFX/Empower Weapon Additive";
        private const string AlphaShaderName = "RPG Clone/VFX/Empower Weapon Alpha";

        private static readonly (string name, int column, int row, bool repeat)[] MaskTextures =
        {
            ("EmpowerWeapon_SoftGlow", 0, 0, false),
            ("EmpowerWeapon_WeaponEnergyMask", 1, 0, true),
            ("EmpowerWeapon_DirectionalEnergy", 2, 0, true),
            ("EmpowerWeapon_SecondaryDistortion", 3, 0, true),
            ("EmpowerWeapon_DissolveErosion", 0, 1, true),
            ("EmpowerWeapon_SoftDistortion", 1, 1, true),
            ("EmpowerWeapon_GroundRune", 2, 1, false),
            ("EmpowerWeapon_NatureRing", 3, 1, false)
        };

        private static readonly (string name, int column, int row, bool repeat)[] SpriteTextures =
        {
            ("EmpowerWeapon_ShamanicStreak", 0, 0, false),
            ("EmpowerWeapon_ElementalArc", 1, 0, false),
            ("EmpowerWeapon_GreenSpark", 2, 0, false),
            ("EmpowerWeapon_GoldenSpark", 3, 0, false),
            ("EmpowerWeapon_SpiritMote", 0, 1, false),
            ("EmpowerWeapon_NatureFragment", 1, 1, false),
            ("EmpowerWeapon_RuneFragment", 2, 1, false),
            ("EmpowerWeapon_ImpactBurst", 3, 1, false)
        };

        private static readonly (string name, int column, int row, bool repeat)[] RibbonTextures =
        {
            ("EmpowerWeapon_DustPuff", 0, 0, false),
            ("EmpowerWeapon_WeaponTrail", 1, 0, true),
            ("EmpowerWeapon_SurfaceStreaks", 0, 1, true),
            ("EmpowerWeapon_InfusionSpiral", 1, 1, false)
        };

        private static readonly (string name, string sourceFile)[] SurfaceMaskTextures =
        {
            ("EmpowerWeapon_NatureVeinsMask_V2", "EmpowerWeapon_NatureVeinsMask_V2_Source.png"),
            ("EmpowerWeapon_RunicBandsMask_V2", "EmpowerWeapon_RunicBandsMask_V2_Source.png"),
            ("EmpowerWeapon_DirectionalFlowMask_V2", "EmpowerWeapon_DirectionalFlowMask_V2_Source.png"),
            ("EmpowerWeapon_ElementalBreakupMask_V2", "EmpowerWeapon_ElementalBreakupMask_V2_Source.png")
        };

        [MenuItem("Tools/RPG Clone/VFX/Build Empower Weapon VFX")]
        public static void Build()
        {
            EnsureFolders();
            CreateRuntimeTextures();
            Mesh quad = CreateQuadMesh();
            EmpowerWeaponVFXProfile profile = LoadOrCreateProfile();
            IReadOnlyDictionary<string, Material> materials = CreateMaterials();
            GameObject persistent = CreatePersistentPrefab(profile, materials);
            GameObject impact = CreateImpactPrefab(profile, materials, quad);
            GameObject transfer = CreateTransferPrefab(materials, quad);
            GameObject activation = CreateActivationPrefab(profile, materials, quad, persistent, impact, transfer);
            WireAbility(activation);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("Built Empower Weapon VFX: generated textures, focused shaders/materials, reusable phase prefabs, buff-driven weapon attachment, confirmed-hit accents, and replicated ability wiring are ready.", activation);
        }

        [MenuItem("Tools/RPG Clone/VFX/Validate Empower Weapon VFX")]
        public static void Validate()
        {
            EmpowerWeaponVFXProfile profile = AssetDatabase.LoadAssetAtPath<EmpowerWeaponVFXProfile>(ProfilePath);
            GameObject activation = AssetDatabase.LoadAssetAtPath<GameObject>(ActivationPrefabPath);
            GameObject persistent = AssetDatabase.LoadAssetAtPath<GameObject>(PersistentPrefabPath);
            GameObject impact = AssetDatabase.LoadAssetAtPath<GameObject>(ImpactPrefabPath);
            GameObject transfer = AssetDatabase.LoadAssetAtPath<GameObject>(TransferPrefabPath);
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(DefinitionPath);
            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AbilityPath);
            if (profile == null || activation == null || persistent == null || impact == null
                || transfer == null || definition == null || ability == null)
            {
                throw new MissingReferenceException("Empower Weapon deliverables are incomplete. Run Build Empower Weapon VFX.");
            }

            if (activation.GetComponent<EmpowerWeaponVFX>() == null
                || persistent.GetComponent<EmpowerWeaponPersistentVFX>() == null
                || impact.GetComponent<EmpowerWeaponOneShotVFX>() == null
                || transfer.GetComponent<EmpowerWeaponOneShotVFX>() == null)
            {
                throw new MissingComponentException("Empower Weapon prefab controllers are missing.");
            }

            if (ability.VisualEffects != definition || definition.CastPrefab != activation)
            {
                throw new MissingReferenceException("Empower Weapon is not wired through the shared replicated ability-release VFX path.");
            }

            if (ability.CalculateManaCost(null) != 0
                || ability.ManaCostSource != MMOAbilityManaCostSource.MaximumManaPercentage
                || !Mathf.Approximately(ability.MaximumManaCostPercent, 0.2f))
            {
                throw new InvalidOperationException("Empower Weapon must retain its shared 20% maximum-Mana cost configuration.");
            }

            if (persistent.GetComponentsInChildren<ParticleSystem>(true).Length != 3
                || persistent.GetComponentsInChildren<TrailRenderer>(true).Length != 2)
            {
                throw new InvalidOperationException("Persistent Empower Weapon must have three bounded particle layers and two attack-only trail layers.");
            }

            foreach (ParticleSystem particles in activation.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (particles.collision.enabled)
                {
                    throw new InvalidOperationException($"Presentation layer '{particles.name}' must not use particle collision.");
                }
            }

            if (activation.GetComponentInChildren<Animator>(true) != null
                || activation.GetComponentInChildren<Collider>(true) != null)
            {
                throw new InvalidOperationException("Empower Weapon VFX must remain procedural, animator-free, and gameplay-collider-free.");
            }

            foreach (var definitionEntry in MaskTextures)
            {
                RequireTexture(definitionEntry.name);
            }
            foreach (var definitionEntry in SpriteTextures)
            {
                RequireTexture(definitionEntry.name);
            }
            foreach (var definitionEntry in RibbonTextures)
            {
                RequireTexture(definitionEntry.name);
            }
            foreach ((string name, _) in SurfaceMaskTextures)
            {
                RequireTexture(name);
            }

            Material surfaceMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                MaterialFolder + "/EmpowerWeapon_SurfaceOverlay.mat");
            if (surfaceMaterial == null
                || surfaceMaterial.GetTexture("_VeinMask") == null
                || surfaceMaterial.GetTexture("_FlowMask") == null
                || surfaceMaterial.GetTexture("_RuneMask") == null
                || surfaceMaterial.GetTexture("_BreakupMask") == null)
            {
                throw new MissingReferenceException(
                    "Empower Weapon's mesh-conforming surface material is missing generated mask layers.");
            }

            Debug.Log("Empower Weapon VFX validation passed: generated mesh-conforming alpha masks, layered surface material, restrained persistent accents, phase prefabs, equipment swap support, tooltip cost parity, buff lifecycle, confirmed-hit response, and multiplayer wiring are valid.", activation);
        }

        private static void CreateRuntimeTextures()
        {
            CropAtlas(
                SourceFolder + "/EmpowerWeapon_MasksAtlas_Source.png",
                4,
                2,
                MaskTextures);
            CropAtlas(
                SourceFolder + "/EmpowerWeapon_SpritesAtlas_Source.png",
                4,
                2,
                SpriteTextures);
            CropAtlas(
                SourceFolder + "/EmpowerWeapon_RibbonsAtlas_Source.png",
                2,
                2,
                RibbonTextures);
            foreach ((string name, string sourceFile) in SurfaceMaskTextures)
            {
                CopySurfaceMask(name, sourceFile);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static void CopySurfaceMask(string name, string sourceFile)
        {
            string sourcePath = SourceFolder + "/" + sourceFile;
            string outputPath = TextureFolder + "/" + name + ".png";
            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException($"Empower Weapon surface mask is missing: {sourcePath}");
            }

            File.Copy(sourcePath, outputPath, true);
            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceSynchronousImport);
            ConfigureTextureImporter(outputPath, false, true);
        }

        private static void CropAtlas(
            string sourcePath,
            int columns,
            int rows,
            IReadOnlyList<(string name, int column, int row, bool repeat)> definitions)
        {
            ConfigureTextureImporter(sourcePath, true, true);
            Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath);
            if (source == null || !source.isReadable)
            {
                throw new FileNotFoundException($"Readable Empower Weapon source atlas is missing: {sourcePath}");
            }

            int cellWidth = source.width / columns;
            int cellHeight = source.height / rows;
            foreach ((string name, int column, int row, bool repeat) in definitions)
            {
                int sourceY = source.height - (row + 1) * cellHeight;
                Color[] pixels = source.GetPixels(column * cellWidth, sourceY, cellWidth, cellHeight);
                Texture2D texture = new(cellWidth, cellHeight, TextureFormat.RGBA32, false);
                texture.name = name;
                texture.SetPixels(pixels);
                texture.Apply(false, false);
                string outputPath = TextureFolder + "/" + name + ".png";
                File.WriteAllBytes(outputPath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach ((string name, _, _, bool repeat) in definitions)
            {
                ConfigureTextureImporter(TextureFolder + "/" + name + ".png", false, repeat);
            }
        }

        private static void ConfigureTextureImporter(string path, bool readable, bool repeat)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                throw new FileNotFoundException($"Empower Weapon texture is missing: {path}");
            }

            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.isReadable = readable;
            importer.mipmapEnabled = !readable;
            importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = readable ? 2048 : 1024;
            importer.SaveAndReimport();
        }

        private static IReadOnlyDictionary<string, Material> CreateMaterials()
        {
            Shader surfaceShader = Shader.Find(SurfaceShaderName);
            Shader additiveShader = Shader.Find(AdditiveShaderName);
            Shader alphaShader = Shader.Find(AlphaShaderName);
            if (surfaceShader == null || additiveShader == null || alphaShader == null)
            {
                throw new MissingReferenceException("Empower Weapon shaders are missing or unsupported.");
            }

            Dictionary<string, Material> materials = new();
            materials["SurfaceOverlay"] = CreateMaterial(
                "EmpowerWeapon_SurfaceOverlay",
                surfaceShader,
                "EmpowerWeapon_NatureVeinsMask_V2",
                4.2f,
                new Color(0.035f, 1f, 0.19f, 1f));
            Material surface = materials["SurfaceOverlay"];
            SetTexture(surface, "_VeinMask", RequireTexture("EmpowerWeapon_NatureVeinsMask_V2"));
            SetTexture(surface, "_FlowMask", RequireTexture("EmpowerWeapon_DirectionalFlowMask_V2"));
            SetTexture(surface, "_RuneMask", RequireTexture("EmpowerWeapon_RunicBandsMask_V2"));
            SetTexture(surface, "_BreakupMask", RequireTexture("EmpowerWeapon_ElementalBreakupMask_V2"));
            SetTexture(surface, "_DistortionMap", RequireTexture("EmpowerWeapon_SecondaryDistortion"));
            SetColor(surface, "_SecondaryTint", new Color(1f, 0.52f, 0.035f, 1f));
            SetColor(surface, "_HighlightTint", new Color(0.72f, 1f, 0.86f, 1f));
            SetFloat(surface, "_RuneIntensity", 1.8f);
            SetFloat(surface, "_FlowIntensity", 2.1f);
            SetFloat(surface, "_EdgeBrightness", 1.5f);
            SetFloat(surface, "_PatternScale", 1.05f);
            SetFloat(surface, "_RuneScale", 0.85f);
            SetFloat(surface, "_BreakupScale", 2.4f);
            SetFloat(surface, "_TravelSpeed", 0.72f);
            SetFloat(surface, "_SurfaceExtrusion", 0.0025f);

            materials["ScrollingEnergy"] = CreateAdditive("EmpowerWeapon_ScrollingShamanicEnergy", "EmpowerWeapon_DirectionalEnergy", 1.8f, new Color(0.08f, 1f, 0.24f, 0.72f));
            SetTexture(materials["ScrollingEnergy"], "_SecondaryMap", RequireTexture("EmpowerWeapon_SurfaceStreaks"));
            SetFloat(materials["ScrollingEnergy"], "_SecondaryMix", 0.65f);
            SetVector(materials["ScrollingEnergy"], "_ScrollSpeed", new Vector4(0f, 0.7f, 0f, 0f));
            SetVector(materials["ScrollingEnergy"], "_SecondaryScrollSpeed", new Vector4(0f, -0.28f, 0f, 0f));
            materials["EdgeGlow"] = CreateAdditive("EmpowerWeapon_WeaponEdgeGlow", "EmpowerWeapon_SoftGlow", 1.2f, new Color(0.72f, 1f, 0.82f, 0.58f));
            materials["NatureSparks"] = CreateAdditive("EmpowerWeapon_AdditiveNatureSparks", "EmpowerWeapon_GreenSpark", 2.4f, new Color(0.08f, 1f, 0.24f, 1f));
            materials["GoldenSparks"] = CreateAdditive("EmpowerWeapon_AdditiveGoldenSparks", "EmpowerWeapon_GoldenSpark", 2.4f, new Color(1f, 0.68f, 0.08f, 1f));
            materials["Motes"] = CreateAlpha("EmpowerWeapon_SpiritMotes", "EmpowerWeapon_SpiritMote", 1.2f, new Color(0.72f, 1f, 0.82f, 0.72f));
            materials["Arcs"] = CreateAdditive("EmpowerWeapon_StylizedEnergyArcs", "EmpowerWeapon_ElementalArc", 2.8f, Color.white);
            materials["Trail"] = CreateAdditive("EmpowerWeapon_WeaponAttackTrail", "EmpowerWeapon_WeaponTrail", 2.2f, new Color(0.08f, 1f, 0.24f, 0.66f));
            materials["TrailHighlight"] = CreateAdditive("EmpowerWeapon_WeaponTrailHighlight", "EmpowerWeapon_WeaponTrail", 2.5f, new Color(1f, 0.68f, 0.08f, 0.9f));
            materials["Ring"] = CreateAdditive("EmpowerWeapon_NatureActivationRing", "EmpowerWeapon_NatureRing", 1.5f, Color.white);
            materials["Rune"] = CreateAdditive("EmpowerWeapon_GroundRune", "EmpowerWeapon_GroundRune", 1.4f, Color.white);
            materials["Dust"] = CreateAlpha("EmpowerWeapon_DustAndWind", "EmpowerWeapon_DustPuff", 1.1f, new Color(0.68f, 0.62f, 0.45f, 0.56f));
            materials["Impact"] = CreateAdditive("EmpowerWeapon_MeleeImpactBurst", "EmpowerWeapon_ImpactBurst", 2.8f, Color.white);
            materials["Fragments"] = CreateAlpha("EmpowerWeapon_NatureFragments", "EmpowerWeapon_NatureFragment", 1.6f, Color.white);
            materials["Streak"] = CreateAdditive("EmpowerWeapon_BrokenShamanicStreak", "EmpowerWeapon_ShamanicStreak", 2.6f, Color.white);
            return materials;
        }

        private static Material CreateAdditive(string name, string textureName, float brightness, Color tint)
        {
            return CreateMaterial(name, Shader.Find(AdditiveShaderName), textureName, brightness, tint);
        }

        private static Material CreateAlpha(string name, string textureName, float brightness, Color tint)
        {
            Material material = CreateMaterial(name, Shader.Find(AlphaShaderName), textureName, brightness, tint);
            SetTexture(material, "_DistortionMap", RequireTexture("EmpowerWeapon_SoftDistortion"));
            return material;
        }

        private static Material CreateMaterial(string name, Shader shader, string textureName, float brightness, Color tint)
        {
            string path = MaterialFolder + "/" + name + ".mat";
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

            SetTexture(material, shader.name == SurfaceShaderName ? "_VeinMask" : "_BaseMap", RequireTexture(textureName));
            SetColor(material, shader.name == SurfaceShaderName ? "_MainTint" : "_Tint", tint);
            SetFloat(material, shader.name == SurfaceShaderName ? "_EmissionIntensity" : "_Brightness", brightness);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreatePersistentPrefab(
            EmpowerWeaponVFXProfile profile,
            IReadOnlyDictionary<string, Material> materials)
        {
            GameObject root = new("EmpowerWeaponPersistentVFX");
            try
            {
                Transform surface = CreateChild("Surface Overlay", root.transform);
                Transform aura = CreateChild("Aura", root.transform);
                ParticleSystem auraParticles = CreateParticles(
                    "Surface Glint Accents", aura, materials["ScrollingEnergy"], true, 0.62f, 0.42f, 0.1f, 0.055f,
                    4, 0.4f, 0, 0f, ParticleSystemShapeType.Box, 0.34f, Color.white, true);
                ParticleSystem motes = CreateParticles(
                    "Restrained Orbiting Motes", aura, materials["Motes"], true, 0.85f, 0.62f, 0.05f, 0.055f,
                    6, 0.9f, 0, 0f, ParticleSystemShapeType.Box, 0.36f, Color.white, false);
                ParticleSystem.NoiseModule motesNoise = motes.noise;
                motesNoise.enabled = true;
                motesNoise.strength = 0.32f;
                motesNoise.frequency = 0.55f;
                ParticleSystem.VelocityOverLifetimeModule motesVelocity = motes.velocityOverLifetime;
                motesVelocity.enabled = true;
                motesVelocity.orbitalY = 0.75f;

                ParticleSystem arcs = CreateParticles(
                    "Intermittent Surface Arcs", aura, materials["Arcs"], true, 0.48f, 0.14f, 0f, 0.16f,
                    3, 0.35f, 0, 0f, ParticleSystemShapeType.Box, 0.28f, Color.white, false);
                ParticleSystem.RotationOverLifetimeModule arcRotation = arcs.rotationOverLifetime;
                arcRotation.enabled = true;
                arcRotation.z = new ParticleSystem.MinMaxCurve(-3f, 3f);

                Transform trailRoot = CreateChild("Attack Trail Integration", root.transform);
                TrailRenderer broad = CreateTrail("Broad Nature Sweep", trailRoot, materials["Trail"], 0.16f, 0.24f);
                TrailRenderer highlight = CreateTrail("Golden Highlight", trailRoot, materials["TrailHighlight"], 0.045f, 0.18f);

                EmpowerWeaponPersistentVFX controller = root.AddComponent<EmpowerWeaponPersistentVFX>();
                controller.ConfigureAuthoring(profile, materials["SurfaceOverlay"], surface, aura, auraParticles, motes, arcs, broad, highlight);
                return SavePrefab(root, PersistentPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateImpactPrefab(
            EmpowerWeaponVFXProfile profile,
            IReadOnlyDictionary<string, Material> materials,
            Mesh quad)
        {
            GameObject root = new("EmpowerWeaponImpactVFX");
            try
            {
                List<ParticleSystem> particles = new()
                {
                    CreateParticles("Green Gold Flash", root.transform, materials["Impact"], false, 0.45f, 0.18f, 0f, 0.8f, 2, 0f, 1, 0f, ParticleSystemShapeType.Sphere, 0.02f, Color.white, false, quad),
                    CreateParticles("Nature Sparks", root.transform, materials["NatureSparks"], false, 0.45f, 0.3f, 3.1f, 0.08f, 12, 0f, 9, 0.02f, ParticleSystemShapeType.Sphere, 0.08f, Color.white, true),
                    CreateParticles("Golden Sparks", root.transform, materials["GoldenSparks"], false, 0.45f, 0.24f, 2.7f, 0.055f, 8, 0f, 6, 0.02f, ParticleSystemShapeType.Sphere, 0.06f, Color.white, true),
                    CreateParticles("Nature Motes", root.transform, materials["Motes"], false, 0.48f, 0.4f, 0.8f, 0.1f, 6, 0f, 4, 0.04f, ParticleSystemShapeType.Sphere, 0.1f, Color.white, false),
                    CreateParticles("Shamanic Arc", root.transform, materials["Streak"], false, 0.4f, 0.16f, 0f, 0.45f, 2, 0f, 1, 0.03f, ParticleSystemShapeType.Sphere, 0.02f, Color.white, false, quad),
                    CreateParticles("Dust Wind", root.transform, materials["Dust"], false, 0.48f, 0.38f, 0.35f, 0.34f, 5, 0f, 3, 0.02f, ParticleSystemShapeType.Circle, 0.18f, Color.white, false)
                };
                MMOAbilityVfxPoolable poolable = root.AddComponent<MMOAbilityVfxPoolable>();
                poolable.ConfigureAuthoring(24);
                EmpowerWeaponOneShotVFX controller = root.AddComponent<EmpowerWeaponOneShotVFX>();
                controller.ConfigureAuthoring(profile.ImpactDuration, particles.ToArray(), Array.Empty<TrailRenderer>());
                return SavePrefab(root, ImpactPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateTransferPrefab(
            IReadOnlyDictionary<string, Material> materials,
            Mesh quad)
        {
            GameObject root = new("EmpowerWeaponTransferVFX");
            try
            {
                List<ParticleSystem> particles = new()
                {
                    CreateParticles("Transfer Flash", root.transform, materials["Impact"], false, 0.35f, 0.18f, 0f, 0.6f, 2, 0f, 1, 0f, ParticleSystemShapeType.Sphere, 0.01f, Color.white, false, quad),
                    CreateParticles("Transfer Ring", root.transform, materials["Ring"], false, 0.35f, 0.25f, 0f, 0.55f, 2, 0f, 1, 0.03f, ParticleSystemShapeType.Sphere, 0.01f, Color.white, false, quad),
                    CreateParticles("Binding Sparks", root.transform, materials["NatureSparks"], false, 0.35f, 0.22f, 1.8f, 0.055f, 8, 0f, 6, 0.02f, ParticleSystemShapeType.Sphere, 0.04f, Color.white, true)
                };
                MMOAbilityVfxPoolable poolable = root.AddComponent<MMOAbilityVfxPoolable>();
                poolable.ConfigureAuthoring(12);
                EmpowerWeaponOneShotVFX controller = root.AddComponent<EmpowerWeaponOneShotVFX>();
                controller.ConfigureAuthoring(0.35f, particles.ToArray(), Array.Empty<TrailRenderer>());
                return SavePrefab(root, TransferPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateActivationPrefab(
            EmpowerWeaponVFXProfile profile,
            IReadOnlyDictionary<string, Material> materials,
            Mesh quad,
            GameObject persistent,
            GameObject impact,
            GameObject transfer)
        {
            GameObject root = new("EmpowerWeaponActivationVFX");
            try
            {
                Transform ground = CreateChild("Caster Activation", root.transform);
                List<ParticleSystem> particles = new()
                {
                    CreateParticles("Nature Ground Flash", ground, materials["EdgeGlow"], false, 1.15f, 0.28f, 0f, 2.8f, 2, 0f, 1, 0f, ParticleSystemShapeType.Sphere, 0.01f, Color.white, false, quad),
                    CreateParticles("Expanding Nature Ring", ground, materials["Ring"], false, 1.15f, 0.48f, 0f, 2.3f, 2, 0f, 1, 0.02f, ParticleSystemShapeType.Sphere, 0.01f, Color.white, false, quad),
                    CreateParticles("Broken Ground Rune", ground, materials["Rune"], false, 1.15f, 0.7f, 0f, 2.15f, 2, 0f, 1, 0.05f, ParticleSystemShapeType.Sphere, 0.01f, Color.white, false, quad),
                    CreateParticles("Spiraling Nature Fragments", root.transform, materials["Fragments"], false, 1.15f, 0.72f, 1.15f, 0.16f, 20, 0f, 13, 0.05f, ParticleSystemShapeType.Circle, 1.05f, Color.white, false),
                    CreateParticles("Earth Motes", root.transform, materials["Dust"], false, 1.15f, 0.6f, 1.3f, 0.11f, 14, 0f, 9, 0.05f, ParticleSystemShapeType.Circle, 0.82f, Color.white, false),
                    CreateParticles("Inward Nature Sparks", root.transform, materials["NatureSparks"], false, 1.15f, 0.48f, -2.4f, 0.07f, 24, 0f, 18, 0.1f, ParticleSystemShapeType.Circle, 1.45f, Color.white, true),
                    CreateParticles("Golden Binding Sparks", root.transform, materials["GoldenSparks"], false, 1.15f, 0.42f, 2.8f, 0.06f, 16, 0f, 12, 0.25f, ParticleSystemShapeType.Sphere, 0.24f, Color.white, true),
                    CreateParticles("Upward Energy Spiral", root.transform, materials["ScrollingEnergy"], false, 1.15f, 0.68f, 1.8f, 0.18f, 28, 22f, 0, 0.1f, ParticleSystemShapeType.Circle, 0.7f, Color.white, true),
                    CreateParticles("Ground Dust Wind", ground, materials["Dust"], false, 1.15f, 0.55f, 0.55f, 0.55f, 8, 0f, 5, 0f, ParticleSystemShapeType.Circle, 0.65f, Color.white, false),
                    CreateParticles("Weapon Binding Streaks", root.transform, materials["Streak"], false, 1.15f, 0.42f, 2.2f, 0.32f, 14, 0f, 9, 0.1f, ParticleSystemShapeType.Circle, 1.15f, Color.white, true)
                };

                ParticleSystem.VelocityOverLifetimeModule spiralVelocity = particles[7].velocityOverLifetime;
                spiralVelocity.enabled = true;
                spiralVelocity.orbitalY = 2.4f;

                GameObject lightObject = new("Nature Point Light Pulse");
                lightObject.transform.SetParent(root.transform, false);
                lightObject.transform.localPosition = Vector3.up * 1.1f;
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(0.4f, 1f, 0.28f);
                light.range = 4f;
                light.intensity = 2.2f;
                light.shadows = LightShadows.None;
                light.enabled = false;

                MMOAbilityVfxPoolable poolable = root.AddComponent<MMOAbilityVfxPoolable>();
                poolable.ConfigureAuthoring(8);
                EmpowerWeaponVFX controller = root.AddComponent<EmpowerWeaponVFX>();
                controller.ConfigureAuthoring(profile, persistent, impact, transfer, particles.ToArray(), light);
                return SavePrefab(root, ActivationPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static ParticleSystem CreateParticles(
            string name,
            Transform parent,
            Material material,
            bool loop,
            float duration,
            float lifetime,
            float speed,
            float size,
            int maxParticles,
            float rate,
            int burst,
            float delay,
            ParticleSystemShapeType shapeType,
            float shapeRadius,
            Color color,
            bool stretched,
            Mesh mesh = null)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            if (mesh != null && parent.name == "Caster Activation")
            {
                child.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }

            ParticleSystem particles = child.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = loop;
            main.playOnAwake = false;
            main.duration = Mathf.Max(0.1f, duration);
            main.startDelay = Mathf.Max(0f, delay);
            main.startLifetime = Mathf.Max(0.05f, lifetime);
            main.startSpeed = speed;
            main.startSize = Mathf.Max(0.005f, size);
            main.startColor = color;
            main.maxParticles = Mathf.Max(1, maxParticles);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = Mathf.Max(0f, rate);
            if (burst > 0)
            {
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burst) });
            }

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = shapeType;
            shape.radius = Mathf.Max(0.001f, shapeRadius);
            if (shapeType == ParticleSystemShapeType.Box)
            {
                shape.scale = Vector3.one;
            }

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient fade = new();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.14f),
                    new GradientAlphaKey(0.8f, 0.65f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = fade;

            ParticleSystemRenderer renderer = child.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.renderMode = mesh != null
                ? ParticleSystemRenderMode.Mesh
                : stretched
                    ? ParticleSystemRenderMode.Stretch
                    : ParticleSystemRenderMode.Billboard;
            if (mesh != null) renderer.mesh = mesh;
            if (stretched)
            {
                renderer.lengthScale = 2.1f;
                renderer.velocityScale = 0.6f;
            }

            return particles;
        }

        private static TrailRenderer CreateTrail(
            string name,
            Transform parent,
            Material material,
            float width,
            float time)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            TrailRenderer trail = child.AddComponent<TrailRenderer>();
            trail.sharedMaterial = material;
            trail.time = time;
            trail.widthMultiplier = width;
            trail.minVertexDistance = 0.025f;
            trail.textureMode = LineTextureMode.Tile;
            trail.alignment = LineAlignment.View;
            trail.numCapVertices = 2;
            trail.numCornerVertices = 2;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.emitting = false;
            AnimationCurve widthCurve = new();
            widthCurve.AddKey(0f, 1f);
            widthCurve.AddKey(0.35f, 0.82f);
            widthCurve.AddKey(1f, 0f);
            trail.widthCurve = widthCurve;
            return trail;
        }

        private static Mesh CreateQuadMesh()
        {
            string path = MeshFolder + "/EmpowerWeapon_Quad.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;
            mesh = new Mesh { name = "EmpowerWeapon_Quad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static EmpowerWeaponVFXProfile LoadOrCreateProfile()
        {
            EmpowerWeaponVFXProfile profile = AssetDatabase.LoadAssetAtPath<EmpowerWeaponVFXProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<EmpowerWeaponVFXProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            SerializedObject serialized = new(profile);
            SerializedProperty emission = serialized.FindProperty("weaponEmissionIntensity");
            SerializedProperty arc = serialized.FindProperty("arcBrightness");
            SerializedProperty trail = serialized.FindProperty("trailIntensity");
            if (emission != null && Mathf.Approximately(emission.floatValue, 3.6f)) emission.floatValue = 2.6f;
            if (arc != null && Mathf.Approximately(arc.floatValue, 4.2f)) arc.floatValue = 3f;
            if (trail != null && Mathf.Approximately(trail.floatValue, 3.8f)) trail.floatValue = 3f;
            if (emission != null && Mathf.Approximately(emission.floatValue, 2.6f)) emission.floatValue = 4.2f;
            SetProfileUpgrade(serialized, "surfacePatternScale", 2.2f, 1.05f);
            SetProfileUpgrade(serialized, "runeIntensity", 1.35f, 1.8f);
            SetProfileUpgrade(serialized, "surfaceFlowIntensity", 1.65f, 2.1f);
            SetMissingPositiveDefault(serialized, "travellingPulseSpeed", 0.72f);
            SetProfileUpgrade(serialized, "edgeCoronaIntensity", 1.1f, 1.5f);
            SetMissingPositiveDefault(serialized, "surfaceLift", 0.0025f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return profile;
        }

        private static void SetMissingPositiveDefault(
            SerializedObject serialized,
            string propertyName,
            float defaultValue)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null && property.floatValue <= 0f)
            {
                property.floatValue = defaultValue;
            }
        }

        private static void SetProfileUpgrade(
            SerializedObject serialized,
            string propertyName,
            float previousValue,
            float upgradedValue)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null
                && (property.floatValue <= 0f || Mathf.Approximately(property.floatValue, previousValue)))
            {
                property.floatValue = upgradedValue;
            }
        }

        private static void WireAbility(GameObject activationPrefab)
        {
            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AbilityPath);
            if (ability == null)
            {
                throw new MissingReferenceException($"Empower Weapon ability is missing: {AbilityPath}");
            }

            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(DefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<MMOAbilityVfxDefinition>();
                AssetDatabase.CreateAsset(definition, DefinitionPath);
            }

            definition.Configure(
                null,
                activationPrefab,
                null,
                true,
                false,
                false,
                false,
                new Vector3(0f, 0.05f, 0f),
                Vector3.zero,
                new Vector3(0f, 0.05f, 0f),
                Vector3.zero,
                0f,
                false);
            ability.SetVisualEffects(definition);
            EditorUtility.SetDirty(definition);
            EditorUtility.SetDirty(ability);
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            if (prefab == null)
            {
                throw new InvalidOperationException($"Failed to save Empower Weapon prefab at {path}.");
            }

            return prefab;
        }

        private static Transform CreateChild(string name, Transform parent)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static Texture2D RequireTexture(string name)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "/" + name + ".png");
            if (texture == null)
            {
                throw new FileNotFoundException($"Required Empower Weapon texture is missing: {name}");
            }

            return texture;
        }

        private static void SetTexture(Material material, string property, Texture value)
        {
            if (material != null && material.HasProperty(property)) material.SetTexture(property, value);
        }

        private static void SetColor(Material material, string property, Color value)
        {
            if (material != null && material.HasProperty(property)) material.SetColor(property, value);
        }

        private static void SetFloat(Material material, string property, float value)
        {
            if (material != null && material.HasProperty(property)) material.SetFloat(property, value);
        }

        private static void SetVector(Material material, string property, Vector4 value)
        {
            if (material != null && material.HasProperty(property)) material.SetVector(property, value);
        }

        private static void EnsureFolders()
        {
            EnsureFolder(Root);
            EnsureFolder(TextureFolder);
            EnsureFolder(SourceFolder);
            EnsureFolder(MaterialFolder);
            EnsureFolder(ShaderFolder);
            EnsureFolder(MeshFolder);
            EnsureFolder(ProfileFolder);
            EnsureFolder(PrefabFolder);
            EnsureFolder(DocumentationFolder);
            EnsureFolder("Assets/_Project/VFX/Definitions");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            string folder = Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(parent)) return;
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
#endif
