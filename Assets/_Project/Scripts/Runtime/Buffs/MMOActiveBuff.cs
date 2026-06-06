using UnityEngine;

namespace RPGClone.Buffs
{
    public sealed class MMOActiveBuff
    {
        private readonly float startedAt;
        private readonly float expiresAt;
        private float nextTickAt;
        private int restoredHealth;
        private int restoredMana;

        public string BuffId { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public Sprite Icon { get; }
        public float DurationSeconds { get; }
        public bool BreakOnMovement { get; }
        public int AttackPowerBonus { get; }
        public float AttackPowerMultiplier { get; }
        public float AttackSpeedMultiplier { get; }
        public float HealthRegenMultiplier { get; }
        public float ManaRegenMultiplier { get; }
        public float DamageTakenAsManaPercent { get; }
        public int RestoreHealthTotal { get; }
        public int RestoreManaTotal { get; }
        public float TickSeconds { get; }

        public float RemainingSeconds => Mathf.Max(0f, expiresAt - Time.time);
        public float NormalizedRemaining => DurationSeconds <= 0f ? 0f : Mathf.Clamp01(RemainingSeconds / DurationSeconds);
        public bool IsExpired => DurationSeconds > 0f && Time.time >= expiresAt;
        public bool IsNearExpiry => DurationSeconds > 0f && RemainingSeconds <= Mathf.Min(5f, DurationSeconds * 0.25f);
        public bool HasPeriodicRestore => RestoreHealthTotal > 0 || RestoreManaTotal > 0;
        public bool IsTickReady => HasPeriodicRestore && Time.time >= nextTickAt;

        public MMOActiveBuff(MMOBuffApplication application)
        {
            BuffId = string.IsNullOrWhiteSpace(application.BuffId) ? "buff" : application.BuffId;
            DisplayName = string.IsNullOrWhiteSpace(application.DisplayName) ? BuffId : application.DisplayName;
            Description = application.Description;
            Icon = application.Icon;
            DurationSeconds = Mathf.Max(0.1f, application.DurationSeconds);
            BreakOnMovement = application.BreakOnMovement;
            AttackPowerBonus = Mathf.Max(0, application.AttackPowerBonus);
            AttackPowerMultiplier = Mathf.Max(1f, application.AttackPowerMultiplier);
            AttackSpeedMultiplier = Mathf.Max(1f, application.AttackSpeedMultiplier);
            HealthRegenMultiplier = Mathf.Max(1f, application.HealthRegenMultiplier);
            ManaRegenMultiplier = Mathf.Max(1f, application.ManaRegenMultiplier);
            DamageTakenAsManaPercent = Mathf.Clamp01(application.DamageTakenAsManaPercent);
            RestoreHealthTotal = Mathf.Max(0, application.RestoreHealthTotal);
            RestoreManaTotal = Mathf.Max(0, application.RestoreManaTotal);
            TickSeconds = Mathf.Max(0.1f, application.TickSeconds);
            startedAt = Time.time;
            expiresAt = startedAt + DurationSeconds;
            nextTickAt = startedAt + TickSeconds;
        }

        public int ConsumeHealthTick()
        {
            return ConsumeTickAmount(RestoreHealthTotal, ref restoredHealth);
        }

        public int ConsumeManaTick()
        {
            return ConsumeTickAmount(RestoreManaTotal, ref restoredMana);
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

            float elapsedAfterTick = Mathf.Clamp(Time.time - startedAt + TickSeconds, 0f, DurationSeconds);
            int expectedTotal = Mathf.RoundToInt(totalAmount * (elapsedAfterTick / DurationSeconds));
            int tickAmount = Mathf.Max(0, expectedTotal - restoredAmount);
            restoredAmount += tickAmount;
            return tickAmount;
        }
    }
}
