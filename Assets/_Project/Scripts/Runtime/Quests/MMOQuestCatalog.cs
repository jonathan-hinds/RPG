using System.Collections.Generic;
using UnityEngine;

namespace RPGClone.Quests
{
    [CreateAssetMenu(menuName = "RPG Clone/Quests/Quest Catalog", fileName = "QuestCatalog")]
    public sealed class MMOQuestCatalog : ScriptableObject
    {
        [SerializeField] private List<MMOQuestDefinition> quests = new();

        public IReadOnlyList<MMOQuestDefinition> Quests => quests;

        public void Configure(IEnumerable<MMOQuestDefinition> newQuests)
        {
            quests = newQuests != null ? new List<MMOQuestDefinition>(newQuests) : new List<MMOQuestDefinition>();
        }

        public MMOQuestDefinition FindById(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId))
            {
                return null;
            }

            foreach (MMOQuestDefinition quest in quests)
            {
                if (quest != null && quest.QuestId == questId)
                {
                    return quest;
                }
            }

            return null;
        }
    }
}
