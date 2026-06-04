using UnityEngine;
using UnityEngine.UI;

namespace RPGClone.UI
{
    public static class MMOUiFactory
    {
        private static Font cachedFont;

        public static RectTransform CreateRect(string objectName, Transform parent)
        {
            GameObject child = new(objectName, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return (RectTransform)child.transform;
        }

        public static Image CreateImage(string objectName, Transform parent, Color color, bool raycastTarget = true)
        {
            RectTransform rectTransform = CreateRect(objectName, parent);
            Image image = rectTransform.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        public static Text CreateText(string objectName, Transform parent, int fontSize, FontStyle style, TextAnchor alignment)
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

        public static Button CreateTextButton(string objectName, Transform parent, string label, Vector2 size, Color backgroundColor)
        {
            Image image = CreateImage(objectName, parent, backgroundColor);
            image.rectTransform.sizeDelta = size;

            Button button = image.gameObject.AddComponent<Button>();
            Text text = CreateText("Label", image.transform, 11, FontStyle.Bold, TextAnchor.MiddleCenter);
            text.text = label;
            Stretch(text.rectTransform);
            return button;
        }

        public static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        public static void DestroyChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                if (Application.isPlaying)
                {
                    Object.Destroy(child);
                }
                else
                {
                    Object.DestroyImmediate(child);
                }
            }
        }

        public static string FormatEnumLabel(System.Enum value)
        {
            string text = value.ToString();
            for (int i = text.Length - 1; i > 0; i--)
            {
                bool previousNeedsSeparator = !char.IsWhiteSpace(text[i - 1]) && !char.IsDigit(text[i - 1]);
                if ((char.IsUpper(text[i]) || char.IsDigit(text[i])) && previousNeedsSeparator)
                {
                    text = text.Insert(i, " ");
                }
            }

            return text;
        }

        public static Font GetFont(int size)
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
    }
}
