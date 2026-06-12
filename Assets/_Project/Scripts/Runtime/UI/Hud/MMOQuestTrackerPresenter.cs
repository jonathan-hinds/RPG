using System.Collections.Generic;
using System.Text;
using RPGClone.Quests;
using RPGClone.Services;
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
            EnsureProgressPopupPresenter();
            Refresh();
        }

        private void OnEnable()
        {
            ResolveReferences();
            EnsureProgressPopupPresenter();
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
            EnsureProgressPopupPresenter();
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
            root.anchorMin = new Vector2(1f, 1f);
            root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(1f, 1f);
            root.anchoredPosition = new Vector2(-24f, -292f);
            root.sizeDelta = new Vector2(300f, 360f);

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

                if (questLog.IsReadyToTurnIn(state))
                {
                    builder.AppendLine($"{state.Quest.DisplayName} (Completed)");
                    builder.AppendLine();
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

        private void EnsureProgressPopupPresenter()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                return;
            }

            Transform existing = canvas.transform.Find("Quest Progress Popups");
            GameObject popupObject = existing != null ? existing.gameObject : new GameObject("Quest Progress Popups", typeof(RectTransform));
            popupObject.transform.SetParent(canvas.transform, false);
            popupObject.SetActive(true);

            MMOQuestProgressPopupPresenter presenter = popupObject.GetComponent<MMOQuestProgressPopupPresenter>();
            if (presenter == null)
            {
                presenter = popupObject.AddComponent<MMOQuestProgressPopupPresenter>();
            }

            presenter.Configure(questLog);
        }
    }

    [RequireComponent(typeof(RectTransform))]
    public sealed class MMOQuestProgressPopupPresenter : MonoBehaviour
    {
        private const int MaxVisiblePopups = 4;

        [SerializeField] private MMOQuestLog questLog;
        [SerializeField, Min(0.25f)] private float visibleSeconds = 2.1f;
        [SerializeField, Min(0.05f)] private float fadeSeconds = 0.45f;
        [SerializeField, Min(18f)] private float rowSpacing = 28f;

        private readonly List<PopupEntry> activePopups = new();
        private readonly Stack<Text> textPool = new();
        private RectTransform root;

        private void Awake()
        {
            ResolveReferences();
            BuildIfNeeded();
        }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
        }

        private void OnDisable()
        {
            if (questLog != null)
            {
                questLog.ObjectiveProgressed -= OnObjectiveProgressed;
            }
        }

        private void Update()
        {
            if (activePopups.Count == 0)
            {
                return;
            }

            float now = Time.unscaledTime;
            for (int i = activePopups.Count - 1; i >= 0; i--)
            {
                PopupEntry entry = activePopups[i];
                float age = now - entry.StartTime;
                if (age >= visibleSeconds + fadeSeconds)
                {
                    Recycle(i);
                    continue;
                }

                float alpha = age <= visibleSeconds ? 1f : 1f - ((age - visibleSeconds) / fadeSeconds);
                entry.Text.color = WithAlpha(entry.BaseColor, alpha);
            }

            LayoutActivePopups();
        }

        public void Configure(MMOQuestLog newQuestLog)
        {
            if (questLog != null)
            {
                questLog.ObjectiveProgressed -= OnObjectiveProgressed;
            }

            questLog = newQuestLog;
            BuildIfNeeded();
            Subscribe();
        }

        private void ResolveReferences()
        {
            if (questLog != null)
            {
                return;
            }

            if (MMORuntimeSceneReferences.TryGetPlayerComponent(out MMOQuestLog playerQuestLog))
            {
                questLog = playerQuestLog;
                return;
            }

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            questLog = player != null ? player.GetComponent<MMOQuestLog>() : null;
        }

        private void Subscribe()
        {
            if (questLog == null)
            {
                return;
            }

            questLog.ObjectiveProgressed -= OnObjectiveProgressed;
            questLog.ObjectiveProgressed += OnObjectiveProgressed;
        }

        private void BuildIfNeeded()
        {
            if (root != null)
            {
                return;
            }

            root = (RectTransform)transform;
            root.anchorMin = new Vector2(0.5f, 0.66f);
            root.anchorMax = new Vector2(0.5f, 0.66f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = new Vector2(560f, 150f);
        }

        private void OnObjectiveProgressed(MMOQuestObjectiveProgressEvent progressEvent)
        {
            if (progressEvent.Quest == null || progressEvent.Objective == null)
            {
                return;
            }

            BuildIfNeeded();
            Text text = GetOrCreateText();
            text.text = FormatProgress(progressEvent);
            text.color = progressEvent.CompletedThisUpdate
                ? new Color(1f, 0.86f, 0.28f, 1f)
                : new Color(1f, 0.96f, 0.78f, 1f);
            text.gameObject.SetActive(true);

            activePopups.Insert(0, new PopupEntry(text, Time.unscaledTime, text.color));

            while (activePopups.Count > MaxVisiblePopups)
            {
                Recycle(activePopups.Count - 1);
            }

            LayoutActivePopups();
        }

        private Text GetOrCreateText()
        {
            if (textPool.Count > 0)
            {
                return textPool.Pop();
            }

            Text text = MMOUiFactory.CreateText("Quest Progress Popup", transform, 18, FontStyle.Bold, TextAnchor.MiddleCenter);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            text.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            text.rectTransform.sizeDelta = new Vector2(560f, 30f);

            Outline outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.82f);
            outline.effectDistance = new Vector2(1.2f, -1.2f);

            Shadow shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.72f);
            shadow.effectDistance = new Vector2(2f, -2f);
            return text;
        }

        private void LayoutActivePopups()
        {
            for (int i = 0; i < activePopups.Count; i++)
            {
                RectTransform rectTransform = activePopups[i].Text.rectTransform;
                rectTransform.anchoredPosition = new Vector2(0f, -i * rowSpacing);
                rectTransform.SetAsLastSibling();
            }
        }

        private void Recycle(int index)
        {
            Text text = activePopups[index].Text;
            activePopups.RemoveAt(index);
            if (text == null)
            {
                return;
            }

            text.gameObject.SetActive(false);
            textPool.Push(text);
        }

        private static string FormatProgress(MMOQuestObjectiveProgressEvent progressEvent)
        {
            MMOQuestObjectiveDefinition objective = progressEvent.Objective;
            string summary = string.IsNullOrWhiteSpace(objective.Summary) ? "Objective" : objective.Summary;
            string completedSuffix = progressEvent.CompletedThisUpdate ? " (Completed)" : string.Empty;
            int current = Mathf.Min(progressEvent.CurrentProgress, progressEvent.RequiredCount);

            return objective.ObjectiveType switch
            {
                MMOQuestObjectiveType.SpeakToNpc => $"{FormatSpeakObjective(summary, objective.RequiredNpcId)}{completedSuffix}",
                MMOQuestObjectiveType.CollectItem => $"{current}/{progressEvent.RequiredCount} {FormatCollectionObjective(summary)}{completedSuffix}",
                MMOQuestObjectiveType.CollectQuestItem => $"{current}/{progressEvent.RequiredCount} {FormatCollectionObjective(summary)}{completedSuffix}",
                MMOQuestObjectiveType.KillCreature => $"{current}/{progressEvent.RequiredCount} {summary}{completedSuffix}",
                MMOQuestObjectiveType.UseItemOnWorldObject => $"{current}/{progressEvent.RequiredCount} {summary}{completedSuffix}",
                _ => $"{current}/{progressEvent.RequiredCount} {summary}{completedSuffix}"
            };
        }

        private static string FormatSpeakObjective(string summary, string npcId)
        {
            if (summary.StartsWith("Speak ", System.StringComparison.OrdinalIgnoreCase)
                || summary.StartsWith("Talk ", System.StringComparison.OrdinalIgnoreCase))
            {
                return summary;
            }

            return $"Speak with {(string.IsNullOrWhiteSpace(npcId) ? summary : npcId)}";
        }

        private static string FormatCollectionObjective(string summary)
        {
            return summary.IndexOf("picked", System.StringComparison.OrdinalIgnoreCase) >= 0
                || summary.IndexOf("collected", System.StringComparison.OrdinalIgnoreCase) >= 0
                || summary.IndexOf("looted", System.StringComparison.OrdinalIgnoreCase) >= 0
                    ? summary
                    : $"{summary} picked up";
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }

        private readonly struct PopupEntry
        {
            public PopupEntry(Text text, float startTime, Color baseColor)
            {
                Text = text;
                StartTime = startTime;
                BaseColor = baseColor;
            }

            public Text Text { get; }
            public float StartTime { get; }
            public Color BaseColor { get; }
        }
    }
}
