using System.Collections.Generic;
using UnityEngine;

namespace RPGClone.Inventory
{
    public sealed class MMOCharacterEquipment : MonoBehaviour
    {
        [SerializeField] private List<MMOEquipmentSlotType> equipmentSlots = new(DefaultSlots);

        public IReadOnlyList<MMOEquipmentSlotType> EquipmentSlots => equipmentSlots;

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
        }

        public static IReadOnlyList<MMOEquipmentSlotType> GetDefaultSlots()
        {
            return DefaultSlots;
        }
    }
}
