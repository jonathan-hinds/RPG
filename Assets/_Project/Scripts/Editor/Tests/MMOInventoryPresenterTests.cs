#if UNITY_EDITOR
using NUnit.Framework;
using RPGClone.Inventory;
using RPGClone.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RPGClone.EditorTests
{
    public sealed class MMOInventoryPresenterTests
    {
        private const string ItemPath = "Assets/_Project/Configs/Items/Brown_Leather_Satchel.asset";

        [Test]
        public void ReenabledBagWindow_RefreshesItemsGrantedWhileClosed()
        {
            MMOItemDefinition item = AssetDatabase.LoadAssetAtPath<MMOItemDefinition>(ItemPath);
            GameObject inventoryObject = new("Inventory Test");
            GameObject panelObject = new("Inventory Panel Test", typeof(RectTransform));

            try
            {
                MMOInventoryContainer inventory = inventoryObject.AddComponent<MMOInventoryContainer>();
                MMOInventoryPresenter presenter = panelObject.AddComponent<MMOInventoryPresenter>();
                presenter.Configure(inventory);
                panelObject.SetActive(false);

                Assert.That(inventory.TryAddItem(item, 1, out int remaining), Is.True);
                Assert.That(remaining, Is.Zero);

                panelObject.SetActive(true);

                Transform centerTextTransform = panelObject.transform.Find(
                    "Slots/Inventory Slot 1/Slot Visual Layers/Center Text");
                Assert.That(centerTextTransform, Is.Not.Null);
                Assert.That(
                    centerTextTransform.GetComponent<Text>().text,
                    Is.EqualTo(MMOItemIconView.GetFallbackLabel(item)));
            }
            finally
            {
                Object.DestroyImmediate(panelObject);
                Object.DestroyImmediate(inventoryObject);
            }
        }
    }
}
#endif
