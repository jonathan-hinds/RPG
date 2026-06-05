using System.Collections.Generic;
using RPGClone.Inventory;
using UnityEngine;

namespace RPGClone.Quests
{
    [CreateAssetMenu(menuName = "RPG Clone/Quests/Quest", fileName = "Quest")]
    public sealed class MMOQuestDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string questId = "quest";
        [SerializeField] private string displayName = "Quest";
        [SerializeField, Min(1)] private int minimumLevel = 1;

        [Header("NPC Routing")]
        [SerializeField] private string offeredByNpcId;
        [SerializeField] private string turnedInToNpcId;

        [Header("Chain")]
        [SerializeField] private List<MMOQuestDefinition> prerequisiteQuests = new();

        [Header("Text")]
        [SerializeField, TextArea(4, 10)] private string offerText;
        [SerializeField, TextArea(2, 6)] private string progressText;
        [SerializeField, TextArea(2, 6)] private string completionText;
        [SerializeField, TextArea(2, 5)] private string objectiveSummary;

        [Header("Objectives And Rewards")]
        [SerializeField] private List<MMOQuestObjectiveDefinition> objectives = new();
        [SerializeField] private List<MMOItemStack> startItems = new();
        [SerializeField] private MMOQuestRewardDefinition rewards = new();

        public string QuestId => string.IsNullOrWhiteSpace(questId) ? name : questId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public int MinimumLevel => Mathf.Max(1, minimumLevel);
        public string OfferedByNpcId => offeredByNpcId;
        public string TurnedInToNpcId => string.IsNullOrWhiteSpace(turnedInToNpcId) ? offeredByNpcId : turnedInToNpcId;
        public IReadOnlyList<MMOQuestDefinition> PrerequisiteQuests => prerequisiteQuests;
        public string OfferText => offerText;
        public string ProgressText => progressText;
        public string CompletionText => completionText;
        public string ObjectiveSummary => objectiveSummary;
        public IReadOnlyList<MMOQuestObjectiveDefinition> Objectives => objectives;
        public IReadOnlyList<MMOItemStack> StartItems => startItems;
        public MMOQuestRewardDefinition Rewards => rewards;

        public void Configure(
            string newQuestId,
            string newDisplayName,
            int newMinimumLevel,
            string newOfferedByNpcId,
            string newTurnedInToNpcId,
            string newOfferText,
            string newProgressText,
            string newCompletionText,
            string newObjectiveSummary,
            IEnumerable<MMOQuestObjectiveDefinition> newObjectives,
            MMOQuestRewardDefinition newRewards,
            IEnumerable<MMOQuestDefinition> newPrerequisiteQuests = null,
            IEnumerable<MMOItemStack> newStartItems = null)
        {
            questId = string.IsNullOrWhiteSpace(newQuestId) ? name : newQuestId.Trim();
            displayName = string.IsNullOrWhiteSpace(newDisplayName) ? questId : newDisplayName;
            minimumLevel = Mathf.Max(1, newMinimumLevel);
            offeredByNpcId = newOfferedByNpcId;
            turnedInToNpcId = string.IsNullOrWhiteSpace(newTurnedInToNpcId) ? newOfferedByNpcId : newTurnedInToNpcId;
            offerText = newOfferText;
            progressText = newProgressText;
            completionText = newCompletionText;
            objectiveSummary = newObjectiveSummary;
            objectives = newObjectives != null ? new List<MMOQuestObjectiveDefinition>(newObjectives) : new List<MMOQuestObjectiveDefinition>();
            rewards = newRewards ?? new MMOQuestRewardDefinition();
            prerequisiteQuests = newPrerequisiteQuests != null ? new List<MMOQuestDefinition>(newPrerequisiteQuests) : new List<MMOQuestDefinition>();
            startItems = newStartItems != null ? new List<MMOItemStack>(newStartItems) : new List<MMOItemStack>();
        }
    }
}
