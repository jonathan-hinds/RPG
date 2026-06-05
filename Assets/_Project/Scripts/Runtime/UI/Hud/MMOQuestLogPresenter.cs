using System.Text;
using RPGClone.Quests;
using UnityEngine;
using UnityEngine.UI;

namespace RPGClone.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class MMOQuestLogPresenter : MonoBehaviour
    {
        [SerializeField] private MMOQuestLog questLog;

        private RectTransform rowsRoot;

        private void Awake()
        {
            ResolveReferences();
            BuildIfNeeded();
            Refresh();
        }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            if (questLog != null)
            {
                questLog.Changed -= OnQuestLogChanged;
            }
        }

        public void Configure(MMOQuestLog newQuestLog)
        {
            if (questLog != null)
            {
                questLog.Changed -= OnQuestLogChanged;
            }

            questLog = newQuestLog;
            BuildIfNeeded();
            Subscribe();
            Refresh();
        }

        public void Toggle()
        {
            gameObject.SetActive(!gameObject.activeSelf);
            if (gameObject.activeSelf)
            {
                Refresh();
            }
        }

        private void ResolveReferences()
        {
            if (questLog != null)
            {
                return;
            }

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            questLog = player != null ? player.GetComponent<MMOQuestLog>() : null;
        }

        private void Subscribe()
        {
            if (questLog != null)
            {
                questLog.Changed -= OnQuestLogChanged;
                questLog.Changed += OnQuestLogChanged;
            }
        }

        private void OnQuestLogChanged(MMOQuestLog changedQuestLog)
        {
            Refresh();
        }

        private void BuildIfNeeded()
        {
            if (rowsRoot != null)
            {
                return;
            }

            RectTransform root = (RectTransform)transform;
            root.sizeDelta = new Vector2(560f, 520f);
            Image background = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            background.color = new Color(0.034f, 0.029f, 0.023f, 0.98f);

            Text title = MMOUiFactory.CreateText("Title", transform, 18, FontStyle.Bold, TextAnchor.MiddleLeft);
            title.text = "Quest Log";
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(14f, -10f);
            title.rectTransform.sizeDelta = new Vector2(-28f, 28f);

            Button closeButton = MMOUiFactory.CreateTextButton("Close", transform, "X", new Vector2(26f, 24f), new Color(0.12f, 0.09f, 0.07f, 0.95f));
            closeButton.onClick.AddListener(() => gameObject.SetActive(false));
            RectTransform closeRect = closeButton.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-10f, -10f);

            rowsRoot = MMOUiFactory.CreateRect("Rows", transform);
            rowsRoot.anchorMin = new Vector2(0f, 0f);
            rowsRoot.anchorMax = new Vector2(1f, 1f);
            rowsRoot.offsetMin = new Vector2(18f, 18f);
            rowsRoot.offsetMax = new Vector2(-18f, -54f);
        }

        private void Refresh()
        {
            BuildIfNeeded();
            MMOUiFactory.DestroyChildren(rowsRoot);
            if (questLog == null || questLog.ActiveQuests.Count == 0)
            {
                Text empty = MMOUiFactory.CreateText("Empty", rowsRoot, 14, FontStyle.Normal, TextAnchor.UpperLeft);
                empty.text = "No active quests.";
                empty.color = new Color(0.82f, 0.76f, 0.66f, 1f);
                MMOUiFactory.Stretch(empty.rectTransform);
                return;
            }

            float y = 0f;
            foreach (MMOQuestRuntimeState state in questLog.ActiveQuests)
            {
                CreateQuestRow(state, y);
                y += 112f;
            }
        }

        private void CreateQuestRow(MMOQuestRuntimeState state, float y)
        {
            MMOQuestDefinition quest = state.Quest;
            if (quest == null)
            {
                return;
            }

            Image row = MMOUiFactory.CreateImage(quest.DisplayName, rowsRoot, new Color(0.052f, 0.044f, 0.034f, 0.96f));
            RectTransform rect = row.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(0f, -y);
            rect.sizeDelta = new Vector2(0f, 104f);

            Text name = MMOUiFactory.CreateText("Name", rect, 14, FontStyle.Bold, TextAnchor.UpperLeft);
            name.text = questLog.IsReadyToTurnIn(state) ? $"{quest.DisplayName} (Complete)" : quest.DisplayName;
            name.color = new Color(1f, 0.84f, 0.28f, 1f);
            name.rectTransform.anchorMin = new Vector2(0f, 1f);
            name.rectTransform.anchorMax = new Vector2(1f, 1f);
            name.rectTransform.pivot = new Vector2(0f, 1f);
            name.rectTransform.anchoredPosition = new Vector2(10f, -8f);
            name.rectTransform.sizeDelta = new Vector2(-128f, 22f);

            Text objectives = MMOUiFactory.CreateText("Objectives", rect, 12, FontStyle.Normal, TextAnchor.UpperLeft);
            objectives.text = FormatObjectives(state);
            objectives.color = Color.white;
            objectives.rectTransform.anchorMin = new Vector2(0f, 0f);
            objectives.rectTransform.anchorMax = new Vector2(1f, 1f);
            objectives.rectTransform.offsetMin = new Vector2(10f, 8f);
            objectives.rectTransform.offsetMax = new Vector2(-10f, -34f);

            Button track = MMOUiFactory.CreateTextButton("Track", rect, state.Tracked ? "Untrack" : "Track", new Vector2(88f, 28f), new Color(0.11f, 0.08f, 0.052f, 1f));
            track.onClick.AddListener(() => questLog.SetTracked(quest, !state.Tracked));
            RectTransform trackRect = track.GetComponent<RectTransform>();
            trackRect.anchorMin = new Vector2(1f, 1f);
            trackRect.anchorMax = new Vector2(1f, 1f);
            trackRect.pivot = new Vector2(1f, 1f);
            trackRect.anchoredPosition = new Vector2(-8f, -8f);
        }

        private static string FormatObjectives(MMOQuestRuntimeState state)
        {
            StringBuilder builder = new();
            for (int i = 0; i < state.Quest.Objectives.Count; i++)
            {
                MMOQuestObjectiveDefinition objective = state.Quest.Objectives[i];
                builder.Append(objective.Summary);
                builder.Append(" ");
                builder.Append(state.GetProgress(i));
                builder.Append("/");
                builder.Append(objective.RequiredCount);
                if (i < state.Quest.Objectives.Count - 1)
                {
                    builder.AppendLine();
                }
            }

            return builder.ToString();
        }
    }
}
