using System.Collections.Generic;
using RPGClone.Inventory;

namespace RPGClone.Loot
{
    public interface IMMOLootSource
    {
        string DisplayName { get; }
        IReadOnlyList<MMOItemStack> Loot { get; }
        bool HasLoot { get; }
        bool TryLootToInventory(MMOInventoryContainer inventory);
        bool TryLootStackToInventory(int index, MMOInventoryContainer inventory);
    }
}
