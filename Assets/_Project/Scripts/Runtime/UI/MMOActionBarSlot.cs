using System;
using RPGClone.Abilities;
using RPGClone.Inventory;
using UnityEngine.InputSystem;

namespace RPGClone.UI
{
    public enum MMOActionBarSlotBindingType
    {
        Empty,
        Ability,
        Item
    }

    [Serializable]
    public sealed class MMOActionBarSlot
    {
        public MMOActionBarSlotBindingType bindingType = MMOActionBarSlotBindingType.Empty;
        public MMOAbilityDefinition ability;
        public MMOItemDefinition item;
        public Key key = Key.None;

        public bool IsEmpty => bindingType == MMOActionBarSlotBindingType.Empty || (ability == null && item == null);

        public void SetAbility(MMOAbilityDefinition newAbility)
        {
            ability = newAbility;
            item = null;
            bindingType = ability != null ? MMOActionBarSlotBindingType.Ability : MMOActionBarSlotBindingType.Empty;
        }

        public void SetItem(MMOItemDefinition newItem)
        {
            item = newItem;
            ability = null;
            bindingType = item != null ? MMOActionBarSlotBindingType.Item : MMOActionBarSlotBindingType.Empty;
        }

        public void CopyBindingFrom(MMOActionBarSlot source)
        {
            if (source == null)
            {
                ClearBinding();
                return;
            }

            bindingType = source.bindingType;
            ability = source.ability;
            item = source.item;
        }

        public void ClearBinding()
        {
            bindingType = MMOActionBarSlotBindingType.Empty;
            ability = null;
            item = null;
        }
    }
}
