using System;
using System.Collections.Generic;
using RPGClone.Abilities;
using RPGClone.Buffs;
using RPGClone.CharacterSelection;
using RPGClone.Characters;
using RPGClone.Enemies;
using RPGClone.Services;
using UnityEngine;

namespace RPGClone.Combat
{
    [RequireComponent(typeof(MMOCharacterIdentity))]
    public sealed class MMOCombatant : MonoBehaviour
    {
        private static readonly HashSet<MMOCombatant> ActiveCombatantSet = new();

        [SerializeField, Min(0.1f)] private float combatDropDelaySeconds = 5f;

        private readonly HashSet<MMOCombatant> combatOpponents = new();
        private MMOCharacterIdentity identity;
        private float lastCombatActivityTime = float.NegativeInfinity;
        private bool inCombat;

        public static event Action<MMOCombatant> CombatantEnabled;
        public static event Action<MMOCombatant> CombatantDisabled;
        public event Action<MMOCombatant, MMOCombatant, MMOAbilityDefinition, int> Damaged;
        public event Action<MMOCombatant, MMOCombatant, MMOAbilityDefinition, int> CriticallyDamaged;
        public event Action<MMOCombatant, MMOCombatant, MMOAbilityDefinition, int> CriticalDamageDealt;
        public event Action<MMOCombatant, MMOCombatant, MMOAbilityDefinition, int> Healed;
        public event Action<MMOCombatant, MMOCombatant, MMOAbilityDefinition> Missed;
        public event Action<MMOCombatant, MMOCombatant, MMOAbilityDefinition, int> Blocked;
        public event Action<MMOCombatant> Died;
        public event Action<MMOCombatant> CombatActivity;
        public event Action<MMOCombatant, bool> CombatStateChanged;
        public static IReadOnlyCollection<MMOCombatant> ActiveCombatants => ActiveCombatantSet;
        public bool IsInCombat => inCombat;

        public MMOCharacterIdentity Identity
        {
            get
            {
                EnsureInitialized();
                return identity;
            }
        }

        public bool IsAlive
        {
            get
            {
                EnsureInitialized();
                return identity != null && identity.Health.CurrentValue > 0;
            }
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            EnsureInitialized();
            if (ActiveCombatantSet.Add(this))
            {
                CombatantEnabled?.Invoke(this);
            }
        }

        private void OnDisable()
        {
            ForceLeaveCombat();
            if (ActiveCombatantSet.Remove(this))
            {
                CombatantDisabled?.Invoke(this);
            }
        }

        private void Update()
        {
            if (!inCombat)
            {
                return;
            }

            if (!IsAlive)
            {
                ForceLeaveCombat();
                return;
            }

            PruneCombatOpponents();
            if (combatOpponents.Count == 0 && Time.time - lastCombatActivityTime >= combatDropDelaySeconds)
            {
                SetInCombat(false);
            }
        }

        public void ApplyDamage(MMOCombatant source, MMOAbilityDefinition ability, int amount, bool isCritical = false, bool publishToCombatEventStream = true)
        {
            if (!IsAlive || amount <= 0 || !CanReceiveHostileActions())
            {
                return;
            }

            if (isCritical)
            {
                amount = Mathf.Max(1, Mathf.RoundToInt(amount * 2f));
            }

            int mitigatedAmount = CalculatePhysicalMitigation(amount);
            MMOCharacterBuffController buffController = GetComponent<MMOCharacterBuffController>();
            int absorbedAmount = buffController != null ? buffController.AbsorbDamageAsMana(mitigatedAmount) : 0;
            int appliedAmount = Mathf.Max(0, mitigatedAmount - absorbedAmount);
            identity.Health.SetCurrent(identity.Health.CurrentValue - appliedAmount);
            source?.RegisterCombatActivity(this);
            RegisterCombatActivity(source);
            Damaged?.Invoke(source, this, ability, appliedAmount);
            if (isCritical && appliedAmount > 0)
            {
                CriticallyDamaged?.Invoke(source, this, ability, appliedAmount);
                source?.CriticalDamageDealt?.Invoke(source, this, ability, appliedAmount);
            }

            if (publishToCombatEventStream)
            {
                PublishDamageEvent(source, ability, appliedAmount, absorbedAmount, isCritical, identity.Health.CurrentValue <= 0);
            }

            if (identity.Health.CurrentValue <= 0)
            {
                ForceLeaveCombat();
                Died?.Invoke(this);
                if (publishToCombatEventStream)
                {
                    PublishDeathEvent(source, ability);
                }
            }
        }

        public void ApplyResolvedDamage(MMOCombatant source, MMOAbilityDefinition ability, int appliedAmount, bool isCritical = false, bool publishToCombatEventStream = true)
        {
            if (!IsAlive || appliedAmount < 0 || !CanReceiveHostileActions())
            {
                return;
            }

            identity.Health.SetCurrent(identity.Health.CurrentValue - appliedAmount);
            source?.RegisterCombatActivity(this);
            RegisterCombatActivity(source);
            Damaged?.Invoke(source, this, ability, appliedAmount);
            if (isCritical && appliedAmount > 0)
            {
                CriticallyDamaged?.Invoke(source, this, ability, appliedAmount);
                source?.CriticalDamageDealt?.Invoke(source, this, ability, appliedAmount);
            }

            if (publishToCombatEventStream)
            {
                PublishDamageEvent(source, ability, appliedAmount, 0, isCritical, identity.Health.CurrentValue <= 0);
            }

            if (identity.Health.CurrentValue <= 0)
            {
                ForceLeaveCombat();
                Died?.Invoke(this);
                if (publishToCombatEventStream)
                {
                    PublishDeathEvent(source, ability);
                }
            }
        }

        public void NotifyMiss(MMOCombatant source, MMOAbilityDefinition ability, bool publishToCombatEventStream = true)
        {
            if (!CanReceiveHostileActions())
            {
                return;
            }

            source?.RegisterCombatActivity(this);
            RegisterCombatActivity(source);
            Missed?.Invoke(source, this, ability);
            if (publishToCombatEventStream)
            {
                CombatEventRecord record = CreateCombatEvent(CombatEventType.Missed, source, ability);
                MMOCombatEventStream.PublishCombatEvent(record, source, this, ability);
            }
        }

        public void NotifyBlock(MMOCombatant source, MMOAbilityDefinition ability, int blockedAmount, bool publishToCombatEventStream = true)
        {
            if (blockedAmount <= 0 || !CanReceiveHostileActions())
            {
                return;
            }

            source?.RegisterCombatActivity(this);
            RegisterCombatActivity(source);
            Blocked?.Invoke(source, this, ability, blockedAmount);
            if (publishToCombatEventStream)
            {
                CombatEventRecord record = CreateCombatEvent(CombatEventType.Blocked, source, ability);
                record.blockedAmount = blockedAmount;
                MMOCombatEventStream.PublishCombatEvent(record, source, this, ability);
            }
        }

        public void ApplyHeal(MMOCombatant source, MMOAbilityDefinition ability, int amount, bool publishToCombatEventStream = true)
        {
            if (!IsAlive || amount <= 0)
            {
                return;
            }

            int missingHealth = identity.Health.MaxValue - identity.Health.CurrentValue;
            int appliedAmount = Mathf.Min(missingHealth, amount);
            if (appliedAmount <= 0)
            {
                return;
            }

            identity.Health.SetCurrent(identity.Health.CurrentValue + appliedAmount);
            Healed?.Invoke(source, this, ability, appliedAmount);
            if (publishToCombatEventStream)
            {
                MMOCombatEventStream.PublishHealResolved(source, this, ability, appliedAmount);
                CombatEventRecord record = CreateCombatEvent(CombatEventType.HealResolved, source, ability);
                record.healAmount = appliedAmount;
                MMOCombatEventStream.PublishCombatEvent(record, source, this, ability);
            }
        }

        public void RegisterCombatActivity(MMOCombatant opponent = null)
        {
            lastCombatActivityTime = Time.time;
            AddCombatOpponent(opponent);
            SetInCombat(true);
            CombatActivity?.Invoke(this);
        }

        public void EngageCombatWith(MMOCombatant opponent)
        {
            if (opponent == null || opponent == this)
            {
                RegisterCombatActivity();
                return;
            }

            RegisterCombatActivity(opponent);
            opponent.RegisterCombatActivity(this);
        }

        public void DisengageCombatWith(MMOCombatant opponent)
        {
            if (opponent == null)
            {
                return;
            }

            combatOpponents.Remove(opponent);
            opponent.combatOpponents.Remove(this);
            lastCombatActivityTime = Time.time;
            opponent.lastCombatActivityTime = Time.time;
        }

        public void DisengageFromAllCombat()
        {
            ForceLeaveCombat();
        }

        private void AddCombatOpponent(MMOCombatant opponent)
        {
            if (opponent == null || opponent == this || !opponent.IsAlive)
            {
                return;
            }

            combatOpponents.Add(opponent);
        }

        private void PruneCombatOpponents()
        {
            combatOpponents.RemoveWhere(opponent => opponent == null || !opponent.IsAlive || !opponent.isActiveAndEnabled);
        }

        private void ForceLeaveCombat()
        {
            if (combatOpponents.Count > 0)
            {
                foreach (MMOCombatant opponent in combatOpponents)
                {
                    if (opponent != null)
                    {
                        opponent.combatOpponents.Remove(this);
                        if (opponent.combatOpponents.Count == 0)
                        {
                            opponent.SetInCombat(false);
                        }
                    }
                }

                combatOpponents.Clear();
            }

            SetInCombat(false);
        }

        private void SetInCombat(bool value)
        {
            if (inCombat == value)
            {
                return;
            }

            inCombat = value;
            CombatStateChanged?.Invoke(this, inCombat);
        }

        private int CalculatePhysicalMitigation(int amount)
        {
            EnsureInitialized();
            int armor = identity != null && identity.Stats != null ? identity.Stats.Armor : 0;
            if (armor <= 0)
            {
                return amount;
            }

            float reduction = armor / (armor + 400f + 85f * Mathf.Max(1, identity.Level));
            return Mathf.Max(1, Mathf.RoundToInt(amount * (1f - Mathf.Clamp01(reduction))));
        }

        private void PublishDamageEvent(
            MMOCombatant source,
            MMOAbilityDefinition ability,
            int appliedAmount,
            int absorbedAsManaAmount,
            bool isCritical,
            bool killedTarget)
        {
            if (appliedAmount <= 0 && absorbedAsManaAmount <= 0)
            {
                return;
            }

            CombatEventRecord record = CreateCombatEvent(CombatEventType.DamageResolved, source, ability);
            record.damageAmount = appliedAmount;
            record.absorbedAsManaAmount = absorbedAsManaAmount;
            record.isCritical = isCritical;
            record.killedTarget = killedTarget;
            MMOCombatEventStream.PublishCombatEvent(record, source, this, ability);
        }

        private void PublishDeathEvent(MMOCombatant source, MMOAbilityDefinition ability)
        {
            CombatEventRecord record = CreateCombatEvent(CombatEventType.Death, source, ability);
            record.killedTarget = true;
            MMOCombatEventStream.PublishCombatEvent(record, source, this, ability);
        }

        private CombatEventRecord CreateCombatEvent(CombatEventType eventType, MMOCombatant source, MMOAbilityDefinition ability)
        {
            CombatEventRecord record = CombatEventRecord.Create(eventType);
            record.sessionId = MMOGameplaySessionService.SessionId ?? string.Empty;
            record.abilityId = ability != null ? ability.AbilityId : string.Empty;
            record.targetPosition = new Vector3SaveData(transform.position);
            PopulateTargetResourceSnapshot(record, this);
            PopulateParticipantIds(record, source, this);
            return record;
        }

        private static void PopulateTargetResourceSnapshot(CombatEventRecord record, MMOCombatant target)
        {
            if (record == null || target == null || target.Identity == null)
            {
                return;
            }

            record.hasTargetResourceSnapshot = true;
            record.targetCurrentHealth = target.Identity.Health.CurrentValue;
            record.targetMaxHealth = target.Identity.Health.MaxValue;
            record.targetCurrentMana = target.Identity.Mana.CurrentValue;
            record.targetMaxMana = target.Identity.Mana.MaxValue;
        }

        private static void PopulateParticipantIds(CombatEventRecord record, MMOCombatant source, MMOCombatant target)
        {
            if (record == null)
            {
                return;
            }

            PopulateEndpoint(source, true, record);
            PopulateEndpoint(target, false, record);
        }

        private static void PopulateEndpoint(MMOCombatant combatant, bool sourceEndpoint, CombatEventRecord record)
        {
            if (combatant == null || combatant.Identity == null)
            {
                return;
            }

            if (MMOGameplaySessionService.Players.TryGetParticipant(combatant.Identity, out MMOPlayerParticipant participant))
            {
                if (sourceEndpoint)
                {
                    record.sourceCharacterId = participant.CharacterId;
                }
                else
                {
                    record.targetCharacterId = participant.CharacterId;
                }
            }

            MMOEnemyController enemy = combatant.GetComponent<MMOEnemyController>();
            if (enemy == null)
            {
                return;
            }

            if (sourceEndpoint)
            {
                record.sourceEnemySpawnId = enemy.SpawnId;
            }
            else
            {
                record.targetEnemySpawnId = enemy.SpawnId;
            }
        }

        private void EnsureInitialized()
        {
            if (identity == null)
            {
                identity = GetComponent<MMOCharacterIdentity>();
            }
        }

        private bool CanReceiveHostileActions()
        {
            IMMOHostileActionReceiver receiver = GetComponent<IMMOHostileActionReceiver>();
            return receiver == null || receiver.CanReceiveHostileActions;
        }
    }
}
