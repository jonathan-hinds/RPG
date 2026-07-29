#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using RPGClone.Abilities;
using RPGClone.Combat;
using RPGClone.Vfx;
using RPGClone.Vfx.Warrior;
using UnityEditor;
using UnityEngine;

namespace RPGClone.EditorTests
{
    public sealed class ThunderClapVFXTests
    {
        private const string AbilityPath = "Assets/_Project/Configs/Abilities/Warrior_Thunderclap.asset";
        private const string DefinitionPath = "Assets/_Project/VFX/Definitions/Warrior_Thunderclap_VFX.asset";
        private const string PrefabPath = "Assets/_Project/VFX/ThunderClap/Prefabs/ThunderClapVFX.prefab";
        private const string InvaderAbilityPath = "Assets/_Project/Configs/Abilities/BristlebackInvader_Thunderclap.asset";
        private const string InvaderDefinitionPath = "Assets/_Project/Configs/Enemies/Bristleback_Invader_Aggressive.asset";
        private const string InvaderPrefabPath = "Assets/Characters/BristlebackInvader/Prefabs/BristlebackInvaderEnemy.prefab";

        [Test]
        public void Package_IsWiredToExistingAbilityDefinition()
        {
            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AbilityPath);
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(DefinitionPath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            Assert.That(ability, Is.Not.Null);
            Assert.That(definition, Is.Not.Null);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(ability.VisualEffects, Is.SameAs(definition));
            Assert.That(definition.CastPrefab, Is.SameAs(prefab));
            Assert.That(definition.HitPrefab, Is.Null);
            Assert.That(definition.CastPrefabControlsHitTiming, Is.True);
            Assert.That(definition.HasCasterBounce, Is.True);
            Assert.That(definition.CasterBounceHeight, Is.EqualTo(0.32f).Within(0.001f));
            Assert.That(definition.CasterBounceDuration, Is.EqualTo(0.3f).Within(0.001f));
            Assert.That(prefab.GetComponentsInChildren<ThunderClapTargetReactionVFX>(true), Has.Length.EqualTo(12));
        }

        [Test]
        public void CanonicalPlayerAbility_IsSupportedAsEnemySelfAreaCombatAbility()
        {
            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AbilityPath);
            MethodInfo supportedMethod = typeof(RPGClone.Enemies.MMOEnemyController).GetMethod(
                "IsSupportedCombatAbility",
                BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo rangeMethod = typeof(RPGClone.Enemies.MMOEnemyController).GetMethod(
                "GetCombatAbilityEngagementRange",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.That(ability, Is.Not.Null);
            Assert.That(ability.TargetType, Is.EqualTo(MMOAbilityTargetType.Self));
            Assert.That(ability.AreaTargetFilter, Is.EqualTo(MMOAbilityAreaTargetFilter.Hostile));
            Assert.That(supportedMethod, Is.Not.Null);
            Assert.That(rangeMethod, Is.Not.Null);
            Assert.That((bool)supportedMethod.Invoke(null, new object[] { ability }), Is.True);
            Assert.That(
                (float)rangeMethod.Invoke(null, new object[] { ability }),
                Is.EqualTo(ability.AreaRadius).Within(0.001f));
        }

        [Test]
        public void BristlebackInvader_UsesDedicatedCooldownVariantAndStandardCreatureVfxFallbacks()
        {
            MMOAbilityDefinition playerAbility = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AbilityPath);
            MMOAbilityDefinition invaderAbility = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(InvaderAbilityPath);
            RPGClone.Enemies.MMOEnemyDefinition enemyDefinition =
                AssetDatabase.LoadAssetAtPath<RPGClone.Enemies.MMOEnemyDefinition>(InvaderDefinitionPath);
            GameObject invaderPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(InvaderPrefabPath);

            Assert.That(playerAbility, Is.Not.Null);
            Assert.That(invaderAbility, Is.Not.Null);
            Assert.That(enemyDefinition, Is.Not.Null);
            Assert.That(invaderPrefab, Is.Not.Null);
            Assert.That(playerAbility.CooldownSeconds, Is.EqualTo(6f).Within(0.001f));
            Assert.That(invaderAbility.CooldownSeconds, Is.EqualTo(15f).Within(0.001f));
            Assert.That(invaderAbility.AbilityId, Is.EqualTo("bristleback_invader_thunderclap"));
            Assert.That(invaderAbility.VisualEffects, Is.SameAs(playerAbility.VisualEffects));
            Assert.That(invaderAbility.TargetType, Is.EqualTo(playerAbility.TargetType));
            Assert.That(invaderAbility.AreaRadius, Is.EqualTo(playerAbility.AreaRadius).Within(0.001f));
            Assert.That(invaderAbility.AreaTargetFilter, Is.EqualTo(playerAbility.AreaTargetFilter));
            Assert.That(invaderAbility.Effects.Count, Is.EqualTo(playerAbility.Effects.Count));
            Assert.That(enemyDefinition.Abilities, Does.Contain(invaderAbility));
            Assert.That(enemyDefinition.Abilities, Has.No.Member(playerAbility));
            Assert.That(
                invaderPrefab.GetComponent<MMOAbilitySystem>().KnownAbilities,
                Does.Contain(invaderAbility));
            Assert.That(
                invaderPrefab.GetComponent<MMOAbilitySystem>().KnownAbilities,
                Has.No.Member(playerAbility));
            Assert.That(invaderPrefab.GetComponent<MMOAbilityVfxAnchors>(), Is.Null);
            Assert.That(invaderPrefab.transform.Find("Spell Cast Origin"), Is.Null);
        }

        [Test]
        public void CasterBounce_LateUpdateOffsetsOnlyVisualRootAndRestores()
        {
            GameObject invaderPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(InvaderPrefabPath);
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(DefinitionPath);
            MMOAbilityDefinition invaderAbility = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(InvaderAbilityPath);
            GameObject caster = Object.Instantiate(invaderPrefab);
            Transform visual = caster.transform.Find("Bristleback Invader Visual");
            GameObject vfxRoot = new("Ability VFX");
            vfxRoot.transform.SetParent(caster.transform, false);

            try
            {
                MMOAbilityVfxController controller = caster.AddComponent<MMOAbilityVfxController>();
                MethodInfo ensureReferences = typeof(MMOAbilityVfxController).GetMethod(
                    "EnsureReferences",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo onEnable = typeof(MMOAbilityVfxController).GetMethod(
                    "OnEnable",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo lateUpdate = typeof(MMOAbilityVfxController).GetMethod(
                    "LateUpdate",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo startedAt = typeof(MMOAbilityVfxController).GetField(
                    "casterBounceStartedAt",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(ensureReferences, Is.Not.Null);
                Assert.That(onEnable, Is.Not.Null);
                Assert.That(lateUpdate, Is.Not.Null);
                Assert.That(startedAt, Is.Not.Null);
                ensureReferences.Invoke(controller, null);
                onEnable.Invoke(controller, null);

                MMOAbilitySystem abilitySystem = caster.GetComponent<MMOAbilitySystem>();
                RPGClone.Characters.MMOCharacterIdentity identity =
                    caster.GetComponent<RPGClone.Characters.MMOCharacterIdentity>();
                Vector3 gameplayStart = caster.transform.position;
                Vector3 visualStart = visual.localPosition;
                abilitySystem.PlayReplicatedAbilityReleased(
                    invaderAbility,
                    identity,
                    caster.transform.position,
                    true);
                startedAt.SetValue(controller, Time.time - definition.CasterBounceDuration * 0.25f);
                lateUpdate.Invoke(controller, null);
                Assert.That(visual.localPosition.y, Is.GreaterThan(visualStart.y + 0.2f));

                visual.localPosition = visualStart;
                startedAt.SetValue(controller, Time.time - definition.CasterBounceDuration * 0.5f);
                lateUpdate.Invoke(controller, null);
                Assert.That(caster.transform.position, Is.EqualTo(gameplayStart));
                Assert.That(visual.localPosition.y, Is.EqualTo(visualStart.y + 0.32f).Within(0.001f));

                startedAt.SetValue(controller, Time.time - definition.CasterBounceDuration * 1.1f);
                lateUpdate.Invoke(controller, null);
                Assert.That(visual.localPosition, Is.EqualTo(visualStart));
                Assert.That(caster.transform.position, Is.EqualTo(gameplayStart));
            }
            finally
            {
                Object.DestroyImmediate(caster);
            }
        }

        [Test]
        public void AuthoritativeDamageEvent_QueuesOneReactionWithoutRadiusQuery()
        {
            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AbilityPath);
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(DefinitionPath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            GameObject sourceObject = new("Thunder Clap Test Source");
            GameObject targetObject = new("Thunder Clap Test Target");
            GameObject instance = null;
            try
            {
                MMOCombatant source = sourceObject.AddComponent<MMOCombatant>();
                MMOCombatant target = targetObject.AddComponent<MMOCombatant>();
                instance = Object.Instantiate(prefab);
                ThunderClapVFX vfx = instance.GetComponent<ThunderClapVFX>();
                vfx.Initialize(new MMOAbilityVfxContext(
                    null,
                    ability,
                    definition,
                    sourceObject.transform,
                    null,
                    sourceObject.transform.position,
                    sourceObject.transform.position,
                    true,
                    null));

                CombatEventRecord record = CombatEventRecord.Create(CombatEventType.DamageResolved);
                record.abilityId = ability.AbilityId;
                record.damageAmount = 1;
                MMOCombatEventStream.PublishCombatEvent(record, source, target, ability);
                MMOCombatEventStream.PublishCombatEvent(record, source, target, ability);

                FieldInfo pendingField = typeof(ThunderClapVFX).GetField("pendingReactions", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(pendingField, Is.Not.Null);
                ICollection pending = pendingField.GetValue(vfx) as ICollection;
                Assert.That(pending, Is.Not.Null);
                Assert.That(pending.Count, Is.EqualTo(1), "Repeated replicated records for the same target must not duplicate the reaction.");
            }
            finally
            {
                if (instance != null) Object.DestroyImmediate(instance);
                Object.DestroyImmediate(targetObject);
                Object.DestroyImmediate(sourceObject);
            }
        }
    }
}
#endif
