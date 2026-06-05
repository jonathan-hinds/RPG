using System;
using RPGClone.Inventory;
using RPGClone.Quests;
using UnityEngine;

namespace RPGClone.Loot
{
    [Serializable]
    public sealed class MMOLootTableEntry
    {
        [SerializeField] private MMOItemDefinition item;
        [SerializeField, Range(0f, 1f)] private float dropChance = 0.25f;
        [SerializeField, Min(1)] private int minQuantity = 1;
        [SerializeField, Min(1)] private int maxQuantity = 1;
        [Header("Quest Conditions")]
        [SerializeField] private MMOQuestDefinition requiredQuest;
        [SerializeField] private bool onlyDropWhileQuestNeedsItem;

        public MMOItemDefinition Item => item;
        public float DropChance => Mathf.Clamp01(dropChance);
        public int MinQuantity => Mathf.Max(1, minQuantity);
        public int MaxQuantity => Mathf.Max(MinQuantity, maxQuantity);
        public MMOQuestDefinition RequiredQuest => requiredQuest;
        public bool OnlyDropWhileQuestNeedsItem => onlyDropWhileQuestNeedsItem;

        public MMOLootTableEntry()
        {
        }

        public MMOLootTableEntry(MMOItemDefinition item, float dropChance, int minQuantity, int maxQuantity)
        {
            Configure(item, dropChance, minQuantity, maxQuantity);
        }

        public MMOLootTableEntry(MMOItemDefinition item, float dropChance, int minQuantity, int maxQuantity, MMOQuestDefinition requiredQuest, bool onlyDropWhileQuestNeedsItem)
        {
            Configure(item, dropChance, minQuantity, maxQuantity, requiredQuest, onlyDropWhileQuestNeedsItem);
        }

        public void Configure(MMOItemDefinition newItem, float newDropChance, int newMinQuantity, int newMaxQuantity, MMOQuestDefinition newRequiredQuest = null, bool newOnlyDropWhileQuestNeedsItem = false)
        {
            item = newItem;
            dropChance = Mathf.Clamp01(newDropChance);
            minQuantity = Mathf.Max(1, newMinQuantity);
            maxQuantity = Mathf.Max(minQuantity, newMaxQuantity);
            requiredQuest = newRequiredQuest;
            onlyDropWhileQuestNeedsItem = newOnlyDropWhileQuestNeedsItem;
        }
    }
}
