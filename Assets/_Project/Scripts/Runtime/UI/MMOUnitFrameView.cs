using System;
using System.Collections.Generic;
using RPGClone.Buffs;
using RPGClone.Characters;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RPGClone.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class MMOUnitFrameView : MonoBehaviour, IPointerClickHandler
    {
        private const int CurrentVisualVersion = 3;
        private const string ThemeResourcePath = "RPGClone/UI/UnitFrames/ClassicUnitFrameTheme";
        private const float BuffSize = 26f;
        private const float BuffSpacing = 4f;

        [Header("Binding")]
        [SerializeField] private bool autoBuild = true;
        [SerializeField] private bool hideWhenUnbound = true;

        [Header("Presentation")]
        [SerializeField] private MMOUnitFrameStyle frameStyle = MMOUnitFrameStyle.Player;
        [SerializeField] private MMOUnitFrameTheme theme;
        [SerializeField, HideInInspector] private int visualVersion;

        private Image frameArtwork;
        private Image portraitImage;
        private Text portraitInitialText;
        private Text nameText;
        private Text levelText;
        private Image healthFill;
        private Text healthText;
        private RectTransform healthBar;
        private Image manaFill;
        private Text manaText;
        private RectTransform manaBar;
        private RectTransform buffRoot;

        private readonly List<BuffIconView> buffIcons = new();
        private MMOCharacterIdentity boundCharacter;
        private MMOCharacterBuffController boundBuffController;
        private bool subscribedToBoundCharacter;
        private float nextBuffTimerRefreshAt;

        private static MMOUnitFrameTheme cachedTheme;
        private static Font cachedFont;

        public event Action<MMOUnitFrameView, MMOCharacterIdentity> Clicked;

        public MMOCharacterIdentity BoundCharacter => boundCharacter;
        public MMOUnitFrameStyle FrameStyle => frameStyle;

        private void Awake()
        {
            if (autoBuild)
            {
                EnsureVisuals();
            }

            Refresh();
        }

        private void OnEnable()
        {
            SubscribeToBoundCharacter();
        }

        private void OnDisable()
        {
            UnsubscribeFromBoundCharacter();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextBuffTimerRefreshAt)
            {
                return;
            }

            nextBuffTimerRefreshAt = Time.unscaledTime + 0.2f;
            RefreshBuffTimers();
        }

        public void ConfigureStyle(MMOUnitFrameStyle style, MMOUnitFrameTheme unitFrameTheme = null)
        {
            bool changed = frameStyle != style || (unitFrameTheme != null && theme != unitFrameTheme);
            frameStyle = style;
            if (unitFrameTheme != null)
            {
                theme = unitFrameTheme;
            }

            if (changed || visualVersion != CurrentVisualVersion)
            {
                RebuildVisuals();
            }
        }

        public void RebuildVisuals()
        {
            DestroyGeneratedChildren();
            ClearVisualReferences();
            CreateVisualHierarchy();
            visualVersion = CurrentVisualVersion;
            Refresh();
        }

        public void Bind(MMOCharacterIdentity character)
        {
            if (boundCharacter == character)
            {
                Refresh();
                return;
            }

            UnsubscribeFromBoundCharacter();
            boundCharacter = character;
            boundBuffController = boundCharacter != null
                ? boundCharacter.GetComponent<MMOCharacterBuffController>()
                : null;

            Refresh();
            SubscribeToBoundCharacter();
        }

        public void Clear()
        {
            Bind(null);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null
                || eventData.button != PointerEventData.InputButton.Left
                || boundCharacter == null)
            {
                return;
            }

            Clicked?.Invoke(this, boundCharacter);
        }

        private void OnCharacterChanged(MMOCharacterIdentity character)
        {
            Refresh();
        }

        private void OnBoundResourceChanged()
        {
            Refresh();
        }

        private void OnBuffsChanged(MMOCharacterBuffController controller)
        {
            RefreshBuffs();
        }

        private void OnBuffsUpdated(MMOCharacterBuffController controller)
        {
            RefreshBuffTimers();
        }

        private void SubscribeToBoundCharacter()
        {
            if (subscribedToBoundCharacter || boundCharacter == null || !isActiveAndEnabled)
            {
                return;
            }

            boundCharacter.Changed += OnCharacterChanged;
            boundCharacter.Health.Changed += OnBoundResourceChanged;
            boundCharacter.Mana.Changed += OnBoundResourceChanged;
            if (boundBuffController != null)
            {
                boundBuffController.BuffsChanged += OnBuffsChanged;
                boundBuffController.BuffsUpdated += OnBuffsUpdated;
            }

            subscribedToBoundCharacter = true;
        }

        private void UnsubscribeFromBoundCharacter()
        {
            if (!subscribedToBoundCharacter || boundCharacter == null)
            {
                subscribedToBoundCharacter = false;
                return;
            }

            boundCharacter.Changed -= OnCharacterChanged;
            boundCharacter.Health.Changed -= OnBoundResourceChanged;
            boundCharacter.Mana.Changed -= OnBoundResourceChanged;
            if (boundBuffController != null)
            {
                boundBuffController.BuffsChanged -= OnBuffsChanged;
                boundBuffController.BuffsUpdated -= OnBuffsUpdated;
            }

            subscribedToBoundCharacter = false;
        }

        private void EnsureVisuals()
        {
            if (frameArtwork != null)
            {
                frameArtwork.raycastTarget = true;
                return;
            }

            if (visualVersion == CurrentVisualVersion && TryBindExistingHierarchy())
            {
                frameArtwork.raycastTarget = true;
                return;
            }

            DestroyGeneratedChildren();
            ClearVisualReferences();
            CreateVisualHierarchy();
            visualVersion = CurrentVisualVersion;
        }

        private void CreateVisualHierarchy()
        {
            MMOUnitFrameTheme resolvedTheme = ResolveTheme();
            MMOUnitFrameLayout layout = resolvedTheme?.GetLayout(frameStyle) ?? CreateFallbackLayout();
            RectTransform root = (RectTransform)transform;
            root.sizeDelta = layout.FrameSize;

            CreateBackplate(resolvedTheme);
            Vector2 portraitCenter = GetPortraitCenter(layout);
            CreatePortrait(resolvedTheme, layout, portraitCenter);
            CreateContent(resolvedTheme, layout);
            CreateLevelBadge(resolvedTheme, layout, portraitCenter);
            CreateBuffRoot(layout);
        }

        private void CreateBackplate(MMOUnitFrameTheme resolvedTheme)
        {
            Image shadow = CreateImage(
                "Frame Shadow",
                transform,
                resolvedTheme != null ? resolvedTheme.FrameShadowColor : new Color(0f, 0f, 0f, 0.62f),
                false);
            shadow.sprite = resolvedTheme?.Backplate;
            shadow.type = Image.Type.Sliced;
            Stretch(shadow.rectTransform);
            shadow.rectTransform.offsetMin = new Vector2(-3f, -5f);
            shadow.rectTransform.offsetMax = new Vector2(3f, 1f);

            frameArtwork = CreateImage("Backplate", transform, Color.white, true);
            frameArtwork.sprite = resolvedTheme?.Backplate;
            frameArtwork.type = Image.Type.Sliced;
            Stretch(frameArtwork.rectTransform);
        }

        private void CreatePortrait(
            MMOUnitFrameTheme resolvedTheme,
            MMOUnitFrameLayout layout,
            Vector2 portraitCenter)
        {
            RectTransform maskRoot = CreateRect("Portrait Mask", transform);
            AnchorToPortraitSide(maskRoot, layout, portraitCenter, layout.PortraitMaskSize);

            Image maskGraphic = maskRoot.gameObject.AddComponent<Image>();
            maskGraphic.sprite = resolvedTheme?.PortraitMask;
            maskGraphic.color = Color.white;
            maskGraphic.raycastTarget = false;
            Mask mask = maskRoot.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            portraitImage = CreateImage("Portrait", maskRoot, Color.white, false);
            Stretch(portraitImage.rectTransform);
            portraitImage.rectTransform.offsetMin = Vector2.one * -3f;
            portraitImage.rectTransform.offsetMax = Vector2.one * 3f;
            portraitImage.preserveAspect = true;

            portraitInitialText = CreateText(
                "Portrait Initial",
                maskRoot,
                layout.NameFontSize + 8,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                true);
            Stretch(portraitInitialText.rectTransform);

            Image bezel = CreateImage("Portrait Bezel", transform, Color.white, false);
            bezel.sprite = resolvedTheme?.PortraitBezel;
            bezel.preserveAspect = true;
            AnchorToPortraitSide(bezel.rectTransform, layout, portraitCenter, layout.PortraitBezelSize);
        }

        private void CreateContent(MMOUnitFrameTheme resolvedTheme, MMOUnitFrameLayout layout)
        {
            RectTransform content = CreateRect("Content", transform);
            Stretch(content);
            content.offsetMin = new Vector2(
                layout.IsMirrored ? layout.ContentOuterInset : layout.ContentPortraitInset,
                layout.ContentBottomInset);
            content.offsetMax = new Vector2(
                -(layout.IsMirrored ? layout.ContentPortraitInset : layout.ContentOuterInset),
                -layout.ContentTopInset);

            float y = 0f;
            Image nameplateArtwork = CreateImage("Nameplate", content, Color.white, false);
            nameplateArtwork.sprite = resolvedTheme?.Nameplate;
            nameplateArtwork.type = Image.Type.Sliced;
            AnchorTop(nameplateArtwork.rectTransform, y, layout.NameHeight);

            nameText = CreateText(
                "Name",
                nameplateArtwork.transform,
                layout.NameFontSize,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                true);
            Stretch(nameText.rectTransform);
            nameText.rectTransform.offsetMin = new Vector2(12f, 1f);
            nameText.rectTransform.offsetMax = new Vector2(-12f, -1f);
            nameText.resizeTextForBestFit = true;
            nameText.resizeTextMinSize = Mathf.Max(9, layout.NameFontSize - 4);
            nameText.resizeTextMaxSize = layout.NameFontSize;
            y += layout.NameHeight + layout.ElementSpacing;

            healthBar = CreateBar(
                "Health Bar",
                content,
                resolvedTheme,
                y,
                layout.HealthHeight,
                layout.ValueFontSize,
                out healthFill,
                out healthText);
            y += layout.HealthHeight + layout.ElementSpacing;

            manaBar = CreateBar(
                "Resource Bar",
                content,
                resolvedTheme,
                y,
                layout.ResourceHeight,
                Mathf.Max(8, layout.ValueFontSize - 1),
                out manaFill,
                out manaText);

            if (resolvedTheme != null)
            {
                healthFill.color = resolvedTheme.HealthColor;
                manaFill.color = resolvedTheme.ManaColor;
                nameText.color = resolvedTheme.TextColor;
                healthText.color = resolvedTheme.TextColor;
                manaText.color = resolvedTheme.TextColor;
            }
        }

        private static void AnchorTop(RectTransform rect, float topOffset, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -topOffset);
            rect.sizeDelta = new Vector2(0f, height);
        }

        private RectTransform CreateBar(
            string objectName,
            Transform parent,
            MMOUnitFrameTheme resolvedTheme,
            float topOffset,
            float height,
            int fontSize,
            out Image fill,
            out Text valueText)
        {
            RectTransform root = CreateRect(objectName, parent);
            AnchorTop(root, topOffset, height);

            Image well = root.gameObject.AddComponent<Image>();
            well.sprite = resolvedTheme?.BarWell;
            well.type = Image.Type.Sliced;
            well.color = Color.white;
            well.raycastTarget = false;

            float horizontalInset = Mathf.Clamp(height * 0.28f, 3f, 6f);
            float verticalInset = Mathf.Clamp(height * 0.18f, 2f, 4f);
            RectTransform fillArea = CreateRect("Fill Area", root);
            Stretch(fillArea);
            fillArea.offsetMin = new Vector2(horizontalInset, verticalInset);
            fillArea.offsetMax = new Vector2(-horizontalInset, -verticalInset);
            fill = CreateImage("Fill", fillArea, Color.white, false);
            Stretch(fill.rectTransform);

            Image highlight = CreateImage(
                "Highlight",
                fill.transform,
                resolvedTheme != null ? resolvedTheme.BarHighlightColor : new Color(1f, 1f, 1f, 0.16f),
                false);
            highlight.rectTransform.anchorMin = new Vector2(0f, 0.62f);
            highlight.rectTransform.anchorMax = Vector2.one;
            highlight.rectTransform.offsetMin = Vector2.zero;
            highlight.rectTransform.offsetMax = Vector2.zero;

            valueText = CreateText(
                "Value",
                root,
                fontSize,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                true);
            Stretch(valueText.rectTransform);
            valueText.resizeTextForBestFit = true;
            valueText.resizeTextMinSize = 7;
            valueText.resizeTextMaxSize = fontSize;
            return root;
        }

        private void CreateLevelBadge(
            MMOUnitFrameTheme resolvedTheme,
            MMOUnitFrameLayout layout,
            Vector2 portraitCenter)
        {
            Vector2 signedOffset = layout.LevelBadgeOffset;
            if (layout.IsMirrored)
            {
                signedOffset.x = -signedOffset.x;
            }

            Image badge = CreateImage("Level Badge", transform, Color.white, false);
            badge.sprite = resolvedTheme?.LevelMedallion;
            badge.preserveAspect = true;
            AnchorToPortraitSide(
                badge.rectTransform,
                layout,
                portraitCenter + signedOffset,
                layout.LevelBadgeSize);

            levelText = CreateText(
                "Level",
                badge.transform,
                Mathf.Max(9, layout.ValueFontSize),
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                true);
            Stretch(levelText.rectTransform);
            levelText.rectTransform.offsetMin = Vector2.one * 3f;
            levelText.rectTransform.offsetMax = Vector2.one * -3f;
            levelText.color = resolvedTheme != null ? resolvedTheme.TextColor : Color.white;
        }

        private void CreateBuffRoot(MMOUnitFrameLayout layout)
        {
            buffRoot = CreateRect("Buffs", transform);
            buffRoot.anchorMin = new Vector2(layout.IsMirrored ? 1f : 0f, 0f);
            buffRoot.anchorMax = buffRoot.anchorMin;
            buffRoot.pivot = new Vector2(layout.IsMirrored ? 1f : 0f, 1f);
            buffRoot.anchoredPosition = new Vector2(
                layout.IsMirrored ? -layout.ContentOuterInset : layout.ContentPortraitInset,
                -4f);
            buffRoot.sizeDelta = new Vector2(layout.FrameSize.x - layout.ContentPortraitInset, BuffSize);
        }

        private static Vector2 GetPortraitCenter(MMOUnitFrameLayout layout)
        {
            float x = layout.PortraitEdgeInset + layout.PortraitBezelSize * 0.5f;
            return new Vector2(layout.IsMirrored ? -x : x, layout.PortraitVerticalOffset);
        }

        private static void AnchorToPortraitSide(
            RectTransform rect,
            MMOUnitFrameLayout layout,
            Vector2 anchoredPosition,
            float size)
        {
            Vector2 anchor = new(layout.IsMirrored ? 1f : 0f, 0.5f);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = Vector2.one * size;
        }

        private bool TryBindExistingHierarchy()
        {
            Transform content = transform.Find("Content");
            Transform portraitMask = transform.Find("Portrait Mask");
            Transform health = content != null ? content.Find("Health Bar") : null;
            Transform resource = content != null ? content.Find("Resource Bar") : null;

            frameArtwork = GetImage(transform.Find("Backplate"));
            portraitImage = GetImage(portraitMask != null ? portraitMask.Find("Portrait") : null);
            portraitInitialText = GetText(portraitMask != null ? portraitMask.Find("Portrait Initial") : null);
            nameText = GetText(content != null ? content.Find("Nameplate/Name") : null);
            levelText = GetText(transform.Find("Level Badge/Level"));
            healthBar = health as RectTransform;
            healthFill = GetImage(health != null ? health.Find("Fill Area/Fill") : null);
            healthText = GetText(health != null ? health.Find("Value") : null);
            manaBar = resource as RectTransform;
            manaFill = GetImage(resource != null ? resource.Find("Fill Area/Fill") : null);
            manaText = GetText(resource != null ? resource.Find("Value") : null);
            buffRoot = transform.Find("Buffs") as RectTransform;

            return frameArtwork != null
                && portraitImage != null
                && portraitInitialText != null
                && nameText != null
                && levelText != null
                && healthBar != null
                && healthFill != null
                && healthText != null
                && manaBar != null
                && manaFill != null
                && manaText != null
                && buffRoot != null;
        }

        private void Refresh()
        {
            EnsureVisuals();

            bool hasCharacter = boundCharacter != null;
            gameObject.SetActive(hasCharacter || !hideWhenUnbound);
            if (!hasCharacter)
            {
                return;
            }

            MMOUnitFrameTheme resolvedTheme = ResolveTheme();
            nameText.text = boundCharacter.DisplayName;
            nameText.color = ResolveNameColor(boundCharacter.Faction, resolvedTheme);
            levelText.text = boundCharacter.Level.ToString();

            portraitImage.sprite = boundCharacter.Portrait;
            portraitImage.color = boundCharacter.Portrait != null
                ? Color.white
                : boundCharacter.PortraitTint;
            portraitInitialText.text = GetInitial(boundCharacter.DisplayName);
            portraitInitialText.enabled = boundCharacter.Portrait == null;
            portraitInitialText.color = resolvedTheme != null ? resolvedTheme.TextColor : Color.white;

            RefreshResource(boundCharacter.Health, healthFill, healthText, healthBar, true);
            RefreshResource(boundCharacter.Mana, manaFill, manaText, manaBar, false);
            RefreshBuffs();
        }

        private static Color ResolveNameColor(MMOEntityFaction faction, MMOUnitFrameTheme resolvedTheme)
        {
            if (resolvedTheme == null)
            {
                return Color.white;
            }

            return faction switch
            {
                MMOEntityFaction.Friendly => resolvedTheme.FriendlyNameColor,
                MMOEntityFaction.Hostile => resolvedTheme.HostileNameColor,
                MMOEntityFaction.Neutral => resolvedTheme.NeutralNameColor,
                _ => resolvedTheme.TextColor
            };
        }

        private void RefreshBuffs()
        {
            EnsureVisuals();
            int buffCount = boundBuffController != null ? boundBuffController.ActiveBuffs.Count : 0;
            EnsureBuffIconCount(buffCount);

            for (int i = 0; i < buffIcons.Count; i++)
            {
                bool active = i < buffCount;
                buffIcons[i].Root.gameObject.SetActive(active);
                if (!active)
                {
                    continue;
                }

                MMOActiveBuff buff = boundBuffController.ActiveBuffs[i];
                BuffIconView iconView = buffIcons[i];
                iconView.Icon.sprite = buff.Icon;
                iconView.Icon.color = buff.Icon != null
                    ? Color.white
                    : buff.IsHarmful
                        ? new Color(0.2f, 0.035f, 0.03f, 1f)
                        : new Color(0.16f, 0.11f, 0.06f, 1f);
                iconView.BorderBaseColor = buff.IsHarmful
                    ? new Color(0.92f, 0.12f, 0.08f, 1f)
                    : new Color(0.64f, 0.54f, 0.32f, 1f);
                iconView.Border.color = iconView.BorderBaseColor;
                iconView.Initial.text = buff.Icon == null ? GetInitial(buff.DisplayName) : string.Empty;
                iconView.Tooltip.Configure(boundBuffController, buff.BuffId);
                RefreshBuffTimer(iconView, buff);
            }
        }

        private void RefreshBuffTimers()
        {
            if (boundBuffController == null || buffIcons.Count == 0)
            {
                return;
            }

            int count = Mathf.Min(boundBuffController.ActiveBuffs.Count, buffIcons.Count);
            for (int i = 0; i < count; i++)
            {
                RefreshBuffTimer(buffIcons[i], boundBuffController.ActiveBuffs[i]);
            }
        }

        private static void RefreshBuffTimer(BuffIconView iconView, MMOActiveBuff buff)
        {
            iconView.Timer.text = FormatBuffTime(buff.RemainingSeconds);
            float alpha = buff.IsNearExpiry
                ? Mathf.Lerp(0.38f, 1f, Mathf.PingPong(Time.unscaledTime * 2.2f, 1f))
                : 1f;
            Color iconColor = iconView.Icon.color;
            iconView.Icon.color = new Color(iconColor.r, iconColor.g, iconColor.b, alpha);
            Color borderColor = iconView.BorderBaseColor;
            iconView.Border.color = new Color(borderColor.r, borderColor.g, borderColor.b, alpha);
        }

        private void EnsureBuffIconCount(int count)
        {
            while (buffIcons.Count < count)
            {
                buffIcons.Add(CreateBuffIcon(buffIcons.Count));
            }
        }

        private BuffIconView CreateBuffIcon(int index)
        {
            bool targetLayout = frameStyle == MMOUnitFrameStyle.Target;
            RectTransform root = CreateRect($"Buff {index + 1}", buffRoot);
            root.anchorMin = new Vector2(targetLayout ? 1f : 0f, 1f);
            root.anchorMax = root.anchorMin;
            root.pivot = new Vector2(targetLayout ? 1f : 0f, 1f);
            float x = index * (BuffSize + BuffSpacing);
            root.anchoredPosition = new Vector2(targetLayout ? -x : x, 0f);
            root.sizeDelta = new Vector2(BuffSize, BuffSize);

            Image border = CreateImage("Border", root, new Color(0.64f, 0.54f, 0.32f, 1f), false);
            Stretch(border.rectTransform);

            Image icon = CreateImage("Icon", root, new Color(0.16f, 0.11f, 0.06f, 1f), true);
            Stretch(icon.rectTransform);
            icon.rectTransform.offsetMin = new Vector2(2f, 2f);
            icon.rectTransform.offsetMax = new Vector2(-2f, -2f);

            Text initial = CreateText("Initial", root, 11, FontStyle.Bold, TextAnchor.MiddleCenter, true);
            Stretch(initial.rectTransform);

            Text timer = CreateText("Timer", root, 8, FontStyle.Bold, TextAnchor.LowerCenter, true);
            Stretch(timer.rectTransform);
            timer.rectTransform.offsetMin = Vector2.one;
            timer.rectTransform.offsetMax = Vector2.one * -1f;

            MMOBuffTooltipTrigger tooltip = root.gameObject.AddComponent<MMOBuffTooltipTrigger>();
            return new BuffIconView(root, icon, border, initial, timer, tooltip);
        }

        private void DestroyGeneratedChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name == "Target Cast Bar")
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        private void ClearVisualReferences()
        {
            frameArtwork = null;
            portraitImage = null;
            portraitInitialText = null;
            nameText = null;
            levelText = null;
            healthFill = null;
            healthText = null;
            healthBar = null;
            manaFill = null;
            manaText = null;
            manaBar = null;
            buffRoot = null;
            buffIcons.Clear();
        }

        private MMOUnitFrameTheme ResolveTheme()
        {
            if (theme != null)
            {
                return theme;
            }

            if (cachedTheme == null)
            {
                cachedTheme = Resources.Load<MMOUnitFrameTheme>(ThemeResourcePath);
            }

            return cachedTheme;
        }

        private MMOUnitFrameLayout CreateFallbackLayout()
        {
            bool party = frameStyle == MMOUnitFrameStyle.Party;
            bool target = frameStyle == MMOUnitFrameStyle.Target;
            return new MMOUnitFrameLayout().Configure(
                party ? new Vector2(250f, 68f) : new Vector2(320f, 96f),
                target ? MMOUnitFramePortraitSide.Right : MMOUnitFramePortraitSide.Left,
                party ? 62f : 88f,
                party ? 43f : 61f,
                party ? 2f : 3f,
                party ? 54f : 76f,
                party ? 8f : 10f,
                party ? 6f : 10f,
                party ? 6f : 10f,
                party ? 18f : 24f,
                party ? 17f : 22f,
                party ? 11f : 14f,
                2f,
                party ? 23f : 29f,
                party ? new Vector2(22f, -21f) : new Vector2(31f, -31f),
                party ? 13 : 15,
                party ? 9 : 11);
        }

        private static void RefreshResource(
            MMOCharacterResource resource,
            Image fill,
            Text valueText,
            RectTransform bar,
            bool alwaysVisible)
        {
            bool visible = alwaysVisible || resource.MaxValue > 0;
            bar.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }

            SetBarFill(fill, resource.Normalized);
            valueText.text = $"{resource.CurrentValue}/{resource.MaxValue}";
        }

        private static void SetBarFill(Image fill, float normalized)
        {
            RectTransform rectTransform = fill.rectTransform;
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = new Vector2(Mathf.Clamp01(normalized), 1f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private static string FormatBuffTime(float seconds)
        {
            if (seconds >= 60f)
            {
                return Mathf.CeilToInt(seconds / 60f) + "m";
            }

            return seconds >= 10f ? Mathf.CeilToInt(seconds).ToString() : seconds.ToString("0");
        }

        private static string GetInitial(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "?"
                : value.Trim()[0].ToString().ToUpperInvariant();
        }

        private static RectTransform CreateRect(string objectName, Transform parent)
        {
            GameObject child = new(objectName, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return (RectTransform)child.transform;
        }

        private static Image CreateImage(
            string objectName,
            Transform parent,
            Color color,
            bool raycastTarget)
        {
            RectTransform rectTransform = CreateRect(objectName, parent);
            Image image = rectTransform.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        private static Text CreateText(
            string objectName,
            Transform parent,
            int fontSize,
            FontStyle style,
            TextAnchor alignment,
            bool addShadow)
        {
            RectTransform rectTransform = CreateRect(objectName, parent);
            Text text = rectTransform.gameObject.AddComponent<Text>();
            text.font = GetFont(fontSize);
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;

            if (addShadow)
            {
                Shadow shadow = rectTransform.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
                shadow.effectDistance = new Vector2(1f, -1f);
                shadow.useGraphicAlpha = true;
            }

            return text;
        }

        private static Font GetFont(int size)
        {
            if (cachedFont != null)
            {
                return cachedFont;
            }

            cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (cachedFont == null)
            {
                cachedFont = Font.CreateDynamicFontFromOSFont(
                    new[] { "Arial", "Segoe UI", "Liberation Sans" },
                    size);
            }

            return cachedFont;
        }

        private static Image GetImage(Transform target)
        {
            return target != null ? target.GetComponent<Image>() : null;
        }

        private static Text GetText(Transform target)
        {
            return target != null ? target.GetComponent<Text>() : null;
        }

        private static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private sealed class BuffIconView
        {
            public readonly RectTransform Root;
            public readonly Image Icon;
            public readonly Image Border;
            public readonly Text Initial;
            public readonly Text Timer;
            public readonly MMOBuffTooltipTrigger Tooltip;
            public Color BorderBaseColor;

            public BuffIconView(
                RectTransform root,
                Image icon,
                Image border,
                Text initial,
                Text timer,
                MMOBuffTooltipTrigger tooltip)
            {
                Root = root;
                Icon = icon;
                Border = border;
                Initial = initial;
                Timer = timer;
                Tooltip = tooltip;
                BorderBaseColor = border != null ? border.color : Color.white;
            }
        }
    }
}
