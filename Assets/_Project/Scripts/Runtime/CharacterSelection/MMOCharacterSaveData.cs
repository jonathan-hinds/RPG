using System;
using System.Collections.Generic;
using RPGClone.Characters;
using UnityEngine;

namespace RPGClone.CharacterSelection
{
    [Serializable]
    public sealed class MMOCharacterRosterSaveData
    {
        public List<MMOCharacterSaveData> characters = new();
    }

    [Serializable]
    public sealed class MMOCharacterSaveData
    {
        public string characterId;
        public string characterName;
        public MMOPlayableRace race;
        public MMOPlayableClass characterClass;
        public int level = 1;
        public int currentExperience;
        public int totalExperienceEarned;
        public int currentHealth;
        public int currentMana;
        public string sceneName = "OrcishStarterValley";
        public Vector3SaveData position;
        public Vector3SaveData rotationEuler;
        public List<MMOInventorySlotSaveData> inventory = new();

        public string DisplayName => string.IsNullOrWhiteSpace(characterName) ? $"{race} {characterClass}" : characterName;
    }

    [Serializable]
    public sealed class MMOInventorySlotSaveData
    {
        public int slotIndex;
        public string itemId;
        public int quantity;
    }

    [Serializable]
    public struct Vector3SaveData
    {
        public float x;
        public float y;
        public float z;

        public Vector3SaveData(Vector3 value)
        {
            x = value.x;
            y = value.y;
            z = value.z;
        }

        public Vector3 ToVector3()
        {
            return new Vector3(x, y, z);
        }
    }
}
