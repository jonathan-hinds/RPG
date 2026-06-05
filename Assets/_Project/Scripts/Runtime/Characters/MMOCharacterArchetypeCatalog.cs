using System.Collections.Generic;
using UnityEngine;

namespace RPGClone.Characters
{
    [CreateAssetMenu(menuName = "RPG Clone/Characters/Character Archetype Catalog", fileName = "CharacterArchetypeCatalog")]
    public sealed class MMOCharacterArchetypeCatalog : ScriptableObject
    {
        [SerializeField] private List<MMOCharacterArchetypeDefinition> archetypes = new();

        public IReadOnlyList<MMOCharacterArchetypeDefinition> Archetypes => archetypes;

        public MMOCharacterArchetypeDefinition Find(MMOPlayableRace race, MMOPlayableClass characterClass)
        {
            foreach (MMOCharacterArchetypeDefinition archetype in archetypes)
            {
                if (archetype != null && archetype.Race == race && archetype.CharacterClass == characterClass)
                {
                    return archetype;
                }
            }

            return null;
        }

        public void Configure(IEnumerable<MMOCharacterArchetypeDefinition> newArchetypes)
        {
            archetypes = newArchetypes != null
                ? new List<MMOCharacterArchetypeDefinition>(newArchetypes)
                : new List<MMOCharacterArchetypeDefinition>();
        }
    }
}
