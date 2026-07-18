using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPGClone.Characters
{
    [Serializable]
    public sealed class MMOHeadStyleDefinition
    {
        [SerializeField] private string headStyleId = "head_1";
        [SerializeField] private string displayName = "Head Style 1";
        [SerializeField] private GameObject modelPrefab;

        public string HeadStyleId => headStyleId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? headStyleId : displayName;
        public GameObject ModelPrefab => modelPrefab;

        public void Configure(string newHeadStyleId, string newDisplayName, GameObject newModelPrefab)
        {
            headStyleId = string.IsNullOrWhiteSpace(newHeadStyleId) ? "head_1" : newHeadStyleId.Trim();
            displayName = string.IsNullOrWhiteSpace(newDisplayName) ? headStyleId : newDisplayName.Trim();
            modelPrefab = newModelPrefab;
        }
    }

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
        [SerializeField] private List<MMOHeadStyleDefinition> headStyles = new();
        [SerializeField] private List<MMOHairstyleDefinition> hairstyles = new();

        public IReadOnlyList<MMOHeadStyleDefinition> HeadStyles => headStyles;
        public IReadOnlyList<MMOHairstyleDefinition> Hairstyles => hairstyles;
        public string DefaultHeadStyleId => headStyles.Count > 0 && headStyles[0] != null
            ? headStyles[0].HeadStyleId
            : "head_1";
        public string DefaultHairstyleId => hairstyles.Count > 0 && hairstyles[0] != null
            ? hairstyles[0].HairstyleId
            : "hair_1";

        public MMOHeadStyleDefinition FindHeadStyle(string headStyleId)
        {
            if (string.IsNullOrWhiteSpace(headStyleId))
            {
                return null;
            }

            foreach (MMOHeadStyleDefinition headStyle in headStyles)
            {
                if (headStyle != null && string.Equals(headStyle.HeadStyleId, headStyleId, StringComparison.Ordinal))
                {
                    return headStyle;
                }
            }

            return null;
        }

        public string NormalizeHeadStyleId(string headStyleId)
        {
            MMOHeadStyleDefinition headStyle = FindHeadStyle(headStyleId);
            return headStyle != null ? headStyle.HeadStyleId : DefaultHeadStyleId;
        }

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

        public void Configure(
            IEnumerable<MMOHeadStyleDefinition> newHeadStyles,
            IEnumerable<MMOHairstyleDefinition> newHairstyles)
        {
            headStyles = newHeadStyles != null
                ? new List<MMOHeadStyleDefinition>(newHeadStyles)
                : new List<MMOHeadStyleDefinition>();
            hairstyles = newHairstyles != null
                ? new List<MMOHairstyleDefinition>(newHairstyles)
                : new List<MMOHairstyleDefinition>();
        }

        public void Configure(IEnumerable<MMOHairstyleDefinition> newHairstyles)
        {
            Configure(headStyles, newHairstyles);
        }
    }
}
