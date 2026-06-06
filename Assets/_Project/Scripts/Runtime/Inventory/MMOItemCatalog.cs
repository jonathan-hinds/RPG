using System.Collections.Generic;
using UnityEngine;

namespace RPGClone.Inventory
{
    [CreateAssetMenu(menuName = "RPG Clone/Inventory/Item Catalog", fileName = "ItemCatalog")]
    public sealed class MMOItemCatalog : ScriptableObject
    {
        [SerializeField] private List<MMOItemDefinition> items = new();

        public IReadOnlyList<MMOItemDefinition> Items => items;

        public void Configure(IEnumerable<MMOItemDefinition> newItems)
        {
            items = newItems != null ? new List<MMOItemDefinition>(newItems) : new List<MMOItemDefinition>();
        }

        public MMOItemDefinition FindById(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return null;
            }

            foreach (MMOItemDefinition item in items)
            {
                if (item != null && item.ItemId == itemId)
                {
                    return item;
                }
            }

            return null;
        }
    }
}
