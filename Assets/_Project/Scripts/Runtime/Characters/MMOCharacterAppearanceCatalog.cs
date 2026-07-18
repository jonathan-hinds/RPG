using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPGClone.Characters
{
    [Serializable]
    public sealed class MMOHairstyleDefinition
    {
        [SerializeField] private string hairstyleId = "hair_1";
        [SerializeField] private string displayName = "Hairstyle 1";
        [SerializeField] private GameObject modelPrefab;

        public string HairstyleId => hairstyleId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? hairstyleId : displayName;
        public GameObject ModelPrefab => modelPrefab;

        public void Configure(string newHairstyleId, string newDisplayName, GameObject newModelPrefab)
        {
            hairstyleId = string.IsNullOrWhiteSpace(newHairstyleId) ? "hair_1" : newHairstyleId.Trim();
            displayName = string.IsNullOrWhiteSpace(newDisplayName) ? hairstyleId : newDisplayName.Trim();
            modelPrefab = newModelPrefab;
        }
    }

    [CreateAssetMenu(menuName = "RPG Clone/Characters/Appearance Catalog", fileName = "CharacterAppearanceCatalog")]
    public sealed class MMOCharacterAppearanceCatalog : ScriptableObject
    {
        [SerializeField] private List<MMOHairstyleDefinition> hairstyles = new();

        public IReadOnlyList<MMOHairstyleDefinition> Hairstyles => hairstyles;
        public string DefaultHairstyleId => hairstyles.Count > 0 && hairstyles[0] != null
            ? hairstyles[0].HairstyleId
            : "hair_1";

        public MMOHairstyleDefinition FindHairstyle(string hairstyleId)
        {
            if (string.IsNullOrWhiteSpace(hairstyleId))
            {
                return null;
            }

            foreach (MMOHairstyleDefinition hairstyle in hairstyles)
            {
                if (hairstyle != null && string.Equals(hairstyle.HairstyleId, hairstyleId, StringComparison.Ordinal))
                {
                    return hairstyle;
                }
            }

            return null;
        }

        public string NormalizeHairstyleId(string hairstyleId)
        {
            MMOHairstyleDefinition hairstyle = FindHairstyle(hairstyleId);
            return hairstyle != null ? hairstyle.HairstyleId : DefaultHairstyleId;
        }

        public int IndexOfHairstyle(string hairstyleId)
        {
            for (int i = 0; i < hairstyles.Count; i++)
            {
                if (hairstyles[i] != null && string.Equals(hairstyles[i].HairstyleId, hairstyleId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return hairstyles.Count > 0 ? 0 : -1;
        }

        public void Configure(IEnumerable<MMOHairstyleDefinition> newHairstyles)
        {
            hairstyles = newHairstyles != null
                ? new List<MMOHairstyleDefinition>(newHairstyles)
                : new List<MMOHairstyleDefinition>();
        }
    }
}
