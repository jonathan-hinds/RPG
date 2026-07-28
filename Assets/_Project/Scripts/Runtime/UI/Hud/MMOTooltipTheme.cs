using UnityEngine;

namespace RPGClone.UI
{
    [CreateAssetMenu(menuName = "RPG Clone/UI/Tooltip Theme", fileName = "TooltipTheme")]
    public sealed class MMOTooltipTheme : ScriptableObject
    {
        public const string DefaultResourcePath = "RPGClone/UI/Tooltip/DefaultTooltipTheme";

        private static MMOTooltipTheme configuredDefault;
        private static bool defaultLoadAttempted;
        private static MMOTooltipTheme fallback;

        [Header("Panel")]
        [SerializeField] private Sprite panelSprite;
        [SerializeField, Min(120f)] private float minimumWidth = 190f;
        [SerializeField, Min(120f)] private float maximumWidth = 360f;
        [SerializeField, Min(48f)] private float maximumHeight = 640f;
        [SerializeField, Min(0f)] private float horizontalPadding = 11f;
        [SerializeField, Min(0f)] private float verticalPadding = 9f;
        [SerializeField, Min(0f)] private float lineSpacing = 2f;
        [SerializeField, Min(0f)] private float sectionSpacing = 7f;
        [SerializeField, Min(0f)] private float doubleLineGap = 20f;
        [SerializeField] private Color fallbackBackground = new(0.015f, 0.018f, 0.028f, 0.96f);
        [SerializeField] private Color fallbackBorder = new(0.52f, 0.55f, 0.58f, 0.95f);

        [Header("Typography")]
        [SerializeField] private int titleFontSize = 14;
        [SerializeField] private int bodyFontSize = 12;
        [SerializeField] private Color primaryText = new(0.96f, 0.95f, 0.91f, 1f);
        [SerializeField] private Color secondaryText = new(0.78f, 0.79f, 0.82f, 1f);
        [SerializeField] private Color descriptionText = new(1f, 0.82f, 0.23f, 1f);
        [SerializeField] private Color positiveText = new(0.16f, 1f, 0.16f, 1f);
        [SerializeField] private Color negativeText = new(1f, 0.2f, 0.16f, 1f);
        [SerializeField] private Color priceText = new(0.94f, 0.9f, 0.78f, 1f);

        public Sprite PanelSprite => panelSprite;
        public float MinimumWidth => Mathf.Min(minimumWidth, MaximumWidth);
        public float MaximumWidth => Mathf.Max(minimumWidth, maximumWidth);
        public float MaximumHeight => Mathf.Max(48f, maximumHeight);
        public float HorizontalPadding => Mathf.Max(0f, horizontalPadding);
        public float VerticalPadding => Mathf.Max(0f, verticalPadding);
        public float LineSpacing => Mathf.Max(0f, lineSpacing);
        public float SectionSpacing => Mathf.Max(LineSpacing, sectionSpacing);
        public float DoubleLineGap => Mathf.Max(0f, doubleLineGap);
        public Color FallbackBackground => fallbackBackground;
        public Color FallbackBorder => fallbackBorder;
        public int TitleFontSize => Mathf.Max(8, titleFontSize);
        public int BodyFontSize => Mathf.Max(8, bodyFontSize);
        public Color PrimaryText => primaryText;
        public Color SecondaryText => secondaryText;
        public Color DescriptionText => descriptionText;
        public Color PositiveText => positiveText;
        public Color NegativeText => negativeText;
        public Color PriceText => priceText;

        public static MMOTooltipTheme LoadDefault()
        {
            if (!defaultLoadAttempted)
            {
                configuredDefault = Resources.Load<MMOTooltipTheme>(DefaultResourcePath);
                defaultLoadAttempted = true;
            }

            if (configuredDefault != null)
            {
                return configuredDefault;
            }

            if (fallback == null)
            {
                fallback = CreateInstance<MMOTooltipTheme>();
                fallback.hideFlags = HideFlags.HideAndDontSave;
            }

            return fallback;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            configuredDefault = null;
            defaultLoadAttempted = false;
            fallback = null;
        }

        public void ConfigurePanel(Sprite sprite)
        {
            panelSprite = sprite;
        }
    }
}
