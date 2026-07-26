using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RPGClone.Abilities;
using RPGClone.Characters;
using RPGClone.Combat;
using RPGClone.Inventory;
using RPGClone.Multiplayer;
using RPGClone.Quests;
using RPGClone.Services;
using RPGClone.Social;
using RPGClone.Trainers;
using RPGClone.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RPGClone.CharacterSelection
{
    [RequireComponent(typeof(MMOCharacterIdentity))]
    public sealed class MMOCharacterPersistenceAgent : MonoBehaviour
    {
        private const float CloudAutosaveSeconds = 30f;

        [SerializeField] private MMOCharacterArchetypeCatalog archetypeCatalog;
        [SerializeField] private MMOItemCatalog itemCatalog;
        [SerializeField] private MMOAbilityCatalog abilityCatalog;
        [SerializeField] private MMOCharacterAppearanceCatalog appearanceCatalog;
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
        private float nextAccountHeartbeatTime;
        private float nextCloudAutosaveTime;
        private bool cloudSaveInProgress;
        private bool isRemoteSessionReplica;

        public bool IsRemoteSessionReplica => isRemoteSessionReplica;

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
            repository = new MMOCloudCharacterRosterRepository();
        }

        private void Start()
        {
            if (isRemoteSessionReplica)
            {
                return;
            }

            ApplySelectedCharacter();
            RegisterLocalSessionPlayer();
            EnsureSharedSessionReplicator();
        }

        private void Update()
        {
            if (isRemoteSessionReplica)
            {
                return;
            }

            if (!MMOSocialIdentityService.IsAuthenticated)
            {
                return;
            }

            if (Time.unscaledTime >= nextAccountHeartbeatTime)
            {
                nextAccountHeartbeatTime = Time.unscaledTime + 5f;
                MMOServiceResult heartbeat = MMOSocialIdentityService.Heartbeat();
                if (!heartbeat.Succeeded)
                {
                    Debug.LogWarning($"Account session ended while in world. {heartbeat.Message}");
                    MMOSocialIdentityService.Logout();
                    return;
                }
            }

            if (Time.unscaledTime >= nextCloudAutosaveTime)
            {
                nextCloudAutosaveTime = Time.unscaledTime + CloudAutosaveSeconds;
                _ = SaveCurrentCharacterAsync();
            }
        }

        private void OnApplicationQuit()
        {
            isQuittingOrUnloading = true;
            _ = SaveCurrentCharacterAsync();
        }

        private void OnDisable()
        {
            if (isRemoteSessionReplica)
            {
                return;
            }

            if (!gameObject.scene.isLoaded)
            {
                return;
            }

            isQuittingOrUnloading = true;
            _ = SaveCurrentCharacterAsync();
            MMOGameplaySessionService.UnregisterLocalPlayer(gameObject);
        }

        public void MarkAsRemoteSessionReplica()
        {
            isRemoteSessionReplica = true;
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

        public void SetAppearanceCatalog(MMOCharacterAppearanceCatalog catalog)
        {
            appearanceCatalog = catalog;
        }

        public void ApplySelectedCharacter()
        {
            if (isRemoteSessionReplica || appliedSession || !MMOCharacterSession.HasSelectedCharacter)
            {
                return;
            }

            appliedSession = true;
            MMOCharacterSaveData saveData = MMOCharacterSession.SelectedCharacter;
            MMOCharacterArchetypeDefinition archetype = archetypeCatalog != null
                ? archetypeCatalog.Find(saveData.race, saveData.characterClass)
                : null;
            MMOCharacterCustomization customization = GetComponent<MMOCharacterCustomization>() ?? gameObject.AddComponent<MMOCharacterCustomization>();
            customization.Configure(saveData.race, saveData.characterClass);

            if (archetype != null)
            {
                identity.Configure(archetype.StartingProfile, saveData.DisplayName, true);
                ApplyProgression(archetype, saveData.level);
                ApplyOwnedAbilities(saveData, archetype);
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
                ApplyOwnedAbilities(saveData, null);
            }

            ApplyCharacterVisuals(saveData.race, archetype);
            ApplyCharacterAppearance(saveData);
            ApplyInventory(saveData);
            ApplyEquipment(saveData);
            ApplyWeaponSkills(saveData, archetype);
            ApplyWallet(saveData);
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

            RegisterLocalSessionPlayer();
        }

        public async Task SaveCurrentCharacterAsync()
        {
            if (cloudSaveInProgress
                || isRemoteSessionReplica
                || !MMOCharacterSession.HasSelectedCharacter
                || repository == null
                || identity == null)
            {
                return;
            }

            GameObject localPlayerObject = MMOGameplaySessionService.LocalPlayer.PlayerObject;
            if (localPlayerObject != null && localPlayerObject != gameObject)
            {
                return;
            }

            cloudSaveInProgress = true;
            try
            {
                MMOCharacterSaveData selected = MMOCharacterSession.SelectedCharacter;
                if (!IsOwnedByCurrentAccount(selected))
                {
                    Debug.LogWarning($"Skipped saving character {selected.characterId}; selected character belongs to a different account.");
                    return;
                }

                MMOCharacterSaveData capturedData = CaptureCurrentCharacterData();
                MMOCharacterRosterSaveData roster = await repository.LoadAsync();
                roster.characters ??= new List<MMOCharacterSaveData>();
                roster.characters.RemoveAll(character => character == null || !IsOwnedByCurrentAccount(character));
                MMOCharacterSaveData saveData = roster.characters.Find(character =>
                    character != null
                    && character.characterId == selected.characterId
                    && IsOwnedByCurrentAccount(character));
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
            finally
            {
                cloudSaveInProgress = false;
            }
        }

        public MMOCharacterSaveData CaptureCurrentCharacterData()
        {
            if (isRemoteSessionReplica)
            {
                return new MMOCharacterSaveData();
            }

            MMOCharacterSaveData saveData = new();
            Capture(saveData);
            return saveData;
        }

        public void ApplySessionReplica(MMOCharacterSaveData saveData, bool includeInventory)
        {
            if (saveData == null)
            {
                return;
            }

            MarkAsRemoteSessionReplica();
            MMOCharacterArchetypeDefinition archetype = archetypeCatalog != null
                ? archetypeCatalog.Find(saveData.race, saveData.characterClass)
                : null;
            MMOCharacterCustomization customization = GetComponent<MMOCharacterCustomization>() ?? gameObject.AddComponent<MMOCharacterCustomization>();
            customization.Configure(saveData.race, saveData.characterClass);

            if (archetype != null)
            {
                identity.Configure(archetype.StartingProfile, saveData.DisplayName, true);
                ApplyProgression(archetype, saveData.level);
                ApplyOwnedAbilities(saveData, archetype);
            }
            else
            {
                identity.SetDisplayName(saveData.DisplayName);
                identity.SetLevel(saveData.level);
                ApplyOwnedAbilities(saveData, null);
            }

            ApplyCharacterVisuals(saveData.race, archetype);
            ApplyCharacterAppearance(saveData);
            if (includeInventory)
            {
                ApplyInventory(saveData);
            }

            ApplyEquipment(saveData);
            ApplyWeaponSkills(saveData, archetype);

            if (saveData.currentHealth > 0)
            {
                identity.Health.SetCurrent(saveData.currentHealth);
            }

            if (saveData.currentMana > 0)
            {
                identity.Mana.SetCurrent(saveData.currentMana);
            }
        }

        private void Capture(MMOCharacterSaveData saveData)
        {
            if (MMOCharacterSession.HasSelectedCharacter)
            {
                CopyCanonicalIdentity(MMOCharacterSession.SelectedCharacter, saveData);
            }
            else
            {
                saveData.characterName = identity.DisplayName;
                saveData.normalizedCharacterName = MMOCharacterNameUtility.NormalizeLookupName(identity.DisplayName);
            }

            MMOCharacterCustomization customization = GetComponent<MMOCharacterCustomization>();
            if (customization != null)
            {
                saveData.race = customization.Race;
                saveData.characterClass = customization.CharacterClass;
            }

            MMOCharacterAppearanceVisuals appearanceVisuals = GetComponent<MMOCharacterAppearanceVisuals>();
            if (appearanceVisuals != null)
            {
                saveData.headStyleId = appearanceVisuals.HeadStyleId;
                saveData.faceId = appearanceVisuals.FaceId;
                saveData.hairstyleId = appearanceVisuals.HairstyleId;
                saveData.hairColorId = appearanceVisuals.HairColorId;
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

        private void RegisterLocalSessionPlayer()
        {
            string characterId = MMOCharacterSession.HasSelectedCharacter
                ? MMOCharacterSession.SelectedCharacter.characterId
                : string.Empty;
            MMOGameplaySessionService.RegisterLocalPlayer(gameObject, characterId);
        }

        private void EnsureSharedSessionReplicator()
        {
            if (GetComponent<MMOSharedSessionReplicator>() == null)
            {
                gameObject.AddComponent<MMOSharedSessionReplicator>();
            }
        }

        private static void CopyCapturedData(MMOCharacterSaveData source, MMOCharacterSaveData destination)
        {
            destination.race = source.race;
            destination.characterClass = source.characterClass;
            destination.headStyleId = source.headStyleId;
            destination.faceId = source.faceId;
            destination.hairstyleId = source.hairstyleId;
            destination.hairColorId = source.hairColorId;
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
            destination.equippedBagItemIds = new System.Collections.Generic.List<string>(source.equippedBagItemIds ?? new System.Collections.Generic.List<string>());
            destination.equipment = new System.Collections.Generic.List<MMOEquipmentSlotSaveData>(source.equipment ?? new System.Collections.Generic.List<MMOEquipmentSlotSaveData>());
            destination.weaponSkills = new System.Collections.Generic.List<MMOWeaponSkillSaveEntry>(source.weaponSkills ?? new System.Collections.Generic.List<MMOWeaponSkillSaveEntry>());
            destination.learnedAbilityIds = new System.Collections.Generic.List<string>(source.learnedAbilityIds ?? new System.Collections.Generic.List<string>());
            destination.actionBarSlots = new System.Collections.Generic.List<MMOActionBarSlotSaveData>(source.actionBarSlots ?? new System.Collections.Generic.List<MMOActionBarSlotSaveData>());
            destination.activeQuests = new System.Collections.Generic.List<MMOQuestStateSaveData>(source.activeQuests ?? new System.Collections.Generic.List<MMOQuestStateSaveData>());
            destination.completedQuestIds = new System.Collections.Generic.List<string>(source.completedQuestIds ?? new System.Collections.Generic.List<string>());
            destination.pendingUsableItemId = source.pendingUsableItemId;
        }

        private static void CopyCanonicalIdentity(MMOCharacterSaveData source, MMOCharacterSaveData destination)
        {
            if (source == null || destination == null)
            {
                return;
            }

            destination.characterId = source.characterId;
            destination.accountId = MMOSocialIdentityService.AccountId;
            destination.headStyleId = source.headStyleId;
            destination.faceId = source.faceId;
            destination.hairstyleId = source.hairstyleId;
            destination.hairColorId = source.hairColorId;
            if (MMOCharacterNameUtility.TryValidate(
                    source.characterName,
                    out string displayName,
                    out string normalizedName,
                    out _))
            {
                destination.characterName = displayName;
                destination.normalizedCharacterName = normalizedName;
                return;
            }

            destination.characterName = MMOCharacterNameUtility.CreateFallbackName($"{source.race}{source.characterClass}", source.characterId);
            destination.normalizedCharacterName = MMOCharacterNameUtility.NormalizeLookupName(destination.characterName);
        }

        private static bool IsOwnedByCurrentAccount(MMOCharacterSaveData character)
        {
            if (character == null)
            {
                return false;
            }

            string accountId = MMOSocialIdentityService.AccountId;
            return !string.IsNullOrWhiteSpace(accountId)
                && string.Equals(character.accountId, accountId, StringComparison.Ordinal);
        }

        private void CaptureInventory(MMOCharacterSaveData saveData)
        {
            saveData.inventory ??= new System.Collections.Generic.List<MMOInventorySlotSaveData>();
            saveData.inventory.Clear();
            saveData.equippedBagItemIds ??= new System.Collections.Generic.List<string>();
            saveData.equippedBagItemIds.Clear();
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

            for (int i = 0; i < inventory.BagSlotCount; i++)
            {
                MMOItemDefinition bag = inventory.GetEquippedBag(i);
                saveData.equippedBagItemIds.Add(bag != null ? bag.ItemId : null);
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

            System.Collections.Generic.List<MMOItemDefinition> equippedBags = new();
            foreach (string itemId in saveData.equippedBagItemIds ?? new System.Collections.Generic.List<string>())
            {
                MMOItemDefinition bag = ResolveItem(itemId);
                equippedBags.Add(bag != null && bag.IsContainer ? bag : null);
            }

            inventory.RestoreEquippedBags(equippedBags);
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

        public MMOAbilityDefinition FindAbilityById(string abilityId)
        {
            return ResolveAbility(abilityId);
        }

        private void ApplyOwnedAbilities(MMOCharacterSaveData saveData, MMOCharacterArchetypeDefinition archetype)
        {
            if (abilitySystem == null)
            {
                return;
            }

            List<MMOAbilityDefinition> ownedAbilities = new();
            AddOwnedAbility(ownedAbilities, ResolveAbility("auto_attack"));
            if (archetype != null)
            {
                foreach (MMOAbilityDefinition ability in archetype.StartingAbilities)
                {
                    AddOwnedAbility(ownedAbilities, ability);
                }

                AddOwnedAbility(ownedAbilities, archetype.RacialAbility);
                AddOwnedAbility(ownedAbilities, archetype.ClassAbility);
            }

            foreach (string abilityId in saveData.learnedAbilityIds ?? new System.Collections.Generic.List<string>())
            {
                MMOAbilityDefinition ability = ResolveAbility(abilityId);
                if (IsAbilityAllowedForCharacter(ability, archetype))
                {
                    AddOwnedAbility(ownedAbilities, ability);
                }
            }

            abilitySystem.ReplaceKnownAbilities(ownedAbilities);
        }

        private static void AddOwnedAbility(List<MMOAbilityDefinition> ownedAbilities, MMOAbilityDefinition ability)
        {
            if (ability != null && !ownedAbilities.Contains(ability))
            {
                ownedAbilities.Add(ability);
            }
        }

        private static bool IsAbilityAllowedForCharacter(MMOAbilityDefinition ability, MMOCharacterArchetypeDefinition archetype)
        {
            if (ability == null)
            {
                return false;
            }

            if (ability.IsAutoAttack || ability.AbilityId == "auto_attack")
            {
                return true;
            }

            if (archetype == null)
            {
                return true;
            }

            if (ability == archetype.RacialAbility || ability == archetype.ClassAbility)
            {
                return true;
            }

            foreach (MMOAbilityDefinition startingAbility in archetype.StartingAbilities)
            {
                if (startingAbility == ability)
                {
                    return true;
                }
            }

            List<MMOTrainerOfferEntry> offers = new();
            MMOTrainerOfferCatalog.AppendOffersForClass(archetype.CharacterClass, offers);
            foreach (MMOTrainerOfferEntry offer in offers)
            {
                if (offer != null && offer.Ability == ability)
                {
                    return true;
                }
            }

            return false;
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
                    slot.SetAbility(ability != null && abilitySystem != null && abilitySystem.KnowsAbility(ability) ? ability : null);
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

        private void ApplyCharacterVisuals(MMOPlayableRace race, MMOCharacterArchetypeDefinition archetype)
        {
            Transform visualRoot = transform.Find("Capsule Visual");
            Renderer renderer = visualRoot != null
                ? visualRoot.GetComponentInChildren<Renderer>(true)
                : GetComponentInChildren<Renderer>(true);

            Transform targetTransform = renderer != null ? renderer.transform : visualRoot;
            if (targetTransform != null)
            {
                targetTransform.localScale = GetRaceVisualScale(race);
            }

        }

        private static Vector3 GetRaceVisualScale(MMOPlayableRace race)
        {
            return race == MMOPlayableRace.Troll
                ? new Vector3(0.66f, 1.18f, 0.66f)
                : new Vector3(0.72f, 1.05f, 0.72f);
        }

        private MMOItemDefinition ResolveItem(string itemId)
        {
            return itemCatalog != null ? itemCatalog.FindById(itemId) : null;
        }

        private void ApplyCharacterAppearance(MMOCharacterSaveData saveData)
        {
            appearanceCatalog ??= Resources.Load<MMOCharacterAppearanceCatalog>("RPGClone/Character_Appearance_Catalog");
            MMOCharacterAppearanceVisuals appearanceVisuals = GetComponent<MMOCharacterAppearanceVisuals>()
                ?? gameObject.AddComponent<MMOCharacterAppearanceVisuals>();
            appearanceVisuals.Configure(
                appearanceCatalog,
                saveData != null ? saveData.headStyleId : string.Empty,
                saveData != null ? saveData.faceId : string.Empty,
                saveData != null ? saveData.hairstyleId : string.Empty,
                saveData != null ? saveData.hairColorId : string.Empty);
        }

        private MMOAbilityDefinition ResolveAbility(string abilityId)
        {
            return abilityCatalog != null ? abilityCatalog.FindById(abilityId) : null;
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
