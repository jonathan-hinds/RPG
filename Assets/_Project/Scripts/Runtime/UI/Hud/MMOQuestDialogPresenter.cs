using System.Collections.Generic;
using RPGClone.Inventory;
using RPGClone.Quests;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RPGClone.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class MMOQuestDialogPresenter : MonoBehaviour
    {
        private RectTransform contentRoot;
        private RectTransform dynamicRoot;
        private Button goodbyeButton;
        private Button acceptButton;
        private Button declineButton;
        private Button backButton;
        private Button completeButton;
        private MMOQuestNpc npc;
        private MMOQuestLog questLog;
        private MMOQuestDefinition selectedQuest;
        private MMOItemDefinition selectedReward;
        private bool selectedQuestTurnIn;
        private bool usesGeneratedFrame;

        public static MMOQuestDialogPresenter Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            BuildFrame();
            Close();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public static void Open(MMOQuestNpc npc, MMOQuestLog questLog, Vector2 screenPosition)
        {
            ResolvePrefabPresenter()?.OpenNpc(npc, questLog);
        }

        public void OpenNpc(MMOQuestNpc newNpc, MMOQuestLog newQuestLog)
        {
            npc = newNpc;
            questLog = newQuestLog;
            selectedQuest = null;
            selectedReward = null;
            selectedQuestTurnIn = false;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            TrackNpcDistance();
            RefreshList();
        }

        public void Close()
        {
            selectedQuest = null;
            selectedReward = null;
            selectedQuestTurnIn = false;
            gameObject.SetActive(false);
        }

        private void TrackNpcDistance()
        {
            if (npc == null || questLog == null)
            {
                return;
            }

            MMONpcPanelDistanceCloser closer = gameObject.GetComponent<MMONpcPanelDistanceCloser>();
            if (closer == null)
            {
                closer = gameObject.AddComponent<MMONpcPanelDistanceCloser>();
            }

            closer.Track(npc.transform, questLog.transform, npc.InteractionDistance + 0.75f, Close);
        }

        private void BuildFrame()
        {
            bool hasAuthoredFrame = MMOStandardWindow.HasAuthoredWindowLayout(gameObject);
            usesGeneratedFrame = !hasAuthoredFrame;
            if (!hasAuthoredFrame && transform.childCount > 0)
            {
                MMOUiFactory.DestroyChildren(transform);
            }

            RectTransform root = (RectTransform)transform;
            MMOStandardWindow.ApplyDefaultPlacementIfGenerated(root);

            MMOStandardWindow window = MMOStandardWindow.Ensure(gameObject, "Quest", Close);
            contentRoot = window.ContentRoot;

            dynamicRoot = window.FindRect("Dynamic Content");
            if (dynamicRoot == null)
            {
                dynamicRoot = MMOUiFactory.CreateRect("Dynamic Content", contentRoot);
                MMOUiFactory.Stretch(dynamicRoot);
                dynamicRoot.offsetMin = new Vector2(0f, 50f);
                dynamicRoot.offsetMax = new Vector2(0f, 0f);
            }

            goodbyeButton = FindOrCreateQuestButton("Goodbye Button", "Goodbye", new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, MMOStandardWindow.QuestButtonSize);
            acceptButton = FindOrCreateQuestButton("Accept Button", "Accept", new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, MMOStandardWindow.QuestButtonSize);
            declineButton = FindOrCreateQuestButton("Decline Button", "Decline", new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, MMOStandardWindow.QuestButtonSize);
            backButton = FindOrCreateQuestButton("Back Button", "Back", new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, MMOStandardWindow.QuestButtonSize);
            completeButton = FindOrCreateQuestButton("Complete Button", "Complete Quest", new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, MMOStandardWindow.QuestButtonSize);
            HideActionButtons();
        }

        private static MMOQuestDialogPresenter ResolvePrefabPresenter()
        {
            MMOQuestDialogPresenter presenter = Instance != null ? Instance : FindExistingPresenter();
            if (presenter != null && !presenter.usesGeneratedFrame && MMOStandardWindow.HasAuthoredWindowLayout(presenter.gameObject))
            {
                return presenter;
            }

            Canvas canvas = presenter != null ? presenter.GetComponentInParent<Canvas>() : FindAnyObjectByType<Canvas>();
            if (presenter != null)
            {
                if (Instance == presenter)
                {
                    Instance = null;
                }

                Destroy(presenter.gameObject);
            }

            if (canvas == null)
            {
                return null;
            }

            GameObject dialogObject = MMOWindowPrefabResolver.Instantiate(MMOWindowPrefabId.Quest, canvas.transform, "Quest Dialog");
            presenter = dialogObject.GetComponent<MMOQuestDialogPresenter>();
            if (presenter == null)
            {
                presenter = dialogObject.AddComponent<MMOQuestDialogPresenter>();
            }

            return presenter;
        }

        private static MMOQuestDialogPresenter FindExistingPresenter()
        {
            MMOQuestDialogPresenter[] presenters = FindObjectsByType<MMOQuestDialogPresenter>(FindObjectsInactive.Include);
            return presenters.Length > 0 ? presenters[0] : null;
        }

        private void RefreshList()
        {
            MMOUiFactory.DestroyChildren(dynamicRoot);
            HideActionButtons();

            Text title = CreateText("Title", npc != null ? npc.DisplayName : "Quest Giver", 20, FontStyle.Bold, TextAnchor.MiddleLeft, 0f, 0f, 40f);
            title.color = MMONpcWindowFrame.TitleColor;

            float y = 52f;
            List<MMOQuestDefinition> turnIns = npc != null && questLog != null ? questLog.GetTurnInQuestsForNpc(npc.NpcId) : new List<MMOQuestDefinition>();
            foreach (MMOQuestDefinition quest in turnIns)
            {
                CreateQuestListButton(quest, "?", y, true);
                y += 42f;
            }

            if (npc != null && questLog != null)
            {
                foreach (MMOQuestDefinition quest in npc.OfferedQuests)
                {
                    if (quest != null && questLog.CanAccept(quest))
                    {
                        CreateQuestListButton(quest, "!", y, false);
                        y += 42f;
                    }
                }
            }

            if (y <= 52f)
            {
                Text empty = CreateText("Empty", "I have no work for you right now.", 14, FontStyle.Normal, TextAnchor.UpperLeft, 0f, y, 80f);
                empty.color = MMONpcWindowFrame.BodyColor;
            }

            ConfigureQuestActionButton(goodbyeButton, "Goodbye", true, Close);
        }

        private void CreateQuestListButton(MMOQuestDefinition quest, string marker, float y, bool turnIn)
        {
            Button button = MMOUiFactory.CreateTextButton($"Quest {quest.DisplayName}", dynamicRoot, string.Empty, new Vector2(0f, 36f), MMONpcWindowFrame.PanelColor);
            button.onClick.AddListener(() => OpenQuest(quest, turnIn));
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(0f, -y);
            rect.sizeDelta = new Vector2(0f, 36f);

            Text label = MMOUiFactory.CreateText("Label", rect, 14, FontStyle.Bold, TextAnchor.MiddleLeft);
            label.text = $"{marker}  {quest.DisplayName}";
            label.color = new Color(1f, 0.84f, 0.28f, 1f);
            MMOUiFactory.Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(12f, 0f);
            label.rectTransform.offsetMax = new Vector2(-12f, 0f);
        }

        private void OpenQuest(MMOQuestDefinition quest, bool turnIn)
        {
            bool changedQuest = selectedQuest != quest || selectedQuestTurnIn != turnIn;
            selectedQuest = quest;
            selectedQuestTurnIn = turnIn;
            if (changedQuest)
            {
                selectedReward = null;
            }

            MMOUiFactory.DestroyChildren(dynamicRoot);
            HideActionButtons();

            CreateText("Title", quest.DisplayName, 20, FontStyle.Bold, TextAnchor.MiddleLeft, 0f, 0f, 38f).color = MMONpcWindowFrame.TitleColor;

            string bodyText = turnIn
                ? (string.IsNullOrWhiteSpace(quest.CompletionText) ? quest.ObjectiveSummary : quest.CompletionText)
                : quest.OfferText;
            Text body = CreateText("Body", bodyText, 14, FontStyle.Normal, TextAnchor.UpperLeft, 0f, 48f, 210f);
            body.color = MMONpcWindowFrame.BodyColor;

            Text objective = CreateText("Objectives", quest.ObjectiveSummary, 13, FontStyle.Bold, TextAnchor.UpperLeft, 0f, 266f, 74f);
            objective.color = Color.white;
            MMOItemDefinition objectiveItem = GetFirstReferencedObjectiveItem(quest);
            if (objectiveItem != null)
            {
                MMOItemTooltipTrigger.Bind(objective.gameObject, objectiveItem);
                CreateObjectiveItemIcon(objectiveItem, 266f);
            }

            float rewardY = 350f;
            MMOQuestRewardDefinition rewards = quest.Rewards;
            if (rewards != null)
            {
                string rewardText = $"Rewards: {rewards.Experience} XP";
                if (rewards.MoneyCopper > 0)
                {
                    rewardText += $"  {MMOCurrencyWallet.FormatCopper(rewards.MoneyCopper)}";
                }

                CreateText("Reward Summary", rewardText, 13, FontStyle.Bold, TextAnchor.UpperLeft, 0f, rewardY, 24f).color = new Color(1f, 0.82f, 0.34f, 1f);
                rewardY += 30f;
                rewardY = CreateGuaranteedRewards(rewards, rewardY);
                CreateRewardChoices(rewards, rewardY, turnIn);
            }

            CreateActionButtons(turnIn);
        }

        private float CreateGuaranteedRewards(MMOQuestRewardDefinition rewards, float startY)
        {
            int index = 0;
            foreach (MMOItemStack stack in rewards.GuaranteedItems)
            {
                if (stack == null || stack.IsEmpty)
                {
                    continue;
                }

                Button itemButton = CreateItemIconButton($"Reward {stack.Item.DisplayName}", stack.Item, stack.Quantity, new Vector2(index * 52f, -startY), false);
                MMOItemTooltipTrigger.Bind(itemButton.gameObject, stack.Item);
                index++;
            }

            return index > 0 ? startY + 54f : startY;
        }

        private void CreateRewardChoices(MMOQuestRewardDefinition rewards, float startY, bool canChoose)
        {
            if (rewards.ChoiceItems.Count == 0)
            {
                return;
            }

            int index = 0;
            foreach (MMOItemDefinition item in rewards.ChoiceItems)
            {
                if (item == null)
                {
                    continue;
                }

                bool isSelected = canChoose && selectedReward == item;
                Button choice = CreateItemIconButton($"Reward {item.DisplayName}", item, 0, new Vector2(index * 52f, -startY), isSelected);
                MMOItemTooltipTrigger.Bind(choice.gameObject, item);
                if (canChoose)
                {
                    choice.onClick.AddListener(() =>
                    {
                        selectedReward = item;
                        OpenQuest(selectedQuest, selectedQuestTurnIn);
                    });
                }

                index++;
            }
        }

        private void CreateObjectiveItemIcon(MMOItemDefinition item, float y)
        {
            Image slot = MMOUiFactory.CreateImage($"Objective Item {item.DisplayName}", dynamicRoot, MMOItemIconView.GetSlotBackgroundColor(item));
            RectTransform rect = slot.rectTransform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(0f, -y);
            rect.sizeDelta = new Vector2(42f, 42f);
            MMOItemIconView.AddToSlot(rect, item, 0);
        }

        private Button CreateItemIconButton(string objectName, MMOItemDefinition item, int quantity, Vector2 anchoredPosition, bool selected)
        {
            Image slot = MMOUiFactory.CreateImage(objectName, dynamicRoot, MMOItemIconView.GetSlotBackgroundColor(item));
            RectTransform rect = slot.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(42f, 42f);

            Button button = slot.gameObject.AddComponent<Button>();
            MMOItemIconView.AddToSlot(rect, item, quantity, false, selected);
            return button;
        }

        private static MMOItemDefinition GetFirstReferencedObjectiveItem(MMOQuestDefinition quest)
        {
            if (quest == null)
            {
                return null;
            }

            foreach (MMOQuestObjectiveDefinition objective in quest.Objectives)
            {
                if (objective == null)
                {
                    continue;
                }

                if (objective.RequiredItem != null)
                {
                    return objective.RequiredItem;
                }

                if (objective.UsableItem != null)
                {
                    return objective.UsableItem;
                }
            }

            return null;
        }

        private void CreateActionButtons(bool turnIn)
        {
            if (turnIn)
            {
                ConfigureQuestActionButton(backButton, "Back", true, RefreshList);
                ConfigureQuestActionButton(completeButton, "Complete Quest", !HasChoiceRewards(selectedQuest?.Rewards) || selectedReward != null, CompleteOrAcceptSelectedQuest);
                return;
            }

            ConfigureQuestActionButton(declineButton, "Decline", true, RefreshList);
            ConfigureQuestActionButton(acceptButton, "Accept", true, CompleteOrAcceptSelectedQuest);
        }

        private void CompleteOrAcceptSelectedQuest()
        {
            if (selectedQuestTurnIn)
            {
                questLog?.TryComplete(selectedQuest, selectedReward);
            }
            else
            {
                questLog?.TryAccept(selectedQuest);
            }

            RefreshList();
        }

        private static bool HasChoiceRewards(MMOQuestRewardDefinition rewards)
        {
            if (rewards == null)
            {
                return false;
            }

            foreach (MMOItemDefinition item in rewards.ChoiceItems)
            {
                if (item != null)
                {
                    return true;
                }
            }

            return false;
        }

        private Text CreateText(string name, string value, int size, FontStyle style, TextAnchor anchor, float x, float y, float height)
        {
            Text text = MMOUiFactory.CreateText(name, dynamicRoot, size, style, anchor);
            text.text = value;
            text.rectTransform.anchorMin = new Vector2(0f, 1f);
            text.rectTransform.anchorMax = new Vector2(1f, 1f);
            text.rectTransform.pivot = new Vector2(0f, 1f);
            text.rectTransform.anchoredPosition = new Vector2(x, -y);
            text.rectTransform.sizeDelta = new Vector2(-x, height);
            return text;
        }

        private Button FindOrCreateQuestButton(string objectName, string label, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            Button button = FindAuthoredQuestButton(objectName);

            if (button == null)
            {
                button = MMOStandardWindow.CreateQuestActionButton(objectName, contentRoot, label);
                RectTransform rect = button.GetComponent<RectTransform>();
                rect.anchorMin = anchor;
                rect.anchorMax = anchor;
                rect.pivot = pivot;
                rect.anchoredPosition = anchoredPosition;
                rect.sizeDelta = size;
            }

            Text text = MMOUiFactory.FindButtonLabel(button);
            if (text != null)
            {
                text.text = label;
            }

            return button;
        }

        private Button FindAuthoredQuestButton(string objectName)
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            foreach (Button candidate in buttons)
            {
                if (candidate.name == objectName)
                {
                    return candidate;
                }
            }

            return null;
        }

        private void ConfigureQuestActionButton(Button button, string label, bool interactable, UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.gameObject.SetActive(true);
            button.interactable = interactable;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);

            Text text = MMOUiFactory.FindButtonLabel(button);
            if (text != null)
            {
                text.text = label;
            }
        }

        private void HideActionButtons()
        {
            SetButtonActive(goodbyeButton, false);
            SetButtonActive(acceptButton, false);
            SetButtonActive(declineButton, false);
            SetButtonActive(backButton, false);
            SetButtonActive(completeButton, false);
        }

        private static void SetButtonActive(Button button, bool active)
        {
            if (button != null)
            {
                button.gameObject.SetActive(active);
            }
        }
    }
}
