using NUnit.Framework;
using RPGClone.Characters;
using RPGClone.Inventory;
using UnityEditor;

namespace RPGClone.EditorTests
{
    public sealed class MMONpcAppearanceContentTests
    {
        private static readonly ArmorVisualExpectation[] ArmorVisuals =
        {
            new("Leather", "Bloodstained Butcher Apron"),
            new("Leather", "Gorecleaver Grips"),
            new("Leather", "Slaughterhouse Leggings"),
            new("Leather", "Bloodslick Workboots"),
            new("Cloth", "Gilded Caravan Vestments"),
            new("Cloth", "Coinweave Gloves"),
            new("Cloth", "Gilded Caravan Breeches"),
            new("Cloth", "Cointrail Boots"),
            new("Cloth", "Astral Seer Vestments"),
            new("Cloth", "Starwoven Handwraps"),
            new("Cloth", "Astral Seer Trousers"),
            new("Cloth", "Celestial Sandals"),
            new("Leather", "Verdant Wayfarer Jerkin"),
            new("Leather", "Wayfarer Grips"),
            new("Leather", "Verdant Trail Leggings"),
            new("Leather", "Longroad Treads"),
            new("Mail", "Bloodbanner Hauberk"),
            new("Mail", "Bloodbanner Gauntlets"),
            new("Mail", "Bloodbanner Legguards"),
            new("Mail", "Bloodbanner Warboots")
        };

        [Test]
        public void NewNpcArmorVisuals_AreVisualOnlyAndCoverEverySetSlot()
        {
            foreach (ArmorVisualExpectation expectation in ArmorVisuals)
            {
                string stem = expectation.DisplayName.Replace("'", string.Empty).Replace(" ", "_").Replace("-", "_");
                string path =
                    $"Assets/_Project/Equipment/Armor/{expectation.Weight}/{expectation.DisplayName}/EV_{stem}.asset";
                MMOEquipmentVisualDefinition visual =
                    AssetDatabase.LoadAssetAtPath<MMOEquipmentVisualDefinition>(path);
                Assert.That(visual, Is.Not.Null, path);
                Assert.That(visual.ModelPrefab, Is.Not.Null, path);
                Assert.That(visual.DiffuseTexture, Is.Not.Null, path);
                Assert.That(visual.MaterialOverride, Is.Not.Null, path);
                Assert.That(visual.HideBaseBodyPart, Is.True, path);
            }

            string[] itemGuids = AssetDatabase.FindAssets(
                "t:MMOItemDefinition",
                new[] { "Assets/_Project/Equipment/Armor" });
            foreach (string itemGuid in itemGuids)
            {
                string itemPath = AssetDatabase.GUIDToAssetPath(itemGuid);
                Assert.That(
                    ArmorVisuals,
                    Has.None.Matches<ArmorVisualExpectation>(expectation => itemPath.Contains(expectation.DisplayName)),
                    $"NPC-only visual '{itemPath}' must not have an obtainable item definition.");
            }
        }

        [Test]
        public void NewHairAndFaces_AreAvailableThroughSharedAppearanceCatalog()
        {
            MMOCharacterAppearanceCatalog catalog = AssetDatabase.LoadAssetAtPath<MMOCharacterAppearanceCatalog>(
                "Assets/Resources/RPGClone/Character_Appearance_Catalog.asset");
            Assert.That(catalog, Is.Not.Null);

            for (int index = 4; index <= 6; index++)
            {
                MMOHairstyleDefinition hairstyle = catalog.FindHairstyle($"hair_{index}");
                Assert.That(hairstyle, Is.Not.Null, $"hair_{index}");
                Assert.That(hairstyle.ModelPrefab, Is.Not.Null, $"hair_{index}");
                Assert.That(hairstyle.ColorMask, Is.Not.Null, $"hair_{index}");

                MMOFaceDefinition face = catalog.FindFace($"face_{index}");
                Assert.That(face, Is.Not.Null, $"face_{index}");
                Assert.That(face.AlbedoTexture, Is.Not.Null, $"face_{index}");
            }
        }

        private readonly struct ArmorVisualExpectation
        {
            public ArmorVisualExpectation(string weight, string displayName)
            {
                Weight = weight;
                DisplayName = displayName;
            }

            public string Weight { get; }
            public string DisplayName { get; }
        }
    }
}
