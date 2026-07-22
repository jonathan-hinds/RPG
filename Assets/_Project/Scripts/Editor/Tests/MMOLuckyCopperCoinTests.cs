#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using RPGClone.Inventory;
using RPGClone.Multiplayer;
using RPGClone.Vendors;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RPGClone.EditorTests
{
    public sealed class MMOLuckyCopperCoinTests
    {
        private const string CoinPath = "Assets/_Project/Configs/Items/Lucky_Copper_Coin.asset";
        private const string CatalogPath = "Assets/_Project/Configs/Items/Starter_Item_Catalog.asset";
        private const string ScenePath = "Assets/Scenes/OrcishStarterValley.unity";

        [Test]
        public void LuckyCopperCoin_IsConfiguredAsInstantExperienceConsumable()
        {
            MMOItemDefinition coin = AssetDatabase.LoadAssetAtPath<MMOItemDefinition>(CoinPath);

            Assert.That(coin, Is.Not.Null);
            Assert.That(coin.ItemId, Is.EqualTo("lucky_copper_coin"));
            Assert.That(coin.DisplayName, Is.EqualTo("Lucky Copper Coin"));
            Assert.That(coin.IsConsumable, Is.True);
            Assert.That(coin.ConsumableType, Is.EqualTo(MMOConsumableType.Experience));
            Assert.That(coin.ExperienceRewardAmount, Is.EqualTo(1000));
            Assert.That(coin.VendorValueCopper, Is.Zero);
            Assert.That(MMOConsumableRewardAuthority.TryGetExperienceReward(coin, out int reward), Is.True);
            Assert.That(reward, Is.EqualTo(1000));
        }

        [Test]
        public void StarterItemCatalog_ContainsLuckyCopperCoin()
        {
            MMOItemCatalog catalog = AssetDatabase.LoadAssetAtPath<MMOItemCatalog>(CatalogPath);

            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.Items.Any(item => item != null && item.ItemId == "lucky_copper_coin"), Is.True);
        }

        [Test]
        public void ConsumableUseRequest_DoesNotCarryClientAuthoredRewardAmount()
        {
            MMOConsumableUseRequest request = new()
            {
                requestId = "request",
                sessionId = "session",
                characterId = "character",
                itemId = "lucky_copper_coin"
            };

            MMOConsumableUseRequest clone = JsonUtility.FromJson<MMOConsumableUseRequest>(JsonUtility.ToJson(request));

            Assert.That(clone.requestId, Is.EqualTo(request.requestId));
            Assert.That(clone.sessionId, Is.EqualTo(request.sessionId));
            Assert.That(clone.characterId, Is.EqualTo(request.characterId));
            Assert.That(clone.itemId, Is.EqualTo(request.itemId));
            Assert.That(JsonUtility.ToJson(request), Does.Not.Contain("experience"));
        }

        [Test]
        public void Quartermaster_StocksLuckyCopperCoinForZeroCopper()
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
                MMOVendorStockEntry coinEntry = quartermaster.Stock.FirstOrDefault(entry =>
                    entry?.Item != null && entry.Item.ItemId == "lucky_copper_coin");
                Assert.That(coinEntry, Is.Not.Null);
                Assert.That(coinEntry.Quantity, Is.EqualTo(1));
                Assert.That(coinEntry.PriceCopper, Is.Zero);
            }
            finally
            {
                if (openedForTest)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }
    }
}
#endif
