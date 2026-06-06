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

        public void Configure(
            string newDisplayName,
            int newLevel,
            Sprite newPortrait,
            Color newPortraitTint,
            MMOEntityFaction newFaction,
            bool newSelectable,
            MMOCharacterStats newStats,
            int newMaxHealth,
            int newMaxMana,
            bool resetResources = true)
        {
            profile = null;
            displayName = string.IsNullOrWhiteSpace(newDisplayName) ? gameObject.name : newDisplayName;
            level = Mathf.Max(1, newLevel);
            portrait = newPortrait;
            portraitTint = newPortraitTint;
            faction = newFaction;
            selectable = newSelectable;
            stats ??= new MMOCharacterStats();
            if (newStats != null)
            {
                stats.CopyFrom(newStats);
            }

            int maxHealth = CalculateMaxHealth(Mathf.Max(1, newMaxHealth));
            int maxMana = CalculateMaxMana(Mathf.Max(0, newMaxMana));
            health.Configure(maxHealth, resetResources ? maxHealth : health.CurrentValue, false);
            mana.Configure(maxMana, resetResources ? maxMana : mana.CurrentValue, false);
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

    public enum MMONpcIdentityRole
    {
        Friendly,
        QuestGiver,
        Vendor,
        Trainer
    }

    [RequireComponent(typeof(MMOCharacterIdentity))]
    public sealed class MMOStandardNpcIdentity : MonoBehaviour
    {
        [SerializeField] private MMOCharacterProfile profile;
        [SerializeField] private MMONpcIdentityRole role = MMONpcIdentityRole.Friendly;
        [SerializeField] private string displayNameOverride;
        [SerializeField] private string titleOverride;
        [SerializeField] private bool applyOnAwake = true;

        private MMOCharacterIdentity identity;

        public MMOCharacterIdentity Identity
        {
            get
            {
                EnsureReference();
                return identity;
            }
        }

        public MMOCharacterProfile Profile => profile;
        public MMONpcIdentityRole Role => role;
        public string DisplayName => string.IsNullOrWhiteSpace(displayNameOverride) ? gameObject.name : displayNameOverride;
        public string Title => string.IsNullOrWhiteSpace(titleOverride) ? MMONpcIdentityStandards.GetDefaultTitle(role) : titleOverride;

        private void Awake()
        {
            if (applyOnAwake)
            {
                Apply(true);
            }
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                EnsureReference();
            }
        }

        public void Configure(MMOCharacterProfile newProfile, string newDisplayName, MMONpcIdentityRole newRole, bool resetResources)
        {
            Configure(newProfile, newDisplayName, MMONpcIdentityStandards.GetDefaultTitle(newRole), newRole, resetResources);
        }

        public void Configure(MMOCharacterProfile newProfile, string newDisplayName, string newTitle, MMONpcIdentityRole newRole, bool resetResources)
        {
            profile = newProfile;
            displayNameOverride = string.IsNullOrWhiteSpace(newDisplayName) ? gameObject.name : newDisplayName;
            titleOverride = newTitle;
            role = newRole;
            Apply(resetResources);
        }

        public void SetDisplayName(string newDisplayName, bool resetResources = false)
        {
            displayNameOverride = string.IsNullOrWhiteSpace(newDisplayName) ? gameObject.name : newDisplayName;
            Apply(resetResources);
        }

        public void SetTitle(string newTitle)
        {
            titleOverride = newTitle;
        }

        public void Apply(bool resetResources)
        {
            EnsureReference();
            MMONpcIdentityStandards.Apply(identity, profile, DisplayName, role, resetResources);
        }

        private void EnsureReference()
        {
            if (identity == null)
            {
                identity = GetComponent<MMOCharacterIdentity>();
            }
        }
    }

    public static class MMONpcIdentityStandards
    {
        private static readonly Color FriendlyPortraitTint = new(0.95f, 0.66f, 0.22f, 1f);

        public static void Apply(
            MMOCharacterIdentity identity,
            MMOCharacterProfile profile,
            string displayName,
            MMONpcIdentityRole role,
            bool resetResources)
        {
            if (identity == null)
            {
                return;
            }

            if (profile != null)
            {
                identity.Configure(profile, displayName, resetResources);
                return;
            }

            identity.Configure(
                displayName,
                GetDefaultLevel(role),
                null,
                GetDefaultPortraitTint(role),
                GetDefaultFaction(role),
                true,
                CreateDefaultStats(role),
                GetDefaultMaxHealth(role),
                GetDefaultMaxMana(role),
                resetResources);
        }

        private static int GetDefaultLevel(MMONpcIdentityRole role)
        {
            return role switch
            {
                MMONpcIdentityRole.Vendor => 4,
                MMONpcIdentityRole.Trainer => 5,
                MMONpcIdentityRole.QuestGiver => 4,
                _ => 4
            };
        }

        public static string GetDefaultTitle(MMONpcIdentityRole role)
        {
            return role switch
            {
                MMONpcIdentityRole.Vendor => "General Goods Merchant",
                MMONpcIdentityRole.Trainer => "Class Trainer",
                _ => string.Empty
            };
        }

        private static int GetDefaultMaxHealth(MMONpcIdentityRole role)
        {
            return 120;
        }

        private static int GetDefaultMaxMana(MMONpcIdentityRole role)
        {
            return 90;
        }

        private static Color GetDefaultPortraitTint(MMONpcIdentityRole role)
        {
            return FriendlyPortraitTint;
        }

        private static MMOEntityFaction GetDefaultFaction(MMONpcIdentityRole role)
        {
            return MMOEntityFaction.Friendly;
        }

        private static MMOCharacterStats CreateDefaultStats(MMONpcIdentityRole role)
        {
            MMOCharacterStats stats = new();
            stats.Configure(12, 9, 12, 11, 10, 12, 8, 4, 3f, 6f, 2.2f, 3f);
            return stats;
        }
    }
}
