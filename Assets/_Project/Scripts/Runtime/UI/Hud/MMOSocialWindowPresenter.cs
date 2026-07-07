using System.Collections.Generic;
using System.Threading.Tasks;
using RPGClone.CharacterSelection;
using RPGClone.Multiplayer;
using RPGClone.Services;
using RPGClone.Social;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RPGClone.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class MMOSocialWindowPresenter : MonoBehaviour
    {
        private const float RefreshSeconds = 2f;

        private RectTransform friendsRoot;
        private RectTransform invitesRoot;
        private Text statusText;
        private Text sessionText;
        private InputField addFriendInput;
        private InputField joinCodeInput;
        private float nextRefreshTime;
        private bool refreshing;
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
            if (friendsRoot != null)
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

            MMOStandardWindow window = MMOStandardWindow.Ensure(gameObject, "Friends", () => gameObject.SetActive(false));
            RectTransform content = window.ContentRoot;

            Text currentCharacter = MMOUiFactory.CreateText("Current Character", content, 13, FontStyle.Bold, TextAnchor.UpperLeft);
            currentCharacter.text = MMOCharacterSession.HasSelectedCharacter
                ? $"Playing as {MMOCharacterSession.SelectedCharacter.DisplayName}"
                : "No character selected";
            currentCharacter.rectTransform.anchorMin = new Vector2(0f, 1f);
            currentCharacter.rectTransform.anchorMax = new Vector2(1f, 1f);
            currentCharacter.rectTransform.pivot = new Vector2(0f, 1f);
            currentCharacter.rectTransform.anchoredPosition = new Vector2(14f, -10f);
            currentCharacter.rectTransform.sizeDelta = new Vector2(-28f, 24f);

            sessionText = MMOUiFactory.CreateText("Session", content, 11, FontStyle.Normal, TextAnchor.UpperLeft);
            sessionText.color = new Color(0.74f, 0.68f, 0.58f, 1f);
            sessionText.rectTransform.anchorMin = new Vector2(0f, 1f);
            sessionText.rectTransform.anchorMax = new Vector2(1f, 1f);
            sessionText.rectTransform.pivot = new Vector2(0f, 1f);
            sessionText.rectTransform.anchoredPosition = new Vector2(14f, -34f);
            sessionText.rectTransform.sizeDelta = new Vector2(-28f, 20f);

            joinCodeInput = CreateInput(content, "Join Code Input", "Join code", new Vector2(14f, -68f), new Vector2(150f, 32f));
            Button hostButton = MMOUiFactory.CreateTextButton("Host Session", content, "Host", new Vector2(70f, 32f), MMONpcWindowFrame.ButtonColor);
            hostButton.onClick.AddListener(HostSession);
            RectTransform hostRect = hostButton.GetComponent<RectTransform>();
            hostRect.anchorMin = new Vector2(1f, 1f);
            hostRect.anchorMax = new Vector2(1f, 1f);
            hostRect.pivot = new Vector2(1f, 1f);
            hostRect.anchoredPosition = new Vector2(-92f, -68f);

            Button joinButton = MMOUiFactory.CreateTextButton("Join Session", content, "Join", new Vector2(70f, 32f), MMONpcWindowFrame.ButtonColor);
            joinButton.onClick.AddListener(JoinSession);
            RectTransform joinRect = joinButton.GetComponent<RectTransform>();
            joinRect.anchorMin = new Vector2(1f, 1f);
            joinRect.anchorMax = new Vector2(1f, 1f);
            joinRect.pivot = new Vector2(1f, 1f);
            joinRect.anchoredPosition = new Vector2(-14f, -68f);

            addFriendInput = CreateInput(content, "Add Friend Input", "Character name", new Vector2(14f, -108f), new Vector2(244f, 32f));
            Button addButton = MMOUiFactory.CreateTextButton("Add Friend", content, "Add", new Vector2(84f, 32f), MMONpcWindowFrame.ButtonColor);
            addButton.onClick.AddListener(AddFriend);
            RectTransform addRect = addButton.GetComponent<RectTransform>();
            addRect.anchorMin = new Vector2(1f, 1f);
            addRect.anchorMax = new Vector2(1f, 1f);
            addRect.pivot = new Vector2(1f, 1f);
            addRect.anchoredPosition = new Vector2(-14f, -108f);

            Text friendsHeader = CreateHeader(content, "Friends", -156f);
            friendsRoot = MMOUiFactory.CreateRect("Friends Rows", content);
            friendsRoot.anchorMin = new Vector2(0f, 1f);
            friendsRoot.anchorMax = new Vector2(1f, 1f);
            friendsRoot.pivot = new Vector2(0f, 1f);
            friendsRoot.anchoredPosition = new Vector2(14f, -184f);
            friendsRoot.sizeDelta = new Vector2(-28f, 210f);

            Text invitesHeader = CreateHeader(content, "Incoming Invites", -410f);
            invitesRoot = MMOUiFactory.CreateRect("Invite Rows", content);
            invitesRoot.anchorMin = new Vector2(0f, 1f);
            invitesRoot.anchorMax = new Vector2(1f, 1f);
            invitesRoot.pivot = new Vector2(0f, 1f);
            invitesRoot.anchoredPosition = new Vector2(14f, -438f);
            invitesRoot.sizeDelta = new Vector2(-28f, 104f);

            statusText = MMOUiFactory.CreateText("Status", content, 12, FontStyle.Bold, TextAnchor.LowerLeft);
            statusText.color = new Color(1f, 0.84f, 0.38f, 1f);
            statusText.rectTransform.anchorMin = new Vector2(0f, 0f);
            statusText.rectTransform.anchorMax = new Vector2(1f, 0f);
            statusText.rectTransform.pivot = new Vector2(0f, 0f);
            statusText.rectTransform.anchoredPosition = new Vector2(14f, 8f);
            statusText.rectTransform.sizeDelta = new Vector2(-28f, 36f);

            friendsHeader.text = "Friends";
            invitesHeader.text = "Incoming Invites";
        }

        private static Text CreateHeader(Transform parent, string label, float y)
        {
            Text header = MMOUiFactory.CreateText(label, parent, 13, FontStyle.Bold, TextAnchor.UpperLeft);
            header.text = label;
            header.color = MMONpcWindowFrame.TitleColor;
            header.rectTransform.anchorMin = new Vector2(0f, 1f);
            header.rectTransform.anchorMax = new Vector2(1f, 1f);
            header.rectTransform.pivot = new Vector2(0f, 1f);
            header.rectTransform.anchoredPosition = new Vector2(14f, y);
            header.rectTransform.sizeDelta = new Vector2(-28f, 22f);
            return header;
        }

        private static InputField CreateInput(Transform parent, string objectName, string placeholderText, Vector2 position, Vector2 size)
        {
            Image image = MMOUiFactory.CreateImage(objectName, parent, new Color(0.018f, 0.016f, 0.014f, 0.98f));
            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            InputField input = image.gameObject.AddComponent<InputField>();
            Text text = MMOUiFactory.CreateText("Text", rect, 13, FontStyle.Normal, TextAnchor.MiddleLeft);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(10f, 3f);
            text.rectTransform.offsetMax = new Vector2(-10f, -3f);

            Text placeholder = MMOUiFactory.CreateText("Placeholder", rect, 13, FontStyle.Italic, TextAnchor.MiddleLeft);
            placeholder.text = placeholderText;
            placeholder.color = new Color(0.55f, 0.49f, 0.41f, 1f);
            placeholder.rectTransform.anchorMin = Vector2.zero;
            placeholder.rectTransform.anchorMax = Vector2.one;
            placeholder.rectTransform.offsetMin = new Vector2(10f, 3f);
            placeholder.rectTransform.offsetMax = new Vector2(-10f, -3f);

            input.textComponent = text;
            input.placeholder = placeholder;
            input.characterLimit = MMOCharacterNameUtility.MaximumLength;
            return input;
        }

        private async void RefreshNow()
        {
            if (refreshing)
            {
                return;
            }

            refreshing = true;
            nextRefreshTime = Time.unscaledTime + RefreshSeconds;
            try
            {
                BuildIfNeeded();

                MMOUiFactory.DestroyChildren(friendsRoot);
                MMOUiFactory.DestroyChildren(invitesRoot);

                if (!MMOCharacterSession.HasSelectedCharacter)
                {
                    SetStatus("Select a character to use friends.");
                    return;
                }

                MMOCharacterSaveData current = MMOCharacterSession.SelectedCharacter;
                await EnsureLocalSessionAdvertisedAsync();
                MMOSessionPresenceRecord hostedSession = await MMOSocialServices.Sessions.GetHostedSessionForCharacterAsync(current.characterId);
                string joinCode = MMOGameplaySessionService.JoinCode;
                bool isHosting = MMOGameplaySessionService.IsLocalHostedSession
                    && (hostedSession != null || !string.IsNullOrWhiteSpace(joinCode));
                sessionText.text = isHosting
                    ? FormatHostedSessionText(joinCode)
                    : "No joinable session advertised.";
                if (isHosting)
                {
                    DisplayHostedJoinCode(joinCode, false);
                }

                IReadOnlyList<MMOFriendEntry> friends = await MMOSocialServices.Friends.GetFriendsAsync(current.characterId);
                if (friends.Count == 0)
                {
                    CreateEmpty(friendsRoot, "No friends yet.");
                }
                else
                {
                    for (int i = 0; i < friends.Count; i++)
                    {
                        MMOFriendEntry friend = friends[i];
                        MMOCharacterPresenceRecord presence = await MMOSocialServices.Presence.GetPresenceAsync(friend.characterId);
                        CreateFriendRow(friend, presence, i);
                    }
                }

                IReadOnlyList<MMOInviteRecord> invites = await MMOSocialServices.Invites.GetIncomingInvitesAsync(current.characterId);
                if (invites.Count == 0)
                {
                    CreateEmpty(invitesRoot, "No pending invites.");
                }
                else
                {
                    for (int i = 0; i < invites.Count; i++)
                    {
                        CreateInviteRow(invites[i], i);
                    }
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"Friends window refresh failed. {exception.Message}");
                SetStatus("Friends are temporarily unavailable.");
            }
            finally
            {
                refreshing = false;
            }
        }

        private void CreateFriendRow(MMOFriendEntry friend, MMOCharacterPresenceRecord presence, int index)
        {
            Image row = MMOUiFactory.CreateImage($"Friend {index + 1}", friendsRoot, new Color(0.052f, 0.044f, 0.034f, 0.96f));
            RectTransform rect = row.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(0f, -index * 46f);
            rect.sizeDelta = new Vector2(0f, 40f);

            Text name = MMOUiFactory.CreateText("Name", rect, 13, FontStyle.Bold, TextAnchor.MiddleLeft);
            name.text = friend.characterName;
            name.rectTransform.anchorMin = new Vector2(0f, 0f);
            name.rectTransform.anchorMax = new Vector2(0.44f, 1f);
            name.rectTransform.offsetMin = new Vector2(10f, 0f);
            name.rectTransform.offsetMax = new Vector2(-4f, 0f);

            Text status = MMOUiFactory.CreateText("Status", rect, 11, FontStyle.Normal, TextAnchor.MiddleLeft);
            status.text = FormatStatus(presence);
            status.color = StatusColor(presence);
            status.rectTransform.anchorMin = new Vector2(0.44f, 0f);
            status.rectTransform.anchorMax = new Vector2(0.72f, 1f);
            status.rectTransform.offsetMin = new Vector2(4f, 0f);
            status.rectTransform.offsetMax = new Vector2(-4f, 0f);

            Button invite = CreateSmallButton(rect, "Invite", "Invite", new Vector2(-84f, 0f));
            invite.interactable = CanInvite(presence);
            invite.onClick.AddListener(() => SendInvite(friend));

            Button remove = CreateSmallButton(rect, "Remove", "X", new Vector2(-30f, 0f));
            remove.onClick.AddListener(() => RemoveFriend(friend));
        }

        private void CreateInviteRow(MMOInviteRecord invite, int index)
        {
            Image row = MMOUiFactory.CreateImage($"Invite {index + 1}", invitesRoot, new Color(0.052f, 0.044f, 0.034f, 0.96f));
            RectTransform rect = row.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(0f, -index * 46f);
            rect.sizeDelta = new Vector2(0f, 40f);

            Text label = MMOUiFactory.CreateText("Label", rect, 12, FontStyle.Bold, TextAnchor.MiddleLeft);
            label.text = $"{invite.senderCharacterName} invited you";
            label.rectTransform.anchorMin = new Vector2(0f, 0f);
            label.rectTransform.anchorMax = new Vector2(1f, 1f);
            label.rectTransform.offsetMin = new Vector2(10f, 0f);
            label.rectTransform.offsetMax = new Vector2(-160f, 0f);

            Button accept = CreateSmallButton(rect, "Accept", "Accept", new Vector2(-116f, 0f), 70f);
            accept.onClick.AddListener(() => AcceptInvite(invite));

            Button decline = CreateSmallButton(rect, "Decline", "Decline", new Vector2(-42f, 0f), 70f);
            decline.onClick.AddListener(() => DeclineInvite(invite));
        }

        private static Button CreateSmallButton(Transform parent, string objectName, string label, Vector2 anchoredPosition, float width = 48f)
        {
            Button button = MMOUiFactory.CreateTextButton(objectName, parent, label, new Vector2(width, 26f), MMONpcWindowFrame.ButtonColor);
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            return button;
        }

        private static void CreateEmpty(Transform parent, string message)
        {
            Text empty = MMOUiFactory.CreateText("Empty", parent, 12, FontStyle.Italic, TextAnchor.UpperLeft);
            empty.text = message;
            empty.color = new Color(0.74f, 0.68f, 0.58f, 1f);
            empty.rectTransform.anchorMin = new Vector2(0f, 1f);
            empty.rectTransform.anchorMax = new Vector2(1f, 1f);
            empty.rectTransform.pivot = new Vector2(0f, 1f);
            empty.rectTransform.anchoredPosition = Vector2.zero;
            empty.rectTransform.sizeDelta = new Vector2(0f, 28f);
        }

        private async void AddFriend()
        {
            if (!MMOCharacterSession.HasSelectedCharacter)
            {
                SetStatus("Select a character first.");
                return;
            }

            MMOCharacterSaveData current = MMOCharacterSession.SelectedCharacter;
            MMOServiceResult result = await MMOSocialServices.Friends.AddFriendByNameAsync(current.characterId, current.characterName, addFriendInput.text);
            if (result.Succeeded)
            {
                addFriendInput.SetTextWithoutNotify(string.Empty);
            }

            SetStatus(result.Message);
            RefreshNow();
        }

        private async void HostSession()
        {
            MMOGameplaySessionService.StartLocalHostedSession();
            SetStatus("Hosting Unity multiplayer session...");
            bool hosted = await MMONetcodeSessionService.WaitForConnectionAsync();
            string joinCode = MMOGameplaySessionService.JoinCode;
            DisplayHostedJoinCode(joinCode, true);
            SetStatus(!hosted
                ? $"Host failed: {MMONetcodeSessionService.LastError}"
                : string.IsNullOrWhiteSpace(joinCode)
                ? "Host started, but Unity did not return a join code. Check the Console."
                : $"Hosted session code copied: {joinCode}");
            RefreshNow();
        }

        private async void JoinSession()
        {
            if (joinCodeInput == null || string.IsNullOrWhiteSpace(joinCodeInput.text))
            {
                SetStatus("Enter a join code.");
                return;
            }

            string joinCode = joinCodeInput.text.Trim();
            SetStatus("Joining Unity multiplayer session...");
            bool joined = await MMOGameplaySessionService.JoinHostedSessionAsync(joinCode, SceneManager.GetActiveScene().name, string.Empty);
            SetStatus(joined
                ? $"Joined Unity multiplayer session {MMOGameplaySessionService.SessionId}."
                : $"Join failed: {MMONetcodeSessionService.LastError}");
            RefreshNow();
        }

        private async void RemoveFriend(MMOFriendEntry friend)
        {
            MMOServiceResult result = await MMOSocialServices.Friends.RemoveFriendAsync(MMOCharacterSession.SelectedCharacter.characterId, friend.characterId);
            SetStatus(result.Message);
            RefreshNow();
        }

        private async void SendInvite(MMOFriendEntry friend)
        {
            if (!await EnsureLocalSessionAdvertisedAsync())
            {
                SetStatus("Unable to refresh your hosted session.");
                return;
            }

            MMOServiceResult result = await MMOSocialServices.Invites.SendInviteAsync(MMOCharacterSession.SelectedCharacter.characterId, friend.characterId);
            SetStatus(result.Message);
            RefreshNow();
        }

        private static async Task<bool> EnsureLocalSessionAdvertisedAsync()
        {
            try
            {
                await MMOSocialPresenceController.AdvertiseSelectedLocalSessionAsync();
                return true;
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"Hosted session advertisement failed. {exception.Message}");
                return false;
            }
        }

        private async void AcceptInvite(MMOInviteRecord invite)
        {
            MMOInviteResolution result = await MMOSocialServices.Invites.AcceptInviteAsync(invite.inviteId, MMOCharacterSession.SelectedCharacter.characterId);
            if (result.Succeeded && result.Session != null)
            {
                string joinCode = !string.IsNullOrWhiteSpace(result.Session.privateConnectionData)
                    ? result.Session.privateConnectionData
                    : result.Session.sessionId;
                MMOGameplaySessionService.JoinHostedSession(
                    joinCode,
                    result.Session.currentSceneName,
                    result.Session.hostCharacterId);
                SetStatus($"Joining {result.Session.hostCharacterName}'s world.");
                if (!string.IsNullOrWhiteSpace(result.Session.currentSceneName)
                    && SceneManager.GetActiveScene().name != result.Session.currentSceneName)
                {
                    SceneManager.LoadScene(result.Session.currentSceneName);
                    return;
                }
            }
            else
            {
                SetStatus(result.Message);
            }

            RefreshNow();
        }

        private async void DeclineInvite(MMOInviteRecord invite)
        {
            MMOServiceResult result = await MMOSocialServices.Invites.DeclineInviteAsync(invite.inviteId, MMOCharacterSession.SelectedCharacter.characterId);
            SetStatus(result.Message);
            RefreshNow();
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message ?? string.Empty;
            }
        }

        private static string FormatHostedSessionText(string joinCode)
        {
            string sceneName = SceneManager.GetActiveScene().name;
            return string.IsNullOrWhiteSpace(joinCode)
                ? $"Hosting {sceneName} - Waiting for Unity join code"
                : $"Hosting {sceneName} - Join Code: {joinCode}";
        }

        private void DisplayHostedJoinCode(string joinCode, bool copyToClipboard)
        {
            if (string.IsNullOrWhiteSpace(joinCode))
            {
                return;
            }

            if (sessionText != null)
            {
                sessionText.text = FormatHostedSessionText(joinCode);
            }

            if (joinCodeInput != null && displayedJoinCode != joinCode)
            {
                joinCodeInput.SetTextWithoutNotify(joinCode);
                displayedJoinCode = joinCode;
            }

            if (copyToClipboard)
            {
                GUIUtility.systemCopyBuffer = joinCode;
            }
        }

        private static bool CanInvite(MMOCharacterPresenceRecord presence)
        {
            return presence != null
                && presence.status != MMOCharacterPresenceStatus.Offline
                && presence.status != MMOCharacterPresenceStatus.BusyUnavailable;
        }

        private static string FormatStatus(MMOCharacterPresenceRecord presence)
        {
            if (presence == null || presence.status == MMOCharacterPresenceStatus.Offline)
            {
                return "Offline";
            }

            if (presence.status == MMOCharacterPresenceStatus.HostingJoinableSession && presence.joinsAllowed)
            {
                return $"Joinable - {presence.currentSceneName}";
            }

            return presence.status switch
            {
                MMOCharacterPresenceStatus.OnlineCharacterSelect => "Online",
                MMOCharacterPresenceStatus.OnlineInWorld => $"In World - {presence.currentSceneName}",
                MMOCharacterPresenceStatus.InvitedToSession => "Invited",
                MMOCharacterPresenceStatus.JoiningSession => "Joining",
                MMOCharacterPresenceStatus.BusyUnavailable => "Unavailable",
                _ => presence.status.ToString()
            };
        }

        private static Color StatusColor(MMOCharacterPresenceRecord presence)
        {
            if (presence == null || presence.status == MMOCharacterPresenceStatus.Offline)
            {
                return new Color(0.55f, 0.55f, 0.55f, 1f);
            }

            if (presence.status == MMOCharacterPresenceStatus.HostingJoinableSession)
            {
                return new Color(0.35f, 0.95f, 0.45f, 1f);
            }

            return new Color(0.66f, 0.84f, 1f, 1f);
        }
    }
}
