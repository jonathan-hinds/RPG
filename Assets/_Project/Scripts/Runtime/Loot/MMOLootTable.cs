using System.Collections.Generic;
using RPGClone.Inventory;
using RPGClone.Quests;
using UnityEngine;

namespace RPGClone.Loot
{
    [CreateAssetMenu(menuName = "RPG Clone/Loot/Loot Table", fileName = "LootTable")]
    public sealed class MMOLootTable : ScriptableObject
    {
        [SerializeField] private List<MMOLootTableEntry> entries = new();

        public IReadOnlyList<MMOLootTableEntry> Entries => entries;

        public List<MMOItemStack> GenerateLoot()
        {
            return GenerateLoot(null);
        }

        public List<MMOItemStack> GenerateLoot(GameObject looter)
        {
            List<MMOItemStack> generatedLoot = new();
            MMOQuestLog questLog = looter != null ? looter.GetComponent<MMOQuestLog>() : null;
            foreach (MMOLootTableEntry entry in entries)
            {
                if (entry == null || entry.Item == null || !CanGenerateEntry(entry, questLog) || Random.value > entry.DropChance)
                {
                    continue;
                }

                int quantity = Random.Range(entry.MinQuantity, entry.MaxQuantity + 1);
                if (quantity > 0)
                {
                    generatedLoot.Add(new MMOItemStack(entry.Item, quantity));
                }
            }

            return generatedLoot;
        }

        private static bool CanGenerateEntry(MMOLootTableEntry entry, MMOQuestLog questLog)
        {
            if (entry.RequiredQuest == null)
            {
                return true;
            }

            return questLog != null
                && (!entry.OnlyDropWhileQuestNeedsItem || questLog.NeedsQuestItem(entry.RequiredQuest, entry.Item));
        }

        public void Configure(IEnumerable<MMOLootTableEntry> newEntries)
        {
            entries = newEntries != null ? new List<MMOLootTableEntry>(newEntries) : new List<MMOLootTableEntry>();
        }
    }
}
