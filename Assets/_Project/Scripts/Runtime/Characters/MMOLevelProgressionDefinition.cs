using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPGClone.Characters
{
    [CreateAssetMenu(menuName = "RPG Clone/Characters/Level Progression", fileName = "LevelProgression")]
    public sealed class MMOLevelProgressionDefinition : ScriptableObject
    {
        [SerializeField, Min(2)] private int maxLevel = 60;
        [SerializeField, Min(1)] private int baseExperienceRequired = 400;
        [SerializeField, Min(0)] private int experienceAddedPerLevel = 100;
        [SerializeField, Min(1f)] private float experienceGrowthMultiplier = 1.12f;
        [SerializeField] private MMOCharacterStatGrowth defaultStatGainsPerLevel;
        [SerializeField] private List<MMOLevelProgressionOverride> levelOverrides = new();

        public int MaxLevel => Mathf.Max(2, maxLevel);

        public int GetExperienceRequiredForNextLevel(int currentLevel)
        {
            if (currentLevel >= MaxLevel)
            {
                return 0;
            }

            int nextLevel = Mathf.Max(2, currentLevel + 1);
            MMOLevelProgressionOverride levelOverride = FindOverride(nextLevel);
            if (levelOverride != null && levelOverride.ExperienceRequired > 0)
            {
                return levelOverride.ExperienceRequired;
            }

            float grownRequirement = baseExperienceRequired * Mathf.Pow(experienceGrowthMultiplier, nextLevel - 2);
            return Mathf.Max(1, Mathf.RoundToInt(grownRequirement + experienceAddedPerLevel * (nextLevel - 2)));
        }

        public MMOCharacterStatGrowth GetStatGainsForLevel(int newLevel)
        {
            MMOLevelProgressionOverride levelOverride = FindOverride(newLevel);
            return levelOverride != null && levelOverride.StatGains != null
                ? levelOverride.StatGains
                : defaultStatGainsPerLevel;
        }

        public void Configure(
            int newMaxLevel,
            int newBaseExperienceRequired,
            int newExperienceAddedPerLevel,
            float newExperienceGrowthMultiplier,
            MMOCharacterStatGrowth newDefaultStatGainsPerLevel)
        {
            maxLevel = Mathf.Max(2, newMaxLevel);
            baseExperienceRequired = Mathf.Max(1, newBaseExperienceRequired);
            experienceAddedPerLevel = Mathf.Max(0, newExperienceAddedPerLevel);
            experienceGrowthMultiplier = Mathf.Max(1f, newExperienceGrowthMultiplier);
            if (newDefaultStatGainsPerLevel != null)
            {
                defaultStatGainsPerLevel = newDefaultStatGainsPerLevel;
            }
        }

        private MMOLevelProgressionOverride FindOverride(int level)
        {
            foreach (MMOLevelProgressionOverride levelOverride in levelOverrides)
            {
                if (levelOverride != null && levelOverride.Level == level)
                {
                    return levelOverride;
                }
            }

            return null;
        }
    }

    [Serializable]
    public sealed class MMOLevelProgressionOverride
    {
        [SerializeField, Min(2)] private int level = 2;
        [SerializeField, Min(0)] private int experienceRequired;
        [SerializeField] private MMOCharacterStatGrowth statGains;

        public int Level => level;
        public int ExperienceRequired => experienceRequired;
        public MMOCharacterStatGrowth StatGains => statGains;
    }
}
