using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RPGClone.UI
{
    public enum MMOSlotVisualLayer
    {
        EmptyBackground,
        CategorySilhouette,
        Icon,
        BorderTint,
        Cooldown,
        Disabled,
        Unavailable,
        Attention,
        NormalFrame,
        HoverFrame,
        PressedFrame,
        SelectedFrame,
        ActiveFrame,
        ValidDropFrame,
        InvalidDropFrame,
        ProcGlow,
        StatusMarker
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class MMOSlotView : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler
    {
        private const float IconInset = 4f;

        private RectTransform layerRoot;
        private Image emptyBackground;
        private Image categorySilhouette;
        private Image icon;
        private Image borderTint;
        private Image cooldown;
        private Image disabledOverlay;
        private Image unavailableOverlay;
        private Image attentionOverlay;
        private Image normalFrame;
        private Image hoverFrame;
        private Image pressedFrame;
        private Image selectedFrame;
        private Image activeFrame;
        private Image validDropFrame;
        private Image invalidDropFrame;
        private Image procGlow;
        private Image statusMarker;
        private Text primaryText;
        private Text secondaryText;
        private Text centerText;
        private Text cooldownText;

        private MMOSlotPresentation presentation;
        private MMOSlotDropState dropState;
        private bool pointerInside;
        private bool pointerPressed;
        private bool dragging;

        public Sprite Icon => icon != null ? icon.sprite : null;

        private void Awake()
        {
            EnsureVisualTree();
            ApplyPresentation();
        }

        private void OnDisable()
        {
            pointerInside = false;
            pointerPressed = false;
            dropState = MMOSlotDropState.None;
            RefreshStates();
        }

        public static MMOSlotView Attach(GameObject slotObject)
        {
            if (slotObject == null)
            {
                return null;
            }

            MMOSlotView view = slotObject.GetComponent<MMOSlotView>();
            if (view == null)
            {
                view = slotObject.AddComponent<MMOSlotView>();
            }

            view.EnsureVisualTree();
            return view;
        }

        public void Present(MMOSlotPresentation newPresentation)
        {
            presentation = newPresentation;
            EnsureVisualTree();
            ApplyPresentation();
        }

        public void SetCooldown(float normalizedRemaining, string label)
        {
            presentation = new MMOSlotPresentation(
                icon: presentation.Icon,
                categorySilhouette: presentation.CategorySilhouette,
                primaryText: presentation.PrimaryText,
                secondaryText: presentation.SecondaryText,
                centerText: presentation.CenterText,
                iconTint: presentation.IconTint,
                borderTint: presentation.BorderTint,
                selected: presentation.Selected,
                active: presentation.Active,
                disabled: presentation.Disabled,
                usable: presentation.Usable,
                inRange: presentation.InRange,
                attention: presentation.Attention,
                procGlow: presentation.ProcGlow,
                showStatusMarker: presentation.ShowStatusMarker,
                cooldownNormalized: normalizedRemaining,
                cooldownText: label);
            ApplyPresentation();
        }

        public void SetDragging(bool value)
        {
            dragging = value;
            if (!value)
            {
                pointerPressed = false;
                RefreshStates();
            }

            ApplyIconTint();
        }

        public void SetDropState(MMOSlotDropState state)
        {
            dropState = state;
            RefreshStates();
        }

        public void SetLayerSprite(MMOSlotVisualLayer layer, Sprite sprite)
        {
            EnsureVisualTree();
            Image target = ResolveLayer(layer);
            if (target != null)
            {
                target.sprite = sprite;
            }
        }

        public void SetLayerTint(MMOSlotVisualLayer layer, Color tint)
        {
            EnsureVisualTree();
            Image target = ResolveLayer(layer);
            if (target != null)
            {
                target.color = tint;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            pointerInside = true;
            EvaluateCurrentDrag();
            RefreshStates();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pointerInside = false;
            pointerPressed = false;
            dropState = MMOSlotDropState.None;
            RefreshStates();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            pointerPressed = eventData != null && eventData.button == PointerEventData.InputButton.Left;
            RefreshStates();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            pointerPressed = false;
            RefreshStates();
        }

        private void EvaluateCurrentDrag()
        {
            if (!MMOSlotDragState.HasPayload)
            {
                dropState = MMOSlotDropState.None;
                return;
            }

            MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IMMOSlotDropTarget target)
                {
                    dropState = target.EvaluateDrop(MMOSlotDragState.Current);
                    return;
                }
            }

            dropState = MMOSlotDropState.Invalid;
        }

        private void EnsureVisualTree()
        {
            if (layerRoot != null)
            {
                return;
            }

            Transform existingRoot = transform.Find("Slot Visual Layers");
            bool createdLayerRoot = existingRoot == null;
            layerRoot = existingRoot != null
                ? (RectTransform)existingRoot
                : MMOUiFactory.CreateRect("Slot Visual Layers", transform);
            if (createdLayerRoot)
            {
                MMOUiFactory.Stretch(layerRoot);
                layerRoot.SetAsFirstSibling();
            }

            emptyBackground = EnsureImage("Empty Background", MMOSlotSkin.SlotBackground, out bool createdEmptyBackground);
            categorySilhouette = EnsureImage("Category Silhouette", MMOSlotSkin.DefaultCategorySilhouette, out bool createdCategorySilhouette);
            icon = EnsureImage("Primary Icon", null, out bool createdIcon);
            borderTint = EnsureImage("Border Tint", MMOSlotSkin.BorderTintMask, out _);
            cooldown = EnsureImage("Cooldown Sweep", null, out bool createdCooldown);
            disabledOverlay = EnsureImage("Disabled Overlay", MMOSlotSkin.DisabledOverlay, out bool createdDisabledOverlay);
            unavailableOverlay = EnsureImage("Unavailable Overlay", MMOSlotSkin.UnavailableOverlay, out bool createdUnavailableOverlay);
            attentionOverlay = EnsureImage("Attention Overlay", MMOSlotSkin.AttentionOverlay, out bool createdAttentionOverlay);
            normalFrame = EnsureImage("Normal Frame", MMOSlotSkin.NormalFrame, out bool createdNormalFrame);
            hoverFrame = EnsureImage("Hover Frame", MMOSlotSkin.HoverFrame, out bool createdHoverFrame);
            pressedFrame = EnsureImage("Pressed Frame", MMOSlotSkin.PressedFrame, out bool createdPressedFrame);
            selectedFrame = EnsureImage("Selected Frame", MMOSlotSkin.SelectedFrame, out bool createdSelectedFrame);
            activeFrame = EnsureImage("Active Frame", MMOSlotSkin.ActiveFrame, out bool createdActiveFrame);
            validDropFrame = EnsureImage("Valid Drop Frame", MMOSlotSkin.ValidDropFrame, out bool createdValidDropFrame);
            invalidDropFrame = EnsureImage("Invalid Drop Frame", MMOSlotSkin.InvalidDropFrame, out bool createdInvalidDropFrame);
            procGlow = EnsureImage("Proc Glow", MMOSlotSkin.ProcGlow, out bool createdProcGlow);
            statusMarker = EnsureImage("Status Marker", MMOSlotSkin.StatusMarker, out bool createdStatusMarker);

            if (createdEmptyBackground)
            {
                SetInset(emptyBackground.rectTransform, IconInset);
            }

            if (createdCategorySilhouette)
            {
                SetInset(categorySilhouette.rectTransform, 12f);
                categorySilhouette.color = new Color(0.56f, 0.56f, 0.54f, 0.22f);
            }

            if (createdIcon)
            {
                SetInset(icon.rectTransform, IconInset);
            }

            if (createdCooldown)
            {
                SetInset(cooldown.rectTransform, IconInset);
                cooldown.color = new Color(0f, 0f, 0f, 0.68f);
                cooldown.type = Image.Type.Filled;
                cooldown.fillMethod = Image.FillMethod.Radial360;
                cooldown.fillOrigin = 2;
                cooldown.fillClockwise = false;
            }

            if (createdDisabledOverlay)
            {
                disabledOverlay.color = new Color(0f, 0f, 0f, 0.52f);
            }

            if (createdUnavailableOverlay)
            {
                unavailableOverlay.color = new Color(0.20f, 0.025f, 0.01f, 0.30f);
            }

            if (createdAttentionOverlay)
            {
                attentionOverlay.color = new Color(1f, 0.62f, 0.16f, 0.24f);
            }

            if (createdNormalFrame)
            {
                normalFrame.color = Color.white;
            }

            if (createdHoverFrame)
            {
                hoverFrame.color = new Color(0.95f, 0.91f, 0.72f, 0.90f);
            }

            if (createdPressedFrame)
            {
                pressedFrame.color = new Color(0.64f, 0.47f, 0.23f, 0.96f);
            }

            if (createdSelectedFrame)
            {
                selectedFrame.color = new Color(0.98f, 0.65f, 0.18f, 0.98f);
            }

            if (createdActiveFrame)
            {
                activeFrame.color = new Color(0.34f, 0.76f, 0.90f, 0.96f);
            }

            if (createdValidDropFrame)
            {
                validDropFrame.color = new Color(0.22f, 0.88f, 0.36f, 0.98f);
            }

            if (createdInvalidDropFrame)
            {
                invalidDropFrame.color = new Color(0.94f, 0.16f, 0.10f, 0.98f);
            }

            if (createdProcGlow)
            {
                procGlow.color = new Color(1f, 0.72f, 0.22f, 0.72f);
            }

            if (createdStatusMarker)
            {
                statusMarker.rectTransform.anchorMin = new Vector2(1f, 1f);
                statusMarker.rectTransform.anchorMax = new Vector2(1f, 1f);
                statusMarker.rectTransform.pivot = new Vector2(1f, 1f);
                statusMarker.rectTransform.anchoredPosition = new Vector2(-4f, -4f);
                statusMarker.rectTransform.sizeDelta = new Vector2(6f, 6f);
                statusMarker.preserveAspect = false;
                statusMarker.color = new Color(1f, 0.72f, 0.20f, 0.96f);
            }

            primaryText = EnsureText("Primary Text", 11, TextAnchor.LowerRight, out bool createdPrimaryText);
            if (createdPrimaryText)
            {
                primaryText.rectTransform.offsetMin = new Vector2(4f, 2f);
                primaryText.rectTransform.offsetMax = new Vector2(-5f, -3f);
            }

            secondaryText = EnsureText("Secondary Text", 10, TextAnchor.UpperLeft, out bool createdSecondaryText);
            if (createdSecondaryText)
            {
                secondaryText.color = new Color(1f, 0.84f, 0.4f, 1f);
                secondaryText.rectTransform.offsetMin = new Vector2(5f, 3f);
                secondaryText.rectTransform.offsetMax = new Vector2(-4f, -3f);
            }

            centerText = EnsureText("Center Text", 11, TextAnchor.MiddleCenter, out bool createdCenterText);
            if (createdCenterText)
            {
                centerText.rectTransform.offsetMin = new Vector2(6f, 6f);
                centerText.rectTransform.offsetMax = new Vector2(-6f, -6f);
            }

            cooldownText = EnsureText("Cooldown Text", 15, TextAnchor.MiddleCenter, out bool createdCooldownText);
            if (createdCooldownText)
            {
                cooldownText.rectTransform.offsetMin = Vector2.zero;
                cooldownText.rectTransform.offsetMax = Vector2.zero;
            }
        }

        private Image EnsureImage(string objectName, Sprite sprite, out bool created)
        {
            Transform existing = layerRoot.Find(objectName);
            Image image = existing != null ? existing.GetComponent<Image>() : null;
            created = image == null;
            if (created)
            {
                image = MMOUiFactory.CreateImage(objectName, layerRoot, Color.white, false);
                image.sprite = sprite;
                image.preserveAspect = sprite != null;
                image.raycastTarget = false;
                MMOUiFactory.Stretch(image.rectTransform);
            }

            return image;
        }

        private Text EnsureText(string objectName, int fontSize, TextAnchor alignment, out bool created)
        {
            Transform existing = layerRoot.Find(objectName);
            Text text = existing != null ? existing.GetComponent<Text>() : null;
            created = text == null;
            if (created)
            {
                text = MMOUiFactory.CreateText(objectName, layerRoot, fontSize, FontStyle.Bold, alignment);
                text.raycastTarget = false;
                MMOUiFactory.Stretch(text.rectTransform);
            }

            return text;
        }

        private void ApplyPresentation()
        {
            if (layerRoot == null)
            {
                return;
            }

            icon.sprite = presentation.Icon;
            icon.gameObject.SetActive(presentation.Icon != null);
            categorySilhouette.sprite = presentation.CategorySilhouette != null
                ? presentation.CategorySilhouette
                : MMOSlotSkin.DefaultCategorySilhouette;
            categorySilhouette.gameObject.SetActive(presentation.Icon == null && presentation.CategorySilhouette != null);

            primaryText.text = presentation.PrimaryText ?? string.Empty;
            primaryText.gameObject.SetActive(!string.IsNullOrWhiteSpace(primaryText.text));
            secondaryText.text = presentation.SecondaryText ?? string.Empty;
            secondaryText.gameObject.SetActive(!string.IsNullOrWhiteSpace(secondaryText.text));
            centerText.text = presentation.CenterText ?? string.Empty;
            centerText.color = presentation.IconTint;
            centerText.gameObject.SetActive(presentation.Icon == null && !string.IsNullOrWhiteSpace(centerText.text));

            bool coolingDown = presentation.CooldownNormalized > 0f;
            cooldown.fillAmount = presentation.CooldownNormalized;
            cooldown.gameObject.SetActive(coolingDown);
            cooldownText.text = presentation.CooldownText ?? string.Empty;
            cooldownText.gameObject.SetActive(coolingDown && !string.IsNullOrWhiteSpace(cooldownText.text));

            Color qualityTint = presentation.BorderTint;
            qualityTint.a *= 0.72f;
            borderTint.color = qualityTint;
            disabledOverlay.gameObject.SetActive(presentation.Disabled);
            unavailableOverlay.gameObject.SetActive(!presentation.Disabled && (!presentation.Usable || !presentation.InRange));
            attentionOverlay.gameObject.SetActive(presentation.Attention && !presentation.ProcGlow);
            procGlow.gameObject.SetActive(presentation.ProcGlow);
            statusMarker.gameObject.SetActive(presentation.ShowStatusMarker);
            ApplyIconTint();
            RefreshStates();
        }

        private void ApplyIconTint()
        {
            if (icon == null)
            {
                return;
            }

            Color tint = presentation.IconTint;
            if (pointerPressed)
            {
                tint.r *= 0.72f;
                tint.g *= 0.72f;
                tint.b *= 0.72f;
            }

            if (dragging)
            {
                tint.a *= 0.35f;
            }

            icon.color = tint;
        }

        private void RefreshStates()
        {
            if (normalFrame == null)
            {
                return;
            }

            normalFrame.gameObject.SetActive(true);
            bool showInvalidDrop = dropState == MMOSlotDropState.Invalid;
            bool showValidDrop = !showInvalidDrop && dropState == MMOSlotDropState.Valid;
            bool showPressed = !showInvalidDrop && !showValidDrop && pointerPressed;
            bool showActive = !showInvalidDrop && !showValidDrop && !showPressed && presentation.Active;
            bool showSelected = !showInvalidDrop
                && !showValidDrop
                && !showPressed
                && !showActive
                && presentation.Selected;
            bool showHover = !showInvalidDrop
                && !showValidDrop
                && !showPressed
                && !showActive
                && !showSelected
                && pointerInside;
            bool hasSemanticState = showInvalidDrop
                || showValidDrop
                || showPressed
                || showActive
                || showSelected
                || showHover;

            hoverFrame.gameObject.SetActive(showHover);
            pressedFrame.gameObject.SetActive(showPressed);
            selectedFrame.gameObject.SetActive(showSelected);
            activeFrame.gameObject.SetActive(showActive);
            validDropFrame.gameObject.SetActive(showValidDrop);
            invalidDropFrame.gameObject.SetActive(showInvalidDrop);
            borderTint.gameObject.SetActive(presentation.BorderTint.a > 0.001f && !hasSemanticState);
            ApplyIconTint();
        }

        private static void SetInset(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private Image ResolveLayer(MMOSlotVisualLayer layer)
        {
            return layer switch
            {
                MMOSlotVisualLayer.EmptyBackground => emptyBackground,
                MMOSlotVisualLayer.CategorySilhouette => categorySilhouette,
                MMOSlotVisualLayer.Icon => icon,
                MMOSlotVisualLayer.BorderTint => borderTint,
                MMOSlotVisualLayer.Cooldown => cooldown,
                MMOSlotVisualLayer.Disabled => disabledOverlay,
                MMOSlotVisualLayer.Unavailable => unavailableOverlay,
                MMOSlotVisualLayer.Attention => attentionOverlay,
                MMOSlotVisualLayer.NormalFrame => normalFrame,
                MMOSlotVisualLayer.HoverFrame => hoverFrame,
                MMOSlotVisualLayer.PressedFrame => pressedFrame,
                MMOSlotVisualLayer.SelectedFrame => selectedFrame,
                MMOSlotVisualLayer.ActiveFrame => activeFrame,
                MMOSlotVisualLayer.ValidDropFrame => validDropFrame,
                MMOSlotVisualLayer.InvalidDropFrame => invalidDropFrame,
                MMOSlotVisualLayer.ProcGlow => procGlow,
                MMOSlotVisualLayer.StatusMarker => statusMarker,
                _ => null
            };
        }
    }
}
