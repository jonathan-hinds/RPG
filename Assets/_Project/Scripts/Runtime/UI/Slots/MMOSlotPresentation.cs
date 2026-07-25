using UnityEngine;

namespace RPGClone.UI
{
    /// <summary>
    /// Content-neutral snapshot consumed by <see cref="MMOSlotView"/>.
    /// Gameplay models remain owned by their inventory, ability, quest, or vendor systems.
    /// </summary>
    public readonly struct MMOSlotPresentation
    {
        public readonly Sprite Icon;
        public readonly Sprite CategorySilhouette;
        public readonly string PrimaryText;
        public readonly string SecondaryText;
        public readonly string CenterText;
        public readonly Color IconTint;
        public readonly Color BorderTint;
        public readonly bool Selected;
        public readonly bool Active;
        public readonly bool Disabled;
        public readonly bool Usable;
        public readonly bool InRange;
        public readonly bool Attention;
        public readonly bool ProcGlow;
        public readonly bool ShowStatusMarker;
        public readonly float CooldownNormalized;
        public readonly string CooldownText;

        public MMOSlotPresentation(
            Sprite icon = null,
            Sprite categorySilhouette = null,
            string primaryText = null,
            string secondaryText = null,
            string centerText = null,
            Color? iconTint = null,
            Color? borderTint = null,
            bool selected = false,
            bool active = false,
            bool disabled = false,
            bool usable = true,
            bool inRange = true,
            bool attention = false,
            bool procGlow = false,
            bool showStatusMarker = false,
            float cooldownNormalized = 0f,
            string cooldownText = null)
        {
            Icon = icon;
            CategorySilhouette = categorySilhouette;
            PrimaryText = primaryText;
            SecondaryText = secondaryText;
            CenterText = centerText;
            IconTint = iconTint ?? Color.white;
            BorderTint = borderTint ?? Color.clear;
            Selected = selected;
            Active = active;
            Disabled = disabled;
            Usable = usable;
            InRange = inRange;
            Attention = attention;
            ProcGlow = procGlow;
            ShowStatusMarker = showStatusMarker;
            CooldownNormalized = Mathf.Clamp01(cooldownNormalized);
            CooldownText = cooldownText;
        }

        public static MMOSlotPresentation Empty(Sprite categorySilhouette = null)
        {
            return new MMOSlotPresentation(categorySilhouette: categorySilhouette);
        }
    }
}
