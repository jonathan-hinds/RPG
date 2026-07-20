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

            MMOGameplaySessionService.LocalPlayer.TryGetComponent(out questLog);
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
            trackerText.supportRichText = true;
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
                    builder.AppendLine($"{MMOExperienceScaling.FormatRichQuestTitle(state.Quest, questLog.PlayerLevel)} (Completed)");
                    builder.AppendLine();
                    continue;
                }

                builder.AppendLine(MMOExperienceScaling.FormatRichQuestTitle(state.Quest, questLog.PlayerLevel));
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

}
