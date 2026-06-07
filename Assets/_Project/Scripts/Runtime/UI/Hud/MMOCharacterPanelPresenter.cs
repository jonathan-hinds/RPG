using System.Collections.Generic;
using RPGClone.Characters;
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
        private Text statsText;
        private RectTransform leftSlots;
        private RectTransform rightSlots;
        private RectTransform bottomSlots;

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

            MMOUiFactory.DestroyChildren(transform);

            RectTransform root = (RectTransform)transform;
            root.sizeDelta = new Vector2(480f, 520f);

            Image background = gameObject.GetComponent<Image>();
            if (background == null)
            {
                background = gameObject.AddComponent<Image>();
            }

            background.color = new Color(0.035f, 0.032f, 0.028f, 0.96f);

            Text title = MMOUiFactory.CreateText("Title", transform, 18, FontStyle.Bold, TextAnchor.MiddleLeft);
            title.text = "Character";
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(14f, -10f);
            title.rectTransform.sizeDelta = new Vector2(-28f, 28f);

            Button closeButton = MMOUiFactory.CreateTextButton("Close", transform, "X", new Vector2(26f, 24f), new Color(0.12f, 0.09f, 0.07f, 0.95f));
            closeButton.onClick.AddListener(() => gameObject.SetActive(false));
            RectTransform closeRect = closeButton.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-10f, -10f);

            RectTransform paperDoll = MMOUiFactory.CreateRect("Paper Doll", transform);
            paperDoll.anchorMin = new Vector2(0.5f, 0.5f);
            paperDoll.anchorMax = new Vector2(0.5f, 0.5f);
            paperDoll.pivot = new Vector2(0.5f, 0.5f);
            paperDoll.anchoredPosition = new Vector2(0f, 52f);
            paperDoll.sizeDelta = new Vector2(168f, 250f);

            Image portrait = MMOUiFactory.CreateImage("Portrait", paperDoll, new Color(0.09f, 0.07f, 0.052f, 0.92f), false);
            MMOUiFactory.Stretch(portrait.rectTransform);

            nameText = MMOUiFactory.CreateText("Name", paperDoll, 17, FontStyle.Bold, TextAnchor.UpperCenter);
            nameText.rectTransform.anchorMin = new Vector2(0f, 1f);
            nameText.rectTransform.anchorMax = new Vector2(1f, 1f);
            nameText.rectTransform.pivot = new Vector2(0.5f, 1f);
            nameText.rectTransform.anchoredPosition = new Vector2(0f, -12f);
            nameText.rectTransform.sizeDelta = new Vector2(0f, 28f);

            levelText = MMOUiFactory.CreateText("Level", paperDoll, 13, FontStyle.Bold, TextAnchor.UpperCenter);
            levelText.rectTransform.anchorMin = new Vector2(0f, 1f);
            levelText.rectTransform.anchorMax = new Vector2(1f, 1f);
            levelText.rectTransform.pivot = new Vector2(0.5f, 1f);
            levelText.rectTransform.anchoredPosition = new Vector2(0f, -40f);
            levelText.rectTransform.sizeDelta = new Vector2(0f, 22f);

            statsText = MMOUiFactory.CreateText("Stats", transform, 12, FontStyle.Normal, TextAnchor.UpperLeft);
            statsText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            statsText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            statsText.rectTransform.pivot = new Vector2(0.5f, 0f);
            statsText.rectTransform.anchoredPosition = new Vector2(0f, 14f);
            statsText.rectTransform.sizeDelta = new Vector2(280f, 150f);

            leftSlots = CreateSlotColumn("Left Slots", new Vector2(18f, -58f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            rightSlots = CreateSlotColumn("Right Slots", new Vector2(-18f, -58f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            bottomSlots = MMOUiFactory.CreateRect("Bottom Slots", transform);
            bottomSlots.anchorMin = new Vector2(0.5f, 0f);
            bottomSlots.anchorMax = new Vector2(0.5f, 0f);
            bottomSlots.pivot = new Vector2(0.5f, 0f);
            bottomSlots.anchoredPosition = new Vector2(0f, 168f);
            bottomSlots.sizeDelta = new Vector2(198f, 62f);
        }

        private RectTransform CreateSlotColumn(string objectName, Vector2 position, Vector2 anchor, Vector2 pivot)
        {
            RectTransform column = MMOUiFactory.CreateRect(objectName, transform);
            column.anchorMin = anchor;
            column.anchorMax = anchor;
            column.pivot = pivot;
            column.anchoredPosition = position;
            column.sizeDelta = new Vector2(120f, 360f);
            return column;
        }

        private void Refresh()
        {
            BuildIfNeeded();

            nameText.text = character != null ? character.DisplayName : "Character";
            levelText.text = character != null ? $"Level {character.Level}" : string.Empty;
            statsText.text = character != null ? FormatStats(character.Stats) : string.Empty;

            MMOUiFactory.DestroyChildren(leftSlots);
            MMOUiFactory.DestroyChildren(rightSlots);
            MMOUiFactory.DestroyChildren(bottomSlots);

            IReadOnlyList<MMOEquipmentSlotType> slots = equipment != null
                ? equipment.EquipmentSlots
                : MMOCharacterEquipment.GetDefaultSlots();

            for (int i = 0; i < slots.Count; i++)
            {
                Transform parent = i < 8 ? leftSlots : i < 16 ? rightSlots : bottomSlots;
                int localIndex = i < 8 ? i : i < 16 ? i - 8 : i - 16;
                CreateEquipmentSlot(parent, slots[i], localIndex, i >= 16);
            }
        }

        private void CreateEquipmentSlot(Transform parent, MMOEquipmentSlotType slotType, int localIndex, bool horizontal)
        {
            Image slot = MMOUiFactory.CreateImage(MMOUiFactory.FormatEnumLabel(slotType), parent, new Color(0.045f, 0.04f, 0.036f, 0.94f));
            RectTransform rectTransform = slot.rectTransform;
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.anchoredPosition = horizontal ? new Vector2(localIndex * 66f, 0f) : new Vector2(0f, -localIndex * 44f);
            rectTransform.sizeDelta = horizontal ? new Vector2(62f, 58f) : new Vector2(120f, 38f);

            Text label = MMOUiFactory.CreateText("Label", rectTransform, 10, FontStyle.Bold, TextAnchor.MiddleCenter);
            MMOItemDefinition equippedItem = equipment != null ? equipment.GetEquippedItem(slotType) : null;
            label.text = equippedItem != null ? string.Empty : MMOUiFactory.FormatEnumLabel(slotType);
            label.color = new Color(0.78f, 0.7f, 0.52f, 1f);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 6;
            label.resizeTextMaxSize = 10;
            MMOUiFactory.Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(4f, 2f);
            label.rectTransform.offsetMax = new Vector2(-4f, -2f);

            if (equippedItem != null)
            {
                slot.color = MMOItemIconView.GetSlotBackgroundColor(equippedItem);
                MMOItemIconView.AddToSlot(rectTransform, equippedItem);
            }
        }

        private static string FormatStats(MMOCharacterStats stats)
        {
            if (stats == null)
            {
                return string.Empty;
            }

            return $"Stamina {stats.Stamina}\nStrength {stats.Strength}\nAgility {stats.Agility}\nIntellect {stats.Intellect}\nSpirit {stats.Spirit}\nArmor {stats.Armor}\nAttack Power {stats.AttackPower}\nSpell Power {stats.SpellPower}\nCrit {stats.CriticalStrikeChance:0.0}%\nDodge {stats.DodgeChance:0.0}%\nHealth Regen {stats.HealthRegenPerSecond:0.0}/s\nMana Regen {stats.ManaRegenPerSecond:0.0}/s";
        }
    }
}
