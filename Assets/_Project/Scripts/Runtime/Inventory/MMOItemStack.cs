using System;
using UnityEngine;

namespace RPGClone.Inventory
{
    [Serializable]
    public sealed class MMOItemStack
    {
        [SerializeField] private MMOItemDefinition item;
        [SerializeField, Min(0)] private int quantity;

        public MMOItemDefinition Item => item;
        public int Quantity => quantity;
        public bool IsEmpty => item == null || quantity <= 0;
        public int RemainingStackSpace => item == null ? 0 : Mathf.Max(0, item.MaxStackSize - quantity);

        public MMOItemStack()
        {
        }

        public MMOItemStack(MMOItemDefinition item, int quantity)
        {
            Configure(item, quantity);
        }

        public void Configure(MMOItemDefinition newItem, int newQuantity)
        {
            item = newItem;
            quantity = item == null ? 0 : Mathf.Clamp(newQuantity, 0, item.MaxStackSize);
        }

        public int Add(int amount)
        {
            if (item == null || amount <= 0)
            {
                return amount;
            }

            int accepted = Mathf.Min(amount, RemainingStackSpace);
            quantity += accepted;
            return amount - accepted;
        }

        public void Clear()
        {
            item = null;
            quantity = 0;
        }

        public MMOItemStack Clone()
        {
            return new MMOItemStack(item, quantity);
        }
    }
}
