using RPGClone.Abilities;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RPGClone.UI
{
    public readonly struct MMOAbilityDragPayload
    {
        public readonly MMOAbilityDefinition Ability;
        public readonly MMOActionBarPresenter SourceActionBar;
        public readonly int SourceSlotIndex;

        public bool IsValid => Ability != null;
        public bool FromActionBar => SourceActionBar != null && SourceSlotIndex >= 0;

        public MMOAbilityDragPayload(MMOAbilityDefinition ability, MMOActionBarPresenter sourceActionBar = null, int sourceSlotIndex = -1)
        {
            Ability = ability;
            SourceActionBar = sourceActionBar;
            SourceSlotIndex = sourceSlotIndex;
        }
    }

    public static class MMOAbilityDragState
    {
        private static RectTransform dragVisual;

        public static MMOAbilityDragPayload Current { get; private set; }
        public static bool HasPayload => Current.IsValid;

        public static bool BeginDrag(MMOAbilityDragPayload payload, PointerEventData eventData, Transform owner, string label, Sprite icon)
        {
            if (!payload.IsValid)
            {
                return false;
            }

            Current = payload;
            CreateDragVisual(owner, label, icon);
            UpdateDrag(eventData);
            return true;
        }

        public static void UpdateDrag(PointerEventData eventData)
        {
            if (dragVisual == null || eventData == null)
            {
                return;
            }

            dragVisual.position = eventData.position;
        }

        public static void EndDrag()
        {
            Current = default;
            if (dragVisual == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(dragVisual.gameObject);
            }
            else
            {
                Object.DestroyImmediate(dragVisual.gameObject);
            }

            dragVisual = null;
        }

        private static void CreateDragVisual(Transform owner, string label, Sprite icon)
        {
            Canvas canvas = owner != null ? owner.GetComponentInParent<Canvas>() : null;
            Transform parent = canvas != null ? canvas.transform : owner;
            GameObject visualObject = new("Dragged Ability", typeof(RectTransform));
            visualObject.transform.SetParent(parent, false);
            visualObject.transform.SetAsLastSibling();

            dragVisual = (RectTransform)visualObject.transform;
            dragVisual.sizeDelta = new Vector2(52f, 52f);

            CanvasGroup group = visualObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.alpha = 0.82f;

            Image background = visualObject.AddComponent<Image>();
            background.color = new Color(0.02f, 0.018f, 0.014f, 0.94f);
            background.raycastTarget = false;

            if (icon != null)
            {
                Image iconImage = MMOUiFactory.CreateImage("Icon", dragVisual, Color.white, false);
                iconImage.sprite = icon;
                MMOUiFactory.Stretch(iconImage.rectTransform);
            }

            Text text = MMOUiFactory.CreateText("Label", dragVisual, 9, FontStyle.Bold, TextAnchor.MiddleCenter);
            text.text = Shorten(label, 10);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(3f, 5f);
            text.rectTransform.offsetMax = new Vector2(-3f, -3f);
        }

        private static string Shorten(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value.Length <= maxLength ? value : value[..maxLength];
        }
    }
}
