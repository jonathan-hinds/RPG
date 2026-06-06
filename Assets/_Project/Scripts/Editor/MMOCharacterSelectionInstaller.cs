using System;
using System.Collections.Generic;
using System.IO;
using RPGClone.Abilities;
using RPGClone.CharacterSelection;
using RPGClone.Characters;
using RPGClone.Inventory;
using RPGClone.Quests;
using RPGClone.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RPGClone.EditorTools
{
    public static class MMOCharacterSelectionInstaller
    {
        private const string RootFolder = "Assets/_Project";
        private const string ConfigFolder = RootFolder + "/Configs";
        private const string AbilityFolder = ConfigFolder + "/Abilities";
        private const string CharacterFolder = ConfigFolder + "/Characters";
        private const string ItemFolder = ConfigFolder + "/Items";
        private const string ProgressionFolder = ConfigFolder + "/Progression";
        private const string ArchetypeFolder = ConfigFolder + "/Archetypes";
        private const string SceneFolder = "Assets/Scenes";
        private const string CharacterSelectionScenePath = SceneFolder + "/CharacterSelection.unity";
        private const string CharacterSelectionSceneName = "CharacterSelection";
        private const string GameplaySceneName = "OrcishStarterValley";
        private const string GameplayScenePath = SceneFolder + "/" + GameplaySceneName + ".unity";

        [MenuItem("Tools/RPG Clone/Build Character Selection")]
        public static void BuildCharacterSelection()
        {
            EnsureFolders();
            AbilitySet abilities = CreateAbilities();
            MMOAbilityCatalog abilityCatalog = CreateAbilityCatalog(abilities.All);
            ProgressionSet progression = CreateProgression();
            MMOCharacterArchetypeCatalog catalog = CreateArchetypes(abilities, progression);
            CreateCharacterSelectionScene(catalog);
            ConfigureGameplayScene(catalog, abilityCatalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Built character selection scene, race/class archetypes, abilities, and gameplay persistence hooks.");
        }

        private static void EnsureFolders()
        {
            CreateFolderIfMissing(RootFolder);
            CreateFolderIfMissing(ConfigFolder);
            CreateFolderIfMissing(AbilityFolder);
            CreateFolderIfMissing(CharacterFolder);
            CreateFolderIfMissing(ItemFolder);
            CreateFolderIfMissing(ProgressionFolder);
            CreateFolderIfMissing(ArchetypeFolder);
            CreateFolderIfMissing(SceneFolder);
        }

        private static void CreateFolderIfMissing(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                return;
            }

            string parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            string folderName = Path.GetFileName(assetPath);
            if (!string.IsNullOrEmpty(parent))
            {
                CreateFolderIfMissing(parent);
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }

        private static AbilitySet CreateAbilities()
        {
            return new AbilitySet
            {
                AutoAttack = GetOrCreateDamageAbility("Auto_Attack", "auto_attack", "Auto Attack", "Repeated weapon swings against a hostile target.", MMOAbilityTargetType.Hostile, true, true, 3f, 0f, 0, 0f, false, false, MMOAbilityAmountSource.WeaponDamage, MMODamageSchool.Physical, 0f, 1f),
                OrcRacial = GetOrCreateBuffAbility("Orc_Blood_Fury", "orc_blood_fury", "Blood Fury", "Increases attack power and attack speed for a short time.", 120f, 15f, 20, 1.15f, 1.1f, 1f),
                TrollRacial = GetOrCreateBuffAbility("Troll_Regeneration", "troll_regeneration", "Regeneration", "Greatly increases health regeneration for a short time.", 120f, 10f, 0, 1f, 1f, 4f),
                Bash = GetOrCreateDamageAbility("Warrior_Bash", "warrior_bash", "Bash", "A close-range shield bash that deals physical damage.", MMOAbilityTargetType.Hostile, false, false, 3f, 8f, 0, 0f, false, false, MMOAbilityAmountSource.AttackPower, MMODamageSchool.Physical, 12f, 0.25f),
                Fireball = GetOrCreateDamageAbility("Mage_Fireball", "mage_fireball", "Fireball", "Hurls a fiery projectile at a hostile target.", MMOAbilityTargetType.Hostile, false, false, 30f, 0f, 16, 2.5f, true, false, MMOAbilityAmountSource.SpellPower, MMODamageSchool.Fire, 24f, 0.85f),
                HealingBeam = GetOrCreateHealAbility("Shaman_Healing_Beam", "shaman_healing_beam", "Healing Beam", "Channels restorative energy into a friendly target. Hostile targets redirect the heal to self.", 30f, 0f, 18, 2f, true, true, 30f, 0.8f),
                Berzerkitis = GetOrCreateBuffAbility("Warrior_Berzerkitis", "warrior_berzerkitis", "Berzerkitis", "Increases attack speed by 50%.", 60f, 15f, 0, 1f, 1.5f, 1f),
                Charge = GetOrCreateChargeAbility("Warrior_Charge", "warrior_charge", "Charge", "Charges a hostile target if a valid path exists, then strikes with physical force.", 25f, 15f, 18f, 2.5f, MMOAbilityAmountSource.AttackPower, MMODamageSchool.Physical, 10f, 0.35f),
                MageArmor = GetOrCreateBuffAbility("Mage_Mage_Armor", "mage_mage_armor", "Mage Armor", "Increases out of combat mana regeneration by 50%.", 0f, 1800f, 0, 1f, 1f, 1f, 1.5f, 0f),
                FireBlast = GetOrCreateDamageAbility("Mage_Fire_Blast", "mage_fire_blast", "Fire Blast", "Blasts a hostile target with instant fire damage.", MMOAbilityTargetType.Hostile, false, false, 20f, 8f, 12, 0f, false, false, MMOAbilityAmountSource.SpellPower, MMODamageSchool.Fire, 18f, 0.45f),
                WaterShield = GetOrCreateBuffAbility("Shaman_Water_Shield", "shaman_water_shield", "Water Shield", "Absorbs 20% of incoming damage and restores that amount as mana.", 0f, 600f, 0, 1f, 1f, 1f, 1f, 0.2f),
                LightningBolt = GetOrCreateDamageAbility("Shaman_Lightning_Bolt", "shaman_lightning_bolt", "Lightning Bolt", "Calls down nature damage on a hostile target.", MMOAbilityTargetType.Hostile, false, false, 30f, 0f, 14, 2f, true, false, MMOAbilityAmountSource.SpellPower, MMODamageSchool.Nature, 22f, 0.75f)
            };
        }

        private static MMOAbilityDefinition GetOrCreateDamageAbility(
            string assetName,
            string abilityId,
            string displayName,
            string description,
            MMOAbilityTargetType targetType,
            bool autoAttack,
            bool toggled,
            float range,
            float cooldown,
            int manaCost,
            float castTime,
            bool interruptOnMovement,
            bool fallbackSelf,
            MMOAbilityAmountSource amountSource,
            MMODamageSchool school,
            float flatAmount,
            float coefficient)
        {
            MMOAbilityEffectDefinition effect = new();
            effect.Configure(MMOAbilityEffectType.Damage, amountSource, school, flatAmount, coefficient);
            return GetOrCreateAbility(assetName, abilityId, displayName, description, targetType, autoAttack, toggled, range, cooldown, manaCost, castTime, interruptOnMovement, fallbackSelf, new[] { effect });
        }

        private static MMOAbilityDefinition GetOrCreateChargeAbility(
            string assetName,
            string abilityId,
            string displayName,
            string description,
            float range,
            float cooldown,
            float chargeSpeed,
            float stopDistance,
            MMOAbilityAmountSource amountSource,
            MMODamageSchool school,
            float flatAmount,
            float coefficient)
        {
            MMOAbilityEffectDefinition effect = new();
            effect.ConfigureCharge(chargeSpeed, stopDistance, amountSource, school, flatAmount, coefficient);
            return GetOrCreateAbility(assetName, abilityId, displayName, description, MMOAbilityTargetType.Hostile, false, false, range, cooldown, 0, 0f, false, false, new[] { effect });
        }

        private static MMOAbilityDefinition GetOrCreateHealAbility(
            string assetName,
            string abilityId,
            string displayName,
            string description,
            float range,
            float cooldown,
            int manaCost,
            float castTime,
            bool interruptOnMovement,
            bool fallbackSelf,
            float flatAmount,
            float coefficient)
        {
            MMOAbilityEffectDefinition effect = new();
            effect.Configure(MMOAbilityEffectType.Heal, MMOAbilityAmountSource.SpellPower, MMODamageSchool.Holy, flatAmount, coefficient);
            return GetOrCreateAbility(assetName, abilityId, displayName, description, MMOAbilityTargetType.Friendly, false, false, range, cooldown, manaCost, castTime, interruptOnMovement, fallbackSelf, new[] { effect });
        }

        private static MMOAbilityDefinition GetOrCreateBuffAbility(
            string assetName,
            string abilityId,
            string displayName,
            string description,
            float cooldown,
            float duration,
            int attackPowerBonus,
            float attackPowerMultiplier,
            float attackSpeedMultiplier,
            float healthRegenMultiplier)
        {
            return GetOrCreateBuffAbility(
                assetName,
                abilityId,
                displayName,
                description,
                cooldown,
                duration,
                attackPowerBonus,
                attackPowerMultiplier,
                attackSpeedMultiplier,
                healthRegenMultiplier,
                1f,
                0f);
        }

        private static MMOAbilityDefinition GetOrCreateBuffAbility(
            string assetName,
            string abilityId,
            string displayName,
            string description,
            float cooldown,
            float duration,
            int attackPowerBonus,
            float attackPowerMultiplier,
            float attackSpeedMultiplier,
            float healthRegenMultiplier,
            float manaRegenMultiplier,
            float damageTakenAsManaPercent)
        {
            MMOAbilityEffectDefinition effect = new();
            effect.ConfigureTemporaryStatModifier(duration, attackPowerBonus, attackPowerMultiplier, attackSpeedMultiplier, healthRegenMultiplier, manaRegenMultiplier, damageTakenAsManaPercent);
            return GetOrCreateAbility(assetName, abilityId, displayName, description, MMOAbilityTargetType.Self, false, false, 0f, cooldown, 0, 0f, false, false, new[] { effect });
        }

        private static MMOAbilityCatalog CreateAbilityCatalog(IEnumerable<MMOAbilityDefinition> abilities)
        {
            string path = $"{AbilityFolder}/Starter_Ability_Catalog.asset";
            MMOAbilityCatalog catalog = AssetDatabase.LoadAssetAtPath<MMOAbilityCatalog>(path);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<MMOAbilityCatalog>();
                AssetDatabase.CreateAsset(catalog, path);
            }

            catalog.Configure(abilities);
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static MMOAbilityDefinition GetOrCreateAbility(
            string assetName,
            string abilityId,
            string displayName,
            string description,
            MMOAbilityTargetType targetType,
            bool autoAttack,
            bool toggled,
            float range,
            float cooldown,
            int manaCost,
            float castTime,
            bool interruptOnMovement,
            bool fallbackSelf,
            IEnumerable<MMOAbilityEffectDefinition> effects)
        {
            string path = $"{AbilityFolder}/{assetName}.asset";
            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(path);
            if (ability == null)
            {
                ability = ScriptableObject.CreateInstance<MMOAbilityDefinition>();
                AssetDatabase.CreateAsset(ability, path);
            }

            ability.Configure(abilityId, displayName, description, targetType, autoAttack, toggled, range, cooldown, manaCost, castTime, interruptOnMovement, fallbackSelf, effects);
            EditorUtility.SetDirty(ability);
            return ability;
        }

        private static ProgressionSet CreateProgression()
        {
            return new ProgressionSet
            {
                Warrior = CreateProgression("Warrior_Level_Progression", CreateStatGrowth(3, 3, 1, 0, 1, 2, 2, 0, 0.35f, 0.55f)),
                Mage = CreateProgression("Mage_Level_Progression", CreateStatGrowth(1, 0, 1, 3, 3, 0, 0, 3, 0.05f, 0.1f)),
                Shaman = CreateProgression("Shaman_Level_Progression", CreateStatGrowth(2, 2, 1, 2, 2, 1, 1, 2, 0.2f, 0.35f))
            };
        }

        private static MMOLevelProgressionDefinition CreateProgression(string assetName, MMOCharacterStatGrowth growth)
        {
            string path = $"{ProgressionFolder}/{assetName}.asset";
            MMOLevelProgressionDefinition progression = AssetDatabase.LoadAssetAtPath<MMOLevelProgressionDefinition>(path);
            if (progression == null)
            {
                progression = ScriptableObject.CreateInstance<MMOLevelProgressionDefinition>();
                AssetDatabase.CreateAsset(progression, path);
            }

            progression.Configure(60, 400, 110, 1.12f, growth);
            EditorUtility.SetDirty(progression);
            return progression;
        }

        private static MMOCharacterStatGrowth CreateStatGrowth(int stamina, int strength, int agility, int intellect, int spirit, int armor, int attackPower, int spellPower, float minDamage, float maxDamage)
        {
            MMOCharacterStatGrowth growth = new();
            growth.Configure(stamina, strength, agility, intellect, spirit, armor, attackPower, spellPower, minDamage, maxDamage);
            return growth;
        }

        private static MMOCharacterArchetypeCatalog CreateArchetypes(AbilitySet abilities, ProgressionSet progression)
        {
            List<MMOCharacterArchetypeDefinition> archetypes = new();
            foreach (MMOPlayableRace race in Enum.GetValues(typeof(MMOPlayableRace)))
            {
                foreach (MMOPlayableClass characterClass in Enum.GetValues(typeof(MMOPlayableClass)))
                {
                    archetypes.Add(CreateArchetype(race, characterClass, abilities, progression));
                }
            }

            string catalogPath = $"{ArchetypeFolder}/Playable_Archetype_Catalog.asset";
            MMOCharacterArchetypeCatalog catalog = AssetDatabase.LoadAssetAtPath<MMOCharacterArchetypeCatalog>(catalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<MMOCharacterArchetypeCatalog>();
                AssetDatabase.CreateAsset(catalog, catalogPath);
            }

            catalog.Configure(archetypes);
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static MMOCharacterArchetypeDefinition CreateArchetype(MMOPlayableRace race, MMOPlayableClass characterClass, AbilitySet abilities, ProgressionSet progression)
        {
            string assetName = $"{race}_{characterClass}";
            MMOCharacterProfile profile = CreateProfile(race, characterClass);
            string path = $"{ArchetypeFolder}/{assetName}.asset";
            MMOCharacterArchetypeDefinition archetype = AssetDatabase.LoadAssetAtPath<MMOCharacterArchetypeDefinition>(path);
            if (archetype == null)
            {
                archetype = ScriptableObject.CreateInstance<MMOCharacterArchetypeDefinition>();
                AssetDatabase.CreateAsset(archetype, path);
            }

            MMOAbilityDefinition racial = race == MMOPlayableRace.Orc ? abilities.OrcRacial : abilities.TrollRacial;
            MMOAbilityDefinition classAbility = characterClass switch
            {
                MMOPlayableClass.Mage => abilities.Fireball,
                MMOPlayableClass.Shaman => abilities.HealingBeam,
                _ => abilities.Bash
            };

            MMOLevelProgressionDefinition selectedProgression = characterClass switch
            {
                MMOPlayableClass.Mage => progression.Mage,
                MMOPlayableClass.Shaman => progression.Shaman,
                _ => progression.Warrior
            };

            archetype.Configure(
                race,
                characterClass,
                $"{race} {characterClass}",
                race == MMOPlayableRace.Orc
                    ? "Orcs are forceful frontline fighters. Blood Fury increases attack power and attack speed for a short time."
                    : "Trolls are resilient survivors. Regeneration greatly increases health recovery for a short time.",
                characterClass switch
                {
                    MMOPlayableClass.Mage => "Mages are pure casters with high intellect growth and Fireball as their ranged damage spell.",
                    MMOPlayableClass.Shaman => "Shaman mix melee and magic growth and can cast Healing Beam on allies or themselves.",
                    _ => "Warriors are pure melee combatants with high stamina, strength, armor, and Bash."
                },
                GetTint(race, characterClass),
                profile,
                selectedProgression,
                racial,
                classAbility,
                new[] { abilities.AutoAttack, racial, classAbility });
            EditorUtility.SetDirty(archetype);
            return archetype;
        }

        private static MMOCharacterProfile CreateProfile(MMOPlayableRace race, MMOPlayableClass characterClass)
        {
            string path = $"{CharacterFolder}/{race}_{characterClass}.asset";
            MMOCharacterProfile profile = AssetDatabase.LoadAssetAtPath<MMOCharacterProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<MMOCharacterProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            MMOCharacterStats stats = characterClass switch
            {
                MMOPlayableClass.Mage => CreateStats(8, 4, 7, 7, 8, 4, 2, 8, 2f, 4f, 2.4f, 3f),
                MMOPlayableClass.Shaman => CreateStats(11, 10, 8, 6, 8, 12, 8, 5, 4f, 7f, 2.2f, 3f),
                _ => CreateStats(14, 14, 9, 4, 8, 22, 12, 0, 5f, 9f, 2.1f, 3f)
            };

            if (race == MMOPlayableRace.Orc)
            {
                stats.AddValues(1, 2, 0, 0, 0, 1, 1, 0, 0.2f, 0.2f);
            }
            else
            {
                stats.AddValues(1, 0, 2, 0, 1, 0, 0, 0, 0f, 0.1f);
            }

            int baseHealth = characterClass == MMOPlayableClass.Mage ? 80 : characterClass == MMOPlayableClass.Shaman ? 105 : 125;
            int baseMana = characterClass == MMOPlayableClass.Warrior ? 0 : characterClass == MMOPlayableClass.Mage ? 35 : 30;
            profile.Configure($"{race} {characterClass}", 1, baseHealth, baseMana, GetTint(race, characterClass), null, true, MMOEntityFaction.Player, stats);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static MMOCharacterStats CreateStats(int stamina, int strength, int agility, int intellect, int spirit, int armor, int attackPower, int spellPower, float minDamage, float maxDamage, float speed, float range)
        {
            MMOCharacterStats stats = new();
            stats.Configure(stamina, strength, agility, intellect, spirit, armor, attackPower, spellPower, minDamage, maxDamage, speed, range);
            return stats;
        }

        private static Color GetTint(MMOPlayableRace race, MMOPlayableClass characterClass)
        {
            Color baseColor = race == MMOPlayableRace.Orc ? new Color(0.34f, 0.58f, 0.28f) : new Color(0.22f, 0.62f, 0.68f);
            Color classColor = characterClass switch
            {
                MMOPlayableClass.Mage => new Color(0.36f, 0.58f, 1f),
                MMOPlayableClass.Shaman => new Color(0.58f, 0.42f, 0.95f),
                _ => new Color(0.86f, 0.56f, 0.34f)
            };

            return Color.Lerp(baseColor, classColor, 0.28f);
        }

        private static void CreateCharacterSelectionScene(MMOCharacterArchetypeCatalog catalog)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject cameraObject = new("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 1.8f, -8f);
            cameraObject.transform.rotation = Quaternion.Euler(8f, 0f, 0f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 42f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.045f, 0.035f);
            cameraObject.AddComponent<AudioListener>();

            GameObject lightObject = new("Key Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 2.2f;
            light.color = new Color(1f, 0.84f, 0.64f);
            lightObject.transform.rotation = Quaternion.Euler(42f, -28f, 0f);

            GameObject previewRoot = new("Character Preview Root");
            previewRoot.transform.position = new Vector3(0f, 0.95f, 0f);

            Canvas canvas = CreateCanvas("Character Select Canvas", 100);
            EnsureEventSystem();

            GameObject controllerObject = new("Character Selection Controller");
            MMOCharacterSelectionController controller = controllerObject.AddComponent<MMOCharacterSelectionController>();
            controller.Configure(catalog, GameplaySceneName);
            SetObjectReference(controller, "previewRoot", previewRoot.transform);
            SetObjectReference(controller, "previewCamera", camera);

            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), CharacterSelectionScenePath);
            AddSceneToBuildSettings(CharacterSelectionScenePath, 0);
        }

        private static void ConfigureGameplayScene(MMOCharacterArchetypeCatalog catalog, MMOAbilityCatalog abilityCatalog)
        {
            if (!File.Exists(AssetPathToFullPath(GameplayScenePath)))
            {
                AddSceneToBuildSettings(GameplayScenePath, 1);
                return;
            }

            EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            GameObject player = GameObject.FindGameObjectWithTag("Player") ?? GameObject.Find("Player");
            if (player != null)
            {
                MMOCharacterPersistenceAgent persistence = player.GetComponent<MMOCharacterPersistenceAgent>();
                if (persistence == null)
                {
                    persistence = player.AddComponent<MMOCharacterPersistenceAgent>();
                }

                persistence.SetArchetypeCatalog(catalog);
                persistence.SetItemCatalog(AssetDatabase.LoadAssetAtPath<MMOItemCatalog>($"{ItemFolder}/Starter_Item_Catalog.asset"));
                persistence.SetAbilityCatalog(abilityCatalog);
                EditorUtility.SetDirty(persistence);

                Component redirector = EnsureComponentByTypeName(player, "RPGClone.CharacterSelection.MMOCharacterSelectionRedirector");
                if (redirector != null)
                {
                    redirector.GetType().GetMethod("Configure")?.Invoke(redirector, new object[] { CharacterSelectionSceneName, true });
                    EditorUtility.SetDirty(redirector);
                }

                Component returnController = EnsureComponentByTypeName(player, "RPGClone.CharacterSelection.MMOReturnToCharacterSelectionController");
                if (returnController != null)
                {
                    returnController.GetType().GetMethod("Configure")?.Invoke(returnController, new object[] { CharacterSelectionSceneName, persistence });
                    EditorUtility.SetDirty(returnController);
                }
            }

            Canvas canvas = FindOrCreateHudCanvas();
            GameObject castBarObject = canvas.transform.Find("Cast Bar")?.gameObject ?? new GameObject("Cast Bar", typeof(RectTransform));
            castBarObject.transform.SetParent(canvas.transform, false);
            MMOCastBarPresenter castBar = castBarObject.GetComponent<MMOCastBarPresenter>();
            if (castBar == null)
            {
                castBar = castBarObject.AddComponent<MMOCastBarPresenter>();
            }

            if (player != null)
            {
                MMOInteractionCastController interactionCastController = player.GetComponent<MMOInteractionCastController>();
                if (interactionCastController == null)
                {
                    interactionCastController = player.AddComponent<MMOInteractionCastController>();
                    EditorUtility.SetDirty(interactionCastController);
                }

                castBar.Configure(player.GetComponent<MMOAbilitySystem>(), interactionCastController);
            }

            EditorUtility.SetDirty(castBar);
            EnsureAbilityTooltip(canvas.transform);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AddSceneToBuildSettings(GameplayScenePath, 1);
            EditorSceneManager.OpenScene(CharacterSelectionScenePath, OpenSceneMode.Single);
        }

        private static MMOAbilityTooltipPresenter EnsureAbilityTooltip(Transform canvas)
        {
            Transform existing = canvas.Find("Ability Tooltip");
            GameObject tooltipObject = existing != null ? existing.gameObject : new GameObject("Ability Tooltip", typeof(RectTransform));
            tooltipObject.transform.SetParent(canvas, false);

            MMOAbilityTooltipPresenter presenter = tooltipObject.GetComponent<MMOAbilityTooltipPresenter>();
            if (presenter == null)
            {
                presenter = tooltipObject.AddComponent<MMOAbilityTooltipPresenter>();
            }

            EditorUtility.SetDirty(presenter);
            return presenter;
        }

        private static Canvas CreateCanvas(string name, int sortingOrder)
        {
            GameObject canvasObject = new(name);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static Canvas FindOrCreateHudCanvas()
        {
            GameObject existing = GameObject.Find("HUD Canvas");
            if (existing != null && existing.TryGetComponent(out Canvas canvas))
            {
                return canvas;
            }

            return CreateCanvas("HUD Canvas", 50);
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null)
            {
                return;
            }

            GameObject eventSystemObject = new("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }

        private static void SetObjectReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            SerializedObject serializedObject = new(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Component EnsureComponentByTypeName(GameObject target, string typeName)
        {
            Type componentType = FindType(typeName);
            if (componentType == null || !typeof(Component).IsAssignableFrom(componentType))
            {
                return null;
            }

            return target.GetComponent(componentType) ?? target.AddComponent(componentType);
        }

        private static Type FindType(string typeName)
        {
            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(typeName);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static void AddSceneToBuildSettings(string scenePath, int preferredIndex)
        {
            List<EditorBuildSettingsScene> scenes = new(EditorBuildSettings.scenes);
            scenes.RemoveAll(scene => scene.path == scenePath);
            EditorBuildSettingsScene buildScene = new(scenePath, true);
            int index = Mathf.Clamp(preferredIndex, 0, scenes.Count);
            scenes.Insert(index, buildScene);
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static string AssetPathToFullPath(string assetPath)
        {
            return Path.Combine(Application.dataPath, assetPath["Assets/".Length..]).Replace('\\', '/');
        }

        private sealed class AbilitySet
        {
            public MMOAbilityDefinition AutoAttack;
            public MMOAbilityDefinition OrcRacial;
            public MMOAbilityDefinition TrollRacial;
            public MMOAbilityDefinition Bash;
            public MMOAbilityDefinition Fireball;
            public MMOAbilityDefinition HealingBeam;
            public MMOAbilityDefinition Berzerkitis;
            public MMOAbilityDefinition Charge;
            public MMOAbilityDefinition MageArmor;
            public MMOAbilityDefinition FireBlast;
            public MMOAbilityDefinition WaterShield;
            public MMOAbilityDefinition LightningBolt;

            public MMOAbilityDefinition[] All => new[]
            {
                AutoAttack,
                OrcRacial,
                TrollRacial,
                Bash,
                Fireball,
                HealingBeam,
                Berzerkitis,
                Charge,
                MageArmor,
                FireBlast,
                WaterShield,
                LightningBolt
            };
        }

        private sealed class ProgressionSet
        {
            public MMOLevelProgressionDefinition Warrior;
            public MMOLevelProgressionDefinition Mage;
            public MMOLevelProgressionDefinition Shaman;
        }
    }
}
