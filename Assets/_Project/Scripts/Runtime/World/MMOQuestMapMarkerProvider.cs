using System.Collections.Generic;
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

            foreach (MMOQuestRuntimeState state in questLog.ActiveQuests)
            {
                if (state == null || state.Quest == null || !state.Tracked)
                {
                    continue;
                }

                if (questLog.IsReadyToTurnIn(state))
                {
                    AddNpcMarker(markers, state.Quest.TurnedInToNpcId, state.Quest.DisplayName, "Turn in", MMOMapMarkerType.QuestTurnIn, TurnInColor);
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

                    AddObjectiveMarkers(markers, quest, objective, i);
                }
            }

            return markers;
        }

        private static void AddObjectiveMarkers(List<MMOMapMarkerData> markers, MMOQuestDefinition quest, MMOQuestObjectiveDefinition objective, int objectiveIndex)
        {
            if (objective.MapHints.Count > 0)
            {
                AddExplicitHints(markers, quest, objective, objectiveIndex);
                return;
            }

            switch (objective.ObjectiveType)
            {
                case MMOQuestObjectiveType.SpeakToNpc:
                    AddNpcMarker(markers, objective.RequiredNpcId, quest.DisplayName, objective.Summary, MMOMapMarkerType.QuestNpc, NpcColor);
                    break;
                case MMOQuestObjectiveType.KillCreature:
                    AddCreatureArea(markers, quest, objective, objectiveIndex);
                    break;
                case MMOQuestObjectiveType.CollectQuestItem:
                    if (!string.IsNullOrWhiteSpace(objective.RequiredWorldObjectId))
                    {
                        AddWorldObjectMarkers(markers, quest, objective, objectiveIndex);
                    }
                    else if (objective.RequiredCreature != null || !string.IsNullOrWhiteSpace(objective.RequiredCreatureId))
                    {
                        AddCreatureArea(markers, quest, objective, objectiveIndex);
                    }
                    else if (objective.RequiredItem != null)
                    {
                        AddLootSourceArea(markers, quest, objective, objectiveIndex);
                    }
                    break;
                case MMOQuestObjectiveType.UseItemOnWorldObject:
                    AddWorldObjectMarkers(markers, quest, objective, objectiveIndex);
                    break;
                case MMOQuestObjectiveType.CollectItem:
                    if (objective.RequiredCreature != null || !string.IsNullOrWhiteSpace(objective.RequiredCreatureId))
                    {
                        AddCreatureArea(markers, quest, objective, objectiveIndex);
                    }
                    else if (objective.RequiredItem != null)
                    {
                        AddLootSourceArea(markers, quest, objective, objectiveIndex);
                    }
                    break;
            }
        }

        private static void AddExplicitHints(List<MMOMapMarkerData> markers, MMOQuestDefinition quest, MMOQuestObjectiveDefinition objective, int objectiveIndex)
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
                    hint.Radius,
                    hint.MarkerType,
                    color,
                    hint.Area));
            }
        }

        private static void AddNpcMarker(List<MMOMapMarkerData> markers, string npcId, string label, string detail, MMOMapMarkerType markerType, Color color)
        {
            if (string.IsNullOrWhiteSpace(npcId))
            {
                return;
            }

            foreach (MMOQuestNpc npc in UnityEngine.Object.FindObjectsByType<MMOQuestNpc>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (npc != null && npc.NpcId == npcId)
                {
                    markers.Add(new MMOMapMarkerData(npcId, label, detail, npc.transform.position, 0f, markerType, color, false));
                }
            }
        }

        private static void AddCreatureArea(List<MMOMapMarkerData> markers, MMOQuestDefinition quest, MMOQuestObjectiveDefinition objective, int objectiveIndex)
        {
            List<Vector3> positions = new();
            foreach (MMOEnemyController enemy in UnityEngine.Object.FindObjectsByType<MMOEnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (enemy == null || !MatchesCreature(enemy, objective))
                {
                    continue;
                }

                positions.Add(enemy.transform.position);
            }

            AddGroupedArea(
                markers,
                $"{quest.QuestId}_{objective.ObjectiveId}_{objectiveIndex}_creatures",
                objective.Summary,
                quest.DisplayName,
                positions,
                MMOMapMarkerType.QuestCreatureArea,
                CreatureAreaColor,
                18f);
        }

        private static void AddWorldObjectMarkers(List<MMOMapMarkerData> markers, MMOQuestDefinition quest, MMOQuestObjectiveDefinition objective, int objectiveIndex)
        {
            if (string.IsNullOrWhiteSpace(objective.RequiredWorldObjectId))
            {
                return;
            }

            List<Vector3> positions = new();
            foreach (MMOQuestWorldInteractable interactable in UnityEngine.Object.FindObjectsByType<MMOQuestWorldInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (interactable != null && interactable.WorldObjectId == objective.RequiredWorldObjectId)
                {
                    positions.Add(interactable.transform.position);
                }
            }

            AddGroupedArea(
                markers,
                $"{quest.QuestId}_{objective.ObjectiveId}_{objectiveIndex}_objects",
                objective.Summary,
                quest.DisplayName,
                positions,
                MMOMapMarkerType.QuestWorldObject,
                WorldObjectColor,
                10f);

            for (int i = 0; i < positions.Count; i++)
            {
                markers.Add(new MMOMapMarkerData(
                    $"{quest.QuestId}_{objective.ObjectiveId}_{objectiveIndex}_object_{i}",
                    objective.Summary,
                    quest.DisplayName,
                    positions[i],
                    0f,
                    MMOMapMarkerType.QuestWorldObject,
                    WorldObjectColor,
                    false));
            }
        }

        private static void AddLootSourceArea(List<MMOMapMarkerData> markers, MMOQuestDefinition quest, MMOQuestObjectiveDefinition objective, int objectiveIndex)
        {
            MMOItemDefinition requiredItem = objective.RequiredItem;
            if (requiredItem == null)
            {
                return;
            }

            List<Vector3> worldObjectPositions = new();
            foreach (MMOQuestWorldInteractable interactable in UnityEngine.Object.FindObjectsByType<MMOQuestWorldInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (interactable != null && interactable.LootItem == requiredItem)
                {
                    worldObjectPositions.Add(interactable.transform.position);
                }
            }

            AddGroupedArea(
                markers,
                $"{quest.QuestId}_{objective.ObjectiveId}_{objectiveIndex}_loot_objects",
                objective.Summary,
                quest.DisplayName,
                worldObjectPositions,
                MMOMapMarkerType.QuestWorldObject,
                WorldObjectColor,
                10f);

            List<Vector3> enemyPositions = new();
            foreach (MMOEnemyController enemy in UnityEngine.Object.FindObjectsByType<MMOEnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (EnemyCanDropItem(enemy, requiredItem, quest))
                {
                    enemyPositions.Add(enemy.transform.position);
                }
            }

            AddGroupedArea(
                markers,
                $"{quest.QuestId}_{objective.ObjectiveId}_{objectiveIndex}_loot_creatures",
                objective.Summary,
                quest.DisplayName,
                enemyPositions,
                MMOMapMarkerType.QuestCreatureArea,
                CreatureAreaColor,
                18f);
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

        private static void AddGroupedArea(
            List<MMOMapMarkerData> markers,
            string markerId,
            string label,
            string detail,
            IReadOnlyList<Vector3> positions,
            MMOMapMarkerType markerType,
            Color color,
            float padding)
        {
            if (positions.Count == 0)
            {
                return;
            }

            Vector3 center = Vector3.zero;
            foreach (Vector3 position in positions)
            {
                center += position;
            }

            center /= positions.Count;
            float radius = padding;
            foreach (Vector3 position in positions)
            {
                Vector2 delta = new(position.x - center.x, position.z - center.z);
                radius = Mathf.Max(radius, delta.magnitude + padding);
            }

            markers.Add(new MMOMapMarkerData(markerId, label, detail, center, radius, markerType, color, positions.Count > 1));
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
    }
}
