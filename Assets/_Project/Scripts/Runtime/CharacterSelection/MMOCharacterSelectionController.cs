using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RPGClone.Characters;
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
        private Text createOrBackButtonLabel;
        private bool creatingCharacter;

        private async void Start()
        {
            repository = useCloudSave ? new MMOCloudCharacterRosterRepository() : new MMOLocalCharacterRosterRepository();
            BuildSceneIfNeeded();
            await LoadRosterAsync();
        }

        private void Update()
        {
            if (previewModel != null)
            {
                previewModel.transform.Rotate(0f, 22f * Time.deltaTime, 0f, Space.World);
            }
        }

        public void Configure(MMOCharacterArchetypeCatalog catalog, string worldSceneName)
        {
            archetypeCatalog = catalog;
            gameplaySceneName = string.IsNullOrWhiteSpace(worldSceneName) ? gameplaySceneName : worldSceneName;
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

            selectedCharacter = roster.characters[0];
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
            titleText.text = creatingCharacter ? "Create Character" : "Character Selection";
            RefreshBottomButtons();
            RefreshCharacterList();
            RefreshCreatePanel();
            RefreshInfo();
            RefreshPreview();
        }

        private void RefreshBottomButtons()
        {
            if (enterWorldButton != null)
            {
                enterWorldButton.gameObject.SetActive(!creatingCharacter);
            }

            if (deleteButton != null)
            {
                deleteButton.gameObject.SetActive(!creatingCharacter);
            }

            if (createOrBackButtonLabel != null)
            {
                createOrBackButtonLabel.text = creatingCharacter ? "Back" : "Create";
            }
        }

        private void RefreshCharacterList()
        {
            characterListPanel.gameObject.SetActive(!creatingCharacter);
            MMOUiFactory.DestroyChildren(characterListPanel);
            characterButtons.Clear();
            if (creatingCharacter)
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
            createPanel.gameObject.SetActive(creatingCharacter);
            if (!creatingCharacter)
            {
                return;
            }

            Text raceHeader = CreatePanelHeader(createPanel, "Race", -24f);
            raceHeader.color = new Color(1f, 0.86f, 0.45f);
            CreateChoiceButton(createPanel, "Orc", selectedRace == MMOPlayableRace.Orc, -78f, () => SelectRace(MMOPlayableRace.Orc));
            CreateChoiceButton(createPanel, "Troll", selectedRace == MMOPlayableRace.Troll, -138f, () => SelectRace(MMOPlayableRace.Troll));

            Text classHeader = CreatePanelHeader(createPanel, "Class", -226f);
            classHeader.color = new Color(1f, 0.86f, 0.45f);
            CreateChoiceButton(createPanel, "Warrior", selectedClass == MMOPlayableClass.Warrior, -280f, () => SelectClass(MMOPlayableClass.Warrior));
            CreateChoiceButton(createPanel, "Mage", selectedClass == MMOPlayableClass.Mage, -340f, () => SelectClass(MMOPlayableClass.Mage));
            CreateChoiceButton(createPanel, "Shaman", selectedClass == MMOPlayableClass.Shaman, -400f, () => SelectClass(MMOPlayableClass.Shaman));

            Button finish = MMOUiFactory.CreateTextButton("Finish", createPanel, "Create Character", new Vector2(330f, 48f), new Color(0.2f, 0.13f, 0.06f, 0.96f));
            finish.onClick.AddListener(CreateCharacter);
            RectTransform rect = finish.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 28f);
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
            infoPanel.gameObject.SetActive(creatingCharacter);
            if (!creatingCharacter)
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
            return CreateCharacterData("Grommash");
        }

        private MMOCharacterSaveData CreateCharacterData(string characterName)
        {
            return new MMOCharacterSaveData
            {
                characterId = Guid.NewGuid().ToString("N"),
                characterName = characterName,
                race = selectedRace,
                characterClass = selectedClass,
                level = 1,
                sceneName = gameplaySceneName,
                position = new Vector3SaveData(Vector3.zero),
                rotationEuler = new Vector3SaveData(Vector3.zero)
            };
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
            Refresh();
        }

        private async void CreateCharacter()
        {
            string generatedName = $"{selectedRace} {selectedClass} {roster.characters.Count + 1}";
            selectedCharacter = CreateCharacterData(generatedName);
            roster.characters.Add(selectedCharacter);
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
            if (selectedCharacter == null)
            {
                SetStatus("Select a character first.");
                return;
            }

            await SaveRosterAsync();
            MMOCharacterSession.Select(selectedCharacter);
            string sceneName = string.IsNullOrWhiteSpace(selectedCharacter.sceneName) ? gameplaySceneName : selectedCharacter.sceneName;
            SceneManager.LoadScene(sceneName);
        }

        private async Task SaveRosterAsync()
        {
            SetStatus("Saving...");
            await repository.SaveAsync(roster);
            SetStatus(string.Empty);
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
