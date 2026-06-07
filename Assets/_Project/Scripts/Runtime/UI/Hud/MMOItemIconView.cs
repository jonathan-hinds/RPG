using RPGClone.Inventory;
using UnityEngine;
using UnityEngine.UI;

namespace RPGClone.UI
{
    public static class MMOItemIconView
    {
        private const float DefaultInset = 4f;

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

            Image icon = MMOUiFactory.CreateImage("Item Icon", slot, Color.white, false);
            icon.sprite = item.Icon;
            icon.preserveAspect = true;
            icon.gameObject.SetActive(item.Icon != null);
            StretchWithInset(icon.rectTransform, inset);

            if (item.Icon == null)
            {
                Text placeholder = MMOUiFactory.CreateText("Icon Placeholder", slot, 16, FontStyle.Bold, TextAnchor.MiddleCenter);
                placeholder.text = BuildFallbackLabel(item);
                placeholder.color = GetQualityTextColor(item.Quality);
                MMOUiFactory.Stretch(placeholder.rectTransform);
                placeholder.rectTransform.offsetMin = new Vector2(inset, inset);
                placeholder.rectTransform.offsetMax = new Vector2(-inset, -inset);
            }

            AddFrame(slot.gameObject, item.Quality, selected);
            if (bindTooltip)
            {
                MMOItemTooltipTrigger.Bind(slot.gameObject, item);
            }

            if (quantity > 1)
            {
                AddQuantity(slot, quantity);
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

        private static void AddFrame(GameObject slot, MMOItemQuality quality, bool selected)
        {
            Outline outline = slot.GetComponent<Outline>();
            if (outline == null)
            {
                outline = slot.AddComponent<Outline>();
            }

            outline.effectColor = selected ? new Color(1f, 0.82f, 0.24f, 1f) : GetQualityTextColor(quality);
            outline.effectDistance = selected ? new Vector2(2f, -2f) : new Vector2(1f, -1f);
        }

        private static void AddQuantity(RectTransform slot, int quantity)
        {
            Text quantityText = MMOUiFactory.CreateText("Quantity", slot, 11, FontStyle.Bold, TextAnchor.LowerRight);
            quantityText.text = quantity.ToString();
            quantityText.color = Color.white;
            quantityText.raycastTarget = false;
            MMOUiFactory.Stretch(quantityText.rectTransform);
            quantityText.rectTransform.offsetMin = new Vector2(4f, 2f);
            quantityText.rectTransform.offsetMax = new Vector2(-4f, -2f);
        }

        private static void StretchWithInset(RectTransform rectTransform, float inset)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = new Vector2(inset, inset);
            rectTransform.offsetMax = new Vector2(-inset, -inset);
        }

        private static string BuildFallbackLabel(MMOItemDefinition item)
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
