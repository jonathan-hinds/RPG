using System.Collections.Generic;
using RPGClone.Characters;
using RPGClone.Combat;
using RPGClone.Inventory;
using UnityEngine;
using UnityEngine.UI;

namespace RPGClone.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class MMOCharacterPanelPresenter : MonoBehaviour
    {
        [SerializeField] private bool autoBuild = true;
        [SerializeField] private MMOCharacterIdentity character;
        [SerializeField] private MMOCharacterEquipment equipment;

        private Text nameText;
        private Text levelText;
        private Text baseStatsValuesText;
        private Text combatStatsValuesText;
        private RectTransform leftSlots;
        private RectTransform rightSlots;
        private RectTransform bottomSlots;

        private static readonly Vector2 EquipmentSlotSize = new(42f, 42f);
        private static readonly Vector2 WeaponSlotSize = new(42f, 42f);
        private const float EquipmentSlotSpacing = 6f;
        private const float WeaponSlotSpacing = 8f;

        private static readonly MMOEquipmentSlotType[] LeftColumnSlots =
        {
            MMOEquipmentSlotType.Head,
            MMOEquipmentSlotType.Neck,
            MMOEquipmentSlotType.Shoulders,
            MMOEquipmentSlotType.Shirt,
            MMOEquipmentSlotType.Chest,
            MMOEquipmentSlotType.Waist,
            MMOEquipmentSlotType.Legs,
            MMOEquipmentSlotType.Feet
        };

        private static readonly MMOEquipmentSlotType[] RightColumnSlots =
        {
            MMOEquipmentSlotType.Wrists,
            MMOEquipmentSlotType.Hands,
            MMOEquipmentSlotType.Finger1,
            MMOEquipmentSlotType.Finger2,
            MMOEquipmentSlotType.Trinket1,
            MMOEquipmentSlotType.Trinket2,
            MMOEquipmentSlotType.Back,
            MMOEquipmentSlotType.Tabard
        };

        private static readonly MMOEquipmentSlotType[] BottomSlots =
        {
            MMOEquipmentSlotType.MainHand,
            MMOEquipmentSlotType.OffHand,
            MMOEquipmentSlotType.Ranged
        };

        private void Awake()
        {
            ResolveReferences();
            if (autoBuild)
            {
                BuildIfNeeded();
            }

            Refresh();
        }

        private void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void Configure(MMOCharacterIdentity newCharacter, MMOCharacterEquipment newEquipment)
        {
            Unsubscribe();
            character = newCharacter;
            equipment = newEquipment;
            BuildIfNeeded();
            Refresh();
            Subscribe();
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
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                return;
            }

            if (character == null)
            {
                character = player.GetComponent<MMOCharacterIdentity>();
            }

            if (equipment == null)
            {
                equipment = player.GetComponent<MMOCharacterEquipment>();
            }
        }

        private void Subscribe()
        {
            if (character != null)
            {
                character.Changed -= OnCharacterChanged;
                character.Changed += OnCharacterChanged;
            }

            if (equipment != null)
            {
                equipment.Changed -= OnEquipmentChanged;
                equipment.Changed += OnEquipmentChanged;
            }
        }

        private void Unsubscribe()
        {
            if (character != null)
            {
                character.Changed -= OnCharacterChanged;
            }

            if (equipment != null)
            {
                equipment.Changed -= OnEquipmentChanged;
            }
        }

        private void OnCharacterChanged(MMOCharacterIdentity changedCharacter)
        {
            Refresh();
        }

        private void OnEquipmentChanged(MMOCharacterEquipment changedEquipment)
        {
            Refresh();
        }

        private void BuildIfNeeded()
        {
            if (nameText != null)
            {
                return;
            }

            bool hasAuthoredLayout = MMOStandardWindow.HasAuthoredWindowLayout(gameObject);
            if (!hasAuthoredLayout && transform.childCount > 0)
            {
                MMOUiFactory.DestroyChildren(transform);
            }

            RectTransform root = (RectTransform)transform;
            if (!hasAuthoredLayout)
            {
                MMOStandardWindow.ApplySecondaryPlacement(root);
            }

            MMOStandardWindow window = MMOStandardWindow.Ensure(gameObject, "Character", () => gameObject.SetActive(false));
            RectTransform content = window.ContentRoot;

            RectTransform paperDoll = window.FindRect("Paper Doll");
            if (paperDoll == null)
            {
                paperDoll = MMOUiFactory.CreateRect("Paper Doll", content);
                paperDoll.anchorMin = new Vector2(0.5f, 0.5f);
                paperDoll.anchorMax = new Vector2(0.5f, 0.5f);
                paperDoll.pivot = new Vector2(0.5f, 0.5f);
                paperDoll.anchoredPosition = new Vector2(0f, 52f);
                paperDoll.sizeDelta = new Vector2(168f, 250f);
            }

            if (paperDoll.Find("Portrait") == null)
            {
                Image portrait = MMOUiFactory.CreateImage("Portrait", paperDoll, new Color(0.09f, 0.07f, 0.052f, 0.92f), false);
                MMOUiFactory.Stretch(portrait.rectTransform);
            }

            nameText = window.FindText("Name");
            if (nameText == null)
            {
                nameText = MMOUiFactory.CreateText("Name", paperDoll, 17, FontStyle.Bold, TextAnchor.UpperCenter);
                nameText.rectTransform.anchorMin = new Vector2(0f, 1f);
                nameText.rectTransform.anchorMax = new Vector2(1f, 1f);
                nameText.rectTransform.pivot = new Vector2(0.5f, 1f);
                nameText.rectTransform.anchoredPosition = new Vector2(0f, -12f);
                nameText.rectTransform.sizeDelta = new Vector2(0f, 28f);
            }

            levelText = window.FindText("Level");
            if (levelText == null)
            {
                levelText = MMOUiFactory.CreateText("Level", paperDoll, 13, FontStyle.Bold, TextAnchor.UpperCenter);
                levelText.rectTransform.anchorMin = new Vector2(0f, 1f);
                levelText.rectTransform.anchorMax = new Vector2(1f, 1f);
                levelText.rectTransform.pivot = new Vector2(0.5f, 1f);
                levelText.rectTransform.anchoredPosition = new Vector2(0f, -40f);
                levelText.rectTransform.sizeDelta = new Vector2(0f, 22f);
            }

            baseStatsValuesText = window.FindText("Base Stats Values") ?? window.FindText("Stats");
            if (baseStatsValuesText == null)
            {
                baseStatsValuesText = MMOUiFactory.CreateText("Base Stats Values", content, 10, FontStyle.Normal, TextAnchor.UpperRight);
                baseStatsValuesText.rectTransform.anchorMin = new Vector2(0f, 0f);
                baseStatsValuesText.rectTransform.anchorMax = new Vector2(0f, 0f);
                baseStatsValuesText.rectTransform.pivot = new Vector2(0f, 0f);
                baseStatsValuesText.rectTransform.anchoredPosition = new Vector2(145f, 28f);
                baseStatsValuesText.rectTransform.sizeDelta = new Vector2(48f, 96f);
            }

            combatStatsValuesText = window.FindText("Combat Stats Values") ?? window.FindText("Combat Stats");
            if (combatStatsValuesText == null)
            {
                combatStatsValuesText = MMOUiFactory.CreateText("Combat Stats Values", content, 10, FontStyle.Normal, TextAnchor.UpperRight);
                combatStatsValuesText.rectTransform.anchorMin = new Vector2(1f, 0f);
                combatStatsValuesText.rectTransform.anchorMax = new Vector2(1f, 0f);
                combatStatsValuesText.rectTransform.pivot = new Vector2(1f, 0f);
                combatStatsValuesText.rectTransform.anchoredPosition = new Vector2(-24f, 28f);
                combatStatsValuesText.rectTransform.sizeDelta = new Vector2(58f, 96f);
            }

            leftSlots = window.FindRect("Left Slots") ?? CreateSlotColumn(content, "Left Slots", new Vector2(10f, -18f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            rightSlots = window.FindRect("Right Slots") ?? CreateSlotColumn(content, "Right Slots", new Vector2(-10f, -18f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            bottomSlots = window.FindRect("Bottom Slots");
            if (bottomSlots == null)
            {
                bottomSlots = MMOUiFactory.CreateRect("Bottom Slots", content);
                bottomSlots.anchorMin = new Vector2(0.5f, 1f);
                bottomSlots.anchorMax = new Vector2(0.5f, 1f);
                bottomSlots.pivot = new Vector2(0.5f, 1f);
                bottomSlots.anchoredPosition = new Vector2(0f, -318f);
                bottomSlots.sizeDelta = new Vector2(142f, 42f);
            }
        }

        private RectTransform CreateSlotColumn(Transform parent, string objectName, Vector2 position, Vector2 anchor, Vector2 pivot)
        {
            RectTransform column = MMOUiFactory.CreateRect(objectName, parent);
            column.anchorMin = anchor;
            column.anchorMax = anchor;
            column.pivot = pivot;
            column.anchoredPosition = position;
            column.sizeDelta = new Vector2(EquipmentSlotSize.x, LeftColumnSlots.Length * EquipmentSlotSize.y + (LeftColumnSlots.Length - 1) * EquipmentSlotSpacing);
            return column;
        }

        private void Refresh()
        {
            BuildIfNeeded();

            nameText.text = character != null ? character.DisplayName : "Character";
            levelText.text = character != null ? $"Level {character.Level}" : string.Empty;
            baseStatsValuesText.text = character != null ? FormatBaseStatValues(character) : string.Empty;
            combatStatsValuesText.text = character != null ? FormatCombatStatValues(character) : string.Empty;

            MMOUiFactory.DestroyChildren(leftSlots);
            MMOUiFactory.DestroyChildren(rightSlots);
            MMOUiFactory.DestroyChildren(bottomSlots);

            IReadOnlyList<MMOEquipmentSlotType> slots = equipment != null
                ? equipment.EquipmentSlots
                : MMOCharacterEquipment.GetDefaultSlots();

            CreateSlotGroup(leftSlots, slots, LeftColumnSlots, false);
            CreateSlotGroup(rightSlots, slots, RightColumnSlots, false);
            CreateSlotGroup(bottomSlots, slots, BottomSlots, true);
        }

        private void CreateSlotGroup(Transform parent, IReadOnlyList<MMOEquipmentSlotType> availableSlots, IReadOnlyList<MMOEquipmentSlotType> orderedSlots, bool horizontal)
        {
            int visibleIndex = 0;
            for (int i = 0; i < orderedSlots.Count; i++)
            {
                MMOEquipmentSlotType slotType = orderedSlots[i];
                if (!HasSlot(availableSlots, slotType))
                {
                    continue;
                }

                CreateEquipmentSlot(parent, slotType, visibleIndex, horizontal);
                visibleIndex++;
            }
        }

        private void CreateEquipmentSlot(Transform parent, MMOEquipmentSlotType slotType, int localIndex, bool horizontal)
        {
            Image slot = MMOUiFactory.CreateImage(MMOUiFactory.FormatEnumLabel(slotType), parent, new Color(0.045f, 0.04f, 0.036f, 0.94f));
            RectTransform rectTransform = slot.rectTransform;
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.anchoredPosition = horizontal
                ? new Vector2(localIndex * (WeaponSlotSize.x + WeaponSlotSpacing), 0f)
                : new Vector2(0f, -localIndex * (EquipmentSlotSize.y + EquipmentSlotSpacing));
            rectTransform.sizeDelta = horizontal ? WeaponSlotSize : EquipmentSlotSize;

            Outline outline = slot.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.42f, 0.35f, 0.23f, 0.9f);
            outline.effectDistance = new Vector2(1f, -1f);

            Text label = MMOUiFactory.CreateText("Label", rectTransform, 10, FontStyle.Bold, TextAnchor.MiddleCenter);
            MMOItemDefinition equippedItem = equipment != null ? equipment.GetEquippedItem(slotType) : null;
            label.text = equippedItem != null ? string.Empty : FormatSlotLabel(slotType, horizontal);
            label.color = new Color(0.78f, 0.7f, 0.52f, 1f);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 6;
            label.resizeTextMaxSize = 8;
            MMOUiFactory.Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(4f, 2f);
            label.rectTransform.offsetMax = new Vector2(-4f, -2f);

            if (equippedItem != null)
            {
                slot.color = MMOItemIconView.GetSlotBackgroundColor(equippedItem);
                MMOItemIconView.AddToSlot(rectTransform, equippedItem);
            }
        }

        private static bool HasSlot(IReadOnlyList<MMOEquipmentSlotType> slots, MMOEquipmentSlotType slotType)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] == slotType)
                {
                    return true;
                }
            }

            return false;
        }

        private static string FormatSlotLabel(MMOEquipmentSlotType slotType, bool compact)
        {
            return slotType switch
            {
                MMOEquipmentSlotType.Shoulders => "Shldr",
                MMOEquipmentSlotType.Finger1 or MMOEquipmentSlotType.Finger2 => "Ring",
                MMOEquipmentSlotType.Trinket1 or MMOEquipmentSlotType.Trinket2 => "Trink",
                MMOEquipmentSlotType.MainHand => "Main",
                MMOEquipmentSlotType.OffHand => "Off",
                MMOEquipmentSlotType.Ranged => "Range",
                _ => MMOUiFactory.FormatEnumLabel(slotType)
            };
        }

        private static string FormatBaseStatValues(MMOCharacterIdentity character)
        {
            MMOCharacterStats stats = character != null ? character.Stats : null;
            if (stats == null)
            {
                return string.Empty;
            }

            return $"{stats.Strength}\n{stats.Agility}\n{stats.Stamina}\n{stats.Intellect}\n{stats.Spirit}\n{stats.Armor}";
        }

        private static string FormatCombatStatValues(MMOCharacterIdentity character)
        {
            MMOCharacterStats stats = character != null ? character.Stats : null;
            if (stats == null)
            {
                return string.Empty;
            }

            MMOWeaponSnapshot weapon = MMOCombatResolver.GetWeaponSnapshot(character);
            int blockValue = MMOCombatResolver.GetBlockValue(character);
            string blockLine = blockValue > 0 ? $"\n{blockValue}" : "\n0";
            return $"{stats.AttackPower}\n{weapon.MinDamage:0}-{weapon.MaxDamage:0}\n{MMOCombatResolver.GetAttackSpeed(character):0.00}\n{stats.SpellPower}\n{stats.CriticalStrikeChance:0.0}%\n{stats.DodgeChance:0.0}%{blockLine}";
        }
    }
}
