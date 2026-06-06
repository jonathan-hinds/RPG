using System;
using RPGClone.Inventory;
using UnityEngine;

namespace RPGClone.Vendors
{
    [Serializable]
    public sealed class MMOVendorStockEntry
    {
        [SerializeField] private MMOItemDefinition item;
        [SerializeField, Min(1)] private int quantity = 1;
        [SerializeField, Min(0)] private int priceCopper;

        public MMOItemDefinition Item => item;
        public int Quantity => Mathf.Max(1, quantity);
        public int PriceCopper => Mathf.Max(0, priceCopper);
        public bool IsValid => item != null && quantity > 0;

        public MMOVendorStockEntry()
        {
        }

        public MMOVendorStockEntry(MMOItemDefinition item, int quantity, int priceCopper)
        {
            Configure(item, quantity, priceCopper);
        }

        public void Configure(MMOItemDefinition newItem, int newQuantity, int newPriceCopper)
        {
            item = newItem;
            quantity = Mathf.Max(1, newQuantity);
            priceCopper = Mathf.Max(0, newPriceCopper);
        }
    }
}
