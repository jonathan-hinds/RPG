using System.Collections.Generic;
using NUnit.Framework;
using RPGClone.Inventory;
using RPGClone.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RPGClone.Tests
{
    public sealed class MMOSlotFrameworkTests
    {
        [Test]
        public void SharedViewBuildsIndependentVisualLayers()
        {
            GameObject slot = new("Test Slot", typeof(RectTransform), typeof(Image));
            try
            {
                MMOSlotView view = MMOSlotView.Attach(slot);
                view.Present(new MMOSlotPresentation(
                    primaryText: "2",
                    secondaryText: "1",
                    selected: true,
                    active: true,
                    disabled: true,
                    attention: true,
                    procGlow: true,
                    cooldownNormalized: 0.5f,
                    cooldownText: "3.0"));

                Transform layers = slot.transform.Find("Slot Visual Layers");
                Assert.That(layers, Is.Not.Null);
                Assert.That(layers.Find("Empty Background"), Is.Not.Null);
                Assert.That(layers.Find("Primary Icon"), Is.Not.Null);
                Assert.That(layers.Find("Normal Frame"), Is.Not.Null);
                Assert.That(layers.Find("Selected Frame"), Is.Not.Null);
                Assert.That(layers.Find("Active Frame"), Is.Not.Null);
                Assert.That(layers.Find("Valid Drop Frame"), Is.Not.Null);
                Assert.That(layers.Find("Invalid Drop Frame"), Is.Not.Null);
                Assert.That(layers.Find("Disabled Overlay"), Is.Not.Null);
                Assert.That(layers.Find("Cooldown Sweep"), Is.Not.Null);
                Assert.That(layers.Find("Proc Glow"), Is.Not.Null);
                Assert.That(layers.Find("Active Frame").gameObject.activeSelf, Is.True);
                Assert.That(layers.Find("Selected Frame").gameObject.activeSelf, Is.False);
                Assert.That(layers.Find("Attention Overlay").gameObject.activeSelf, Is.False);

                view.SetDropState(MMOSlotDropState.Invalid);
                Assert.That(layers.Find("Invalid Drop Frame").gameObject.activeSelf, Is.True);
                Assert.That(layers.Find("Active Frame").gameObject.activeSelf, Is.False);
                Assert.That(layers.Find("Selected Frame").gameObject.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(slot);
            }
        }

        [Test]
        public void CancelledDragDoesNotMutateInventorySource()
        {
            GameObject inventoryObject = new("Inventory");
            MMOItemDefinition item = ScriptableObject.CreateInstance<MMOItemDefinition>();
            try
            {
                MMOInventoryContainer inventory = inventoryObject.AddComponent<MMOInventoryContainer>();
                inventory.Resize(2);
                inventory.SetSlot(0, item, 3);

                MMOSlotDragPayload payload = MMOSlotDragPayload.InventoryItem(item, inventory, 0, 3);
                Assert.That(payload.IsValid, Is.True);
                Assert.That(inventory.GetSlot(0).Quantity, Is.EqualTo(3));

                MMOSlotDragState.EndDrag();
                Assert.That(inventory.GetSlot(0).Item, Is.SameAs(item));
                Assert.That(inventory.GetSlot(0).Quantity, Is.EqualTo(3));
            }
            finally
            {
                Object.DestroyImmediate(item);
                Object.DestroyImmediate(inventoryObject);
            }
        }

        [Test]
        public void InventorySwapPreservesItemsAndQuantities()
        {
            GameObject inventoryObject = new("Inventory");
            MMOItemDefinition firstItem = ScriptableObject.CreateInstance<MMOItemDefinition>();
            MMOItemDefinition secondItem = ScriptableObject.CreateInstance<MMOItemDefinition>();
            try
            {
                MMOInventoryContainer inventory = inventoryObject.AddComponent<MMOInventoryContainer>();
                inventory.Resize(2);
                inventory.SetSlot(0, firstItem, 3);
                inventory.SetSlot(1, secondItem, 2);

                Assert.That(inventory.TryMoveSlot(0, 1), Is.True);
                Assert.That(inventory.GetSlot(0).Item, Is.SameAs(secondItem));
                Assert.That(inventory.GetSlot(0).Quantity, Is.EqualTo(2));
                Assert.That(inventory.GetSlot(1).Item, Is.SameAs(firstItem));
                Assert.That(inventory.GetSlot(1).Quantity, Is.EqualTo(3));
            }
            finally
            {
                Object.DestroyImmediate(firstItem);
                Object.DestroyImmediate(secondItem);
                Object.DestroyImmediate(inventoryObject);
            }
        }

        [Test]
        public void EquippingContainerAddsItsSlotsAndRemovesTheBagItem()
        {
            GameObject inventoryObject = new("Inventory");
            MMOItemDefinition bag = ScriptableObject.CreateInstance<MMOItemDefinition>();
            try
            {
                bag.ConfigureContainer(
                    "test_8_slot_bag",
                    "Test Satchel",
                    string.Empty,
                    MMOItemQuality.Common,
                    8,
                    0);
                MMOInventoryContainer inventory = inventoryObject.AddComponent<MMOInventoryContainer>();
                inventory.SetSlot(0, bag, 1);

                Assert.That(inventory.TryEquipBagFromInventory(0), Is.True);
                Assert.That(inventory.GetEquippedBag(0), Is.SameAs(bag));
                Assert.That(inventory.BaseSlotCount, Is.EqualTo(16));
                Assert.That(inventory.SlotCount, Is.EqualTo(24));
                Assert.That(inventory.GetSlot(0).IsEmpty, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(bag);
                Object.DestroyImmediate(inventoryObject);
            }
        }

        [Test]
        public void EquippedBagMustBeEmptyBeforeItCanBeUnequipped()
        {
            GameObject inventoryObject = new("Inventory");
            MMOItemDefinition bag = ScriptableObject.CreateInstance<MMOItemDefinition>();
            MMOItemDefinition item = ScriptableObject.CreateInstance<MMOItemDefinition>();
            try
            {
                bag.ConfigureContainer(
                    "test_8_slot_bag",
                    "Test Satchel",
                    string.Empty,
                    MMOItemQuality.Common,
                    8,
                    0);
                MMOInventoryContainer inventory = inventoryObject.AddComponent<MMOInventoryContainer>();
                inventory.SetSlot(0, bag, 1);
                Assert.That(inventory.TryEquipBagFromInventory(0), Is.True);

                inventory.SetSlot(inventory.GetBagStartIndex(0), item, 1);
                Assert.That(inventory.CanUnequipBagToInventory(0), Is.False);

                inventory.SetSlot(inventory.GetBagStartIndex(0), null, 0);
                Assert.That(inventory.TryUnequipBagToInventory(0), Is.True);
                Assert.That(inventory.SlotCount, Is.EqualTo(16));
                Assert.That(inventory.CountItem(bag), Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(item);
                Object.DestroyImmediate(bag);
                Object.DestroyImmediate(inventoryObject);
            }
        }

        [Test]
        public void SkinResourcesAndSharedPrefabAreImportable()
        {
            Assert.That(MMOSlotSkin.SlotBackground, Is.Not.Null);
            Assert.That(MMOSlotSkin.NormalFrame, Is.Not.Null);
            Assert.That(MMOSlotSkin.ValidDropFrame, Is.Not.Null);
            Assert.That(MMOSlotSkin.InvalidDropFrame, Is.Not.Null);
            Assert.That(MMOSlotSkin.HoverFrame, Is.Not.SameAs(MMOSlotSkin.NormalFrame));
            Assert.That(MMOSlotSkin.SelectedFrame, Is.SameAs(MMOSlotSkin.HoverFrame));
            Assert.That(MMOSlotSkin.PanelFrame, Is.Not.Null);
            Assert.That(MMOSlotSkin.PanelHeader, Is.Not.Null);
            Assert.That(MMOSlotSkin.BagMedallion, Is.Not.Null);
            Assert.That(MMOSlotSkin.BagCurrencyBar, Is.Not.Null);
            Assert.That(MMOSlotSkin.ActionBarCenter, Is.Not.Null);
            Assert.That(MMOSlotSkin.ActionBarLeftCap, Is.Not.Null);
            Assert.That(MMOSlotSkin.CloseNormal, Is.Not.Null);

            GameObject prefab = Resources.Load<GameObject>("RPGClone/UI/SlotFramework/SharedSlot");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<MMOSlotView>(), Is.Not.Null);
            Assert.That(prefab.transform.Find("Slot Visual Layers"), Is.Not.Null);

            GameObject bottomHudPrefab = Resources.Load<GameObject>("RPGClone/UI/Hud/BottomHUD");
            GameObject inventoryPrefab = Resources.Load<GameObject>("RPGClone/UI/Hud/InventoryPanel");
            Assert.That(bottomHudPrefab, Is.Not.Null);
            Assert.That(bottomHudPrefab.GetComponent<MMOBottomHudPresenter>(), Is.Not.Null);
            Assert.That(bottomHudPrefab.transform.Find("Action Bar"), Is.Not.Null);
            Assert.That(bottomHudPrefab.transform.Find("Menu Buttons"), Is.Not.Null);
            Transform bagSlots = bottomHudPrefab.transform.Find("Bag Slots");
            Assert.That(bagSlots, Is.Not.Null);
            Assert.That(bagSlots.Find("Backpack"), Is.Not.Null);
            Assert.That(bagSlots.Find("Bag Slot 1"), Is.Not.Null);
            Assert.That(bagSlots.Find("Bag Slot 2"), Is.Not.Null);
            Assert.That(bagSlots.Find("Bag Slot 3"), Is.Not.Null);
            Assert.That(bagSlots.Find("Bag Slot 4"), Is.Not.Null);
            Assert.That(bottomHudPrefab.transform.Find("Menu Buttons/Inventory"), Is.Null);
            Assert.That(inventoryPrefab, Is.Not.Null);
            Assert.That(inventoryPrefab.activeSelf, Is.True);
            Assert.That(inventoryPrefab.GetComponent<MMOInventoryPresenter>(), Is.Not.Null);
            Assert.That(inventoryPrefab.transform.Find("Slots"), Is.Not.Null);
        }

        [Test]
        public void EditableHudPrefabsContainNoMissingScripts()
        {
            GameObject bottomHudPrefab = Resources.Load<GameObject>("RPGClone/UI/Hud/BottomHUD");
            GameObject inventoryPrefab = Resources.Load<GameObject>("RPGClone/UI/Hud/InventoryPanel");

            Assert.That(bottomHudPrefab, Is.Not.Null);
            Assert.That(inventoryPrefab, Is.Not.Null);
            Assert.That(
                GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(bottomHudPrefab),
                Is.Zero);
            Assert.That(
                GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(inventoryPrefab),
                Is.Zero);
        }

        [Test]
        public void AuthoredHudPrefabGeometryAndStaticArtworkSurviveRuntimeBinding()
        {
            AssertBottomHudPrefabSurvivesRuntimeBinding();
            AssertInventoryPrefabSurvivesRuntimeBinding();
        }

        [Test]
        public void StandardWindowInitializationPreservesTheOriginalWindowArtwork()
        {
            const string prefabPath =
                "Assets/Resources/RPGClone/UI/Windows/GenericWindow.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Image background = root.GetComponent<Image>();
                Button closeButton = root.transform
                    .Find("Close Button")
                    .GetComponent<Button>();
                Image closeImage = closeButton.targetGraphic as Image;
                Assert.That(background, Is.Not.Null);
                Assert.That(closeImage, Is.Not.Null);

                Sprite backgroundSprite = background.sprite;
                Color backgroundColor = background.color;
                Image.Type backgroundType = background.type;
                Sprite closeSprite = closeImage.sprite;
                Color closeColor = closeImage.color;
                SpriteState closeStates = closeButton.spriteState;

                MMOStandardWindow.Ensure(root, "Original Window", null);

                Assert.That(background.sprite, Is.SameAs(backgroundSprite));
                Assert.That(background.color, Is.EqualTo(backgroundColor));
                Assert.That(background.type, Is.EqualTo(backgroundType));
                Assert.That(closeImage.sprite, Is.SameAs(closeSprite));
                Assert.That(closeImage.color, Is.EqualTo(closeColor));
                Assert.That(
                    closeButton.spriteState.highlightedSprite,
                    Is.SameAs(closeStates.highlightedSprite));
                Assert.That(
                    closeButton.spriteState.pressedSprite,
                    Is.SameAs(closeStates.pressedSprite));
                Assert.That(root.transform.Find("Panel Background Art"), Is.Null);
                Assert.That(root.transform.Find("Panel Frame Art"), Is.Null);
                Assert.That(root.transform.Find("Panel Header Art"), Is.Null);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void NpcWindowItemSlotsKeepTheirOriginalPresentation()
        {
            GameObject slot = new("NPC Window Item", typeof(RectTransform), typeof(Image));
            MMOItemDefinition item = ScriptableObject.CreateInstance<MMOItemDefinition>();
            try
            {
                item.Configure(
                    "window_test_item",
                    "Window Test Item",
                    string.Empty,
                    MMOItemType.Quest,
                    MMOItemQuality.Rare,
                    20,
                    0);

                MMOItemIconView.AddToWindowSlot(
                    (RectTransform)slot.transform,
                    item,
                    3,
                    false,
                    true);

                Assert.That(slot.GetComponent<MMOSlotView>(), Is.Null);
                Assert.That(slot.GetComponent<Outline>(), Is.Not.Null);
                Assert.That(slot.transform.Find("Icon Placeholder"), Is.Not.Null);
                Assert.That(slot.transform.Find("Quantity"), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(item);
                Object.DestroyImmediate(slot);
            }
        }

        [Test]
        public void PanelSkinsUseTheExistingPlaceholderFootprints()
        {
            GameObject actionBar = new("Action Bar", typeof(RectTransform), typeof(Image));
            GameObject bottomHud = new("Bottom HUD", typeof(RectTransform), typeof(Image));
            try
            {
                RectTransform actionRect = (RectTransform)actionBar.transform;
                actionRect.sizeDelta = new Vector2(642f, 58f);
                Image actionImage = actionBar.GetComponent<Image>();
                actionImage.sprite = MMOSlotSkin.ActionBarCenter;
                actionImage.color = Color.white;

                MMOPanelSkin.ApplyActionBar(actionBar);

                Assert.That(actionRect.sizeDelta, Is.EqualTo(new Vector2(642f, 58f)));
                Assert.That(actionImage.sprite, Is.Null);
                Assert.That(actionImage.color.a, Is.EqualTo(0f));

                RectTransform hudRect = (RectTransform)bottomHud.transform;
                hudRect.sizeDelta = new Vector2(1080f, 96f);
                MMOPanelSkin.ApplyBottomHud(bottomHud);

                Assert.That(hudRect.sizeDelta, Is.EqualTo(new Vector2(1080f, 96f)));
                Assert.That(bottomHud.GetComponent<Image>().sprite, Is.Null);
                Transform backgroundArt = bottomHud.transform.Find("HUD Background Art");
                Assert.That(backgroundArt, Is.Not.Null);
                Image hudImage = backgroundArt.GetComponent<Image>();
                Assert.That(hudImage.sprite, Is.SameAs(MMOSlotSkin.ActionBarCenter));
                Assert.That(hudImage.type, Is.EqualTo(Image.Type.Sliced));
                Assert.That(((RectTransform)backgroundArt).offsetMin.x, Is.EqualTo(-32f));
                Assert.That(((RectTransform)backgroundArt).offsetMax.x, Is.EqualTo(-32f));
                Assert.That(bottomHud.transform.Find("HUD Left End Cap"), Is.Null);
                Assert.That(bottomHud.transform.Find("HUD Right End Cap"), Is.Null);

                ((RectTransform)backgroundArt).anchoredPosition = new Vector2(-55f, 3f);
                MMOPanelSkin.ApplyBottomHud(bottomHud);
                Assert.That(
                    ((RectTransform)backgroundArt).anchoredPosition,
                    Is.EqualTo(new Vector2(-55f, 3f)));
            }
            finally
            {
                Object.DestroyImmediate(actionBar);
                Object.DestroyImmediate(bottomHud);
            }
        }

        [Test]
        public void SixteenSlotBagPreservesTheOriginalPlaceholderSize()
        {
            GameObject inventoryObject = new("Inventory Data");
            GameObject panel = new("Inventory Panel", typeof(RectTransform), typeof(Image));
            panel.SetActive(false);
            try
            {
                MMOInventoryContainer inventory = inventoryObject.AddComponent<MMOInventoryContainer>();
                inventory.Resize(16);
                RectTransform panelRect = (RectTransform)panel.transform;
                panelRect.sizeDelta = new Vector2(300f, 364f);

                MMOInventoryPresenter presenter = panel.AddComponent<MMOInventoryPresenter>();
                presenter.Configure(inventory);

                Assert.That(panelRect.sizeDelta, Is.EqualTo(new Vector2(300f, 364f)));
                Assert.That(panel.GetComponent<Image>().sprite, Is.Null);
                Transform backgroundArt = panel.transform.Find("Bag Panel Background Art");
                Assert.That(backgroundArt, Is.Not.Null);
                Assert.That(
                    backgroundArt.GetComponent<Image>().sprite,
                    Is.SameAs(MMOSlotSkin.BagBackground));
                Assert.That(panel.transform.Find("Slots").childCount, Is.EqualTo(16));

                RectTransform frame = (RectTransform)panel.transform.Find("Bag Panel Frame");
                Assert.That(frame.anchorMin, Is.EqualTo(Vector2.zero));
                Assert.That(frame.anchorMax, Is.EqualTo(Vector2.one));
                Assert.That(frame.offsetMin, Is.EqualTo(new Vector2(-14f, -14f)));
                Assert.That(frame.offsetMax, Is.EqualTo(new Vector2(14f, 14f)));

                RectTransform firstSlot = (RectTransform)panel.transform
                    .Find("Slots/Inventory Slot 1");
                firstSlot.anchoredPosition = new Vector2(9f, -7f);
                presenter.RefreshNow();
                Assert.That(firstSlot.anchoredPosition, Is.EqualTo(new Vector2(9f, -7f)));
            }
            finally
            {
                Object.DestroyImmediate(panel);
                Object.DestroyImmediate(inventoryObject);
            }
        }

        [Test]
        public void EightSlotBagShortensTheWindowWithoutScalingItsChildren()
        {
            GameObject inventoryObject = new("Inventory Data");
            GameObject panel = new("Inventory Panel", typeof(RectTransform), typeof(Image));
            MMOItemDefinition bag = ScriptableObject.CreateInstance<MMOItemDefinition>();
            panel.SetActive(false);
            try
            {
                bag.ConfigureContainer(
                    "test_8_slot_bag",
                    "Test Satchel",
                    string.Empty,
                    MMOItemQuality.Common,
                    8,
                    0);
                MMOInventoryContainer inventory = inventoryObject.AddComponent<MMOInventoryContainer>();
                inventory.SetSlot(0, bag, 1);
                Assert.That(inventory.TryEquipBagFromInventory(0), Is.True);

                RectTransform panelRect = (RectTransform)panel.transform;
                panelRect.sizeDelta = new Vector2(300f, 364f);
                MMOInventoryPresenter presenter = panel.AddComponent<MMOInventoryPresenter>();
                presenter.Configure(inventory, 0);

                Assert.That(panelRect.sizeDelta, Is.EqualTo(new Vector2(300f, 240f)));
                Assert.That(panel.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(panel.transform.Find("Slots").childCount, Is.EqualTo(8));
            }
            finally
            {
                Object.DestroyImmediate(bag);
                Object.DestroyImmediate(panel);
                Object.DestroyImmediate(inventoryObject);
            }
        }

        [Test]
        public void ActionBarSwapPreservesBothBindingsAndKeys()
        {
            GameObject actionBarObject = new("Action Bar", typeof(RectTransform));
            MMOItemDefinition firstItem = ScriptableObject.CreateInstance<MMOItemDefinition>();
            MMOItemDefinition secondItem = ScriptableObject.CreateInstance<MMOItemDefinition>();
            try
            {
                MMOActionBarPresenter presenter = actionBarObject.AddComponent<MMOActionBarPresenter>();
                MMOActionBarSlot first = new() { key = Key.Digit1 };
                MMOActionBarSlot second = new() { key = Key.Digit2 };
                first.SetItem(firstItem);
                second.SetItem(secondItem);
                presenter.ApplySlots(new List<MMOActionBarSlot> { first, second });

                MMOSlotDragPayload payload = MMOSlotDragPayload.ActionBarItem(firstItem, presenter, 0);
                presenter.AcceptDrop(1, payload);

                Assert.That(presenter.Slots[0].item, Is.SameAs(secondItem));
                Assert.That(presenter.Slots[1].item, Is.SameAs(firstItem));
                Assert.That(presenter.Slots[0].key, Is.EqualTo(Key.Digit1));
                Assert.That(presenter.Slots[1].key, Is.EqualTo(Key.Digit2));
            }
            finally
            {
                Object.DestroyImmediate(firstItem);
                Object.DestroyImmediate(secondItem);
                Object.DestroyImmediate(actionBarObject);
            }
        }

        private static void AssertBottomHudPrefabSurvivesRuntimeBinding()
        {
            const string prefabPath = "Assets/Resources/RPGClone/UI/Hud/BottomHUD.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Dictionary<string, RectLayoutState> layoutsBefore = CaptureRectLayouts(root);
                Dictionary<string, StaticImageState> imagesBefore = CaptureStaticImages(root);
                bool hadFallbackBackground = root.transform.Find("HUD Background Art") != null;

                MMOActionBarPresenter actionBar =
                    root.transform.Find("Action Bar").GetComponent<MMOActionBarPresenter>();
                MMOBottomHudPresenter bottomHud = root.GetComponent<MMOBottomHudPresenter>();
                Assert.That(actionBar, Is.Not.Null);
                Assert.That(bottomHud, Is.Not.Null);

                actionBar.Configure(null, null, null, actionBar.Slots);
                bottomHud.Configure(actionBar, null, null, null);

                AssertRectLayoutsEqual(layoutsBefore, CaptureRectLayouts(root), "BottomHUD");
                AssertStaticImagesEqual(imagesBefore, CaptureStaticImages(root), "BottomHUD");
                Assert.That(
                    root.transform.Find("HUD Background Art") != null,
                    Is.EqualTo(hadFallbackBackground),
                    "Runtime binding must not create a hidden fallback background.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void AssertInventoryPrefabSurvivesRuntimeBinding()
        {
            const string prefabPath = "Assets/Resources/RPGClone/UI/Hud/InventoryPanel.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            GameObject inventoryObject = new("Inventory Test Data");
            try
            {
                MMOInventoryContainer inventory =
                    inventoryObject.AddComponent<MMOInventoryContainer>();
                inventory.Resize(16);

                Dictionary<string, RectLayoutState> layoutsBefore = CaptureRectLayouts(root);
                Dictionary<string, StaticImageState> imagesBefore = CaptureStaticImages(root);
                MMOInventoryPresenter presenter = root.GetComponent<MMOInventoryPresenter>();
                Assert.That(presenter, Is.Not.Null);

                presenter.Configure(inventory);

                AssertRectLayoutsEqual(layoutsBefore, CaptureRectLayouts(root), "InventoryPanel");
                AssertStaticImagesEqual(imagesBefore, CaptureStaticImages(root), "InventoryPanel");

                Vector2 authoredRootSize = ((RectTransform)root.transform).sizeDelta;
                inventory.Resize(20);
                presenter.RefreshNow();
                Assert.That(
                    ((RectTransform)root.transform).sizeDelta,
                    Is.EqualTo(authoredRootSize + new Vector2(0f, 62f)),
                    "Authored bag chrome should resize by rows without scaling the prefab.");
            }
            finally
            {
                Object.DestroyImmediate(inventoryObject);
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Dictionary<string, RectLayoutState> CaptureRectLayouts(GameObject root)
        {
            Dictionary<string, RectLayoutState> layouts = new();
            foreach (RectTransform rect in root.GetComponentsInChildren<RectTransform>(true))
            {
                string path = AnimationUtility.CalculateTransformPath(rect, root.transform);
                layouts[path] = new RectLayoutState(rect);
            }

            return layouts;
        }

        private static Dictionary<string, StaticImageState> CaptureStaticImages(GameObject root)
        {
            Dictionary<string, StaticImageState> images = new();
            foreach (Image image in root.GetComponentsInChildren<Image>(true))
            {
                string path = AnimationUtility.CalculateTransformPath(image.transform, root.transform);
                if (path.Contains("/Slot Visual Layers/"))
                {
                    continue;
                }

                images[path] = new StaticImageState(image);
            }

            return images;
        }

        private static void AssertRectLayoutsEqual(
            Dictionary<string, RectLayoutState> expected,
            Dictionary<string, RectLayoutState> actual,
            string prefabName)
        {
            Assert.That(
                actual.Keys,
                Is.EquivalentTo(expected.Keys),
                $"{prefabName} runtime binding changed the authored object hierarchy.");
            foreach (KeyValuePair<string, RectLayoutState> pair in expected)
            {
                Assert.That(
                    actual[pair.Key],
                    Is.EqualTo(pair.Value),
                    $"{prefabName}/{pair.Key} runtime binding changed its authored RectTransform.");
            }
        }

        private static void AssertStaticImagesEqual(
            Dictionary<string, StaticImageState> expected,
            Dictionary<string, StaticImageState> actual,
            string prefabName)
        {
            Assert.That(
                actual.Keys,
                Is.EquivalentTo(expected.Keys),
                $"{prefabName} runtime binding changed the authored static artwork hierarchy.");
            foreach (KeyValuePair<string, StaticImageState> pair in expected)
            {
                Assert.That(
                    actual[pair.Key],
                    Is.EqualTo(pair.Value),
                    $"{prefabName}/{pair.Key} runtime binding changed authored static artwork.");
            }
        }

        private readonly struct RectLayoutState
        {
            private readonly Vector2 anchorMin;
            private readonly Vector2 anchorMax;
            private readonly Vector2 pivot;
            private readonly Vector2 anchoredPosition;
            private readonly Vector2 sizeDelta;
            private readonly Vector3 localPosition;
            private readonly Quaternion localRotation;
            private readonly Vector3 localScale;
            private readonly int siblingIndex;

            public RectLayoutState(RectTransform rect)
            {
                anchorMin = rect.anchorMin;
                anchorMax = rect.anchorMax;
                pivot = rect.pivot;
                anchoredPosition = rect.anchoredPosition;
                sizeDelta = rect.sizeDelta;
                localPosition = rect.localPosition;
                localRotation = rect.localRotation;
                localScale = rect.localScale;
                siblingIndex = rect.GetSiblingIndex();
            }
        }

        private readonly struct StaticImageState
        {
            private readonly Sprite sprite;
            private readonly Color color;
            private readonly Image.Type type;
            private readonly bool preserveAspect;
            private readonly bool raycastTarget;

            public StaticImageState(Image image)
            {
                sprite = image.sprite;
                color = image.color;
                type = image.type;
                preserveAspect = image.preserveAspect;
                raycastTarget = image.raycastTarget;
            }
        }
    }
}
