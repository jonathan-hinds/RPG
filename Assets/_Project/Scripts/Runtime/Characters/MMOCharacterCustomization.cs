using UnityEngine;

namespace RPGClone.Characters
{
    public sealed class MMOCharacterCustomization : MonoBehaviour
    {
        [SerializeField] private MMOPlayableRace race;
        [SerializeField] private MMOPlayableClass characterClass;

        public MMOPlayableRace Race => race;
        public MMOPlayableClass CharacterClass => characterClass;

        public void Configure(MMOPlayableRace newRace, MMOPlayableClass newClass)
        {
            race = newRace;
            characterClass = newClass;
        }
    }
}
