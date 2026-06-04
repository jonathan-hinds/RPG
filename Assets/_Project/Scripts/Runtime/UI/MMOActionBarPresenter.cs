using System.Collections.Generic;
using RPGClone.Abilities;
using RPGClone.Characters;
using RPGClone.Combat;
using RPGClone.Targeting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RPGClone.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class MMOActionBarPresenter : MonoBehaviour
    {
        public const int DefaultSlotCount = 12;

        [SerializeField] private bool autoBuild = true;
        [SerializeField, Min(1)] private int slotCount = DefaultSlotCount;
        [SerializeField] private MMOAbilitySystem abilitySystem;
        [SerializeField] private MMOAutoAttackController autoAttackController;
        [SerializeField] private MMOTargetSelectionController targetSelectionController;
        [SerializeField] private List<MMOActionBarSlot> slots = new();

        private readonly List<Button> buttons = new();
        private readonly List<Text> labels = new();
        private readonly List<Text> keyLabels = new();
        private readonly List<Image> iconImages = new();
        private readonly List<Image> backgrounds = new();
        private readonly List<MMOActionBarSlotView> slotViews = new();
        private static Font cachedFont;

        private void Awake()
        {
            ResolveReferences();
            if (autoBuild)
            {
                BuildIfNeeded();
            }

            Refresh();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                Key key = slots[i].key;
                if (key != Key.None && keyboard[key].wasPressedThisFrame)
                {
                    ActivateSlot(i);
                }
            }
        }

        public void Configure(
            MMOAbilitySystem newAbilitySystem,
            MMOAutoAttackController newAutoAttackController,
            MMOTargetSelectionController newTargetSelectionController,
            IReadOnlyList<MMOActionBarSlot> newSlots)
        {
            abilitySystem = newAbilitySystem;
            autoAttackController = newAutoAttackController;
            targetSelectionController = newTargetSelectionController;
            slots = newSlots != null ? new List<MMOActionBarSlot>(newSlots) : new List<MMOActionBarSlot>();
            slotCount = Mathf.Max(DefaultSlotCount, slots.Count);
            EnsureSlotState();
            BuildIfNeeded();
            Refresh();
        }

        public void ActivateSlot(int index)
        {
            if (index < 0 || index >= slots.Count)
            {
                return;
            }

            MMOAbilityDefinition ability = slots[index].ability;
            if (ability == null)
            {
                return;
            }

            MMOCharacterIdentity target = targetSelectionController != null ? targetSelectionController.CurrentTarget : null;
            if (ability.IsAutoAttack && autoAttackController != null)
            {
                autoAttackController.ToggleAutoAttack(target);
                return;
            }

            abilitySystem?.TryUseAbility(ability, target, out _);
        }

        public bool BeginSlotDrag(int index, PointerEventData eventData, Transform owner)
        {
            if (index < 0 || index >= slots.Count)
            {
                return false;
            }

            MMOAbilityDefinition ability = slots[index].ability;
            if (ability == null)
            {
                return false;
            }

            return MMOAbilityDragState.BeginDrag(
                new MMOAbilityDragPayload(ability, this, index),
                eventData,
                owner,
                ability.DisplayName,
                ability.Icon);
        }

        public void AcceptAbilityDrop(int targetIndex, MMOAbilityDragPayload payload)
        {
            if (!payload.IsValid || targetIndex < 0 || targetIndex >= slots.Count)
            {
                return;
            }

            if (payload.FromActionBar && payload.SourceActionBar == this)
            {
                if (payload.SourceSlotIndex == targetIndex)
                {
                    return;
                }

                (slots[targetIndex].ability, slots[payload.SourceSlotIndex].ability) = (slots[payload.SourceSlotIndex].ability, slots[targetIndex].ability);
            }
            else
            {
                slots[targetIndex].ability = payload.Ability;
            }

            Refresh();
        }

        private void ResolveReferences()
        {
            if (abilitySystem == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    abilitySystem = player.GetComponent<MMOAbilitySystem>();
                    autoAttackController = player.GetComponent<MMOAutoAttackController>();
                }
            }

            if (targetSelectionController == null)
            {
                targetSelectionController = FindAnyObjectByType<MMOTargetSelectionController>();
            }
        }

        private void BuildIfNeeded()
        {
            EnsureSlotState();
            if (buttons.Count == 0 && transform.childCount > 0)
            {
                MMOUiFactory.DestroyChildren(transform);
            }

            RectTransform root = (RectTransform)transform;
            if (root.sizeDelta == Vector2.zero)
            {
                root.sizeDelta = new Vector2((48f * slotCount) + (6f * (slotCount - 1)), 58f);
            }

            while (buttons.Count < slots.Count)
            {
                int index = buttons.Count;
                CreateSlotButton(index);
            }

            for (int i = 0; i < buttons.Count; i++)
            {
                buttons[i].gameObject.SetActive(i < slotCount);
                if (i < slotViews.Count)
                {
                    slotViews[i].Configure(this, i);
                }
            }
        }

        private void CreateSlotButton(int index)
        {
            GameObject buttonObject = new($"Action Slot {index + 1}", typeof(RectTransform));
            buttonObject.transform.SetParent(transform, false);

            RectTransform rectTransform = (RectTransform)buttonObject.transform;
            rectTransform.anchorMin = new Vector2(0f, 0.5f);
            rectTransform.anchorMax = new Vector2(0f, 0.5f);
            rectTransform.pivot = new Vector2(0f, 0.5f);
            rectTransform.anchoredPosition = new Vector2(index * 54f, 0f);
            rectTransform.sizeDelta = new Vector2(48f, 48f);

            Image background = buttonObject.AddComponent<Image>();
            background.color = new Color(0.05f, 0.045f, 0.04f, 0.95f);

            Button button = buttonObject.AddComponent<Button>();
            int capturedIndex = index;
            button.onClick.AddListener(() => ActivateSlot(capturedIndex));

            Text label = CreateText("Ability Label", buttonObject.transform, 10, FontStyle.Bold, TextAnchor.MiddleCenter);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(3f, 9f);
            label.rectTransform.offsetMax = new Vector2(-3f, -3f);

            Image iconImage = MMOUiFactory.CreateImage("Icon", buttonObject.transform, Color.white, false);
            iconImage.rectTransform.anchorMin = Vector2.zero;
            iconImage.rectTransform.anchorMax = Vector2.one;
            iconImage.rectTransform.offsetMin = new Vector2(3f, 3f);
            iconImage.rectTransform.offsetMax = new Vector2(-3f, -3f);
            iconImage.gameObject.SetActive(false);
            iconImage.transform.SetAsFirstSibling();

            Text keyLabel = CreateText("Key Label", buttonObject.transform, 10, FontStyle.Bold, TextAnchor.UpperLeft);
            keyLabel.rectTransform.anchorMin = Vector2.zero;
            keyLabel.rectTransform.anchorMax = Vector2.one;
            keyLabel.rectTransform.offsetMin = new Vector2(4f, 2f);
            keyLabel.rectTransform.offsetMax = new Vector2(-4f, -2f);
            keyLabel.color = new Color(0.98f, 0.82f, 0.36f, 1f);

            MMOActionBarSlotView slotView = buttonObject.AddComponent<MMOActionBarSlotView>();
            slotView.Configure(this, index);

            buttons.Add(button);
            labels.Add(label);
            keyLabels.Add(keyLabel);
            iconImages.Add(iconImage);
            backgrounds.Add(background);
            slotViews.Add(slotView);
        }

        private void Refresh()
        {
            BuildIfNeeded();
            for (int i = 0; i < slots.Count; i++)
            {
                MMOAbilityDefinition ability = slots[i].ability;
                bool hasAbility = ability != null;
                labels[i].text = hasAbility && ability.Icon == null ? Shorten(ability.DisplayName) : string.Empty;
                keyLabels[i].text = slots[i].key == Key.None ? string.Empty : GetKeyLabel(slots[i].key);
                buttons[i].interactable = true;
                backgrounds[i].color = hasAbility
                    ? new Color(0.12f, 0.09f, 0.055f, 0.98f)
                    : new Color(0.04f, 0.036f, 0.034f, 0.78f);
                iconImages[i].sprite = hasAbility ? ability.Icon : null;
                iconImages[i].gameObject.SetActive(hasAbility && ability.Icon != null);
            }
        }

        private void EnsureSlotState()
        {
            slotCount = Mathf.Max(DefaultSlotCount, slotCount);
            slots ??= new List<MMOActionBarSlot>();
            while (slots.Count < slotCount)
            {
                slots.Add(new MMOActionBarSlot());
            }
        }

        private static string Shorten(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value.Length <= 11 ? value : value[..11];
        }

        private static string GetKeyLabel(Key key)
        {
            string value = key.ToString();
            return value.StartsWith("Digit") ? value["Digit".Length..] : value;
        }

        private static Text CreateText(string objectName, Transform parent, int fontSize, FontStyle style, TextAnchor alignment)
        {
            GameObject child = new(objectName, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            Text text = child.AddComponent<Text>();
            text.font = GetFont(fontSize);
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.supportRichText = false;
            return text;
        }

        private static Font GetFont(int size)
        {
            if (cachedFont != null)
            {
                return cachedFont;
            }

            cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (cachedFont == null)
            {
                cachedFont = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Segoe UI", "Liberation Sans" }, size);
            }

            return cachedFont;
        }
    }
}
