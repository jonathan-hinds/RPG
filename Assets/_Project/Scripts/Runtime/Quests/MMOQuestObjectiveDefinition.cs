using System;
using RPGClone.Enemies;
using RPGClone.Inventory;
using UnityEngine;

namespace RPGClone.Quests
{
    [Serializable]
    public sealed class MMOQuestObjectiveDefinition
    {
        [SerializeField] private string objectiveId = "objective";
        [SerializeField] private MMOQuestObjectiveType objectiveType;
        [SerializeField] private string summary = "Complete the objective.";
        [SerializeField, Min(1)] private int requiredCount = 1;
        [SerializeField] private MMOItemDefinition requiredItem;
        [SerializeField] private MMOItemDefinition usableItem;
        [SerializeField] private MMOEnemyDefinition requiredCreature;
        [SerializeField] private string requiredCreatureId;
        [SerializeField] private string requiredWorldObjectId;
        [SerializeField] private string requiredNpcId;
        [SerializeField] private bool consumeRequiredItemsOnTurnIn = true;

        public string ObjectiveId => string.IsNullOrWhiteSpace(objectiveId) ? summary : objectiveId;
        public MMOQuestObjectiveType ObjectiveType => objectiveType;
        public string Summary => string.IsNullOrWhiteSpace(summary) ? ObjectiveId : summary;
        public int RequiredCount => Mathf.Max(1, requiredCount);
        public MMOItemDefinition RequiredItem => requiredItem;
        public MMOItemDefinition UsableItem => usableItem;
        public MMOEnemyDefinition RequiredCreature => requiredCreature;
        public string RequiredCreatureId => requiredCreatureId;
        public string RequiredWorldObjectId => requiredWorldObjectId;
        public string RequiredNpcId => requiredNpcId;
        public bool ConsumeRequiredItemsOnTurnIn => consumeRequiredItemsOnTurnIn;

        public void Configure(
            string newObjectiveId,
            MMOQuestObjectiveType newObjectiveType,
            string newSummary,
            int newRequiredCount,
            MMOItemDefinition newRequiredItem = null,
            MMOItemDefinition newUsableItem = null,
            MMOEnemyDefinition newRequiredCreature = null,
            string newRequiredCreatureId = "",
            string newRequiredWorldObjectId = "",
            string newRequiredNpcId = "",
            bool newConsumeRequiredItemsOnTurnIn = true)
        {
            objectiveId = string.IsNullOrWhiteSpace(newObjectiveId) ? newObjectiveId : newObjectiveId.Trim();
            objectiveType = newObjectiveType;
            summary = newSummary;
            requiredCount = Mathf.Max(1, newRequiredCount);
            requiredItem = newRequiredItem;
            usableItem = newUsableItem;
            requiredCreature = newRequiredCreature;
            requiredCreatureId = newRequiredCreatureId;
            requiredWorldObjectId = newRequiredWorldObjectId;
            requiredNpcId = newRequiredNpcId;
            consumeRequiredItemsOnTurnIn = newConsumeRequiredItemsOnTurnIn;
        }
    }
}
