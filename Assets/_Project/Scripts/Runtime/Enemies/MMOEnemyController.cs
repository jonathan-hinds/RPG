using System.Collections;
using System.Collections.Generic;
using RPGClone.Abilities;
using RPGClone.Characters;
using RPGClone.Combat;
using RPGClone.Inventory;
using RPGClone.Loot;
using RPGClone.Quests;
using UnityEngine;
using UnityEngine.AI;

namespace RPGClone.Enemies
{
    [RequireComponent(typeof(MMOCharacterIdentity))]
    [RequireComponent(typeof(MMOCombatant))]
    [RequireComponent(typeof(MMOAbilitySystem))]
    [RequireComponent(typeof(MMOAutoAttackController))]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class MMOEnemyController : MonoBehaviour
    {
        private const int DetectionBufferSize = 16;

        [SerializeField] private MMOEnemyDefinition definition;
        [SerializeField] private string displayNameOverride = string.Empty;
        [SerializeField] private LayerMask aggroMask = ~0;
        [SerializeField] private bool resetResourcesOnLeash = true;
        [SerializeField] private bool drawDebugGizmos = true;
        [SerializeField, Min(0.02f)] private float chaseRepathInterval = 0.2f;
        [SerializeField, Min(0.01f)] private float chaseRepathDistance = 0.35f;

        private readonly Collider[] detectionBuffer = new Collider[DetectionBufferSize];
        private MMOCharacterIdentity identity;
        private MMOCombatant combatant;
        private MMOAbilitySystem abilitySystem;
        private MMOAutoAttackController autoAttackController;
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
        private Vector3 lastChaseDestination;
        private Coroutine despawnRoutine;
        private MMOCombatant lastDamageSource;
        private Renderer[] renderers;
        private Collider[] colliders;

        public MMOEnemyDefinition Definition => definition;
        public MMOCharacterIdentity CurrentTarget => currentTarget;
        public bool IsInCombat => currentTarget != null && !corpseActive;

        private void Awake()
        {
            EnsureReferences();
            homePosition = transform.position;
            homeRotation = transform.rotation;
            CachePresentationComponents();
            ConfigureFromDefinition(true);
        }

        private void OnEnable()
        {
            EnsureReferences();
            combatant.Damaged += OnDamaged;
            combatant.Died += OnDied;
        }

        private void OnDisable()
        {
            if (combatant != null)
            {
                combatant.Damaged -= OnDamaged;
                combatant.Died -= OnDied;
            }
        }

        private void Update()
        {
            if (definition == null || corpseActive || !combatant.IsAlive)
            {
                StopMoving();
                return;
            }

            EnsureAgentOnNavMesh();

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

            agent.speed = definition.WalkSpeed;
            agent.stoppingDistance = definition.StoppingDistance;
            agent.autoBraking = true;
            agent.updateRotation = true;

            configured = true;
        }

        private void UpdateCombat()
        {
            if (!IsValidTarget(currentTarget))
            {
                ClearCombat(false);
                return;
            }

            float distanceFromHome = Vector3.Distance(transform.position, homePosition);
            if (distanceFromHome > definition.LeashRadius)
            {
                ClearCombat(true);
                return;
            }

            float attackRange = GetAttackRange();
            float sqrDistance = (currentTarget.transform.position - transform.position).sqrMagnitude;
            bool inAttackRange = sqrDistance <= attackRange * attackRange;

            if (CanMoveOnNavMesh())
            {
                agent.speed = definition.ChaseSpeed;
                agent.stoppingDistance = Mathf.Max(0.05f, attackRange * 0.85f);
                agent.isStopped = inAttackRange;

                if (!inAttackRange && ShouldRepathToTarget(currentTarget.transform.position))
                {
                    lastChaseDestination = currentTarget.transform.position;
                    nextChaseRepathTime = Time.time + chaseRepathInterval;
                    agent.SetDestination(lastChaseDestination);
                }
            }

            if (autoAttackController.CurrentTarget != currentTarget)
            {
                autoAttackController.StartAutoAttack(currentTarget);
            }
        }

        private void UpdateRoaming()
        {
            if (!definition.CanRoam || definition.RoamRadius <= 0f || !CanMoveOnNavMesh())
            {
                StopMoving();
                return;
            }

            agent.speed = definition.WalkSpeed;
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
                nextRoamDecisionTime = Time.time + Random.Range(definition.MinRoamIdleSeconds, definition.MaxRoamIdleSeconds);
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
            if (!IsValidTarget(target))
            {
                return;
            }

            currentTarget = target;
            waitingAtRoamPoint = false;
            nextRoamDecisionTime = 0f;
            nextChaseRepathTime = 0f;
            if (CanMoveOnNavMesh())
            {
                agent.ResetPath();
            }
        }

        private void ClearCombat(bool leashReset)
        {
            currentTarget = null;
            autoAttackController.StopAutoAttack();

            if (resetResourcesOnLeash && leashReset)
            {
                identity.RestoreResources();
            }

            if (CanMoveOnNavMesh())
            {
                waitingAtRoamPoint = false;
                nextChaseRepathTime = 0f;
                agent.speed = definition.WalkSpeed;
                agent.stoppingDistance = 0.15f;
                agent.isStopped = false;
                agent.SetDestination(homePosition);
            }
        }

        private void OnDamaged(MMOCombatant source, MMOCombatant target, MMOAbilityDefinition ability, int amount)
        {
            if (target != combatant || source == null || source.Identity == null)
            {
                return;
            }

            lastDamageSource = source;
            EnterCombat(source.Identity);
        }

        private void OnDied(MMOCombatant deadCombatant)
        {
            if (deadCombatant != combatant)
            {
                return;
            }

            ClearCombat(false);
            StopMoving();
            AwardExperience();
            BecomeCorpse();
        }

        private void AwardExperience()
        {
            if (definition == null || definition.ExperienceReward <= 0 || lastDamageSource == null || lastDamageSource.Identity == null)
            {
                return;
            }

            MMOExperienceComponent experience = lastDamageSource.Identity.GetComponent<MMOExperienceComponent>();
            experience?.AddExperience(definition.ExperienceReward);
        }

        private void BecomeCorpse()
        {
            corpseActive = true;
            identity.SetSelectable(false);
            if (agent != null)
            {
                agent.enabled = false;
            }

            GameObject looter = lastDamageSource != null && lastDamageSource.Identity != null ? lastDamageSource.Identity.gameObject : null;
            List<MMOItemStack> droppedLoot = definition != null && definition.LootTable != null
                ? definition.LootTable.GenerateLoot(looter)
                : new List<MMOItemStack>();

            if (looter != null)
            {
                MMOQuestLog questLog = looter.GetComponent<MMOQuestLog>();
                questLog?.RecordCreatureKilled(definition, definition != null ? definition.name : gameObject.name);
            }

            lootableCorpse.LootEmptied -= OnCorpseLooted;
            lootableCorpse.SetLoot(droppedLoot);
            if (droppedLoot.Count > 0)
            {
                lootableCorpse.LootEmptied += OnCorpseLooted;
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

        private void BeginDespawn(float delaySeconds)
        {
            if (despawnRoutine != null)
            {
                StopCoroutine(despawnRoutine);
            }

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

            float respawnDelay = definition != null ? definition.RespawnSeconds : 30f;
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
            lastDamageSource = null;
            currentTarget = null;
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

        private bool TryGetRandomNavMeshPoint(Vector3 origin, float radius, out Vector3 point)
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector2 offset = Random.insideUnitCircle * radius;
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
                || (targetPosition - lastChaseDestination).sqrMagnitude >= chaseRepathDistance * chaseRepathDistance;
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
        }
    }
}
