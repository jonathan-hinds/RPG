using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using RPGClone.CharacterSelection;
using RPGClone.Characters;
using RPGClone.Inventory;
using RPGClone.Player;
using UnityEditor;
using UnityEngine;

namespace RPGClone.EditorTests
{
    public sealed class MMOCharacterAppearanceTests
    {
        [Test]
        public void AppearanceCatalog_NormalizesMissingAndUnknownStylesToFirstEntry()
        {
            MMOCharacterAppearanceCatalog catalog = ScriptableObject.CreateInstance<MMOCharacterAppearanceCatalog>();
            try
            {
                MMOHairstyleDefinition first = new();
                first.Configure("hair_1", "Hairstyle 1", null);
                MMOHairstyleDefinition second = new();
                second.Configure("hair_2", "Hairstyle 2", null);
                catalog.Configure(new[] { first, second });

                Assert.That(catalog.NormalizeHairstyleId(string.Empty), Is.EqualTo("hair_1"));
                Assert.That(catalog.NormalizeHairstyleId("unknown"), Is.EqualTo("hair_1"));
                Assert.That(catalog.NormalizeHairstyleId("hair_2"), Is.EqualTo("hair_2"));
            }
            finally
            {
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void NewCharacterSaveData_UsesFirstHairstyleAsMigrationDefault()
        {
            MMOCharacterSaveData saveData = new();

            Assert.That(saveData.hairstyleId, Is.EqualTo("hair_1"));
        }

        [Test]
        public void CreationPreviewEquipment_DoesNotModifyStartingEquipment()
        {
            MMOCharacterArchetypeDefinition archetype = ScriptableObject.CreateInstance<MMOCharacterArchetypeDefinition>();
            try
            {
                archetype.ConfigureStartingItems(null, null, null);
                archetype.ConfigureCreationPreview(new RPGClone.Inventory.MMOItemDefinition[] { null });

                Assert.That(archetype.StartingEquipment, Is.Empty);
                Assert.That(archetype.CreationPreviewEquipment, Has.Count.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(archetype);
            }
        }

        [Test]
        public void HairstyleModels_BindToProductionPlayerSkeleton()
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/PlayerCapsule.prefab");
            MMOCharacterAppearanceCatalog catalog = AssetDatabase.LoadAssetAtPath<MMOCharacterAppearanceCatalog>(
                "Assets/Resources/RPGClone/Character_Appearance_Catalog.asset");
            Assert.That(playerPrefab, Is.Not.Null);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.Hairstyles, Has.Count.EqualTo(3));

            GameObject root = new("Character Preview Test Root");
            try
            {
                foreach (MMOHairstyleDefinition hairstyle in catalog.Hairstyles)
                {
                    GameObject actor = MMOCharacterPreviewActor.Create(
                        playerPrefab,
                        root.transform,
                        MMOPlayableRace.Orc,
                        MMOPlayableClass.Warrior,
                        null,
                        catalog,
                        hairstyle.HairstyleId);
                    Assert.That(actor, Is.Not.Null, hairstyle.DisplayName);

                    MMOAppearanceVisualInstanceMarker marker = actor.GetComponentInChildren<MMOAppearanceVisualInstanceMarker>(true);
                    Assert.That(marker, Is.Not.Null, $"{hairstyle.DisplayName} was not instantiated.");
                    bool hasEnabledSkinnedRenderer = false;
                    foreach (SkinnedMeshRenderer renderer in marker.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        hasEnabledSkinnedRenderer |= renderer.enabled;
                    }

                    Assert.That(hasEnabledSkinnedRenderer, Is.True, $"{hairstyle.DisplayName} did not bind to the player skeleton.");
                    Object.DestroyImmediate(actor);
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PreviewActor_PreservesBodyBindingsAndKeepsGeneratedMeshesOnProductionSkeleton()
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/PlayerCapsule.prefab");
            MMOItemDefinition chest = AssetDatabase.LoadAssetAtPath<MMOItemDefinition>(
                "Assets/_Project/Configs/Items/Ashguard_Vest_Mail.asset");
            MMOCharacterAppearanceCatalog catalog = AssetDatabase.LoadAssetAtPath<MMOCharacterAppearanceCatalog>(
                "Assets/Resources/RPGClone/Character_Appearance_Catalog.asset");
            Assert.That(playerPrefab, Is.Not.Null);
            Assert.That(chest, Is.Not.Null);
            Assert.That(catalog, Is.Not.Null);

            GameObject root = new("Character Preview Binding Test Root");
            try
            {
                GameObject actor = MMOCharacterPreviewActor.Create(
                    playerPrefab,
                    root.transform,
                    MMOPlayableRace.Orc,
                    MMOPlayableClass.Warrior,
                    new[] { chest },
                    catalog,
                    "hair_1");
                MMOPlayerEquipmentVisuals equipmentVisuals = actor.GetComponent<MMOPlayerEquipmentVisuals>();
                FieldInfo slotsField = typeof(MMOPlayerEquipmentVisuals).GetField(
                    "bodyPartSlots",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                List<MMOBodyPartRendererSlot> slots = slotsField?.GetValue(equipmentVisuals) as List<MMOBodyPartRendererSlot>;
                MMOBodyPartRendererSlot torso = slots?.Find(slot => slot != null && slot.BodyPart == MMOCharacterBodyPart.Torso);

                Assert.That(torso, Is.Not.Null, "The authored torso renderer binding was lost.");
                foreach (Renderer renderer in torso.Renderers)
                {
                    Assert.That(renderer, Is.Not.Null);
                    Assert.That(renderer.GetComponentInParent<MMOEquipmentVisualInstanceMarker>(), Is.Null);
                    Assert.That(renderer.GetComponentInParent<MMOAppearanceVisualInstanceMarker>(), Is.Null);
                }

                AssertGeneratedSkinnedMeshesUseProductionBones(
                    actor.GetComponentsInChildren<MMOEquipmentVisualInstanceMarker>(true),
                    "chest armor");
                AssertGeneratedSkinnedMeshesUseProductionBones(
                    actor.GetComponentsInChildren<MMOAppearanceVisualInstanceMarker>(true),
                    "hairstyle");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PreviewActor_UsesMobilityPrefabForShieldsOnly()
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/PlayerCapsule.prefab");
            MMOItemDefinition sword = AssetDatabase.LoadAssetAtPath<MMOItemDefinition>(
                "Assets/_Project/Configs/Items/Recruits_Shortsword.asset");
            MMOItemDefinition shield = AssetDatabase.LoadAssetAtPath<MMOItemDefinition>(
                "Assets/_Project/Configs/Items/Recruits_Shield.asset");
            Assert.That(playerPrefab, Is.Not.Null);
            Assert.That(sword, Is.Not.Null);
            Assert.That(shield, Is.Not.Null);
            Assert.That(shield.EquipmentVisual.CombatMovementModelPrefab, Is.Not.Null);

            GameObject root = new("Character Preview Shield Test Root");
            try
            {
                GameObject actor = MMOCharacterPreviewActor.Create(
                    playerPrefab,
                    root.transform,
                    MMOPlayableRace.Orc,
                    MMOPlayableClass.Warrior,
                    new[] { sword, shield },
                    null,
                    string.Empty);
                bool foundReadySword = false;
                bool foundMobilityShield = false;
                foreach (MMOEquipmentVisualInstanceMarker marker in actor.GetComponentsInChildren<MMOEquipmentVisualInstanceMarker>(true))
                {
                    foundReadySword |= marker.name == sword.EquipmentVisual.ModelPrefab.name;
                    foundMobilityShield |= marker.name == shield.EquipmentVisual.CombatMovementModelPrefab.name;
                }

                Assert.That(foundReadySword, Is.True, "The weapon preview should retain its ready-position prefab.");
                Assert.That(foundMobilityShield, Is.True, "The shield preview should use its mobility-positioned prefab.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void AssertGeneratedSkinnedMeshesUseProductionBones<TMarker>(
            IReadOnlyList<TMarker> markers,
            string visualName)
            where TMarker : Component
        {
            bool foundEnabledRenderer = false;
            foreach (TMarker marker in markers)
            {
                foreach (SkinnedMeshRenderer renderer in marker.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (!renderer.enabled)
                    {
                        continue;
                    }

                    foundEnabledRenderer = true;
                    foreach (Transform bone in renderer.bones)
                    {
                        Assert.That(bone, Is.Not.Null, $"{visualName} has an unbound bone.");
                        Assert.That(bone.GetComponentInParent<MMOEquipmentVisualInstanceMarker>(), Is.Null,
                            $"{visualName} bound to a generated equipment skeleton.");
                        Assert.That(bone.GetComponentInParent<MMOAppearanceVisualInstanceMarker>(), Is.Null,
                            $"{visualName} bound to a generated appearance skeleton.");
                    }
                }
            }

            Assert.That(foundEnabledRenderer, Is.True, $"No enabled skinned renderer was found for {visualName}.");
        }

        [TestCase("Assets/_Project/Configs/Archetypes/Orc_Warrior.asset", MMOArmorWeight.Mail)]
        [TestCase("Assets/_Project/Configs/Archetypes/Orc_Mage.asset", MMOArmorWeight.Cloth)]
        [TestCase("Assets/_Project/Configs/Archetypes/Orc_Shaman.asset", MMOArmorWeight.Leather)]
        public void CreationPreview_UsesClassArmorAndIncludesWeapon(string archetypePath, MMOArmorWeight expectedArmorWeight)
        {
            MMOCharacterArchetypeDefinition archetype = AssetDatabase.LoadAssetAtPath<MMOCharacterArchetypeDefinition>(archetypePath);
            Assert.That(archetype, Is.Not.Null);
            Assert.That(archetype.CreationPreviewEquipment, Is.Not.Empty);

            bool hasWeapon = false;
            foreach (MMOItemDefinition item in archetype.CreationPreviewEquipment)
            {
                Assert.That(item, Is.Not.Null);
                hasWeapon |= item.IsWeapon;
                if (item.EquipmentSlot is MMOEquipmentSlotType.Chest
                    or MMOEquipmentSlotType.Hands
                    or MMOEquipmentSlotType.Legs
                    or MMOEquipmentSlotType.Feet)
                {
                    Assert.That(item.ArmorWeight, Is.EqualTo(expectedArmorWeight), item.DisplayName);
                }
            }

            Assert.That(hasWeapon, Is.True, $"{archetype.DisplayName} preview has no weapon.");
        }
    }
}
