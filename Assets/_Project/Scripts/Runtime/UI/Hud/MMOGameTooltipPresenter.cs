using System;
using System.Collections.Generic;
using RPGClone.Abilities;
using RPGClone.Buffs;
using RPGClone.Characters;
using RPGClone.Inventory;
using RPGClone.Quests;
using RPGClone.Services;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RPGClone.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class MMOGameTooltipPresenter : MonoBehaviour
    {
        [SerializeField] private Vector2 cursorOffset = new(18f, -12f);
        [SerializeField] private MMOTooltipTheme theme;

        private readonly List<RenderedLine> renderedLines = new();
        private RectTransform root;
        private RectTransform contentRoot;
        private Canvas canvas;
        private Vector2 lastScreenPosition;
        private Func<MMOTooltipContent> liveContentProvider;
        private float nextLiveRefreshTime;
        private int displayedContentHash;

        public static MMOGameTooltipPresenter Instance { get; private set; }
        public Vector2 CurrentSize => root != null ? root.sizeDelta : Vector2.zero;

        private void Awake()
        {
            Instance = this;
            canvas = GetComponentInParent<Canvas>();
            theme ??= MMOTooltipTheme.LoadDefault();
            BuildIfNeeded();
            Hide();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (!gameObject.activeSelf)
            {
                return;
            }

            FollowCursor();
            if (liveContentProvider == null || Time.unscaledTime < nextLiveRefreshTime)
            {
                return;
            }

            MMOTooltipContent content = liveContentProvider.Invoke();
            if (content == null)
            {
                Hide();
                return;
            }

            nextLiveRefreshTime = Time.unscaledTime + 0.1f;
            int contentHash = content.CalculateContentHash();
            if (contentHash == displayedContentHash)
            {
                return;
            }

            BuildContent(content);
            ResizeToContent();
            SetPosition(lastScreenPosition);
            displayedContentHash = contentHash;
        }

        public static void ShowItem(MMOItemDefinition item, Vector2 screenPosition)
        {
            MMOQuestLog questLog = null;
            MMOGameplaySessionService.LocalPlayer.TryGetComponent(out questLog);
            MMOCharacterIdentity viewer = MMOGameplaySessionService.LocalPlayer.Identity;
            Func<MMOTooltipContent> provider = () => MMOTooltipContentBuilder.BuildItem(
                item,
                questLog,
                viewerLevel: viewer != null ? viewer.Level : null);
            ResolvePresenter()?.Show(
                provider.Invoke(),
                screenPosition,
                provider);
        }

        public static void ShowAbility(MMOAbilityDefinition ability, Vector2 screenPosition)
        {
            Func<MMOTooltipContent> provider = () =>
                MMOTooltipContentBuilder.BuildAbility(
                    ability,
                    MMOGameplaySessionService.LocalPlayer.Identity);
            ResolvePresenter()?.Show(provider.Invoke(), screenPosition, provider);
        }

        public static void ShowBuff(
            MMOCharacterBuffController controller,
            string buffId,
            Vector2 screenPosition)
        {
            if (controller == null || string.IsNullOrWhiteSpace(buffId))
            {
                ResolvePresenter()?.Hide();
                return;
            }

            Func<MMOTooltipContent> provider = () =>
            {
                MMOActiveBuff buff = controller.FindBuff(buffId);
                return MMOTooltipContentBuilder.BuildBuff(buff);
            };
            ResolvePresenter()?.Show(provider.Invoke(), screenPosition, provider);
        }

        public static void ShowContent(
            MMOTooltipContent content,
            Vector2 screenPosition,
            Func<MMOTooltipContent> liveProvider = null)
        {
            ResolvePresenter()?.Show(content, screenPosition, liveProvider);
        }

        public static void HideTooltip()
        {
            MMOGameTooltipPresenter presenter = Instance != null
                ? Instance
                : FindAnyObjectByType<MMOGameTooltipPresenter>();
            presenter?.Hide();
        }

        public void ConfigureTheme(MMOTooltipTheme tooltipTheme)
        {
            if (tooltipTheme == null || tooltipTheme == theme)
            {
                return;
            }

            theme = tooltipTheme;
            ApplyPanelStyle();
        }

        public void Show(
            MMOTooltipContent content,
            Vector2 screenPosition,
            Func<MMOTooltipContent> liveProvider)
        {
            if (content == null)
            {
                Hide();
                return;
            }

            theme ??= MMOTooltipTheme.LoadDefault();
            BuildIfNeeded();
            liveContentProvider = liveProvider;
            lastScreenPosition = screenPosition;
            gameObject.SetActive(true);
            BuildContent(content);
            ResizeToContent();
            SetPosition(screenPosition);
            transform.SetAsLastSibling();
            displayedContentHash = content.CalculateContentHash();
            nextLiveRefreshTime = Time.unscaledTime + 0.1f;
        }

        public void Hide()
        {
            liveContentProvider = null;
            displayedContentHash = 0;
            gameObject.SetActive(false);
        }

        private static MMOGameTooltipPresenter ResolvePresenter()
        {
            if (Instance != null)
            {
                return Instance;
            }

            MMOGameTooltipPresenter presenter = FindAnyObjectByType<MMOGameTooltipPresenter>();
            if (presenter != null)
            {
                return presenter;
            }

            Canvas targetCanvas = FindAnyObjectByType<Canvas>();
            if (targetCanvas == null)
            {
                return null;
            }

            GameObject tooltipObject = new("Game Tooltip", typeof(RectTransform));
            tooltipObject.transform.SetParent(targetCanvas.transform, false);
            return tooltipObject.AddComponent<MMOGameTooltipPresenter>();
        }

        private void BuildIfNeeded()
        {
            if (root != null)
            {
                return;
            }

            theme ??= MMOTooltipTheme.LoadDefault();
            root = (RectTransform)transform;
            root.sizeDelta = new Vector2(theme.MinimumWidth, 72f);
            ApplyPanelStyle();

            contentRoot = MMOUiFactory.CreateRect("Content", transform);
            contentRoot.anchorMin = Vector2.zero;
            contentRoot.anchorMax = Vector2.one;
            contentRoot.offsetMin = new Vector2(theme.HorizontalPadding, theme.VerticalPadding);
            contentRoot.offsetMax = new Vector2(-theme.HorizontalPadding, -theme.VerticalPadding);

            VerticalLayoutGroup layout = contentRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.spacing = 0f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        private void ApplyPanelStyle()
        {
            Image background = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            background.raycastTarget = false;
            background.sprite = theme.PanelSprite;
            background.type = theme.PanelSprite != null && theme.PanelSprite.border.sqrMagnitude > 0f
                ? Image.Type.Sliced
                : Image.Type.Simple;
            background.color = theme.PanelSprite != null ? Color.white : theme.FallbackBackground;

            Shadow shadow = gameObject.GetComponent<Shadow>() ?? gameObject.AddComponent<Shadow>();
            shadow.enabled = theme.PanelSprite == null;
            shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
            shadow.effectDistance = new Vector2(1f, -1f);
            shadow.useGraphicAlpha = true;

            Outline outline = gameObject.GetComponent<Outline>();
            if (theme.PanelSprite == null)
            {
                outline ??= gameObject.AddComponent<Outline>();
                outline.effectColor = theme.FallbackBorder;
                outline.effectDistance = new Vector2(1f, -1f);
            }
            else if (outline != null)
            {
                outline.enabled = false;
            }
        }

        private void BuildContent(MMOTooltipContent content)
        {
            MMOUiFactory.DestroyChildren(contentRoot);
            renderedLines.Clear();
            AddLine(new MMOTooltipLine(
                content.Title,
                string.Empty,
                theme.TitleFontSize,
                FontStyle.Normal,
                content.TitleColor,
                0f));
            foreach (MMOTooltipLine line in content.Lines)
            {
                AddLine(line);
            }
        }

        private void AddLine(MMOTooltipLine line)
        {
            if (string.IsNullOrWhiteSpace(line.Text) && string.IsNullOrWhiteSpace(line.RightText))
            {
                return;
            }

            RectTransform row = MMOUiFactory.CreateRect("Tooltip Line", contentRoot);
            LayoutElement layout = row.gameObject.AddComponent<LayoutElement>();
            Text left = CreateLineText("Left", row, line.Text, line.FontSize, line.Style, line.Color);
            Text right = null;

            if (string.IsNullOrWhiteSpace(line.RightText))
            {
                MMOUiFactory.Stretch(left.rectTransform);
            }
            else
            {
                right = CreateLineText(
                    "Right",
                    row,
                    line.RightText,
                    line.FontSize,
                    line.Style,
                    line.Color);
                left.alignment = TextAnchor.UpperLeft;
                right.alignment = TextAnchor.UpperRight;
            }

            renderedLines.Add(new RenderedLine(row, layout, left, right, line.SpacingBefore));
        }

        private static Text CreateLineText(
            string objectName,
            Transform parent,
            string value,
            int fontSize,
            FontStyle style,
            Color color)
        {
            Text text = MMOUiFactory.CreateText(
                objectName,
                parent,
                fontSize,
                style,
                TextAnchor.UpperLeft);
            text.text = value ?? string.Empty;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private void ResizeToContent()
        {
            float desiredContentWidth = 0f;
            foreach (RenderedLine line in renderedLines)
            {
                float width = CalculatePreferredWidth(line.Left);
                if (line.Right != null)
                {
                    width += theme.DoubleLineGap + CalculatePreferredWidth(line.Right);
                }

                desiredContentWidth = Mathf.Max(desiredContentWidth, width);
            }

            float widthWithPadding = desiredContentWidth + theme.HorizontalPadding * 2f;
            float resolvedWidth = Mathf.Clamp(
                widthWithPadding,
                theme.MinimumWidth,
                theme.MaximumWidth);
            root.sizeDelta = new Vector2(resolvedWidth, root.sizeDelta.y);

            float contentWidth = resolvedWidth - theme.HorizontalPadding * 2f;
            float totalHeight = theme.VerticalPadding * 2f;
            for (int i = 0; i < renderedLines.Count; i++)
            {
                RenderedLine line = renderedLines[i];
                float availableWidth = contentWidth;
                if (line.Right != null)
                {
                    float leftPreferred = CalculatePreferredWidth(line.Left);
                    float rightPreferred = CalculatePreferredWidth(line.Right);
                    float combined = Mathf.Max(1f, leftPreferred + rightPreferred);
                    float leftRatio = Mathf.Clamp(leftPreferred / combined, 0.42f, 0.65f);
                    float gapHalf = theme.DoubleLineGap * 0.5f;

                    line.Left.rectTransform.anchorMin = Vector2.zero;
                    line.Left.rectTransform.anchorMax = new Vector2(leftRatio, 1f);
                    line.Left.rectTransform.offsetMin = Vector2.zero;
                    line.Left.rectTransform.offsetMax = new Vector2(-gapHalf, 0f);

                    line.Right.rectTransform.anchorMin = new Vector2(leftRatio, 0f);
                    line.Right.rectTransform.anchorMax = Vector2.one;
                    line.Right.rectTransform.offsetMin = new Vector2(gapHalf, 0f);
                    line.Right.rectTransform.offsetMax = Vector2.zero;

                    float leftWidth = contentWidth * leftRatio - gapHalf;
                    float rightWidth = contentWidth * (1f - leftRatio) - gapHalf;
                    availableWidth = Mathf.Max(1f, leftWidth);
                    float height = Mathf.Max(
                        CalculateTextHeight(line.Left, availableWidth),
                        CalculateTextHeight(line.Right, Mathf.Max(1f, rightWidth)));
                    SetLineHeight(line, height);
                }
                else
                {
                    SetLineHeight(line, CalculateTextHeight(line.Left, availableWidth));
                }

                float spacing = i == 0
                    ? 0f
                    : Mathf.Max(theme.LineSpacing, line.SpacingBefore);
                ApplyTopSpacing(line.Left.rectTransform, spacing);
                if (line.Right != null)
                {
                    ApplyTopSpacing(line.Right.rectTransform, spacing);
                }

                line.Layout.preferredHeight += spacing;
                totalHeight += line.Layout.preferredHeight;
            }

            root.sizeDelta = new Vector2(
                resolvedWidth,
                Mathf.Clamp(totalHeight, 48f, theme.MaximumHeight));
            Canvas.ForceUpdateCanvases();
        }

        private static void SetLineHeight(RenderedLine line, float textHeight)
        {
            line.Layout.minHeight = textHeight;
            line.Layout.preferredHeight = textHeight;
        }

        private static void ApplyTopSpacing(RectTransform rect, float spacing)
        {
            Vector2 offsetMax = rect.offsetMax;
            offsetMax.y = -spacing;
            rect.offsetMax = offsetMax;
        }

        private static float CalculatePreferredWidth(Text text)
        {
            if (text == null || string.IsNullOrEmpty(text.text))
            {
                return 0f;
            }

            TextGenerationSettings settings = text.GetGenerationSettings(Vector2.zero);
            return Mathf.Ceil(text.cachedTextGeneratorForLayout.GetPreferredWidth(text.text, settings) / text.pixelsPerUnit);
        }

        private static float CalculateTextHeight(Text text, float width)
        {
            if (text == null || string.IsNullOrEmpty(text.text))
            {
                return 0f;
            }

            TextGenerationSettings settings = text.GetGenerationSettings(new Vector2(width, 0f));
            return Mathf.Ceil(
                text.cachedTextGeneratorForLayout.GetPreferredHeight(text.text, settings)
                / text.pixelsPerUnit) + 1f;
        }

        private void FollowCursor()
        {
            SetPosition(GetCurrentPointerPosition());
        }

        private void SetPosition(Vector2 screenPosition)
        {
            canvas ??= GetComponentInParent<Canvas>();
            RectTransform canvasRect = canvas != null ? (RectTransform)canvas.transform : null;
            if (canvasRect == null)
            {
                return;
            }

            if (screenPosition == Vector2.zero && lastScreenPosition != Vector2.zero)
            {
                screenPosition = lastScreenPosition;
            }

            lastScreenPosition = screenPosition;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPosition,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                out Vector2 localPosition);

            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = ClampToCanvas(localPosition + cursorOffset, canvasRect);
        }

        private static Vector2 GetCurrentPointerPosition()
        {
            Mouse mouse = Mouse.current;
            return mouse != null ? mouse.position.ReadValue() : (Vector2)Input.mousePosition;
        }

        private Vector2 ClampToCanvas(Vector2 position, RectTransform canvasRect)
        {
            Rect rect = canvasRect.rect;
            Vector2 size = root.sizeDelta;
            float padding = theme.HorizontalPadding;
            position.x = Mathf.Clamp(position.x, rect.xMin + padding, rect.xMax - size.x - padding);
            position.y = Mathf.Clamp(position.y, rect.yMin + size.y + padding, rect.yMax - padding);
            return position;
        }

        private sealed class RenderedLine
        {
            public RenderedLine(
                RectTransform row,
                LayoutElement layout,
                Text left,
                Text right,
                float spacingBefore)
            {
                Row = row;
                Layout = layout;
                Left = left;
                Right = right;
                SpacingBefore = spacingBefore;
            }

            public RectTransform Row { get; }
            public LayoutElement Layout { get; }
            public Text Left { get; }
            public Text Right { get; }
            public float SpacingBefore { get; }
        }
    }

    public sealed class MMOTooltipContent
    {
        private readonly List<MMOTooltipLine> lines = new();

        public MMOTooltipContent(string title, Color titleColor)
        {
            Title = title;
            TitleColor = titleColor;
        }

        public string Title { get; }
        public Color TitleColor { get; }
        public IReadOnlyList<MMOTooltipLine> Lines => lines;

        public int CalculateContentHash()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (Title != null ? Title.GetHashCode() : 0);
                hash = hash * 31 + TitleColor.GetHashCode();
                foreach (MMOTooltipLine line in lines)
                {
                    hash = hash * 31 + (line.Text != null ? line.Text.GetHashCode() : 0);
                    hash = hash * 31 + (line.RightText != null ? line.RightText.GetHashCode() : 0);
                    hash = hash * 31 + line.FontSize;
                    hash = hash * 31 + (int)line.Style;
                    hash = hash * 31 + line.Color.GetHashCode();
                    hash = hash * 31 + line.SpacingBefore.GetHashCode();
                }

                return hash;
            }
        }

        public void Add(
            string text,
            int fontSize,
            FontStyle style,
            Color color,
            float spacingBefore = 0f)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                lines.Add(new MMOTooltipLine(
                    text,
                    string.Empty,
                    fontSize,
                    style,
                    color,
                    spacingBefore));
            }
        }

        public void AddDouble(
            string leftText,
            string rightText,
            int fontSize,
            FontStyle style,
            Color color,
            float spacingBefore = 0f)
        {
            if (!string.IsNullOrWhiteSpace(leftText) || !string.IsNullOrWhiteSpace(rightText))
            {
                lines.Add(new MMOTooltipLine(
                    leftText,
                    rightText,
                    fontSize,
                    style,
                    color,
                    spacingBefore));
            }
        }
    }

    public readonly struct MMOTooltipLine
    {
        public MMOTooltipLine(
            string text,
            int fontSize,
            FontStyle style,
            Color color)
            : this(text, string.Empty, fontSize, style, color, 0f)
        {
        }

        public MMOTooltipLine(
            string text,
            string rightText,
            int fontSize,
            FontStyle style,
            Color color,
            float spacingBefore)
        {
            Text = text;
            RightText = rightText;
            FontSize = fontSize;
            Style = style;
            Color = color;
            SpacingBefore = Mathf.Max(0f, spacingBefore);
        }

        public string Text { get; }
        public string RightText { get; }
        public int FontSize { get; }
        public FontStyle Style { get; }
        public Color Color { get; }
        public float SpacingBefore { get; }
    }

    public sealed class MMOBuffTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private MMOCharacterBuffController controller;
        private string buffId;

        public void Configure(MMOCharacterBuffController newController, string newBuffId)
        {
            controller = newController;
            buffId = newBuffId;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            MMOGameTooltipPresenter.ShowBuff(controller, buffId, eventData.position);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            MMOGameTooltipPresenter.HideTooltip();
        }

        private void OnDisable()
        {
            MMOGameTooltipPresenter.HideTooltip();
        }
    }
}
