using RPGClone.Inventory;
using UnityEngine;

namespace RPGClone.UI
{
    public static class MMOItemSlotAdapter
    {
        public static MMOSlotPresentation Present(
            MMOItemDefinition item,
            int quantity = 0,
            bool selected = false,
            string secondaryText = null,
            bool disabled = false)
        {
            if (item == null)
            {
                return MMOSlotPresentation.Empty();
            }

            bool restricted = MMOItemIconView.IsRestrictedForLocalPlayer(item);
            return new MMOSlotPresentation(
                icon: item.Icon,
                primaryText: quantity > 1 ? quantity.ToString() : null,
                secondaryText: secondaryText,
                centerText: item.Icon == null ? MMOItemIconView.GetFallbackLabel(item) : null,
                iconTint: MMOItemIconView.GetIconTint(item),
                borderTint: MMOItemIconView.GetQualityTextColor(item.Quality),
                selected: selected,
                disabled: disabled,
                usable: !restricted);
        }
    }
}
