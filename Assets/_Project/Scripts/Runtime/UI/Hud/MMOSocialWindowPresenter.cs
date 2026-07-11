using RPGClone.CharacterSelection;
using RPGClone.Multiplayer;
using RPGClone.Services;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RPGClone.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class MMOSocialWindowPresenter : MonoBehaviour
    {
        private const float RefreshSeconds = 1f;

        private Text currentCharacterText;
        private Text sessionText;
        private Text statusText;
        private InputField joinCodeInput;
        private float nextRefreshTime;
        private string displayedJoinCode;

        private void Awake()
        {
            BuildIfNeeded();
        }

        private void OnEnable()
        {
            BuildIfNeeded();
            RefreshNow();
        }

        private void Update()
        {
            if (Time.unscaledTime >= nextRefreshTime)
            {
                RefreshNow();
            }
        }

        public void Toggle()
        {
            gameObject.SetActive(!gameObject.activeSelf);
            if (gameObject.activeSelf)
            {
                RefreshNow();
            }
        }

        private void BuildIfNeeded()
        {
            if (joinCodeInput != null)
            {
                return;
            }

            bool hasAuthoredLayout = MMOStandardWindow.HasAuthoredWindowLayout(gameObject);
            if (!hasAuthoredLayout && transform.childCount > 0)
            {
                MMOUiFactory.DestroyChildren(transform);
            }

            RectTransform root = (RectTransform)transform;
            if (!hasAuthoredLayout)
            {
                MMOStandardWindow.ApplySecondaryPlacement(root);
            }

            MMOStandardWindow window = MMOStandardWindow.Ensure(gameObject, "Online Session", () => gameObject.SetActive(false));
            RectTransform content = window.ContentRoot;

            currentCharacterText = CreateText(content, "Current Character", 14, FontStyle.Bold, -18f, 28f);
            sessionText = CreateText(content, "Session", 12, FontStyle.Normal, -56f, 54f);
            sessionText.color = new Color(0.74f, 0.68f, 0.58f, 1f);

            Text instructions = CreateText(content, "Instructions", 12, FontStyle.Normal, -124f, 72f);
            instructions.text = "Every adventure runs in a Unity Relay session. Share your join code with friends, or enter theirs to join the same world.";
            instructions.horizontalOverflow = HorizontalWrapMode.Wrap;
            instructions.verticalOverflow = VerticalWrapMode.Overflow;

            joinCodeInput = CreateInput(content, new Vector2(14f, -218f), new Vector2(220f, 36f));

            Button hostButton = CreateButton(content, "Host Session", "Host", new Vector2(-176f, -218f), 90f);
            hostButton.onClick.AddListener(HostSession);

            Button copyButton = CreateButton(content, "Copy Join Code", "Copy", new Vector2(-92f, -218f), 70f);
            copyButton.onClick.AddListener(CopyJoinCode);

            Button joinButton = CreateButton(content, "Join Session", "Join", new Vector2(-14f, -218f), 70f);
            joinButton.onClick.AddListener(JoinSession);

            statusText = CreateText(content, "Status", 12, FontStyle.Bold, -276f, 68f);
            statusText.color = new Color(1f, 0.84f, 0.38f, 1f);
        }

        private static Text CreateText(
            Transform parent,
            string objectName,
            int fontSize,
            FontStyle fontStyle,
            float y,
            float height)
        {
            Text text = MMOUiFactory.CreateText(objectName, parent, fontSize, fontStyle, TextAnchor.UpperLeft);
            text.rectTransform.anchorMin = new Vector2(0f, 1f);
            text.rectTransform.anchorMax = new Vector2(1f, 1f);
            text.rectTransform.pivot = new Vector2(0f, 1f);
            text.rectTransform.anchoredPosition = new Vector2(14f, y);
            text.rectTransform.sizeDelta = new Vector2(-28f, height);
            return text;
        }

        private static InputField CreateInput(Transform parent, Vector2 position, Vector2 size)
        {
            Image image = MMOUiFactory.CreateImage("Join Code Input", parent, new Color(0.018f, 0.016f, 0.014f, 0.98f));
            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            InputField input = image.gameObject.AddComponent<InputField>();
            Text value = MMOUiFactory.CreateText("Text", rect, 14, FontStyle.Bold, TextAnchor.MiddleLeft);
            value.rectTransform.anchorMin = Vector2.zero;
            value.rectTransform.anchorMax = Vector2.one;
            value.rectTransform.offsetMin = new Vector2(10f, 3f);
            value.rectTransform.offsetMax = new Vector2(-10f, -3f);

            Text placeholder = MMOUiFactory.CreateText("Placeholder", rect, 13, FontStyle.Italic, TextAnchor.MiddleLeft);
            placeholder.text = "Unity join code";
            placeholder.color = new Color(0.55f, 0.49f, 0.41f, 1f);
            placeholder.rectTransform.anchorMin = Vector2.zero;
            placeholder.rectTransform.anchorMax = Vector2.one;
            placeholder.rectTransform.offsetMin = new Vector2(10f, 3f);
            placeholder.rectTransform.offsetMax = new Vector2(-10f, -3f);

            input.textComponent = value;
            input.placeholder = placeholder;
            input.characterLimit = 32;
            return input;
        }

        private static Button CreateButton(
            Transform parent,
            string objectName,
            string label,
            Vector2 position,
            float width)
        {
            Button button = MMOUiFactory.CreateTextButton(
                objectName,
                parent,
                label,
                new Vector2(width, 36f),
                MMONpcWindowFrame.ButtonColor);
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = position;
            return button;
        }

        private void RefreshNow()
        {
            nextRefreshTime = Time.unscaledTime + RefreshSeconds;
            BuildIfNeeded();

            currentCharacterText.text = MMOCharacterSession.HasSelectedCharacter
                ? $"Playing as {MMOCharacterSession.SelectedCharacter.DisplayName}"
                : "No character selected";

            string joinCode = MMOGameplaySessionService.JoinCode;
            if (MMOGameplaySessionService.IsHosting)
            {
                sessionText.text = FormatHostedSessionText(joinCode);
                DisplayHostedJoinCode(joinCode, false);
                return;
            }

            sessionText.text = MMONetcodeSessionService.IsConnected
                ? $"Connected to session {MMOGameplaySessionService.SessionId}"
                : MMONetcodeSessionService.IsConnecting
                    ? "Connecting to Unity multiplayer services..."
                    : "No active Unity multiplayer session.";
        }

        private async void HostSession()
        {
            SetStatus("Creating a Unity Relay session...");
            bool hosted = await MMOGameplaySessionService.StartHostedSessionAsync();
            string joinCode = MMOGameplaySessionService.JoinCode;
            if (!hosted)
            {
                SetStatus($"Host failed: {MMOGameplaySessionService.LastError}");
                return;
            }

            DisplayHostedJoinCode(joinCode, true);
            SetStatus(string.IsNullOrWhiteSpace(joinCode)
                ? "Session started, but Unity did not return a join code."
                : $"Join code copied: {joinCode}");
        }

        private async void JoinSession()
        {
            if (joinCodeInput == null || string.IsNullOrWhiteSpace(joinCodeInput.text))
            {
                SetStatus("Enter a Unity join code.");
                return;
            }

            string joinCode = joinCodeInput.text.Trim();
            SetStatus("Joining Unity Relay session...");
            bool joined = await MMOGameplaySessionService.JoinHostedSessionAsync(
                joinCode,
                SceneManager.GetActiveScene().name,
                string.Empty);
            SetStatus(joined
                ? $"Joined session {MMOGameplaySessionService.SessionId}."
                : $"Join failed: {MMOGameplaySessionService.LastError}");
            RefreshNow();
        }

        private void CopyJoinCode()
        {
            string joinCode = MMOGameplaySessionService.JoinCode;
            if (string.IsNullOrWhiteSpace(joinCode))
            {
                SetStatus("Host a session before copying its join code.");
                return;
            }

            DisplayHostedJoinCode(joinCode, true);
            SetStatus($"Join code copied: {joinCode}");
        }

        private void DisplayHostedJoinCode(string joinCode, bool copyToClipboard)
        {
            if (string.IsNullOrWhiteSpace(joinCode))
            {
                return;
            }

            sessionText.text = FormatHostedSessionText(joinCode);
            if (displayedJoinCode != joinCode)
            {
                joinCodeInput.SetTextWithoutNotify(joinCode);
                displayedJoinCode = joinCode;
            }

            if (copyToClipboard)
            {
                GUIUtility.systemCopyBuffer = joinCode;
            }
        }

        private static string FormatHostedSessionText(string joinCode)
        {
            string sceneName = SceneManager.GetActiveScene().name;
            return string.IsNullOrWhiteSpace(joinCode)
                ? $"Hosting {sceneName}; waiting for Unity join code."
                : $"Hosting {sceneName} — Join Code: {joinCode}";
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message ?? string.Empty;
            }
        }
    }
}
