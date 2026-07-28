#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using RPGClone.Inventory;
using RPGClone.UI;
using RPGClone.Vendors;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RPGClone.EditorTests
{
    public sealed class MMOBagContentTests
    {
        private const string BagPath = "Assets/_Project/Configs/Items/Brown_Leather_Satchel.asset";
        private const string CatalogPath = "Assets/_Project/Configs/Items/Starter_Item_Catalog.asset";
        private const string ScenePath = "Assets/Scenes/OrcishStarterValley.unity";
        private const string BottomHudPrefabPath =
            "Assets/Resources/RPGClone/UI/Hud/BottomHUD.prefab";

        [Test]
        public void BrownLeatherSatchel_IsConfiguredAsAnEightSlotContainer()
        {
            MMOItemDefinition bag = AssetDatabase.LoadAssetAtPath<MMOItemDefinition>(BagPath);

            Assert.That(bag, Is.Not.Null);
            Assert.That(bag.ItemId, Is.EqualTo("brown_leather_satchel"));
            Assert.That(bag.DisplayName, Is.EqualTo("Brown Leather Satchel"));
            Assert.That(bag.IsContainer, Is.True);
            Assert.That(bag.ContainerSlotCount, Is.EqualTo(8));
            Assert.That(bag.MaxStackSize, Is.EqualTo(1));
            Assert.That(bag.VendorValueCopper, Is.EqualTo(625));
        }

        [Test]
        public void StarterItemCatalog_ContainsBrownLeatherSatchel()
        {
            MMOItemCatalog catalog = AssetDatabase.LoadAssetAtPath<MMOItemCatalog>(CatalogPath);

            Assert.That(catalog, Is.Not.Null);
            Assert.That(
                catalog.Items.Any(item => item != null && item.ItemId == "brown_leather_satchel"),
                Is.True);
        }

        [Test]
        public void Quartermaster_StocksBrownLeatherSatchelForTwentyFiveSilver()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedForTest = !scene.IsValid() || !scene.isLoaded;
            if (openedForTest)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            }

            try
            {
                MMOVendorNpc quartermaster = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<MMOVendorNpc>(true))
                    .FirstOrDefault(vendor => vendor.VendorId == "quartermaster_grakka");

                Assert.That(quartermaster, Is.Not.Null);
                MMOVendorStockEntry bagEntry = quartermaster.Stock.FirstOrDefault(entry =>
                    entry?.Item != null && entry.Item.ItemId == "brown_leather_satchel");
                Assert.That(bagEntry, Is.Not.Null);
                Assert.That(bagEntry.Quantity, Is.EqualTo(1));
                Assert.That(bagEntry.PriceCopper, Is.EqualTo(2500));
            }
            finally
            {
                if (openedForTest)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        [Test]
        public void BagWindows_UseVisibleClassicSpacing()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BottomHudPrefabPath);
            Assert.That(prefab, Is.Not.Null);

            MMOBagBarPresenter bagBar = prefab.GetComponentInChildren<MMOBagBarPresenter>(true);
            Assert.That(bagBar, Is.Not.Null);
            SerializedObject serializedBagBar = new(bagBar);
            Assert.That(
                serializedBagBar.FindProperty("windowSpacing").floatValue,
                Is.GreaterThanOrEqualTo(10f));
            Assert.That(
                serializedBagBar.FindProperty("columnSpacing").floatValue,
                Is.GreaterThanOrEqualTo(10f));
        }
    }
}
#endif
