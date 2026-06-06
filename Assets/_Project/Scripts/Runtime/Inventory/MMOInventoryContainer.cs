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

        public bool CanAddItem(MMOItemDefinition item, int quantity)
        {
            EnsureSlotList();
            int remainingQuantity = Mathf.Max(0, quantity);
            if (item == null || remainingQuantity <= 0)
            {
                return false;
            }

            foreach (MMOItemStack slot in slots)
            {
                if (slot != null && !slot.IsEmpty && slot.Item == item)
                {
                    remainingQuantity -= slot.RemainingStackSpace;
                    if (remainingQuantity <= 0)
                    {
                        return true;
                    }
                }
            }

            foreach (MMOItemStack slot in slots)
            {
                if (slot == null || !slot.IsEmpty)
                {
                    continue;
                }

                remainingQuantity -= item.MaxStackSize;
                if (remainingQuantity <= 0)
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryAddStack(MMOItemStack stack, out int remainingQuantity)
        {
            return stack != null
                ? TryAddItem(stack.Item, stack.Quantity, out remainingQuantity)
                : TryAddItem(null, 0, out remainingQuantity);
        }

        public int CountItem(MMOItemDefinition item)
        {
            EnsureSlotList();
            if (item == null)
            {
                return 0;
            }

            int count = 0;
            foreach (MMOItemStack slot in slots)
            {
                if (slot != null && !slot.IsEmpty && slot.Item == item)
                {
                    count += slot.Quantity;
                }
            }

            return count;
        }

        public bool TryRemoveItem(MMOItemDefinition item, int quantity)
        {
            EnsureSlotList();
            int remaining = Mathf.Max(0, quantity);
            if (item == null || remaining <= 0 || CountItem(item) < remaining)
            {
                return false;
            }

            for (int i = slots.Count - 1; i >= 0 && remaining > 0; i--)
            {
                MMOItemStack slot = slots[i];
                if (slot == null || slot.IsEmpty || slot.Item != item)
                {
                    continue;
                }

                int removed = Mathf.Min(remaining, slot.Quantity);
                int newQuantity = slot.Quantity - removed;
                if (newQuantity <= 0)
                {
                    slot.Clear();
                }
                else
                {
                    slot.Configure(item, newQuantity);
                }

                remaining -= removed;
            }

            Changed?.Invoke();
            return true;
        }

        public void Clear()
        {
            EnsureSlotList();
            for (int i = 0; i < slots.Count; i++)
            {
                slots[i].Clear();
            }

            Changed?.Invoke();
        }

        public void SetSlot(int index, MMOItemDefinition item, int quantity)
        {
            EnsureSlotList();
            if (index < 0 || index >= slots.Count)
            {
                return;
            }

            slots[index].Configure(item, quantity);
            Changed?.Invoke();
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
