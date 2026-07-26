using RPGClone.CharacterSelection;
using RPGClone.Services;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RPGClone.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class MMOBottomHudPresenter : MonoBehaviour
    {
        [SerializeField] private bool autoBuild = true;
        [SerializeField] private MMOActionBarPresenter actionBar;
        [SerializeField] private MMOCharacterPanelPresenter characterPanel;
        [SerializeField] private MMOInventoryPresenter inventoryPanel;
        [SerializeField] private MMOSpellBookPresenter spellBookPanel;
        [SerializeField] private MMOQuestLogPresenter questLogPanel;
        [SerializeField] private MMOSocialWindowPresenter socialPanel;
        [SerializeField] private MMOBagBarPresenter bagBar;
        [SerializeField] private MMOReturnToCharacterSelectionController returnToCharacterSelectionController;

        private RectTransform menuButtons;

        private void Awake()
        {
            if (autoBuild)
            {
                BuildIfNeeded();
            }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.cKey.wasPressedThisFrame)
            {
                characterPanel?.Toggle();
            }

            if (keyboard.bKey.wasPressedThisFrame || keyboard.iKey.wasPressedThisFrame)
            {
                bagBar?.ToggleBag(MMOBagBarPresenter.BackpackBagIndex);
            }

            if (keyboard.pKey.wasPressedThisFrame)
            {
                spellBookPanel?.Toggle();
            }

            if (keyboard.lKey.wasPressedThisFrame)
            {
                questLogPanel?.Toggle();
            }

            if (keyboard.oKey.wasPressedThisFrame)
            {
                socialPanel?.Toggle();
            }

            if (keyboard.f12Key.wasPressedThisFrame)
            {
                bagBar?.ToggleBag(MMOBagBarPresenter.BackpackBagIndex);
            }
            else if (keyboard.f11Key.wasPressedThisFrame)
            {
                bagBar?.ToggleBag(0);
            }
            else if (keyboard.f10Key.wasPressedThisFrame)
            {
                bagBar?.ToggleBag(1);
            }
            else if (keyboard.f9Key.wasPressedThisFrame)
            {
                bagBar?.ToggleBag(2);
            }
            else if (keyboard.f8Key.wasPressedThisFrame)
            {
                bagBar?.ToggleBag(3);
            }
        }

        public void Configure(
            MMOActionBarPresenter newActionBar,
            MMOCharacterPanelPresenter newCharacterPanel,
            MMOInventoryPresenter newInventoryPanel,
            MMOSpellBookPresenter newSpellBookPanel,
            MMOQuestLogPresenter newQuestLogPanel = null,
            MMOSocialWindowPresenter newSocialPanel = null,
            MMOReturnToCharacterSelectionController newReturnToCharacterSelectionController = null)
        {
            actionBar = newActionBar;
            characterPanel = newCharacterPanel;
            inventoryPanel = newInventoryPanel;
            spellBookPanel = newSpellBookPanel;
            questLogPanel = newQuestLogPanel;
            socialPanel = newSocialPanel;
            returnToCharacterSelectionController = newReturnToCharacterSelectionController;
            BuildIfNeeded();
        }

        private void BuildIfNeeded()
        {
            bool hasAuthoredLayout = transform.childCount > 0;
            RectTransform root = (RectTransform)transform;
            if (root.sizeDelta.x <= 0f || root.sizeDelta.y <= 0f)
            {
                root.anchorMin = new Vector2(0.5f, 0f);
                root.anchorMax = new Vector2(0.5f, 0f);
                root.pivot = new Vector2(0.5f, 0f);
                root.anchoredPosition = new Vector2(0f, 18f);
                root.sizeDelta = new Vector2(1080f, 96f);
            }

            if (!hasAuthoredLayout)
            {
                MMOPanelSkin.ApplyBottomHud(gameObject);
            }

            ResolveSocialPanel();

            if (menuButtons == null)
            {
                Transform existing = transform.Find("Menu Buttons");
                bool createdMenuButtons = existing == null;
                menuButtons = createdMenuButtons
                    ? MMOUiFactory.CreateRect("Menu Buttons", transform)
                    : (RectTransform)existing;
                if (createdMenuButtons)
                {
                    menuButtons.anchorMin = new Vector2(1f, 0.5f);
                    menuButtons.anchorMax = new Vector2(1f, 0.5f);
                    menuButtons.pivot = new Vector2(1f, 0.5f);
                    menuButtons.anchoredPosition = new Vector2(-12f, 0f);
                    menuButtons.sizeDelta = new Vector2(378f, 48f);
                }
            }

            BuildMenuButtons();
            ResolveBagBar();

            if (actionBar != null)
            {
                RectTransform actionRect = (RectTransform)actionBar.transform;
                bool wasReparented = actionRect.parent != transform;
                if (wasReparented)
                {
                    actionRect.SetParent(transform, false);
                    actionRect.anchorMin = new Vector2(0f, 0.5f);
                    actionRect.anchorMax = new Vector2(0f, 0.5f);
                    actionRect.pivot = new Vector2(0f, 0.5f);
                    actionRect.anchoredPosition = new Vector2(12f, 0f);
                }
            }
        }

        private void BuildMenuButtons()
        {
            if (returnToCharacterSelectionController == null)
            {
                MMOGameplaySessionService.LocalPlayer.TryGetComponent(out returnToCharacterSelectionController);
            }

            CreateMenuButton("Character", "Char", 0, () => characterPanel?.Toggle());
            Transform legacyInventoryButton = menuButtons.Find("Inventory");
            if (legacyInventoryButton != null)
            {
                legacyInventoryButton.gameObject.SetActive(false);
            }

            CreateMenuButton("Spellbook", "Book", 1, () => spellBookPanel?.Toggle());
            CreateMenuButton("Quest Log", "Quest", 2, () => questLogPanel?.Toggle());
            CreateMenuButton("Friends", "Social", 3, () => socialPanel?.Toggle());
            CreateMenuButton("Exit", "Exit", 4, () => returnToCharacterSelectionController?.ReturnToCharacterSelection());
        }

        private void CreateMenuButton(string objectName, string label, int index, UnityEngine.Events.UnityAction onClick)
        {
            Transform existing = menuButtons.Find(objectName);
            bool created = existing == null;
            Button button = created
                ? MMOUiFactory.CreateTextButton(objectName, menuButtons, label, new Vector2(58f, 42f), new Color(0.09f, 0.07f, 0.052f, 0.95f))
                : existing.GetComponent<Button>();
            if (button == null)
            {
                button = existing.gameObject.AddComponent<Button>();
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(onClick);
            Text buttonLabel = MMOUiFactory.FindButtonLabel(button);
            if (created && buttonLabel != null)
            {
                buttonLabel.text = label;
            }

            if (created)
            {
                RectTransform rectTransform = button.GetComponent<RectTransform>();
                rectTransform.anchorMin = new Vector2(0f, 0.5f);
                rectTransform.anchorMax = new Vector2(0f, 0.5f);
                rectTransform.pivot = new Vector2(0f, 0.5f);
                rectTransform.anchoredPosition = new Vector2(index * 64f, 0f);
            }
        }

        private void ResolveSocialPanel()
        {
            if (socialPanel != null)
            {
                return;
            }

            socialPanel = FindAnyObjectByType<MMOSocialWindowPresenter>(FindObjectsInactive.Include);
            if (socialPanel != null)
            {
                return;
            }

            Transform canvas = transform.parent;
            if (canvas == null)
            {
                return;
            }

            GameObject panelObject = MMOWindowPrefabResolver.Instantiate(MMOWindowPrefabId.Social, canvas, "Friends Panel");
            socialPanel = panelObject.GetComponent<MMOSocialWindowPresenter>();
            if (socialPanel == null)
            {
                socialPanel = panelObject.AddComponent<MMOSocialWindowPresenter>();
            }

            panelObject.SetActive(false);
        }

        private void ResolveBagBar()
        {
            if (bagBar == null)
            {
                Transform existing = transform.Find("Bag Slots");
                bool created = existing == null;
                RectTransform bagRoot = created
                    ? MMOUiFactory.CreateRect("Bag Slots", transform)
                    : (RectTransform)existing;
                if (created)
                {
                    bagRoot.anchorMin = new Vector2(1f, 0.5f);
                    bagRoot.anchorMax = new Vector2(1f, 0.5f);
                    bagRoot.pivot = new Vector2(1f, 0.5f);
                    bagRoot.anchoredPosition = new Vector2(-12f, 23f);
                    bagRoot.sizeDelta = new Vector2(226f, 42f);
                }

                bagBar = bagRoot.GetComponent<MMOBagBarPresenter>();
                if (bagBar == null)
                {
                    bagBar = bagRoot.gameObject.AddComponent<MMOBagBarPresenter>();
                }
            }

            MMOGameplaySessionService.LocalPlayer.TryGetComponent(out RPGClone.Inventory.MMOInventoryContainer inventory);
            bagBar.Configure(inventory, inventoryPanel);
        }
    }
}
