using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPGClone.Quests
{
    [Serializable]
    public sealed class MMOQuestRuntimeState
    {
        [SerializeField] private MMOQuestDefinition quest;
        [SerializeField] private bool completed;
        [SerializeField] private bool tracked = true;
        [SerializeField] private List<int> objectiveProgress = new();

        public MMOQuestDefinition Quest => quest;
        public bool Completed => completed;
        public bool Tracked => tracked;
        public IReadOnlyList<int> ObjectiveProgress => objectiveProgress;

        public MMOQuestRuntimeState(MMOQuestDefinition quest)
        {
            this.quest = quest;
            EnsureProgressSlots();
        }

        public void SetTracked(bool value)
        {
            tracked = value;
        }

        public int GetProgress(int objectiveIndex)
        {
            EnsureProgressSlots();
            return objectiveIndex >= 0 && objectiveIndex < objectiveProgress.Count ? objectiveProgress[objectiveIndex] : 0;
        }

        public void SetProgress(int objectiveIndex, int value)
        {
            EnsureProgressSlots();
            if (objectiveIndex < 0 || objectiveIndex >= objectiveProgress.Count)
            {
                return;
            }

            int requiredCount = quest != null && objectiveIndex < quest.Objectives.Count
                ? quest.Objectives[objectiveIndex].RequiredCount
                : int.MaxValue;
            objectiveProgress[objectiveIndex] = Mathf.Clamp(value, 0, requiredCount);
        }

        public void AddProgress(int objectiveIndex, int value)
        {
            SetProgress(objectiveIndex, GetProgress(objectiveIndex) + Mathf.Max(0, value));
        }

        public void MarkCompleted()
        {
            completed = true;
            tracked = false;
        }

        public void EnsureProgressSlots()
        {
            objectiveProgress ??= new List<int>();
            int requiredSlots = quest != null ? quest.Objectives.Count : 0;
            while (objectiveProgress.Count < requiredSlots)
            {
                objectiveProgress.Add(0);
            }

            if (objectiveProgress.Count > requiredSlots)
            {
                objectiveProgress.RemoveRange(requiredSlots, objectiveProgress.Count - requiredSlots);
            }
        }
    }

    public readonly struct MMOQuestObjectiveProgressEvent
    {
        public MMOQuestObjectiveProgressEvent(
            MMOQuestDefinition quest,
            MMOQuestObjectiveDefinition objective,
            int objectiveIndex,
            int previousProgress,
            int currentProgress,
            int requiredCount)
        {
            Quest = quest;
            Objective = objective;
            ObjectiveIndex = objectiveIndex;
            PreviousProgress = previousProgress;
            CurrentProgress = currentProgress;
            RequiredCount = requiredCount;
        }

        public MMOQuestDefinition Quest { get; }
        public MMOQuestObjectiveDefinition Objective { get; }
        public int ObjectiveIndex { get; }
        public int PreviousProgress { get; }
        public int CurrentProgress { get; }
        public int RequiredCount { get; }
        public bool CompletedThisUpdate => PreviousProgress < RequiredCount && CurrentProgress >= RequiredCount;
    }
}
