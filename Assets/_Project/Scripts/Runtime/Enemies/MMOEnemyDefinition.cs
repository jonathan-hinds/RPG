using System.Collections.Generic;
using RPGClone.Abilities;
using RPGClone.Characters;
using RPGClone.Loot;
using UnityEngine;

namespace RPGClone.Enemies
{
    [CreateAssetMenu(menuName = "RPG Clone/Enemies/Enemy Definition", fileName = "EnemyDefinition")]
    public sealed class MMOEnemyDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private MMOCharacterProfile characterProfile;
        [SerializeField] private MMOEnemyDisposition disposition = MMOEnemyDisposition.Aggressive;

        [Header("Abilities")]
        [SerializeField] private MMOAbilityDefinition autoAttackAbility;
        [SerializeField] private List<MMOAbilityDefinition> abilities = new();

        [Header("Aggro")]
        [SerializeField, Min(0f)] private float aggroRadius = 12f;
        [SerializeField, Min(1f)] private float leashRadius = 28f;
        [SerializeField, Min(0.1f)] private float aggroScanInterval = 0.25f;

        [Header("Movement")]
        [SerializeField] private bool canRoam = true;
        [SerializeField, Min(0f)] private float roamRadius = 5f;
        [SerializeField, Min(0f)] private float minRoamIdleSeconds = 2.5f;
        [SerializeField, Min(0f)] private float maxRoamIdleSeconds = 6f;
        [SerializeField, Min(0.1f)] private float walkSpeed = 1.4f;
        [SerializeField, Min(0.1f)] private float chaseSpeed = 4.2f;
        [SerializeField, Min(0f)] private float stoppingDistance = 2.4f;

        [Header("Rewards")]
        [SerializeField, Min(0)] private int experienceReward = 45;
        [SerializeField] private MMOLootTable lootTable;

        [Header("Corpse And Respawn")]
        [SerializeField, Min(0f)] private float lootedCorpseDespawnSeconds = 2.5f;
        [SerializeField, Min(0f)] private float emptyCorpseDespawnSeconds = 6f;
        [SerializeField, Min(0f)] private float unlootedCorpseDespawnSeconds = 120f;
        [SerializeField, Min(0f)] private float respawnSeconds = 30f;

        public MMOCharacterProfile CharacterProfile => characterProfile;
        public MMOEnemyDisposition Disposition => disposition;
        public MMOAbilityDefinition AutoAttackAbility => autoAttackAbility;
        public IReadOnlyList<MMOAbilityDefinition> Abilities => abilities;
        public float AggroRadius => aggroRadius;
        public float LeashRadius => leashRadius;
        public float AggroScanInterval => aggroScanInterval;
        public bool CanRoam => canRoam;
        public float RoamRadius => roamRadius;
        public float MinRoamIdleSeconds => minRoamIdleSeconds;
        public float MaxRoamIdleSeconds => Mathf.Max(minRoamIdleSeconds, maxRoamIdleSeconds);
        public float WalkSpeed => walkSpeed;
        public float ChaseSpeed => chaseSpeed;
        public float StoppingDistance => stoppingDistance;
        public int ExperienceReward => experienceReward;
        public MMOLootTable LootTable => lootTable;
        public float LootedCorpseDespawnSeconds => lootedCorpseDespawnSeconds;
        public float EmptyCorpseDespawnSeconds => emptyCorpseDespawnSeconds;
        public float UnlootedCorpseDespawnSeconds => unlootedCorpseDespawnSeconds;
        public float RespawnSeconds => respawnSeconds;

        public void Configure(
            MMOCharacterProfile newCharacterProfile,
            MMOEnemyDisposition newDisposition,
            MMOAbilityDefinition newAutoAttackAbility,
            IEnumerable<MMOAbilityDefinition> newAbilities,
            float newAggroRadius,
            float newLeashRadius,
            float newAggroScanInterval,
            bool newCanRoam,
            float newRoamRadius,
            float newMinRoamIdleSeconds,
            float newMaxRoamIdleSeconds,
            float newWalkSpeed,
            float newChaseSpeed,
            float newStoppingDistance,
            int newExperienceReward = 0,
            MMOLootTable newLootTable = null,
            float newLootedCorpseDespawnSeconds = 2.5f,
            float newEmptyCorpseDespawnSeconds = 6f,
            float newUnlootedCorpseDespawnSeconds = 120f,
            float newRespawnSeconds = 30f)
        {
            characterProfile = newCharacterProfile;
            disposition = newDisposition;
            autoAttackAbility = newAutoAttackAbility;
            abilities = newAbilities != null ? new List<MMOAbilityDefinition>(newAbilities) : new List<MMOAbilityDefinition>();
            aggroRadius = Mathf.Max(0f, newAggroRadius);
            leashRadius = Mathf.Max(1f, newLeashRadius);
            aggroScanInterval = Mathf.Max(0.1f, newAggroScanInterval);
            canRoam = newCanRoam;
            roamRadius = Mathf.Max(0f, newRoamRadius);
            minRoamIdleSeconds = Mathf.Max(0f, newMinRoamIdleSeconds);
            maxRoamIdleSeconds = Mathf.Max(minRoamIdleSeconds, newMaxRoamIdleSeconds);
            walkSpeed = Mathf.Max(0.1f, newWalkSpeed);
            chaseSpeed = Mathf.Max(0.1f, newChaseSpeed);
            stoppingDistance = Mathf.Max(0f, newStoppingDistance);
            experienceReward = Mathf.Max(0, newExperienceReward);
            lootTable = newLootTable;
            lootedCorpseDespawnSeconds = Mathf.Max(0f, newLootedCorpseDespawnSeconds);
            emptyCorpseDespawnSeconds = Mathf.Max(0f, newEmptyCorpseDespawnSeconds);
            unlootedCorpseDespawnSeconds = Mathf.Max(0f, newUnlootedCorpseDespawnSeconds);
            respawnSeconds = Mathf.Max(0f, newRespawnSeconds);
        }
    }
}
