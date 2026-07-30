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
        private const string AbilityIconFolder = RootFolder + "/UI/Icons/Abilities";
        private const string EmpowerWeaponIconPath = AbilityIconFolder + "/Buffs/buffs_shaman_empower_weapon_256.png";
        private const string FrostWaveIconPath = AbilityIconFolder + "/Active/abilities_mage_frost_wave_256.png";
        private const string PressTheAttackIconPath = AbilityIconFolder + "/Buffs/buffs_warrior_press_the_attack_256.png";

        [MenuItem("Tools/RPG Clone/Abilities/Install Starter Ability Content")]
        public static void InstallStarterAbilityContent()
        {
            EnsureFolders();

            MMOAbilityDefinition thunderclap = GetOrCreateThunderclap();
            MMOAbilityDefinition flamestrike = GetOrCreateFlamestrike();
            MMOAbilityDefinition frostShock = GetOrCreateFrostShock();
            MMOAbilityDefinition gouge = GetOrCreateGouge();
            MMOAbilityDefinition arcaneMissile = GetOrCreateArcaneMissile();
            MMOAbilityDefinition earthquake = GetOrCreateEarthquake();
            MMOAbilityDefinition empowerWeapon = GetOrCreateEmpowerWeapon();
            MMOAbilityDefinition frostWave = GetOrCreateFrostWave();
            MMOAbilityDefinition pressTheAttack = GetOrCreatePressTheAttack();

            UpdateAbilityCatalog(new[]
            {
                thunderclap,
                flamestrike,
                frostShock,
                gouge,
                arcaneMissile,
                earthquake,
                empowerWeapon,
                frostWave,
                pressTheAttack
            });
            UpdateTrainerOfferCatalog(
                thunderclap,
                flamestrike,
                frostShock,
                gouge,
                arcaneMissile,
                earthquake,
                empowerWeapon,
                frostWave,
                pressTheAttack);

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
            ability.SetAnimationStyle(MMOAbilityAnimationStyle.WeaponAttack);
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
            ability.SetAnimationStyle(MMOAbilityAnimationStyle.SpellCast);
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
            ability.SetAnimationStyle(MMOAbilityAnimationStyle.SpellCast);
            EditorUtility.SetDirty(ability);
            return ability;
        }

        private static MMOAbilityDefinition GetOrCreateGouge()
        {
            MMOAbilityDefinition ability = GetOrCreateAbility("Warrior_Gouge");
            MMOAbilityEffectDefinition strike = new();
            strike.Configure(MMOAbilityEffectType.Damage, MMOAbilityAmountSource.WeaponDamage, MMODamageSchool.Physical, 4f, 0.85f);

            MMOAbilityEffectDefinition bleed = new();
            bleed.ConfigurePeriodicDamage(9f, 3f, MMOAbilityAmountSource.AttackPower, MMODamageSchool.Physical, 18f, 0.25f, 3);

            ability.Configure(
                "warrior_gouge",
                "Gouge",
                "Cuts a hostile target for weapon damage and applies a stacking bleed. Critical hits reset Gouge's cooldown.",
                MMOAbilityTargetType.Hostile,
                false,
                false,
                3f,
                10f,
                0,
                0f,
                false,
                false,
                false,
                true,
                0f,
                MMOAbilityAreaTargetFilter.Hostile,
                new[] { strike, bleed });
            ability.SetAnimationStyle(MMOAbilityAnimationStyle.WeaponAttack);
            EditorUtility.SetDirty(ability);
            return ability;
        }

        private static MMOAbilityDefinition GetOrCreateArcaneMissile()
        {
            MMOAbilityDefinition ability = GetOrCreateAbility("Mage_Arcane_Missile");
            MMOAbilityEffectDefinition arcaneDamage = new();
            arcaneDamage.ConfigurePeriodicDamage(5f, 1f, MMOAbilityAmountSource.SpellPower, MMODamageSchool.Arcane, 42f, 0.85f);

            ability.Configure(
                "mage_arcane_missile",
                "Arcane Missile",
                "Channels arcane missiles into a hostile target, dealing damage over the channel.",
                MMOAbilityTargetType.Hostile,
                false,
                false,
                30f,
                0f,
                28,
                5f,
                true,
                true,
                false,
                false,
                0f,
                MMOAbilityAreaTargetFilter.Hostile,
                new[] { arcaneDamage });
            ability.SetAnimationStyle(MMOAbilityAnimationStyle.SpellCast);
            EditorUtility.SetDirty(ability);
            return ability;
        }

        private static MMOAbilityDefinition GetOrCreateEarthquake()
        {
            MMOAbilityDefinition ability = GetOrCreateAbility("Shaman_Earthquake");
            MMOAbilityEffectDefinition damage = new();
            damage.Configure(MMOAbilityEffectType.Damage, MMOAbilityAmountSource.SpellPower, MMODamageSchool.Nature, 24f, 0.45f);

            ability.Configure(
                "shaman_earthquake",
                "Earthquake",
                "Shakes the ground around the caster, damaging nearby enemies.",
                MMOAbilityTargetType.Self,
                false,
                false,
                0f,
                10f,
                22,
                0f,
                false,
                false,
                false,
                false,
                6f,
                MMOAbilityAreaTargetFilter.Hostile,
                new[] { damage });
            ability.SetAnimationStyle(MMOAbilityAnimationStyle.SpellCast);
            EditorUtility.SetDirty(ability);
            return ability;
        }

        private static MMOAbilityDefinition GetOrCreateEmpowerWeapon()
        {
            MMOAbilityDefinition ability = GetOrCreateAbility("Shaman_Empower_Weapon");
            MMOAbilityEffectDefinition empowerment = new();
            empowerment.ConfigureWeaponEmpowerment(300f, 0.10f);

            ability.Configure(
                "shaman_empower_weapon",
                "Empower Weapon",
                "Empowers your weapon with elemental energy, causing your melee attacks to deal additional damage equal to 10% of your maximum Mana.",
                MMOAbilityTargetType.Self,
                false,
                false,
                0f,
                0f,
                0,
                0f,
                false,
                false,
                new[] { empowerment });
            ability.SetMaximumManaCost(0.20f);
            ability.SetAnimationStyle(MMOAbilityAnimationStyle.SpellCast);
            ability.SetVisualEffects(null);
            ability.SetIcon(AssetDatabase.LoadAssetAtPath<Sprite>(EmpowerWeaponIconPath));
            EditorUtility.SetDirty(ability);
            return ability;
        }

        private static MMOAbilityDefinition GetOrCreateFrostWave()
        {
            MMOAbilityDefinition ability = GetOrCreateAbility("Mage_Frost_Wave");
            MMOAbilityEffectDefinition damage = new();
            damage.Configure(
                MMOAbilityEffectType.Damage,
                MMOAbilityAmountSource.SpellPower,
                MMODamageSchool.Frost,
                34f,
                0.42f);

            MMOAbilityEffectDefinition freeze = new();
            freeze.ConfigureMovementPrevention(3f);

            ability.Configure(
                "mage_frost_wave",
                "Frost Wave",
                "Unleashes a wave of frost from beneath the caster, striking all enemies within 8 yards for 34 + 42% of Spell Power Frost damage and freezing them in place for 3 sec.",
                MMOAbilityTargetType.Self,
                false,
                false,
                0f,
                20f,
                0,
                0f,
                false,
                false,
                false,
                false,
                8f,
                MMOAbilityAreaTargetFilter.Hostile,
                new[] { damage, freeze });
            ability.SetAnimationStyle(MMOAbilityAnimationStyle.SpellCast);
            ability.SetVisualEffects(null);
            ability.SetIcon(AssetDatabase.LoadAssetAtPath<Sprite>(FrostWaveIconPath));
            EditorUtility.SetDirty(ability);
            return ability;
        }

        private static MMOAbilityDefinition GetOrCreatePressTheAttack()
        {
            MMOAbilityDefinition ability = GetOrCreateAbility("Warrior_Press_The_Attack");
            MMOAbilityEffectDefinition aggressiveStance = new();
            aggressiveStance.ConfigureTemporaryStatModifier(
                6f,
                0,
                1f,
                1.15f,
                1f,
                1f,
                0f,
                1.20f,
                false);

            ability.Configure(
                "warrior_press_the_attack",
                "Press the Attack",
                "Enter an aggressive stance for 6 sec, increasing your movement speed by 20% and your attack speed by 15%.",
                MMOAbilityTargetType.Self,
                false,
                false,
                0f,
                30f,
                0,
                0f,
                false,
                false,
                new[] { aggressiveStance });
            ability.SetAnimationStyle(MMOAbilityAnimationStyle.WeaponAttack);
            ability.SetVisualEffects(null);
            ability.SetIcon(AssetDatabase.LoadAssetAtPath<Sprite>(PressTheAttackIconPath));
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
            MMOAbilityDefinition frostShock,
            MMOAbilityDefinition gouge,
            MMOAbilityDefinition arcaneMissile,
            MMOAbilityDefinition earthquake,
            MMOAbilityDefinition empowerWeapon,
            MMOAbilityDefinition frostWave,
            MMOAbilityDefinition pressTheAttack)
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
            AddOffer(offers, gouge, MMOPlayableClass.Warrior, 7, 200);
            AddOffer(offers, pressTheAttack, MMOPlayableClass.Warrior, 8, 250);
            AddOffer(offers, "Mage_Mage_Armor", MMOPlayableClass.Mage, 3, 75);
            AddOffer(offers, "Mage_Fire_Blast", MMOPlayableClass.Mage, 3, 75);
            AddOffer(offers, flamestrike, MMOPlayableClass.Mage, 5, 125);
            AddOffer(offers, arcaneMissile, MMOPlayableClass.Mage, 7, 200);
            AddOffer(offers, frostWave, MMOPlayableClass.Mage, 8, 250);
            AddOffer(offers, "Shaman_Water_Shield", MMOPlayableClass.Shaman, 3, 75);
            AddOffer(offers, "Shaman_Lightning_Bolt", MMOPlayableClass.Shaman, 3, 75);
            AddOffer(offers, frostShock, MMOPlayableClass.Shaman, 5, 125);
            AddOffer(offers, earthquake, MMOPlayableClass.Shaman, 7, 200);
            AddOffer(offers, empowerWeapon, MMOPlayableClass.Shaman, 8, 250);

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
