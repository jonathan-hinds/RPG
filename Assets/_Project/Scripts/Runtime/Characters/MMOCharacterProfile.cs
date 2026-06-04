using UnityEngine;

namespace RPGClone.Characters
{
    [CreateAssetMenu(menuName = "RPG Clone/Characters/Character Profile", fileName = "CharacterProfile")]
    public sealed class MMOCharacterProfile : ScriptableObject
    {
        [SerializeField] private string displayName = "Adventurer";
        [SerializeField, Min(1)] private int level = 1;
        [SerializeField] private Sprite portrait;
        [SerializeField] private Color portraitTint = Color.white;
        [SerializeField, Min(1)] private int maxHealth = 100;
        [SerializeField, Min(0)] private int maxMana = 100;
        [SerializeField] private MMOEntityFaction faction = MMOEntityFaction.Neutral;
        [SerializeField] private MMOCharacterStats baseStats = new();
        [SerializeField] private bool selectable = true;

        public string DisplayName => displayName;
        public int Level => level;
        public Sprite Portrait => portrait;
        public Color PortraitTint => portraitTint;
        public int MaxHealth => maxHealth;
        public int MaxMana => maxMana;
        public MMOEntityFaction Faction => faction;
        public MMOCharacterStats BaseStats => baseStats;
        public bool Selectable => selectable;

        public void Configure(
            string newDisplayName,
            int newLevel,
            int newMaxHealth,
            int newMaxMana,
            Color newPortraitTint,
            Sprite newPortrait = null,
            bool newSelectable = true,
            MMOEntityFaction newFaction = MMOEntityFaction.Neutral,
            MMOCharacterStats newBaseStats = null)
        {
            displayName = string.IsNullOrWhiteSpace(newDisplayName) ? name : newDisplayName;
            level = Mathf.Max(1, newLevel);
            maxHealth = Mathf.Max(1, newMaxHealth);
            maxMana = Mathf.Max(0, newMaxMana);
            portraitTint = newPortraitTint;
            portrait = newPortrait;
            selectable = newSelectable;
            faction = newFaction;
            baseStats ??= new MMOCharacterStats();
            if (newBaseStats != null)
            {
                baseStats.CopyFrom(newBaseStats);
            }
        }
    }
}
