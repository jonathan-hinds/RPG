using System.Collections.Generic;
using RPGClone.Abilities;
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
    }
}
