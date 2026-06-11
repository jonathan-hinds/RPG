using System.Collections.Generic;
using RPGClone.Abilities;
using RPGClone.Inventory;
using UnityEngine;

namespace RPGClone.Characters
{
    [CreateAssetMenu(menuName = "RPG Clone/Characters/Character Archetype", fileName = "CharacterArchetype")]
    public sealed class MMOCharacterArchetypeDefinition : ScriptableObject
    {
        [SerializeField] private MMOPlayableRace race;
        [SerializeField] private MMOPlayableClass characterClass;
        [SerializeField] private string displayName = "Orc Warrior";
        [SerializeField, TextArea] private string raceDescription;
        [SerializeField, TextArea] private string classDescription;
        [SerializeField] private Color modelTint = Color.white;
        [SerializeField] private MMOCharacterProfile startingProfile;
        [SerializeField] private MMOLevelProgressionDefinition progression;
        [SerializeField] private MMOAbilityDefinition racialAbility;
        [SerializeField] private MMOAbilityDefinition classAbility;
        [SerializeField] private List<MMOAbilityDefinition> startingAbilities = new();
        [Header("Starting Items")]
        [SerializeField] private List<MMOItemStack> startingInventoryItems = new();
        [SerializeField] private List<MMOItemDefinition> startingEquipment = new();
        [SerializeField] private List<MMOWeaponType> startingWeaponSkills = new();

        public MMOPlayableRace Race => race;
        public MMOPlayableClass CharacterClass => characterClass;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? $"{race} {characterClass}" : displayName;
        public string RaceDescription => raceDescription;
        public string ClassDescription => classDescription;
        public Color ModelTint => modelTint;
        public MMOCharacterProfile StartingProfile => startingProfile;
        public MMOLevelProgressionDefinition Progression => progression;
        public MMOAbilityDefinition RacialAbility => racialAbility;
        public MMOAbilityDefinition ClassAbility => classAbility;
        public IReadOnlyList<MMOAbilityDefinition> StartingAbilities => startingAbilities;
        public IReadOnlyList<MMOItemStack> StartingInventoryItems => startingInventoryItems;
        public IReadOnlyList<MMOItemDefinition> StartingEquipment => startingEquipment;
        public IReadOnlyList<MMOWeaponType> StartingWeaponSkills => startingWeaponSkills;

        public void Configure(
            MMOPlayableRace newRace,
            MMOPlayableClass newClass,
            string newDisplayName,
            string newRaceDescription,
            string newClassDescription,
            Color newModelTint,
            MMOCharacterProfile newStartingProfile,
            MMOLevelProgressionDefinition newProgression,
            MMOAbilityDefinition newRacialAbility,
            MMOAbilityDefinition newClassAbility,
            IEnumerable<MMOAbilityDefinition> newStartingAbilities)
        {
            race = newRace;
            characterClass = newClass;
            displayName = string.IsNullOrWhiteSpace(newDisplayName) ? $"{race} {characterClass}" : newDisplayName;
            raceDescription = newRaceDescription;
            classDescription = newClassDescription;
            modelTint = newModelTint;
            startingProfile = newStartingProfile;
            progression = newProgression;
            racialAbility = newRacialAbility;
            classAbility = newClassAbility;
            startingAbilities = newStartingAbilities != null
                ? new List<MMOAbilityDefinition>(newStartingAbilities)
                : new List<MMOAbilityDefinition>();
        }

        public void ConfigureStartingItems(
            IEnumerable<MMOItemStack> newStartingInventoryItems,
            IEnumerable<MMOItemDefinition> newStartingEquipment,
            IEnumerable<MMOWeaponType> newStartingWeaponSkills)
        {
            startingInventoryItems = newStartingInventoryItems != null
                ? new List<MMOItemStack>(newStartingInventoryItems)
                : new List<MMOItemStack>();
            startingEquipment = newStartingEquipment != null
                ? new List<MMOItemDefinition>(newStartingEquipment)
                : new List<MMOItemDefinition>();
            startingWeaponSkills = newStartingWeaponSkills != null
                ? new List<MMOWeaponType>(newStartingWeaponSkills)
                : new List<MMOWeaponType>();
        }
    }
}
