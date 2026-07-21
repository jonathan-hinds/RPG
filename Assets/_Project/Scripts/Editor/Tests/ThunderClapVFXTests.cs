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
            Assert.That(prefab.GetComponentsInChildren<ThunderClapTargetReactionVFX>(true), Has.Length.EqualTo(12));
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
