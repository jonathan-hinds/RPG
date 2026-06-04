using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPGClone.Inventory
{
    public sealed class MMOInventoryContainer : MonoBehaviour
    {
        [SerializeField, Min(0)] private int slotCount = 16;
        [SerializeField] private List<MMOItemStack> slots = new();

        public event Action Changed;
        public int SlotCount => slotCount;
        public IReadOnlyList<MMOItemStack> Slots => slots;

        private void Awake()
        {
            EnsureSlotList();
        }

        private void OnValidate()
        {
            slotCount = Mathf.Max(0, slotCount);
            EnsureSlotList();
        }

        public void Resize(int newSlotCount)
        {
            int clampedSlotCount = Mathf.Max(0, newSlotCount);
            if (slotCount == clampedSlotCount)
            {
                return;
            }

            slotCount = clampedSlotCount;
            EnsureSlotList();
            Changed?.Invoke();
        }

        public MMOItemStack GetSlot(int index)
        {
            EnsureSlotList();
            return index >= 0 && index < slots.Count ? slots[index] : null;
        }

        public bool TryAddItem(MMOItemDefinition item, int quantity, out int remainingQuantity)
        {
            EnsureSlotList();
            remainingQuantity = Mathf.Max(0, quantity);
            if (item == null || remainingQuantity <= 0)
            {
                return false;
            }

            for (int i = 0; i < slots.Count && remainingQuantity > 0; i++)
            {
                MMOItemStack slot = slots[i];
                if (slot.IsEmpty || slot.Item != item)
                {
                    continue;
                }

                remainingQuantity = slot.Add(remainingQuantity);
            }

            for (int i = 0; i < slots.Count && remainingQuantity > 0; i++)
            {
                MMOItemStack slot = slots[i];
                if (!slot.IsEmpty)
                {
                    continue;
                }

                int accepted = Mathf.Min(remainingQuantity, item.MaxStackSize);
                slot.Configure(item, accepted);
                remainingQuantity -= accepted;
            }

            bool acceptedAny = remainingQuantity != quantity;
            if (acceptedAny)
            {
                Changed?.Invoke();
            }

            return remainingQuantity <= 0;
        }

        public bool TryAddStack(MMOItemStack stack, out int remainingQuantity)
        {
            return stack != null
                ? TryAddItem(stack.Item, stack.Quantity, out remainingQuantity)
                : TryAddItem(null, 0, out remainingQuantity);
        }

        private void EnsureSlotList()
        {
            slots ??= new List<MMOItemStack>();
            while (slots.Count < slotCount)
            {
                slots.Add(new MMOItemStack());
            }

            if (slots.Count > slotCount)
            {
                slots.RemoveRange(slotCount, slots.Count - slotCount);
            }

            for (int i = 0; i < slots.Count; i++)
            {
                slots[i] ??= new MMOItemStack();
            }
        }
    }
}
