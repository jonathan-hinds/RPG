using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RPGClone.UI
{
    /// <summary>
    /// One shared drag session for every slot content category.
    /// Payload ownership stays at the source until a destination gameplay system accepts it.
    /// </summary>
    public static class MMOSlotDragState
    {
        private static RectTransform dragVisual;
        private static MMOSlotView sourceView;

        public static MMOSlotDragPayload Current { get; private set; }
        public static bool HasPayload => Current.IsValid;
        public static bool BlocksGameplayMouseInput => HasPayload;

        public static bool BeginDrag(
            MMOSlotDragPayload payload,
            PointerEventData eventData,
            Transform owner,
            string fallbackLabel,
            Sprite icon)
        {
            if (!payload.IsValid)
            {
                return false;
            }

            EndDrag();
            Current = payload;
            sourceView = owner != null ? owner.GetComponent<MMOSlotView>() : null;
            sourceView?.SetDragging(true);
            CreateDragVisual(payload, owner, fallbackLabel, icon);
            UpdateDrag(eventData);
            return true;
        }

        public static void UpdateDrag(PointerEventData eventData)
        {
            if (dragVisual != null && eventData != null)
            {
                dragVisual.position = eventData.position;
            }
        }

        public static void EndDrag()
        {
            sourceView?.SetDragging(false);
            sourceView = null;
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

        private static void CreateDragVisual(
            MMOSlotDragPayload payload,
            Transform owner,
            string fallbackLabel,
            Sprite icon)
        {
            Canvas canvas = owner != null ? owner.GetComponentInParent<Canvas>() : null;
            Transform parent = canvas != null ? canvas.transform : owner;
            GameObject visualObject = new("Dragged Slot Content", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            visualObject.transform.SetParent(parent, false);
            visualObject.transform.SetAsLastSibling();

            dragVisual = (RectTransform)visualObject.transform;
            dragVisual.sizeDelta = new Vector2(54f, 54f);

            CanvasGroup group = visualObject.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 0.84f;

            Image raycastImage = visualObject.GetComponent<Image>();
            raycastImage.color = new Color(1f, 1f, 1f, 0.001f);
            raycastImage.raycastTarget = false;

            Image shadow = MMOUiFactory.CreateImage(
                "Drag Shadow",
                dragVisual,
                new Color(0f, 0f, 0f, 0.34f),
                false);
            shadow.sprite = MMOSlotSkin.DragShadow;
            shadow.preserveAspect = false;
            MMOUiFactory.Stretch(shadow.rectTransform);
            shadow.rectTransform.offsetMin = new Vector2(3f, -5f);
            shadow.rectTransform.offsetMax = new Vector2(7f, -1f);
            shadow.transform.SetAsFirstSibling();

            MMOSlotView slotView = MMOSlotView.Attach(visualObject);
            shadow.transform.SetAsFirstSibling();
            string quantity = payload.Quantity > 1 ? payload.Quantity.ToString() : null;
            string fallback = icon == null ? Shorten(fallbackLabel, 8) : quantity;
            Color iconTint = payload.Item != null ? MMOItemIconView.GetIconTint(payload.Item) : Color.white;
            Color borderTint = payload.Item != null
                ? MMOItemIconView.GetQualityTextColor(payload.Item.Quality)
                : Color.clear;
            slotView.Present(new MMOSlotPresentation(
                icon: icon,
                primaryText: icon != null ? fallback : null,
                centerText: icon == null ? fallback : null,
                iconTint: iconTint,
                borderTint: borderTint));
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

    /// <summary>
    /// Compatibility bridge for existing integrations. New code should use MMOSlotDragState.
    /// </summary>
    public static class MMOActionBarDragState
    {
        public static MMOSlotDragPayload Current => MMOSlotDragState.Current;
        public static bool HasPayload => MMOSlotDragState.HasPayload;
        public static bool BlocksGameplayMouseInput => MMOSlotDragState.BlocksGameplayMouseInput;

        public static bool BeginDrag(
            MMOSlotDragPayload payload,
            PointerEventData eventData,
            Transform owner,
            string label,
            Sprite icon)
        {
            return MMOSlotDragState.BeginDrag(payload, eventData, owner, label, icon);
        }

        public static void UpdateDrag(PointerEventData eventData)
        {
            MMOSlotDragState.UpdateDrag(eventData);
        }

        public static void EndDrag()
        {
            MMOSlotDragState.EndDrag();
        }
    }
}
