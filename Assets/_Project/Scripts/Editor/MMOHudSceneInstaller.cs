using RPGClone.Characters;
using RPGClone.Abilities;
using RPGClone.Combat;
using RPGClone.Inventory;
using RPGClone.Player;
using RPGClone.Targeting;
using RPGClone.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace RPGClone.EditorTools
{
    public static class MMOHudSceneInstaller
    {
        private const string RootFolder = "Assets/_Project";
        private const string ConfigFolder = RootFolder + "/Configs";
        private const string CharacterProfileFolder = ConfigFolder + "/Characters";
        private const string AbilityFolder = ConfigFolder + "/Abilities";
        private const string ProgressionFolder = ConfigFolder + "/Progression";

        [MenuItem("Tools/RPG Clone/Install HUD And Target Frames")]
        public static void InstallIntoOpenScene()
        {
            InstallIntoOpenScene(true);
        }

        public static void InstallIntoOpenScene(bool saveScene)
        {
            EnsureFolders();

            CharacterProfiles profiles = GetOrCreateProfiles();
            MMOLevelProgressionDefinition levelProgression = GetOrCreateLevelProgression();
            MMOAbilityDefinition autoAttackAbility = GetOrCreateAutoAttackAbility();
            GameObject player = FindPlayer();
            if (player == null)
            {
                Debug.LogWarning("HUD installer could not find a Player tagged object in the open scene.");
                return;
            }

            MMOCharacterIdentity playerIdentity = EnsureIdentity(player, profiles.Player, "Player", true);
            Camera gameplayCamera = Camera.main;
            MMOTargetSelectionController selectionController = EnsureSelectionController(gameplayCamera);
            MMOAbilitySystem playerAbilitySystem = EnsureCombatSetup(player, autoAttackAbility);
            MMOAutoAttackController autoAttackController = EnsurePlayerAutoAttack(player, autoAttackAbility, selectionController, gameplayCamera);
            EnsurePlayerInputReader(player);
            MMOExperienceComponent experience = EnsureExperience(player, levelProgression);
            MMOInventoryContainer inventory = EnsureInventory(player);
            MMOCharacterEquipment equipment = EnsureEquipment(player);

            InstallTargetIdentities(player, profiles, autoAttackAbility);
            EnsureHud(playerIdentity, selectionController, playerAbilitySystem, autoAttackController, autoAttackAbility, gameplayCamera, inventory, equipment, experience);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            if (saveScene)
            {
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            }

            Debug.Log("Installed player and target unit frames for the open scene.");
        }

        private static void EnsureFolders()
        {
            CreateFolderIfMissing(RootFolder);
            CreateFolderIfMissing(ConfigFolder);
            CreateFolderIfMissing(CharacterProfileFolder);
            CreateFolderIfMissing(AbilityFolder);
            CreateFolderIfMissing(ProgressionFolder);
        }

        private static void CreateFolderIfMissing(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            string folderName = System.IO.Path.GetFileName(assetPath);
            if (!string.IsNullOrEmpty(parent))
            {
                CreateFolderIfMissing(parent);
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }

        private static CharacterProfiles GetOrCreateProfiles()
        {
            return new CharacterProfiles
            {
                Player = GetOrCreateProfile("Player", "Player", 1, 140, 100, new Color(0.18f, 0.38f, 0.75f), MMOEntityFaction.Player, CreateStats(14, 12, 11, 10, 10, 20, 12, 0, 5f, 8f, 2f, 3f)),
                FriendlyNpc = GetOrCreateProfile("Friendly NPC", "Razorcrag Scout", 4, 120, 90, new Color(0.95f, 0.66f, 0.22f), MMOEntityFaction.Friendly, CreateStats(12, 9, 12, 11, 10, 12, 8, 4, 3f, 6f, 2.2f, 3f)),
                HostileCreature = GetOrCreateProfile("Hostile Creature", "Bristleback", 3, 90, 45, new Color(0.72f, 0.18f, 0.16f), MMOEntityFaction.Hostile, CreateStats(9, 11, 8, 4, 5, 8, 10, 0, 4f, 7f, 2.4f, 3f)),
                TrainingDummy = GetOrCreateProfile("Training Dummy", "Training Dummy", 1, 180, 0, new Color(0.34f, 0.22f, 0.13f), MMOEntityFaction.Hostile, CreateStats(18, 1, 1, 1, 1, 0, 0, 0, 1f, 1f, 2f, 3f))
            };
        }

        private static MMOCharacterProfile GetOrCreateProfile(string assetName, string displayName, int level, int health, int mana, Color portraitTint, MMOEntityFaction faction, MMOCharacterStats stats)
        {
            string path = $"{CharacterProfileFolder}/{Sanitize(assetName)}.asset";
            MMOCharacterProfile profile = AssetDatabase.LoadAssetAtPath<MMOCharacterProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<MMOCharacterProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            profile.Configure(displayName, level, health, mana, portraitTint, null, true, faction, stats);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static MMOCharacterStats CreateStats(
            int stamina,
            int strength,
            int agility,
            int intellect,
            int spirit,
            int armor,
            int attackPower,
            int spellPower,
            float meleeMinDamage,
            float meleeMaxDamage,
            float meleeAttackSpeed,
            float meleeRange)
        {
            MMOCharacterStats stats = new();
            stats.Configure(stamina, strength, agility, intellect, spirit, armor, attackPower, spellPower, meleeMinDamage, meleeMaxDamage, meleeAttackSpeed, meleeRange);
            return stats;
        }

        private static MMOAbilityDefinition GetOrCreateAutoAttackAbility()
        {
            string path = $"{AbilityFolder}/Auto_Attack.asset";
            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(path);
            if (ability == null)
            {
                ability = ScriptableObject.CreateInstance<MMOAbilityDefinition>();
                AssetDatabase.CreateAsset(ability, path);
            }

            MMOAbilityEffectDefinition weaponDamage = new();
            weaponDamage.Configure(MMOAbilityEffectType.Damage, MMOAbilityAmountSource.WeaponDamage, MMODamageSchool.Physical, 0f, 1f);
            ability.Configure(
                "auto_attack",
                "Auto Attack",
                "Repeated weapon swings against a hostile target.",
                MMOAbilityTargetType.Hostile,
                true,
                true,
                3f,
                0f,
                0,
                new[] { weaponDamage });

            EditorUtility.SetDirty(ability);
            return ability;
        }

        private static MMOLevelProgressionDefinition GetOrCreateLevelProgression()
        {
            string path = $"{ProgressionFolder}/Starter_Level_Progression.asset";
            MMOLevelProgressionDefinition progression = AssetDatabase.LoadAssetAtPath<MMOLevelProgressionDefinition>(path);
            if (progression == null)
            {
                progression = ScriptableObject.CreateInstance<MMOLevelProgressionDefinition>();
                AssetDatabase.CreateAsset(progression, path);
            }

            progression.Configure(
                60,
                400,
                110,
                1.12f,
                CreateStatGrowth(2, 1, 1, 1, 1, 1, 1, 1, 0.2f, 0.3f));
            EditorUtility.SetDirty(progression);
            return progression;
        }

        private static MMOCharacterStatGrowth CreateStatGrowth(
            int stamina,
            int strength,
            int agility,
            int intellect,
            int spirit,
            int armor,
            int attackPower,
            int spellPower,
            float meleeMinDamage,
            float meleeMaxDamage)
        {
            MMOCharacterStatGrowth statGrowth = new();
            statGrowth.Configure(stamina, strength, agility, intellect, spirit, armor, attackPower, spellPower, meleeMinDamage, meleeMaxDamage);
            return statGrowth;
        }

        private static GameObject FindPlayer()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                return player;
            }

            GameObject fallback = GameObject.Find("Player");
            return fallback != null ? fallback : GameObject.Find("PlayerCapsule");
        }

        private static MMOTargetSelectionController EnsureSelectionController(Camera gameplayCamera)
        {
            MMOTargetSelectionController selectionController = Object.FindAnyObjectByType<MMOTargetSelectionController>();
            GameObject host = gameplayCamera != null ? gameplayCamera.gameObject : FindPlayer();

            if (selectionController == null && host != null)
            {
                selectionController = host.AddComponent<MMOTargetSelectionController>();
            }

            if (selectionController != null)
            {
                selectionController.SetSelectionCamera(gameplayCamera);
                EditorUtility.SetDirty(selectionController);
            }

            return selectionController;
        }

        private static void InstallTargetIdentities(GameObject player, CharacterProfiles profiles, MMOAbilityDefinition autoAttackAbility)
        {
            GameObject[] sceneObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
            foreach (GameObject sceneObject in sceneObjects)
            {
                if (sceneObject == player)
                {
                    continue;
                }

                if (sceneObject.GetComponent<Collider>() == null)
                {
                    continue;
                }

                string objectName = sceneObject.name;
                if (objectName.StartsWith("Quest Giver"))
                {
                    EnsureIdentity(sceneObject, profiles.FriendlyNpc, CleanDisplayName(objectName), true);
                    EnsureCombatSetup(sceneObject, autoAttackAbility);
                }
                else if (objectName.StartsWith("Bristleback Creature") || objectName.StartsWith("Ash Canyon Creature"))
                {
                    EnsureIdentity(sceneObject, profiles.HostileCreature, CleanDisplayName(objectName), true);
                    EnsureCombatSetup(sceneObject, autoAttackAbility);
                }
                else if (objectName.StartsWith("Training Dummy"))
                {
                    EnsureIdentity(sceneObject, profiles.TrainingDummy, CleanDisplayName(objectName), true);
                    EnsureCombatSetup(sceneObject, autoAttackAbility);
                }
            }
        }

        private static MMOCharacterIdentity EnsureIdentity(GameObject target, MMOCharacterProfile profile, string displayNameOverride, bool resetResources)
        {
            MMOCharacterIdentity identity = target.GetComponent<MMOCharacterIdentity>();
            if (identity == null)
            {
                identity = target.AddComponent<MMOCharacterIdentity>();
            }

            identity.Configure(profile, displayNameOverride, resetResources);
            EditorUtility.SetDirty(identity);
            return identity;
        }

        private static MMOAbilitySystem EnsureCombatSetup(GameObject target, MMOAbilityDefinition autoAttackAbility)
        {
            MMOCombatant combatant = target.GetComponent<MMOCombatant>();
            if (combatant == null)
            {
                combatant = target.AddComponent<MMOCombatant>();
            }

            MMOAbilitySystem abilitySystem = target.GetComponent<MMOAbilitySystem>();
            if (abilitySystem == null)
            {
                abilitySystem = target.AddComponent<MMOAbilitySystem>();
            }

            abilitySystem.LearnAbility(autoAttackAbility);
            EnsureRegeneration(target);
            EditorUtility.SetDirty(combatant);
            EditorUtility.SetDirty(abilitySystem);
            return abilitySystem;
        }

        private static MMOExperienceComponent EnsureExperience(GameObject player, MMOLevelProgressionDefinition progression)
        {
            MMOExperienceComponent experience = player.GetComponent<MMOExperienceComponent>();
            if (experience == null)
            {
                experience = player.AddComponent<MMOExperienceComponent>();
            }

            experience.SetProgression(progression);
            EditorUtility.SetDirty(experience);
            return experience;
        }

        private static MMOCharacterRegeneration EnsureRegeneration(GameObject target)
        {
            MMOCharacterRegeneration regeneration = target.GetComponent<MMOCharacterRegeneration>();
            if (regeneration == null)
            {
                regeneration = target.AddComponent<MMOCharacterRegeneration>();
            }

            EditorUtility.SetDirty(regeneration);
            return regeneration;
        }

        private static MMOAutoAttackController EnsurePlayerAutoAttack(
            GameObject player,
            MMOAbilityDefinition autoAttackAbility,
            MMOTargetSelectionController selectionController,
            Camera gameplayCamera)
        {
            MMOAutoAttackController controller = player.GetComponent<MMOAutoAttackController>();
            if (controller == null)
            {
                controller = player.AddComponent<MMOAutoAttackController>();
            }

            controller.SetAutoAttackAbility(autoAttackAbility);
            controller.SetTargetSelectionController(selectionController);
            controller.SetInteractionCamera(gameplayCamera);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static MMOInputReader EnsurePlayerInputReader(GameObject player)
        {
            MMOInputReader inputReader = player.GetComponent<MMOInputReader>();
            if (inputReader != null)
            {
                inputReader.SetLockCursorWhileMouseLooking(false);
                EditorUtility.SetDirty(inputReader);
            }

            return inputReader;
        }

        private static MMOInventoryContainer EnsureInventory(GameObject player)
        {
            MMOInventoryContainer inventory = player.GetComponent<MMOInventoryContainer>();
            if (inventory == null)
            {
                inventory = player.AddComponent<MMOInventoryContainer>();
            }

            EditorUtility.SetDirty(inventory);
            return inventory;
        }

        private static MMOCharacterEquipment EnsureEquipment(GameObject player)
        {
            MMOCharacterEquipment equipment = player.GetComponent<MMOCharacterEquipment>();
            if (equipment == null)
            {
                equipment = player.AddComponent<MMOCharacterEquipment>();
            }

            equipment.EnsureDefaultSlots();
            EditorUtility.SetDirty(equipment);
            return equipment;
        }

        private static void EnsureHud(
            MMOCharacterIdentity playerIdentity,
            MMOTargetSelectionController selectionController,
            MMOAbilitySystem playerAbilitySystem,
            MMOAutoAttackController autoAttackController,
            MMOAbilityDefinition autoAttackAbility,
            Camera gameplayCamera,
            MMOInventoryContainer inventory,
            MMOCharacterEquipment equipment,
            MMOExperienceComponent experience)
        {
            Canvas canvas = EnsureCanvas();
            EnsureEventSystem();

            MMOUnitFrameView playerFrame = EnsureFrame(canvas.transform, "Player Unit Frame", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -32f));
            MMOUnitFrameView targetFrame = EnsureFrame(canvas.transform, "Target Unit Frame", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-32f, -32f));
            MMOUnitFramePresenter presenter = canvas.GetComponent<MMOUnitFramePresenter>();
            if (presenter == null)
            {
                presenter = canvas.gameObject.AddComponent<MMOUnitFramePresenter>();
            }

            playerFrame.Bind(playerIdentity);
            targetFrame.Clear();
            presenter.Configure(playerIdentity, selectionController, playerFrame, targetFrame);
            MMOCharacterPanelPresenter characterPanel = EnsureCharacterPanel(canvas.transform, playerIdentity, equipment);
            MMOInventoryPresenter inventoryPanel = EnsureInventoryPanel(canvas.transform, inventory);
            MMOSpellBookPresenter spellBookPanel = EnsureSpellBookPanel(canvas.transform, playerAbilitySystem);
            MMOActionBarPresenter actionBar = EnsureActionBar(canvas.transform, playerAbilitySystem, autoAttackController, selectionController, autoAttackAbility);
            EnsureExperienceBar(canvas.transform, experience);
            EnsureLootWindow(canvas.transform);
            EnsureItemTooltip(canvas.transform);
            EnsureAbilityTooltip(canvas.transform);
            EnsureBottomHud(canvas.transform, actionBar, characterPanel, inventoryPanel, spellBookPanel);
            EnsureCombatFeedback(canvas.transform, playerAbilitySystem, gameplayCamera);

            EditorUtility.SetDirty(playerFrame);
            EditorUtility.SetDirty(targetFrame);
            EditorUtility.SetDirty(presenter);
            EditorUtility.SetDirty(canvas);
        }

        private static Canvas EnsureCanvas()
        {
            GameObject existing = GameObject.Find("HUD Canvas");
            Canvas canvas = existing != null ? existing.GetComponent<Canvas>() : null;
            if (canvas == null)
            {
                GameObject canvasObject = new("HUD Canvas");
                canvas = canvasObject.AddComponent<Canvas>();
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (canvas.GetComponent<GraphicRaycaster>() == null)
            {
                canvas.gameObject.AddComponent<GraphicRaycaster>();
            }

            return canvas;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null)
            {
                return;
            }

            GameObject eventSystemObject = new("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }

        private static MMOUnitFrameView EnsureFrame(Transform canvas, string objectName, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition)
        {
            Transform existing = canvas.Find(objectName);
            GameObject frameObject = existing != null ? existing.gameObject : new GameObject(objectName, typeof(RectTransform));
            frameObject.transform.SetParent(canvas, false);
            frameObject.SetActive(true);

            RectTransform rectTransform = (RectTransform)frameObject.transform;
            rectTransform.anchorMin = anchor;
            rectTransform.anchorMax = anchor;
            rectTransform.pivot = pivot;
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = new Vector2(290f, 82f);

            MMOUnitFrameView frame = frameObject.GetComponent<MMOUnitFrameView>();
            if (frame == null)
            {
                frame = frameObject.AddComponent<MMOUnitFrameView>();
            }

            RemoveDuplicateGeneratedFrameChildren(frameObject.transform);
            return frame;
        }

        private static void RemoveDuplicateGeneratedFrameChildren(Transform frame)
        {
            System.Collections.Generic.HashSet<string> seenNames = new();
            string[] generatedNames =
            {
                "Frame Background",
                "Frame Border",
                "Portrait",
                "Content",
                "Level Badge",
                "Level"
            };

            for (int i = frame.childCount - 1; i >= 0; i--)
            {
                Transform child = frame.GetChild(i);
                if (!IsGeneratedFrameChild(child.name, generatedNames))
                {
                    continue;
                }

                if (!seenNames.Add(child.name))
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        private static bool IsGeneratedFrameChild(string childName, string[] generatedNames)
        {
            foreach (string generatedName in generatedNames)
            {
                if (childName == generatedName)
                {
                    return true;
                }
            }

            return false;
        }

        private static MMOActionBarPresenter EnsureActionBar(
            Transform canvas,
            MMOAbilitySystem playerAbilitySystem,
            MMOAutoAttackController autoAttackController,
            MMOTargetSelectionController selectionController,
            MMOAbilityDefinition autoAttackAbility)
        {
            Transform bottomHud = canvas.Find("Bottom HUD");
            Transform existing = bottomHud != null ? bottomHud.Find("Action Bar") : null;
            if (existing == null)
            {
                existing = canvas.Find("Action Bar");
            }

            GameObject actionBarObject = existing != null ? existing.gameObject : new GameObject("Action Bar", typeof(RectTransform));
            actionBarObject.transform.SetParent(bottomHud != null ? bottomHud : canvas, false);
            actionBarObject.SetActive(true);

            RectTransform rectTransform = (RectTransform)actionBarObject.transform;
            rectTransform.anchorMin = new Vector2(0f, 0.5f);
            rectTransform.anchorMax = new Vector2(0f, 0.5f);
            rectTransform.pivot = new Vector2(0f, 0.5f);
            rectTransform.anchoredPosition = new Vector2(12f, 0f);
            rectTransform.sizeDelta = new Vector2(642f, 58f);

            MMOActionBarPresenter presenter = actionBarObject.GetComponent<MMOActionBarPresenter>();
            if (presenter == null)
            {
                presenter = actionBarObject.AddComponent<MMOActionBarPresenter>();
            }

            MMOActionBarSlot autoAttackSlot = new()
            {
                ability = autoAttackAbility,
                key = Key.Digit1
            };
            presenter.Configure(playerAbilitySystem, autoAttackController, selectionController, CreateDefaultActionSlots(autoAttackSlot));
            EditorUtility.SetDirty(presenter);
            return presenter;
        }

        private static MMOActionBarSlot[] CreateDefaultActionSlots(MMOActionBarSlot firstSlot)
        {
            Key[] keys =
            {
                Key.Digit1,
                Key.Digit2,
                Key.Digit3,
                Key.Digit4,
                Key.Digit5,
                Key.Digit6,
                Key.Digit7,
                Key.Digit8,
                Key.Digit9,
                Key.Digit0,
                Key.Minus,
                Key.Equals
            };

            MMOActionBarSlot[] slots = new MMOActionBarSlot[MMOActionBarPresenter.DefaultSlotCount];
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = new MMOActionBarSlot
                {
                    ability = i == 0 ? firstSlot.ability : null,
                    key = keys[i]
                };
            }

            return slots;
        }

        private static MMOBottomHudPresenter EnsureBottomHud(
            Transform canvas,
            MMOActionBarPresenter actionBar,
            MMOCharacterPanelPresenter characterPanel,
            MMOInventoryPresenter inventoryPanel,
            MMOSpellBookPresenter spellBookPanel)
        {
            Transform existing = canvas.Find("Bottom HUD");
            GameObject bottomHudObject = existing != null ? existing.gameObject : new GameObject("Bottom HUD", typeof(RectTransform));
            bottomHudObject.transform.SetParent(canvas, false);
            bottomHudObject.SetActive(true);

            MMOBottomHudPresenter presenter = bottomHudObject.GetComponent<MMOBottomHudPresenter>();
            if (presenter == null)
            {
                presenter = bottomHudObject.AddComponent<MMOBottomHudPresenter>();
            }

            presenter.Configure(actionBar, characterPanel, inventoryPanel, spellBookPanel);
            EditorUtility.SetDirty(presenter);
            return presenter;
        }

        private static MMOCharacterPanelPresenter EnsureCharacterPanel(Transform canvas, MMOCharacterIdentity playerIdentity, MMOCharacterEquipment equipment)
        {
            Transform existing = canvas.Find("Character Panel");
            GameObject panelObject = existing != null ? existing.gameObject : new GameObject("Character Panel", typeof(RectTransform));
            panelObject.transform.SetParent(canvas, false);

            RectTransform rectTransform = (RectTransform)panelObject.transform;
            rectTransform.anchorMin = new Vector2(0f, 0.5f);
            rectTransform.anchorMax = new Vector2(0f, 0.5f);
            rectTransform.pivot = new Vector2(0f, 0.5f);
            rectTransform.anchoredPosition = new Vector2(42f, 20f);

            MMOCharacterPanelPresenter presenter = panelObject.GetComponent<MMOCharacterPanelPresenter>();
            if (presenter == null)
            {
                presenter = panelObject.AddComponent<MMOCharacterPanelPresenter>();
            }

            presenter.Configure(playerIdentity, equipment);
            panelObject.SetActive(false);
            EditorUtility.SetDirty(presenter);
            return presenter;
        }

        private static MMOInventoryPresenter EnsureInventoryPanel(Transform canvas, MMOInventoryContainer inventory)
        {
            Transform existing = canvas.Find("Inventory Panel");
            GameObject panelObject = existing != null ? existing.gameObject : new GameObject("Inventory Panel", typeof(RectTransform));
            panelObject.transform.SetParent(canvas, false);

            RectTransform rectTransform = (RectTransform)panelObject.transform;
            rectTransform.anchorMin = new Vector2(1f, 0f);
            rectTransform.anchorMax = new Vector2(1f, 0f);
            rectTransform.pivot = new Vector2(1f, 0f);
            rectTransform.anchoredPosition = new Vector2(-38f, 124f);

            MMOInventoryPresenter presenter = panelObject.GetComponent<MMOInventoryPresenter>();
            if (presenter == null)
            {
                presenter = panelObject.AddComponent<MMOInventoryPresenter>();
            }

            presenter.Configure(inventory);
            panelObject.SetActive(false);
            EditorUtility.SetDirty(presenter);
            return presenter;
        }

        private static MMOExperienceBarPresenter EnsureExperienceBar(Transform canvas, MMOExperienceComponent experience)
        {
            Transform existing = canvas.Find("Experience Bar");
            GameObject panelObject = existing != null ? existing.gameObject : new GameObject("Experience Bar", typeof(RectTransform));
            panelObject.transform.SetParent(canvas, false);
            panelObject.SetActive(true);

            MMOExperienceBarPresenter presenter = panelObject.GetComponent<MMOExperienceBarPresenter>();
            if (presenter == null)
            {
                presenter = panelObject.AddComponent<MMOExperienceBarPresenter>();
            }

            presenter.Configure(experience);
            EditorUtility.SetDirty(presenter);
            return presenter;
        }

        private static MMOLootWindowPresenter EnsureLootWindow(Transform canvas)
        {
            Transform existing = canvas.Find("Loot Window");
            GameObject panelObject = existing != null ? existing.gameObject : new GameObject("Loot Window", typeof(RectTransform));
            panelObject.transform.SetParent(canvas, false);

            MMOLootWindowPresenter presenter = panelObject.GetComponent<MMOLootWindowPresenter>();
            if (presenter == null)
            {
                presenter = panelObject.AddComponent<MMOLootWindowPresenter>();
            }

            EditorUtility.SetDirty(presenter);
            return presenter;
        }

        private static MMOItemTooltipPresenter EnsureItemTooltip(Transform canvas)
        {
            Transform existing = canvas.Find("Item Tooltip");
            GameObject panelObject = existing != null ? existing.gameObject : new GameObject("Item Tooltip", typeof(RectTransform));
            panelObject.transform.SetParent(canvas, false);

            MMOItemTooltipPresenter presenter = panelObject.GetComponent<MMOItemTooltipPresenter>();
            if (presenter == null)
            {
                presenter = panelObject.AddComponent<MMOItemTooltipPresenter>();
            }

            EditorUtility.SetDirty(presenter);
            return presenter;
        }

        private static MMOAbilityTooltipPresenter EnsureAbilityTooltip(Transform canvas)
        {
            Transform existing = canvas.Find("Ability Tooltip");
            GameObject panelObject = existing != null ? existing.gameObject : new GameObject("Ability Tooltip", typeof(RectTransform));
            panelObject.transform.SetParent(canvas, false);

            MMOAbilityTooltipPresenter presenter = panelObject.GetComponent<MMOAbilityTooltipPresenter>();
            if (presenter == null)
            {
                presenter = panelObject.AddComponent<MMOAbilityTooltipPresenter>();
            }

            EditorUtility.SetDirty(presenter);
            return presenter;
        }

        private static MMOSpellBookPresenter EnsureSpellBookPanel(Transform canvas, MMOAbilitySystem playerAbilitySystem)
        {
            Transform existing = canvas.Find("Spellbook Panel");
            GameObject panelObject = existing != null ? existing.gameObject : new GameObject("Spellbook Panel", typeof(RectTransform));
            panelObject.transform.SetParent(canvas, false);

            RectTransform rectTransform = (RectTransform)panelObject.transform;
            rectTransform.anchorMin = new Vector2(0.5f, 0f);
            rectTransform.anchorMax = new Vector2(0.5f, 0f);
            rectTransform.pivot = new Vector2(0.5f, 0f);
            rectTransform.anchoredPosition = new Vector2(0f, 124f);

            MMOSpellBookPresenter presenter = panelObject.GetComponent<MMOSpellBookPresenter>();
            if (presenter == null)
            {
                presenter = panelObject.AddComponent<MMOSpellBookPresenter>();
            }

            presenter.Configure(playerAbilitySystem);
            panelObject.SetActive(false);
            EditorUtility.SetDirty(presenter);
            return presenter;
        }

        private static MMOCombatFeedbackPresenter EnsureCombatFeedback(
            Transform canvas,
            MMOAbilitySystem playerAbilitySystem,
            Camera gameplayCamera)
        {
            Transform existing = canvas.Find("Combat Feedback");
            GameObject feedbackObject = existing != null ? existing.gameObject : new GameObject("Combat Feedback", typeof(RectTransform));
            feedbackObject.transform.SetParent(canvas, false);
            feedbackObject.SetActive(true);

            RectTransform rectTransform = (RectTransform)feedbackObject.transform;
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            MMOCombatFeedbackPresenter presenter = feedbackObject.GetComponent<MMOCombatFeedbackPresenter>();
            if (presenter == null)
            {
                presenter = feedbackObject.AddComponent<MMOCombatFeedbackPresenter>();
            }

            presenter.Configure(playerAbilitySystem, gameplayCamera);
            EditorUtility.SetDirty(presenter);
            return presenter;
        }

        private static string CleanDisplayName(string objectName)
        {
            return objectName.Replace(" Placeholder", string.Empty);
        }

        private static string Sanitize(string value)
        {
            foreach (char invalid in System.IO.Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }

            return value.Replace(' ', '_');
        }

        private sealed class CharacterProfiles
        {
            public MMOCharacterProfile Player;
            public MMOCharacterProfile FriendlyNpc;
            public MMOCharacterProfile HostileCreature;
            public MMOCharacterProfile TrainingDummy;
        }
    }
}
