using UnityEngine;
using UnityEngine.UI;

namespace RPGClone.UI
{
    public static class MMOPanelSkin
    {
        public static void ApplyBagPanel(GameObject panelObject)
        {
            if (panelObject == null)
            {
                return;
            }

            ConfigureTransparentHitArea(panelObject);

            Image background = EnsureAuthoredLayer(
                panelObject.transform,
                "Bag Panel Background Art",
                MMOSlotSkin.BagBackground,
                out bool createdBackground);
            if (createdBackground)
            {
                MMOUiFactory.Stretch(background.rectTransform);
                background.rectTransform.offsetMin = new Vector2(-14f, -14f);
                background.rectTransform.offsetMax = new Vector2(14f, 14f);
            }

            background.color = Color.white;
            background.transform.SetAsFirstSibling();

            Image header = EnsureAuthoredLayer(
                panelObject.transform,
                "Bag Panel Header",
                MMOSlotSkin.PanelHeader,
                out bool createdHeader);
            if (createdHeader)
            {
                header.rectTransform.anchorMin = new Vector2(0f, 1f);
                header.rectTransform.anchorMax = new Vector2(1f, 1f);
                header.rectTransform.pivot = new Vector2(0.5f, 1f);
                header.rectTransform.anchoredPosition = new Vector2(8f, -5f);
                header.rectTransform.sizeDelta = new Vector2(-76f, 42f);
            }

            header.color = Color.white;

            Image currencyWell = EnsureAuthoredLayer(
                panelObject.transform,
                "Bag Panel Currency Well",
                MMOSlotSkin.BagCurrencyBar,
                out bool createdCurrencyWell);
            if (createdCurrencyWell)
            {
                currencyWell.rectTransform.anchorMin = new Vector2(0f, 0f);
                currencyWell.rectTransform.anchorMax = new Vector2(1f, 0f);
                currencyWell.rectTransform.pivot = new Vector2(0.5f, 0f);
                currencyWell.rectTransform.anchoredPosition = new Vector2(0f, 7f);
                currencyWell.rectTransform.sizeDelta = new Vector2(-16f, 38f);
            }

            currencyWell.color = Color.white;

            Image frame = EnsureAuthoredLayer(
                panelObject.transform,
                "Bag Panel Frame",
                MMOSlotSkin.BagFrame,
                out bool createdFrame);
            if (createdFrame)
            {
                MMOUiFactory.Stretch(frame.rectTransform);
                frame.rectTransform.offsetMin = new Vector2(-14f, -14f);
                frame.rectTransform.offsetMax = new Vector2(14f, 14f);
            }

            Image medallion = EnsureAuthoredLayer(
                panelObject.transform,
                "Bag Panel Medallion",
                MMOSlotSkin.BagMedallion,
                out bool createdMedallion);
            if (createdMedallion)
            {
                medallion.rectTransform.anchorMin = new Vector2(0f, 1f);
                medallion.rectTransform.anchorMax = new Vector2(0f, 1f);
                medallion.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                medallion.rectTransform.anchoredPosition = new Vector2(16f, -23f);
                medallion.rectTransform.sizeDelta = new Vector2(70f, 70f);
            }

            medallion.color = Color.white;
        }

        public static void ApplyStandardPanel(GameObject panelObject)
        {
            if (panelObject == null)
            {
                return;
            }

            Image background = panelObject.GetComponent<Image>() ?? panelObject.AddComponent<Image>();
            ConfigureImage(background, MMOSlotSkin.PanelBackground, true);
            background.color = Color.white;
            Image header = EnsureLayer(panelObject.transform, "Panel Header", MMOSlotSkin.PanelHeader);
            header.rectTransform.anchorMin = new Vector2(0f, 1f);
            header.rectTransform.anchorMax = new Vector2(1f, 1f);
            header.rectTransform.pivot = new Vector2(0.5f, 1f);
            header.rectTransform.anchoredPosition = new Vector2(0f, -3f);
            header.rectTransform.sizeDelta = new Vector2(-12f, 36f);
            header.color = Color.white;
            header.transform.SetAsFirstSibling();
            EnsureLayer(panelObject.transform, "Panel Frame", MMOSlotSkin.PanelFrame).transform.SetAsLastSibling();
        }

        public static void ApplyActionBar(GameObject barObject)
        {
            if (barObject == null)
            {
                return;
            }

            // The Action Bar is the content group inside Bottom HUD. The 1080x96
            // Bottom HUD owns the visual background; keeping a second image here
            // creates a visibly smaller 642x58 panel on top of it.
            Image legacyBackground = barObject.GetComponent<Image>();
            if (legacyBackground != null)
            {
                legacyBackground.sprite = null;
                legacyBackground.overrideSprite = null;
                legacyBackground.color = Color.clear;
                legacyBackground.raycastTarget = false;
                legacyBackground.type = Image.Type.Simple;
            }

            SetLegacyLayerActive(barObject.transform, "Left End Cap", false);
            SetLegacyLayerActive(barObject.transform, "Right End Cap", false);
        }

        public static void ApplyBottomHud(GameObject hudObject)
        {
            if (hudObject == null)
            {
                return;
            }

            ConfigureTransparentHitArea(hudObject);

            Image background = EnsureAuthoredLayer(
                hudObject.transform,
                "HUD Background Art",
                MMOSlotSkin.ActionBarCenter,
                out bool createdBackground);
            if (createdBackground)
            {
                MMOUiFactory.Stretch(background.rectTransform);
                background.rectTransform.offsetMin = new Vector2(-32f, 0f);
                background.rectTransform.offsetMax = new Vector2(-32f, 0f);
            }

            background.color = Color.white;
            background.transform.SetAsFirstSibling();

            SetLegacyLayerActive(hudObject.transform, "HUD Left End Cap", false);
            SetLegacyLayerActive(hudObject.transform, "HUD Right End Cap", false);
        }

        public static void ConfigureCloseButton(Button button)
        {
            if (button == null)
            {
                return;
            }

            MMOUiFactory.ConfigureButtonSprites(button, MMOSlotSkin.CloseNormal, null, null);
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.84f, 0.82f, 0.76f, 1f);
            colors.highlightedColor = new Color(1f, 0.80f, 0.34f, 1f);
            colors.pressedColor = new Color(0.62f, 0.47f, 0.25f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.34f, 0.33f, 0.31f, 0.72f);
            button.colors = colors;

            Text label = MMOUiFactory.FindButtonLabel(button);
            if (label != null)
            {
                label.text = string.Empty;
            }
        }

        private static Image EnsureLayer(Transform parent, string objectName, Sprite sprite)
        {
            Transform existing = parent.Find(objectName);
            Image layer = existing != null ? existing.GetComponent<Image>() : null;
            if (layer == null)
            {
                layer = MMOUiFactory.CreateImage(objectName, parent, Color.white, false);
            }

            ConfigureImage(layer, sprite, false);
            MMOUiFactory.Stretch(layer.rectTransform);
            return layer;
        }

        private static Image EnsureAuthoredLayer(
            Transform parent,
            string objectName,
            Sprite sprite,
            out bool created)
        {
            Transform existing = parent.Find(objectName);
            Image layer = existing != null ? existing.GetComponent<Image>() : null;
            created = layer == null;
            if (created)
            {
                layer = MMOUiFactory.CreateImage(objectName, parent, Color.white, false);
            }

            ConfigureImage(layer, sprite, false);
            return layer;
        }

        private static void ConfigureImage(Image image, Sprite sprite, bool raycast)
        {
            image.sprite = sprite;
            image.color = sprite != null ? Color.white : MMONpcWindowFrame.BackgroundColor;
            image.raycastTarget = raycast;
            image.type = sprite != null && sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        }

        private static void SetLegacyLayerActive(Transform parent, string objectName, bool active)
        {
            Transform existing = parent.Find(objectName);
            if (existing != null)
            {
                existing.gameObject.SetActive(active);
            }
        }

        private static void ConfigureTransparentHitArea(GameObject target)
        {
            Image hitArea = target.GetComponent<Image>() ?? target.AddComponent<Image>();
            hitArea.sprite = null;
            hitArea.overrideSprite = null;
            hitArea.color = new Color(1f, 1f, 1f, 0.001f);
            hitArea.raycastTarget = true;
            hitArea.type = Image.Type.Simple;
        }
    }
}
