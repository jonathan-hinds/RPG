using System.Collections.Generic;
using RPGClone.Inventory;
using RPGClone.Quests;
using UnityEngine;
using UnityEngine.UI;

namespace RPGClone.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class MMOQuestDialogPresenter : MonoBehaviour
    {
        private RectTransform contentRoot;
        private MMOQuestNpc npc;
        private MMOQuestLog questLog;
        private MMOQuestDefinition selectedQuest;
        private MMOItemDefinition selectedReward;

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
            MMOQuestDialogPresenter presenter = Instance != null ? Instance : FindAnyObjectByType<MMOQuestDialogPresenter>();
            presenter?.OpenNpc(npc, questLog);
        }

        public void OpenNpc(MMOQuestNpc newNpc, MMOQuestLog newQuestLog)
        {
            npc = newNpc;
            questLog = newQuestLog;
            selectedQuest = null;
            selectedReward = null;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            TrackNpcDistance();
            RefreshList();
        }

        public void Close()
        {
            selectedQuest = null;
            selectedReward = null;
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
            RectTransform root = (RectTransform)transform;
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = new Vector2(0f, 20f);
            root.sizeDelta = new Vector2(520f, 580f);

            MMONpcWindowFrame.Apply(gameObject);

            contentRoot = MMOUiFactory.CreateRect("Content", transform);
            MMOUiFactory.Stretch(contentRoot);
            contentRoot.offsetMin = new Vector2(22f, 18f);
            contentRoot.offsetMax = new Vector2(-22f, -18f);
        }

        private void RefreshList()
        {
            MMOUiFactory.DestroyChildren(contentRoot);

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

            Button close = MMOUiFactory.CreateTextButton("Goodbye", contentRoot, "Goodbye", new Vector2(116f, 34f), MMONpcWindowFrame.ButtonColor);
            close.onClick.AddListener(Close);
            RectTransform closeRect = close.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 0f);
            closeRect.anchorMax = new Vector2(1f, 0f);
            closeRect.pivot = new Vector2(1f, 0f);
            closeRect.anchoredPosition = Vector2.zero;
        }

        private void CreateQuestListButton(MMOQuestDefinition quest, string marker, float y, bool turnIn)
        {
            Button button = MMOUiFactory.CreateTextButton($"Quest {quest.DisplayName}", contentRoot, string.Empty, new Vector2(476f, 36f), MMONpcWindowFrame.PanelColor);
            button.onClick.AddListener(() => OpenQuest(quest, turnIn));
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(0f, -y);

            Text label = MMOUiFactory.CreateText("Label", rect, 14, FontStyle.Bold, TextAnchor.MiddleLeft);
            label.text = $"{marker}  {quest.DisplayName}";
            label.color = new Color(1f, 0.84f, 0.28f, 1f);
            MMOUiFactory.Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(12f, 0f);
            label.rectTransform.offsetMax = new Vector2(-12f, 0f);
        }

        private void OpenQuest(MMOQuestDefinition quest, bool turnIn)
        {
            selectedQuest = quest;
            MMOUiFactory.DestroyChildren(contentRoot);

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
                CreateRewardChoices(rewards, rewardY);
            }

            CreateActionButtons(turnIn);
        }

        private float CreateGuaranteedRewards(MMOQuestRewardDefinition rewards, float startY)
        {
            float y = startY;
            foreach (MMOItemStack stack in rewards.GuaranteedItems)
            {
                if (stack == null || stack.IsEmpty)
                {
                    continue;
                }

                Button itemButton = CreateItemIconButton($"Reward {stack.Item.DisplayName}", stack.Item, stack.Quantity, new Vector2(0f, -y), false);
                MMOItemTooltipTrigger.Bind(itemButton.gameObject, stack.Item);
                y += 48f;
            }

            return y;
        }

        private void CreateRewardChoices(MMOQuestRewardDefinition rewards, float startY)
        {
            if (rewards.ChoiceItems.Count == 0)
            {
                return;
            }

            MMOCharacterEquipment equipment = questLog != null ? questLog.GetComponent<MMOCharacterEquipment>() : null;
            float y = startY;
            foreach (MMOItemDefinition item in rewards.ChoiceItems)
            {
                if (item == null || (equipment != null && !equipment.CanEquip(item)))
                {
                    continue;
                }

                if (selectedReward == null)
                {
                    selectedReward = item;
                }

                bool isSelected = selectedReward == item;
                Button choice = CreateItemIconButton($"Reward {item.DisplayName}", item, 0, new Vector2(0f, -y), isSelected);
                MMOItemTooltipTrigger.Bind(choice.gameObject, item);
                choice.onClick.AddListener(() =>
                {
                    selectedReward = item;
                    OpenQuest(selectedQuest, true);
                });
                y += 48f;
            }
        }

        private void CreateObjectiveItemIcon(MMOItemDefinition item, float y)
        {
            Image slot = MMOUiFactory.CreateImage($"Objective Item {item.DisplayName}", contentRoot, MMOItemIconView.GetSlotBackgroundColor(item));
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
            Image slot = MMOUiFactory.CreateImage(objectName, contentRoot, MMOItemIconView.GetSlotBackgroundColor(item));
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
            Button back = MMOUiFactory.CreateTextButton("Back", contentRoot, "Back", new Vector2(96f, 34f), MMONpcWindowFrame.ButtonColor);
            back.onClick.AddListener(RefreshList);
            RectTransform backRect = back.GetComponent<RectTransform>();
            backRect.anchorMin = new Vector2(0f, 0f);
            backRect.anchorMax = new Vector2(0f, 0f);
            backRect.pivot = new Vector2(0f, 0f);

            Button action = MMOUiFactory.CreateTextButton(turnIn ? "Complete" : "Accept", contentRoot, turnIn ? "Complete Quest" : "Accept", new Vector2(132f, 34f), MMONpcWindowFrame.AccentButtonColor);
            action.onClick.AddListener(() =>
            {
                if (turnIn)
                {
                    questLog?.TryComplete(selectedQuest, selectedReward);
                }
                else
                {
                    questLog?.TryAccept(selectedQuest);
                }

                RefreshList();
            });
            RectTransform actionRect = action.GetComponent<RectTransform>();
            actionRect.anchorMin = new Vector2(1f, 0f);
            actionRect.anchorMax = new Vector2(1f, 0f);
            actionRect.pivot = new Vector2(1f, 0f);
            actionRect.anchoredPosition = Vector2.zero;
        }

        private Text CreateText(string name, string value, int size, FontStyle style, TextAnchor anchor, float x, float y, float height)
        {
            Text text = MMOUiFactory.CreateText(name, contentRoot, size, style, anchor);
            text.text = value;
            text.rectTransform.anchorMin = new Vector2(0f, 1f);
            text.rectTransform.anchorMax = new Vector2(1f, 1f);
            text.rectTransform.pivot = new Vector2(0f, 1f);
            text.rectTransform.anchoredPosition = new Vector2(x, -y);
            text.rectTransform.sizeDelta = new Vector2(-x, height);
            return text;
        }
    }
}
