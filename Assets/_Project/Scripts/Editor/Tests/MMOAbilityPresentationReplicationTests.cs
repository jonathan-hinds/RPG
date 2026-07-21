#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using RPGClone.Abilities;
using RPGClone.Buffs;
using RPGClone.Characters;
using RPGClone.Combat;
using RPGClone.Loot;
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

        [Test]
        public void ClientOperationTimestamps_AreNormalizedToHostReceiptTime()
        {
            System.Reflection.MethodInfo normalizeMethod = typeof(MMONetcodeSharedSessionTransport).GetMethod(
                "NormalizeOperationTimestampsForReceiver",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            CombatActionRequest request = new()
            {
                requestedUtcTicks = 1
            };
            MMOSharedSessionNetworkOperation operation = new()
            {
                combatRequest = request
            };
            long before = System.DateTime.UtcNow.Ticks;

            normalizeMethod?.Invoke(null, new object[] { operation, true });

            long after = System.DateTime.UtcNow.Ticks;
            Assert.That(normalizeMethod, Is.Not.Null);
            Assert.That(request.requestedUtcTicks, Is.InRange(before, after));
        }

        [Test]
        public void PersonalLootUpdate_CanOnlyDepleteRequestingCharactersLoot()
        {
            const string sessionId = "loot-authority-test";
            const string spawnId = "enemy-1";
            MMOSharedSessionState.Reset();
            try
            {
                MMOCorpseLootState authoritative = new()
                {
                    sessionId = sessionId,
                    corpseId = spawnId,
                    enemySpawnId = spawnId,
                    updatedUtcTicks = System.DateTime.UtcNow.Ticks,
                    personalLoot = new List<MMOPersonalLootState>
                    {
                        CreatePersonalLoot("host", "host-item", 1),
                        CreatePersonalLoot("client", "client-item", 2)
                    }
                };
                MMOSharedSessionState.UpsertCorpseLootSnapshot(authoritative);
                MMOCorpseLootState proposed = new()
                {
                    sessionId = sessionId,
                    corpseId = spawnId,
                    enemySpawnId = spawnId,
                    personalLoot = new List<MMOPersonalLootState>
                    {
                        new() { characterId = "host", looted = true },
                        new() { characterId = "client", looted = true }
                    }
                };

                bool accepted = MMOSharedSessionState.TryApplyPersonalLootUpdate(
                    proposed,
                    "client",
                    out MMOCorpseLootState merged);

                Assert.That(accepted, Is.True);
                Assert.That(merged.personalLoot.Find(state => state.characterId == "client").HasLoot, Is.False);
                Assert.That(merged.personalLoot.Find(state => state.characterId == "host").HasLoot, Is.True);
            }
            finally
            {
                MMOSharedSessionState.Reset();
            }
        }

        [Test]
        public void QuestKillReward_PreservesPartyCreditSemantics()
        {
            const string sessionId = "quest-credit-test";
            const string targetCharacterId = "target";
            MMOSharedSessionState.Reset();
            try
            {
                MMOSharedSessionState.PublishQuestKillCreditEvent(
                    sessionId,
                    targetCharacterId,
                    "enemy-1",
                    "wolf",
                    "wolf",
                    true,
                    "source");

                IReadOnlyList<MMOSharedRewardEvent> rewards =
                    MMOSharedSessionState.GetPendingRewardEvents(sessionId, targetCharacterId);

                Assert.That(rewards, Has.Count.EqualTo(1));
                Assert.That(rewards[0].isPartyCredit, Is.True);
            }
            finally
            {
                MMOSharedSessionState.Reset();
            }
        }

        [Test]
        public void PendingLootClaim_BlocksStaleSnapshotFromRehydratingItems()
        {
            GameObject corpseObject = new("Loot Race Corpse");
            try
            {
                MMOLootableCorpse corpse = corpseObject.AddComponent<MMOLootableCorpse>();
                MMOCorpseLootState staleSnapshot = new()
                {
                    sessionId = "loot-race-test",
                    corpseId = "enemy-1",
                    enemySpawnId = "enemy-1",
                    personalLoot = new List<MMOPersonalLootState>
                    {
                        CreatePersonalLoot("client", "client-item", 1)
                    }
                };
                System.Reflection.FieldInfo pendingField = typeof(MMOLootableCorpse).GetField(
                    "pendingLocalLootUpdate",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                pendingField?.SetValue(corpse, new MMOPersonalLootState
                {
                    characterId = "client",
                    looted = true
                });

                corpse.ApplyPersonalLootSnapshot(staleSnapshot);

                MMOCorpseLootState applied = corpse.CreatePersonalLootSnapshot();
                Assert.That(pendingField, Is.Not.Null);
                Assert.That(applied.personalLoot.Find(state => state.characterId == "client").HasLoot, Is.False);
                Assert.That(pendingField.GetValue(corpse), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(corpseObject);
            }
        }

        private static MMOPersonalLootState CreatePersonalLoot(string characterId, string itemId, int quantity)
        {
            return new MMOPersonalLootState
            {
                characterId = characterId,
                items = new List<MMOPersonalLootItemState>
                {
                    new() { itemId = itemId, quantity = quantity }
                }
            };
        }
    }
}
#endif
