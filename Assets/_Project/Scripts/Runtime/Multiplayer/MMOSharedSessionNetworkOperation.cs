using System;
using System.Collections.Generic;
using RPGClone.Combat;
using RPGClone.Enemies;
using RPGClone.Inventory;
using RPGClone.Loot;
using RPGClone.Quests;

namespace RPGClone.Multiplayer
{
    [Serializable]
    public sealed class MMOSharedSessionNetworkOperation
    {
        public string kind;
        public string sessionId;
        public string characterId;
        public string observerCharacterId;
        public string eventId;
        public string requestId;
        public string initiallyAppliedCharacterId;
        public MMOSessionParticipantSnapshot participant;
        public CombatActionRequest combatRequest;
        public CombatEventRecord combatEvent;
        public MMOSharedRewardEvent rewardEvent;
        public MMOSharedWorldObjectSnapshot worldObjectSnapshot;
        public MMOSharedWorldObjectInteractionRequest worldObjectInteractionRequest;
        public MMONpcFacingSnapshot npcFacingSnapshot;
        public MMOConsumableUseRequest consumableUseRequest;
        public EnemySnapshot enemySnapshot;
        public MMOCorpseLootState corpseLootSnapshot;
        public List<MMOSharedWorldObjectSnapshot> worldObjectSnapshots = new();
        public List<EnemySnapshot> enemySnapshots = new();
        public MMOSharedAbilityEvent abilityEvent;
    }

    public static class MMOSharedSessionNetworkOperationKind
    {
        public const string UpsertParticipant = "upsert_participant";
        public const string RemoveParticipant = "remove_participant";
        public const string PublishAbilityEvent = "publish_ability_event";
        public const string MarkAbilityEventApplied = "mark_ability_event_applied";
        public const string PublishCombatRequest = "publish_combat_request";
        public const string MarkCombatRequestProcessed = "mark_combat_request_processed";
        public const string PublishCombatEvent = "publish_combat_event";
        public const string MarkCombatEventApplied = "mark_combat_event_applied";
        public const string PublishRewardEvent = "publish_reward_event";
        public const string MarkRewardEventApplied = "mark_reward_event_applied";
        public const string UpsertWorldObjectSnapshot = "upsert_world_object_snapshot";
        public const string UpsertWorldObjectSnapshots = "upsert_world_object_snapshots";
        public const string PublishWorldObjectInteractionRequest = "publish_world_object_interaction_request";
        public const string UpsertNpcFacingSnapshot = "upsert_npc_facing_snapshot";
        public const string RequestConsumableUse = "request_consumable_use";
        public const string MarkWorldObjectInteractionRequestProcessed = "mark_world_object_interaction_request_processed";
        public const string UpsertEnemySnapshot = "upsert_enemy_snapshot";
        public const string UpsertEnemySnapshots = "upsert_enemy_snapshots";
        public const string UpsertCorpseLootSnapshot = "upsert_corpse_loot_snapshot";
        public const string RequestSnapshot = "request_snapshot";
    }
}
