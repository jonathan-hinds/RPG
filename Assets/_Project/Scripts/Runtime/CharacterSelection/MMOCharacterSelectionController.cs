using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RPGClone.Characters;
using RPGClone.Combat;
using RPGClone.Inventory;
using RPGClone.Social;
using RPGClone.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RPGClone.CharacterSelection
{
    public sealed class MMOCharacterSelectionController : MonoBehaviour
    {
        [SerializeField] private MMOCharacterArchetypeCatalog archetypeCatalog;
        [SerializeField] private string gameplaySceneName = "OrcishStarterValley";
        [SerializeField] private bool useCloudSave = true;
        [SerializeField] private Transform previewRoot;
        [SerializeField] private Camera previewCamera;

        private readonly List<Button> characterButtons = new();
        private MMOCharacterRosterRepository repository;
        private MMOCharacterRosterSaveData roster = new();
        private MMOCharacterSaveData selectedCharacter;
        private MMOPlayableRace selectedRace = MMOPlayableRace.Orc;
        private MMOPlayableClass selectedClass = MMOPlayableClass.Warrior;
        private GameObject previewModel;
        private RectTransform root;
        private RectTransform characterListPanel;
        private RectTransform createPanel;
        private RectTransform infoPanel;
        private Text titleText;
        private Text statusText;
        private Text infoText;
        private Button enterWorldButton;
        private Button createOrBackButton;
        private Button deleteButton;
        private RectTransform accountPanel;
        private Text createOrBackButtonLabel;
        private Text accountStatusText;
        private InputField accountNameInput;
        private InputField accountPasswordInput;
        private string pendingCharacterName = string.Empty;
        private float nextAccountHeartbeatTime;
        private bool creatingCharacter;

        private async void Start()
        {
            BuildSceneIfNeeded();
            if (MMOSocialIdentityService.IsAuthenticated)
            {
                repository = CreateRepository();
                await LoadRosterAsync();
            }
            else
            {
                roster = new MMOCharacterRosterSaveData();
                selectedCharacter = null;
                MMOCharacterSession.Clear();
                Refresh();
                SetStatus("Log in or register an account.");
            }
        }

        private void Update()
        {
            if (previewModel != null)
            {
                previewModel.transform.Rotate(0f, 22f * Time.deltaTime, 0f, Space.World);
            }

            if (MMOSocialIdentityService.IsAuthenticated && Time.unscaledTime >= nextAccountHeartbeatTime)
            {
                nextAccountHeartbeatTime = Time.unscaledTime + 5f;
                MMOServiceResult heartbeat = MMOSocialIdentityService.Heartbeat();
                if (!heartbeat.Succeeded)
                {
                    HandleAccountSessionLost(heartbeat.Message);
                }
            }
        }

        private void OnApplicationQuit()
        {
            MMOSocialIdentityService.Logout();
        }

        public void Configure(MMOCharacterArchetypeCatalog catalog, string worldSceneName)
        {
            archetypeCatalog = catalog;
            gameplaySceneName = string.IsNullOrWhiteSpace(worldSceneName) ? gameplaySceneName : worldSceneName;
        }

        private MMOCharacterRosterRepository CreateRepository()
        {
            return useCloudSave ? new MMOCloudCharacterRosterRepository() : new MMOLocalCharacterRosterRepository();
        }

        private async Task LoadRosterAsync()
        {
            SetStatus("Loading characters...");
            roster = await repository.LoadAsync();
            roster.characters ??= new List<MMOCharacterSaveData>();
            if (roster.characters.Count == 0)
            {
                roster.characters.Add(CreateDefaultCharacter());
                await repository.SaveAsync(roster);
            }

            bool migrated = NormalizeRosterCharacters();
            foreach (MMOCharacterSaveData character in roster.characters)
            {
                await MMOSocialPresenceController.RegisterCharacterNameAsync(character);
            }

            if (migrated)
            {
                await repository.SaveAsync(roster);
            }

            selectedCharacter = roster.characters[0];
            MMOCharacterSession.Select(selectedCharacter);
            await MMOSocialPresenceController.SetSelectedCharacterPresenceAsync(MMOCharacterPresenceStatus.OnlineCharacterSelect, false);
            creatingCharacter = false;
            Refresh();
            SetStatus(string.Empty);
        }

        private void BuildSceneIfNeeded()
        {
            if (previewCamera == null)
            {
                previewCamera = Camera.main;
            }

            if (previewRoot == null)
            {
                previewRoot = new GameObject("Character Preview Root").transform;
                previewRoot.position = Vector3.zero;
            }

            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObject = new("Character Select Canvas");
                canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
                canvasObject.AddComponent<GraphicRaycaster>();
            }

            root = canvas.GetComponent<RectTransform>();
            BuildTitle();
            BuildCharacterList();
            BuildCreatePanel();
            BuildInfoPanel();
            BuildBottomButtons();
            BuildAccountPanel();
        }

        private void BuildTitle()
        {
            titleText = MMOUiFactory.CreateText("Title", root, 28, FontStyle.Bold, TextAnchor.UpperCenter);
            titleText.text = "Character Selection";
            titleText.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            titleText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            titleText.rectTransform.pivot = new Vector2(0.5f, 1f);
            titleText.rectTransform.anchoredPosition = new Vector2(0f, -30f);
            titleText.rectTransform.sizeDelta = new Vector2(520f, 48f);

            statusText = MMOUiFactory.CreateText("Status", root, 18, FontStyle.Bold, TextAnchor.MiddleCenter);
            statusText.color = new Color(1f, 0.84f, 0.38f);
            statusText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            statusText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            statusText.rectTransform.pivot = new Vector2(0.5f, 0f);
            statusText.rectTransform.anchoredPosition = new Vector2(0f, 104f);
            statusText.rectTransform.sizeDelta = new Vector2(720f, 34f);
        }

        private void BuildCharacterList()
        {
            Image panel = MMOUiFactory.CreateImage("Character List", root, new Color(0.035f, 0.03f, 0.026f, 0.86f));
            characterListPanel = panel.rectTransform;
            characterListPanel.anchorMin = new Vector2(1f, 0.5f);
            characterListPanel.anchorMax = new Vector2(1f, 0.5f);
            characterListPanel.pivot = new Vector2(1f, 0.5f);
            characterListPanel.anchoredPosition = new Vector2(-46f, 20f);
            characterListPanel.sizeDelta = new Vector2(380f, 660f);
        }

        private void BuildCreatePanel()
        {
            Image panel = MMOUiFactory.CreateImage("Create Character Panel", root, new Color(0.035f, 0.03f, 0.026f, 0.86f));
            createPanel = panel.rectTransform;
            createPanel.anchorMin = new Vector2(0f, 0.5f);
            createPanel.anchorMax = new Vector2(0f, 0.5f);
            createPanel.pivot = new Vector2(0f, 0.5f);
            createPanel.anchoredPosition = new Vector2(46f, 20f);
            createPanel.sizeDelta = new Vector2(420f, 660f);
        }

        private void BuildInfoPanel()
        {
            Image panel = MMOUiFactory.CreateImage("Creation Info", root, new Color(0.035f, 0.03f, 0.026f, 0.86f));
            infoPanel = panel.rectTransform;
            infoPanel.anchorMin = new Vector2(1f, 0.5f);
            infoPanel.anchorMax = new Vector2(1f, 0.5f);
            infoPanel.pivot = new Vector2(1f, 0.5f);
            infoPanel.anchoredPosition = new Vector2(-46f, 20f);
            infoPanel.sizeDelta = new Vector2(380f, 660f);

            infoText = MMOUiFactory.CreateText("Info", infoPanel, 17, FontStyle.Normal, TextAnchor.UpperLeft);
            infoText.rectTransform.anchorMin = Vector2.zero;
            infoText.rectTransform.anchorMax = Vector2.one;
            infoText.rectTransform.offsetMin = new Vector2(24f, 24f);
            infoText.rectTransform.offsetMax = new Vector2(-24f, -24f);
        }

        private void BuildBottomButtons()
        {
            RectTransform bottom = MMOUiFactory.CreateRect("Bottom Buttons", root);
            bottom.anchorMin = new Vector2(0.5f, 0f);
            bottom.anchorMax = new Vector2(0.5f, 0f);
            bottom.pivot = new Vector2(0.5f, 0f);
            bottom.anchoredPosition = new Vector2(0f, 34f);
            bottom.sizeDelta = new Vector2(620f, 62f);

            enterWorldButton = CreateBottomButton(bottom, "Enter World", -184f, EnterWorld);
            createOrBackButton = CreateBottomButton(bottom, "Create", 0f, ToggleCreateCharacter);
            deleteButton = CreateBottomButton(bottom, "Delete", 184f, DeleteSelectedCharacter);
            createOrBackButtonLabel = createOrBackButton.GetComponentInChildren<Text>();
        }

        private void BuildAccountPanel()
        {
            Image panel = MMOUiFactory.CreateImage("Account Panel", root, new Color(0.035f, 0.03f, 0.026f, 0.88f));
            accountPanel = panel.rectTransform;
            accountPanel.anchorMin = new Vector2(0f, 1f);
            accountPanel.anchorMax = new Vector2(0f, 1f);
            accountPanel.pivot = new Vector2(0f, 1f);
            accountPanel.anchoredPosition = new Vector2(46f, -34f);
            accountPanel.sizeDelta = new Vector2(420f, 196f);
            RefreshAccountPanel();
        }

        private Button CreateBottomButton(Transform parent, string label, float x, UnityEngine.Events.UnityAction action)
        {
            Button button = MMOUiFactory.CreateTextButton(label, parent, label, new Vector2(172f, 48f), new Color(0.18f, 0.12f, 0.06f, 0.96f));
            button.onClick.AddListener(action);
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, 0f);
            return button;
        }

        private void Refresh()
        {
            titleText.text = !MMOSocialIdentityService.IsAuthenticated
                ? "Account Login"
                : creatingCharacter ? "Create Character" : "Character Selection";
            RefreshBottomButtons();
            RefreshCharacterList();
            RefreshCreatePanel();
            RefreshInfo();
            RefreshPreview();
            RefreshAccountPanel();
        }

        private void RefreshAccountPanel()
        {
            if (accountPanel == null)
            {
                return;
            }

            MMOUiFactory.DestroyChildren(accountPanel);
            if (MMOSocialIdentityService.IsAuthenticated)
            {
                accountPanel.sizeDelta = new Vector2(420f, 104f);
                Text label = MMOUiFactory.CreateText("Account", accountPanel, 16, FontStyle.Bold, TextAnchor.UpperLeft);
                label.text = $"Account: {MMOSocialIdentityService.AccountName}";
                label.rectTransform.anchorMin = new Vector2(0f, 1f);
                label.rectTransform.anchorMax = new Vector2(1f, 1f);
                label.rectTransform.pivot = new Vector2(0f, 1f);
                label.rectTransform.anchoredPosition = new Vector2(18f, -14f);
                label.rectTransform.sizeDelta = new Vector2(-128f, 30f);

                Button logout = MMOUiFactory.CreateTextButton("Logout", accountPanel, "Logout", new Vector2(96f, 34f), new Color(0.12f, 0.08f, 0.055f, 0.96f));
                logout.onClick.AddListener(LogoutAccount);
                RectTransform logoutRect = logout.GetComponent<RectTransform>();
                logoutRect.anchorMin = new Vector2(1f, 1f);
                logoutRect.anchorMax = new Vector2(1f, 1f);
                logoutRect.pivot = new Vector2(1f, 1f);
                logoutRect.anchoredPosition = new Vector2(-16f, -14f);

                accountStatusText = MMOUiFactory.CreateText("Account Status", accountPanel, 12, FontStyle.Normal, TextAnchor.UpperLeft);
                accountStatusText.text = "Characters on this account are available below.";
                accountStatusText.color = new Color(0.82f, 0.76f, 0.66f, 1f);
                accountStatusText.rectTransform.anchorMin = new Vector2(0f, 1f);
                accountStatusText.rectTransform.anchorMax = new Vector2(1f, 1f);
                accountStatusText.rectTransform.pivot = new Vector2(0f, 1f);
                accountStatusText.rectTransform.anchoredPosition = new Vector2(18f, -52f);
                accountStatusText.rectTransform.sizeDelta = new Vector2(-36f, 36f);
                return;
            }

            accountPanel.sizeDelta = new Vector2(420f, 196f);
            Text header = MMOUiFactory.CreateText("Header", accountPanel, 18, FontStyle.Bold, TextAnchor.UpperLeft);
            header.text = "Account Login";
            header.color = new Color(1f, 0.86f, 0.45f);
            header.rectTransform.anchorMin = new Vector2(0f, 1f);
            header.rectTransform.anchorMax = new Vector2(1f, 1f);
            header.rectTransform.pivot = new Vector2(0f, 1f);
            header.rectTransform.anchoredPosition = new Vector2(18f, -14f);
            header.rectTransform.sizeDelta = new Vector2(-36f, 26f);

            accountNameInput = CreateAccountInput(accountPanel, "Account Name", new Vector2(18f, -48f), "Account name", false);
            accountNameInput.SetTextWithoutNotify(MMOSocialIdentityService.LastAccountName);
            accountPasswordInput = CreateAccountInput(accountPanel, "Password", new Vector2(18f, -88f), "Password", true);

            Button login = MMOUiFactory.CreateTextButton("Login", accountPanel, "Login", new Vector2(118f, 34f), new Color(0.18f, 0.12f, 0.06f, 0.96f));
            login.onClick.AddListener(LoginAccount);
            RectTransform loginRect = login.GetComponent<RectTransform>();
            loginRect.anchorMin = new Vector2(0f, 1f);
            loginRect.anchorMax = new Vector2(0f, 1f);
            loginRect.pivot = new Vector2(0f, 1f);
            loginRect.anchoredPosition = new Vector2(18f, -130f);

            Button register = MMOUiFactory.CreateTextButton("Register", accountPanel, "Register", new Vector2(118f, 34f), new Color(0.18f, 0.12f, 0.06f, 0.96f));
            register.onClick.AddListener(RegisterAccount);
            RectTransform registerRect = register.GetComponent<RectTransform>();
            registerRect.anchorMin = new Vector2(0f, 1f);
            registerRect.anchorMax = new Vector2(0f, 1f);
            registerRect.pivot = new Vector2(0f, 1f);
            registerRect.anchoredPosition = new Vector2(148f, -130f);

            accountStatusText = MMOUiFactory.CreateText("Account Status", accountPanel, 12, FontStyle.Bold, TextAnchor.UpperLeft);
            accountStatusText.color = new Color(1f, 0.84f, 0.38f, 1f);
            accountStatusText.rectTransform.anchorMin = new Vector2(0f, 1f);
            accountStatusText.rectTransform.anchorMax = new Vector2(1f, 1f);
            accountStatusText.rectTransform.pivot = new Vector2(0f, 1f);
            accountStatusText.rectTransform.anchoredPosition = new Vector2(18f, -166f);
            accountStatusText.rectTransform.sizeDelta = new Vector2(-36f, 24f);
            accountStatusText.text = "Use separate accounts in each editor instance.";
        }

        private InputField CreateAccountInput(Transform parent, string objectName, Vector2 position, string placeholderText, bool password)
        {
            Image image = MMOUiFactory.CreateImage(objectName, parent, new Color(0.018f, 0.016f, 0.014f, 0.98f));
            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(384f, 32f);

            InputField input = image.gameObject.AddComponent<InputField>();
            input.contentType = password ? InputField.ContentType.Password : InputField.ContentType.Standard;
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
            return input;
        }

        private void RefreshBottomButtons()
        {
            bool canUseCharacters = MMOSocialIdentityService.IsAuthenticated;
            if (enterWorldButton != null)
            {
                enterWorldButton.gameObject.SetActive(canUseCharacters && !creatingCharacter);
            }

            if (deleteButton != null)
            {
                deleteButton.gameObject.SetActive(canUseCharacters && !creatingCharacter);
            }

            if (createOrBackButton != null)
            {
                createOrBackButton.gameObject.SetActive(canUseCharacters);
            }

            if (createOrBackButtonLabel != null)
            {
                createOrBackButtonLabel.text = creatingCharacter ? "Back" : "Create";
            }
        }

        private void RefreshCharacterList()
        {
            bool canUseCharacters = MMOSocialIdentityService.IsAuthenticated;
            characterListPanel.gameObject.SetActive(canUseCharacters && !creatingCharacter);
            MMOUiFactory.DestroyChildren(characterListPanel);
            characterButtons.Clear();
            if (creatingCharacter || !canUseCharacters)
            {
                return;
            }

            Text header = MMOUiFactory.CreateText("Header", characterListPanel, 20, FontStyle.Bold, TextAnchor.MiddleCenter);
            header.text = "Characters";
            header.rectTransform.anchorMin = new Vector2(0f, 1f);
            header.rectTransform.anchorMax = new Vector2(1f, 1f);
            header.rectTransform.pivot = new Vector2(0.5f, 1f);
            header.rectTransform.anchoredPosition = new Vector2(0f, -18f);
            header.rectTransform.sizeDelta = new Vector2(0f, 34f);

            for (int i = 0; i < roster.characters.Count; i++)
            {
                MMOCharacterSaveData character = roster.characters[i];
                Button button = MMOUiFactory.CreateTextButton(
                    $"Character {i + 1}",
                    characterListPanel,
                    $"{character.DisplayName}\nLevel {character.level} {character.race} {character.characterClass}",
                    new Vector2(330f, 64f),
                    character == selectedCharacter ? new Color(0.28f, 0.18f, 0.08f, 0.96f) : new Color(0.09f, 0.07f, 0.052f, 0.95f));
                int captured = i;
                button.onClick.AddListener(() =>
                {
                    creatingCharacter = false;
                    selectedCharacter = roster.characters[captured];
                    Refresh();
                });

                RectTransform rect = button.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -70f - i * 72f);
                characterButtons.Add(button);
            }
        }

        private void RefreshCreatePanel()
        {
            MMOUiFactory.DestroyChildren(createPanel);
            createPanel.gameObject.SetActive(MMOSocialIdentityService.IsAuthenticated && creatingCharacter);
            if (!creatingCharacter || !MMOSocialIdentityService.IsAuthenticated)
            {
                return;
            }

            Text nameHeader = CreatePanelHeader(createPanel, "Name", -24f);
            nameHeader.color = new Color(1f, 0.86f, 0.45f);
            CreateNameInput(createPanel, -72f);

            Text raceHeader = CreatePanelHeader(createPanel, "Race", -136f);
            raceHeader.color = new Color(1f, 0.86f, 0.45f);
            CreateChoiceButton(createPanel, "Orc", selectedRace == MMOPlayableRace.Orc, -190f, () => SelectRace(MMOPlayableRace.Orc));
            CreateChoiceButton(createPanel, "Troll", selectedRace == MMOPlayableRace.Troll, -250f, () => SelectRace(MMOPlayableRace.Troll));

            Text classHeader = CreatePanelHeader(createPanel, "Class", -338f);
            classHeader.color = new Color(1f, 0.86f, 0.45f);
            CreateChoiceButton(createPanel, "Warrior", selectedClass == MMOPlayableClass.Warrior, -392f, () => SelectClass(MMOPlayableClass.Warrior));
            CreateChoiceButton(createPanel, "Mage", selectedClass == MMOPlayableClass.Mage, -452f, () => SelectClass(MMOPlayableClass.Mage));
            CreateChoiceButton(createPanel, "Shaman", selectedClass == MMOPlayableClass.Shaman, -512f, () => SelectClass(MMOPlayableClass.Shaman));

            Button finish = MMOUiFactory.CreateTextButton("Finish", createPanel, "Create Character", new Vector2(330f, 48f), new Color(0.2f, 0.13f, 0.06f, 0.96f));
            finish.onClick.AddListener(CreateCharacter);
            RectTransform rect = finish.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 28f);
        }

        private void CreateNameInput(Transform parent, float y)
        {
            Image image = MMOUiFactory.CreateImage("Character Name Input", parent, new Color(0.018f, 0.016f, 0.014f, 0.98f));
            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(330f, 46f);

            InputField input = image.gameObject.AddComponent<InputField>();
            Text text = MMOUiFactory.CreateText("Text", rect, 16, FontStyle.Normal, TextAnchor.MiddleLeft);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(12f, 4f);
            text.rectTransform.offsetMax = new Vector2(-12f, -4f);

            Text placeholder = MMOUiFactory.CreateText("Placeholder", rect, 16, FontStyle.Italic, TextAnchor.MiddleLeft);
            placeholder.text = "Character name";
            placeholder.color = new Color(0.55f, 0.49f, 0.41f, 1f);
            placeholder.rectTransform.anchorMin = Vector2.zero;
            placeholder.rectTransform.anchorMax = Vector2.one;
            placeholder.rectTransform.offsetMin = new Vector2(12f, 4f);
            placeholder.rectTransform.offsetMax = new Vector2(-12f, -4f);

            input.textComponent = text;
            input.placeholder = placeholder;
            input.characterLimit = MMOCharacterNameUtility.MaximumLength;
            input.SetTextWithoutNotify(pendingCharacterName);
            input.onValueChanged.AddListener(value => pendingCharacterName = value);
        }

        private Text CreatePanelHeader(Transform parent, string text, float y)
        {
            Text header = MMOUiFactory.CreateText(text, parent, 20, FontStyle.Bold, TextAnchor.MiddleLeft);
            header.text = text;
            RectTransform rect = header.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(330f, 34f);
            return header;
        }

        private void CreateChoiceButton(Transform parent, string label, bool selected, float y, UnityEngine.Events.UnityAction action)
        {
            Button button = MMOUiFactory.CreateTextButton(label, parent, label, new Vector2(330f, 46f), selected ? new Color(0.28f, 0.18f, 0.08f, 0.96f) : new Color(0.08f, 0.065f, 0.05f, 0.94f));
            button.onClick.AddListener(action);
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
        }

        private void RefreshInfo()
        {
            infoPanel.gameObject.SetActive(MMOSocialIdentityService.IsAuthenticated && creatingCharacter);
            if (!creatingCharacter || !MMOSocialIdentityService.IsAuthenticated)
            {
                return;
            }

            MMOPlayableRace race = selectedRace;
            MMOPlayableClass characterClass = selectedClass;
            MMOCharacterArchetypeDefinition archetype = archetypeCatalog != null ? archetypeCatalog.Find(race, characterClass) : null;
            string header = $"{race} {characterClass}";
            infoText.text = archetype != null
                ? $"{header}\n\n{archetype.RaceDescription}\n\n{archetype.ClassDescription}"
                : $"{header}\n\nSelect a race and class.";
        }

        private void RefreshPreview()
        {
            if (previewModel != null)
            {
                Destroy(previewModel);
            }

            MMOPlayableRace race = creatingCharacter ? selectedRace : selectedCharacter?.race ?? MMOPlayableRace.Orc;
            MMOPlayableClass characterClass = creatingCharacter ? selectedClass : selectedCharacter?.characterClass ?? MMOPlayableClass.Warrior;
            MMOCharacterArchetypeDefinition archetype = archetypeCatalog != null ? archetypeCatalog.Find(race, characterClass) : null;
            previewModel = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            previewModel.name = "Selected Character Preview";
            previewModel.transform.SetParent(previewRoot, false);
            previewModel.transform.localPosition = Vector3.zero;
            previewModel.transform.localScale = race == MMOPlayableRace.Troll ? new Vector3(0.92f, 1.18f, 0.92f) : new Vector3(1f, 1.05f, 1f);

            Renderer renderer = previewModel.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = archetype != null ? archetype.ModelTint : Color.white;
            }
        }

        private MMOCharacterSaveData CreateDefaultCharacter()
        {
            selectedRace = MMOPlayableRace.Orc;
            selectedClass = MMOPlayableClass.Warrior;
            string characterId = Guid.NewGuid().ToString("N");
            return CreateCharacterData(MMOCharacterNameUtility.CreateFallbackName($"{selectedRace}{selectedClass}", characterId), characterId);
        }

        private MMOCharacterSaveData CreateCharacterData(string characterName, string characterId = null)
        {
            characterId = string.IsNullOrWhiteSpace(characterId) ? Guid.NewGuid().ToString("N") : characterId;
            if (!MMOCharacterNameUtility.TryValidate(characterName, out string displayName, out string normalizedName, out _))
            {
                displayName = MMOCharacterNameUtility.CreateFallbackName($"{selectedRace}{selectedClass}", characterId);
                normalizedName = MMOCharacterNameUtility.NormalizeLookupName(displayName);
            }

            MMOCharacterSaveData saveData = new()
            {
                characterId = characterId,
                accountId = MMOSocialIdentityService.AccountId,
                characterName = displayName,
                normalizedCharacterName = normalizedName,
                race = selectedRace,
                characterClass = selectedClass,
                level = 1,
                sceneName = gameplaySceneName,
                position = new Vector3SaveData(Vector3.zero),
                rotationEuler = new Vector3SaveData(Vector3.zero)
            };

            ApplyArchetypeStartingContent(saveData);
            return saveData;
        }

        private void ApplyArchetypeStartingContent(MMOCharacterSaveData saveData)
        {
            MMOCharacterArchetypeDefinition archetype = archetypeCatalog != null ? archetypeCatalog.Find(saveData.race, saveData.characterClass) : null;
            if (archetype == null)
            {
                return;
            }

            int slotIndex = 0;
            foreach (MMOItemStack stack in archetype.StartingInventoryItems)
            {
                if (stack == null || stack.IsEmpty)
                {
                    continue;
                }

                saveData.inventory.Add(new MMOInventorySlotSaveData
                {
                    slotIndex = slotIndex++,
                    itemId = stack.Item.ItemId,
                    quantity = stack.Quantity
                });
            }

            foreach (MMOItemDefinition item in archetype.StartingEquipment)
            {
                if (item == null || !item.IsEquipment)
                {
                    continue;
                }

                saveData.equipment.Add(new MMOEquipmentSlotSaveData
                {
                    slotType = item.EquipmentSlot,
                    itemId = item.ItemId
                });
            }

            AddStartingWeaponSkill(saveData, MMOWeaponType.Unarmed);
            foreach (MMOWeaponType weaponType in archetype.StartingWeaponSkills)
            {
                AddStartingWeaponSkill(saveData, weaponType);
            }

            foreach (MMOItemDefinition item in archetype.StartingEquipment)
            {
                if (item != null && item.WeaponType != MMOWeaponType.None)
                {
                    AddStartingWeaponSkill(saveData, item.WeaponType);
                }
            }
        }

        private static void AddStartingWeaponSkill(MMOCharacterSaveData saveData, MMOWeaponType weaponType)
        {
            if (weaponType == MMOWeaponType.None)
            {
                return;
            }

            foreach (MMOWeaponSkillSaveEntry existing in saveData.weaponSkills)
            {
                if (existing.weaponType == weaponType)
                {
                    return;
                }
            }

            saveData.weaponSkills.Add(new MMOWeaponSkillSaveEntry
            {
                weaponType = weaponType,
                skillValue = Mathf.Max(5, saveData.level * 5)
            });
        }

        private void SelectRace(MMOPlayableRace race)
        {
            selectedRace = race;
            Refresh();
        }

        private void SelectClass(MMOPlayableClass characterClass)
        {
            selectedClass = characterClass;
            Refresh();
        }

        private void ToggleCreateCharacter()
        {
            creatingCharacter = !creatingCharacter;
            if (creatingCharacter)
            {
                pendingCharacterName = string.Empty;
            }

            Refresh();
        }

        private async void CreateCharacter()
        {
            if (!MMOCharacterNameUtility.TryValidate(pendingCharacterName, out string displayName, out string normalizedName, out string error))
            {
                SetStatus(error);
                return;
            }

            if (roster.characters.Exists(character => character.normalizedCharacterName == normalizedName))
            {
                SetStatus($"{displayName} is already in your roster.");
                return;
            }

            MMOCharacterNameRecord existing = await MMOSocialServices.CharacterNames.FindByNameAsync(displayName);
            if (existing != null)
            {
                SetStatus($"{displayName} is already taken.");
                return;
            }

            selectedCharacter = CreateCharacterData(displayName);
            roster.characters.Add(selectedCharacter);
            MMOServiceResult registration = await MMOSocialServices.CharacterNames.RegisterOrUpdateAsync(new MMOCharacterNameRecord
            {
                playerId = MMOSocialIdentityService.AccountId,
                characterId = selectedCharacter.characterId,
                characterName = selectedCharacter.characterName,
                normalizedCharacterName = selectedCharacter.normalizedCharacterName
            });
            if (!registration.Succeeded)
            {
                roster.characters.Remove(selectedCharacter);
                selectedCharacter = roster.characters.Count > 0 ? roster.characters[0] : null;
                SetStatus(registration.Message);
                return;
            }

            await SaveRosterAsync();
            creatingCharacter = false;
            Refresh();
        }

        private async void DeleteSelectedCharacter()
        {
            if (selectedCharacter == null || roster.characters.Count <= 1)
            {
                SetStatus("At least one character is required.");
                return;
            }

            roster.characters.Remove(selectedCharacter);
            selectedCharacter = roster.characters.Count > 0 ? roster.characters[0] : null;
            await SaveRosterAsync();
            Refresh();
        }

        private async void EnterWorld()
        {
            if (!MMOSocialIdentityService.IsAuthenticated)
            {
                SetStatus("Log in before entering the world.");
                return;
            }

            if (selectedCharacter == null)
            {
                SetStatus("Select a character first.");
                return;
            }

            await SaveRosterAsync();
            MMOCharacterSession.Select(selectedCharacter);
            await MMOSocialPresenceController.RegisterSelectedCharacterNameAsync();
            string sceneName = string.IsNullOrWhiteSpace(selectedCharacter.sceneName) ? gameplaySceneName : selectedCharacter.sceneName;
            SceneManager.LoadScene(sceneName);
        }

        private async Task SaveRosterAsync()
        {
            SetStatus("Saving...");
            NormalizeRosterCharacters();
            await repository.SaveAsync(roster);
            SetStatus(string.Empty);
        }

        private bool NormalizeRosterCharacters()
        {
            bool changed = false;
            HashSet<string> usedNames = new();
            foreach (MMOCharacterSaveData character in roster.characters)
            {
                string previousId = character.characterId;
                string previousName = character.characterName;
                string previousNormalized = character.normalizedCharacterName;
                MMOSocialPresenceController.EnsureCharacterNameData(character);
                while (!usedNames.Add(character.normalizedCharacterName))
                {
                    character.characterName = MMOCharacterNameUtility.CreateFallbackName($"{character.race}{character.characterClass}", character.characterId + usedNames.Count);
                    character.normalizedCharacterName = MMOCharacterNameUtility.NormalizeLookupName(character.characterName);
                }

                changed |= previousId != character.characterId
                    || previousName != character.characterName
                    || previousNormalized != character.normalizedCharacterName;
            }

            return changed;
        }

        private async void LoginAccount()
        {
            MMOAccountServiceResult result = MMOSocialIdentityService.Login(accountNameInput != null ? accountNameInput.text : string.Empty, accountPasswordInput != null ? accountPasswordInput.text : string.Empty);
            if (!result.Succeeded)
            {
                SetAccountStatus(result.Message);
                SetStatus(result.Message);
                return;
            }

            await LoadAuthenticatedRosterAsync(result.Message);
        }

        private async void RegisterAccount()
        {
            MMOAccountServiceResult result = MMOSocialIdentityService.Register(accountNameInput != null ? accountNameInput.text : string.Empty, accountPasswordInput != null ? accountPasswordInput.text : string.Empty);
            if (!result.Succeeded)
            {
                SetAccountStatus(result.Message);
                SetStatus(result.Message);
                return;
            }

            await LoadAuthenticatedRosterAsync(result.Message);
        }

        private async Task LoadAuthenticatedRosterAsync(string message)
        {
            repository = CreateRepository();
            selectedCharacter = null;
            roster = new MMOCharacterRosterSaveData();
            creatingCharacter = false;
            Refresh();
            await LoadRosterAsync();
            SetStatus(message);
        }

        private async void LogoutAccount()
        {
            await MMOSocialPresenceController.SetSelectedCharacterOfflineAsync();
            MMOCharacterSession.Clear();
            MMOSocialIdentityService.Logout();
            repository = null;
            selectedCharacter = null;
            roster = new MMOCharacterRosterSaveData();
            creatingCharacter = false;
            Refresh();
            SetStatus("Logged out.");
        }

        private void HandleAccountSessionLost(string message)
        {
            MMOCharacterSession.Clear();
            MMOSocialIdentityService.Logout();
            repository = null;
            selectedCharacter = null;
            roster = new MMOCharacterRosterSaveData();
            creatingCharacter = false;
            Refresh();
            SetStatus(string.IsNullOrWhiteSpace(message) ? "Account session ended." : message);
        }

        private void SetAccountStatus(string message)
        {
            if (accountStatusText != null)
            {
                accountStatusText.text = message ?? string.Empty;
            }
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
        }
    }
}
