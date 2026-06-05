using System;
using UnityEngine;

namespace RPGClone.Characters
{
    [RequireComponent(typeof(MMOCharacterIdentity))]
    public sealed class MMOExperienceComponent : MonoBehaviour
    {
        [SerializeField] private MMOLevelProgressionDefinition progression;
        [SerializeField, Min(0)] private int currentExperience;
        [SerializeField, Min(0)] private int totalExperienceEarned;

        private MMOCharacterIdentity identity;

        public event Action<MMOExperienceComponent> Changed;
        public event Action<MMOExperienceComponent, int> ExperienceGained;
        public event Action<MMOExperienceComponent, int> LeveledUp;

        public int CurrentExperience => currentExperience;
        public int TotalExperienceEarned => totalExperienceEarned;
        public int ExperienceToNextLevel => progression != null && Identity.Level < progression.MaxLevel
            ? progression.GetExperienceRequiredForNextLevel(Identity.Level)
            : 0;
        public bool IsAtMaxLevel => progression != null && Identity.Level >= progression.MaxLevel;
        public MMOLevelProgressionDefinition Progression => progression;
        public MMOCharacterIdentity Identity
        {
            get
            {
                EnsureReferences();
                return identity;
            }
        }

        private void Awake()
        {
            EnsureReferences();
        }

        public void SetProgression(MMOLevelProgressionDefinition newProgression)
        {
            progression = newProgression;
            Changed?.Invoke(this);
        }

        public void SetExperienceState(int newCurrentExperience, int newTotalExperienceEarned)
        {
            currentExperience = Mathf.Max(0, newCurrentExperience);
            totalExperienceEarned = Mathf.Max(currentExperience, newTotalExperienceEarned);
            if (IsAtMaxLevel)
            {
                currentExperience = 0;
            }

            Changed?.Invoke(this);
        }

        public void AddExperience(int amount)
        {
            EnsureReferences();
            if (amount <= 0 || identity == null || progression == null || IsAtMaxLevel)
            {
                return;
            }

            currentExperience += amount;
            totalExperienceEarned += amount;
            ExperienceGained?.Invoke(this, amount);

            while (!IsAtMaxLevel)
            {
                int requiredExperience = ExperienceToNextLevel;
                if (requiredExperience <= 0 || currentExperience < requiredExperience)
                {
                    break;
                }

                currentExperience -= requiredExperience;
                int newLevel = identity.Level + 1;
                identity.SetLevel(newLevel);
                identity.ApplyStatGrowth(progression.GetStatGainsForLevel(newLevel), true);
                LeveledUp?.Invoke(this, newLevel);
            }

            if (IsAtMaxLevel)
            {
                currentExperience = 0;
            }

            Changed?.Invoke(this);
        }

        private void EnsureReferences()
        {
            if (identity == null)
            {
                identity = GetComponent<MMOCharacterIdentity>();
            }
        }
    }
}
