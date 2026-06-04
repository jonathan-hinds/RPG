using System.Collections.Generic;
using RPGClone.Inventory;
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
            List<MMOItemStack> generatedLoot = new();
            foreach (MMOLootTableEntry entry in entries)
            {
                if (entry == null || entry.Item == null || Random.value > entry.DropChance)
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

        public void Configure(IEnumerable<MMOLootTableEntry> newEntries)
        {
            entries = newEntries != null ? new List<MMOLootTableEntry>(newEntries) : new List<MMOLootTableEntry>();
        }
    }
}
