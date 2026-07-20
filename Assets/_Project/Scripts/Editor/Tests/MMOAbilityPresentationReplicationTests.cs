#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using RPGClone.Abilities;
using RPGClone.Buffs;
using RPGClone.Characters;
using RPGClone.Multiplayer;
using UnityEngine;

namespace RPGClone.EditorTests
{
    public sealed class MMOAbilityPresentationReplicationTests
    {
        [Test]
        public void ReplicatedChargeLifecycle_ForwardsAllPresentationEvents()
        {
            GameObject casterObject = new("Replicated Charge Caster");
            GameObject targetObject = new("Replicated Charge Target");
            MMOAbilityDefinition ability = ScriptableObject.CreateInstance<MMOAbilityDefinition>();

            try
            {
                MMOAbilitySystem abilitySystem = casterObject.AddComponent<MMOAbilitySystem>();
                MMOCharacterIdentity target = targetObject.AddComponent<MMOCharacterIdentity>();
                List<string> phases = new();
                float observedImpactDelay = -1f;

                abilitySystem.ChargeStarted += (_, eventAbility, eventTarget) =>
                {
                    Assert.That(eventAbility, Is.SameAs(ability));
                    Assert.That(eventTarget, Is.SameAs(target));
                    phases.Add(MMOSharedAbilityEventTypes.ChargeStarted);
                };
                abilitySystem.ChargeImpactStarted += (_, eventAbility, eventTarget, delay) =>
                {
                    Assert.That(eventAbility, Is.SameAs(ability));
                    Assert.That(eventTarget, Is.SameAs(target));
                    observedImpactDelay = delay;
                    phases.Add(MMOSharedAbilityEventTypes.ChargeImpactStarted);
                };
                abilitySystem.ChargeCompleted += (_, eventAbility, eventTarget) =>
                {
                    Assert.That(eventAbility, Is.SameAs(ability));
                    Assert.That(eventTarget, Is.SameAs(target));
                    phases.Add(MMOSharedAbilityEventTypes.ChargeCompleted);
                };

                abilitySystem.PlayReplicatedChargeStarted(ability, target);
                abilitySystem.PlayReplicatedChargeImpactStarted(ability, target, 0.18f);
                abilitySystem.PlayReplicatedChargeCompleted(ability, target);

                Assert.That(phases, Is.EqualTo(new[]
                {
                    MMOSharedAbilityEventTypes.ChargeStarted,
                    MMOSharedAbilityEventTypes.ChargeImpactStarted,
                    MMOSharedAbilityEventTypes.ChargeCompleted
                }));
                Assert.That(observedImpactDelay, Is.EqualTo(0.18f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(ability);
                Object.DestroyImmediate(targetObject);
                Object.DestroyImmediate(casterObject);
            }
        }

        [Test]
        public void PublishedChargeLifecycle_IsVisibleToOtherSessionParticipantsOnly()
        {
            const string sessionId = "vfx-replication-test";
            const string casterCharacterId = "caster";
            const string observerCharacterId = "observer";

            MMOSharedSessionState.Reset();
            try
            {
                MMOSharedSessionState.PublishChargeStartedEvent(
                    sessionId,
                    casterCharacterId,
                    string.Empty,
                    "warrior_charge",
                    casterCharacterId);
                MMOSharedSessionState.PublishChargeImpactStartedEvent(
                    sessionId,
                    casterCharacterId,
                    string.Empty,
                    "warrior_charge",
                    0.18f,
                    casterCharacterId);
                MMOSharedSessionState.PublishChargeCompletedEvent(
                    sessionId,
                    casterCharacterId,
                    string.Empty,
                    "warrior_charge",
                    casterCharacterId);

                IReadOnlyList<MMOSharedAbilityEvent> observerEvents =
                    MMOSharedSessionState.GetPendingEvents(sessionId, observerCharacterId);
                IReadOnlyList<MMOSharedAbilityEvent> casterEvents =
                    MMOSharedSessionState.GetPendingEvents(sessionId, casterCharacterId);

                Assert.That(observerEvents, Has.Count.EqualTo(3));
                Assert.That(observerEvents[0].eventType, Is.EqualTo(MMOSharedAbilityEventTypes.ChargeStarted));
                Assert.That(observerEvents[1].eventType, Is.EqualTo(MMOSharedAbilityEventTypes.ChargeImpactStarted));
                Assert.That(observerEvents[1].castDurationSeconds, Is.EqualTo(0.18f).Within(0.001f));
                Assert.That(observerEvents[2].eventType, Is.EqualTo(MMOSharedAbilityEventTypes.ChargeCompleted));
                Assert.That(casterEvents, Is.Empty);
            }
            finally
            {
                MMOSharedSessionState.Reset();
            }
        }

        [Test]
        public void ReplicatedCastCompletion_IsIndependentFromAbilityRelease()
        {
            GameObject casterObject = new("Replicated Cast Caster");
            MMOAbilityDefinition ability = ScriptableObject.CreateInstance<MMOAbilityDefinition>();

            try
            {
                MMOAbilitySystem abilitySystem = casterObject.AddComponent<MMOAbilitySystem>();
                int releaseCount = 0;
                int completionCount = 0;
                abilitySystem.AbilityReleased += (_, _, _, _, _) => releaseCount++;
                abilitySystem.CastCompleted += (_, _, _) => completionCount++;

                abilitySystem.PlayReplicatedAbilityReleased(ability, null, Vector3.zero, false);
                Assert.That(releaseCount, Is.EqualTo(1));
                Assert.That(completionCount, Is.Zero);

                abilitySystem.PlayReplicatedCastCompleted(ability, null);
                Assert.That(completionCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(ability);
                Object.DestroyImmediate(casterObject);
            }
        }

        [Test]
        public void ReplicatedManaAbsorption_NotifiesPresentationWithoutChangingResources()
        {
            GameObject targetObject = new("Replicated Water Shield Target");

            try
            {
                MMOCharacterIdentity identity = targetObject.AddComponent<MMOCharacterIdentity>();
                MMOCharacterBuffController buffs = targetObject.AddComponent<MMOCharacterBuffController>();
                int manaBefore = identity.Mana.CurrentValue;
                int observedAmount = 0;
                buffs.DamageAbsorbedAsMana += (_, amount) => observedAmount += amount;

                buffs.NotifyReplicatedDamageAbsorbedAsMana(7);

                Assert.That(observedAmount, Is.EqualTo(7));
                Assert.That(identity.Mana.CurrentValue, Is.EqualTo(manaBefore));
            }
            finally
            {
                Object.DestroyImmediate(targetObject);
            }
        }
    }
}
#endif
