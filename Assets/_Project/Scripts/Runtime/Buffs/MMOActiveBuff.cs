using UnityEngine;

namespace RPGClone.Buffs
{
    public sealed class MMOActiveBuff
    {
        private readonly float startedAt;
        private float tickWindowStartedAt;
        private float expiresAt;
        private float nextTickAt;
        private int restoredHealth;
        private int restoredMana;
        private int appliedPeriodicDamage;
        private readonly int periodicDamagePerStackTotal;

        public string BuffId { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public Sprite Icon { get; }
        public float DurationSeconds { get; }
        public bool BreakOnMovement { get; }
        public bool IsHarmful { get; }
        public int AttackPowerBonus { get; }
        public float AttackPowerMultiplier { get; }
        public float AttackSpeedMultiplier { get; }
        public float HealthRegenMultiplier { get; }
        public float ManaRegenMultiplier { get; }
        public float MovementSpeedMultiplier { get; }
        public float DamageTakenAsManaPercent { get; }
        public int RestoreHealthTotal { get; }
        public int RestoreManaTotal { get; }
        public int PeriodicDamageTotal => periodicDamagePerStackTotal * StackCount;
        public float TickSeconds { get; }
        public int MaxStacks { get; }
        public int StackCount { get; private set; }
        public RPGClone.Combat.MMOCombatant Source { get; private set; }
        public RPGClone.Abilities.MMOAbilityDefinition Ability { get; private set; }

        public float RemainingSeconds => Mathf.Max(0f, expiresAt - Time.time);
        public float NormalizedRemaining => DurationSeconds <= 0f ? 0f : Mathf.Clamp01(RemainingSeconds / DurationSeconds);
        public bool IsExpired => DurationSeconds > 0f && Time.time >= expiresAt;
        public bool IsNearExpiry => DurationSeconds > 0f && RemainingSeconds <= Mathf.Min(5f, DurationSeconds * 0.25f);
        public bool HasPeriodicEffect => RestoreHealthTotal > 0 || RestoreManaTotal > 0 || PeriodicDamageTotal > 0;
        public bool IsTickReady => HasPeriodicEffect && Time.time >= nextTickAt;

        public MMOActiveBuff(MMOBuffApplication application)
        {
            BuffId = string.IsNullOrWhiteSpace(application.BuffId) ? "buff" : application.BuffId;
            DisplayName = string.IsNullOrWhiteSpace(application.DisplayName) ? BuffId : application.DisplayName;
            Description = application.Description;
            Icon = application.Icon;
            DurationSeconds = Mathf.Max(0.1f, application.DurationSeconds);
            BreakOnMovement = application.BreakOnMovement;
            IsHarmful = application.IsHarmful;
            AttackPowerBonus = Mathf.Max(0, application.AttackPowerBonus);
            AttackPowerMultiplier = Mathf.Max(0.1f, application.AttackPowerMultiplier);
            AttackSpeedMultiplier = Mathf.Max(0.1f, application.AttackSpeedMultiplier);
            HealthRegenMultiplier = Mathf.Max(0.1f, application.HealthRegenMultiplier);
            ManaRegenMultiplier = Mathf.Max(0.1f, application.ManaRegenMultiplier);
            MovementSpeedMultiplier = Mathf.Max(0.1f, application.MovementSpeedMultiplier);
            DamageTakenAsManaPercent = Mathf.Clamp01(application.DamageTakenAsManaPercent);
            RestoreHealthTotal = Mathf.Max(0, application.RestoreHealthTotal);
            RestoreManaTotal = Mathf.Max(0, application.RestoreManaTotal);
            periodicDamagePerStackTotal = Mathf.Max(0, application.PeriodicDamageTotal);
            TickSeconds = Mathf.Max(0.1f, application.TickSeconds);
            MaxStacks = Mathf.Max(1, application.MaxStacks);
            StackCount = 1;
            Source = application.Source;
            Ability = application.Ability;
            startedAt = Time.time;
            tickWindowStartedAt = startedAt;
            expiresAt = startedAt + DurationSeconds;
            nextTickAt = startedAt + TickSeconds;
        }

        public void RefreshStack(MMOBuffApplication application)
        {
            if (application == null)
            {
                return;
            }

            StackCount = Mathf.Min(MaxStacks, StackCount + 1);
            Source = application.Source;
            Ability = application.Ability;
            tickWindowStartedAt = Time.time;
            expiresAt = Time.time + DurationSeconds;
            nextTickAt = Time.time + TickSeconds;
            appliedPeriodicDamage = 0;
        }

        public int ConsumeHealthTick()
        {
            return ConsumeTickAmount(RestoreHealthTotal, ref restoredHealth);
        }

        public int ConsumeManaTick()
        {
            return ConsumeTickAmount(RestoreManaTotal, ref restoredMana);
        }

        public int ConsumeDamageTick()
        {
            return ConsumeTickAmount(PeriodicDamageTotal, ref appliedPeriodicDamage);
        }

        public void ScheduleNextTick()
        {
            nextTickAt = Time.time + TickSeconds;
        }

        private int ConsumeTickAmount(int totalAmount, ref int restoredAmount)
        {
            if (totalAmount <= 0)
            {
                return 0;
            }

            float elapsedAfterTick = Mathf.Clamp(Time.time - tickWindowStartedAt + TickSeconds, 0f, DurationSeconds);
            int expectedTotal = Mathf.RoundToInt(totalAmount * (elapsedAfterTick / DurationSeconds));
            int tickAmount = Mathf.Max(0, expectedTotal - restoredAmount);
            restoredAmount += tickAmount;
            return tickAmount;
        }
    }
}
