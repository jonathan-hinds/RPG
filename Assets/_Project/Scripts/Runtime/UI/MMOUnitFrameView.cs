using System;
using System.Collections.Generic;
using RPGClone.Buffs;
using RPGClone.Characters;
using RPGClone.PlayerInteraction;
using RPGClone.Services;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace RPGClone.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class MMOUnitFrameView : MonoBehaviour, IPointerClickHandler
    {
        private const string ThemeResourcePath = "RPGClone/UI/UnitFrames/ClassicUnitFrameTheme";
        private const float BuffSize = 26f;
        private const float BuffSpacing = 4f;

        [Header("Binding")]
        [FormerlySerializedAs("autoBuild")]
        [SerializeField] private bool autoBind = true;
        [SerializeField] private bool hideWhenUnbound = true;

        [Header("Presentation")]
        [SerializeField] private MMOUnitFrameStyle frameStyle = MMOUnitFrameStyle.Player;
        [SerializeField] private MMOUnitFrameTheme theme;

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
        private bool authoredHierarchyBound;
        private bool bindingErrorLogged;
        private float nextBuffTimerRefreshAt;

        private static MMOUnitFrameTheme cachedTheme;
        private static Font cachedFont;

        public event Action<MMOUnitFrameView, MMOCharacterIdentity> Clicked;
        public event Action<MMOUnitFrameView, MMOCharacterIdentity, Vector2> RightClicked;

        public MMOCharacterIdentity BoundCharacter => boundCharacter;
        public MMOUnitFrameStyle FrameStyle => frameStyle;

        private void Awake()
        {
            if (autoBind)
            {
                EnsureAuthoredHierarchyBound();
            }

            Refresh();
        }

        private void OnEnable()
        {
            SubscribeToBoundCharacter();
            MMOPlayerInteractionState.Changed -= OnPlayerInteractionStateChanged;
            MMOPlayerInteractionState.Changed += OnPlayerInteractionStateChanged;
        }

        private void OnDisable()
        {
            MMOPlayerInteractionState.Changed -= OnPlayerInteractionStateChanged;
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
            frameStyle = style;
            if (unitFrameTheme != null)
            {
                theme = unitFrameTheme;
            }

            RebindAuthoredHierarchy(out _);
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
            if (eventData == null || boundCharacter == null)
            {
                return;
            }

            if (eventData.button == PointerEventData.InputButton.Left)
            {
                Clicked?.Invoke(this, boundCharacter);
            }
            else if (eventData.button == PointerEventData.InputButton.Right)
            {
                RightClicked?.Invoke(this, boundCharacter, eventData.position);
            }
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

        private void OnPlayerInteractionStateChanged()
        {
            if (boundCharacter == null || nameText == null)
            {
                return;
            }

            nameText.color = ResolveNameColor(boundCharacter, ResolveTheme());
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

        public bool RebindAuthoredHierarchy(out string bindingError)
        {
            authoredHierarchyBound = false;
            frameArtwork = FindNamedComponent<Image>("Backplate", transform, true);

            Transform portraitMask = FindNamedTransform("Portrait Mask", transform);
            portraitImage = FindNamedComponent<Image>("Portrait", portraitMask);
            portraitInitialText = FindNamedComponent<Text>("Portrait Initial", portraitMask);
            nameText = FindNamedComponent<Text>("Name", transform);
            levelText = FindNamedComponent<Text>("Level", transform);
            buffRoot = FindNamedTransform("Buffs", transform) as RectTransform;

            bool healthBound = TryBindBar(
                "Health Bar",
                out healthBar,
                out healthFill,
                out healthText);
            bool resourceBound = TryBindBar(
                "Resource Bar",
                out manaBar,
                out manaFill,
                out manaText);

            List<string> missing = new();
            AddMissing(missing, frameArtwork, "Backplate (Image)");
            AddMissing(missing, portraitImage, "Portrait (Image)");
            AddMissing(missing, portraitInitialText, "Portrait Initial (Text)");
            AddMissing(missing, nameText, "Name (Text)");
            AddMissing(missing, levelText, "Level (Text)");
            if (!healthBound)
            {
                missing.Add("Health Bar with Fill Area/Fill and Value");
            }

            if (!resourceBound)
            {
                missing.Add("Resource Bar with Fill Area/Fill and Value");
            }

            AddMissing(missing, buffRoot, "Buffs (RectTransform)");
            bindingError = string.Join(", ", missing);
            authoredHierarchyBound = missing.Count == 0;

            if (authoredHierarchyBound)
            {
                bindingErrorLogged = false;
                frameArtwork.raycastTarget = true;
            }

            return authoredHierarchyBound;
        }

        private bool EnsureAuthoredHierarchyBound()
        {
            if (authoredHierarchyBound
                && frameArtwork != null
                && portraitImage != null
                && portraitInitialText != null
                && nameText != null
                && levelText != null
                && healthFill != null
                && healthText != null
                && healthBar != null
                && manaFill != null
                && manaText != null
                && manaBar != null
                && buffRoot != null)
            {
                return true;
            }

            if (!autoBind)
            {
                return false;
            }

            bool bound = RebindAuthoredHierarchy(out string bindingError);
            if (!bound && !bindingErrorLogged)
            {
                Debug.LogError(
                    $"{name} cannot bind its authored unit-frame prefab hierarchy. Missing: "
                    + $"{bindingError}. Runtime frame generation is disabled so prefab appearance "
                    + "remains the single source of truth.",
                    this);
                bindingErrorLogged = true;
            }

            return bound;
        }

        private bool TryBindBar(
            string barName,
            out RectTransform bar,
            out Image fill,
            out Text valueText)
        {
            RectTransform[] candidates = GetComponentsInChildren<RectTransform>(true);
            foreach (RectTransform candidate in candidates)
            {
                if (candidate == transform || candidate.name != barName)
                {
                    continue;
                }

                Transform fillArea = FindNamedTransform("Fill Area", candidate);
                Image candidateFill = FindNamedComponent<Image>("Fill", fillArea);
                Text candidateValue = FindNamedComponent<Text>("Value", candidate);
                if (fillArea == null || candidateFill == null || candidateValue == null)
                {
                    continue;
                }

                bar = candidate;
                fill = candidateFill;
                valueText = candidateValue;
                return true;
            }

            bar = null;
            fill = null;
            valueText = null;
            return false;
        }

        private static Transform FindNamedTransform(string objectName, Transform searchRoot)
        {
            if (searchRoot == null)
            {
                return null;
            }

            foreach (Transform candidate in searchRoot.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name == objectName)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static T FindNamedComponent<T>(
            string objectName,
            Transform searchRoot,
            bool preferDirectChild = false)
            where T : Component
        {
            if (searchRoot == null)
            {
                return null;
            }

            if (preferDirectChild)
            {
                Transform directChild = searchRoot.Find(objectName);
                T directComponent = directChild != null ? directChild.GetComponent<T>() : null;
                if (directComponent != null)
                {
                    return directComponent;
                }
            }

            foreach (T candidate in searchRoot.GetComponentsInChildren<T>(true))
            {
                if (candidate.name == objectName)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static void AddMissing<T>(ICollection<string> missing, T value, string label)
            where T : UnityEngine.Object
        {
            if (value == null)
            {
                missing.Add(label);
            }
        }

        private void Refresh()
        {
            if (!EnsureAuthoredHierarchyBound())
            {
                return;
            }

            bool hasCharacter = boundCharacter != null;
            gameObject.SetActive(hasCharacter || !hideWhenUnbound);
            if (!hasCharacter)
            {
                return;
            }

            MMOUnitFrameTheme resolvedTheme = ResolveTheme();
            nameText.text = boundCharacter.DisplayName;
            nameText.color = ResolveNameColor(boundCharacter, resolvedTheme);
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

        private static Color ResolveNameColor(MMOCharacterIdentity character, MMOUnitFrameTheme resolvedTheme)
        {
            if (resolvedTheme == null || character == null)
            {
                return Color.white;
            }

            MMOCharacterIdentity localPlayer = MMOGameplaySessionService.LocalPlayer.Identity;
            if (localPlayer != null && MMOFactionRules.CanDamage(localPlayer, character))
            {
                return resolvedTheme.HostileNameColor;
            }

            if (character != localPlayer && MMOGameplaySessionService.Players.Contains(character))
            {
                return resolvedTheme.FriendlyNameColor;
            }

            return character.Faction switch
            {
                MMOEntityFaction.Friendly => resolvedTheme.FriendlyNameColor,
                MMOEntityFaction.Hostile => resolvedTheme.HostileNameColor,
                MMOEntityFaction.Neutral => resolvedTheme.NeutralNameColor,
                _ => resolvedTheme.TextColor
            };
        }

        private void RefreshBuffs()
        {
            if (!EnsureAuthoredHierarchyBound())
            {
                return;
            }
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
