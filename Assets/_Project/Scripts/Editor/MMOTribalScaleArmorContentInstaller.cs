using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RPGClone.Characters;
using RPGClone.Inventory;
using RPGClone.Loot;
using UnityEditor;
using UnityEngine;

namespace RPGClone.EditorTools
{
    public static class MMOTribalScaleArmorContentInstaller
    {
        private const string SourceRoot = "Assets/_Project/New Equipment";
        private const string SourceIconRoot = SourceRoot + "/Icons";
        private const string ArmorRoot = "Assets/_Project/Equipment/Armor";
        private const string ItemCatalogPath = "Assets/_Project/Configs/Items/Starter_Item_Catalog.asset";

        private static readonly ArmorPieceSpec[] ArmorPieces =
        {
            new(
                "Tribal",
                "Chest",
                "Chest",
                "tribal_seers_vestments",
                "Cloth",
                "Tribal Seer's Vestments",
                "Tribal Seer's Vestments",
                "Layered ritual cloth that expands a young spellcaster's reserves.",
                MMOArmorWeight.Cloth,
                MMOEquipmentSlotType.Chest,
                MMOCharacterBodyPart.Torso,
                4,
                6,
                32,
                1,
                0,
                0,
                2,
                1),
            new(
                "Tribal",
                "Pants",
                "Pants",
                "tribal_seers_legwraps",
                "Cloth",
                "Tribal Seer's Legwraps",
                "Tribal Seer's Legwraps",
                "Painted legwraps that steady the mind between difficult casts.",
                MMOArmorWeight.Cloth,
                MMOEquipmentSlotType.Legs,
                MMOCharacterBodyPart.Legs,
                5,
                5,
                38,
                0,
                0,
                0,
                2,
                1),
            new(
                "Tribal",
                "Gloves",
                "Gloves",
                "tribal_mystics_grips",
                "Leather",
                "Tribal Mystic's Grips",
                "Tribal Mystic's Grips",
                "Supple ritual grips that protect the hands without dulling concentration.",
                MMOArmorWeight.Leather,
                MMOEquipmentSlotType.Hands,
                MMOCharacterBodyPart.Hands,
                6,
                5,
                45,
                1,
                0,
                0,
                2,
                1),
            new(
                "Tribal",
                "Boots",
                "Boots",
                "tribal_mystics_treads",
                "Leather",
                "Tribal Mystic's Treads",
                "Tribal Mystic's Treads",
                "Bone-fastened treads made for long marches between places of power.",
                MMOArmorWeight.Leather,
                MMOEquipmentSlotType.Feet,
                MMOCharacterBodyPart.Feet,
                7,
                6,
                52,
                1,
                0,
                0,
                2,
                2),
            new(
                "Scale",
                "Gloves",
                "gloves",
                "scalehunter_grips",
                "Leather",
                "Scalehunter Grips",
                "Scalehunter Grips",
                "Leather grips plated to reward a quick and forceful hand.",
                MMOArmorWeight.Leather,
                MMOEquipmentSlotType.Hands,
                MMOCharacterBodyPart.Hands,
                4,
                4,
                32,
                0,
                1,
                1,
                0,
                0),
            new(
                "Scale",
                "Boots",
                "boots",
                "scalehunter_treads",
                "Leather",
                "Scalehunter Treads",
                "Scalehunter Treads",
                "Scale-toed boots built for sure footing in a close fight.",
                MMOArmorWeight.Leather,
                MMOEquipmentSlotType.Feet,
                MMOCharacterBodyPart.Feet,
                5,
                5,
                38,
                1,
                1,
                1,
                0,
                0),
            new(
                "Scale",
                "Pants",
                "pants",
                "scaleguard_legguards",
                "Mail",
                "Scaleguard Legguards",
                "Scaleguard Legguards",
                "Overlapping scales turn glancing blows while leaving room to advance.",
                MMOArmorWeight.Mail,
                MMOEquipmentSlotType.Legs,
                MMOCharacterBodyPart.Legs,
                6,
                8,
                48,
                1,
                1,
                1,
                0,
                0),
            new(
                "Scale",
                "Chest",
                "chest",
                "scaleguard_hauberk",
                "Mail",
                "Scaleguard Hauberk",
                "Scaleguard Hauberk",
                "A compact scale hauberk that lends confidence to an advancing fighter.",
                MMOArmorWeight.Mail,
                MMOEquipmentSlotType.Chest,
                MMOCharacterBodyPart.Torso,
                7,
                10,
                58,
                2,
                2,
                0,
                0,
                0)
        };

        private static readonly LootAssignment[] LootAssignments =
        {
            new("Assets/_Project/Configs/Loot/Wolf_Trash_Loot.asset", "tribal_mystics_treads", 0.03f),
            new("Assets/_Project/Configs/Loot/Bristleback_Trash_Loot.asset", "scalehunter_grips", 0.04f),
            new("Assets/_Project/Configs/Loot/Trog_Trash_Loot.asset", "tribal_seers_vestments", 0.05f),
            new("Assets/_Project/Configs/Loot/Trog_Trash_Loot.asset", "tribal_seers_legwraps", 0.02f),
            new("Assets/_Project/Configs/Loot/Ogre_Trash_Loot.asset", "scaleguard_legguards", 0.03f),
            new("Assets/_Project/Configs/Loot/Ash_Canyon_Quest_Loot.asset", "scalehunter_treads", 0.04f),
            new("Assets/_Project/Configs/Loot/AshGeneral_Trash_Loot.asset", "scaleguard_hauberk", 0.01f),
            new("Assets/_Project/Configs/Loot/AshGeneral_Trash_Loot.asset", "tribal_mystics_grips", 0.02f)
        };

        [MenuItem("Tools/RPG Clone/Items/Install Tribal and Scale Creature Drops")]
        public static void Install()
        {
            EnsureFolder(ArmorRoot);
            Dictionary<string, MMOItemDefinition> items = ArmorPieces
                .Select(CreateOrUpdateArmorPiece)
                .ToDictionary(item => item.ItemId, StringComparer.Ordinal);

            UpdateItemCatalog(items.Values);
            UpdateLootTables(items);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"Installed {items.Count} Tribal/Scale armor items with equipment visuals and " +
                $"{LootAssignments.Length} low-chance creature drop assignments.");
        }

        private static MMOItemDefinition CreateOrUpdateArmorPiece(ArmorPieceSpec spec)
        {
            string itemFolder = $"{ArmorRoot}/{spec.WeightFolder}/{spec.FolderName}";
            EnsureFolder(itemFolder);

            string stem = Sanitize(spec.DisplayName);
            string modelPath = MoveSourceAsset(
                $"{SourceRoot}/{spec.SourceSet}/{spec.SourceModel}.fbx",
                $"{itemFolder}/{stem}.fbx");
            string texturePath = MoveSourceAsset(
                $"{SourceRoot}/{spec.SourceSet}/{spec.SourceTexture}.png",
                $"{itemFolder}/T_{stem}_BaseColor.png");
            string iconPath = MoveSourceAsset(
                $"{SourceIconRoot}/{spec.IconName}.png",
                $"{itemFolder}/I_{stem}.png");

            AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
            ConfigureIconImporter(iconPath);

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            if (model == null || texture == null || icon == null)
            {
                throw new InvalidOperationException(
                    $"Could not import the model, texture, or icon for '{spec.DisplayName}'.");
            }

            if (model.GetComponentInChildren<SkinnedMeshRenderer>(true) == null)
            {
                throw new InvalidOperationException(
                    $"Armor model '{modelPath}' does not contain a SkinnedMeshRenderer.");
            }

            Material material = GetOrCreateMaterial($"{itemFolder}/M_{stem}.mat", texture);
            MMOEquipmentVisualDefinition visual = GetOrCreateVisual(
                $"{itemFolder}/EV_{stem}.asset",
                spec,
                model,
                material,
                texture);

            string itemPath = $"{itemFolder}/{stem}.asset";
            MMOItemDefinition item = AssetDatabase.LoadAssetAtPath<MMOItemDefinition>(itemPath);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<MMOItemDefinition>();
                AssetDatabase.CreateAsset(item, itemPath);
            }

            item.name = stem;
            item.ConfigureEquipment(
                spec.ItemId,
                spec.DisplayName,
                spec.Description,
                MMOItemQuality.Uncommon,
                spec.Slot,
                spec.ArmorWeight,
                CreateStats(spec),
                spec.SellValueCopper,
                icon);
            item.SetRequiredLevel(spec.RequiredLevel);
            item.SetEquipmentVisual(visual);
            EditorUtility.SetDirty(item);
            return item;
        }

        private static void UpdateItemCatalog(IEnumerable<MMOItemDefinition> newItems)
        {
            MMOItemCatalog catalog = AssetDatabase.LoadAssetAtPath<MMOItemCatalog>(ItemCatalogPath);
            if (catalog == null)
            {
                throw new InvalidOperationException($"Required item catalog '{ItemCatalogPath}' was not found.");
            }

            List<MMOItemDefinition> installedItems = newItems.ToList();
            HashSet<string> installedIds = new(
                installedItems.Select(item => item.ItemId),
                StringComparer.Ordinal);
            catalog.Configure(
                catalog.Items
                    .Where(item => item != null && !installedIds.Contains(item.ItemId))
                    .Concat(installedItems));
            EditorUtility.SetDirty(catalog);
        }

        private static void UpdateLootTables(IReadOnlyDictionary<string, MMOItemDefinition> items)
        {
            HashSet<string> installedIds = new(items.Keys, StringComparer.Ordinal);
            foreach (IGrouping<string, LootAssignment> group in LootAssignments.GroupBy(
                         assignment => assignment.LootTablePath,
                         StringComparer.Ordinal))
            {
                MMOLootTable lootTable = AssetDatabase.LoadAssetAtPath<MMOLootTable>(group.Key);
                if (lootTable == null)
                {
                    throw new InvalidOperationException($"Required loot table '{group.Key}' was not found.");
                }

                List<MMOLootTableEntry> entries = lootTable.Entries
                    .Where(entry => entry?.Item != null && !installedIds.Contains(entry.Item.ItemId))
                    .ToList();
                entries.AddRange(group.Select(assignment =>
                    new MMOLootTableEntry(items[assignment.ItemId], assignment.DropChance, 1, 1)));
                lootTable.Configure(entries);
                EditorUtility.SetDirty(lootTable);
            }
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
            MMOEquipmentVisualDefinition visual =
                AssetDatabase.LoadAssetAtPath<MMOEquipmentVisualDefinition>(path);
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

        private static void ConfigureIconImporter(string iconPath)
        {
            AssetDatabase.ImportAsset(iconPath, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(iconPath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"Could not configure icon importer for '{iconPath}'.");
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 256;
            importer.SaveAndReimport();
        }

        private static string MoveSourceAsset(string sourcePath, string destinationPath)
        {
            if (AssetDatabase.LoadMainAssetAtPath(destinationPath) != null)
            {
                return destinationPath;
            }

            if (AssetDatabase.LoadMainAssetAtPath(sourcePath) == null)
            {
                throw new FileNotFoundException($"Missing source armor asset '{sourcePath}'.");
            }

            string error = AssetDatabase.MoveAsset(sourcePath, destinationPath);
            if (!string.IsNullOrWhiteSpace(error))
            {
                throw new InvalidOperationException(
                    $"Could not move '{sourcePath}' to '{destinationPath}': {error}");
            }

            return destinationPath;
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
                string sourceModel,
                string sourceTexture,
                string iconName,
                string weightFolder,
                string folderName,
                string displayName,
                string description,
                MMOArmorWeight armorWeight,
                MMOEquipmentSlotType slot,
                MMOCharacterBodyPart bodyPart,
                int requiredLevel,
                int armor,
                int sellValueCopper,
                int stamina,
                int strength,
                int agility,
                int intellect,
                int spirit)
            {
                SourceSet = sourceSet;
                SourceModel = sourceModel;
                SourceTexture = sourceTexture;
                IconName = iconName;
                WeightFolder = weightFolder;
                FolderName = folderName;
                DisplayName = displayName;
                Description = description;
                ArmorWeight = armorWeight;
                Slot = slot;
                BodyPart = bodyPart;
                RequiredLevel = requiredLevel;
                Armor = armor;
                SellValueCopper = sellValueCopper;
                Stamina = stamina;
                Strength = strength;
                Agility = agility;
                Intellect = intellect;
                Spirit = spirit;
                ItemId = iconName;
            }

            public string SourceSet { get; }
            public string SourceModel { get; }
            public string SourceTexture { get; }
            public string IconName { get; }
            public string WeightFolder { get; }
            public string FolderName { get; }
            public string DisplayName { get; }
            public string Description { get; }
            public MMOArmorWeight ArmorWeight { get; }
            public MMOEquipmentSlotType Slot { get; }
            public MMOCharacterBodyPart BodyPart { get; }
            public int RequiredLevel { get; }
            public int Armor { get; }
            public int SellValueCopper { get; }
            public int Stamina { get; }
            public int Strength { get; }
            public int Agility { get; }
            public int Intellect { get; }
            public int Spirit { get; }
            public string ItemId { get; }
        }

        private sealed class LootAssignment
        {
            public LootAssignment(string lootTablePath, string itemId, float dropChance)
            {
                LootTablePath = lootTablePath;
                ItemId = itemId;
                DropChance = dropChance;
            }

            public string LootTablePath { get; }
            public string ItemId { get; }
            public float DropChance { get; }
        }
    }
}
