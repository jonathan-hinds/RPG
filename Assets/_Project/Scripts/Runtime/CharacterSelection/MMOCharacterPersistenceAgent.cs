using System;
using System.Threading.Tasks;
using RPGClone.Abilities;
using RPGClone.Characters;
using RPGClone.Combat;
using RPGClone.Inventory;
using RPGClone.Quests;
using RPGClone.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RPGClone.CharacterSelection
{
    [RequireComponent(typeof(MMOCharacterIdentity))]
    public sealed class MMOCharacterPersistenceAgent : MonoBehaviour
    {
        [SerializeField] private MMOCharacterArchetypeCatalog archetypeCatalog;
        [SerializeField] private MMOItemCatalog itemCatalog;
        [SerializeField] private MMOAbilityCatalog abilityCatalog;
        [SerializeField] private bool useCloudSave = true;

        private MMOCharacterIdentity identity;
        private MMOExperienceComponent experience;
        private MMOInventoryContainer inventory;
        private MMOCharacterEquipment equipment;
        private MMOWeaponSkillController weaponSkills;
        private MMOCurrencyWallet wallet;
        private MMOQuestLog questLog;
        private MMOAbilitySystem abilitySystem;
        private MMOActionBarPresenter actionBar;
        private MMOCharacterRosterRepository repository;
        private bool appliedSession;
        private bool isQuittingOrUnloading;

        private void Awake()
        {
            identity = GetComponent<MMOCharacterIdentity>();
            experience = GetComponent<MMOExperienceComponent>();
            inventory = GetComponent<MMOInventoryContainer>();
            equipment = GetComponent<MMOCharacterEquipment>();
            weaponSkills = GetComponent<MMOWeaponSkillController>();
            wallet = GetComponent<MMOCurrencyWallet>();
            questLog = GetComponent<MMOQuestLog>();
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

        public void SetItemCatalog(MMOItemCatalog catalog)
        {
            itemCatalog = catalog;
        }

        public void SetAbilityCatalog(MMOAbilityCatalog catalog)
        {
            abilityCatalog = catalog;
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

            ApplyInventory(saveData);
            ApplyEquipment(saveData);
            ApplyWeaponSkills(saveData, archetype);
            ApplyWallet(saveData);
            ApplyLearnedAbilities(saveData);
            ApplyActionBar(saveData);
            ApplyQuests(saveData);

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
            saveData.copper = wallet != null ? wallet.Copper : 0;

            if (experience != null)
            {
                saveData.currentExperience = experience.CurrentExperience;
                saveData.totalExperienceEarned = experience.TotalExperienceEarned;
            }

            CaptureInventory(saveData);
            CaptureEquipment(saveData);
            CaptureWeaponSkills(saveData);
            CaptureLearnedAbilities(saveData);
            CaptureActionBar(saveData);
            CaptureQuests(saveData);
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
            destination.copper = source.copper;
            destination.inventory = new System.Collections.Generic.List<MMOInventorySlotSaveData>(source.inventory ?? new System.Collections.Generic.List<MMOInventorySlotSaveData>());
            destination.equipment = new System.Collections.Generic.List<MMOEquipmentSlotSaveData>(source.equipment ?? new System.Collections.Generic.List<MMOEquipmentSlotSaveData>());
            destination.weaponSkills = new System.Collections.Generic.List<MMOWeaponSkillSaveEntry>(source.weaponSkills ?? new System.Collections.Generic.List<MMOWeaponSkillSaveEntry>());
            destination.learnedAbilityIds = new System.Collections.Generic.List<string>(source.learnedAbilityIds ?? new System.Collections.Generic.List<string>());
            destination.actionBarSlots = new System.Collections.Generic.List<MMOActionBarSlotSaveData>(source.actionBarSlots ?? new System.Collections.Generic.List<MMOActionBarSlotSaveData>());
            destination.activeQuests = new System.Collections.Generic.List<MMOQuestStateSaveData>(source.activeQuests ?? new System.Collections.Generic.List<MMOQuestStateSaveData>());
            destination.completedQuestIds = new System.Collections.Generic.List<string>(source.completedQuestIds ?? new System.Collections.Generic.List<string>());
            destination.pendingUsableItemId = source.pendingUsableItemId;
        }

        private void CaptureInventory(MMOCharacterSaveData saveData)
        {
            saveData.inventory ??= new System.Collections.Generic.List<MMOInventorySlotSaveData>();
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

        private void CaptureEquipment(MMOCharacterSaveData saveData)
        {
            saveData.equipment ??= new System.Collections.Generic.List<MMOEquipmentSlotSaveData>();
            saveData.equipment.Clear();
            if (equipment == null)
            {
                return;
            }

            foreach (MMOEquippedItemSlot equippedItem in equipment.EquippedItems)
            {
                if (equippedItem?.Item == null)
                {
                    continue;
                }

                saveData.equipment.Add(new MMOEquipmentSlotSaveData
                {
                    slotType = equippedItem.SlotType,
                    itemId = equippedItem.Item.ItemId
                });
            }
        }

        private void CaptureWeaponSkills(MMOCharacterSaveData saveData)
        {
            saveData.weaponSkills ??= new System.Collections.Generic.List<MMOWeaponSkillSaveEntry>();
            saveData.weaponSkills.Clear();
            if (weaponSkills == null)
            {
                return;
            }

            foreach (MMOWeaponSkillEntry entry in weaponSkills.WeaponSkills)
            {
                if (entry == null || entry.WeaponType == MMOWeaponType.None)
                {
                    continue;
                }

                saveData.weaponSkills.Add(new MMOWeaponSkillSaveEntry
                {
                    weaponType = entry.WeaponType,
                    skillValue = entry.SkillValue
                });
            }
        }

        private void CaptureLearnedAbilities(MMOCharacterSaveData saveData)
        {
            saveData.learnedAbilityIds ??= new System.Collections.Generic.List<string>();
            saveData.learnedAbilityIds.Clear();
            if (abilitySystem == null)
            {
                return;
            }

            foreach (MMOAbilityDefinition ability in abilitySystem.KnownAbilities)
            {
                if (ability != null && !saveData.learnedAbilityIds.Contains(ability.AbilityId))
                {
                    saveData.learnedAbilityIds.Add(ability.AbilityId);
                }
            }
        }

        private void CaptureActionBar(MMOCharacterSaveData saveData)
        {
            saveData.actionBarSlots ??= new System.Collections.Generic.List<MMOActionBarSlotSaveData>();
            saveData.actionBarSlots.Clear();

            MMOActionBarPresenter presenter = ResolveActionBar();
            if (presenter == null)
            {
                return;
            }

            for (int i = 0; i < presenter.Slots.Count; i++)
            {
                MMOActionBarSlot slot = presenter.Slots[i];
                if (slot == null)
                {
                    continue;
                }

                saveData.actionBarSlots.Add(new MMOActionBarSlotSaveData
                {
                    slotIndex = i,
                    bindingType = slot.bindingType,
                    abilityId = slot.ability != null ? slot.ability.AbilityId : null,
                    itemId = slot.item != null ? slot.item.ItemId : null,
                    key = slot.key
                });
            }
        }

        private void CaptureQuests(MMOCharacterSaveData saveData)
        {
            saveData.activeQuests ??= new System.Collections.Generic.List<MMOQuestStateSaveData>();
            saveData.completedQuestIds ??= new System.Collections.Generic.List<string>();
            saveData.activeQuests.Clear();
            saveData.completedQuestIds.Clear();
            saveData.pendingUsableItemId = questLog != null && questLog.PendingUsableItem != null ? questLog.PendingUsableItem.ItemId : null;
            if (questLog == null)
            {
                return;
            }

            foreach (MMOQuestRuntimeState state in questLog.ActiveQuests)
            {
                if (state?.Quest == null)
                {
                    continue;
                }

                saveData.activeQuests.Add(new MMOQuestStateSaveData
                {
                    questId = state.Quest.QuestId,
                    tracked = state.Tracked,
                    objectiveProgress = new System.Collections.Generic.List<int>(state.ObjectiveProgress)
                });
            }

            foreach (MMOQuestDefinition quest in questLog.CompletedQuests)
            {
                if (quest != null)
                {
                    saveData.completedQuestIds.Add(quest.QuestId);
                }
            }
        }

        private void ApplyInventory(MMOCharacterSaveData saveData)
        {
            if (inventory == null)
            {
                return;
            }

            inventory.Clear();
            foreach (MMOInventorySlotSaveData slot in saveData.inventory ?? new System.Collections.Generic.List<MMOInventorySlotSaveData>())
            {
                MMOItemDefinition item = ResolveItem(slot.itemId);
                if (item != null)
                {
                    inventory.SetSlot(slot.slotIndex, item, slot.quantity);
                }
            }
        }

        private void ApplyEquipment(MMOCharacterSaveData saveData)
        {
            if (equipment == null)
            {
                return;
            }

            equipment.EnsureDefaultSlots();
            equipment.ClearEquipment(false);
            foreach (MMOEquipmentSlotSaveData slot in saveData.equipment ?? new System.Collections.Generic.List<MMOEquipmentSlotSaveData>())
            {
                MMOItemDefinition item = ResolveItem(slot.itemId);
                if (item != null)
                {
                    equipment.TryEquip(item);
                }
            }
        }

        private void ApplyWeaponSkills(MMOCharacterSaveData saveData, MMOCharacterArchetypeDefinition archetype)
        {
            if (weaponSkills == null)
            {
                weaponSkills = GetComponent<MMOWeaponSkillController>() ?? gameObject.AddComponent<MMOWeaponSkillController>();
            }

            if (saveData.weaponSkills != null && saveData.weaponSkills.Count > 0)
            {
                weaponSkills.RestoreSkills(saveData.weaponSkills);
                return;
            }

            weaponSkills.SetSkillToCap(MMOWeaponType.Unarmed);
            if (archetype == null)
            {
                return;
            }

            weaponSkills.LearnAtCap(archetype.StartingWeaponSkills);
            foreach (MMOItemDefinition item in archetype.StartingEquipment)
            {
                if (item != null && item.WeaponType != MMOWeaponType.None)
                {
                    weaponSkills.SetSkillToCap(item.WeaponType);
                }
            }
        }

        private void ApplyWallet(MMOCharacterSaveData saveData)
        {
            wallet?.SetCopper(saveData.copper);
        }

        private void ApplyLearnedAbilities(MMOCharacterSaveData saveData)
        {
            if (abilitySystem == null)
            {
                return;
            }

            foreach (string abilityId in saveData.learnedAbilityIds ?? new System.Collections.Generic.List<string>())
            {
                MMOAbilityDefinition ability = ResolveAbility(abilityId);
                if (ability != null)
                {
                    abilitySystem.LearnAbility(ability);
                }
            }

            FillActionBar();
        }

        private void ApplyActionBar(MMOCharacterSaveData saveData)
        {
            MMOActionBarPresenter presenter = ResolveActionBar();
            if (presenter == null)
            {
                return;
            }

            System.Collections.Generic.List<MMOActionBarSlotSaveData> savedSlots = saveData.actionBarSlots ?? new System.Collections.Generic.List<MMOActionBarSlotSaveData>();
            if (savedSlots.Count == 0)
            {
                FillActionBar();
                return;
            }

            int slotCount = MMOActionBarPresenter.DefaultSlotCount;
            foreach (MMOActionBarSlotSaveData savedSlot in savedSlots)
            {
                if (savedSlot != null)
                {
                    slotCount = Mathf.Max(slotCount, savedSlot.slotIndex + 1);
                }
            }

            System.Collections.Generic.List<MMOActionBarSlot> restoredSlots = new(slotCount);
            for (int i = 0; i < slotCount; i++)
            {
                MMOActionBarSlot existingSlot = i < presenter.Slots.Count ? presenter.Slots[i] : null;
                restoredSlots.Add(new MMOActionBarSlot
                {
                    key = existingSlot != null ? existingSlot.key : UnityEngine.InputSystem.Key.None
                });
            }

            foreach (MMOActionBarSlotSaveData savedSlot in savedSlots)
            {
                if (savedSlot == null || savedSlot.slotIndex < 0 || savedSlot.slotIndex >= restoredSlots.Count)
                {
                    continue;
                }

                MMOActionBarSlot slot = restoredSlots[savedSlot.slotIndex];
                slot.key = savedSlot.key;
                if (savedSlot.bindingType == MMOActionBarSlotBindingType.Ability)
                {
                    MMOAbilityDefinition ability = ResolveAbility(savedSlot.abilityId);
                    if (ability != null && abilitySystem != null && !abilitySystem.KnowsAbility(ability))
                    {
                        abilitySystem.LearnAbility(ability);
                    }

                    slot.SetAbility(ability);
                }
                else if (savedSlot.bindingType == MMOActionBarSlotBindingType.Item)
                {
                    slot.SetItem(ResolveItem(savedSlot.itemId));
                }
                else
                {
                    slot.ClearBinding();
                }
            }

            presenter.ApplySlots(restoredSlots);
        }

        private void ApplyQuests(MMOCharacterSaveData saveData)
        {
            if (questLog == null || questLog.QuestCatalog == null)
            {
                return;
            }

            System.Collections.Generic.List<MMOQuestRuntimeState> restoredActive = new();
            foreach (MMOQuestStateSaveData savedState in saveData.activeQuests ?? new System.Collections.Generic.List<MMOQuestStateSaveData>())
            {
                MMOQuestDefinition quest = questLog.QuestCatalog.FindById(savedState.questId);
                if (quest == null)
                {
                    continue;
                }

                MMOQuestRuntimeState runtimeState = new(quest);
                runtimeState.SetTracked(savedState.tracked);
                System.Collections.Generic.List<int> progress = savedState.objectiveProgress ?? new System.Collections.Generic.List<int>();
                for (int i = 0; i < progress.Count; i++)
                {
                    runtimeState.SetProgress(i, progress[i]);
                }

                restoredActive.Add(runtimeState);
            }

            System.Collections.Generic.List<MMOQuestDefinition> restoredCompleted = new();
            foreach (string questId in saveData.completedQuestIds ?? new System.Collections.Generic.List<string>())
            {
                MMOQuestDefinition quest = questLog.QuestCatalog.FindById(questId);
                if (quest != null && !restoredCompleted.Contains(quest))
                {
                    restoredCompleted.Add(quest);
                }
            }

            questLog.RestoreState(restoredActive, restoredCompleted, ResolveItem(saveData.pendingUsableItemId));
        }

        private MMOItemDefinition ResolveItem(string itemId)
        {
            MMOItemDefinition item = itemCatalog != null ? itemCatalog.FindById(itemId) : null;
#if UNITY_EDITOR
            if (item == null && !string.IsNullOrWhiteSpace(itemId))
            {
                string[] guids = AssetDatabase.FindAssets("t:MMOItemDefinition");
                foreach (string guid in guids)
                {
                    MMOItemDefinition candidate = AssetDatabase.LoadAssetAtPath<MMOItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                    if (candidate != null && candidate.ItemId == itemId)
                    {
                        return candidate;
                    }
                }
            }
#endif
            return item;
        }

        private MMOAbilityDefinition ResolveAbility(string abilityId)
        {
            MMOAbilityDefinition ability = abilityCatalog != null ? abilityCatalog.FindById(abilityId) : null;
#if UNITY_EDITOR
            if (ability == null && !string.IsNullOrWhiteSpace(abilityId))
            {
                string[] guids = AssetDatabase.FindAssets("t:MMOAbilityDefinition");
                foreach (string guid in guids)
                {
                    MMOAbilityDefinition candidate = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                    if (candidate != null && candidate.AbilityId == abilityId)
                    {
                        return candidate;
                    }
                }
            }
#endif
            return ability;
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
            ResolveActionBar()?.FillEmptySlotsFromKnownAbilities();
        }

        private MMOActionBarPresenter ResolveActionBar()
        {
            if (actionBar == null)
            {
                actionBar = FindAnyObjectByType<MMOActionBarPresenter>();
            }

            return actionBar;
        }
    }
}
