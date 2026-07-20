using System;
using System.IO;
using RPGClone.Characters;
using RPGClone.Inventory;
using UnityEditor;
using UnityEngine;

namespace RPGClone.EditorTools
{
    public static class MMONpcAppearanceContentInstaller
    {
        private const string SourceRoot = "Assets/_Project/New Equipment";
        private const string HairRoot = SourceRoot + "/Hair";
        private const string ArmorRoot = "Assets/_Project/Equipment/Armor";

        private static readonly ArmorVisualSpec[] ArmorVisuals =
        {
            new("Butcher", "Chest", "Leather", "Bloodstained Butcher Apron", MMOEquipmentSlotType.Chest, MMOCharacterBodyPart.Torso),
            new("Butcher", "Gloves", "Leather", "Gorecleaver Grips", MMOEquipmentSlotType.Hands, MMOCharacterBodyPart.Hands),
            new("Butcher", "Pants", "Leather", "Slaughterhouse Leggings", MMOEquipmentSlotType.Legs, MMOCharacterBodyPart.Legs),
            new("Butcher", "Boots", "Leather", "Bloodslick Workboots", MMOEquipmentSlotType.Feet, MMOCharacterBodyPart.Feet),
            new("Merchent", "Chest", "Cloth", "Gilded Caravan Vestments", MMOEquipmentSlotType.Chest, MMOCharacterBodyPart.Torso),
            new("Merchent", "Gloves", "Cloth", "Coinweave Gloves", MMOEquipmentSlotType.Hands, MMOCharacterBodyPart.Hands),
            new("Merchent", "Pants", "Cloth", "Gilded Caravan Breeches", MMOEquipmentSlotType.Legs, MMOCharacterBodyPart.Legs, "Legs"),
            new("Merchent", "Boots", "Cloth", "Cointrail Boots", MMOEquipmentSlotType.Feet, MMOCharacterBodyPart.Feet),
            new("Seer", "Chest", "Cloth", "Astral Seer Vestments", MMOEquipmentSlotType.Chest, MMOCharacterBodyPart.Torso),
            new("Seer", "Gloves", "Cloth", "Starwoven Handwraps", MMOEquipmentSlotType.Hands, MMOCharacterBodyPart.Hands),
            new("Seer", "Pants", "Cloth", "Astral Seer Trousers", MMOEquipmentSlotType.Legs, MMOCharacterBodyPart.Legs),
            new("Seer", "Boots", "Cloth", "Celestial Sandals", MMOEquipmentSlotType.Feet, MMOCharacterBodyPart.Feet),
            new("Traveler", "Chest", "Leather", "Verdant Wayfarer Jerkin", MMOEquipmentSlotType.Chest, MMOCharacterBodyPart.Torso),
            new("Traveler", "Gloves", "Leather", "Wayfarer Grips", MMOEquipmentSlotType.Hands, MMOCharacterBodyPart.Hands),
            new("Traveler", "Pants", "Leather", "Verdant Trail Leggings", MMOEquipmentSlotType.Legs, MMOCharacterBodyPart.Legs),
            new("Traveler", "Boots", "Leather", "Longroad Treads", MMOEquipmentSlotType.Feet, MMOCharacterBodyPart.Feet),
            new("Warchief", "Chest", "Mail", "Bloodbanner Hauberk", MMOEquipmentSlotType.Chest, MMOCharacterBodyPart.Torso),
            new("Warchief", "Gloves", "Mail", "Bloodbanner Gauntlets", MMOEquipmentSlotType.Hands, MMOCharacterBodyPart.Hands),
            new("Warchief", "Pants", "Mail", "Bloodbanner Legguards", MMOEquipmentSlotType.Legs, MMOCharacterBodyPart.Legs),
            new("Warchief", "Boots", "Mail", "Bloodbanner Warboots", MMOEquipmentSlotType.Feet, MMOCharacterBodyPart.Feet)
        };

        private static readonly HairMaskSpec[] HairMasks =
        {
            new("Hairstyle4.JPEG", "HairStyle4_ColorMask.png"),
            new("golden_hair_3d_model_basecolor.JPEG", "HairStyle5_ColorMask.png"),
            new("Hairstyle6.JPEG", "HairStyle6_ColorMask.png")
        };

        [MenuItem("Tools/RPG Clone/Characters/Install New NPC and Appearance Visuals")]
        public static void InstallNewVisualContent()
        {
            CreateHairColorMasks();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            foreach (ArmorVisualSpec spec in ArmorVisuals)
            {
                CreateOrUpdateArmorVisual(spec);
            }

            CleanupEmptyArmorSourceFolders();

            MMOCharacterSelectionInstaller.RefreshCharacterAppearanceCatalog();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"Installed {ArmorVisuals.Length} NPC-only armor visuals, {HairMasks.Length} hairstyles, " +
                "and 3 face textures. No obtainable armor items were created.");
        }

        private static void CreateHairColorMasks()
        {
            foreach (HairMaskSpec spec in HairMasks)
            {
                string sourcePath = $"{HairRoot}/{spec.SourceFileName}";
                string outputPath = $"{HairRoot}/{spec.OutputFileName}";
                if (!File.Exists(sourcePath))
                {
                    throw new FileNotFoundException($"Hair color source texture was not found: {sourcePath}", sourcePath);
                }

                Texture2D source = new(2, 2, TextureFormat.RGBA32, false, false);
                Texture2D mask = null;
                try
                {
                    if (!ImageConversion.LoadImage(source, File.ReadAllBytes(sourcePath), false))
                    {
                        throw new InvalidOperationException($"Unity could not decode hair texture '{sourcePath}'.");
                    }

                    Color32[] pixels = source.GetPixels32();
                    for (int index = 0; index < pixels.Length; index++)
                    {
                        Color32 pixel = pixels[index];
                        byte luminance = (byte)Mathf.Clamp(
                            Mathf.RoundToInt(0.2126f * pixel.r + 0.7152f * pixel.g + 0.0722f * pixel.b),
                            0,
                            255);
                        pixels[index] = new Color32(luminance, luminance, luminance, pixel.a);
                    }

                    mask = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, false);
                    mask.SetPixels32(pixels);
                    mask.Apply(false, false);
                    File.WriteAllBytes(outputPath, mask.EncodeToPNG());
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(source);
                    if (mask != null)
                    {
                        UnityEngine.Object.DestroyImmediate(mask);
                    }
                }
            }
        }

        private static void CreateOrUpdateArmorVisual(ArmorVisualSpec spec)
        {
            string pieceFolder = $"{ArmorRoot}/{spec.WeightFolder}/{spec.DisplayName}";
            EnsureFolder(pieceFolder);
            string assetStem = Sanitize(spec.DisplayName);
            string modelPath = MoveSourceAsset(
                spec,
                spec.SourceModelStem,
                "fbx",
                $"{pieceFolder}/{assetStem}.fbx");
            string texturePath = MoveSourceAsset(
                spec,
                spec.SourceTextureStem,
                "png",
                $"{pieceFolder}/T_{assetStem}_BaseColor.png");
            AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (model == null || texture == null)
            {
                throw new InvalidOperationException(
                    $"NPC armor visual '{spec.DisplayName}' is missing its model or texture.");
            }

            if (model.GetComponentInChildren<SkinnedMeshRenderer>(true) == null)
            {
                throw new InvalidOperationException($"NPC armor model '{modelPath}' has no SkinnedMeshRenderer.");
            }

            Material material = GetOrCreateMaterial($"{pieceFolder}/M_{assetStem}.mat", texture);
            string visualPath = $"{pieceFolder}/EV_{assetStem}.asset";
            MMOEquipmentVisualDefinition visual =
                AssetDatabase.LoadAssetAtPath<MMOEquipmentVisualDefinition>(visualPath);
            if (visual == null)
            {
                visual = ScriptableObject.CreateInstance<MMOEquipmentVisualDefinition>();
                AssetDatabase.CreateAsset(visual, visualPath);
            }

            visual.name = $"EV_{assetStem}";
            visual.Configure(
                spec.Slot,
                spec.BodyPart,
                true,
                model,
                material,
                false,
                Color.white,
                texture,
                null,
                Vector3.zero,
                Vector3.zero,
                Vector3.one);
            EditorUtility.SetDirty(visual);
        }

        private static string MoveSourceAsset(
            ArmorVisualSpec spec,
            string sourceStem,
            string extension,
            string destinationPath)
        {
            if (AssetDatabase.LoadMainAssetAtPath(destinationPath) != null)
            {
                return destinationPath;
            }

            string sourcePath = $"{SourceRoot}/{spec.SourceSetFolder}/{sourceStem}.{extension}";
            if (AssetDatabase.LoadMainAssetAtPath(sourcePath) == null)
            {
                throw new FileNotFoundException(
                    $"Source asset for '{spec.DisplayName}' was not found at '{sourcePath}'.",
                    sourcePath);
            }

            string error = AssetDatabase.MoveAsset(sourcePath, destinationPath);
            if (!string.IsNullOrWhiteSpace(error))
            {
                throw new InvalidOperationException(
                    $"Could not organize '{sourcePath}' as '{destinationPath}': {error}");
            }

            return destinationPath;
        }

        private static Material GetOrCreateMaterial(string path, Texture2D texture)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (shader == null)
                {
                    throw new InvalidOperationException("No supported lit shader is available for NPC armor materials.");
                }

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.name = Path.GetFileNameWithoutExtension(path);
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void CleanupEmptyArmorSourceFolders()
        {
            foreach (string folder in new[] { "Butcher", "Merchent", "Seer", "Traveler", "Warchief" })
            {
                string path = $"{SourceRoot}/{folder}";
                if (AssetDatabase.IsValidFolder(path)
                    && AssetDatabase.FindAssets(string.Empty, new[] { path }).Length == 0)
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

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(parent))
            {
                throw new InvalidOperationException($"Cannot resolve parent folder for '{path}'.");
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static string Sanitize(string value)
        {
            return value.Replace("'", string.Empty).Replace(" ", "_").Replace("-", "_");
        }

        private readonly struct ArmorVisualSpec
        {
            public ArmorVisualSpec(
                string setFolder,
                string sourceModelStem,
                string weightFolder,
                string displayName,
                MMOEquipmentSlotType slot,
                MMOCharacterBodyPart bodyPart,
                string sourceTextureStem = null)
            {
                SourceSetFolder = setFolder;
                SourceModelStem = sourceModelStem;
                SourceTextureStem = string.IsNullOrWhiteSpace(sourceTextureStem) ? sourceModelStem : sourceTextureStem;
                WeightFolder = weightFolder;
                DisplayName = displayName;
                Slot = slot;
                BodyPart = bodyPart;
            }

            public string SourceSetFolder { get; }
            public string SourceModelStem { get; }
            public string SourceTextureStem { get; }
            public string WeightFolder { get; }
            public string DisplayName { get; }
            public MMOEquipmentSlotType Slot { get; }
            public MMOCharacterBodyPart BodyPart { get; }
        }

        private readonly struct HairMaskSpec
        {
            public HairMaskSpec(string sourceFileName, string outputFileName)
            {
                SourceFileName = sourceFileName;
                OutputFileName = outputFileName;
            }

            public string SourceFileName { get; }
            public string OutputFileName { get; }
        }
    }
}
