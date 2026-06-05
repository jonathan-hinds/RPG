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
            RefreshList();
        }

        public void Close()
        {
            selectedQuest = null;
            selectedReward = null;
            gameObject.SetActive(false);
        }

        private void BuildFrame()
        {
            RectTransform root = (RectTransform)transform;
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = new Vector2(0f, 20f);
            root.sizeDelta = new Vector2(520f, 580f);

            Image background = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            background.color = new Color(0.036f, 0.028f, 0.019f, 0.98f);
            Outline outline = gameObject.GetComponent<Outline>() ?? gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.76f, 0.61f, 0.33f, 1f);
            outline.effectDistance = new Vector2(1f, -1f);

            contentRoot = MMOUiFactory.CreateRect("Content", transform);
            MMOUiFactory.Stretch(contentRoot);
            contentRoot.offsetMin = new Vector2(22f, 18f);
            contentRoot.offsetMax = new Vector2(-22f, -18f);
        }

        private void RefreshList()
        {
            MMOUiFactory.DestroyChildren(contentRoot);

            Text title = CreateText("Title", npc != null ? npc.DisplayName : "Quest Giver", 20, FontStyle.Bold, TextAnchor.MiddleLeft, 0f, 0f, 40f);
            title.color = new Color(1f, 0.82f, 0.34f, 1f);

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
                empty.color = new Color(0.86f, 0.79f, 0.66f, 1f);
            }

            Button close = MMOUiFactory.CreateTextButton("Goodbye", contentRoot, "Goodbye", new Vector2(116f, 34f), new Color(0.12f, 0.085f, 0.05f, 1f));
            close.onClick.AddListener(Close);
            RectTransform closeRect = close.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 0f);
            closeRect.anchorMax = new Vector2(1f, 0f);
            closeRect.pivot = new Vector2(1f, 0f);
            closeRect.anchoredPosition = Vector2.zero;
        }

        private void CreateQuestListButton(MMOQuestDefinition quest, string marker, float y, bool turnIn)
        {
            Button button = MMOUiFactory.CreateTextButton($"Quest {quest.DisplayName}", contentRoot, string.Empty, new Vector2(476f, 36f), new Color(0.07f, 0.052f, 0.034f, 0.96f));
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

            CreateText("Title", quest.DisplayName, 20, FontStyle.Bold, TextAnchor.MiddleLeft, 0f, 0f, 38f).color = new Color(1f, 0.82f, 0.34f, 1f);

            string bodyText = turnIn
                ? (string.IsNullOrWhiteSpace(quest.CompletionText) ? quest.ObjectiveSummary : quest.CompletionText)
                : quest.OfferText;
            Text body = CreateText("Body", bodyText, 14, FontStyle.Normal, TextAnchor.UpperLeft, 0f, 48f, 210f);
            body.color = new Color(0.91f, 0.84f, 0.69f, 1f);

            Text objective = CreateText("Objectives", quest.ObjectiveSummary, 13, FontStyle.Bold, TextAnchor.UpperLeft, 0f, 266f, 74f);
            objective.color = Color.white;

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
                CreateRewardChoices(rewards, rewardY);
            }

            CreateActionButtons(turnIn);
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

                Button choice = MMOUiFactory.CreateTextButton($"Reward {item.DisplayName}", contentRoot, item.DisplayName, new Vector2(150f, 30f), new Color(0.065f, 0.05f, 0.036f, 1f));
                choice.onClick.AddListener(() =>
                {
                    selectedReward = item;
                    OpenQuest(selectedQuest, true);
                });
                RectTransform rect = choice.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(0f, -y);

                if (selectedReward == null)
                {
                    selectedReward = item;
                }

                y += 34f;
            }
        }

        private void CreateActionButtons(bool turnIn)
        {
            Button back = MMOUiFactory.CreateTextButton("Back", contentRoot, "Back", new Vector2(96f, 34f), new Color(0.11f, 0.08f, 0.052f, 1f));
            back.onClick.AddListener(RefreshList);
            RectTransform backRect = back.GetComponent<RectTransform>();
            backRect.anchorMin = new Vector2(0f, 0f);
            backRect.anchorMax = new Vector2(0f, 0f);
            backRect.pivot = new Vector2(0f, 0f);

            Button action = MMOUiFactory.CreateTextButton(turnIn ? "Complete" : "Accept", contentRoot, turnIn ? "Complete Quest" : "Accept", new Vector2(132f, 34f), new Color(0.16f, 0.105f, 0.045f, 1f));
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
