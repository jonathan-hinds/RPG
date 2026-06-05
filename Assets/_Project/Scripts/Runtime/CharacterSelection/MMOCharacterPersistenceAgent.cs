using System;
using System.Threading.Tasks;
using RPGClone.Abilities;
using RPGClone.Characters;
using RPGClone.Inventory;
using RPGClone.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RPGClone.CharacterSelection
{
    [RequireComponent(typeof(MMOCharacterIdentity))]
    public sealed class MMOCharacterPersistenceAgent : MonoBehaviour
    {
        [SerializeField] private MMOCharacterArchetypeCatalog archetypeCatalog;
        [SerializeField] private bool useCloudSave = true;

        private MMOCharacterIdentity identity;
        private MMOExperienceComponent experience;
        private MMOInventoryContainer inventory;
        private MMOAbilitySystem abilitySystem;
        private MMOCharacterRosterRepository repository;
        private bool appliedSession;
        private bool isQuittingOrUnloading;

        private void Awake()
        {
            identity = GetComponent<MMOCharacterIdentity>();
            experience = GetComponent<MMOExperienceComponent>();
            inventory = GetComponent<MMOInventoryContainer>();
            abilitySystem = GetComponent<MMOAbilitySystem>();
            repository = useCloudSave ? new MMOCloudCharacterRosterRepository() : new MMOLocalCharacterRosterRepository();
        }

        private void Start()
        {
            ApplySelectedCharacter();
        }

        private void OnApplicationQuit()
        {
            isQuittingOrUnloading = true;
            _ = SaveCurrentCharacterAsync();
        }

        private void OnDisable()
        {
            if (!gameObject.scene.isLoaded)
            {
                return;
            }

            isQuittingOrUnloading = true;
            _ = SaveCurrentCharacterAsync();
        }

        public void SetArchetypeCatalog(MMOCharacterArchetypeCatalog catalog)
        {
            archetypeCatalog = catalog;
        }

        public void ApplySelectedCharacter()
        {
            if (appliedSession || !MMOCharacterSession.HasSelectedCharacter)
            {
                return;
            }

            appliedSession = true;
            MMOCharacterSaveData saveData = MMOCharacterSession.SelectedCharacter;
            MMOCharacterArchetypeDefinition archetype = archetypeCatalog != null
                ? archetypeCatalog.Find(saveData.race, saveData.characterClass)
                : null;

            if (archetype != null)
            {
                identity.Configure(archetype.StartingProfile, saveData.DisplayName, true);
                MMOCharacterCustomization customization = GetComponent<MMOCharacterCustomization>() ?? gameObject.AddComponent<MMOCharacterCustomization>();
                customization.Configure(saveData.race, saveData.characterClass);
                ApplyProgression(archetype, saveData.level);
                LearnArchetypeAbilities(archetype);
                FillActionBar();
                if (experience != null)
                {
                    experience.SetProgression(archetype.Progression);
                    experience.SetExperienceState(saveData.currentExperience, saveData.totalExperienceEarned);
                }
            }
            else
            {
                identity.SetDisplayName(saveData.DisplayName);
                identity.SetLevel(saveData.level);
            }

            if (saveData.currentHealth > 0)
            {
                identity.Health.SetCurrent(saveData.currentHealth);
            }

            if (saveData.currentMana > 0)
            {
                identity.Mana.SetCurrent(saveData.currentMana);
            }

            Vector3 savedPosition = saveData.position.ToVector3();
            if (savedPosition != Vector3.zero)
            {
                transform.SetPositionAndRotation(savedPosition, Quaternion.Euler(saveData.rotationEuler.ToVector3()));
            }
        }

        public async Task SaveCurrentCharacterAsync()
        {
            if (!MMOCharacterSession.HasSelectedCharacter || repository == null || identity == null)
            {
                return;
            }

            try
            {
                MMOCharacterSaveData capturedData = CaptureCurrentCharacterData();
                MMOCharacterSaveData selected = MMOCharacterSession.SelectedCharacter;
                MMOCharacterRosterSaveData roster = await repository.LoadAsync();
                MMOCharacterSaveData saveData = roster.characters.Find(character => character.characterId == selected.characterId);
                if (saveData == null)
                {
                    saveData = selected;
                    roster.characters.Add(saveData);
                }

                CopyCapturedData(capturedData, saveData);
                await repository.SaveAsync(roster);
                if (!isQuittingOrUnloading)
                {
                    MMOCharacterSession.Select(saveData);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Failed to save active character. {exception.Message}");
            }
        }

        private MMOCharacterSaveData CaptureCurrentCharacterData()
        {
            MMOCharacterSaveData saveData = new();
            Capture(saveData);
            return saveData;
        }

        private void Capture(MMOCharacterSaveData saveData)
        {
            saveData.characterName = identity.DisplayName;
            MMOCharacterCustomization customization = GetComponent<MMOCharacterCustomization>();
            if (customization != null)
            {
                saveData.race = customization.Race;
                saveData.characterClass = customization.CharacterClass;
            }

            saveData.level = identity.Level;
            saveData.currentHealth = identity.Health.CurrentValue;
            saveData.currentMana = identity.Mana.CurrentValue;
            saveData.sceneName = SceneManager.GetActiveScene().name;
            saveData.position = new Vector3SaveData(transform.position);
            saveData.rotationEuler = new Vector3SaveData(transform.eulerAngles);

            if (experience != null)
            {
                saveData.currentExperience = experience.CurrentExperience;
                saveData.totalExperienceEarned = experience.TotalExperienceEarned;
            }

            CaptureInventory(saveData);
        }

        private static void CopyCapturedData(MMOCharacterSaveData source, MMOCharacterSaveData destination)
        {
            destination.characterName = source.characterName;
            destination.race = source.race;
            destination.characterClass = source.characterClass;
            destination.level = source.level;
            destination.currentExperience = source.currentExperience;
            destination.totalExperienceEarned = source.totalExperienceEarned;
            destination.currentHealth = source.currentHealth;
            destination.currentMana = source.currentMana;
            destination.sceneName = source.sceneName;
            destination.position = source.position;
            destination.rotationEuler = source.rotationEuler;
            destination.inventory = new System.Collections.Generic.List<MMOInventorySlotSaveData>(source.inventory);
        }

        private void CaptureInventory(MMOCharacterSaveData saveData)
        {
            saveData.inventory.Clear();
            if (inventory == null)
            {
                return;
            }

            for (int i = 0; i < inventory.Slots.Count; i++)
            {
                MMOItemStack stack = inventory.Slots[i];
                if (stack == null || stack.IsEmpty)
                {
                    continue;
                }

                saveData.inventory.Add(new MMOInventorySlotSaveData
                {
                    slotIndex = i,
                    itemId = stack.Item.ItemId,
                    quantity = stack.Quantity
                });
            }
        }

        private void ApplyProgression(MMOCharacterArchetypeDefinition archetype, int savedLevel)
        {
            identity.SetLevel(1);
            for (int level = 2; level <= Mathf.Max(1, savedLevel); level++)
            {
                identity.SetLevel(level);
                identity.ApplyStatGrowth(archetype.Progression != null ? archetype.Progression.GetStatGainsForLevel(level) : null, false);
            }

            identity.RestoreResources();
        }

        private void LearnArchetypeAbilities(MMOCharacterArchetypeDefinition archetype)
        {
            if (abilitySystem == null || archetype == null)
            {
                return;
            }

            foreach (MMOAbilityDefinition ability in archetype.StartingAbilities)
            {
                abilitySystem.LearnAbility(ability);
            }

            abilitySystem.LearnAbility(archetype.RacialAbility);
            abilitySystem.LearnAbility(archetype.ClassAbility);
        }

        private void FillActionBar()
        {
            MMOActionBarPresenter actionBar = FindAnyObjectByType<MMOActionBarPresenter>();
            actionBar?.FillEmptySlotsFromKnownAbilities();
        }
    }
}
