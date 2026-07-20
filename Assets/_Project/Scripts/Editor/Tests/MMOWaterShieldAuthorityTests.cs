#if UNITY_EDITOR
using NUnit.Framework;
using RPGClone.Abilities;
using RPGClone.Buffs;
using RPGClone.Characters;
using RPGClone.Combat;
using UnityEditor;
using UnityEngine;

namespace RPGClone.EditorTests
{
    public sealed class MMOWaterShieldAuthorityTests
    {
        private const string WaterShieldAssetPath = "Assets/_Project/Configs/Abilities/Shaman_Water_Shield.asset";

        [Test]
        public void WaterShield_RequiresHostResolution()
        {
            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(WaterShieldAssetPath);

            Assert.That(ability, Is.Not.Null);
            Assert.That(ability.TargetType, Is.EqualTo(MMOAbilityTargetType.Self));
            Assert.That(MMOSessionCombatAuthority.RequiresHostResolution(ability), Is.True);
        }

        [Test]
        public void AuthoredGameplayAbilities_AllRequireHostResolution()
        {
            string[] abilityGuids = AssetDatabase.FindAssets(
                "t:MMOAbilityDefinition",
                new[] { "Assets/_Project/Configs/Abilities" });
            int testedAbilityCount = 0;

            foreach (string abilityGuid in abilityGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(abilityGuid);
                MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(path);
                if (ability == null || ability.Effects.Count == 0)
                {
                    continue;
                }

                testedAbilityCount++;
                Assert.That(
                    MMOSessionCombatAuthority.RequiresHostResolution(ability),
                    Is.True,
                    $"{ability.DisplayName} ({path}) bypasses host authority.");
            }

            Assert.That(testedAbilityCount, Is.GreaterThan(0));
        }

        [TestCase(CombatActionRequestKind.ChannelStart)]
        [TestCase(CombatActionRequestKind.ChannelCancel)]
        [TestCase(CombatActionRequestKind.ChargeImpact)]
        public void SpecializedAuthorityRequestKind_SurvivesNetworkSerialization(CombatActionRequestKind requestKind)
        {
            CombatActionRequest request = CombatActionRequest.Create(
                "session",
                "caster",
                "caster",
                "target",
                string.Empty,
                "ability",
                Vector3.zero,
                false,
                requestKind);

            CombatActionRequest clone = JsonUtility.FromJson<CombatActionRequest>(JsonUtility.ToJson(request));

            Assert.That(clone, Is.Not.Null);
            Assert.That(clone.requestKind, Is.EqualTo(requestKind));
        }

        [Test]
        public void ReplicatedWaterShieldModifier_AbsorbsDamageAndRestoresMana()
        {
            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(WaterShieldAssetPath);
            GameObject targetObject = new("Water Shield Authority Target");
            GameObject controlObject = new("Unshielded Authority Target");

            try
            {
                MMOCombatant target = targetObject.AddComponent<MMOCombatant>();
                MMOCharacterIdentity identity = target.Identity;
                MMOCharacterBuffController buffs = targetObject.AddComponent<MMOCharacterBuffController>();
                identity.Health.Configure(100, 100);
                identity.Mana.Configure(100, 10);

                MMOCombatant control = controlObject.AddComponent<MMOCombatant>();
                MMOCharacterIdentity controlIdentity = control.Identity;
                controlIdentity.Health.Configure(100, 100);
                controlIdentity.Mana.Configure(100, 10);
                control.ApplyDamage(null, null, 50, false, false);
                int mitigatedDamage = 100 - controlIdentity.Health.CurrentValue;
                int expectedAbsorption = Mathf.RoundToInt(mitigatedDamage * 0.2f);

                bool applied = buffs.ApplyTemporaryModifiers(ability, target);
                target.ApplyDamage(null, null, 50, false, false);

                Assert.That(applied, Is.True);
                Assert.That(buffs.FindBuff(ability.AbilityId), Is.Not.Null);
                Assert.That(identity.Health.CurrentValue, Is.EqualTo(100 - mitigatedDamage + expectedAbsorption));
                Assert.That(identity.Mana.CurrentValue, Is.EqualTo(10 + expectedAbsorption));
            }
            finally
            {
                Object.DestroyImmediate(controlObject);
                Object.DestroyImmediate(targetObject);
            }
        }
    }
}
#endif
