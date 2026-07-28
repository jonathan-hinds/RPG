using System;
using UnityEngine;

namespace RPGClone.UI
{
    public enum MMOUnitFrameStyle
    {
        Player,
        Target,
        Party
    }

    public enum MMOUnitFramePortraitSide
    {
        Left,
        Right
    }

    [Serializable]
    public sealed class MMOUnitFrameLayout
    {
        [SerializeField] private Vector2 frameSize = new(320f, 96f);
        [SerializeField] private MMOUnitFramePortraitSide portraitSide = MMOUnitFramePortraitSide.Left;
        [SerializeField, Min(1f)] private float portraitBezelSize = 88f;
        [SerializeField, Min(1f)] private float portraitMaskSize = 61f;
        [SerializeField, Min(0f)] private float portraitEdgeInset = 3f;
        [SerializeField] private float portraitVerticalOffset;
        [SerializeField, Min(0f)] private float contentPortraitInset = 76f;
        [SerializeField, Min(0f)] private float contentOuterInset = 10f;
        [SerializeField, Min(0f)] private float contentTopInset = 10f;
        [SerializeField, Min(0f)] private float contentBottomInset = 10f;
        [SerializeField, Min(1f)] private float nameHeight = 24f;
        [SerializeField, Min(1f)] private float healthHeight = 22f;
        [SerializeField, Min(1f)] private float resourceHeight = 14f;
        [SerializeField, Min(0f)] private float elementSpacing = 2f;
        [SerializeField, Min(1f)] private float levelBadgeSize = 29f;
        [SerializeField] private Vector2 levelBadgeOffset = new(31f, -31f);
        [SerializeField, Min(1)] private int nameFontSize = 15;
        [SerializeField, Min(1)] private int valueFontSize = 11;

        public Vector2 FrameSize => frameSize;
        public MMOUnitFramePortraitSide PortraitSide => portraitSide;
        public bool IsMirrored => portraitSide == MMOUnitFramePortraitSide.Right;
        public float PortraitBezelSize => portraitBezelSize;
        public float PortraitMaskSize => portraitMaskSize;
        public float PortraitEdgeInset => portraitEdgeInset;
        public float PortraitVerticalOffset => portraitVerticalOffset;
        public float ContentPortraitInset => contentPortraitInset;
        public float ContentOuterInset => contentOuterInset;
        public float ContentTopInset => contentTopInset;
        public float ContentBottomInset => contentBottomInset;
        public float NameHeight => nameHeight;
        public float HealthHeight => healthHeight;
        public float ResourceHeight => resourceHeight;
        public float ElementSpacing => elementSpacing;
        public float LevelBadgeSize => levelBadgeSize;
        public Vector2 LevelBadgeOffset => levelBadgeOffset;
        public int NameFontSize => nameFontSize;
        public int ValueFontSize => valueFontSize;

        public MMOUnitFrameLayout Configure(
            Vector2 size,
            MMOUnitFramePortraitSide side,
            float bezelSize,
            float maskSize,
            float edgeInset,
            float contentInset,
            float outerInset,
            float topInset,
            float bottomInset,
            float newNameHeight,
            float newHealthHeight,
            float newResourceHeight,
            float spacing,
            float badgeSize,
            Vector2 badgeOffset,
            int newNameFontSize,
            int newValueFontSize)
        {
            frameSize = size;
            portraitSide = side;
            portraitBezelSize = bezelSize;
            portraitMaskSize = maskSize;
            portraitEdgeInset = edgeInset;
            contentPortraitInset = contentInset;
            contentOuterInset = outerInset;
            contentTopInset = topInset;
            contentBottomInset = bottomInset;
            nameHeight = newNameHeight;
            healthHeight = newHealthHeight;
            resourceHeight = newResourceHeight;
            elementSpacing = spacing;
            levelBadgeSize = badgeSize;
            levelBadgeOffset = badgeOffset;
            nameFontSize = newNameFontSize;
            valueFontSize = newValueFontSize;
            return this;
        }
    }

    [CreateAssetMenu(fileName = "ClassicUnitFrameTheme", menuName = "RPG Clone/UI/Unit Frame Theme")]
    public sealed class MMOUnitFrameTheme : ScriptableObject
    {
        [Header("Modular Artwork")]
        [SerializeField] private Sprite backplate;
        [SerializeField] private Sprite portraitBezel;
        [SerializeField] private Sprite portraitMask;
        [SerializeField] private Sprite nameplate;
        [SerializeField] private Sprite barWell;
        [SerializeField] private Sprite levelMedallion;

        [Header("Layouts")]
        [SerializeField] private MMOUnitFrameLayout playerLayout = new();
        [SerializeField] private MMOUnitFrameLayout targetLayout = new();
        [SerializeField] private MMOUnitFrameLayout partyLayout = new();

        [Header("Palette")]
        [SerializeField] private Color healthColor = new(0.08f, 0.58f, 0.14f, 1f);
        [SerializeField] private Color manaColor = new(0.07f, 0.28f, 0.76f, 1f);
        [SerializeField] private Color textColor = new(0.95f, 0.90f, 0.77f, 1f);
        [SerializeField] private Color friendlyNameColor = new(0.42f, 0.94f, 0.34f, 1f);
        [SerializeField] private Color neutralNameColor = new(1f, 0.82f, 0.22f, 1f);
        [SerializeField] private Color hostileNameColor = new(1f, 0.25f, 0.18f, 1f);
        [SerializeField] private Color barHighlightColor = new(1f, 1f, 1f, 0.16f);
        [SerializeField] private Color frameShadowColor = new(0f, 0f, 0f, 0.62f);

        public Sprite Backplate => backplate;
        public Sprite PortraitBezel => portraitBezel;
        public Sprite PortraitMask => portraitMask;
        public Sprite Nameplate => nameplate;
        public Sprite BarWell => barWell;
        public Sprite LevelMedallion => levelMedallion;
        public Color HealthColor => healthColor;
        public Color ManaColor => manaColor;
        public Color TextColor => textColor;
        public Color FriendlyNameColor => friendlyNameColor;
        public Color NeutralNameColor => neutralNameColor;
        public Color HostileNameColor => hostileNameColor;
        public Color BarHighlightColor => barHighlightColor;
        public Color FrameShadowColor => frameShadowColor;

        public MMOUnitFrameLayout GetLayout(MMOUnitFrameStyle style)
        {
            return style switch
            {
                MMOUnitFrameStyle.Target => targetLayout,
                MMOUnitFrameStyle.Party => partyLayout,
                _ => playerLayout
            };
        }

        public Vector2 GetFrameSize(MMOUnitFrameStyle style)
        {
            return GetLayout(style)?.FrameSize ?? new Vector2(320f, 96f);
        }

        public void ConfigureArtwork(
            Sprite newBackplate,
            Sprite newPortraitBezel,
            Sprite newPortraitMask,
            Sprite newNameplate,
            Sprite newBarWell,
            Sprite newLevelMedallion)
        {
            backplate = newBackplate;
            portraitBezel = newPortraitBezel;
            portraitMask = newPortraitMask;
            nameplate = newNameplate;
            barWell = newBarWell;
            levelMedallion = newLevelMedallion;
        }

        public void ConfigureLayouts(
            MMOUnitFrameLayout newPlayerLayout,
            MMOUnitFrameLayout newTargetLayout,
            MMOUnitFrameLayout newPartyLayout)
        {
            playerLayout = newPlayerLayout;
            targetLayout = newTargetLayout;
            partyLayout = newPartyLayout;
        }
    }
}
