using System;
using System.Collections.Generic;
using RPGClone.Inventory;
using UnityEngine;

namespace RPGClone.Loot
{
    [Serializable]
    public sealed class MMOPersonalLootItemState
    {
        public string itemId;
        public int quantity;

        public MMOPersonalLootItemState()
        {
        }

        public MMOPersonalLootItemState(MMOItemStack stack)
        {
            itemId = stack != null && stack.Item != null ? stack.Item.ItemId : string.Empty;
            quantity = stack != null ? Mathf.Max(0, stack.Quantity) : 0;
        }
    }

    [Serializable]
    public sealed class MMOPersonalLootState
    {
        public string characterId;
        public string participantId;
        public List<MMOPersonalLootItemState> items = new();
        public bool looted;

        public bool HasLoot
        {
            get
            {
                if (looted)
                {
                    return false;
                }

                foreach (MMOPersonalLootItemState item in items)
                {
                    if (item != null && !string.IsNullOrWhiteSpace(item.itemId) && item.quantity > 0)
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }

    [Serializable]
    public sealed class MMOCorpseLootState
    {
        public string sessionId;
        public string corpseId;
        public string enemySpawnId;
        public List<MMOPersonalLootState> personalLoot = new();
        public long updatedUtcTicks;

        public bool HasAnyUnlootedItems()
        {
            foreach (MMOPersonalLootState state in personalLoot)
            {
                if (state != null && state.HasLoot)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
