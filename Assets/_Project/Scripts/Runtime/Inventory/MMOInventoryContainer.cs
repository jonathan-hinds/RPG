using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace RPGClone.Inventory
{
    public sealed class MMOInventoryContainer : MonoBehaviour
    {
        public const int DefaultBackpackSlotCount = 16;
        public const int DefaultEquippedBagSlotCount = 4;

        [FormerlySerializedAs("slotCount")]
        [SerializeField, Min(0)] private int baseSlotCount = DefaultBackpackSlotCount;
        [SerializeField, Min(0)] private int bagSlotCount = DefaultEquippedBagSlotCount;
        [SerializeField] private List<MMOItemDefinition> equippedBags = new();
        [SerializeField] private List<MMOItemStack> slots = new();

        public event Action Changed;
        public int SlotCount => CalculateTotalSlotCount();
        public int BaseSlotCount => Mathf.Max(0, baseSlotCount);
        public int BagSlotCount => Mathf.Max(0, bagSlotCount);
        public IReadOnlyList<MMOItemDefinition> EquippedBags => equippedBags;
        public IReadOnlyList<MMOItemStack> Slots => slots;

        private void Awake()
        {
            EnsureSlotList();
        }

        private void OnValidate()
        {
            baseSlotCount = Mathf.Max(0, baseSlotCount);
            bagSlotCount = Mathf.Max(0, bagSlotCount);
            EnsureBagList();
            EnsureSlotList();
        }

        public void Resize(int newSlotCount)
        {
            int clampedSlotCount = Mathf.Max(0, newSlotCount);
            if (baseSlotCount == clampedSlotCount)
            {
                return;
            }

            baseSlotCount = clampedSlotCount;
            EnsureSlotList();
            Changed?.Invoke();
        }

        public MMOItemDefinition GetEquippedBag(int bagSlotIndex)
        {
            EnsureBagList();
            return bagSlotIndex >= 0 && bagSlotIndex < equippedBags.Count
                ? equippedBags[bagSlotIndex]
                : null;
        }

        public int GetBagCapacity(int bagSlotIndex)
        {
            if (bagSlotIndex < 0)
            {
                return BaseSlotCount;
            }

            MMOItemDefinition bag = GetEquippedBag(bagSlotIndex);
            return bag != null ? bag.ContainerSlotCount : 0;
        }

        public int GetBagStartIndex(int bagSlotIndex)
        {
            EnsureBagList();
            if (bagSlotIndex < 0)
            {
                return 0;
            }

            int startIndex = BaseSlotCount;
            int upperBound = Mathf.Min(bagSlotIndex, equippedBags.Count);
            for (int i = 0; i < upperBound; i++)
            {
                MMOItemDefinition bag = equippedBags[i];
                if (bag != null)
                {
                    startIndex += bag.ContainerSlotCount;
                }
            }

            return startIndex;
        }

        public bool CanEquipBagFromInventory(int inventorySlotIndex, int bagSlotIndex = -1)
        {
            EnsureSlotList();
            MMOItemStack source = GetSlot(inventorySlotIndex);
            if (source == null || source.IsEmpty || source.Quantity != 1 || !source.Item.IsContainer)
            {
                return false;
            }

            int targetSlot = ResolveBagSlotForEquip(bagSlotIndex);
            if (targetSlot < 0)
            {
                return false;
            }

            MMOItemDefinition currentBag = GetEquippedBag(targetSlot);
            return currentBag == null || IsBagSegmentEmpty(targetSlot);
        }

        public bool TryEquipBagFromInventory(int inventorySlotIndex, int bagSlotIndex = -1)
        {
            if (!CanEquipBagFromInventory(inventorySlotIndex, bagSlotIndex))
            {
                return false;
            }

            int targetSlot = ResolveBagSlotForEquip(bagSlotIndex);
            MMOItemStack source = GetSlot(inventorySlotIndex);
            MMOItemDefinition newBag = source.Item;
            MMOItemDefinition replacedBag = GetEquippedBag(targetSlot);

            source.Clear();
            if (replacedBag != null)
            {
                int oldStart = GetBagStartIndex(targetSlot);
                slots.RemoveRange(oldStart, replacedBag.ContainerSlotCount);
            }

            equippedBags[targetSlot] = newBag;
            int newStart = GetBagStartIndex(targetSlot);
            slots.InsertRange(newStart, CreateEmptySlots(newBag.ContainerSlotCount));

            if (replacedBag != null && !TryAddItemWithoutNotification(replacedBag, 1))
            {
                Debug.LogError($"Could not return replaced bag '{replacedBag.DisplayName}' to inventory.", this);
                return false;
            }

            EnsureSlotList();
            Changed?.Invoke();
            return true;
        }

        public bool CanUnequipBagToInventory(int bagSlotIndex, int targetInventorySlotIndex = -1)
        {
            EnsureSlotList();
            MMOItemDefinition bag = GetEquippedBag(bagSlotIndex);
            if (bag == null || !IsBagSegmentEmpty(bagSlotIndex))
            {
                return false;
            }

            int start = GetBagStartIndex(bagSlotIndex);
            int end = start + bag.ContainerSlotCount;
            if (targetInventorySlotIndex >= 0)
            {
                MMOItemStack target = GetSlot(targetInventorySlotIndex);
                return targetInventorySlotIndex < start || targetInventorySlotIndex >= end
                    ? target != null && target.IsEmpty
                    : false;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                if ((i < start || i >= end) && slots[i].IsEmpty)
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryUnequipBagToInventory(int bagSlotIndex, int targetInventorySlotIndex = -1)
        {
            if (!CanUnequipBagToInventory(bagSlotIndex, targetInventorySlotIndex))
            {
                return false;
            }

            MMOItemDefinition bag = GetEquippedBag(bagSlotIndex);
            int start = GetBagStartIndex(bagSlotIndex);
            int capacity = bag.ContainerSlotCount;
            slots.RemoveRange(start, capacity);
            equippedBags[bagSlotIndex] = null;
            EnsureSlotList();

            int target = targetInventorySlotIndex;
            if (target >= start + capacity)
            {
                target -= capacity;
            }

            if (target < 0)
            {
                target = slots.FindIndex(slot => slot != null && slot.IsEmpty);
            }

            if (target < 0 || target >= slots.Count || !slots[target].IsEmpty)
            {
                Debug.LogError($"Could not find an inventory slot for unequipped bag '{bag.DisplayName}'.", this);
                return false;
            }

            slots[target].Configure(bag, 1);
            Changed?.Invoke();
            return true;
        }

        public void RestoreEquippedBags(IEnumerable<MMOItemDefinition> bags)
        {
            equippedBags = new List<MMOItemDefinition>();
            if (bags != null)
            {
                foreach (MMOItemDefinition bag in bags)
                {
                    if (equippedBags.Count >= BagSlotCount)
                    {
                        break;
                    }

                    equippedBags.Add(bag != null && bag.IsContainer ? bag : null);
                }
            }

            EnsureBagList();
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

        public bool CanAddItems(IEnumerable<MMOItemDefinition> items, int emptiedSlotIndex = -1)
        {
            EnsureSlotList();
            if (items == null)
            {
                return true;
            }

            List<MMOItemStack> simulatedSlots = new(slots.Count);
            for (int i = 0; i < slots.Count; i++)
            {
                simulatedSlots.Add(i == emptiedSlotIndex ? new MMOItemStack() : slots[i].Clone());
            }

            foreach (MMOItemDefinition item in items)
            {
                if (item == null || !CanAddItemToSimulatedSlots(simulatedSlots, item, 1))
                {
                    return false;
                }
            }

            return true;
        }

        public bool TryAddStack(MMOItemStack stack, out int remainingQuantity)
        {
            return stack != null
                ? TryAddItem(stack.Item, stack.Quantity, out remainingQuantity)
                : TryAddItem(null, 0, out remainingQuantity);
        }

        public bool TryFindFirstSlotContaining(MMOItemDefinition item, out int slotIndex)
        {
            EnsureSlotList();
            slotIndex = -1;
            if (item == null)
            {
                return false;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                MMOItemStack slot = slots[i];
                if (slot != null && !slot.IsEmpty && slot.Item == item)
                {
                    slotIndex = i;
                    return true;
                }
            }

            return false;
        }

        public bool TryMoveSlot(int sourceIndex, int targetIndex)
        {
            EnsureSlotList();
            if (sourceIndex < 0 || sourceIndex >= slots.Count || targetIndex < 0 || targetIndex >= slots.Count || sourceIndex == targetIndex)
            {
                return false;
            }

            MMOItemStack source = slots[sourceIndex];
            MMOItemStack target = slots[targetIndex];
            if (source == null || source.IsEmpty)
            {
                return false;
            }

            if (target == null || target.IsEmpty)
            {
                slots[targetIndex] = source.Clone();
                source.Clear();
                Changed?.Invoke();
                return true;
            }

            if (target.Item == source.Item && target.RemainingStackSpace > 0)
            {
                int previousQuantity = source.Quantity;
                int remaining = target.Add(source.Quantity);
                if (remaining <= 0)
                {
                    source.Clear();
                }
                else
                {
                    source.Configure(source.Item, remaining);
                }

                if (remaining != previousQuantity)
                {
                    Changed?.Invoke();
                    return true;
                }

                return false;
            }

            (slots[sourceIndex], slots[targetIndex]) = (target, source);
            Changed?.Invoke();
            return true;
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
            EnsureBagList();
            slots ??= new List<MMOItemStack>();
            int slotCount = CalculateTotalSlotCount();
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

        private void EnsureBagList()
        {
            equippedBags ??= new List<MMOItemDefinition>();
            while (equippedBags.Count < BagSlotCount)
            {
                equippedBags.Add(null);
            }

            if (equippedBags.Count > BagSlotCount)
            {
                equippedBags.RemoveRange(BagSlotCount, equippedBags.Count - BagSlotCount);
            }

            for (int i = 0; i < equippedBags.Count; i++)
            {
                if (equippedBags[i] != null && !equippedBags[i].IsContainer)
                {
                    equippedBags[i] = null;
                }
            }
        }

        private int CalculateTotalSlotCount()
        {
            int count = BaseSlotCount;
            if (equippedBags == null)
            {
                return count;
            }

            for (int i = 0; i < equippedBags.Count; i++)
            {
                MMOItemDefinition bag = equippedBags[i];
                if (bag != null && bag.IsContainer)
                {
                    count += bag.ContainerSlotCount;
                }
            }

            return count;
        }

        private int ResolveBagSlotForEquip(int requestedBagSlot)
        {
            EnsureBagList();
            if (requestedBagSlot >= 0)
            {
                return requestedBagSlot < equippedBags.Count ? requestedBagSlot : -1;
            }

            return equippedBags.FindIndex(bag => bag == null);
        }

        private bool IsBagSegmentEmpty(int bagSlotIndex)
        {
            MMOItemDefinition bag = GetEquippedBag(bagSlotIndex);
            if (bag == null)
            {
                return true;
            }

            int start = GetBagStartIndex(bagSlotIndex);
            int end = start + bag.ContainerSlotCount;
            for (int i = start; i < end && i < slots.Count; i++)
            {
                if (slots[i] != null && !slots[i].IsEmpty)
                {
                    return false;
                }
            }

            return true;
        }

        private static IEnumerable<MMOItemStack> CreateEmptySlots(int count)
        {
            List<MMOItemStack> emptySlots = new(Mathf.Max(0, count));
            for (int i = 0; i < count; i++)
            {
                emptySlots.Add(new MMOItemStack());
            }

            return emptySlots;
        }

        private bool TryAddItemWithoutNotification(MMOItemDefinition item, int quantity)
        {
            int remaining = quantity;
            for (int i = 0; i < slots.Count && remaining > 0; i++)
            {
                MMOItemStack slot = slots[i];
                if (slot == null || !slot.IsEmpty)
                {
                    continue;
                }

                int accepted = Mathf.Min(remaining, item.MaxStackSize);
                slot.Configure(item, accepted);
                remaining -= accepted;
            }

            return remaining <= 0;
        }

        private static bool CanAddItemToSimulatedSlots(List<MMOItemStack> simulatedSlots, MMOItemDefinition item, int quantity)
        {
            int remainingQuantity = Mathf.Max(0, quantity);
            if (item == null || remainingQuantity <= 0)
            {
                return false;
            }

            foreach (MMOItemStack slot in simulatedSlots)
            {
                if (slot == null || slot.IsEmpty || slot.Item != item)
                {
                    continue;
                }

                remainingQuantity = slot.Add(remainingQuantity);
                if (remainingQuantity <= 0)
                {
                    return true;
                }
            }

            foreach (MMOItemStack slot in simulatedSlots)
            {
                if (slot == null || !slot.IsEmpty)
                {
                    continue;
                }

                int accepted = Mathf.Min(remainingQuantity, item.MaxStackSize);
                slot.Configure(item, accepted);
                remainingQuantity -= accepted;
                if (remainingQuantity <= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
