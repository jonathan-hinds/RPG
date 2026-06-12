using System.Collections.Generic;
using RPGClone.Abilities;
using RPGClone.Characters;
using RPGClone.Trainers;
using UnityEditor;
using UnityEngine;

namespace RPGClone.EditorTools
{
    public static class MMOAbilityContentInstaller
    {
        private const string RootFolder = "Assets/_Project";
        private const string ConfigFolder = RootFolder + "/Configs";
        private const string AbilityFolder = ConfigFolder + "/Abilities";
        private const string ResourcesFolder = "Assets/Resources";
        private const string TrainerOfferFolder = ResourcesFolder + "/RPGClone";
        private const string TrainerOfferCatalogPath = TrainerOfferFolder + "/Starter_Trainer_Offer_Catalog.asset";

        [MenuItem("Tools/RPG Clone/Abilities/Install Starter Ability Content")]
        public static void InstallStarterAbilityContent()
        {
            EnsureFolders();

            MMOAbilityDefinition thunderclap = GetOrCreateThunderclap();
            MMOAbilityDefinition flamestrike = GetOrCreateFlamestrike();
            MMOAbilityDefinition frostShock = GetOrCreateFrostShock();

            UpdateAbilityCatalog(new[] { thunderclap, flamestrike, frostShock });
            UpdateTrainerOfferCatalog(thunderclap, flamestrike, frostShock);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Starter ability content installed without modifying scene content.");
        }

        private static MMOAbilityDefinition GetOrCreateThunderclap()
        {
            MMOAbilityDefinition ability = GetOrCreateAbility("Warrior_Thunderclap");
            MMOAbilityEffectDefinition damage = new();
            damage.Configure(MMOAbilityEffectType.Damage, MMOAbilityAmountSource.AttackPower, MMODamageSchool.Physical, 8f, 0.25f);

            MMOAbilityEffectDefinition attackSpeedDebuff = new();
            attackSpeedDebuff.ConfigureTemporaryStatModifier(10f, 0, 1f, 0.75f, 1f, 1f, 0f, 1f, true);

            ability.Configure(
                "warrior_thunderclap",
                "Thunderclap",
                "Blasts nearby enemies with physical damage and reduces their attack speed.",
                MMOAbilityTargetType.Self,
                false,
                false,
                0f,
                6f,
                0,
                0f,
                false,
                false,
                6f,
                MMOAbilityAreaTargetFilter.Hostile,
                new[] { damage, attackSpeedDebuff });
            EditorUtility.SetDirty(ability);
            return ability;
        }

        private static MMOAbilityDefinition GetOrCreateFlamestrike()
        {
            MMOAbilityDefinition ability = GetOrCreateAbility("Mage_Flamestrike");
            MMOAbilityEffectDefinition impact = new();
            impact.Configure(MMOAbilityEffectType.Damage, MMOAbilityAmountSource.SpellPower, MMODamageSchool.Fire, 18f, 0.35f);

            MMOAbilityEffectDefinition burning = new();
            burning.ConfigurePeriodicDamage(8f, 2f, MMOAbilityAmountSource.SpellPower, MMODamageSchool.Fire, 24f, 0.45f);

            ability.Configure(
                "mage_flamestrike",
                "Flamestrike",
                "Calls down a pillar of flame at the targeted area, then burns afflicted enemies over time.",
                MMOAbilityTargetType.GroundArea,
                false,
                false,
                30f,
                12f,
                24,
                2f,
                true,
                false,
                5f,
                MMOAbilityAreaTargetFilter.Hostile,
                new[] { impact, burning });
            EditorUtility.SetDirty(ability);
            return ability;
        }

        private static MMOAbilityDefinition GetOrCreateFrostShock()
        {
            MMOAbilityDefinition ability = GetOrCreateAbility("Shaman_Frost_Shock");
            MMOAbilityEffectDefinition damage = new();
            damage.Configure(MMOAbilityEffectType.Damage, MMOAbilityAmountSource.SpellPower, MMODamageSchool.Frost, 20f, 0.5f);

            MMOAbilityEffectDefinition snare = new();
            snare.ConfigureTemporaryStatModifier(6f, 0, 1f, 1f, 1f, 1f, 0f, 0.5f, true);

            ability.Configure(
                "shaman_frost_shock",
                "Frost Shock",
                "Shocks a hostile target with frost damage and reduces movement speed.",
                MMOAbilityTargetType.Hostile,
                false,
                false,
                20f,
                6f,
                16,
                0f,
                false,
                false,
                new[] { damage, snare });
            EditorUtility.SetDirty(ability);
            return ability;
        }

        private static MMOAbilityDefinition GetOrCreateAbility(string assetName)
        {
            string path = $"{AbilityFolder}/{assetName}.asset";
            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(path);
            if (ability != null)
            {
                return ability;
            }

            ability = ScriptableObject.CreateInstance<MMOAbilityDefinition>();
            AssetDatabase.CreateAsset(ability, path);
            return ability;
        }

        private static void UpdateAbilityCatalog(IEnumerable<MMOAbilityDefinition> newAbilities)
        {
            string path = $"{AbilityFolder}/Starter_Ability_Catalog.asset";
            MMOAbilityCatalog catalog = AssetDatabase.LoadAssetAtPath<MMOAbilityCatalog>(path);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<MMOAbilityCatalog>();
                AssetDatabase.CreateAsset(catalog, path);
            }

            List<MMOAbilityDefinition> abilities = new(catalog.Abilities);
            AddExistingAbility(abilities, "Auto_Attack");
            AddExistingAbility(abilities, "Orc_Blood_Fury");
            AddExistingAbility(abilities, "Troll_Regeneration");
            AddExistingAbility(abilities, "Warrior_Bash");
            AddExistingAbility(abilities, "Mage_Fireball");
            AddExistingAbility(abilities, "Shaman_Healing_Beam");
            AddExistingAbility(abilities, "Warrior_Berzerkitis");
            AddExistingAbility(abilities, "Warrior_Charge");
            AddExistingAbility(abilities, "Mage_Mage_Armor");
            AddExistingAbility(abilities, "Mage_Fire_Blast");
            AddExistingAbility(abilities, "Shaman_Water_Shield");
            AddExistingAbility(abilities, "Shaman_Lightning_Bolt");

            foreach (MMOAbilityDefinition ability in newAbilities)
            {
                AddAbility(abilities, ability);
            }

            catalog.Configure(abilities);
            EditorUtility.SetDirty(catalog);
        }

        private static void UpdateTrainerOfferCatalog(
            MMOAbilityDefinition thunderclap,
            MMOAbilityDefinition flamestrike,
            MMOAbilityDefinition frostShock)
        {
            MMOTrainerOfferCatalog catalog = AssetDatabase.LoadAssetAtPath<MMOTrainerOfferCatalog>(TrainerOfferCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<MMOTrainerOfferCatalog>();
                AssetDatabase.CreateAsset(catalog, TrainerOfferCatalogPath);
            }

            List<MMOTrainerOfferEntry> offers = new();
            AddOffer(offers, "Warrior_Berzerkitis", MMOPlayableClass.Warrior, 3, 75);
            AddOffer(offers, "Warrior_Charge", MMOPlayableClass.Warrior, 3, 75);
            AddOffer(offers, thunderclap, MMOPlayableClass.Warrior, 5, 125);
            AddOffer(offers, "Mage_Mage_Armor", MMOPlayableClass.Mage, 3, 75);
            AddOffer(offers, "Mage_Fire_Blast", MMOPlayableClass.Mage, 3, 75);
            AddOffer(offers, flamestrike, MMOPlayableClass.Mage, 5, 125);
            AddOffer(offers, "Shaman_Water_Shield", MMOPlayableClass.Shaman, 3, 75);
            AddOffer(offers, "Shaman_Lightning_Bolt", MMOPlayableClass.Shaman, 3, 75);
            AddOffer(offers, frostShock, MMOPlayableClass.Shaman, 5, 125);

            catalog.Configure(offers);
            EditorUtility.SetDirty(catalog);
        }

        private static void AddExistingAbility(List<MMOAbilityDefinition> abilities, string assetName)
        {
            AddAbility(abilities, AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>($"{AbilityFolder}/{assetName}.asset"));
        }

        private static void AddAbility(List<MMOAbilityDefinition> abilities, MMOAbilityDefinition ability)
        {
            if (ability != null && !abilities.Contains(ability))
            {
                abilities.Add(ability);
            }
        }

        private static void AddOffer(List<MMOTrainerOfferEntry> offers, string abilityAssetName, MMOPlayableClass requiredClass, int requiredLevel, int priceCopper)
        {
            AddOffer(
                offers,
                AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>($"{AbilityFolder}/{abilityAssetName}.asset"),
                requiredClass,
                requiredLevel,
                priceCopper);
        }

        private static void AddOffer(List<MMOTrainerOfferEntry> offers, MMOAbilityDefinition ability, MMOPlayableClass requiredClass, int requiredLevel, int priceCopper)
        {
            if (ability != null)
            {
                offers.Add(new MMOTrainerOfferEntry(ability, requiredClass, requiredLevel, priceCopper));
            }
        }

        private static void EnsureFolders()
        {
            CreateFolderIfMissing(RootFolder);
            CreateFolderIfMissing(ConfigFolder);
            CreateFolderIfMissing(AbilityFolder);
            CreateFolderIfMissing(ResourcesFolder);
            CreateFolderIfMissing(TrainerOfferFolder);
        }

        private static void CreateFolderIfMissing(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/");
            string folderName = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                CreateFolderIfMissing(parent);
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }
    }
}
