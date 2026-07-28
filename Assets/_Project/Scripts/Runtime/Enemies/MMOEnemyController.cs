using System.Collections;
using System.Collections.Generic;
using System;
using RPGClone.Abilities;
using RPGClone.Animation;
using RPGClone.CharacterSelection;
using RPGClone.Characters;
using RPGClone.Combat;
using RPGClone.Inventory;
using RPGClone.Loot;
using RPGClone.Quests;
using RPGClone.Services;
using UnityEngine;
using UnityEngine.AI;

namespace RPGClone.Enemies
{
    [RequireComponent(typeof(MMOCharacterIdentity))]
    [RequireComponent(typeof(MMOCombatant))]
    [RequireComponent(typeof(MMOAbilitySystem))]
    [RequireComponent(typeof(MMOAutoAttackController))]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class MMOEnemyController : MonoBehaviour, IEnemySessionAuthority, IEnemyStateReplicator, IMMOCreatureLocomotionSource, IMMOHostileActionReceiver
    {
        private const int DetectionBufferSize = 16;
        private static readonly Dictionary<string, MMOEnemyController> ActiveEnemiesBySpawnId = new();

        [SerializeField] private MMOEnemyDefinition definition;
        [SerializeField] private string stableSpawnId;
        [SerializeField] private string displayNameOverride = string.Empty;
        [SerializeField] private LayerMask aggroMask = ~0;
        [SerializeField] private bool resetResourcesOnLeash = true;
        [SerializeField] private bool drawDebugGizmos = true;
        [SerializeField, Min(0.02f)] private float chaseRepathInterval = 0.2f;
        [SerializeField, Min(0.01f)] private float chaseRepathDistance = 0.35f;
        [SerializeField, Range(0.5f, 0.95f)] private float engagementRangeFraction = 0.85f;
        [SerializeField, Range(0.01f, 0.2f)] private float engagementStoppingDistanceFraction = 0.05f;
        [SerializeField, Min(0.02f)] private float proxyInterpolationSeconds = 0.18f;
        [SerializeField, Min(0.5f)] private float proxySnapDistance = 8f;
        [SerializeField] private MMORewardEligibilitySettings rewardEligibility = new();

        private readonly Collider[] detectionBuffer = new Collider[DetectionBufferSize];
        private readonly MMOEnemyLeashStateMachine leashState = new();
        private MMOCharacterIdentity identity;
        private MMOCombatant combatant;
        private MMOAbilitySystem abilitySystem;
        private MMOAutoAttackController autoAttackController;
        private MMOCreatureAnimator creatureAnimator;
        private NavMeshAgent agent;
        private MMOLootableCorpse lootableCorpse;
        private MMOCharacterIdentity currentTarget;
        private Vector3 homePosition;
        private Quaternion homeRotation;
        private float nextAggroScanTime;
        private float nextRoamDecisionTime;
        private bool waitingAtRoamPoint;
        private bool configured;
        private bool corpseActive;
        private float nextChaseRepathTime;
        private float nextReturnRepathTime;
        private Vector3 lastChaseTargetPosition;
        private Coroutine despawnRoutine;
        private MMOCombatant lastDamageSource;
        private Renderer[] renderers;
        private Collider[] colliders;
        private bool respawning;
        private float corpseDespawnEndTime;
        private float respawnEndTime;
        private Vector3 proxyTargetPosition;
        private Quaternion proxyTargetRotation;
        private float proxyWorldSpeed;
        private bool proxyHasSnapshot;
        private bool authorityEventsSubscribed;
        private bool authorityModeInitialized;
        private bool lastAuthorityMode;
        private long lastAppliedSnapshotUtcTicks;

        public MMOEnemyDefinition Definition => definition;
        public string SpawnId
        {
            get
            {
                EnsureSpawnId();
                return stableSpawnId;
            }
        }

        public MMOCharacterIdentity CurrentTarget => currentTarget;
        public bool IsInCombat => currentTarget != null && !corpseActive;
        public bool IsReturningHome => leashState.IsReturningHome;
        public bool CanReceiveHostileActions => !corpseActive
            && !respawning
            && combatant != null
            && combatant.IsAlive
            && !leashState.IsReturningHome;
        public bool IsAuthorityOwner => MMOGameplaySessionService.IsHostAuthority;
        public float CurrentWorldSpeed => IsAuthorityOwner ? GetAuthoritativeWorldSpeed() : proxyWorldSpeed;

        public static IReadOnlyCollection<MMOEnemyController> ActiveEnemies => ActiveEnemiesBySpawnId.Values;

        public static bool TryGetEnemy(string spawnId, out MMOEnemyController enemy)
        {
            if (string.IsNullOrWhiteSpace(spawnId))
            {
                enemy = null;
                return false;
            }

            return ActiveEnemiesBySpawnId.TryGetValue(spawnId, out enemy) && enemy != null;
        }

        private void Awake()
        {
            EnsureReferences();
            EnsureSpawnId();
            homePosition = transform.position;
            homeRotation = transform.rotation;
            leashState.Reset(homePosition);
            CachePresentationComponents();
            ConfigureFromDefinition(true);
        }

        private void OnEnable()
        {
            EnsureReferences();
            RegisterSpawn();
            RefreshAuthorityMode();
        }

        private void OnDisable()
        {
            UnregisterSpawn();
            SetAuthorityEventSubscriptions(false);
            authorityModeInitialized = false;
        }

        private void Update()
        {
            RefreshAuthorityMode();
            if (!IsAuthorityOwner)
            {
                UpdateProxyPresentation();
                StopMoving();
                return;
            }

            if (definition == null || corpseActive || !combatant.IsAlive)
            {
                StopMoving();
                return;
            }

            EnsureAgentOnNavMesh();

            if (leashState.IsReturningHome)
            {
                UpdateReturnHome();
                return;
            }

            if (leashState.Phase == MMOEnemyLeashPhase.Engaged && currentTarget == null)
            {
                BeginReturnHome();
                return;
            }

            if (currentTarget != null)
            {
                UpdateCombat();
                return;
            }

            if (definition.Disposition == MMOEnemyDisposition.Aggressive && Time.time >= nextAggroScanTime)
            {
                nextAggroScanTime = Time.time + definition.AggroScanInterval;
                TryAcquireAggroTarget();
            }

            if (currentTarget == null)
            {
                UpdateRoaming();
            }
        }

        public void SetDefinition(MMOEnemyDefinition newDefinition, bool resetResources = true)
        {
            definition = newDefinition;
            configured = false;
            ConfigureFromDefinition(resetResources);
        }

        public EnemySnapshot CreateSnapshot()
        {
            EnsureReferences();
            EnsureSpawnId();
            EnemyRuntimeState runtimeState = respawning
                ? EnemyRuntimeState.Respawning
                : corpseActive || !combatant.IsAlive
                    ? EnemyRuntimeState.Corpse
                    : EnemyRuntimeState.Alive;

            string currentTargetCharacterId = string.Empty;
            if (currentTarget != null && MMOGameplaySessionService.Players.TryGetParticipant(currentTarget, out MMOPlayerParticipant targetParticipant))
            {
                currentTargetCharacterId = targetParticipant.CharacterId;
            }

            MMOAbilityDefinition castAbility = abilitySystem != null ? abilitySystem.CurrentCastAbility : null;
            string castTargetCharacterId = string.Empty;
            MMOCharacterIdentity castTarget = abilitySystem != null ? abilitySystem.CurrentCastTarget : null;
            if (castTarget != null && MMOGameplaySessionService.Players.TryGetParticipant(castTarget, out MMOPlayerParticipant castTargetParticipant))
            {
                castTargetCharacterId = castTargetParticipant.CharacterId;
            }

            return new EnemySnapshot
            {
                sessionId = MMOGameplaySessionService.SessionId,
                spawnId = SpawnId,
                definitionId = definition != null ? definition.name : string.Empty,
                displayName = identity != null ? identity.DisplayName : gameObject.name,
                runtimeState = runtimeState,
                currentHealth = identity != null ? identity.Health.CurrentValue : 0,
                maxHealth = identity != null ? identity.Health.MaxValue : 0,
                currentMana = identity != null ? identity.Mana.CurrentValue : 0,
                maxMana = identity != null ? identity.Mana.MaxValue : 0,
                position = new Vector3SaveData(transform.position),
                rotationEuler = new Vector3SaveData(transform.eulerAngles),
                worldSpeed = runtimeState == EnemyRuntimeState.Alive ? GetAuthoritativeWorldSpeed() : 0f,
                currentTargetCharacterId = currentTargetCharacterId,
                inCombat = IsInCombat,
                leashing = leashState.IsReturningHome,
                leashAnchorPosition = new Vector3SaveData(leashState.AnchorPosition),
                castAbilityId = castAbility != null ? castAbility.AbilityId : string.Empty,
                castTargetCharacterId = castTargetCharacterId,
                castDurationSeconds = abilitySystem != null ? abilitySystem.CurrentCastDuration : 0f,
                castNormalizedProgress = abilitySystem != null ? abilitySystem.CurrentCastNormalized : 0f,
                corpseRemainingSeconds = Mathf.Max(0f, corpseDespawnEndTime - Time.time),
                respawnRemainingSeconds = Mathf.Max(0f, respawnEndTime - Time.time),
                updatedUtcTicks = DateTime.UtcNow.Ticks
            };
        }

        public void ApplySnapshot(EnemySnapshot snapshot)
        {
            if (snapshot == null
                || snapshot.spawnId != SpawnId
                || IsAuthorityOwner
                || snapshot.updatedUtcTicks <= lastAppliedSnapshotUtcTicks)
            {
                return;
            }

            EnsureReferences();
            lastAppliedSnapshotUtcTicks = snapshot.updatedUtcTicks;
            Vector3 snapshotPosition = snapshot.position.ToVector3();
            Quaternion snapshotRotation = Quaternion.Euler(snapshot.rotationEuler.ToVector3());
            bool wasRespawning = respawning;
            bool wasCorpse = corpseActive;
            ApplyResourceSnapshot(identity.Health, Mathf.Max(1, snapshot.maxHealth), snapshot.currentHealth);
            ApplyResourceSnapshot(identity.Mana, Mathf.Max(0, snapshot.maxMana), snapshot.currentMana);
            corpseActive = snapshot.runtimeState == EnemyRuntimeState.Corpse;
            respawning = snapshot.runtimeState == EnemyRuntimeState.Respawning;
            currentTarget = ResolveSnapshotTarget(snapshot);
            leashState.ApplyReplicatedState(
                snapshot.inCombat,
                snapshot.leashing,
                snapshot.leashAnchorPosition.ToVector3(),
                homePosition);
            creatureAnimator?.SetDeadState(snapshot.runtimeState != EnemyRuntimeState.Alive);
            bool shouldSnap = !proxyHasSnapshot
                || wasRespawning != respawning
                || wasCorpse != corpseActive
                || (transform.position - snapshotPosition).sqrMagnitude >= proxySnapDistance * proxySnapDistance;
            proxyTargetPosition = snapshotPosition;
            proxyTargetRotation = snapshotRotation;
            proxyWorldSpeed = snapshot.runtimeState == EnemyRuntimeState.Alive ? Mathf.Max(0f, snapshot.worldSpeed) : 0f;
            proxyHasSnapshot = true;
            MMOAbilityDefinition replicatedCastAbility = abilitySystem.FindKnownAbilityById(snapshot.castAbilityId);
            MMOCharacterIdentity replicatedCastTarget = null;
            if (!string.IsNullOrWhiteSpace(snapshot.castTargetCharacterId)
                && MMOGameplaySessionService.Players.TryGetParticipantByCharacterId(snapshot.castTargetCharacterId, out MMOPlayerParticipant castTargetParticipant))
            {
                replicatedCastTarget = castTargetParticipant.Identity;
            }

            abilitySystem.ApplyReplicatedCastSnapshot(
                replicatedCastAbility,
                replicatedCastTarget,
                snapshot.castDurationSeconds,
                snapshot.castNormalizedProgress);
            if (shouldSnap)
            {
                transform.SetPositionAndRotation(proxyTargetPosition, proxyTargetRotation);
            }

            SetPresentationActive(snapshot.runtimeState != EnemyRuntimeState.Respawning);
            identity.SetSelectable(snapshot.runtimeState == EnemyRuntimeState.Alive);
            if (agent != null)
            {
                agent.enabled = false;
            }
        }

        private void UpdateProxyPresentation()
        {
            if (!proxyHasSnapshot || respawning)
            {
                proxyWorldSpeed = 0f;
                return;
            }

            float interpolation = Mathf.Clamp01(Time.deltaTime / Mathf.Max(0.02f, proxyInterpolationSeconds));
            transform.position = Vector3.Lerp(transform.position, proxyTargetPosition, interpolation);
            transform.rotation = Quaternion.Slerp(transform.rotation, proxyTargetRotation, interpolation);
        }

        private void ConfigureFromDefinition(bool resetResources)
        {
            EnsureReferences();
            if (configured || definition == null)
            {
                return;
            }

            identity.Configure(definition.CharacterProfile, displayNameOverride, resetResources);
            abilitySystem.LearnAbility(definition.AutoAttackAbility);
            foreach (MMOAbilityDefinition ability in definition.Abilities)
            {
                abilitySystem.LearnAbility(ability);
            }

            autoAttackController.SetHandleRightClickInput(false);
            autoAttackController.SetAutoAttackAbility(definition.AutoAttackAbility);

            agent.speed = definition.WalkSpeed * GetMovementSpeedMultiplier();
            agent.stoppingDistance = definition.StoppingDistance;
            agent.autoBraking = true;
            agent.updateRotation = true;

            configured = true;
        }

        private void UpdateCombat()
        {
            if (!IsValidTarget(currentTarget))
            {
                BeginReturnHome();
                return;
            }

            if (leashState.ShouldReturnHome(
                    currentTarget.transform.position,
                    definition.LeashRadius,
                    definition.LeashGraceSeconds,
                    Time.time))
            {
                BeginReturnHome();
                return;
            }

            float sqrDistance = (currentTarget.transform.position - transform.position).sqrMagnitude;

            if (abilitySystem.IsCasting)
            {
                autoAttackController.StopAutoAttack();
                StopForCasting();
                FaceCurrentTarget();
                return;
            }

            MMOAbilityDefinition readySpell = FindReadyCombatSpell();
            if (readySpell != null)
            {
                float spellRange = Mathf.Max(0.1f, readySpell.Range);
                bool inSpellRange = sqrDistance <= spellRange * spellRange;
                autoAttackController.StopAutoAttack();

                if (inSpellRange)
                {
                    StopForCasting();
                    FaceCurrentTarget();
                    abilitySystem.TryUseAbility(readySpell, currentTarget, out _);
                }
                else
                {
                    ChaseTarget(spellRange);
                }

                return;
            }

            float attackRange = GetAttackRange();
            bool inAttackRange = sqrDistance <= attackRange * attackRange;

            ChaseTarget(attackRange, inAttackRange);

            if (autoAttackController.CurrentTarget != currentTarget)
            {
                autoAttackController.StartAutoAttack(currentTarget);
            }
        }

        private void ChaseTarget(float desiredRange, bool? rangeOverride = null)
        {
            bool inRange = rangeOverride ?? abilitySystem.IsInRange(currentTarget, desiredRange);

            if (CanMoveOnNavMesh())
            {
                float sanitizedRange = Mathf.Max(0.1f, desiredRange);
                agent.speed = definition.ChaseSpeed * GetMovementSpeedMultiplier();
                agent.stoppingDistance = Mathf.Max(
                    0.01f,
                    sanitizedRange * Mathf.Clamp(
                        engagementStoppingDistanceFraction,
                        0.01f,
                        0.2f));
                agent.isStopped = inRange;

                Vector3 targetPosition = currentTarget.transform.position;
                if (!inRange && ShouldRepathToTarget(targetPosition))
                {
                    lastChaseTargetPosition = targetPosition;
                    nextChaseRepathTime = Time.time + chaseRepathInterval;
                    agent.SetDestination(CalculateEngagementDestination(
                        transform.position,
                        targetPosition,
                        sanitizedRange,
                        engagementRangeFraction));
                }
            }
        }

        private static Vector3 CalculateEngagementDestination(
            Vector3 enemyPosition,
            Vector3 targetPosition,
            float desiredRange,
            float rangeFraction)
        {
            Vector3 directionFromTarget = enemyPosition - targetPosition;
            directionFromTarget.y = 0f;
            if (directionFromTarget.sqrMagnitude <= 0.0001f)
            {
                return enemyPosition;
            }

            float engagementDistance = Mathf.Max(0.1f, desiredRange)
                * Mathf.Clamp(rangeFraction, 0.5f, 0.95f);
            Vector3 destination = targetPosition + directionFromTarget.normalized * engagementDistance;
            destination.y = targetPosition.y;
            return destination;
        }

        private MMOAbilityDefinition FindReadyCombatSpell()
        {
            if (definition == null || identity == null || abilitySystem == null)
            {
                return null;
            }

            foreach (MMOAbilityDefinition ability in definition.Abilities)
            {
                if (ability == null
                    || ability == definition.AutoAttackAbility
                    || ability.IsAutoAttack
                    || ability.RequiresGroundTarget
                    || ability.TargetType != MMOAbilityTargetType.Hostile
                    || ability.ManaCost > identity.Mana.CurrentValue
                    || abilitySystem.IsOnCooldown(ability, out _))
                {
                    continue;
                }

                return ability;
            }

            return null;
        }

        private void StopForCasting()
        {
            if (!CanMoveOnNavMesh())
            {
                return;
            }

            agent.isStopped = true;
            if (agent.hasPath)
            {
                agent.ResetPath();
            }
        }

        private void FaceCurrentTarget()
        {
            if (currentTarget == null)
            {
                return;
            }

            Vector3 direction = currentTarget.transform.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
        }

        private void UpdateRoaming()
        {
            if (!definition.CanRoam || definition.RoamRadius <= 0f || !CanMoveOnNavMesh())
            {
                StopMoving();
                return;
            }

            agent.speed = definition.WalkSpeed * GetMovementSpeedMultiplier();
            agent.stoppingDistance = 0.15f;

            bool hasArrived = !agent.pathPending && agent.remainingDistance <= Mathf.Max(agent.stoppingDistance, 0.25f);
            if (!hasArrived && agent.hasPath)
            {
                return;
            }

            if (agent.hasPath && hasArrived)
            {
                agent.isStopped = true;
                waitingAtRoamPoint = true;
                nextRoamDecisionTime = Time.time + UnityEngine.Random.Range(definition.MinRoamIdleSeconds, definition.MaxRoamIdleSeconds);
                return;
            }

            if (waitingAtRoamPoint && Time.time < nextRoamDecisionTime)
            {
                agent.isStopped = true;
                return;
            }

            if (TryGetRandomNavMeshPoint(homePosition, definition.RoamRadius, out Vector3 destination))
            {
                waitingAtRoamPoint = false;
                agent.isStopped = false;
                agent.SetDestination(destination);
            }
        }

        private void TryAcquireAggroTarget()
        {
            int hitCount = Physics.OverlapSphereNonAlloc(
                transform.position,
                definition.AggroRadius,
                detectionBuffer,
                aggroMask,
                QueryTriggerInteraction.Ignore);

            MMOCharacterIdentity bestTarget = null;
            float bestSqrDistance = float.MaxValue;
            for (int i = 0; i < hitCount; i++)
            {
                MMOCharacterIdentity candidate = detectionBuffer[i].GetComponentInParent<MMOCharacterIdentity>();
                if (!IsValidTarget(candidate))
                {
                    continue;
                }

                float sqrDistance = (candidate.transform.position - transform.position).sqrMagnitude;
                if (sqrDistance < bestSqrDistance)
                {
                    bestSqrDistance = sqrDistance;
                    bestTarget = candidate;
                }
            }

            if (bestTarget != null)
            {
                EnterCombat(bestTarget);
            }
        }

        private void EnterCombat(MMOCharacterIdentity target)
        {
            if (!IsValidTarget(target) || !leashState.BeginEngagement(transform.position, Time.time))
            {
                return;
            }

            currentTarget = target;
            MMOCombatant targetCombatant = target.GetComponent<MMOCombatant>();
            combatant.EngageCombatWith(targetCombatant);
            waitingAtRoamPoint = false;
            nextRoamDecisionTime = 0f;
            nextChaseRepathTime = 0f;
            if (CanMoveOnNavMesh())
            {
                agent.ResetPath();
            }
        }

        private void BeginReturnHome()
        {
            if (leashState.IsReturningHome)
            {
                return;
            }

            leashState.BeginReturnHome();
            ClearCombat(true);
            waitingAtRoamPoint = false;
            nextChaseRepathTime = 0f;
            nextReturnRepathTime = 0f;
            SetReturnDestination();
        }

        private void ClearCombat(bool evading)
        {
            abilitySystem.CancelActiveCast(evading ? "Casting interrupted by evading." : "Casting interrupted.");
            currentTarget = null;
            autoAttackController.StopAutoAttack();
            combatant.DisengageFromAllCombat();

            if (!evading)
            {
                leashState.Reset(homePosition);
            }
        }

        private static void ApplyResourceSnapshot(MMOCharacterResource resource, int maxValue, int currentValue)
        {
            if (resource == null
                || (resource.MaxValue == maxValue && resource.CurrentValue == Mathf.Clamp(currentValue, 0, maxValue)))
            {
                return;
            }

            resource.Configure(maxValue, currentValue);
        }

        private void UpdateReturnHome()
        {
            if (!CanMoveOnNavMesh())
            {
                return;
            }

            if (leashState.IsAtHome(transform.position, homePosition, definition.LeashReturnArrivalDistance))
            {
                agent.isStopped = true;
                agent.ResetPath();
                transform.rotation = homeRotation;
                leashState.CompleteReturnHome(homePosition);
                if (resetResourcesOnLeash)
                {
                    identity.RestoreResources();
                }

                nextAggroScanTime = Time.time + definition.AggroScanInterval;
                waitingAtRoamPoint = false;
                return;
            }

            agent.speed = definition.ChaseSpeed
                * definition.LeashReturnSpeedMultiplier
                * GetMovementSpeedMultiplier();
            agent.stoppingDistance = definition.LeashReturnArrivalDistance;
            agent.isStopped = false;
            if (!agent.pathPending && (!agent.hasPath || Time.time >= nextReturnRepathTime))
            {
                SetReturnDestination();
            }
        }

        private void SetReturnDestination()
        {
            if (!CanMoveOnNavMesh())
            {
                return;
            }

            agent.speed = definition.ChaseSpeed
                * definition.LeashReturnSpeedMultiplier
                * GetMovementSpeedMultiplier();
            agent.stoppingDistance = definition.LeashReturnArrivalDistance;
            agent.isStopped = false;
            agent.SetDestination(homePosition);
            nextReturnRepathTime = Time.time + chaseRepathInterval;
        }

        private void OnDamaged(MMOCombatant source, MMOCombatant target, MMOAbilityDefinition ability, int amount)
        {
            if (target != combatant || source == null || source.Identity == null)
            {
                return;
            }

            lastDamageSource = source;
            EnterCombat(source.Identity);
            if (amount > 0 && IsAuthorityOwner && CanReceiveHostileActions)
            {
                leashState.RecordCombatActivity(transform.position, Time.time);
            }
        }

        private void OnDamageDealt(MMOCombatant source, MMOCombatant target, MMOAbilityDefinition ability, int amount)
        {
            if (source == combatant && amount > 0 && IsAuthorityOwner && CanReceiveHostileActions)
            {
                leashState.RecordCombatActivity(transform.position, Time.time);
            }
        }

        private void OnDied(MMOCombatant deadCombatant)
        {
            if (deadCombatant != combatant)
            {
                return;
            }

            ClearCombat(false);
            StopMoving();
            AwardDeathRewards();
            BecomeCorpse();
        }

        private void AwardDeathRewards()
        {
            if (definition == null || !TryResolveLastDamageParticipant(out MMOPlayerParticipant sourceParticipant))
            {
                return;
            }

            Vector3 eventPosition = transform.position;
            MMOPartyExperienceRewardService.AwardEnemyExperience(
                definition,
                sourceParticipant,
                eventPosition,
                rewardEligibility.PartyExperienceRadius);
            MMOPartyQuestCreditService.AwardKillCredit(
                definition,
                definition != null ? definition.name : gameObject.name,
                SpawnId,
                sourceParticipant,
                eventPosition,
                rewardEligibility.PartyCreditRadius);
        }

        private void BecomeCorpse()
        {
            corpseActive = true;
            creatureAnimator?.SetDeadState(true);
            identity.SetSelectable(false);
            if (agent != null)
            {
                agent.enabled = false;
            }

            lootableCorpse.LootEmptied -= OnCorpseLooted;
            lootableCorpse.AllPersonalLootEmptied -= OnAllPersonalLooted;

            MMOCorpseLootState corpseLoot = null;
            if (TryResolveLastDamageParticipant(out MMOPlayerParticipant sourceParticipant))
            {
                corpseLoot = MMOPersonalLootService.GenerateCorpseLoot(
                    SpawnId,
                    definition,
                    sourceParticipant,
                    transform.position,
                    rewardEligibility.PartyLootRadius);
                lootableCorpse.SetPersonalLoot(corpseLoot);
                MMOPersonalLootService.PublishCorpseLoot(corpseLoot);
            }
            else
            {
                lootableCorpse.ClearLoot();
            }

            if (corpseLoot != null && corpseLoot.HasAnyUnlootedItems())
            {
                lootableCorpse.AllPersonalLootEmptied += OnAllPersonalLooted;
                BeginDespawn(definition.UnlootedCorpseDespawnSeconds);
            }
            else
            {
                BeginDespawn(definition.EmptyCorpseDespawnSeconds);
            }
        }

        private void OnCorpseLooted(MMOLootableCorpse corpse)
        {
            lootableCorpse.LootEmptied -= OnCorpseLooted;
            BeginDespawn(definition != null ? definition.LootedCorpseDespawnSeconds : 2.5f);
        }

        private void OnAllPersonalLooted(MMOLootableCorpse corpse)
        {
            lootableCorpse.LootEmptied -= OnCorpseLooted;
            lootableCorpse.AllPersonalLootEmptied -= OnAllPersonalLooted;
            BeginDespawn(definition != null ? definition.LootedCorpseDespawnSeconds : 2.5f);
        }

        private void BeginDespawn(float delaySeconds)
        {
            if (despawnRoutine != null)
            {
                StopCoroutine(despawnRoutine);
            }

            corpseDespawnEndTime = Time.time + Mathf.Max(0f, delaySeconds);
            despawnRoutine = StartCoroutine(DespawnAndRespawn(delaySeconds));
        }

        private IEnumerator DespawnAndRespawn(float corpseDelaySeconds)
        {
            if (corpseDelaySeconds > 0f)
            {
                yield return new WaitForSeconds(corpseDelaySeconds);
            }

            SetPresentationActive(false);
            lootableCorpse.ClearLoot();
            respawning = true;

            float respawnDelay = definition != null
                ? definition.RespawnSeconds
                : MMOClassicRespawnDefaults.StandardOutdoorSeconds;
            respawnEndTime = Time.time + Mathf.Max(0f, respawnDelay);
            if (respawnDelay > 0f)
            {
                yield return new WaitForSeconds(respawnDelay);
            }

            Respawn();
            despawnRoutine = null;
        }

        private void Respawn()
        {
            corpseActive = false;
            respawning = false;
            creatureAnimator?.SetDeadState(false);
            corpseDespawnEndTime = 0f;
            respawnEndTime = 0f;
            lastDamageSource = null;
            currentTarget = null;
            leashState.Reset(homePosition);
            waitingAtRoamPoint = false;
            nextRoamDecisionTime = 0f;
            transform.SetPositionAndRotation(homePosition, homeRotation);
            SetPresentationActive(true);
            configured = false;
            ConfigureFromDefinition(true);

            if (agent != null)
            {
                agent.enabled = true;
                EnsureAgentOnNavMesh();
                if (agent.isOnNavMesh)
                {
                    agent.Warp(homePosition);
                    agent.ResetPath();
                    agent.isStopped = false;
                }
            }

            identity.SetSelectable(true);
            identity.RestoreResources();
        }

        private bool TryResolveLastDamageParticipant(out MMOPlayerParticipant participant)
        {
            participant = default;
            if (lastDamageSource == null || lastDamageSource.Identity == null)
            {
                return false;
            }

            if (MMOGameplaySessionService.Players.TryGetParticipant(lastDamageSource.Identity, out participant))
            {
                return true;
            }

            participant = new MMOPlayerParticipant(
                MMOGameplaySessionService.LocalPlayer.ParticipantId,
                MMOGameplaySessionService.LocalPlayer.CharacterId,
                lastDamageSource.Identity == MMOGameplaySessionService.LocalPlayer.Identity,
                MMOGameplaySessionService.IsHostAuthority,
                lastDamageSource.Identity);
            return participant.IsValid;
        }

        private bool IsValidTarget(MMOCharacterIdentity target)
        {
            if (target == null || target == identity || !MMOFactionRules.CanDamage(identity, target))
            {
                return false;
            }

            MMOCombatant targetCombatant = target.GetComponent<MMOCombatant>();
            return targetCombatant != null && targetCombatant.IsAlive;
        }

        private float GetAttackRange()
        {
            if (identity.Stats != null)
            {
                return identity.Stats.MeleeRange;
            }

            return definition.AutoAttackAbility != null ? definition.AutoAttackAbility.Range : definition.StoppingDistance;
        }

        private float GetMovementSpeedMultiplier()
        {
            return identity != null && identity.Stats != null ? identity.Stats.MovementSpeedMultiplier : 1f;
        }

        private float GetAuthoritativeWorldSpeed()
        {
            if (corpseActive || respawning || combatant == null || !combatant.IsAlive || !CanMoveOnNavMesh())
            {
                return 0f;
            }

            if (agent.isStopped)
            {
                return 0f;
            }

            Vector3 velocity = agent.velocity.sqrMagnitude > 0.0001f ? agent.velocity : agent.desiredVelocity;
            velocity.y = 0f;
            return velocity.magnitude;
        }

        private bool TryGetRandomNavMeshPoint(Vector3 origin, float radius, out Vector3 point)
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector2 offset = UnityEngine.Random.insideUnitCircle * radius;
                Vector3 candidate = origin + new Vector3(offset.x, 0f, offset.y);
                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3f, agent.areaMask))
                {
                    point = hit.position;
                    return true;
                }
            }

            point = origin;
            return false;
        }

        private void EnsureAgentOnNavMesh()
        {
            if (agent == null || !agent.enabled || agent.isOnNavMesh)
            {
                return;
            }

            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, agent.areaMask))
            {
                agent.Warp(hit.position);
            }
        }

        private bool CanMoveOnNavMesh()
        {
            return agent != null && agent.enabled && agent.isOnNavMesh;
        }

        private bool ShouldRepathToTarget(Vector3 targetPosition)
        {
            return Time.time >= nextChaseRepathTime
                || (targetPosition - lastChaseTargetPosition).sqrMagnitude >= chaseRepathDistance * chaseRepathDistance;
        }

        private void StopMoving()
        {
            if (CanMoveOnNavMesh())
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
        }

        private void EnsureReferences()
        {
            if (identity == null)
            {
                identity = GetComponent<MMOCharacterIdentity>();
            }

            if (combatant == null)
            {
                combatant = GetComponent<MMOCombatant>();
            }

            if (abilitySystem == null)
            {
                abilitySystem = GetComponent<MMOAbilitySystem>();
            }

            if (autoAttackController == null)
            {
                autoAttackController = GetComponent<MMOAutoAttackController>();
            }

            if (creatureAnimator == null)
            {
                creatureAnimator = GetComponent<MMOCreatureAnimator>();
            }

            if (agent == null)
            {
                agent = GetComponent<NavMeshAgent>();
            }

            if (lootableCorpse == null)
            {
                lootableCorpse = GetComponent<MMOLootableCorpse>();
                if (lootableCorpse == null)
                {
                    lootableCorpse = gameObject.AddComponent<MMOLootableCorpse>();
                }
            }
        }

        private MMOCharacterIdentity ResolveSnapshotTarget(EnemySnapshot snapshot)
        {
            if (snapshot == null
                || !snapshot.inCombat
                || string.IsNullOrWhiteSpace(snapshot.currentTargetCharacterId)
                || !MMOGameplaySessionService.Players.TryGetParticipantByCharacterId(
                    snapshot.currentTargetCharacterId,
                    out MMOPlayerParticipant participant))
            {
                return null;
            }

            return participant.Identity;
        }

        private void RefreshAuthorityMode()
        {
            bool isAuthority = IsAuthorityOwner;
            if (authorityModeInitialized && lastAuthorityMode == isAuthority)
            {
                return;
            }

            authorityModeInitialized = true;
            lastAuthorityMode = isAuthority;
            SetAuthorityEventSubscriptions(isAuthority);

            if (agent == null)
            {
                return;
            }

            if (!isAuthority)
            {
                if (agent.enabled && agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                    agent.ResetPath();
                }

                agent.enabled = false;
                return;
            }

            proxyHasSnapshot = false;
            proxyWorldSpeed = 0f;
            lastAppliedSnapshotUtcTicks = 0;
            if (!corpseActive && !respawning && combatant != null && combatant.IsAlive)
            {
                agent.enabled = true;
                EnsureAgentOnNavMesh();
            }
        }

        private void SetAuthorityEventSubscriptions(bool subscribe)
        {
            if (combatant == null || authorityEventsSubscribed == subscribe)
            {
                return;
            }

            combatant.Damaged -= OnDamaged;
            combatant.DamageDealt -= OnDamageDealt;
            combatant.Died -= OnDied;
            if (subscribe)
            {
                combatant.Damaged += OnDamaged;
                combatant.DamageDealt += OnDamageDealt;
                combatant.Died += OnDied;
            }

            authorityEventsSubscribed = subscribe;
        }

        private void EnsureSpawnId()
        {
            if (!string.IsNullOrWhiteSpace(stableSpawnId))
            {
                return;
            }

            string sceneKey = gameObject.scene.IsValid()
                ? string.IsNullOrWhiteSpace(gameObject.scene.path) ? gameObject.scene.name : gameObject.scene.path
                : "runtime";
            stableSpawnId = $"{sceneKey}:{BuildHierarchyPath(transform)}";
        }

        private static string BuildHierarchyPath(Transform current)
        {
            if (current == null)
            {
                return string.Empty;
            }

            Stack<string> segments = new();
            Transform walker = current;
            while (walker != null)
            {
                int siblingIndex = walker.GetSiblingIndex();
                segments.Push($"{walker.name}[{siblingIndex}]");
                walker = walker.parent;
            }

            return string.Join("/", segments);
        }

        private void RegisterSpawn()
        {
            EnsureSpawnId();
            if (!string.IsNullOrWhiteSpace(stableSpawnId))
            {
                ActiveEnemiesBySpawnId[stableSpawnId] = this;
            }
        }

        private void UnregisterSpawn()
        {
            if (!string.IsNullOrWhiteSpace(stableSpawnId)
                && ActiveEnemiesBySpawnId.TryGetValue(stableSpawnId, out MMOEnemyController registered)
                && registered == this)
            {
                ActiveEnemiesBySpawnId.Remove(stableSpawnId);
            }
        }

        private void CachePresentationComponents()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);
        }

        private void SetPresentationActive(bool active)
        {
            renderers ??= GetComponentsInChildren<Renderer>(true);
            colliders ??= GetComponentsInChildren<Collider>(true);

            foreach (Renderer cachedRenderer in renderers)
            {
                if (cachedRenderer != null)
                {
                    cachedRenderer.enabled = active;
                }
            }

            foreach (Collider cachedCollider in colliders)
            {
                if (cachedCollider != null)
                {
                    cachedCollider.enabled = active;
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawDebugGizmos || definition == null)
            {
                return;
            }

            Vector3 origin = Application.isPlaying ? homePosition : transform.position;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(origin, definition.RoamRadius);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, definition.AggroRadius);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(origin, definition.LeashRadius);
            if (Application.isPlaying && leashState.Phase == MMOEnemyLeashPhase.Engaged)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(leashState.AnchorPosition, definition.LeashRadius);
            }
        }
    }
}
