using System.Collections.Generic;
using RPGClone.Buffs;
using RPGClone.Characters;
using UnityEngine;
using UnityEngine.UI;

namespace RPGClone.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class MMOUnitFrameView : MonoBehaviour
    {
        [Header("Binding")]
        [SerializeField] private bool autoBuild = true;
        [SerializeField] private bool hideWhenUnbound = true;

        [Header("Colors")]
        [SerializeField] private Color frameColor = new(0.05f, 0.05f, 0.055f, 0.92f);
        [SerializeField] private Color borderColor = new(0.72f, 0.63f, 0.42f, 1f);
        [SerializeField] private Color healthColor = new(0.08f, 0.62f, 0.14f, 1f);
        [SerializeField] private Color manaColor = new(0.05f, 0.28f, 0.82f, 1f);
        [SerializeField] private Color emptyBarColor = new(0.02f, 0.02f, 0.025f, 1f);

        private const float PortraitSize = 56f;
        private const float Padding = 6f;
        private const float BarHeight = 18f;
        private const float BuffSize = 26f;
        private const float BuffSpacing = 4f;

        private Image background;
        private Image portraitImage;
        private Text portraitInitialText;
        private Text nameText;
        private Text levelText;
        private Image healthFill;
        private Text healthText;
        private Image manaFill;
        private Text manaText;
        private RectTransform buffRoot;
        private readonly List<BuffIconView> buffIcons = new();
        private MMOCharacterIdentity boundCharacter;
        private MMOCharacterBuffController boundBuffController;
        private bool subscribedToBoundCharacter;

        private static Font cachedFont;

        private void Awake()
        {
            if (autoBuild)
            {
                BuildIfNeeded();
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
            RefreshBuffTimers();
        }

        public void Bind(MMOCharacterIdentity character)
        {
            if (boundCharacter == character)
            {
                Refresh();
                return;
            }

            if (boundCharacter != null)
            {
                UnsubscribeFromBoundCharacter();
            }

            boundCharacter = character;
            boundBuffController = boundCharacter != null ? boundCharacter.GetComponent<MMOCharacterBuffController>() : null;

            Refresh();
            SubscribeToBoundCharacter();
        }

        public void Clear()
        {
            Bind(null);
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

        private void BuildIfNeeded()
        {
            if (background != null)
            {
                return;
            }

            if (TryBindExistingHierarchy())
            {
                return;
            }

            RectTransform root = (RectTransform)transform;
            if (root.sizeDelta == Vector2.zero)
            {
                root.sizeDelta = new Vector2(270f, 76f);
            }

            background = CreateImage("Frame Background", transform, frameColor);
            Stretch(background.rectTransform);
            background.rectTransform.offsetMin = Vector2.one * 2f;
            background.rectTransform.offsetMax = Vector2.one * -2f;

            Image border = CreateImage("Frame Border", transform, borderColor);
            Stretch(border.rectTransform);
            border.transform.SetAsFirstSibling();

            RectTransform portrait = CreateRect("Portrait", transform);
            portrait.anchorMin = new Vector2(0f, 0.5f);
            portrait.anchorMax = new Vector2(0f, 0.5f);
            portrait.pivot = new Vector2(0f, 0.5f);
            portrait.anchoredPosition = new Vector2(Padding, 0f);
            portrait.sizeDelta = new Vector2(PortraitSize, PortraitSize);
            portraitImage = portrait.gameObject.AddComponent<Image>();
            portraitImage.raycastTarget = false;

            portraitInitialText = CreateText("Portrait Initial", portrait, 22, FontStyle.Bold, TextAnchor.MiddleCenter);
            Stretch(portraitInitialText.rectTransform);

            RectTransform content = CreateRect("Content", transform);
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(1f, 1f);
            content.offsetMin = new Vector2(Padding + PortraitSize + Padding, Padding);
            content.offsetMax = new Vector2(-Padding, -Padding);

            nameText = CreateText("Name", content, 15, FontStyle.Bold, TextAnchor.UpperLeft);
            nameText.rectTransform.anchorMin = new Vector2(0f, 1f);
            nameText.rectTransform.anchorMax = new Vector2(1f, 1f);
            nameText.rectTransform.pivot = new Vector2(0f, 1f);
            nameText.rectTransform.anchoredPosition = Vector2.zero;
            nameText.rectTransform.sizeDelta = new Vector2(0f, 20f);

            RectTransform healthBar = CreateBar("Health Bar", content, 0f, out healthFill, out healthText);
            healthBar.anchorMin = new Vector2(0f, 0.5f);
            healthBar.anchorMax = new Vector2(1f, 0.5f);
            healthBar.pivot = new Vector2(0.5f, 0.5f);
            healthBar.anchoredPosition = new Vector2(0f, -2f);

            RectTransform manaBar = CreateBar("Mana Bar", content, 0f, out manaFill, out manaText);
            manaBar.anchorMin = new Vector2(0f, 0f);
            manaBar.anchorMax = new Vector2(1f, 0f);
            manaBar.pivot = new Vector2(0.5f, 0f);
            manaBar.anchoredPosition = Vector2.zero;

            healthFill.color = healthColor;
            manaFill.color = manaColor;

            Image levelBackground = CreateImage("Level Badge", transform, new Color(0.02f, 0.02f, 0.025f, 0.95f));
            levelBackground.rectTransform.anchorMin = new Vector2(0f, 0f);
            levelBackground.rectTransform.anchorMax = new Vector2(0f, 0f);
            levelBackground.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            levelBackground.rectTransform.anchoredPosition = new Vector2(Padding + PortraitSize, Padding);
            levelBackground.rectTransform.sizeDelta = new Vector2(28f, 22f);

            levelText = CreateText("Level", transform, 13, FontStyle.Bold, TextAnchor.MiddleCenter);
            levelText.rectTransform.anchorMin = new Vector2(0f, 0f);
            levelText.rectTransform.anchorMax = new Vector2(0f, 0f);
            levelText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            levelText.rectTransform.anchoredPosition = new Vector2(Padding + PortraitSize, Padding);
            levelText.rectTransform.sizeDelta = new Vector2(28f, 22f);

            buffRoot = CreateRect("Buffs", transform);
            buffRoot.anchorMin = new Vector2(0f, 0f);
            buffRoot.anchorMax = new Vector2(0f, 0f);
            buffRoot.pivot = new Vector2(0f, 1f);
            buffRoot.anchoredPosition = new Vector2(Padding, -4f);
            buffRoot.sizeDelta = new Vector2(270f, BuffSize);
        }

        private bool TryBindExistingHierarchy()
        {
            Transform content = transform.Find("Content");
            Transform portrait = transform.Find("Portrait");
            Transform healthBar = content != null ? content.Find("Health Bar") : null;
            Transform manaBar = content != null ? content.Find("Mana Bar") : null;

            background = GetImage(transform.Find("Frame Background"));
            portraitImage = GetImage(portrait);
            portraitInitialText = GetText(portrait != null ? portrait.Find("Portrait Initial") : null);
            nameText = GetText(content != null ? content.Find("Name") : null);
            levelText = GetText(transform.Find("Level"));
            healthFill = GetImage(healthBar != null ? healthBar.Find("Fill") : null);
            healthText = GetText(healthBar != null ? healthBar.Find("Value") : null);
            manaFill = GetImage(manaBar != null ? manaBar.Find("Fill") : null);
            manaText = GetText(manaBar != null ? manaBar.Find("Value") : null);
            buffRoot = transform.Find("Buffs") as RectTransform;
            if (buffRoot == null)
            {
                buffRoot = CreateRect("Buffs", transform);
                buffRoot.anchorMin = new Vector2(0f, 0f);
                buffRoot.anchorMax = new Vector2(0f, 0f);
                buffRoot.pivot = new Vector2(0f, 1f);
                buffRoot.anchoredPosition = new Vector2(Padding, -4f);
                buffRoot.sizeDelta = new Vector2(270f, BuffSize);
            }

            return background != null
                && portraitImage != null
                && portraitInitialText != null
                && nameText != null
                && levelText != null
                && healthFill != null
                && healthText != null
                && manaFill != null
                && manaText != null;
        }

        private static Image GetImage(Transform target)
        {
            return target != null ? target.GetComponent<Image>() : null;
        }

        private static Text GetText(Transform target)
        {
            return target != null ? target.GetComponent<Text>() : null;
        }

        private RectTransform CreateBar(string objectName, Transform parent, float y, out Image fill, out Text valueText)
        {
            RectTransform root = CreateRect(objectName, parent);
            root.sizeDelta = new Vector2(0f, BarHeight);
            root.anchoredPosition = new Vector2(0f, y);

            Image backgroundImage = root.gameObject.AddComponent<Image>();
            backgroundImage.color = emptyBarColor;
            backgroundImage.raycastTarget = false;

            fill = CreateImage("Fill", root, Color.white);
            Stretch(fill.rectTransform);
            fill.type = Image.Type.Simple;
            fill.fillAmount = 1f;

            valueText = CreateText("Value", root, 11, FontStyle.Bold, TextAnchor.MiddleCenter);
            Stretch(valueText.rectTransform);
            return root;
        }

        private void Refresh()
        {
            BuildIfNeeded();

            bool hasCharacter = boundCharacter != null;
            gameObject.SetActive(hasCharacter || !hideWhenUnbound);
            if (!hasCharacter)
            {
                return;
            }

            nameText.text = boundCharacter.DisplayName;
            levelText.text = boundCharacter.Level.ToString();
            portraitImage.sprite = boundCharacter.Portrait;
            portraitImage.color = boundCharacter.Portrait != null ? Color.white : boundCharacter.PortraitTint;
            portraitInitialText.text = GetInitial(boundCharacter.DisplayName);
            portraitInitialText.enabled = boundCharacter.Portrait == null;

            RefreshResource(boundCharacter.Health, healthFill, healthText);
            RefreshResource(boundCharacter.Mana, manaFill, manaText);
            RefreshBuffs();
        }

        private void RefreshBuffs()
        {
            BuildIfNeeded();
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
                    : buff.IsHarmful ? new Color(0.2f, 0.035f, 0.03f, 1f) : new Color(0.16f, 0.11f, 0.06f, 1f);
                iconView.BorderBaseColor = buff.IsHarmful ? new Color(0.92f, 0.12f, 0.08f, 1f) : new Color(0.72f, 0.63f, 0.42f, 1f);
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
            float alpha = buff.IsNearExpiry ? Mathf.Lerp(0.38f, 1f, Mathf.PingPong(Time.unscaledTime * 2.2f, 1f)) : 1f;
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
            RectTransform root = CreateRect($"Buff {index + 1}", buffRoot);
            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = new Vector2(index * (BuffSize + BuffSpacing), 0f);
            root.sizeDelta = new Vector2(BuffSize, BuffSize);

            Image border = CreateImage("Border", root, new Color(0.72f, 0.63f, 0.42f, 1f));
            Stretch(border.rectTransform);

            Image icon = CreateImage("Icon", root, new Color(0.16f, 0.11f, 0.06f, 1f));
            icon.rectTransform.anchorMin = Vector2.zero;
            icon.rectTransform.anchorMax = Vector2.one;
            icon.rectTransform.offsetMin = new Vector2(2f, 2f);
            icon.rectTransform.offsetMax = new Vector2(-2f, -2f);
            icon.raycastTarget = true;

            Text initial = CreateText("Initial", root, 11, FontStyle.Bold, TextAnchor.MiddleCenter);
            Stretch(initial.rectTransform);

            Text timer = CreateText("Timer", root, 8, FontStyle.Bold, TextAnchor.LowerCenter);
            Stretch(timer.rectTransform);
            timer.rectTransform.offsetMin = new Vector2(1f, 1f);
            timer.rectTransform.offsetMax = new Vector2(-1f, -1f);
            timer.color = Color.white;

            MMOBuffTooltipTrigger tooltip = root.gameObject.AddComponent<MMOBuffTooltipTrigger>();
            return new BuffIconView(root, icon, border, initial, timer, tooltip);
        }

        private static string FormatBuffTime(float seconds)
        {
            if (seconds >= 60f)
            {
                return Mathf.CeilToInt(seconds / 60f) + "m";
            }

            return seconds >= 10f ? Mathf.CeilToInt(seconds).ToString() : seconds.ToString("0");
        }

        private static void RefreshResource(MMOCharacterResource resource, Image fill, Text valueText)
        {
            SetBarFill(fill, resource.Normalized);
            valueText.text = $"{resource.CurrentValue}/{resource.MaxValue}";
        }

        private static void SetBarFill(Image fill, float normalized)
        {
            float clampedValue = Mathf.Clamp01(normalized);
            fill.type = Image.Type.Simple;
            fill.fillAmount = 1f;

            RectTransform rectTransform = fill.rectTransform;
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = new Vector2(clampedValue, 1f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private static string GetInitial(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "?" : value.Trim()[0].ToString().ToUpperInvariant();
        }

        private static RectTransform CreateRect(string objectName, Transform parent)
        {
            GameObject child = new(objectName, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return (RectTransform)child.transform;
        }

        private static Image CreateImage(string objectName, Transform parent, Color color)
        {
            RectTransform rectTransform = CreateRect(objectName, parent);
            Image image = rectTransform.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text CreateText(string objectName, Transform parent, int fontSize, FontStyle style, TextAnchor alignment)
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
                cachedFont = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Segoe UI", "Liberation Sans" }, size);
            }

            return cachedFont;
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

            public BuffIconView(RectTransform root, Image icon, Image border, Text initial, Text timer, MMOBuffTooltipTrigger tooltip)
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
