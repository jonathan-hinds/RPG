using System.Text;
using RPGClone.Quests;
using UnityEngine;
using UnityEngine.UI;

namespace RPGClone.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class MMOQuestTrackerPresenter : MonoBehaviour
    {
        [SerializeField] private MMOQuestLog questLog;
        private Text trackerText;

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
            if (trackerText != null)
            {
                return;
            }

            RectTransform root = (RectTransform)transform;
            root.anchorMin = new Vector2(1f, 0.5f);
            root.anchorMax = new Vector2(1f, 0.5f);
            root.pivot = new Vector2(1f, 0.5f);
            root.anchoredPosition = new Vector2(-32f, 110f);
            root.sizeDelta = new Vector2(310f, 420f);

            trackerText = MMOUiFactory.CreateText("Tracker Text", transform, 13, FontStyle.Bold, TextAnchor.UpperLeft);
            trackerText.color = Color.white;
            MMOUiFactory.Stretch(trackerText.rectTransform);
        }

        private void Refresh()
        {
            BuildIfNeeded();
            if (questLog == null)
            {
                trackerText.text = string.Empty;
                return;
            }

            StringBuilder builder = new();
            if (questLog.PendingUsableItem != null)
            {
                builder.AppendLine($"Using: {questLog.PendingUsableItem.DisplayName}");
                builder.AppendLine();
            }

            foreach (MMOQuestRuntimeState state in questLog.ActiveQuests)
            {
                if (state == null || state.Quest == null || !state.Tracked)
                {
                    continue;
                }

                builder.AppendLine(state.Quest.DisplayName);
                for (int i = 0; i < state.Quest.Objectives.Count; i++)
                {
                    MMOQuestObjectiveDefinition objective = state.Quest.Objectives[i];
                    builder.Append("  ");
                    builder.Append(objective.Summary);
                    builder.Append(" ");
                    builder.Append(state.GetProgress(i));
                    builder.Append("/");
                    builder.AppendLine(objective.RequiredCount.ToString());
                }

                builder.AppendLine();
            }

            trackerText.text = builder.ToString();
        }
    }
}
