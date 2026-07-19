using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RPGClone.Characters;
using RPGClone.Inventory;
using RPGClone.Vendors;
using RPGClone.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RPGClone.EditorTools
{
    public static class MMOLowLevelArmorContentInstaller
    {
        private const string SourceRoot = "Assets/_Project/New Equipment";
        private const string ArmorRoot = "Assets/_Project/Equipment/Armor";
        private const string ItemCatalogPath = "Assets/_Project/Configs/Items/Starter_Item_Catalog.asset";
        private const string FriendlyNpcProfilePath = "Assets/_Project/Configs/Characters/Friendly_NPC.asset";
        private const string StarterScenePath = "Assets/Scenes/OrcishStarterValley.unity";

        private static readonly ArmorPieceSpec[] ArmorPieces =
        {
            new("Cultist", "Chest", "Cloth", "Cinderweave Robe", "cinderweave_robe", "A soot-dark robe stitched for careful spellwork.", MMOArmorWeight.Cloth, MMOEquipmentSlotType.Chest, MMOCharacterBodyPart.Torso, 5, 140, 35, 0, 0, 0, 0, 0),
            new("Cultist", "Gloves", "Cloth", "Cinderweave Handwraps", "cinderweave_handwraps", "Fine wraps that steady the hands and focus the mind.", MMOArmorWeight.Cloth, MMOEquipmentSlotType.Hands, MMOCharacterBodyPart.Hands, 3, 75, 18, 0, 0, 0, 1, 1),
            new("Cultist", "Pants", "Cloth", "Cinderweave Trousers", "cinderweave_trousers", "Layered cloth trousers made for the ash-choked valley.", MMOArmorWeight.Cloth, MMOEquipmentSlotType.Legs, MMOCharacterBodyPart.Legs, 4, 110, 27, 0, 0, 0, 0, 0),
            new("Cultist", "Boots", "Cloth", "Cinderweave Sandals", "cinderweave_sandals", "Light sandals reinforced against hot stone and cinders.", MMOArmorWeight.Cloth, MMOEquipmentSlotType.Feet, MMOCharacterBodyPart.Feet, 3, 75, 18, 0, 0, 0, 0, 0),

            new("Wolfs Fur", "Chest", "Leather", "Wolfpelt Vest", "wolfpelt_vest", "A warm hide vest cut for hunters of the valley.", MMOArmorWeight.Leather, MMOEquipmentSlotType.Chest, MMOCharacterBodyPart.Torso, 6, 155, 38, 0, 0, 0, 0, 0),
            new("Wolfs Fur", "Gloves", "Leather", "Wolfpelt Grips", "wolfpelt_grips", "Supple hide grips that keep their hold in rain and blood.", MMOArmorWeight.Leather, MMOEquipmentSlotType.Hands, MMOCharacterBodyPart.Hands, 4, 85, 21, 0, 0, 0, 0, 0),
            new("Wolfs Fur", "Pants", "Leather", "Wolfpelt Leggings", "wolfpelt_leggings", "Tough leggings sewn from layered wolf hide.", MMOArmorWeight.Leather, MMOEquipmentSlotType.Legs, MMOCharacterBodyPart.Legs, 5, 125, 31, 0, 0, 0, 0, 0),
            new("Wolfs Fur", "Boots", "Leather", "Wolfpelt Treads", "wolfpelt_treads", "Quiet leather boots for stalking dangerous ground.", MMOArmorWeight.Leather, MMOEquipmentSlotType.Feet, MMOCharacterBodyPart.Feet, 4, 85, 21, 1, 0, 1, 0, 0),

            new("Bone Breaker", "Chest", "Mail", "Bonebreaker Hauberk", "bonebreaker_hauberk", "A compact mail hauberk reinforced for close fighting.", MMOArmorWeight.Mail, MMOEquipmentSlotType.Chest, MMOCharacterBodyPart.Torso, 7, 170, 42, 1, 1, 0, 0, 0),
            new("Bone Breaker", "Gloves", "Mail", "Bonebreaker Gauntlets", "bonebreaker_gauntlets", "Linked gauntlets with battered iron knuckles.", MMOArmorWeight.Mail, MMOEquipmentSlotType.Hands, MMOCharacterBodyPart.Hands, 5, 95, 23, 0, 0, 0, 0, 0),
            new("Bone Breaker", "Pants", "Mail", "Bonebreaker Legguards", "bonebreaker_legguards", "Mail legguards built to turn glancing blows.", MMOArmorWeight.Mail, MMOEquipmentSlotType.Legs, MMOCharacterBodyPart.Legs, 6, 140, 35, 0, 0, 0, 0, 0),
            new("Bone Breaker", "Boots", "Mail", "Bonebreaker Warboots", "bonebreaker_warboots", "Heavy warboots that still leave room to run.", MMOArmorWeight.Mail, MMOEquipmentSlotType.Feet, MMOCharacterBodyPart.Feet, 5, 95, 23, 0, 0, 0, 0, 0)
        };

        [MenuItem("Tools/RPG Clone/Items/Install Level 5 Armor Vendors")]
        public static void InstallLowLevelArmorContent()
        {
            if (SceneManager.GetActiveScene().path != StarterScenePath)
            {
                EditorSceneManager.OpenScene(StarterScenePath, OpenSceneMode.Single);
            }

            InstallIntoOpenStarterScene(true);
        }

        public static void InstallIntoOpenStarterScene(bool saveSceneAndAssets)
        {
            if (SceneManager.GetActiveScene().path != StarterScenePath)
            {
                throw new InvalidOperationException($"Open '{StarterScenePath}' before installing level 5 armor content.");
            }

            EnsureFolder(ArmorRoot);
            List<MMOItemDefinition> items = ArmorPieces.Select(CreateOrUpdateArmorPiece).ToList();
            UpdateItemCatalog(items);
            InstallVendors(items);
            CleanupEmptySourceFolder();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (saveSceneAndAssets)
            {
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log("Installed 12 level 5 armor pieces and the cloth, leather, and mail armor vendors.");
        }

        private static MMOItemDefinition CreateOrUpdateArmorPiece(ArmorPieceSpec spec)
        {
            string pieceFolder = $"{ArmorRoot}/{spec.WeightFolder}/{spec.DisplayName}";
            EnsureFolder(pieceFolder);

            string assetStem = Sanitize(spec.DisplayName);
            string modelPath = MoveSourceAsset(spec, "fbx", $"{pieceFolder}/{assetStem}.fbx");
            string texturePath = MoveSourceAsset(spec, "png", $"{pieceFolder}/T_{assetStem}_BaseColor.png");
            AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (model == null || texture == null)
            {
                throw new InvalidOperationException($"Could not import the model or texture for '{spec.DisplayName}'.");
            }

            if (model.GetComponentInChildren<SkinnedMeshRenderer>(true) == null)
            {
                throw new InvalidOperationException($"Armor model '{modelPath}' does not contain a SkinnedMeshRenderer.");
            }

            Material material = GetOrCreateMaterial($"{pieceFolder}/M_{assetStem}.mat", texture);
            MMOEquipmentVisualDefinition visual = GetOrCreateVisual(
                $"{pieceFolder}/EV_{assetStem}.asset",
                spec,
                model,
                material,
                texture);

            string itemPath = $"{pieceFolder}/{assetStem}.asset";
            MMOItemDefinition item = AssetDatabase.LoadAssetAtPath<MMOItemDefinition>(itemPath);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<MMOItemDefinition>();
                AssetDatabase.CreateAsset(item, itemPath);
            }

            item.name = assetStem;
            item.ConfigureEquipment(
                spec.ItemId,
                spec.DisplayName,
                spec.Description,
                MMOItemQuality.Common,
                spec.Slot,
                spec.ArmorWeight,
                CreateStats(spec),
                spec.SellValueCopper);
            item.SetEquipmentVisual(visual);
            EditorUtility.SetDirty(item);
            return item;
        }

        private static Material GetOrCreateMaterial(string path, Texture2D texture)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (shader == null)
                {
                    throw new InvalidOperationException("No supported lit shader is available for the armor materials.");
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

        private static MMOEquipmentVisualDefinition GetOrCreateVisual(
            string path,
            ArmorPieceSpec spec,
            GameObject model,
            Material material,
            Texture2D texture)
        {
            MMOEquipmentVisualDefinition visual = AssetDatabase.LoadAssetAtPath<MMOEquipmentVisualDefinition>(path);
            if (visual == null)
            {
                visual = ScriptableObject.CreateInstance<MMOEquipmentVisualDefinition>();
                AssetDatabase.CreateAsset(visual, path);
            }

            visual.name = Path.GetFileNameWithoutExtension(path);
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
            return visual;
        }

        private static MMOCharacterStats CreateStats(ArmorPieceSpec spec)
        {
            MMOCharacterStats stats = new();
            stats.Configure(
                spec.Stamina,
                spec.Strength,
                spec.Agility,
                spec.Intellect,
                spec.Spirit,
                spec.Armor,
                0,
                0,
                0f,
                0f,
                2f,
                3f);
            return stats;
        }

        private static void UpdateItemCatalog(IReadOnlyCollection<MMOItemDefinition> newItems)
        {
            MMOItemCatalog catalog = AssetDatabase.LoadAssetAtPath<MMOItemCatalog>(ItemCatalogPath);
            if (catalog == null)
            {
                throw new InvalidOperationException($"Required item catalog '{ItemCatalogPath}' was not found.");
            }

            HashSet<string> newItemIds = new(newItems.Select(item => item.ItemId), StringComparer.Ordinal);
            List<MMOItemDefinition> merged = catalog.Items
                .Where(item => item != null && !newItemIds.Contains(item.ItemId))
                .Concat(newItems)
                .ToList();
            catalog.Configure(merged);
            EditorUtility.SetDirty(catalog);
        }

        private static void InstallVendors(IReadOnlyCollection<MMOItemDefinition> items)
        {
            Dictionary<string, MMOItemDefinition> byId = items.ToDictionary(item => item.ItemId, StringComparer.Ordinal);
            MMOCharacterProfile profile = AssetDatabase.LoadAssetAtPath<MMOCharacterProfile>(FriendlyNpcProfilePath);
            if (profile == null)
            {
                throw new InvalidOperationException($"Required NPC profile '{FriendlyNpcProfilePath}' was not found.");
            }

            EnsureVendor(
                "Vendor - Tailor",
                "tailor_velara",
                "Tailor Velara",
                "Cloth Armor Tailor",
                new Vector3(-18f, 2f, -128f),
                profile,
                Stock(byId, MMOArmorWeight.Cloth));
            EnsureVendor(
                "Vendor - Huntsman",
                "huntsman_roka",
                "Huntsman Roka",
                "Leather Armor Huntsman",
                new Vector3(-13f, 2f, -134f),
                profile,
                Stock(byId, MMOArmorWeight.Leather));
            EnsureVendor(
                "Vendor - Blacksmith",
                "blacksmith_durgan",
                "Blacksmith Durgan",
                "Mail Armor Blacksmith",
                new Vector3(-8f, 2f, -128f),
                profile,
                Stock(byId, MMOArmorWeight.Mail));
        }

        private static MMOVendorStockEntry[] Stock(
            IReadOnlyDictionary<string, MMOItemDefinition> byId,
            MMOArmorWeight armorWeight)
        {
            return ArmorPieces
                .Where(piece => piece.ArmorWeight == armorWeight)
                .Select(piece => new MMOVendorStockEntry(byId[piece.ItemId], 1, piece.BuyPriceCopper))
                .ToArray();
        }

        private static void EnsureVendor(
            string objectName,
            string vendorId,
            string displayName,
            string title,
            Vector3 fallbackPosition,
            MMOCharacterProfile profile,
            MMOVendorStockEntry[] stock)
        {
            GameObject vendor = GameObject.Find(objectName);
            if (vendor == null)
            {
                vendor = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                vendor.name = objectName;
                vendor.transform.position = fallbackPosition;
            }

            vendor.transform.SetParent(null, true);
            vendor.isStatic = false;
            MMOVendorNpc vendorNpc = vendor.GetComponent<MMOVendorNpc>() ?? vendor.AddComponent<MMOVendorNpc>();
            vendorNpc.Configure(vendorId, displayName, title, stock, true);
            MMOStandardNpcIdentity identity = vendor.GetComponent<MMOStandardNpcIdentity>() ?? vendor.AddComponent<MMOStandardNpcIdentity>();
            identity.Configure(profile, displayName, title, MMONpcIdentityRole.Vendor, true);
            MMOGroundingUtility.SnapTransformToGround(vendor.transform, vendor.GetComponent<Collider>());

            EditorUtility.SetDirty(vendor);
            EditorUtility.SetDirty(vendorNpc);
            EditorUtility.SetDirty(identity);
            EditorUtility.SetDirty(identity.Identity);
        }

        private static string MoveSourceAsset(ArmorPieceSpec spec, string extension, string destinationPath)
        {
            if (AssetDatabase.LoadMainAssetAtPath(destinationPath) != null)
            {
                return destinationPath;
            }

            string sourcePath = $"{SourceRoot}/{spec.SourceSet}/{spec.SourcePiece}.{extension}";
            if (AssetDatabase.LoadMainAssetAtPath(sourcePath) == null)
            {
                throw new FileNotFoundException($"Missing source armor asset '{sourcePath}'.");
            }

            string error = AssetDatabase.MoveAsset(sourcePath, destinationPath);
            if (!string.IsNullOrWhiteSpace(error))
            {
                throw new InvalidOperationException($"Could not move '{sourcePath}' to '{destinationPath}': {error}");
            }

            return destinationPath;
        }

        private static void CleanupEmptySourceFolder()
        {
            if (!AssetDatabase.IsValidFolder(SourceRoot))
            {
                return;
            }

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string absoluteSourceRoot = Path.Combine(projectRoot, SourceRoot);
            bool containsSourceFiles = Directory
                .EnumerateFiles(absoluteSourceRoot, "*", SearchOption.AllDirectories)
                .Any(file => !file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase));
            if (!containsSourceFiles)
            {
                AssetDatabase.DeleteAsset(SourceRoot);
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

        private sealed class ArmorPieceSpec
        {
            public ArmorPieceSpec(
                string sourceSet,
                string sourcePiece,
                string weightFolder,
                string displayName,
                string itemId,
                string description,
                MMOArmorWeight armorWeight,
                MMOEquipmentSlotType slot,
                MMOCharacterBodyPart bodyPart,
                int armor,
                int buyPriceCopper,
                int sellValueCopper,
                int stamina,
                int strength,
                int agility,
                int intellect,
                int spirit)
            {
                SourceSet = sourceSet;
                SourcePiece = sourcePiece;
                WeightFolder = weightFolder;
                DisplayName = displayName;
                ItemId = itemId;
                Description = description;
                ArmorWeight = armorWeight;
                Slot = slot;
                BodyPart = bodyPart;
                Armor = armor;
                BuyPriceCopper = buyPriceCopper;
                SellValueCopper = sellValueCopper;
                Stamina = stamina;
                Strength = strength;
                Agility = agility;
                Intellect = intellect;
                Spirit = spirit;
            }

            public string SourceSet { get; }
            public string SourcePiece { get; }
            public string WeightFolder { get; }
            public string DisplayName { get; }
            public string ItemId { get; }
            public string Description { get; }
            public MMOArmorWeight ArmorWeight { get; }
            public MMOEquipmentSlotType Slot { get; }
            public MMOCharacterBodyPart BodyPart { get; }
            public int Armor { get; }
            public int BuyPriceCopper { get; }
            public int SellValueCopper { get; }
            public int Stamina { get; }
            public int Strength { get; }
            public int Agility { get; }
            public int Intellect { get; }
            public int Spirit { get; }
        }
    }
}
