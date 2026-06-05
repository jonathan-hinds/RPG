using System;
using UnityEngine;

namespace RPGClone.Characters
{
    public sealed class MMOCharacterIdentity : MonoBehaviour
    {
        [SerializeField] private MMOCharacterProfile profile;
        [SerializeField] private string displayName = "Adventurer";
        [SerializeField, Min(1)] private int level = 1;
        [SerializeField] private Sprite portrait;
        [SerializeField] private Color portraitTint = Color.white;
        [SerializeField] private MMOEntityFaction faction = MMOEntityFaction.Neutral;
        [SerializeField] private bool selectable = true;
        [SerializeField] private MMOCharacterStats stats = new();
        [SerializeField] private MMOCharacterResource health = new(100);
        [SerializeField] private MMOCharacterResource mana = new(100);

        public event Action<MMOCharacterIdentity> Changed;

        public MMOCharacterProfile Profile => profile;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? gameObject.name : displayName;
        public int Level => level;
        public Sprite Portrait => portrait;
        public Color PortraitTint => portraitTint;
        public MMOEntityFaction Faction => faction;
        public bool Selectable => selectable;
        public MMOCharacterStats Stats => stats;
        public MMOCharacterResource Health => health;
        public MMOCharacterResource Mana => mana;

        private void OnEnable()
        {
            health.Changed += OnResourceChanged;
            mana.Changed += OnResourceChanged;
        }

        private void OnDisable()
        {
            health.Changed -= OnResourceChanged;
            mana.Changed -= OnResourceChanged;
        }

        private void Awake()
        {
            ClampValues();
        }

        private void OnValidate()
        {
            ClampValues();
        }

        public void Configure(MMOCharacterProfile newProfile, string displayNameOverride = null, bool resetResources = true)
        {
            profile = newProfile;
            if (profile != null)
            {
                displayName = string.IsNullOrWhiteSpace(displayNameOverride) ? profile.DisplayName : displayNameOverride;
                level = profile.Level;
                portrait = profile.Portrait;
                portraitTint = profile.PortraitTint;
                faction = profile.Faction;
                selectable = profile.Selectable;
                stats ??= new MMOCharacterStats();
                stats.CopyFrom(profile.BaseStats);

                int maxHealth = CalculateMaxHealth(profile.MaxHealth);
                int maxMana = CalculateMaxMana(profile.MaxMana);
                int healthCurrent = resetResources ? maxHealth : health.CurrentValue;
                int manaCurrent = resetResources ? maxMana : mana.CurrentValue;
                health.Configure(maxHealth, healthCurrent, false);
                mana.Configure(maxMana, manaCurrent, false);
            }
            else if (!string.IsNullOrWhiteSpace(displayNameOverride))
            {
                displayName = displayNameOverride;
            }

            ClampValues();
            Changed?.Invoke(this);
        }

        public void SetDisplayName(string newDisplayName)
        {
            displayName = string.IsNullOrWhiteSpace(newDisplayName) ? gameObject.name : newDisplayName;
            Changed?.Invoke(this);
        }

        public void SetLevel(int newLevel)
        {
            int clampedLevel = Mathf.Max(1, newLevel);
            if (level == clampedLevel)
            {
                return;
            }

            level = clampedLevel;
            Changed?.Invoke(this);
        }

        public void ApplyStatGains(MMOCharacterStats statGains, bool restoreResources)
        {
            stats ??= new MMOCharacterStats();
            stats.Add(statGains);
            RecalculateResourceMaximums(restoreResources);
            Changed?.Invoke(this);
        }

        public void RemoveStatGains(MMOCharacterStats statGains, bool restoreResources)
        {
            stats ??= new MMOCharacterStats();
            stats.Subtract(statGains);
            RecalculateResourceMaximums(restoreResources);
            Changed?.Invoke(this);
        }

        public void ApplyStatGrowth(MMOCharacterStatGrowth statGrowth, bool restoreResources)
        {
            stats ??= new MMOCharacterStats();
            statGrowth?.ApplyTo(stats);
            RecalculateResourceMaximums(restoreResources);
            Changed?.Invoke(this);
        }

        public void SetSelectable(bool value)
        {
            if (selectable == value)
            {
                return;
            }

            selectable = value;
            Changed?.Invoke(this);
        }

        public void RestoreResources()
        {
            RecalculateResourceMaximums(false);
            health.SetCurrent(health.MaxValue);
            mana.SetCurrent(mana.MaxValue);
        }

        private void OnResourceChanged()
        {
            Changed?.Invoke(this);
        }

        private void ClampValues()
        {
            level = Mathf.Max(1, level);
            stats ??= new MMOCharacterStats();
            health ??= new MMOCharacterResource(100);
            mana ??= new MMOCharacterResource(100);
            health.Configure(health.MaxValue, health.CurrentValue, false);
            mana.Configure(mana.MaxValue, mana.CurrentValue, false);
        }

        private void RecalculateResourceMaximums(bool restoreResources)
        {
            int maxHealth = CalculateMaxHealth(profile != null ? profile.MaxHealth : health.MaxValue);
            int maxMana = CalculateMaxMana(profile != null ? profile.MaxMana : mana.MaxValue);
            health.Configure(maxHealth, restoreResources ? maxHealth : health.CurrentValue, false);
            mana.Configure(maxMana, restoreResources ? maxMana : mana.CurrentValue, false);
        }

        private int CalculateMaxHealth(int baseValue)
        {
            return Mathf.Max(1, baseValue + (stats != null ? stats.MaxHealthBonus : 0));
        }

        private int CalculateMaxMana(int baseValue)
        {
            return Mathf.Max(0, baseValue + (stats != null ? stats.MaxManaBonus : 0));
        }
    }
}
