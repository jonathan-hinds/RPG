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
                inventoryPanel?.Toggle();
            }

            if (keyboard.pKey.wasPressedThisFrame)
            {
                spellBookPanel?.Toggle();
            }
        }

        public void Configure(
            MMOActionBarPresenter newActionBar,
            MMOCharacterPanelPresenter newCharacterPanel,
            MMOInventoryPresenter newInventoryPanel,
            MMOSpellBookPresenter newSpellBookPanel)
        {
            actionBar = newActionBar;
            characterPanel = newCharacterPanel;
            inventoryPanel = newInventoryPanel;
            spellBookPanel = newSpellBookPanel;
            BuildIfNeeded();
        }

        private void BuildIfNeeded()
        {
            RectTransform root = (RectTransform)transform;
            root.anchorMin = new Vector2(0.5f, 0f);
            root.anchorMax = new Vector2(0.5f, 0f);
            root.pivot = new Vector2(0.5f, 0f);
            root.anchoredPosition = new Vector2(0f, 18f);
            root.sizeDelta = new Vector2(870f, 96f);

            Image background = gameObject.GetComponent<Image>();
            if (background == null)
            {
                background = gameObject.AddComponent<Image>();
            }

            background.color = new Color(0.024f, 0.022f, 0.02f, 0.86f);

            if (menuButtons == null)
            {
                Transform existing = transform.Find("Menu Buttons");
                menuButtons = existing != null ? (RectTransform)existing : MMOUiFactory.CreateRect("Menu Buttons", transform);
                menuButtons.anchorMin = new Vector2(1f, 0.5f);
                menuButtons.anchorMax = new Vector2(1f, 0.5f);
                menuButtons.pivot = new Vector2(1f, 0.5f);
                menuButtons.anchoredPosition = new Vector2(-12f, 0f);
                menuButtons.sizeDelta = new Vector2(162f, 48f);
                BuildMenuButtons();
            }

            if (actionBar != null)
            {
                RectTransform actionRect = (RectTransform)actionBar.transform;
                actionRect.SetParent(transform, false);
                actionRect.anchorMin = new Vector2(0f, 0.5f);
                actionRect.anchorMax = new Vector2(0f, 0.5f);
                actionRect.pivot = new Vector2(0f, 0.5f);
                actionRect.anchoredPosition = new Vector2(12f, 0f);
            }
        }

        private void BuildMenuButtons()
        {
            MMOUiFactory.DestroyChildren(menuButtons);
            CreateMenuButton("Character", "C", 0, () => characterPanel?.Toggle());
            CreateMenuButton("Inventory", "B", 1, () => inventoryPanel?.Toggle());
            CreateMenuButton("Spellbook", "P", 2, () => spellBookPanel?.Toggle());
        }

        private void CreateMenuButton(string objectName, string label, int index, UnityEngine.Events.UnityAction onClick)
        {
            Button button = MMOUiFactory.CreateTextButton(objectName, menuButtons, label, new Vector2(46f, 42f), new Color(0.09f, 0.07f, 0.052f, 0.95f));
            button.onClick.AddListener(onClick);

            RectTransform rectTransform = button.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0f, 0.5f);
            rectTransform.anchorMax = new Vector2(0f, 0.5f);
            rectTransform.pivot = new Vector2(0f, 0.5f);
            rectTransform.anchoredPosition = new Vector2(index * 52f, 0f);
        }
    }
}
