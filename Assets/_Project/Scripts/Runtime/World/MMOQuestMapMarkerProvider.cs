using System.Collections.Generic;
using RPGClone.Combat;
using RPGClone.Enemies;
using RPGClone.Inventory;
using RPGClone.Loot;
using RPGClone.Quests;
using UnityEngine;

namespace RPGClone.World
{
    public static class MMOQuestMapMarkerProvider
    {
        private static readonly Color ObjectiveColor = new(1f, 0.78f, 0.12f, 1f);
        private static readonly Color TurnInColor = new(1f, 0.92f, 0.24f, 1f);
        private static readonly Color CreatureAreaColor = new(0.95f, 0.22f, 0.14f, 1f);
        private static readonly Color WorldObjectColor = new(0.25f, 0.76f, 1f, 1f);
        private static readonly Color NpcColor = new(0.45f, 1f, 0.42f, 1f);

        public static List<MMOMapMarkerData> BuildTrackedQuestMarkers(MMOQuestLog questLog)
        {
            List<MMOMapMarkerData> markers = new();
            if (questLog == null)
            {
                return markers;
            }

            SceneQuestMarkerSources sources = SceneQuestMarkerSources.Capture();
            foreach (MMOQuestRuntimeState state in questLog.ActiveQuests)
            {
                if (state == null || state.Quest == null || !state.Tracked)
                {
                    continue;
                }

                if (questLog.IsReadyToTurnIn(state))
                {
                    AddNpcMarker(markers, sources, state.Quest.TurnedInToNpcId, $"{state.Quest.QuestId}_turn_in", state.Quest.DisplayName, "Turn in", MMOMapMarkerType.QuestTurnIn, TurnInColor);
                    continue;
                }

                MMOQuestDefinition quest = state.Quest;
                for (int i = 0; i < quest.Objectives.Count; i++)
                {
                    MMOQuestObjectiveDefinition objective = quest.Objectives[i];
                    if (objective == null || state.GetProgress(i) >= objective.RequiredCount)
                    {
                        continue;
                    }

                    AddObjectiveMarkers(markers, sources, quest, objective, i);
                }
            }

            return markers;
        }

        private static void AddObjectiveMarkers(List<MMOMapMarkerData> markers, SceneQuestMarkerSources sources, MMOQuestDefinition quest, MMOQuestObjectiveDefinition objective, int objectiveIndex)
        {
            int added = 0;
            switch (objective.ObjectiveType)
            {
                case MMOQuestObjectiveType.SpeakToNpc:
                    added += AddNpcMarker(markers, sources, objective.RequiredNpcId, $"{quest.QuestId}_{objective.ObjectiveId}_{objectiveIndex}_npc", quest.DisplayName, objective.Summary, MMOMapMarkerType.QuestNpc, NpcColor);
                    break;
                case MMOQuestObjectiveType.KillCreature:
                    added += AddCreatureMarkers(markers, sources, quest, objective, objectiveIndex, MMOMapMarkerType.QuestCreatureArea);
                    break;
                case MMOQuestObjectiveType.CollectQuestItem:
                    if (!string.IsNullOrWhiteSpace(objective.RequiredWorldObjectId))
                    {
                        added += AddWorldObjectMarkers(markers, sources, quest, objective, objectiveIndex);
                    }
                    else if (objective.RequiredCreature != null || !string.IsNullOrWhiteSpace(objective.RequiredCreatureId))
                    {
                        added += AddCreatureMarkers(markers, sources, quest, objective, objectiveIndex, MMOMapMarkerType.QuestCreatureArea);
                    }
                    else if (objective.RequiredItem != null)
                    {
                        added += AddLootSourceMarkers(markers, sources, quest, objective, objectiveIndex);
                    }
                    break;
                case MMOQuestObjectiveType.UseItemOnWorldObject:
                    added += AddWorldObjectMarkers(markers, sources, quest, objective, objectiveIndex);
                    break;
                case MMOQuestObjectiveType.CollectItem:
                    if (objective.RequiredCreature != null || !string.IsNullOrWhiteSpace(objective.RequiredCreatureId))
                    {
                        added += AddCreatureMarkers(markers, sources, quest, objective, objectiveIndex, MMOMapMarkerType.QuestCreatureArea);
                    }
                    else if (objective.RequiredItem != null)
                    {
                        added += AddLootSourceMarkers(markers, sources, quest, objective, objectiveIndex);
                    }
                    break;
            }

            if (added == 0 && objective.MapHints.Count > 0)
            {
                AddExplicitHintPoints(markers, quest, objective, objectiveIndex);
            }
        }

        private static void AddExplicitHintPoints(List<MMOMapMarkerData> markers, MMOQuestDefinition quest, MMOQuestObjectiveDefinition objective, int objectiveIndex)
        {
            foreach (MMOQuestObjectiveMapHint hint in objective.MapHints)
            {
                if (hint == null)
                {
                    continue;
                }

                Color color = ColorForType(hint.MarkerType);
                markers.Add(new MMOMapMarkerData(
                    $"{quest.QuestId}_{objective.ObjectiveId}_{objectiveIndex}_{hint.MarkerId}",
                    string.IsNullOrWhiteSpace(hint.Label) ? objective.Summary : hint.Label,
                    quest.DisplayName,
                    hint.WorldPosition,
                    0f,
                    hint.MarkerType,
                    color,
                    false));
            }
        }

        private static int AddNpcMarker(List<MMOMapMarkerData> markers, SceneQuestMarkerSources sources, string npcId, string markerId, string label, string detail, MMOMapMarkerType markerType, Color color)
        {
            if (string.IsNullOrWhiteSpace(npcId))
            {
                return 0;
            }

            int added = 0;
            foreach (MMOQuestNpc npc in sources.Npcs)
            {
                if (npc != null && npc.isActiveAndEnabled && npc.NpcId == npcId)
                {
                    markers.Add(new MMOMapMarkerData($"{markerId}_{MarkerObjectId(npc)}", label, detail, npc.transform.position, 0f, markerType, color, false));
                    added++;
                }
            }

            return added;
        }

        private static int AddCreatureMarkers(List<MMOMapMarkerData> markers, SceneQuestMarkerSources sources, MMOQuestDefinition quest, MMOQuestObjectiveDefinition objective, int objectiveIndex, MMOMapMarkerType markerType)
        {
            int added = 0;
            foreach (MMOEnemyController enemy in sources.Enemies)
            {
                if (!IsTrackableEnemy(enemy) || !MatchesCreature(enemy, objective))
                {
                    continue;
                }

                markers.Add(new MMOMapMarkerData(
                    $"{quest.QuestId}_{objective.ObjectiveId}_{objectiveIndex}_creature_{MarkerObjectId(enemy)}",
                    objective.Summary,
                    quest.DisplayName,
                    enemy.transform.position,
                    0f,
                    markerType,
                    CreatureAreaColor,
                    false));
                added++;
            }

            return added;
        }

        private static int AddWorldObjectMarkers(List<MMOMapMarkerData> markers, SceneQuestMarkerSources sources, MMOQuestDefinition quest, MMOQuestObjectiveDefinition objective, int objectiveIndex)
        {
            if (string.IsNullOrWhiteSpace(objective.RequiredWorldObjectId))
            {
                return 0;
            }

            int added = 0;
            foreach (MMOQuestWorldInteractable interactable in sources.WorldInteractables)
            {
                if (interactable != null && interactable.isActiveAndEnabled && interactable.WorldObjectId == objective.RequiredWorldObjectId)
                {
                    markers.Add(new MMOMapMarkerData(
                        $"{quest.QuestId}_{objective.ObjectiveId}_{objectiveIndex}_object_{MarkerObjectId(interactable)}",
                        objective.Summary,
                        quest.DisplayName,
                        interactable.transform.position,
                        0f,
                        MMOMapMarkerType.QuestWorldObject,
                        WorldObjectColor,
                        false));
                    added++;
                }
            }

            return added;
        }

        private static int AddLootSourceMarkers(List<MMOMapMarkerData> markers, SceneQuestMarkerSources sources, MMOQuestDefinition quest, MMOQuestObjectiveDefinition objective, int objectiveIndex)
        {
            MMOItemDefinition requiredItem = objective.RequiredItem;
            if (requiredItem == null)
            {
                return 0;
            }

            int added = 0;
            foreach (MMOQuestWorldInteractable interactable in sources.WorldInteractables)
            {
                if (interactable != null && interactable.isActiveAndEnabled && interactable.LootItem == requiredItem)
                {
                    markers.Add(new MMOMapMarkerData(
                        $"{quest.QuestId}_{objective.ObjectiveId}_{objectiveIndex}_loot_object_{MarkerObjectId(interactable)}",
                        objective.Summary,
                        quest.DisplayName,
                        interactable.transform.position,
                        0f,
                        MMOMapMarkerType.QuestWorldObject,
                        WorldObjectColor,
                        false));
                    added++;
                }
            }

            foreach (MMOEnemyController enemy in sources.Enemies)
            {
                if (IsTrackableEnemy(enemy) && EnemyCanDropItem(enemy, requiredItem, quest))
                {
                    markers.Add(new MMOMapMarkerData(
                        $"{quest.QuestId}_{objective.ObjectiveId}_{objectiveIndex}_loot_creature_{MarkerObjectId(enemy)}",
                        objective.Summary,
                        quest.DisplayName,
                        enemy.transform.position,
                        0f,
                        MMOMapMarkerType.QuestCreatureArea,
                        CreatureAreaColor,
                        false));
                    added++;
                }
            }

            return added;
        }

        private static bool EnemyCanDropItem(MMOEnemyController enemy, MMOItemDefinition requiredItem, MMOQuestDefinition quest)
        {
            MMOLootTable lootTable = enemy != null && enemy.Definition != null ? enemy.Definition.LootTable : null;
            if (lootTable == null)
            {
                return false;
            }

            foreach (MMOLootTableEntry entry in lootTable.Entries)
            {
                if (entry == null || entry.Item != requiredItem)
                {
                    continue;
                }

                return entry.RequiredQuest == null || entry.RequiredQuest == quest;
            }

            return false;
        }

        private static bool MatchesCreature(MMOEnemyController enemy, MMOQuestObjectiveDefinition objective)
        {
            if (objective.RequiredCreature != null && enemy.Definition == objective.RequiredCreature)
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(objective.RequiredCreatureId)
                && (enemy.name == objective.RequiredCreatureId || (enemy.Definition != null && enemy.Definition.name == objective.RequiredCreatureId));
        }

        private static bool IsTrackableEnemy(MMOEnemyController enemy)
        {
            if (enemy == null || !enemy.isActiveAndEnabled || !enemy.gameObject.activeInHierarchy)
            {
                return false;
            }

            MMOCombatant combatant = enemy.GetComponent<MMOCombatant>();
            if (combatant != null && !combatant.IsAlive)
            {
                return false;
            }

            return true;
        }

        private static Color ColorForType(MMOMapMarkerType markerType)
        {
            return markerType switch
            {
                MMOMapMarkerType.QuestTurnIn => TurnInColor,
                MMOMapMarkerType.QuestNpc => NpcColor,
                MMOMapMarkerType.QuestCreatureArea => CreatureAreaColor,
                MMOMapMarkerType.QuestWorldObject => WorldObjectColor,
                _ => ObjectiveColor
            };
        }

        private static int MarkerObjectId(Object target)
        {
            return target != null ? target.GetHashCode() : 0;
        }

        private sealed class SceneQuestMarkerSources
        {
            public MMOQuestNpc[] Npcs { get; private set; }
            public MMOEnemyController[] Enemies { get; private set; }
            public MMOQuestWorldInteractable[] WorldInteractables { get; private set; }

            public static SceneQuestMarkerSources Capture()
            {
                return new SceneQuestMarkerSources
                {
                    Npcs = UnityEngine.Object.FindObjectsByType<MMOQuestNpc>(FindObjectsInactive.Exclude),
                    Enemies = UnityEngine.Object.FindObjectsByType<MMOEnemyController>(FindObjectsInactive.Exclude),
                    WorldInteractables = UnityEngine.Object.FindObjectsByType<MMOQuestWorldInteractable>(FindObjectsInactive.Exclude)
                };
            }
        }
    }
}
