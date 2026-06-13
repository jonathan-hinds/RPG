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

            bool hasStandardWindow = TryGetComponent(out MMOStandardWindow _);
            if (!hasStandardWindow && transform.childCount > 0)
            {
                MMOUiFactory.DestroyChildren(transform);
            }

            RectTransform root = (RectTransform)transform;
            if (root.sizeDelta == Vector2.zero)
            {
                root.sizeDelta = new Vector2(480f, 520f);
            }

            MMOStandardWindow window = MMOStandardWindow.Ensure(gameObject, "Character", () => gameObject.SetActive(false));
            RectTransform content = window.ContentRoot;

            RectTransform paperDoll = window.FindRect("Paper Doll");
            if (paperDoll == null)
            {
                paperDoll = MMOUiFactory.CreateRect("Paper Doll", content);
            }

            paperDoll.anchorMin = new Vector2(0.5f, 0.5f);
            paperDoll.anchorMax = new Vector2(0.5f, 0.5f);
            paperDoll.pivot = new Vector2(0.5f, 0.5f);
            paperDoll.anchoredPosition = new Vector2(0f, 52f);
            paperDoll.sizeDelta = new Vector2(168f, 250f);

            if (paperDoll.Find("Portrait") == null)
            {
                Image portrait = MMOUiFactory.CreateImage("Portrait", paperDoll, new Color(0.09f, 0.07f, 0.052f, 0.92f), false);
                MMOUiFactory.Stretch(portrait.rectTransform);
            }

            nameText = window.FindText("Name") ?? MMOUiFactory.CreateText("Name", paperDoll, 17, FontStyle.Bold, TextAnchor.UpperCenter);
            nameText.rectTransform.anchorMin = new Vector2(0f, 1f);
            nameText.rectTransform.anchorMax = new Vector2(1f, 1f);
            nameText.rectTransform.pivot = new Vector2(0.5f, 1f);
            nameText.rectTransform.anchoredPosition = new Vector2(0f, -12f);
            nameText.rectTransform.sizeDelta = new Vector2(0f, 28f);

            levelText = window.FindText("Level") ?? MMOUiFactory.CreateText("Level", paperDoll, 13, FontStyle.Bold, TextAnchor.UpperCenter);
            levelText.rectTransform.anchorMin = new Vector2(0f, 1f);
            levelText.rectTransform.anchorMax = new Vector2(1f, 1f);
            levelText.rectTransform.pivot = new Vector2(0.5f, 1f);
            levelText.rectTransform.anchoredPosition = new Vector2(0f, -40f);
            levelText.rectTransform.sizeDelta = new Vector2(0f, 22f);

            statsText = window.FindText("Stats") ?? MMOUiFactory.CreateText("Stats", content, 12, FontStyle.Normal, TextAnchor.UpperLeft);
            statsText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            statsText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            statsText.rectTransform.pivot = new Vector2(0.5f, 0f);
            statsText.rectTransform.anchoredPosition = new Vector2(0f, 14f);
            statsText.rectTransform.sizeDelta = new Vector2(280f, 150f);

            leftSlots = window.FindRect("Left Slots") ?? CreateSlotColumn(content, "Left Slots", new Vector2(18f, -58f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            rightSlots = window.FindRect("Right Slots") ?? CreateSlotColumn(content, "Right Slots", new Vector2(-18f, -58f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            bottomSlots = window.FindRect("Bottom Slots") ?? MMOUiFactory.CreateRect("Bottom Slots", content);
            bottomSlots.anchorMin = new Vector2(0.5f, 0f);
            bottomSlots.anchorMax = new Vector2(0.5f, 0f);
            bottomSlots.pivot = new Vector2(0.5f, 0f);
            bottomSlots.anchoredPosition = new Vector2(0f, 168f);
            bottomSlots.sizeDelta = new Vector2(198f, 62f);
        }

        private RectTransform CreateSlotColumn(Transform parent, string objectName, Vector2 position, Vector2 anchor, Vector2 pivot)
        {
            RectTransform column = MMOUiFactory.CreateRect(objectName, parent);
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
            statsText.text = character != null ? FormatStats(character, equipment) : string.Empty;

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

        private static string FormatStats(MMOCharacterIdentity character, MMOCharacterEquipment equipment)
        {
            MMOCharacterStats stats = character != null ? character.Stats : null;
            if (stats == null)
            {
                return string.Empty;
            }

            MMOWeaponSnapshot weapon = MMOCombatResolver.GetWeaponSnapshot(character);
            int blockValue = MMOCombatResolver.GetBlockValue(character);
            string weaponLine = $"Damage {weapon.MinDamage:0}-{weapon.MaxDamage:0}\nSpeed {MMOCombatResolver.GetAttackSpeed(character):0.00}";
            string blockLine = blockValue > 0 ? $"\nBlock {blockValue}" : string.Empty;
            return $"Stamina {stats.Stamina}\nStrength {stats.Strength}\nAgility {stats.Agility}\nIntellect {stats.Intellect}\nSpirit {stats.Spirit}\nArmor {stats.Armor}\nAttack Power {stats.AttackPower}\nSpell Power {stats.SpellPower}\n{weaponLine}{blockLine}\nCrit {stats.CriticalStrikeChance:0.0}%\nDodge {stats.DodgeChance:0.0}%";
        }
    }
}
