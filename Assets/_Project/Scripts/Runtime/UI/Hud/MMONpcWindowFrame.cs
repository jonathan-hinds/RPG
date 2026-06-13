using UnityEngine;
using UnityEngine.UI;

namespace RPGClone.UI
{
    public static class MMONpcWindowFrame
    {
        public static readonly Color BackgroundColor = new(0.035f, 0.032f, 0.028f, 0.97f);
        public static readonly Color PanelColor = new(0.055f, 0.046f, 0.035f, 0.96f);
        public static readonly Color ButtonColor = new(0.12f, 0.09f, 0.065f, 0.95f);
        public static readonly Color AccentButtonColor = new(0.16f, 0.105f, 0.045f, 1f);
        public static readonly Color BorderColor = new(0.72f, 0.63f, 0.42f, 1f);
        public static readonly Color TitleColor = new(1f, 0.82f, 0.34f, 1f);
        public static readonly Color BodyColor = new(0.91f, 0.84f, 0.69f, 1f);

        public static void Apply(GameObject panelObject)
        {
            if (panelObject.TryGetComponent(out MMOStandardWindow standardWindow))
            {
                standardWindow.Initialize(null, null);
                return;
            }

            Image background = panelObject.GetComponent<Image>() ?? panelObject.AddComponent<Image>();
            background.color = BackgroundColor;

            Outline outline = panelObject.GetComponent<Outline>() ?? panelObject.AddComponent<Outline>();
            outline.effectColor = BorderColor;
            outline.effectDistance = new Vector2(1f, -1f);
        }

        public static Text CreateTitle(Transform parent, string title)
        {
            Text titleText = MMOUiFactory.CreateText("Title", parent, 18, FontStyle.Bold, TextAnchor.MiddleLeft);
            titleText.text = title;
            titleText.color = TitleColor;
            titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            titleText.rectTransform.pivot = new Vector2(0f, 1f);
            titleText.rectTransform.anchoredPosition = new Vector2(14f, -10f);
            titleText.rectTransform.sizeDelta = new Vector2(-58f, 28f);
            return titleText;
        }

        public static Button CreateCloseButton(Transform parent, System.Action onClose)
        {
            Button closeButton = MMOUiFactory.CreateTextButton("Close", parent, "X", new Vector2(26f, 24f), ButtonColor);
            closeButton.onClick.AddListener(() => onClose?.Invoke());
            RectTransform closeRect = closeButton.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-10f, -10f);
            return closeButton;
        }
    }
}
