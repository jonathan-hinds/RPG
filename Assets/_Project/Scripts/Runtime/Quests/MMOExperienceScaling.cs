using RPGClone.Characters;
using RPGClone.Enemies;
using UnityEngine;

namespace RPGClone.Quests
{
    public enum MMOContentDifficulty
    {
        Gray,
        Green,
        Yellow,
        Orange,
        Red
    }

    public static class MMOExperienceScaling
    {
        private const float GreenQuestExperienceMultiplier = 0.8f;
        private const float GrayQuestExperienceMultiplier = 0.1f;
        private const float HigherLevelMobExperiencePerLevel = 0.05f;
        private const float MaxHigherLevelMobExperienceMultiplier = 1.2f;

        public static MMOContentDifficulty GetDifficulty(int playerLevel, int contentLevel)
        {
            playerLevel = Mathf.Max(1, playerLevel);
            contentLevel = Mathf.Max(1, contentLevel);

            if (contentLevel <= GetGrayLevel(playerLevel))
            {
                return MMOContentDifficulty.Gray;
            }

            int delta = contentLevel - playerLevel;
            if (delta >= 5)
            {
                return MMOContentDifficulty.Red;
            }

            if (delta >= 3)
            {
                return MMOContentDifficulty.Orange;
            }

            if (delta >= -2)
            {
                return MMOContentDifficulty.Yellow;
            }

            return MMOContentDifficulty.Green;
        }

        public static Color GetDifficultyColor(int playerLevel, int contentLevel)
        {
            return GetDifficulty(playerLevel, contentLevel) switch
            {
                MMOContentDifficulty.Gray => new Color(0.58f, 0.58f, 0.58f, 1f),
                MMOContentDifficulty.Green => new Color(0.25f, 0.95f, 0.25f, 1f),
                MMOContentDifficulty.Yellow => new Color(1f, 0.84f, 0.28f, 1f),
                MMOContentDifficulty.Orange => new Color(1f, 0.48f, 0.12f, 1f),
                MMOContentDifficulty.Red => new Color(1f, 0.18f, 0.12f, 1f),
                _ => Color.white
            };
        }

        public static string FormatQuestTitle(MMOQuestDefinition quest)
        {
            return quest == null ? string.Empty : $"[{quest.QuestLevel}] {quest.DisplayName}";
        }

        public static string FormatRichQuestTitle(MMOQuestDefinition quest, int playerLevel)
        {
            if (quest == null)
            {
                return string.Empty;
            }

            string color = ColorUtility.ToHtmlStringRGB(GetDifficultyColor(playerLevel, quest.QuestLevel));
            return $"<color=#{color}>{FormatQuestTitle(quest)}</color>";
        }

        public static int CalculateQuestExperience(int baseExperience, int playerLevel, int questLevel)
        {
            baseExperience = Mathf.Max(0, baseExperience);
            if (baseExperience == 0)
            {
                return 0;
            }

            MMOContentDifficulty difficulty = GetDifficulty(playerLevel, questLevel);
            float multiplier = difficulty switch
            {
                MMOContentDifficulty.Gray => GrayQuestExperienceMultiplier,
                MMOContentDifficulty.Green => GreenQuestExperienceMultiplier,
                _ => 1f
            };

            return Mathf.Max(0, Mathf.RoundToInt(baseExperience * multiplier));
        }

        public static int CalculateMobExperience(MMOEnemyDefinition enemy, MMOCharacterIdentity player)
        {
            if (enemy == null || player == null || enemy.ExperienceReward <= 0)
            {
                return 0;
            }

            int playerLevel = Mathf.Max(1, player.Level);
            int enemyLevel = Mathf.Max(1, enemy.Level);
            if (enemyLevel <= GetGrayLevel(playerLevel))
            {
                return 0;
            }

            float multiplier = 1f;
            if (enemyLevel < playerLevel)
            {
                int levelDifference = playerLevel - enemyLevel;
                multiplier = 1f - (levelDifference / (float)GetZeroDifference(playerLevel));
            }
            else if (enemyLevel > playerLevel)
            {
                int levelDifference = enemyLevel - playerLevel;
                multiplier = Mathf.Min(MaxHigherLevelMobExperienceMultiplier, 1f + levelDifference * HigherLevelMobExperiencePerLevel);
            }

            return Mathf.Max(0, Mathf.RoundToInt(enemy.ExperienceReward * Mathf.Max(0f, multiplier)));
        }

        private static int GetGrayLevel(int playerLevel)
        {
            if (playerLevel <= 5)
            {
                return 0;
            }

            return Mathf.Max(0, playerLevel - 5 - playerLevel / 10);
        }

        private static int GetZeroDifference(int playerLevel)
        {
            if (playerLevel <= 7)
            {
                return 5;
            }

            if (playerLevel <= 9)
            {
                return 6;
            }

            if (playerLevel <= 11)
            {
                return 7;
            }

            if (playerLevel <= 15)
            {
                return 8;
            }

            if (playerLevel <= 19)
            {
                return 9;
            }

            if (playerLevel <= 29)
            {
                return 11;
            }

            if (playerLevel <= 39)
            {
                return 12;
            }

            if (playerLevel <= 44)
            {
                return 13;
            }

            if (playerLevel <= 49)
            {
                return 14;
            }

            if (playerLevel <= 54)
            {
                return 15;
            }

            if (playerLevel <= 59)
            {
                return 16;
            }

            return 17;
        }
    }
}
