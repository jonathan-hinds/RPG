#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using RPGClone.Abilities;
using RPGClone.Buffs;
using RPGClone.Characters;
using RPGClone.Trainers;
using RPGClone.UI;
using UnityEditor;
using UnityEngine;

namespace RPGClone.EditorTests
{
    public sealed class MMOLevelEightAbilityTests
    {
        private const string AbilityRoot = "Assets/_Project/Configs/Abilities/";
        private const string CatalogPath = AbilityRoot + "Starter_Ability_Catalog.asset";
        private const string TrainerCatalogPath = "Assets/Resources/RPGClone/Starter_Trainer_Offer_Catalog.asset";

        [Test]
        public void LevelEightAbilities_AreCatalogedAndOfferedToTheirClasses()
        {
            MMOAbilityCatalog abilityCatalog = AssetDatabase.LoadAssetAtPath<MMOAbilityCatalog>(CatalogPath);
            MMOTrainerOfferCatalog trainerCatalog = AssetDatabase.LoadAssetAtPath<MMOTrainerOfferCatalog>(TrainerCatalogPath);

            Assert.That(abilityCatalog, Is.Not.Null);
            Assert.That(trainerCatalog, Is.Not.Null);
            AssertLevelEightOffer(abilityCatalog, trainerCatalog, "Shaman_Empower_Weapon", MMOPlayableClass.Shaman);
            AssertLevelEightOffer(abilityCatalog, trainerCatalog, "Mage_Frost_Wave", MMOPlayableClass.Mage);
            AssertLevelEightOffer(abilityCatalog, trainerCatalog, "Warrior_Press_The_Attack", MMOPlayableClass.Warrior);
        }

        [Test]
        public void EmpowerWeapon_UsesCalculatedManaCostAndMaximumManaMeleeScaling()
        {
            MMOAbilityDefinition ability = LoadAbility("Shaman_Empower_Weapon");
            GameObject casterObject = new("Empower Weapon Test Caster");

            try
            {
                MMOCharacterIdentity caster = casterObject.AddComponent<MMOCharacterIdentity>();
                MMOCharacterBuffController buffs = casterObject.AddComponent<MMOCharacterBuffController>();
                caster.Mana.Configure(137, 137);

                Assert.That(ability.ManaCostSource, Is.EqualTo(MMOAbilityManaCostSource.MaximumManaPercentage));
                Assert.That(ability.MaximumManaCostPercent, Is.EqualTo(0.20f).Within(0.001f));
                Assert.That(ability.CalculateManaCost(caster), Is.EqualTo(28));
                Assert.That(
                    MMOTooltipContentBuilder.BuildAbility(ability, caster).Lines.Any(line => line.Text == "28 Mana"),
                    Is.True,
                    "The tooltip must show the caster-specific numeric Mana cost.");

                Assert.That(buffs.ApplyTemporaryModifiers(ability, null), Is.True);
                Assert.That(buffs.CalculateMeleeAttackBonusDamage(), Is.EqualTo(14));
                Assert.That(ability.Effects.Single().DurationSeconds, Is.EqualTo(300f).Within(0.001f));
                Assert.That(ability.VisualEffects, Is.Null);
                Assert.That(ability.Icon, Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(casterObject);
            }
        }

        [Test]
        public void FrostWave_IsAnAuthoritativeSelfCenteredAreaFreeze()
        {
            MMOAbilityDefinition ability = LoadAbility("Mage_Frost_Wave");
            MMOAbilityEffectDefinition damage = ability.Effects.Single(
                effect => effect.EffectType == MMOAbilityEffectType.Damage);
            MMOAbilityEffectDefinition freeze = ability.Effects.Single(
                effect => effect.EffectType == MMOAbilityEffectType.TemporaryStatModifier);
            GameObject targetObject = new("Frost Wave Test Target");

            try
            {
                MMOCharacterIdentity target = targetObject.AddComponent<MMOCharacterIdentity>();
                MMOCharacterBuffController buffs = targetObject.AddComponent<MMOCharacterBuffController>();

                Assert.That(ability.TargetType, Is.EqualTo(MMOAbilityTargetType.Self));
                Assert.That(ability.AreaRadius, Is.EqualTo(8f).Within(0.001f));
                Assert.That(ability.AreaTargetFilter, Is.EqualTo(MMOAbilityAreaTargetFilter.Hostile));
                Assert.That(ability.CooldownSeconds, Is.EqualTo(20f).Within(0.001f));
                Assert.That(damage.DamageSchool, Is.EqualTo(MMODamageSchool.Frost));
                Assert.That(damage.FlatAmount, Is.EqualTo(34f).Within(0.001f));
                Assert.That(damage.Coefficient, Is.EqualTo(0.42f).Within(0.001f));
                Assert.That(freeze.DurationSeconds, Is.EqualTo(3f).Within(0.001f));
                Assert.That(freeze.PreventsMovement, Is.True);
                Assert.That(buffs.ApplyTemporaryModifiers(ability, null), Is.True);
                Assert.That(buffs.IsMovementPrevented, Is.True);
                Assert.That(ability.VisualEffects, Is.Not.Null);
                Assert.That(ability.VisualEffects.CastPrefab, Is.Not.Null);
                Assert.That(ability.Icon, Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(targetObject);
            }
        }

        [Test]
        public void PressTheAttack_ConfiguresBothRuntimeSpeedModifiers()
        {
            MMOAbilityDefinition ability = LoadAbility("Warrior_Press_The_Attack");
            MMOAbilityEffectDefinition stance = ability.Effects.Single();

            Assert.That(ability.TargetType, Is.EqualTo(MMOAbilityTargetType.Self));
            Assert.That(ability.CooldownSeconds, Is.EqualTo(30f).Within(0.001f));
            Assert.That(stance.DurationSeconds, Is.EqualTo(6f).Within(0.001f));
            Assert.That(stance.MovementSpeedMultiplier, Is.EqualTo(1.20f).Within(0.001f));
            Assert.That(stance.AttackSpeedMultiplier, Is.EqualTo(1.15f).Within(0.001f));
            Assert.That(ability.VisualEffects, Is.Null);
            Assert.That(ability.Icon, Is.Not.Null);
        }

        private static MMOAbilityDefinition LoadAbility(string assetName)
        {
            MMOAbilityDefinition ability =
                AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>($"{AbilityRoot}{assetName}.asset");
            Assert.That(ability, Is.Not.Null, $"Missing level 8 ability asset: {assetName}");
            return ability;
        }

        private static void AssertLevelEightOffer(
            MMOAbilityCatalog abilityCatalog,
            MMOTrainerOfferCatalog trainerCatalog,
            string assetName,
            MMOPlayableClass expectedClass)
        {
            MMOAbilityDefinition ability = LoadAbility(assetName);
            Assert.That(abilityCatalog.Abilities, Does.Contain(ability));

            MMOTrainerOfferEntry offer = trainerCatalog.Offers.Single(entry => entry.Ability == ability);
            Assert.That(offer.RequiredClass, Is.EqualTo(expectedClass));
            Assert.That(offer.RequiredLevel, Is.EqualTo(8));
        }
    }
}
#endif
