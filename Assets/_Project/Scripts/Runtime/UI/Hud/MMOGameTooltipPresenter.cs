using System;
using System.Collections.Generic;
using RPGClone.Abilities;
using RPGClone.Buffs;
using RPGClone.Characters;
using RPGClone.Inventory;
using RPGClone.Quests;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RPGClone.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class MMOGameTooltipPresenter : MonoBehaviour
    {
        private const float Padding = 10f;
        private const float Width = 320f;

        [SerializeField] private Vector2 cursorOffset = new(18f, -12f);

        private RectTransform root;
        private RectTransform contentRoot;
        private Canvas canvas;
        private Vector2 lastScreenPosition;
        private Func<MMOTooltipContent> liveContentProvider;
        private float nextLiveRefreshTime;

        public static MMOGameTooltipPresenter Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            canvas = GetComponentInParent<Canvas>();
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
            if (liveContentProvider != null && Time.unscaledTime >= nextLiveRefreshTime)
            {
                MMOTooltipContent content = liveContentProvider.Invoke();
                if (content == null)
                {
                    Hide();
                    return;
                }

                BuildContent(content);
                ResizeToContent();
                nextLiveRefreshTime = Time.unscaledTime + 0.1f;
            }
        }

        public static void ShowItem(MMOItemDefinition item, Vector2 screenPosition)
        {
            ResolvePresenter()?.Show(BuildItemContent(item), screenPosition, null);
        }

        public static void ShowAbility(MMOAbilityDefinition ability, Vector2 screenPosition)
        {
            ResolvePresenter()?.Show(BuildAbilityContent(ability), screenPosition, null);
        }

        public static void ShowBuff(MMOCharacterBuffController controller, string buffId, Vector2 screenPosition)
        {
            if (controller == null || string.IsNullOrWhiteSpace(buffId))
            {
                ResolvePresenter()?.Hide();
                return;
            }

            Func<MMOTooltipContent> provider = () =>
            {
                MMOActiveBuff buff = controller.FindBuff(buffId);
                return buff != null ? BuildBuffContent(buff) : null;
            };
            ResolvePresenter()?.Show(provider.Invoke(), screenPosition, provider);
        }

        public static void HideTooltip()
        {
            MMOGameTooltipPresenter presenter = Instance != null ? Instance : FindAnyObjectByType<MMOGameTooltipPresenter>();
            presenter?.Hide();
        }

        public void Show(MMOTooltipContent content, Vector2 screenPosition, Func<MMOTooltipContent> liveProvider)
        {
            if (content == null)
            {
                Hide();
                return;
            }

            BuildIfNeeded();
            liveContentProvider = liveProvider;
            lastScreenPosition = screenPosition;
            gameObject.SetActive(true);
            BuildContent(content);
            ResizeToContent();
            SetPosition(screenPosition);
            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            liveContentProvider = null;
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

            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                return null;
            }

            GameObject tooltipObject = new("Game Tooltip", typeof(RectTransform));
            tooltipObject.transform.SetParent(canvas.transform, false);
            return tooltipObject.AddComponent<MMOGameTooltipPresenter>();
        }

        private void BuildIfNeeded()
        {
            if (root != null)
            {
                return;
            }

            root = (RectTransform)transform;
            root.sizeDelta = new Vector2(Width, 120f);

            Image background = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            background.color = new Color(0.015f, 0.012f, 0.01f, 0.97f);
            background.raycastTarget = false;

            Outline outline = gameObject.GetComponent<Outline>() ?? gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.72f, 0.63f, 0.42f, 1f);
            outline.effectDistance = new Vector2(1f, -1f);

            contentRoot = MMOUiFactory.CreateRect("Content", transform);
            contentRoot.anchorMin = Vector2.zero;
            contentRoot.anchorMax = Vector2.one;
            contentRoot.offsetMin = new Vector2(Padding, Padding);
            contentRoot.offsetMax = new Vector2(-Padding, -Padding);

            VerticalLayoutGroup layout = contentRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.spacing = 5f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        private void BuildContent(MMOTooltipContent content)
        {
            MMOUiFactory.DestroyChildren(contentRoot);
            AddLine(content.Title, 15, FontStyle.Bold, content.TitleColor);
            foreach (MMOTooltipLine line in content.Lines)
            {
                AddLine(line.Text, line.FontSize, line.Style, line.Color);
            }
        }

        private Text AddLine(string text, int fontSize, FontStyle style, Color color)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            Text line = MMOUiFactory.CreateText("Line", contentRoot, fontSize, style, TextAnchor.UpperLeft);
            line.text = text;
            line.color = color;
            line.horizontalOverflow = HorizontalWrapMode.Wrap;
            line.verticalOverflow = VerticalWrapMode.Overflow;
            line.raycastTarget = false;
            LayoutElement layoutElement = line.gameObject.AddComponent<LayoutElement>();
            layoutElement.minHeight = fontSize + 4f;
            return line;
        }

        private void ResizeToContent()
        {
            Canvas.ForceUpdateCanvases();
            float contentWidth = Width - Padding * 2f;
            float height = Padding * 2f;
            for (int i = 0; i < contentRoot.childCount; i++)
            {
                Text text = contentRoot.GetChild(i).GetComponent<Text>();
                if (text == null)
                {
                    continue;
                }

                height += CalculateTextHeight(text, contentWidth);
                if (i < contentRoot.childCount - 1)
                {
                    height += 5f;
                }
            }

            root.sizeDelta = new Vector2(Width, Mathf.Clamp(height, 72f, 360f));
        }

        private static MMOTooltipContent BuildItemContent(MMOItemDefinition item)
        {
            if (item == null)
            {
                return null;
            }

            MMOTooltipContent content = new(item.DisplayName, GetQualityColor(item.Quality));
            content.Add($"{MMOUiFactory.FormatEnumLabel(item.Quality)} {MMOUiFactory.FormatEnumLabel(item.ItemType)}", 11, FontStyle.Normal, new Color(0.82f, 0.78f, 0.68f, 1f));
            if (item.IsEquipment)
            {
                if (item.IsWeapon)
                {
                    content.Add($"{FormatWeaponHand(item)} {MMOUiFactory.FormatEnumLabel(item.WeaponType)}", 11, FontStyle.Normal, Color.white);
                    content.Add($"{item.WeaponMinDamage:0}-{item.WeaponMaxDamage:0} Damage     Speed {item.WeaponSpeedSeconds:0.00}", 11, FontStyle.Normal, Color.white);
                    content.Add($"({item.WeaponDps:0.0} damage per second)", 11, FontStyle.Normal, Color.white);
                }
                else if (item.IsShield)
                {
                    content.Add("Off Hand Shield", 11, FontStyle.Normal, Color.white);
                    content.Add($"{item.StatBonuses.Armor} Armor", 11, FontStyle.Normal, Color.white);
                    content.Add($"{item.ShieldBlockValue} Block", 11, FontStyle.Normal, Color.white);
                }
                else
                {
                    content.Add($"{MMOUiFactory.FormatEnumLabel(item.ArmorWeight)} - {MMOUiFactory.FormatEnumLabel(item.EquipmentSlot)}", 11, FontStyle.Normal, Color.white);
                }

                string classLine = FormatAllowedClasses(item);
                if (!string.IsNullOrWhiteSpace(classLine))
                {
                    content.Add(classLine, 11, FontStyle.Normal, Color.white);
                }

                foreach (string statLine in BuildStatLines(item.StatBonuses))
                {
                    content.Add(statLine, 11, FontStyle.Normal, new Color(0.18f, 1f, 0.18f, 1f));
                }
            }

            if (item.IsConsumable)
            {
                string effect = BuildConsumableEffectText(item);
                if (!string.IsNullOrWhiteSpace(effect))
                {
                    content.Add(effect, 11, FontStyle.Normal, new Color(0.12f, 1f, 0.12f, 1f));
                }
            }

            foreach (string questLine in BuildQuestLines(item))
            {
                content.Add(questLine, 11, FontStyle.Normal, new Color(1f, 0.86f, 0.35f, 1f));
            }

            if (!string.IsNullOrWhiteSpace(item.Description))
            {
                content.Add(item.Description, 11, FontStyle.Italic, Color.white);
            }

            if (item.VendorValueCopper > 0)
            {
                content.Add($"Sell Price: {MMOCurrencyWallet.FormatCopper(item.VendorValueCopper)}", 11, FontStyle.Normal, new Color(0.95f, 0.82f, 0.48f, 1f));
            }

            return content;
        }

        private static string FormatWeaponHand(MMOItemDefinition item)
        {
            return item.IsTwoHandedWeapon ? "Two-Hand" : "One-Hand";
        }

        private static string FormatAllowedClasses(MMOItemDefinition item)
        {
            if (item == null || item.AllowedClasses == null || item.AllowedClasses.Count == 0)
            {
                return string.Empty;
            }

            List<string> classNames = new();
            foreach (MMOPlayableClass playableClass in item.AllowedClasses)
            {
                classNames.Add(MMOUiFactory.FormatEnumLabel(playableClass));
            }

            return $"Classes: {string.Join(", ", classNames)}";
        }

        private static MMOTooltipContent BuildAbilityContent(MMOAbilityDefinition ability)
        {
            if (ability == null)
            {
                return null;
            }

            MMOTooltipContent content = new(ability.DisplayName, new Color(1f, 0.84f, 0.35f, 1f));
            content.Add(BuildAbilityDetails(ability), 11, FontStyle.Normal, new Color(0.92f, 0.88f, 0.78f, 1f));
            content.Add(ability.Description, 11, FontStyle.Italic, Color.white);
            return content;
        }

        private static MMOTooltipContent BuildBuffContent(MMOActiveBuff buff)
        {
            MMOTooltipContent content = new(buff.DisplayName, new Color(1f, 0.84f, 0.35f, 1f));
            content.Add(buff.Description, 11, FontStyle.Normal, Color.white);
            content.Add($"Remaining: {FormatDuration(buff.RemainingSeconds)}", 11, FontStyle.Bold, buff.IsNearExpiry ? new Color(1f, 0.35f, 0.24f, 1f) : new Color(0.92f, 0.88f, 0.78f, 1f));
            return content;
        }

        private static IEnumerable<string> BuildStatLines(MMOCharacterStats stats)
        {
            if (stats == null)
            {
                yield break;
            }

            if (stats.Stamina > 0) yield return $"+{stats.Stamina} Stamina";
            if (stats.Strength > 0) yield return $"+{stats.Strength} Strength";
            if (stats.Agility > 0) yield return $"+{stats.Agility} Agility";
            if (stats.Intellect > 0) yield return $"+{stats.Intellect} Intellect";
            if (stats.Spirit > 0) yield return $"+{stats.Spirit} Spirit";
            if (stats.Armor > 0) yield return $"+{stats.Armor} Armor";
            if (stats.AttackPower > 0) yield return $"+{stats.AttackPower} Attack Power";
            if (stats.SpellPower > 0) yield return $"+{stats.SpellPower} Spell Power";
        }

        private static string BuildConsumableEffectText(MMOItemDefinition item)
        {
            List<string> effects = new();
            if (item.RestoreHealthAmount > 0)
            {
                effects.Add($"{item.RestoreHealthAmount} health");
            }

            if (item.RestoreManaAmount > 0)
            {
                effects.Add($"{item.RestoreManaAmount} mana");
            }

            if (effects.Count == 0)
            {
                return string.Empty;
            }

            string stationary = item.RequiresStationary ? " Must remain stationary." : string.Empty;
            return $"Use: Restores {string.Join(" and ", effects)} over {item.ConsumeDurationSeconds:0.#} sec.{stationary}";
        }

        private static IEnumerable<string> BuildQuestLines(MMOItemDefinition item)
        {
            if (item.ItemType != MMOItemType.Quest)
            {
                yield break;
            }

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            MMOQuestLog questLog = player != null ? player.GetComponent<MMOQuestLog>() : null;
            if (questLog == null)
            {
                yield return "Quest Item";
                yield break;
            }

            bool matchedQuest = false;
            foreach (MMOQuestRuntimeState state in questLog.ActiveQuests)
            {
                MMOQuestDefinition quest = state.Quest;
                if (quest == null)
                {
                    continue;
                }

                foreach (MMOQuestObjectiveDefinition objective in quest.Objectives)
                {
                    if (objective.RequiredItem == item || objective.UsableItem == item)
                    {
                        matchedQuest = true;
                        yield return $"Quest: {quest.DisplayName}";
                        break;
                    }
                }
            }

            if (!matchedQuest)
            {
                yield return "Quest Item";
            }
        }

        private static string BuildAbilityDetails(MMOAbilityDefinition ability)
        {
            string details = MMOUiFactory.FormatEnumLabel(ability.TargetType);
            if (ability.ManaCost > 0)
            {
                details += $"\nMana: {ability.ManaCost}";
            }

            if (ability.Range > 0f)
            {
                details += $"\nRange: {ability.Range:0.#} yd";
            }

            details += ability.CastTimeSeconds > 0f ? $"\nCast: {ability.CastTimeSeconds:0.#} sec" : "\nCast: Instant";
            details += ability.CooldownSeconds > 0f ? $"\nCooldown: {ability.CooldownSeconds:0.#} sec" : "\nCooldown: None";
            return details;
        }

        private static string FormatDuration(float seconds)
        {
            if (seconds >= 60f)
            {
                int minutes = Mathf.FloorToInt(seconds / 60f);
                int remainder = Mathf.CeilToInt(seconds % 60f);
                return $"{minutes}:{remainder:00}";
            }

            return seconds >= 10f ? $"{Mathf.CeilToInt(seconds)} sec" : $"{seconds:0.0} sec";
        }

        private static Color GetQualityColor(MMOItemQuality quality)
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

        private static float CalculateTextHeight(Text text, float width)
        {
            TextGenerationSettings settings = text.GetGenerationSettings(new Vector2(width, 0f));
            return Mathf.Ceil(text.cachedTextGeneratorForLayout.GetPreferredHeight(text.text, settings) / text.pixelsPerUnit) + 2f;
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
            position.x = Mathf.Clamp(position.x, rect.xMin + Padding, rect.xMax - size.x - Padding);
            position.y = Mathf.Clamp(position.y, rect.yMin + size.y + Padding, rect.yMax - Padding);
            return position;
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

        public void Add(string text, int fontSize, FontStyle style, Color color)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                lines.Add(new MMOTooltipLine(text, fontSize, style, color));
            }
        }
    }

    public readonly struct MMOTooltipLine
    {
        public readonly string Text;
        public readonly int FontSize;
        public readonly FontStyle Style;
        public readonly Color Color;

        public MMOTooltipLine(string text, int fontSize, FontStyle style, Color color)
        {
            Text = text;
            FontSize = fontSize;
            Style = style;
            Color = color;
        }
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
