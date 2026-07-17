using RPGClone.Characters;

namespace RPGClone.Inventory
{
    public static class MMOItemClassCompatibility
    {
        public static bool CanEquip(MMOItemDefinition item, MMOPlayableClass characterClass)
        {
            if (item == null || !item.IsEquipment || !item.CanClassEquip(characterClass))
            {
                return false;
            }

            return item.IsWeapon
                || item.IsShield
                || item.ArmorWeight <= GetMaximumArmorWeight(characterClass);
        }

        public static bool IsRestricted(MMOItemDefinition item, MMOPlayableClass characterClass)
        {
            return item != null && item.IsEquipment && !CanEquip(item, characterClass);
        }

        public static MMOArmorWeight GetMaximumArmorWeight(MMOPlayableClass characterClass)
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
}
