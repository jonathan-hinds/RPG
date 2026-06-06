using System;
using System.Collections.Generic;
using System.Linq;
using RPGClone.Characters;
using RPGClone.Enemies;
using RPGClone.Inventory;
using UnityEngine;

namespace RPGClone.Quests
{
    [RequireComponent(typeof(MMOInventoryContainer))]
    public sealed class MMOQuestLog : MonoBehaviour
    {
        [SerializeField] private MMOQuestCatalog questCatalog;
        [SerializeField] private List<MMOQuestRuntimeState> activeQuests = new();
        [SerializeField] private List<MMOQuestDefinition> completedQuests = new();
        [SerializeField] private MMOItemDefinition pendingUsableItem;

        private MMOInventoryContainer inventory;
        private MMOCharacterIdentity identity;
        private MMOCurrencyWallet wallet;
        private MMOExperienceComponent experience;

        public event Action<MMOQuestLog> Changed;
        public MMOQuestCatalog QuestCatalog => questCatalog;
        public IReadOnlyList<MMOQuestRuntimeState> ActiveQuests => activeQuests;
        public IReadOnlyList<MMOQuestDefinition> CompletedQuests => completedQuests;
        public MMOItemDefinition PendingUsableItem => pendingUsableItem;

        private void Awake()
        {
            ResolveReferences();
            RefreshCollectionObjectives();
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (inventory != null)
            {
                inventory.Changed -= OnInventoryChanged;
                inventory.Changed += OnInventoryChanged;
            }
        }

        private void OnDisable()
        {
            if (inventory != null)
            {
                inventory.Changed -= OnInventoryChanged;
            }
        }

        public void Configure(MMOQuestCatalog newQuestCatalog)
        {
            questCatalog = newQuestCatalog;
            RefreshCollectionObjectives();
            Changed?.Invoke(this);
        }

        public void RestoreState(IEnumerable<MMOQuestRuntimeState> restoredActiveQuests, IEnumerable<MMOQuestDefinition> restoredCompletedQuests, MMOItemDefinition restoredPendingUsableItem)
        {
            activeQuests = restoredActiveQuests != null ? new List<MMOQuestRuntimeState>(restoredActiveQuests) : new List<MMOQuestRuntimeState>();
            completedQuests = restoredCompletedQuests != null ? new List<MMOQuestDefinition>(restoredCompletedQuests) : new List<MMOQuestDefinition>();
            pendingUsableItem = restoredPendingUsableItem;
            RefreshCollectionObjectives();
            Changed?.Invoke(this);
        }

        public MMOQuestState GetQuestState(MMOQuestDefinition quest)
        {
            if (quest == null)
            {
                return MMOQuestState.Unavailable;
            }

            MMOQuestRuntimeState activeState = FindActiveState(quest);
            if (activeState != null)
            {
                return IsReadyToTurnIn(activeState) ? MMOQuestState.ReadyToTurnIn : MMOQuestState.Accepted;
            }

            if (IsCompleted(quest))
            {
                return MMOQuestState.Completed;
            }

            return CanAccept(quest) ? MMOQuestState.Available : MMOQuestState.Unavailable;
        }

        public bool CanAccept(MMOQuestDefinition quest)
        {
            if (quest == null || FindActiveState(quest) != null || IsCompleted(quest))
            {
                return false;
            }

            ResolveReferences();
            int level = identity != null ? identity.Level : 1;
            if (level < quest.MinimumLevel)
            {
                return false;
            }

            foreach (MMOQuestDefinition prerequisite in quest.PrerequisiteQuests)
            {
                if (prerequisite != null && !IsCompleted(prerequisite))
                {
                    return false;
                }
            }

            return true;
        }

        public bool TryAccept(MMOQuestDefinition quest)
        {
            if (!CanAccept(quest))
            {
                return false;
            }

            ResolveReferences();
            MMOQuestRuntimeState state = new(quest);
            activeQuests.Add(state);
            GrantStartItems(quest);
            RefreshCollectionObjectives();
            Changed?.Invoke(this);
            return true;
        }

        public bool TryComplete(MMOQuestDefinition quest, MMOItemDefinition chosenReward = null)
        {
            MMOQuestRuntimeState state = FindActiveState(quest);
            if (state == null || !IsReadyToTurnIn(state))
            {
                return false;
            }

            ResolveReferences();
            if (!ConsumeTurnInItems(quest))
            {
                return false;
            }

            GrantRewards(quest, chosenReward);
            state.MarkCompleted();
            activeQuests.Remove(state);
            if (!completedQuests.Contains(quest))
            {
                completedQuests.Add(quest);
            }

            if (pendingUsableItem != null && !HasOpenUseObjectiveForItem(pendingUsableItem))
            {
                pendingUsableItem = null;
            }

            Changed?.Invoke(this);
            return true;
        }

        public void SetTracked(MMOQuestDefinition quest, bool tracked)
        {
            MMOQuestRuntimeState state = FindActiveState(quest);
            if (state == null)
            {
                return;
            }

            state.SetTracked(tracked);
            Changed?.Invoke(this);
        }

        public bool NeedsQuestItem(MMOQuestDefinition quest, MMOItemDefinition item)
        {
            if (quest == null || item == null)
            {
                return false;
            }

            MMOQuestRuntimeState state = FindActiveState(quest);
            if (state == null)
            {
                return false;
            }

            for (int i = 0; i < quest.Objectives.Count; i++)
            {
                MMOQuestObjectiveDefinition objective = quest.Objectives[i];
                if (IsCollectObjective(objective) && objective.RequiredItem == item && state.GetProgress(i) < objective.RequiredCount)
                {
                    return true;
                }
            }

            return false;
        }

        public bool NeedsWorldItem(string worldObjectId, MMOItemDefinition item)
        {
            if (string.IsNullOrWhiteSpace(worldObjectId) || item == null)
            {
                return false;
            }

            foreach (MMOQuestRuntimeState state in activeQuests)
            {
                MMOQuestDefinition quest = state.Quest;
                if (quest == null)
                {
                    continue;
                }

                for (int i = 0; i < quest.Objectives.Count; i++)
                {
                    MMOQuestObjectiveDefinition objective = quest.Objectives[i];
                    if (objective.ObjectiveType == MMOQuestObjectiveType.CollectQuestItem
                        && objective.RequiredItem == item
                        && objective.RequiredWorldObjectId == worldObjectId
                        && state.GetProgress(i) < objective.RequiredCount)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool TryLootWorldItem(string worldObjectId, MMOItemDefinition item, int quantity)
        {
            if (!NeedsWorldItem(worldObjectId, item))
            {
                return false;
            }

            ResolveReferences();
            if (inventory == null || !inventory.TryAddItem(item, Mathf.Max(1, quantity), out int remaining) || remaining > 0)
            {
                return false;
            }

            RefreshCollectionObjectives();
            Changed?.Invoke(this);
            return true;
        }

        public bool CanBeginUseQuestItem(MMOItemDefinition item)
        {
            return item != null && HasOpenUseObjectiveForItem(item);
        }

        public bool TryBeginUseQuestItem(MMOItemDefinition item)
        {
            if (!CanBeginUseQuestItem(item))
            {
                return false;
            }

            pendingUsableItem = item;
            Changed?.Invoke(this);
            return true;
        }

        public bool CanUsePendingItemOnWorldObject(string worldObjectId)
        {
            return pendingUsableItem != null && FindUseObjective(pendingUsableItem, worldObjectId, out _, out _);
        }

        public bool TryUsePendingItemOnWorldObject(string worldObjectId)
        {
            if (pendingUsableItem == null || !FindUseObjective(pendingUsableItem, worldObjectId, out MMOQuestRuntimeState state, out int objectiveIndex))
            {
                return false;
            }

            state.AddProgress(objectiveIndex, 1);
            if (!HasOpenUseObjectiveForItem(pendingUsableItem))
            {
                pendingUsableItem = null;
            }

            Changed?.Invoke(this);
            return true;
        }

        public void RecordCreatureKilled(MMOEnemyDefinition creatureDefinition, string creatureId)
        {
            bool changed = false;
            foreach (MMOQuestRuntimeState state in activeQuests)
            {
                MMOQuestDefinition quest = state.Quest;
                if (quest == null)
                {
                    continue;
                }

                for (int i = 0; i < quest.Objectives.Count; i++)
                {
                    MMOQuestObjectiveDefinition objective = quest.Objectives[i];
                    if (objective.ObjectiveType != MMOQuestObjectiveType.KillCreature || state.GetProgress(i) >= objective.RequiredCount)
                    {
                        continue;
                    }

                    bool matchesDefinition = objective.RequiredCreature != null && objective.RequiredCreature == creatureDefinition;
                    bool matchesId = !string.IsNullOrWhiteSpace(objective.RequiredCreatureId) && objective.RequiredCreatureId == creatureId;
                    if (matchesDefinition || matchesId)
                    {
                        state.AddProgress(i, 1);
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                Changed?.Invoke(this);
            }
        }

        public void RecordSpeakToNpc(string npcId)
        {
            if (string.IsNullOrWhiteSpace(npcId))
            {
                return;
            }

            bool changed = false;
            foreach (MMOQuestRuntimeState state in activeQuests)
            {
                MMOQuestDefinition quest = state.Quest;
                if (quest == null)
                {
                    continue;
                }

                for (int i = 0; i < quest.Objectives.Count; i++)
                {
                    MMOQuestObjectiveDefinition objective = quest.Objectives[i];
                    if (objective.ObjectiveType == MMOQuestObjectiveType.SpeakToNpc
                        && objective.RequiredNpcId == npcId
                        && state.GetProgress(i) < objective.RequiredCount)
                    {
                        state.SetProgress(i, objective.RequiredCount);
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                Changed?.Invoke(this);
            }
        }

        public bool HasAvailableQuestForNpc(string npcId)
        {
            if (string.IsNullOrWhiteSpace(npcId) || questCatalog == null)
            {
                return false;
            }

            foreach (MMOQuestDefinition quest in questCatalog.Quests)
            {
                if (quest != null && quest.OfferedByNpcId == npcId && CanAccept(quest))
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasReadyTurnInForNpc(string npcId)
        {
            if (string.IsNullOrWhiteSpace(npcId))
            {
                return false;
            }

            foreach (MMOQuestRuntimeState state in activeQuests)
            {
                MMOQuestDefinition quest = state.Quest;
                if (quest != null && quest.TurnedInToNpcId == npcId && IsReadyToTurnIn(state))
                {
                    return true;
                }
            }

            return false;
        }

        public List<MMOQuestDefinition> GetAvailableQuestsForNpc(string npcId)
        {
            List<MMOQuestDefinition> quests = new();
            if (questCatalog == null)
            {
                return quests;
            }

            foreach (MMOQuestDefinition quest in questCatalog.Quests)
            {
                if (quest != null && quest.OfferedByNpcId == npcId && CanAccept(quest))
                {
                    quests.Add(quest);
                }
            }

            return quests;
        }

        public List<MMOQuestDefinition> GetTurnInQuestsForNpc(string npcId)
        {
            List<MMOQuestDefinition> quests = new();
            foreach (MMOQuestRuntimeState state in activeQuests)
            {
                MMOQuestDefinition quest = state.Quest;
                if (quest != null && quest.TurnedInToNpcId == npcId && IsReadyToTurnIn(state))
                {
                    quests.Add(quest);
                }
            }

            return quests;
        }

        public MMOQuestRuntimeState FindActiveState(MMOQuestDefinition quest)
        {
            foreach (MMOQuestRuntimeState state in activeQuests)
            {
                if (state != null && state.Quest == quest)
                {
                    return state;
                }
            }

            return null;
        }

        public bool IsReadyToTurnIn(MMOQuestRuntimeState state)
        {
            if (state == null || state.Quest == null)
            {
                return false;
            }

            RefreshCollectionObjective(state);
            for (int i = 0; i < state.Quest.Objectives.Count; i++)
            {
                if (state.GetProgress(i) < state.Quest.Objectives[i].RequiredCount)
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsCompleted(MMOQuestDefinition quest)
        {
            return quest != null && completedQuests.Contains(quest);
        }

        private void ResolveReferences()
        {
            if (inventory == null)
            {
                inventory = GetComponent<MMOInventoryContainer>();
            }

            if (identity == null)
            {
                identity = GetComponent<MMOCharacterIdentity>();
            }

            if (wallet == null)
            {
                wallet = GetComponent<MMOCurrencyWallet>();
            }

            if (experience == null)
            {
                experience = GetComponent<MMOExperienceComponent>();
            }
        }

        private void OnInventoryChanged()
        {
            RefreshCollectionObjectives();
            Changed?.Invoke(this);
        }

        private void RefreshCollectionObjectives()
        {
            foreach (MMOQuestRuntimeState state in activeQuests)
            {
                RefreshCollectionObjective(state);
            }
        }

        private void RefreshCollectionObjective(MMOQuestRuntimeState state)
        {
            if (state == null || state.Quest == null)
            {
                return;
            }

            ResolveReferences();
            for (int i = 0; i < state.Quest.Objectives.Count; i++)
            {
                MMOQuestObjectiveDefinition objective = state.Quest.Objectives[i];
                if (IsCollectObjective(objective) && objective.RequiredItem != null)
                {
                    int count = inventory != null ? inventory.CountItem(objective.RequiredItem) : 0;
                    state.SetProgress(i, Mathf.Min(count, objective.RequiredCount));
                }
            }
        }

        private static bool IsCollectObjective(MMOQuestObjectiveDefinition objective)
        {
            return objective != null
                && (objective.ObjectiveType == MMOQuestObjectiveType.CollectItem
                    || objective.ObjectiveType == MMOQuestObjectiveType.CollectQuestItem);
        }

        private bool ConsumeTurnInItems(MMOQuestDefinition quest)
        {
            ResolveReferences();
            if (inventory == null || quest == null)
            {
                return false;
            }

            foreach (MMOQuestObjectiveDefinition objective in quest.Objectives)
            {
                if (!IsCollectObjective(objective) || !objective.ConsumeRequiredItemsOnTurnIn || objective.RequiredItem == null)
                {
                    continue;
                }

                if (inventory.CountItem(objective.RequiredItem) < objective.RequiredCount)
                {
                    return false;
                }
            }

            foreach (MMOQuestObjectiveDefinition objective in quest.Objectives)
            {
                if (IsCollectObjective(objective) && objective.ConsumeRequiredItemsOnTurnIn && objective.RequiredItem != null)
                {
                    inventory.TryRemoveItem(objective.RequiredItem, objective.RequiredCount);
                }
            }

            return true;
        }

        private void GrantStartItems(MMOQuestDefinition quest)
        {
            ResolveReferences();
            if (inventory == null || quest == null)
            {
                return;
            }

            foreach (MMOItemStack stack in quest.StartItems)
            {
                if (stack != null && !stack.IsEmpty)
                {
                    inventory.TryAddStack(stack, out _);
                }
            }
        }

        private void GrantRewards(MMOQuestDefinition quest, MMOItemDefinition chosenReward)
        {
            ResolveReferences();
            MMOQuestRewardDefinition rewards = quest.Rewards;
            if (rewards == null)
            {
                return;
            }

            if (rewards.Experience > 0)
            {
                experience?.AddExperience(rewards.Experience);
            }

            if (rewards.MoneyCopper > 0)
            {
                wallet?.AddCopper(rewards.MoneyCopper);
            }

            if (inventory != null)
            {
                foreach (MMOItemStack stack in rewards.GuaranteedItems)
                {
                    if (stack != null && !stack.IsEmpty)
                    {
                        inventory.TryAddStack(stack, out _);
                    }
                }

                MMOItemDefinition selectedChoice = ChooseRewardItem(rewards, chosenReward);
                if (selectedChoice != null)
                {
                    inventory.TryAddItem(selectedChoice, 1, out _);
                }
            }
        }

        private MMOItemDefinition ChooseRewardItem(MMOQuestRewardDefinition rewards, MMOItemDefinition chosenReward)
        {
            if (rewards.ChoiceItems.Count == 0)
            {
                return null;
            }

            MMOCharacterEquipment equipment = GetComponent<MMOCharacterEquipment>();
            if (chosenReward != null && rewards.ChoiceItems.Contains(chosenReward) && (equipment == null || equipment.CanEquip(chosenReward)))
            {
                return chosenReward;
            }

            foreach (MMOItemDefinition candidate in rewards.ChoiceItems)
            {
                if (candidate != null && (equipment == null || equipment.CanEquip(candidate)))
                {
                    return candidate;
                }
            }

            return null;
        }

        private bool HasOpenUseObjectiveForItem(MMOItemDefinition item)
        {
            if (item == null)
            {
                return false;
            }

            foreach (MMOQuestRuntimeState state in activeQuests)
            {
                MMOQuestDefinition quest = state.Quest;
                if (quest == null)
                {
                    continue;
                }

                for (int i = 0; i < quest.Objectives.Count; i++)
                {
                    MMOQuestObjectiveDefinition objective = quest.Objectives[i];
                    if (objective.ObjectiveType == MMOQuestObjectiveType.UseItemOnWorldObject
                        && objective.UsableItem == item
                        && state.GetProgress(i) < objective.RequiredCount)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool FindUseObjective(MMOItemDefinition item, string worldObjectId, out MMOQuestRuntimeState state, out int objectiveIndex)
        {
            foreach (MMOQuestRuntimeState activeState in activeQuests)
            {
                MMOQuestDefinition quest = activeState.Quest;
                if (quest == null)
                {
                    continue;
                }

                for (int i = 0; i < quest.Objectives.Count; i++)
                {
                    MMOQuestObjectiveDefinition objective = quest.Objectives[i];
                    if (objective.ObjectiveType == MMOQuestObjectiveType.UseItemOnWorldObject
                        && objective.UsableItem == item
                        && objective.RequiredWorldObjectId == worldObjectId
                        && activeState.GetProgress(i) < objective.RequiredCount)
                    {
                        state = activeState;
                        objectiveIndex = i;
                        return true;
                    }
                }
            }

            state = null;
            objectiveIndex = -1;
            return false;
        }
    }
}
