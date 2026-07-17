using System;
using System.Collections.Generic;
using RPGClone.Characters;
using UnityEngine;

namespace RPGClone.Inventory
{
    public sealed class MMOCharacterEquipment : MonoBehaviour
    {
        [SerializeField] private List<MMOEquipmentSlotType> equipmentSlots = new(DefaultSlots);
        [SerializeField] private List<MMOEquippedItemSlot> equippedItems = new();

        public event Action<MMOCharacterEquipment> Changed;
        public IReadOnlyList<MMOEquipmentSlotType> EquipmentSlots => equipmentSlots;
        public IReadOnlyList<MMOEquippedItemSlot> EquippedItems => equippedItems;

        private static readonly MMOEquipmentSlotType[] DefaultSlots =
        {
            MMOEquipmentSlotType.Head,
            MMOEquipmentSlotType.Neck,
            MMOEquipmentSlotType.Shoulders,
            MMOEquipmentSlotType.Back,
            MMOEquipmentSlotType.Chest,
            MMOEquipmentSlotType.Shirt,
            MMOEquipmentSlotType.Tabard,
            MMOEquipmentSlotType.Wrists,
            MMOEquipmentSlotType.Hands,
            MMOEquipmentSlotType.Waist,
            MMOEquipmentSlotType.Legs,
            MMOEquipmentSlotType.Feet,
            MMOEquipmentSlotType.Finger1,
            MMOEquipmentSlotType.Finger2,
            MMOEquipmentSlotType.Trinket1,
            MMOEquipmentSlotType.Trinket2,
            MMOEquipmentSlotType.MainHand,
            MMOEquipmentSlotType.OffHand,
            MMOEquipmentSlotType.Ranged
        };

        private void OnValidate()
        {
            EnsureDefaultSlots();
        }

        public void EnsureDefaultSlots()
        {
            equipmentSlots ??= new List<MMOEquipmentSlotType>();
            foreach (MMOEquipmentSlotType slotType in DefaultSlots)
            {
                if (!equipmentSlots.Contains(slotType))
                {
                    equipmentSlots.Add(slotType);
                }
            }

            equippedItems ??= new List<MMOEquippedItemSlot>();
        }

        public static IReadOnlyList<MMOEquipmentSlotType> GetDefaultSlots()
        {
            return DefaultSlots;
        }

        public bool CanEquip(MMOItemDefinition item)
        {
            if (item == null || !item.IsEquipment || !equipmentSlots.Contains(item.EquipmentSlot))
            {
                return false;
            }

            MMOCharacterCustomization customization = GetComponent<MMOCharacterCustomization>();
            MMOPlayableClass characterClass = customization != null ? customization.CharacterClass : MMOPlayableClass.Warrior;
            if (!MMOItemClassCompatibility.CanEquip(item, characterClass))
            {
                return false;
            }

            if (item.IsWeapon && item.EquipmentSlot != MMOEquipmentSlotType.MainHand)
            {
                return false;
            }

            if (item.IsShield && item.EquipmentSlot != MMOEquipmentSlotType.OffHand)
            {
                return false;
            }

            MMOItemDefinition mainHand = GetEquippedItem(MMOEquipmentSlotType.MainHand);
            if (item.EquipmentSlot == MMOEquipmentSlotType.OffHand && mainHand != null && mainHand.IsTwoHandedWeapon)
            {
                return false;
            }

            return true;
        }

        public bool TryEquipFromInventory(MMOInventoryContainer inventory, int slotIndex)
        {
            if (inventory == null)
            {
                return false;
            }

            MMOItemStack stack = inventory.GetSlot(slotIndex);
            if (stack == null || stack.IsEmpty)
            {
                return false;
            }

            MMOItemDefinition item = stack.Item;
            if (!CanEquip(item))
            {
                return false;
            }

            List<MMOEquipmentSlotType> displacedSlots = GetSlotsDisplacedBy(item);
            List<MMOItemDefinition> displacedItems = GetEquippedItems(displacedSlots);
            int emptiedSlotIndex = stack.Quantity <= 1 ? slotIndex : -1;
            if (!inventory.CanAddItems(displacedItems, emptiedSlotIndex))
            {
                return false;
            }

            EquipItem(item, displacedSlots);
            int remainingSourceQuantity = stack.Quantity - 1;
            inventory.SetSlot(slotIndex, remainingSourceQuantity > 0 ? item : null, remainingSourceQuantity);
            AddItemsToInventory(inventory, displacedItems);
            Changed?.Invoke(this);
            return true;
        }

        public bool TryEquip(MMOItemDefinition item)
        {
            if (!CanEquip(item))
            {
                return false;
            }

            EquipItem(item, GetSlotsDisplacedBy(item));
            Changed?.Invoke(this);
            return true;
        }

        public bool TryUnequipToInventory(MMOInventoryContainer inventory, MMOEquipmentSlotType slotType, int preferredSlotIndex = -1)
        {
            if (inventory == null)
            {
                return false;
            }

            MMOItemDefinition item = GetEquippedItem(slotType);
            if (item == null || !inventory.CanAddItem(item, 1))
            {
                return false;
            }

            if (!TryAddUnequippedItem(inventory, item, preferredSlotIndex))
            {
                return false;
            }

            ClearSlot(slotType, true);
            Changed?.Invoke(this);
            return true;
        }

        public void ClearEquipment(bool removeStatBonuses = true)
        {
            MMOCharacterIdentity identity = GetComponent<MMOCharacterIdentity>();
            foreach (MMOEquippedItemSlot equippedItem in equippedItems)
            {
                if (equippedItem == null)
                {
                    continue;
                }

                if (removeStatBonuses && identity != null && equippedItem.Item != null)
                {
                    identity.RemoveStatGains(equippedItem.Item.StatBonuses, true);
                }

                equippedItem.Configure(equippedItem.SlotType, null);
            }

            Changed?.Invoke(this);
        }

        public MMOItemDefinition GetEquippedItem(MMOEquipmentSlotType slotType)
        {
            foreach (MMOEquippedItemSlot equippedItem in equippedItems)
            {
                if (equippedItem != null && equippedItem.SlotType == slotType)
                {
                    return equippedItem.Item;
                }
            }

            return null;
        }

        private MMOEquippedItemSlot GetOrCreateSlot(MMOEquipmentSlotType slotType)
        {
            equippedItems ??= new List<MMOEquippedItemSlot>();
            foreach (MMOEquippedItemSlot equippedItem in equippedItems)
            {
                if (equippedItem != null && equippedItem.SlotType == slotType)
                {
                    return equippedItem;
                }
            }

            MMOEquippedItemSlot slot = new(slotType, null);
            equippedItems.Add(slot);
            return slot;
        }

        private void ClearSlot(MMOEquipmentSlotType slotType, bool removeStatBonuses)
        {
            MMOEquippedItemSlot slot = GetOrCreateSlot(slotType);
            if (slot.Item == null)
            {
                return;
            }

            MMOCharacterIdentity identity = GetComponent<MMOCharacterIdentity>();
            if (removeStatBonuses && identity != null)
            {
                identity.RemoveStatGains(slot.Item.StatBonuses, true);
            }

            slot.Configure(slotType, null);
        }

        private void EquipItem(MMOItemDefinition item, IReadOnlyList<MMOEquipmentSlotType> displacedSlots)
        {
            MMOCharacterIdentity identity = GetComponent<MMOCharacterIdentity>();
            if (displacedSlots != null)
            {
                for (int i = 0; i < displacedSlots.Count; i++)
                {
                    MMOEquippedItemSlot displacedSlot = GetOrCreateSlot(displacedSlots[i]);
                    if (identity != null && displacedSlot.Item != null)
                    {
                        identity.RemoveStatGains(displacedSlot.Item.StatBonuses, true);
                    }

                    displacedSlot.Configure(displacedSlots[i], null);
                }
            }

            MMOEquippedItemSlot targetSlot = GetOrCreateSlot(item.EquipmentSlot);
            targetSlot.Configure(item.EquipmentSlot, item);
            if (identity != null)
            {
                identity.ApplyStatGains(item.StatBonuses, true);
            }
        }

        private List<MMOEquipmentSlotType> GetSlotsDisplacedBy(MMOItemDefinition item)
        {
            List<MMOEquipmentSlotType> slots = new();
            if (item == null)
            {
                return slots;
            }

            slots.Add(item.EquipmentSlot);
            if (item.IsTwoHandedWeapon && item.EquipmentSlot == MMOEquipmentSlotType.MainHand)
            {
                slots.Add(MMOEquipmentSlotType.OffHand);
            }

            return slots;
        }

        private List<MMOItemDefinition> GetEquippedItems(IReadOnlyList<MMOEquipmentSlotType> slotTypes)
        {
            List<MMOItemDefinition> items = new();
            if (slotTypes == null)
            {
                return items;
            }

            for (int i = 0; i < slotTypes.Count; i++)
            {
                MMOItemDefinition equippedItem = GetEquippedItem(slotTypes[i]);
                if (equippedItem != null)
                {
                    items.Add(equippedItem);
                }
            }

            return items;
        }

        private static void AddItemsToInventory(MMOInventoryContainer inventory, IReadOnlyList<MMOItemDefinition> items)
        {
            if (inventory == null || items == null)
            {
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                inventory.TryAddItem(items[i], 1, out _);
            }
        }

        private static bool TryAddUnequippedItem(MMOInventoryContainer inventory, MMOItemDefinition item, int preferredSlotIndex)
        {
            if (preferredSlotIndex >= 0)
            {
                MMOItemStack preferredSlot = inventory.GetSlot(preferredSlotIndex);
                if (preferredSlot != null && preferredSlot.IsEmpty)
                {
                    inventory.SetSlot(preferredSlotIndex, item, 1);
                    return true;
                }
            }

            return inventory.TryAddItem(item, 1, out int remainingQuantity) && remainingQuantity <= 0;
        }

    }

    [Serializable]
    public sealed class MMOEquippedItemSlot
    {
        [SerializeField] private MMOEquipmentSlotType slotType;
        [SerializeField] private MMOItemDefinition item;

        public MMOEquipmentSlotType SlotType => slotType;
        public MMOItemDefinition Item => item;

        public MMOEquippedItemSlot(MMOEquipmentSlotType slotType, MMOItemDefinition item)
        {
            Configure(slotType, item);
        }

        public void Configure(MMOEquipmentSlotType newSlotType, MMOItemDefinition newItem)
        {
            slotType = newSlotType;
            item = newItem;
        }
    }
}
