using System;
using System.Collections.Generic;
using RPGClone.Characters;
using RPGClone.Inventory;
using RPGClone.UI;
using UnityEngine;
using UnityEngine.InputSystem;

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
        public int copper;
        public List<MMOInventorySlotSaveData> inventory = new();
        public List<MMOEquipmentSlotSaveData> equipment = new();
        public List<string> learnedAbilityIds = new();
        public List<MMOActionBarSlotSaveData> actionBarSlots = new();
        public List<MMOQuestStateSaveData> activeQuests = new();
        public List<string> completedQuestIds = new();
        public string pendingUsableItemId;

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
    public sealed class MMOEquipmentSlotSaveData
    {
        public MMOEquipmentSlotType slotType;
        public string itemId;
    }

    [Serializable]
    public sealed class MMOActionBarSlotSaveData
    {
        public int slotIndex;
        public MMOActionBarSlotBindingType bindingType;
        public string abilityId;
        public string itemId;
        public Key key = Key.None;
    }

    [Serializable]
    public sealed class MMOQuestStateSaveData
    {
        public string questId;
        public bool tracked = true;
        public List<int> objectiveProgress = new();
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
