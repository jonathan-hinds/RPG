using System.Collections.Generic;
using RPGClone.Abilities;
using RPGClone.CharacterSelection;
using RPGClone.Combat;
using RPGClone.Inventory;
using RPGClone.Quests;
using RPGClone.Targeting;
using RPGClone.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RPGClone.EditorTools
{
    public static class MMOHudLayoutPrefabAuthoring
    {
        public const string PrefabFolder = "Assets/Resources/RPGClone/UI/Hud";
        public const string BottomHudPrefabPath = PrefabFolder + "/BottomHUD.prefab";
        public const string InventoryPanelPrefabPath = PrefabFolder + "/InventoryPanel.prefab";

        [MenuItem("Tools/RPG Clone/UI/Rebuild Editable HUD Prefabs")]
        public static void RebuildEditableHudPrefabs()
        {
            EnsureFolder(PrefabFolder);
            BuildBottomHudPrefab();
            BuildInventoryPanelPrefab();
            ApplyPrefabsToActiveScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "Editable HUD prefabs rebuilt and connected: "
                + BottomHudPrefabPath
                + " and "
                + InventoryPanelPrefabPath
                + ".");
        }

        [MenuItem("Tools/RPG Clone/UI/Install Bag Bar Into Editable HUD Prefab")]
        public static void InstallBagBarIntoEditableHudPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(BottomHudPrefabPath);
            try
            {
                Transform menuButtons = root.transform.Find("Menu Buttons");
                if (menuButtons != null)
                {
                    Transform legacyInventoryButton = menuButtons.Find("Inventory");
                    if (legacyInventoryButton != null)
                    {
                        Object.DestroyImmediate(legacyInventoryButton.gameObject);
                    }

                    RectTransform menuRect = (RectTransform)menuButtons;
                    menuRect.anchoredPosition = new Vector2(menuRect.anchoredPosition.x, -23f);
                    menuRect.sizeDelta = new Vector2(314f, menuRect.sizeDelta.y);
                    PositionMenuButton(menuButtons, "Character", 0);
                    PositionMenuButton(menuButtons, "Spellbook", 1);
                    PositionMenuButton(menuButtons, "Quest Log", 2);
                    PositionMenuButton(menuButtons, "Friends", 3);
                    PositionMenuButton(menuButtons, "Exit", 4);
                }

                Transform existingBagRoot = root.transform.Find("Bag Slots");
                GameObject bagObject = existingBagRoot != null
                    ? existingBagRoot.gameObject
                    : new GameObject("Bag Slots", typeof(RectTransform), typeof(MMOBagBarPresenter));
                RectTransform bagRect = (RectTransform)bagObject.transform;
                bagRect.SetParent(root.transform, false);
                bagRect.anchorMin = new Vector2(1f, 0.5f);
                bagRect.anchorMax = new Vector2(1f, 0.5f);
                bagRect.pivot = new Vector2(1f, 0.5f);
                bagRect.anchoredPosition = new Vector2(-12f, 23f);
                bagRect.sizeDelta = new Vector2(226f, 42f);

                MMOBagBarPresenter bagBar = bagObject.GetComponent<MMOBagBarPresenter>();
                if (bagBar == null)
                {
                    bagBar = bagObject.AddComponent<MMOBagBarPresenter>();
                }

                bagBar.Configure(null, null);
                MMOBottomHudPresenter bottomHud = root.GetComponent<MMOBottomHudPresenter>();
                if (bottomHud != null)
                {
                    SerializedObject serializedBottomHud = new(bottomHud);
                    serializedBottomHud.FindProperty("bagBar").objectReferenceValue = bagBar;
                    serializedBottomHud.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, BottomHudPrefabPath);
                ConfigureInventoryPrefabForDynamicRows();
                AssetDatabase.SaveAssets();
                Debug.Log("Installed the five-slot bag bar and configured the inventory prefab for dynamic bag rows.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("Tools/RPG Clone/UI/Sync HUD Scene Instances From Prefabs")]
        public static void SyncHudSceneInstancesFromPrefabs()
        {
            ApplyPrefabsToActiveScene();
            AssetDatabase.SaveAssets();
            Debug.Log(
                "HUD scene instances synchronized from the editable prefabs without changing "
                + "the prefab assets.");
        }

        private static void BuildBottomHudPrefab()
        {
            GameObject root = new("Bottom HUD", typeof(RectTransform), typeof(Image));
            try
            {
                RectTransform rootRect = (RectTransform)root.transform;
                rootRect.anchorMin = new Vector2(0.5f, 0f);
                rootRect.anchorMax = new Vector2(0.5f, 0f);
                rootRect.pivot = new Vector2(0.5f, 0f);
                rootRect.anchoredPosition = new Vector2(0f, 18f);
                rootRect.sizeDelta = new Vector2(1080f, 96f);

                GameObject actionBarObject = new("Action Bar", typeof(RectTransform));
                actionBarObject.transform.SetParent(root.transform, false);
                RectTransform actionRect = (RectTransform)actionBarObject.transform;
                actionRect.anchorMin = new Vector2(0f, 0.5f);
                actionRect.anchorMax = new Vector2(0f, 0.5f);
                actionRect.pivot = new Vector2(0f, 0.5f);
                actionRect.anchoredPosition = new Vector2(12f, 0f);
                actionRect.sizeDelta = new Vector2(642f, 58f);

                MMOActionBarPresenter actionBar = actionBarObject.AddComponent<MMOActionBarPresenter>();
                actionBar.ApplySlots(CreateEmptyActionSlots());

                MMOPanelSkin.ApplyBottomHud(root);
                MMOBottomHudPresenter bottomHud = root.AddComponent<MMOBottomHudPresenter>();
                bottomHud.Configure(actionBar, null, null, null);
                PrefabUtility.SaveAsPrefabAsset(root, BottomHudPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void PositionMenuButton(Transform menuButtons, string buttonName, int index)
        {
            Transform button = menuButtons.Find(buttonName);
            if (button == null)
            {
                return;
            }

            RectTransform rect = (RectTransform)button;
            rect.anchoredPosition = new Vector2(index * 64f, rect.anchoredPosition.y);
        }

        private static void ConfigureInventoryPrefabForDynamicRows()
        {
            GameObject inventoryRoot = PrefabUtility.LoadPrefabContents(InventoryPanelPrefabPath);
            try
            {
                ReanchorVertically(inventoryRoot.transform.Find("Bag Panel Header") as RectTransform, 1f);
                ReanchorVertically(inventoryRoot.transform.Find("Bag Panel Currency Well") as RectTransform, 0f);
                EnsureSlicedImage(inventoryRoot.transform.Find("Bag Panel Background Art"));
                EnsureSlicedImage(inventoryRoot.transform.Find("Bag Panel Frame"));
                PrefabUtility.SaveAsPrefabAsset(inventoryRoot, InventoryPanelPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(inventoryRoot);
            }
        }

        private static void ReanchorVertically(RectTransform rect, float anchorY)
        {
            if (rect == null || rect.parent is not RectTransform parent)
            {
                return;
            }

            float oldAnchorY = (rect.anchorMin.y + rect.anchorMax.y) * 0.5f;
            Vector2 anchoredPosition = rect.anchoredPosition;
            anchoredPosition.y += (oldAnchorY - anchorY) * parent.rect.height;
            rect.anchorMin = new Vector2(rect.anchorMin.x, anchorY);
            rect.anchorMax = new Vector2(rect.anchorMax.x, anchorY);
            rect.anchoredPosition = anchoredPosition;
        }

        private static void EnsureSlicedImage(Transform target)
        {
            Image image = target != null ? target.GetComponent<Image>() : null;
            if (image != null && image.sprite != null && image.sprite.border.sqrMagnitude > 0f)
            {
                image.type = Image.Type.Sliced;
            }
        }

        private static void BuildInventoryPanelPrefab()
        {
            GameObject inventoryDataObject = new("Inventory Prefab Authoring Data");
            GameObject root = new("Inventory Panel", typeof(RectTransform), typeof(Image));
            try
            {
                root.SetActive(false);
                RectTransform rootRect = (RectTransform)root.transform;
                rootRect.anchorMin = new Vector2(1f, 0f);
                rootRect.anchorMax = new Vector2(1f, 0f);
                rootRect.pivot = new Vector2(1f, 0f);
                rootRect.anchoredPosition = new Vector2(-38f, 124f);
                rootRect.sizeDelta = new Vector2(300f, 364f);

                MMOInventoryContainer inventory = inventoryDataObject.AddComponent<MMOInventoryContainer>();
                inventory.Resize(16);
                MMOInventoryPresenter presenter = root.AddComponent<MMOInventoryPresenter>();
                presenter.Configure(inventory);

                SerializedObject serializedPresenter = new(presenter);
                serializedPresenter.FindProperty("inventory").objectReferenceValue = null;
                serializedPresenter.FindProperty("wallet").objectReferenceValue = null;
                serializedPresenter.ApplyModifiedPropertiesWithoutUndo();

                // Prefab assets should be visible in Prefab Mode. Scene instances remain
                // closed by default through an explicit active-state override.
                root.SetActive(true);
                PrefabUtility.SaveAsPrefabAsset(root, InventoryPanelPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(inventoryDataObject);
            }
        }

        private static void ApplyPrefabsToActiveScene()
        {
            Transform canvas = FindHudCanvas();
            if (canvas == null)
            {
                Debug.LogWarning("Editable HUD prefabs were created, but no HUD Canvas was found to connect.");
                return;
            }

            MMOInventoryPresenter inventoryPanel = ReplaceInventoryPanel(canvas);
            ReplaceBottomHud(canvas, inventoryPanel);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }

        private static MMOInventoryPresenter ReplaceInventoryPanel(Transform canvas)
        {
            Transform existing = canvas.Find("Inventory Panel");
            MMOInventoryContainer inventory = existing != null
                ? ReadObjectReference<MMOInventoryContainer>(
                    existing.GetComponent<MMOInventoryPresenter>(),
                    "inventory")
                : null;
            MMOCurrencyWallet wallet = existing != null
                ? ReadObjectReference<MMOCurrencyWallet>(
                    existing.GetComponent<MMOInventoryPresenter>(),
                    "wallet")
                : null;
            bool wasActive = existing != null && existing.gameObject.activeSelf;
            int siblingIndex = existing != null ? existing.GetSiblingIndex() : canvas.childCount;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(InventoryPanelPrefabPath);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas);
            instance.name = "Inventory Panel";
            RevertRootRectTransform(instance);
            instance.transform.SetSiblingIndex(siblingIndex);

            MMOInventoryPresenter presenter = instance.GetComponent<MMOInventoryPresenter>();
            ApplyInventoryBindings(presenter, inventory, wallet);
            instance.SetActive(wasActive);

            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            return presenter;
        }

        private static void ReplaceBottomHud(Transform canvas, MMOInventoryPresenter inventoryPanel)
        {
            Transform existing = canvas.Find("Bottom HUD");
            MMOBottomHudPresenter existingBottom = existing != null
                ? existing.GetComponent<MMOBottomHudPresenter>()
                : null;
            MMOActionBarPresenter existingActionBar = existingBottom != null
                ? ReadObjectReference<MMOActionBarPresenter>(existingBottom, "actionBar")
                : null;

            MMOAbilitySystem abilitySystem = ReadObjectReference<MMOAbilitySystem>(
                existingActionBar,
                "abilitySystem");
            MMOAutoAttackController autoAttack = ReadObjectReference<MMOAutoAttackController>(
                existingActionBar,
                "autoAttackController");
            MMOTargetSelectionController targetSelection = ReadObjectReference<MMOTargetSelectionController>(
                existingActionBar,
                "targetSelectionController");
            MMOInventoryContainer inventory = ReadObjectReference<MMOInventoryContainer>(
                existingActionBar,
                "inventory");
            MMOGroundTargetingController groundTargeting =
                ReadObjectReference<MMOGroundTargetingController>(
                    existingActionBar,
                    "groundTargetingController");
            List<MMOActionBarSlot> actionSlots = existingActionBar != null
                ? new List<MMOActionBarSlot>(existingActionBar.Slots)
                : CreateEmptyActionSlots();

            MMOCharacterPanelPresenter characterPanel = ReadObjectReference<MMOCharacterPanelPresenter>(
                existingBottom,
                "characterPanel");
            MMOSpellBookPresenter spellBook = ReadObjectReference<MMOSpellBookPresenter>(
                existingBottom,
                "spellBookPanel");
            MMOQuestLogPresenter questLog = ReadObjectReference<MMOQuestLogPresenter>(
                existingBottom,
                "questLogPanel");
            MMOSocialWindowPresenter social = ReadObjectReference<MMOSocialWindowPresenter>(
                existingBottom,
                "socialPanel");
            MMOReturnToCharacterSelectionController returnController =
                ReadObjectReference<MMOReturnToCharacterSelectionController>(
                    existingBottom,
                    "returnToCharacterSelectionController");

            bool wasActive = existing == null || existing.gameObject.activeSelf;
            int siblingIndex = existing != null ? existing.GetSiblingIndex() : canvas.childCount;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BottomHudPrefabPath);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas);
            instance.name = "Bottom HUD";
            RevertRootRectTransform(instance);
            instance.transform.SetSiblingIndex(siblingIndex);

            MMOActionBarPresenter actionBar = instance.transform
                .Find("Action Bar")
                .GetComponent<MMOActionBarPresenter>();
            ApplyActionBarBindings(
                actionBar,
                abilitySystem,
                inventory,
                autoAttack,
                targetSelection,
                groundTargeting,
                actionSlots);

            MMOBottomHudPresenter bottomHud = instance.GetComponent<MMOBottomHudPresenter>();
            ApplyBottomHudBindings(
                bottomHud,
                actionBar,
                characterPanel,
                inventoryPanel,
                spellBook,
                questLog,
                social,
                returnController);
            instance.SetActive(wasActive);

            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }
        }

        private static void ApplyInventoryBindings(
            MMOInventoryPresenter presenter,
            MMOInventoryContainer inventory,
            MMOCurrencyWallet wallet)
        {
            SerializedObject serialized = new(presenter);
            serialized.FindProperty("inventory").objectReferenceValue = inventory;
            serialized.FindProperty("wallet").objectReferenceValue = wallet;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(presenter);
        }

        private static void ApplyActionBarBindings(
            MMOActionBarPresenter presenter,
            MMOAbilitySystem abilitySystem,
            MMOInventoryContainer inventory,
            MMOAutoAttackController autoAttack,
            MMOTargetSelectionController targetSelection,
            MMOGroundTargetingController groundTargeting,
            IReadOnlyList<MMOActionBarSlot> actionSlots)
        {
            SerializedObject serialized = new(presenter);
            serialized.FindProperty("abilitySystem").objectReferenceValue = abilitySystem;
            serialized.FindProperty("inventory").objectReferenceValue = inventory;
            serialized.FindProperty("autoAttackController").objectReferenceValue = autoAttack;
            serialized.FindProperty("targetSelectionController").objectReferenceValue = targetSelection;
            serialized.FindProperty("groundTargetingController").objectReferenceValue = groundTargeting;

            int slotTotal = actionSlots != null
                ? Mathf.Max(MMOActionBarPresenter.DefaultSlotCount, actionSlots.Count)
                : MMOActionBarPresenter.DefaultSlotCount;
            serialized.FindProperty("slotCount").intValue = slotTotal;
            SerializedProperty serializedSlots = serialized.FindProperty("slots");
            serializedSlots.arraySize = slotTotal;
            for (int index = 0; index < slotTotal; index++)
            {
                MMOActionBarSlot source = actionSlots != null && index < actionSlots.Count
                    ? actionSlots[index]
                    : new MMOActionBarSlot();
                SerializedProperty destination = serializedSlots.GetArrayElementAtIndex(index);
                destination.FindPropertyRelative("bindingType").enumValueIndex =
                    (int)source.bindingType;
                destination.FindPropertyRelative("ability").objectReferenceValue = source.ability;
                destination.FindPropertyRelative("item").objectReferenceValue = source.item;
                destination.FindPropertyRelative("key").intValue = (int)source.key;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(presenter);
        }

        private static void ApplyBottomHudBindings(
            MMOBottomHudPresenter presenter,
            MMOActionBarPresenter actionBar,
            MMOCharacterPanelPresenter characterPanel,
            MMOInventoryPresenter inventoryPanel,
            MMOSpellBookPresenter spellBook,
            MMOQuestLogPresenter questLog,
            MMOSocialWindowPresenter social,
            MMOReturnToCharacterSelectionController returnController)
        {
            SerializedObject serialized = new(presenter);
            serialized.FindProperty("actionBar").objectReferenceValue = actionBar;
            serialized.FindProperty("characterPanel").objectReferenceValue = characterPanel;
            serialized.FindProperty("inventoryPanel").objectReferenceValue = inventoryPanel;
            serialized.FindProperty("spellBookPanel").objectReferenceValue = spellBook;
            serialized.FindProperty("questLogPanel").objectReferenceValue = questLog;
            serialized.FindProperty("socialPanel").objectReferenceValue = social;
            serialized.FindProperty("returnToCharacterSelectionController").objectReferenceValue =
                returnController;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(presenter);
        }

        private static List<MMOActionBarSlot> CreateEmptyActionSlots()
        {
            Key[] keys =
            {
                Key.Digit1,
                Key.Digit2,
                Key.Digit3,
                Key.Digit4,
                Key.Digit5,
                Key.Digit6,
                Key.Digit7,
                Key.Digit8,
                Key.Digit9,
                Key.Digit0,
                Key.Minus,
                Key.Equals
            };
            List<MMOActionBarSlot> slots = new(MMOActionBarPresenter.DefaultSlotCount);
            for (int i = 0; i < MMOActionBarPresenter.DefaultSlotCount; i++)
            {
                slots.Add(new MMOActionBarSlot { key = keys[i] });
            }

            return slots;
        }

        private static Transform FindHudCanvas()
        {
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (Canvas canvas in canvases)
            {
                if (canvas.name == "HUD Canvas")
                {
                    return canvas.transform;
                }
            }

            return null;
        }

        private static T ReadObjectReference<T>(Object owner, string propertyName)
            where T : Object
        {
            if (owner == null)
            {
                return null;
            }

            SerializedObject serialized = new(owner);
            SerializedProperty property = serialized.FindProperty(propertyName);
            return property != null ? property.objectReferenceValue as T : null;
        }

        private static void RevertRootRectTransform(GameObject instance)
        {
            RectTransform root = instance != null ? instance.GetComponent<RectTransform>() : null;
            if (root != null)
            {
                PrefabUtility.RevertObjectOverride(root, InteractionMode.AutomatedAction);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent))
            {
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}
