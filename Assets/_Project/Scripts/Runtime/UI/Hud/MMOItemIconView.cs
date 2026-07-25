using RPGClone.Characters;
using RPGClone.Inventory;
using RPGClone.Services;
using UnityEngine;
using UnityEngine.UI;

namespace RPGClone.UI
{
    public static class MMOItemIconView
    {
        private const float DefaultInset = 4f;
        private static readonly Color RestrictedTint = new(1f, 0.24f, 0.24f, 1f);

        public static void AddToSlot(
            RectTransform slot,
            MMOItemDefinition item,
            int quantity = 0,
            bool bindTooltip = true,
            bool selected = false,
            float inset = DefaultInset)
        {
            if (slot == null || item == null)
            {
                return;
            }

            MMOSlotView view = MMOSlotView.Attach(slot.gameObject);
            view.Present(MMOItemSlotAdapter.Present(item, quantity, selected));

            if (bindTooltip)
            {
                MMOItemTooltipTrigger.Bind(slot.gameObject, item);
            }
        }

        public static Color GetSlotBackgroundColor(MMOItemDefinition item)
        {
            if (item == null)
            {
                return new Color(0.045f, 0.04f, 0.036f, 0.94f);
            }

            return item.Quality switch
            {
                MMOItemQuality.Common => new Color(0.07f, 0.065f, 0.058f, 0.96f),
                MMOItemQuality.Uncommon => new Color(0.035f, 0.08f, 0.035f, 0.96f),
                MMOItemQuality.Rare => new Color(0.035f, 0.055f, 0.095f, 0.96f),
                MMOItemQuality.Epic => new Color(0.075f, 0.035f, 0.095f, 0.96f),
                _ => new Color(0.055f, 0.05f, 0.045f, 0.96f)
            };
        }

        public static Color GetIconTint(MMOItemDefinition item)
        {
            return IsRestrictedForLocalPlayer(item) ? RestrictedTint : Color.white;
        }

        public static bool IsRestrictedForLocalPlayer(MMOItemDefinition item)
        {
            if (item == null
                || !item.IsEquipment
                || !MMOGameplaySessionService.LocalPlayer.TryGetComponent(out MMOCharacterCustomization customization))
            {
                return false;
            }

            return MMOItemClassCompatibility.IsRestricted(item, customization.CharacterClass);
        }

        public static Color GetQualityTextColor(MMOItemQuality quality)
        {
            return quality switch
            {
                MMOItemQuality.Common => Color.white,
                MMOItemQuality.Uncommon => new Color(0.12f, 1f, 0f, 1f),
                MMOItemQuality.Rare => new Color(0f, 0.44f, 0.87f, 1f),
                MMOItemQuality.Epic => new Color(0.64f, 0.21f, 0.93f, 1f),
                _ => new Color(0.62f, 0.62f, 0.62f, 1f)
            };
        }

        public static string GetFallbackLabel(MMOItemDefinition item)
        {
            string displayName = item.DisplayName;
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return "?";
            }

            string[] words = displayName.Split(' ');
            if (words.Length == 1)
            {
                return displayName.Length <= 2 ? displayName.ToUpperInvariant() : displayName[..2].ToUpperInvariant();
            }

            char first = words[0].Length > 0 ? words[0][0] : '?';
            char second = words[^1].Length > 0 ? words[^1][0] : '?';
            return $"{char.ToUpperInvariant(first)}{char.ToUpperInvariant(second)}";
        }
    }
}
