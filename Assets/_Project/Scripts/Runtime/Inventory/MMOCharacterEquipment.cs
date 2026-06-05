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
            return item.ArmorWeight <= GetMaximumArmorWeight(characterClass);
        }

        public bool TryEquipFromInventory(MMOInventoryContainer inventory, int slotIndex)
        {
            if (inventory == null)
            {
                return false;
            }

            MMOItemStack stack = inventory.GetSlot(slotIndex);
            if (stack == null || stack.IsEmpty || !TryEquip(stack.Item))
            {
                return false;
            }

            stack.Clear();
            inventory.SetSlot(slotIndex, null, 0);
            return true;
        }

        public bool TryEquip(MMOItemDefinition item)
        {
            if (!CanEquip(item))
            {
                return false;
            }

            MMOEquippedItemSlot existing = GetOrCreateSlot(item.EquipmentSlot);
            MMOCharacterIdentity identity = GetComponent<MMOCharacterIdentity>();
            if (identity != null && existing.Item != null)
            {
                identity.RemoveStatGains(existing.Item.StatBonuses, true);
            }

            existing.Configure(item.EquipmentSlot, item);
            if (identity != null)
            {
                identity.ApplyStatGains(item.StatBonuses, true);
            }

            Changed?.Invoke(this);
            return true;
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

        private static MMOArmorWeight GetMaximumArmorWeight(MMOPlayableClass characterClass)
        {
            return characterClass switch
            {
                MMOPlayableClass.Mage => MMOArmorWeight.Cloth,
                MMOPlayableClass.Shaman => MMOArmorWeight.Leather,
                MMOPlayableClass.Warrior => MMOArmorWeight.Mail,
                _ => MMOArmorWeight.Cloth
            };
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
