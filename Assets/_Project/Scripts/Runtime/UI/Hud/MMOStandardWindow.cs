using System;
using UnityEngine;
using UnityEngine.UI;

namespace RPGClone.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class MMOStandardWindow : MonoBehaviour
    {
        public static readonly Vector2 NativeWindowSize = new(1026f, 1402f);
        public static readonly Vector2 NativeCloseButtonSize = new(100f, 100f);
        public static readonly Vector2 NativeQuestButtonSize = new(332f, 64f);
        public const float DefaultWindowScale = 0.45f;
        public static readonly Vector2 WindowSize = NativeWindowSize * DefaultWindowScale;
        public static readonly Vector2 CloseButtonSize = NativeCloseButtonSize * DefaultWindowScale;
        public static readonly Vector2 QuestButtonSize = NativeQuestButtonSize * DefaultWindowScale;
        public static readonly Vector2 DefaultContentOffsetMin = new Vector2(62f, 126f) * DefaultWindowScale;
        public static readonly Vector2 DefaultContentOffsetMax = new Vector2(-62f, -126f) * DefaultWindowScale;
        public static readonly Vector2 DefaultCloseButtonPosition = new Vector2(-62f, -62f) * DefaultWindowScale;
        public static readonly Vector2 DefaultActionButtonPosition = new Vector2(-54f, -96f) * DefaultWindowScale;
        public static readonly Vector2 DefaultNpcWindowPosition = new(0f, 20f);

        [SerializeField] private Image backgroundImage;
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private Text titleText;
        [SerializeField] private Button closeButton;
        [SerializeField] private bool deactivateOnClose = true;

        private Action closeRequested;

        public RectTransform ContentRoot
        {
            get
            {
                ResolveReferences();
                return contentRoot != null ? contentRoot : (RectTransform)transform;
            }
        }

        public Text TitleText
        {
            get
            {
                ResolveReferences();
                return titleText;
            }
        }

        public Button CloseButton
        {
            get
            {
                ResolveReferences();
                return closeButton;
            }
        }

        private void Awake()
        {
            ResolveReferences();
            RegisterCloseButton();
        }

        private void OnDestroy()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(RequestClose);
            }
        }

        public static MMOStandardWindow Ensure(GameObject windowObject, string title, Action onClose)
        {
            MMOStandardWindow window = windowObject.GetComponent<MMOStandardWindow>();
            if (window == null)
            {
                window = windowObject.AddComponent<MMOStandardWindow>();
            }

            window.Initialize(title, onClose);
            return window;
        }

        public void Initialize(string title, Action onClose)
        {
            closeRequested = onClose;
            ((RectTransform)transform).sizeDelta = WindowSize;
            ResolveReferences();
            EnsureFallbackVisuals();
            RegisterCloseButton();
            SetTitle(title);
        }

        public void SetTitle(string title)
        {
            ResolveReferences();
            if (titleText != null && !string.IsNullOrWhiteSpace(title))
            {
                titleText.text = title;
            }
        }

        public static void ApplyDefaultPlacement(RectTransform root)
        {
            if (root == null)
            {
                return;
            }

            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = DefaultNpcWindowPosition;
            root.sizeDelta = WindowSize;
        }

        public RectTransform FindRect(string objectName)
        {
            ResolveReferences();
            return FindChild<RectTransform>(ContentRoot, objectName);
        }

        public Text FindText(string objectName)
        {
            ResolveReferences();
            return FindChild<Text>(ContentRoot, objectName);
        }

        public Button FindButton(string objectName)
        {
            ResolveReferences();
            return FindChild<Button>(ContentRoot, objectName);
        }

        private void RequestClose()
        {
            if (closeRequested != null)
            {
                closeRequested.Invoke();
                return;
            }

            if (deactivateOnClose)
            {
                gameObject.SetActive(false);
            }
        }

        private void RegisterCloseButton()
        {
            if (closeButton == null)
            {
                return;
            }

            closeButton.onClick.RemoveListener(RequestClose);
            closeButton.onClick.AddListener(RequestClose);
        }

        private void ResolveReferences()
        {
            backgroundImage ??= GetComponent<Image>();
            contentRoot ??= FindChild<RectTransform>(transform, "Content");
            titleText ??= FindChild<Text>(transform, "Title");
            closeButton ??= FindChild<Button>(transform, "Close Button") ?? FindChild<Button>(transform, "Close");
        }

        private void EnsureFallbackVisuals()
        {
            if (backgroundImage == null)
            {
                backgroundImage = gameObject.AddComponent<Image>();
                backgroundImage.color = MMONpcWindowFrame.BackgroundColor;
            }

            if (contentRoot == null)
            {
                contentRoot = MMOUiFactory.CreateRect("Content", transform);
                MMOUiFactory.Stretch(contentRoot);
                contentRoot.offsetMin = DefaultContentOffsetMin;
                contentRoot.offsetMax = DefaultContentOffsetMax;
            }

            if (titleText == null)
            {
                titleText = MMOUiFactory.CreateText("Title", transform, 18, FontStyle.Bold, TextAnchor.MiddleLeft);
                titleText.color = MMONpcWindowFrame.TitleColor;
                titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
                titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
                titleText.rectTransform.pivot = new Vector2(0f, 1f);
                titleText.rectTransform.anchoredPosition = new Vector2(92f, -34f) * DefaultWindowScale;
                titleText.rectTransform.sizeDelta = new Vector2(-220f, 42f) * DefaultWindowScale;
            }

            if (closeButton == null)
            {
                closeButton = MMOUiFactory.CreateTextButton("Close Button", transform, string.Empty, CloseButtonSize, MMONpcWindowFrame.ButtonColor);
                RectTransform closeRect = closeButton.GetComponent<RectTransform>();
                closeRect.anchorMin = new Vector2(1f, 1f);
                closeRect.anchorMax = new Vector2(1f, 1f);
                closeRect.pivot = new Vector2(0.5f, 0.5f);
                closeRect.anchoredPosition = DefaultCloseButtonPosition;
            }
        }

        private static T FindChild<T>(Transform root, string objectName) where T : Component
        {
            if (root == null || string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            T[] children = root.GetComponentsInChildren<T>(true);
            foreach (T child in children)
            {
                if (child.name == objectName)
                {
                    return child;
                }
            }

            return null;
        }
    }
}
