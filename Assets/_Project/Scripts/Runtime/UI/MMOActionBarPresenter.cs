using System.Collections.Generic;
using RPGClone.Abilities;
using RPGClone.Characters;
using RPGClone.Combat;
using RPGClone.Inventory;
using RPGClone.Services;
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
        [SerializeField] private MMOInventoryContainer inventory;
        [SerializeField] private MMOAutoAttackController autoAttackController;
        [SerializeField] private MMOTargetSelectionController targetSelectionController;
        [SerializeField] private MMOGroundTargetingController groundTargetingController;
        [SerializeField] private List<MMOActionBarSlot> slots = new();

        private readonly List<Button> buttons = new();
        private readonly List<MMOSlotView> sharedSlotViews = new();
        private readonly List<MMOActionBarSlotView> slotViews = new();
        private readonly List<MMOAbilityTooltipTrigger> tooltipTriggers = new();

        public IReadOnlyList<MMOActionBarSlot> Slots => slots;

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

            UpdateCooldowns();
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
            if (abilitySystem != null)
            {
                inventory = abilitySystem.GetComponent<MMOInventoryContainer>();
            }

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
            MMOItemDefinition item = slots[index].item;
            if (ability == null && item == null)
            {
                return;
            }

            if (item != null)
            {
                MMOInventoryItemUseService.TryUseItem(inventory, item);
                return;
            }

            MMOCharacterIdentity target = targetSelectionController != null ? targetSelectionController.CurrentTarget : null;
            if (ability.IsAutoAttack && autoAttackController != null)
            {
                autoAttackController.ToggleAutoAttack(target);
                return;
            }

            if (ability.RequiresGroundTarget)
            {
                ResolveGroundTargetingController();
                groundTargetingController?.BeginTargeting(abilitySystem, ability);
                return;
            }

            abilitySystem?.TryUseAbility(ability, target, out _);
        }

        public void FillEmptySlotsFromKnownAbilities()
        {
            if (abilitySystem == null)
            {
                return;
            }

            EnsureSlotState();
            foreach (MMOAbilityDefinition ability in abilitySystem.KnownAbilities)
            {
                if (ability == null || SlotsContain(ability))
                {
                    continue;
                }

                for (int i = 0; i < slots.Count; i++)
                {
                    if (slots[i].ability != null)
                    {
                        continue;
                    }

                    slots[i].SetAbility(ability);
                    break;
                }
            }

            Refresh();
        }

        public void ApplySlots(IReadOnlyList<MMOActionBarSlot> newSlots)
        {
            slots = newSlots != null ? new List<MMOActionBarSlot>(newSlots) : new List<MMOActionBarSlot>();
            slotCount = Mathf.Max(DefaultSlotCount, slots.Count);
            EnsureSlotState();
            BuildIfNeeded();
            Refresh();
        }

        private bool SlotsContain(MMOAbilityDefinition ability)
        {
            foreach (MMOActionBarSlot slot in slots)
            {
                if (slot.bindingType == MMOActionBarSlotBindingType.Ability && slot.ability == ability)
                {
                    return true;
                }
            }

            return false;
        }

        public bool BeginSlotDrag(int index, PointerEventData eventData, Transform owner)
        {
            if (index < 0 || index >= slots.Count)
            {
                return false;
            }

            MMOActionBarSlot slot = slots[index];
            if (slot == null || slot.IsEmpty)
            {
                return false;
            }

            MMOGameTooltipPresenter.HideTooltip();
            string label = slot.bindingType == MMOActionBarSlotBindingType.Item
                ? slot.item.DisplayName
                : slot.ability.DisplayName;
            Sprite icon = slot.bindingType == MMOActionBarSlotBindingType.Item
                ? slot.item.Icon
                : slot.ability.Icon;
            MMOSlotDragPayload payload = slot.bindingType == MMOActionBarSlotBindingType.Item
                ? MMOSlotDragPayload.ActionBarItem(slot.item, this, index)
                : MMOSlotDragPayload.AbilityBinding(slot.ability, this, index);

            return MMOSlotDragState.BeginDrag(
                payload,
                eventData,
                owner,
                label,
                icon);
        }

        public bool CanAcceptDrop(int targetIndex, MMOSlotDragPayload payload)
        {
            return payload.IsValid
                && targetIndex >= 0
                && targetIndex < slots.Count
                && payload.Category is MMOSlotContentCategory.Ability
                    or MMOSlotContentCategory.Item
                    or MMOSlotContentCategory.Equipment
                && (payload.Ability != null || payload.Item != null);
        }

        public void AcceptDrop(int targetIndex, MMOSlotDragPayload payload)
        {
            if (!CanAcceptDrop(targetIndex, payload))
            {
                return;
            }

            if (payload.FromActionBar && payload.SourceActionBar == this)
            {
                if (payload.SourceSlotIndex == targetIndex)
                {
                    return;
                }

                SwapSlotBindings(targetIndex, payload.SourceSlotIndex);
            }
            else
            {
                if (payload.Item != null)
                {
                    slots[targetIndex].SetItem(payload.Item);
                }
                else
                {
                    slots[targetIndex].SetAbility(payload.Ability);
                }
            }

            Refresh();
        }

        private void ResolveReferences()
        {
            if (abilitySystem == null || inventory == null)
            {
                MMOGameplaySessionService.LocalPlayer.TryGetComponent(out MMOAbilitySystem resolvedAbilitySystem);
                MMOGameplaySessionService.LocalPlayer.TryGetComponent(out MMOInventoryContainer resolvedInventory);
                MMOGameplaySessionService.LocalPlayer.TryGetComponent(out MMOAutoAttackController resolvedAutoAttackController);

                abilitySystem ??= resolvedAbilitySystem;
                inventory ??= resolvedInventory;
                autoAttackController ??= resolvedAutoAttackController;
            }

            if (autoAttackController == null)
            {
                MMOGameplaySessionService.LocalPlayer.TryGetComponent(out autoAttackController);
            }

            if (targetSelectionController == null)
            {
                targetSelectionController = FindAnyObjectByType<MMOTargetSelectionController>();
            }

            ResolveGroundTargetingController();
        }

        private void ResolveGroundTargetingController()
        {
            if (groundTargetingController != null)
            {
                return;
            }

            groundTargetingController = FindAnyObjectByType<MMOGroundTargetingController>();
            if (groundTargetingController == null)
            {
                groundTargetingController = gameObject.AddComponent<MMOGroundTargetingController>();
            }
        }

        private void SwapSlotBindings(int firstIndex, int secondIndex)
        {
            if (firstIndex < 0 || firstIndex >= slots.Count || secondIndex < 0 || secondIndex >= slots.Count)
            {
                return;
            }

            MMOActionBarSlot first = slots[firstIndex];
            MMOActionBarSlot second = slots[secondIndex];
            MMOActionBarSlotBindingType firstType = first.bindingType;
            MMOAbilityDefinition firstAbility = first.ability;
            MMOItemDefinition firstItem = first.item;

            first.bindingType = second.bindingType;
            first.ability = second.ability;
            first.item = second.item;

            second.bindingType = firstType;
            second.ability = firstAbility;
            second.item = firstItem;
        }

        private void BuildIfNeeded()
        {
            EnsureSlotState();

            bool hasAuthoredLayout = transform.childCount > 0;
            RectTransform root = (RectTransform)transform;
            if (!hasAuthoredLayout)
            {
                Vector2 requiredSize = new((48f * slotCount) + (4f * (slotCount - 1)) + 12f, 56f);
                root.sizeDelta = new Vector2(
                    Mathf.Max(root.sizeDelta.x, requiredSize.x),
                    Mathf.Max(root.sizeDelta.y, requiredSize.y));
                MMOPanelSkin.ApplyActionBar(gameObject);
            }

            ResolveAuthoredSlotButtons();

            for (int i = 0; i < buttons.Count; i++)
            {
                buttons[i].gameObject.SetActive(i < slotCount);
                if (i < slotViews.Count)
                {
                    slotViews[i].Configure(this, i);
                }
            }
        }

        private void ResolveAuthoredSlotButtons()
        {
            if (buttons.Count > 0)
            {
                return;
            }

            for (int index = 0; index < slotCount; index++)
            {
                Transform existing = transform.Find($"Action Slot {index + 1}");
                if (existing == null)
                {
                    CreateSlotButton(index);
                    continue;
                }

                RegisterSlotButton(existing.gameObject, index, false);
            }
        }

        private void CreateSlotButton(int index)
        {
            GameObject buttonObject = new($"Action Slot {index + 1}", typeof(RectTransform));
            buttonObject.transform.SetParent(transform, false);
            RegisterSlotButton(buttonObject, index, true);
        }

        private void RegisterSlotButton(GameObject buttonObject, int index, bool applyDefaultLayout)
        {
            RectTransform rectTransform = (RectTransform)buttonObject.transform;
            if (applyDefaultLayout)
            {
                rectTransform.anchorMin = new Vector2(0f, 0.5f);
                rectTransform.anchorMax = new Vector2(0f, 0.5f);
                rectTransform.pivot = new Vector2(0f, 0.5f);
                rectTransform.anchoredPosition = new Vector2(6f + index * 52f, 0f);
                rectTransform.sizeDelta = new Vector2(48f, 48f);
            }

            Image background = buttonObject.GetComponent<Image>();
            bool createdBackground = background == null;
            if (createdBackground)
            {
                background = buttonObject.AddComponent<Image>();
            }

            if ((applyDefaultLayout || createdBackground) && background.sprite == null)
            {
                background.color = new Color(1f, 1f, 1f, 0.001f);
            }

            Button button = buttonObject.GetComponent<Button>() ?? buttonObject.AddComponent<Button>();
            button.onClick.RemoveAllListeners();
            int capturedIndex = index;
            button.onClick.AddListener(() => ActivateSlot(capturedIndex));

            MMOActionBarSlotView slotView = buttonObject.GetComponent<MMOActionBarSlotView>()
                ?? buttonObject.AddComponent<MMOActionBarSlotView>();
            slotView.Configure(this, index);
            MMOSlotView sharedSlotView = MMOSlotView.Attach(buttonObject);
            sharedSlotView.Present(MMOSlotPresentation.Empty());
            MMOAbilityTooltipTrigger tooltipTrigger = buttonObject.GetComponent<MMOAbilityTooltipTrigger>()
                ?? buttonObject.AddComponent<MMOAbilityTooltipTrigger>();

            buttons.Add(button);
            sharedSlotViews.Add(sharedSlotView);
            slotViews.Add(slotView);
            tooltipTriggers.Add(tooltipTrigger);
        }

        private void Refresh()
        {
            BuildIfNeeded();
            for (int i = 0; i < slots.Count; i++)
            {
                MMOActionBarSlot slot = slots[i];
                MMOAbilityDefinition ability = slot.ability;
                MMOItemDefinition item = slot.item;
                bool hasAbility = slot.bindingType == MMOActionBarSlotBindingType.Ability && ability != null;
                bool hasItem = slot.bindingType == MMOActionBarSlotBindingType.Item && item != null;
                string keybinding = slots[i].key == Key.None ? string.Empty : GetKeyLabel(slots[i].key);
                bool active = hasAbility
                    && ability.IsAutoAttack
                    && autoAttackController != null
                    && autoAttackController.IsAutoAttacking;
                MMOSlotPresentation presentation;
                if (hasItem)
                {
                    int quantity = inventory != null ? inventory.CountItem(item) : 0;
                    presentation = MMOItemSlotAdapter.Present(item, quantity, false, keybinding);
                }
                else if (hasAbility)
                {
                    presentation = MMOAbilitySlotAdapter.Present(ability, keybinding, active);
                }
                else
                {
                    presentation = new MMOSlotPresentation(secondaryText: keybinding);
                }

                sharedSlotViews[i].Present(presentation);
                buttons[i].interactable = true;
                tooltipTriggers[i].Configure(hasAbility ? ability : null);
                MMOItemTooltipTrigger itemTooltip = buttons[i].GetComponent<MMOItemTooltipTrigger>();
                if (hasItem)
                {
                    MMOItemTooltipTrigger.Bind(buttons[i].gameObject, item);
                }
                else if (itemTooltip != null)
                {
                    itemTooltip.Configure(null);
                }
            }

            UpdateCooldowns();
        }

        private void UpdateCooldowns()
        {
            int count = Mathf.Min(slots.Count, sharedSlotViews.Count);
            for (int i = 0; i < count; i++)
            {
                MMOAbilityDefinition ability = slots[i].ability;
                bool isAutoAttack = ability != null && ability.IsAutoAttack && autoAttackController != null;
                float remaining = abilitySystem == null
                    ? 0f
                    : isAutoAttack
                    ? autoAttackController.GetAutoAttackCooldownRemaining()
                    : abilitySystem.GetCooldownRemaining(ability);
                bool coolingDown = ability != null && remaining > 0f;
                if (!coolingDown)
                {
                    sharedSlotViews[i].SetCooldown(0f, null);
                    continue;
                }

                float normalized = isAutoAttack
                    ? autoAttackController.GetAutoAttackCooldownNormalized()
                    : abilitySystem.GetCooldownNormalized(ability);
                sharedSlotViews[i].SetCooldown(normalized, FormatCooldown(remaining));
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

            foreach (MMOActionBarSlot slot in slots)
            {
                if (slot == null)
                {
                    continue;
                }

                if (slot.bindingType == MMOActionBarSlotBindingType.Empty)
                {
                    if (slot.ability != null)
                    {
                        slot.bindingType = MMOActionBarSlotBindingType.Ability;
                    }
                    else if (slot.item != null)
                    {
                        slot.bindingType = MMOActionBarSlotBindingType.Item;
                    }
                }

                if (slot.bindingType == MMOActionBarSlotBindingType.Ability)
                {
                    slot.item = null;
                }
                else if (slot.bindingType == MMOActionBarSlotBindingType.Item)
                {
                    slot.ability = null;
                }
                else
                {
                    slot.ClearBinding();
                }
            }
        }

        private static string GetKeyLabel(Key key)
        {
            string value = key.ToString();
            return value.StartsWith("Digit") ? value["Digit".Length..] : value;
        }

        private static string FormatCooldown(float seconds)
        {
            return seconds >= 10f ? Mathf.CeilToInt(seconds).ToString() : seconds.ToString("0.0");
        }

    }
}
