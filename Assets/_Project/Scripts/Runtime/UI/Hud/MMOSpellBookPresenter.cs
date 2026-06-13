using RPGClone.Abilities;
using UnityEngine;
using UnityEngine.UI;

namespace RPGClone.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class MMOSpellBookPresenter : MonoBehaviour
    {
        [SerializeField] private bool autoBuild = true;
        [SerializeField] private MMOAbilitySystem abilitySystem;

        private RectTransform abilityGrid;
        private Text emptyText;

        private void Awake()
        {
            ResolveReferences();
            if (autoBuild)
            {
                BuildIfNeeded();
            }

            Refresh();
        }

        public void Configure(MMOAbilitySystem newAbilitySystem)
        {
            abilitySystem = newAbilitySystem;
            BuildIfNeeded();
            Refresh();
        }

        public void Toggle()
        {
            gameObject.SetActive(!gameObject.activeSelf);
            if (gameObject.activeSelf)
            {
                Refresh();
            }
        }

        private void ResolveReferences()
        {
            if (abilitySystem != null)
            {
                return;
            }

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                abilitySystem = player.GetComponent<MMOAbilitySystem>();
            }
        }

        private void BuildIfNeeded()
        {
            if (abilityGrid != null)
            {
                return;
            }

            bool hasStandardWindow = TryGetComponent(out MMOStandardWindow _);
            if (!hasStandardWindow && transform.childCount > 0)
            {
                MMOUiFactory.DestroyChildren(transform);
            }

            RectTransform root = (RectTransform)transform;
            if (root.sizeDelta == Vector2.zero)
            {
                root.sizeDelta = new Vector2(430f, 360f);
            }

            MMOStandardWindow window = MMOStandardWindow.Ensure(gameObject, "Spellbook", () => gameObject.SetActive(false));
            RectTransform content = window.ContentRoot;

            abilityGrid = window.FindRect("Ability Grid") ?? MMOUiFactory.CreateRect("Ability Grid", content);
            abilityGrid.anchorMin = new Vector2(0f, 0f);
            abilityGrid.anchorMax = new Vector2(1f, 1f);
            abilityGrid.offsetMin = new Vector2(14f, 18f);
            abilityGrid.offsetMax = new Vector2(-14f, -18f);

            emptyText = window.FindText("Empty") ?? MMOUiFactory.CreateText("Empty", content, 13, FontStyle.Italic, TextAnchor.MiddleCenter);
            emptyText.text = "No known abilities";
            emptyText.rectTransform.anchorMin = Vector2.zero;
            emptyText.rectTransform.anchorMax = Vector2.one;
            emptyText.rectTransform.offsetMin = new Vector2(20f, 20f);
            emptyText.rectTransform.offsetMax = new Vector2(-20f, -20f);
        }

        private void Refresh()
        {
            BuildIfNeeded();
            MMOUiFactory.DestroyChildren(abilityGrid);

            int abilityCount = abilitySystem != null ? abilitySystem.KnownAbilities.Count : 0;
            emptyText.gameObject.SetActive(abilityCount == 0);
            for (int i = 0; i < abilityCount; i++)
            {
                MMOAbilityDefinition ability = abilitySystem.KnownAbilities[i];
                if (ability == null)
                {
                    continue;
                }

                CreateAbilityButton(ability, i);
            }
        }

        private void CreateAbilityButton(MMOAbilityDefinition ability, int index)
        {
            GameObject buttonObject = new($"Ability {index + 1}", typeof(RectTransform));
            buttonObject.transform.SetParent(abilityGrid, false);

            RectTransform rectTransform = (RectTransform)buttonObject.transform;
            int column = index % 2;
            int row = index / 2;
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.anchoredPosition = new Vector2(column * 198f, -row * 58f);
            rectTransform.sizeDelta = new Vector2(188f, 48f);

            Image background = buttonObject.AddComponent<Image>();
            background.color = new Color(0.08f, 0.065f, 0.05f, 0.95f);

            Button button = buttonObject.AddComponent<Button>();
            button.onClick.AddListener(() => MMOActionBarDragState.EndDrag());

            Image icon = MMOUiFactory.CreateImage("Icon", buttonObject.transform, ability.Icon != null ? Color.white : new Color(0.18f, 0.12f, 0.055f, 1f), false);
            icon.sprite = ability.Icon;
            icon.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            icon.rectTransform.pivot = new Vector2(0f, 0.5f);
            icon.rectTransform.anchoredPosition = new Vector2(5f, 0f);
            icon.rectTransform.sizeDelta = new Vector2(38f, 38f);

            Text label = MMOUiFactory.CreateText("Name", buttonObject.transform, 12, FontStyle.Bold, TextAnchor.MiddleLeft);
            label.text = ability.DisplayName;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(50f, 5f);
            label.rectTransform.offsetMax = new Vector2(-8f, -5f);

            MMOSpellBookAbilityView dragView = buttonObject.AddComponent<MMOSpellBookAbilityView>();
            dragView.Configure(ability);

            MMOAbilityTooltipTrigger tooltipTrigger = buttonObject.AddComponent<MMOAbilityTooltipTrigger>();
            tooltipTrigger.Configure(ability);
        }
    }
}
