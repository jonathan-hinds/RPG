#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using RPGClone.Abilities;
using RPGClone.Buffs;
using RPGClone.CharacterSelection;
using RPGClone.Characters;
using RPGClone.Combat;
using RPGClone.Inventory;
using RPGClone.PlayerInteraction;
using RPGClone.Multiplayer;
using RPGClone.Services;
using UnityEngine;

namespace RPGClone.EditorTests
{
    public sealed class MMOPlayerInteractionTests
    {
        private readonly List<Object> createdObjects = new();

        [SetUp]
        public void SetUp()
        {
            MMOPlayerInteractionState.Reset();
            MMOGameplaySessionService.Players.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            MMOPlayerInteractionState.Reset();
            MMOGameplaySessionService.Players.Clear();
            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                {
                    Object.DestroyImmediate(createdObjects[i]);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void TradeSettlement_ExchangesItemsAndCopperAtomically()
        {
            MMOItemDefinition ore = CreateItem("ore", "Copper Ore", MMOItemType.Material, 20);
            MMOItemDefinition cloth = CreateItem("cloth", "Linen Cloth", MMOItemType.Material, 20);
            MMOCharacterSaveData initiator = CreateCharacter("first", 250, (0, ore, 5));
            MMOCharacterSaveData recipient = CreateCharacter("second", 100, (3, cloth, 2));
            MMOTradeSessionSnapshot trade = new()
            {
                initiatorCharacterId = "first",
                recipientCharacterId = "second",
                initiatorCopper = 50,
                recipientCopper = 10,
                initiatorOffers = new List<MMOTradeOfferEntry>
                {
                    new() { offerSlotIndex = 0, sourceInventorySlotIndex = 0, itemId = ore.ItemId, quantity = 3 }
                },
                recipientOffers = new List<MMOTradeOfferEntry>
                {
                    new() { offerSlotIndex = 0, sourceInventorySlotIndex = 3, itemId = cloth.ItemId, quantity = 2 }
                }
            };

            bool settled = MMOTradeTransaction.TryBuildSettlement(
                initiator,
                recipient,
                trade,
                out List<MMOInventorySlotSaveData> firstResult,
                out List<MMOInventorySlotSaveData> secondResult,
                out int firstCopper,
                out int secondCopper,
                out string failureReason);

            Assert.That(settled, Is.True, failureReason);
            Assert.That(firstCopper, Is.EqualTo(210));
            Assert.That(secondCopper, Is.EqualTo(140));
            Assert.That(Count(firstResult, ore.ItemId), Is.EqualTo(2));
            Assert.That(Count(firstResult, cloth.ItemId), Is.EqualTo(2));
            Assert.That(Count(secondResult, ore.ItemId), Is.EqualTo(3));
            Assert.That(Count(secondResult, cloth.ItemId), Is.EqualTo(0));
        }

        [Test]
        public void TradeSettlement_RejectsChangedSourceSlotWithoutMutatingInputs()
        {
            MMOItemDefinition ore = CreateItem("stale_ore", "Stale Ore", MMOItemType.Material, 20);
            MMOCharacterSaveData initiator = CreateCharacter("first", 0, (0, ore, 1));
            MMOCharacterSaveData recipient = CreateCharacter("second", 0);
            MMOTradeSessionSnapshot trade = new()
            {
                initiatorCharacterId = "first",
                recipientCharacterId = "second",
                initiatorOffers = new List<MMOTradeOfferEntry>
                {
                    new() { offerSlotIndex = 0, sourceInventorySlotIndex = 0, itemId = ore.ItemId, quantity = 2 }
                }
            };

            bool settled = MMOTradeTransaction.TryBuildSettlement(
                initiator,
                recipient,
                trade,
                out _, out _, out _, out _, out string failureReason);

            Assert.That(settled, Is.False);
            Assert.That(failureReason, Does.Contain("no longer"));
            Assert.That(initiator.inventory[0].quantity, Is.EqualTo(1));
        }

        [Test]
        public void TradeSettlement_RejectsQuestItemsAndFullRecipientInventory()
        {
            MMOItemDefinition questItem = CreateItem("quest_token", "Quest Token", MMOItemType.Quest, 1);
            MMOItemDefinition filler = CreateItem("filler", "Filler", MMOItemType.Trash, 1);
            MMOCharacterSaveData initiator = CreateCharacter("first", 0, (0, questItem, 1));
            MMOCharacterSaveData recipient = CreateCharacter("second", 0);
            for (int i = 0; i < MMOInventoryContainer.DefaultBackpackSlotCount; i++)
            {
                recipient.inventory.Add(new MMOInventorySlotSaveData { slotIndex = i, itemId = filler.ItemId, quantity = 1 });
            }

            MMOTradeSessionSnapshot questTrade = new()
            {
                initiatorCharacterId = "first",
                recipientCharacterId = "second",
                initiatorOffers = new List<MMOTradeOfferEntry>
                {
                    new() { offerSlotIndex = 0, sourceInventorySlotIndex = 0, itemId = questItem.ItemId, quantity = 1 }
                }
            };
            Assert.That(
                MMOTradeTransaction.TryBuildSettlement(initiator, recipient, questTrade, out _, out _, out _, out _, out _),
                Is.False);

            MMOItemDefinition material = CreateItem("material", "Material", MMOItemType.Material, 1);
            initiator = CreateCharacter("first", 0, (0, material, 1));
            questTrade.initiatorOffers[0].itemId = material.ItemId;
            Assert.That(
                MMOTradeTransaction.TryBuildSettlement(initiator, recipient, questTrade, out _, out _, out _, out _, out string fullReason),
                Is.False);
            Assert.That(fullReason, Does.Contain("inventory space"));
        }

        [Test]
        public void TradeSettlement_RejectsInventorySlotsOutsideAuthoritativeCapacity()
        {
            MMOItemDefinition material = CreateItem("invalid_slot_material", "Material", MMOItemType.Material, 20);
            MMOCharacterSaveData malformed = CreateCharacter(
                "first",
                0,
                (MMOInventoryContainer.DefaultBackpackSlotCount, material, 1));
            MMOCharacterSaveData recipient = CreateCharacter("second", 0);
            MMOTradeSessionSnapshot trade = new()
            {
                initiatorCharacterId = "first",
                recipientCharacterId = "second"
            };

            bool settled = MMOTradeTransaction.TryBuildSettlement(
                malformed, recipient, trade, out _, out _, out _, out _, out string failureReason);

            Assert.That(settled, Is.False);
            Assert.That(failureReason, Does.Contain("invalid"));
        }

        [Test]
        public void ActiveDuel_EnablesPlayerDamageAndClampsLethalDamageToOneHealth()
        {
            MMOCombatant first = CreateCombatant("First", "first");
            MMOCombatant second = CreateCombatant("Second", "second");
            second.Identity.Health.Configure(100, 100);
            MMODuelSessionSnapshot duel = new()
            {
                duelId = "duel",
                challengerCharacterId = "first",
                challengedCharacterId = "second",
                status = MMODuelSessionStatus.Active,
                revision = 1
            };
            MMOPlayerInteractionState.Upsert(duel);

            Assert.That(MMOFactionRules.CanDamage(first.Identity, second.Identity), Is.True);
            Assert.That(MMOPlayerInteractionAuthority.TryResolveDuelDamage(first, second, 500, out int amount), Is.True);
            Assert.That(amount, Is.EqualTo(99));
            second.ApplyResolvedDamage(first, null, 10, false, false);
            Assert.That(first.IsInCombat, Is.True);
            Assert.That(second.IsInCombat, Is.True);

            duel.status = MMODuelSessionStatus.Won;
            duel.revision++;
            MMOPlayerInteractionState.Upsert(duel);
            MMOPlayerInteractionService.CleanupDuelEffects(duel);
            Assert.That(MMOFactionRules.CanDamage(first.Identity, second.Identity), Is.False);
            Assert.That(first.IsInCombat, Is.False);
            Assert.That(second.IsInCombat, Is.False);

            second.ApplyResolvedDamage(first, null, 1, false, false);
            Assert.That(first.IsInCombat, Is.False, "A delayed replicated final hit must not recreate combat.");
            Assert.That(second.IsInCombat, Is.False, "A delayed replicated final hit must not recreate combat.");
        }

        [Test]
        public void DuelCleanup_RemovesOnlyOpponentSourcedHarmfulEffects()
        {
            MMOCombatant first = CreateCombatant("First", "first");
            MMOCombatant second = CreateCombatant("Second", "second");
            first.EngageCombatWith(second);
            Assert.That(first.IsInCombat, Is.True);
            Assert.That(second.IsInCombat, Is.True);
            MMOCharacterBuffController secondBuffs = second.gameObject.AddComponent<MMOCharacterBuffController>();
            secondBuffs.ApplyBuff(new MMOBuffApplication
            {
                BuffId = "duel_dot",
                DisplayName = "Duel DoT",
                DurationSeconds = 30f,
                IsHarmful = true,
                PeriodicDamageTotal = 30,
                Source = first
            });
            secondBuffs.ApplyBuff(new MMOBuffApplication
            {
                BuffId = "friendly_buff",
                DisplayName = "Friendly Buff",
                DurationSeconds = 30f,
                IsHarmful = false,
                Source = first
            });

            MMODuelSessionSnapshot duel = new()
            {
                duelId = "cleanup_duel",
                challengerCharacterId = "first",
                challengedCharacterId = "second",
                status = MMODuelSessionStatus.Won,
                revision = 2
            };
            MMOPlayerInteractionService.CleanupDuelEffects(duel);

            Assert.That(secondBuffs.FindBuff("duel_dot"), Is.Null);
            Assert.That(secondBuffs.FindBuff("friendly_buff"), Is.Not.Null);
            Assert.That(first.IsInCombat, Is.False);
            Assert.That(second.IsInCombat, Is.False);

            secondBuffs.ApplyBuff(new MMOBuffApplication
            {
                BuffId = "late_duel_dot",
                DisplayName = "Late Duel DoT",
                DurationSeconds = 30f,
                IsHarmful = true,
                PeriodicDamageTotal = 30,
                Source = first
            });
            MMOPlayerInteractionService.CleanupDuelEffects(duel);
            Assert.That(secondBuffs.FindBuff("late_duel_dot"), Is.Null,
                "Terminal cleanup must remain idempotent for effects replayed after the duel snapshot.");
        }

        [Test]
        public void DuelCleanup_StopsBothPlayersAutoAttackingEachOther()
        {
            MMOCombatant first = CreateCombatant("First", "first");
            MMOCombatant second = CreateCombatant("Second", "second");
            MMOAbilityDefinition autoAttack = ScriptableObject.CreateInstance<MMOAbilityDefinition>();
            createdObjects.Add(autoAttack);
            autoAttack.Configure(
                "test_auto_attack",
                "Auto Attack",
                string.Empty,
                MMOAbilityTargetType.Hostile,
                true,
                true,
                5f,
                0f,
                0,
                System.Array.Empty<MMOAbilityEffectDefinition>());
            MMOAutoAttackController firstAutoAttack = first.gameObject.AddComponent<MMOAutoAttackController>();
            MMOAutoAttackController secondAutoAttack = second.gameObject.AddComponent<MMOAutoAttackController>();
            firstAutoAttack.SetAutoAttackAbility(autoAttack);
            secondAutoAttack.SetAutoAttackAbility(autoAttack);

            MMODuelSessionSnapshot duel = new()
            {
                duelId = "auto_attack_cleanup_duel",
                challengerCharacterId = "first",
                challengedCharacterId = "second",
                status = MMODuelSessionStatus.Active,
                revision = 1
            };
            MMOPlayerInteractionState.Upsert(duel);
            Assert.That(firstAutoAttack.StartAutoAttack(second.Identity), Is.True);
            Assert.That(secondAutoAttack.StartAutoAttack(first.Identity), Is.True);

            duel.status = MMODuelSessionStatus.Won;
            duel.revision++;
            MMOPlayerInteractionState.Upsert(duel);
            MMOPlayerInteractionService.CleanupDuelEffects(duel);

            Assert.That(firstAutoAttack.IsAutoAttacking, Is.False);
            Assert.That(secondAutoAttack.IsAutoAttacking, Is.False);
            Assert.That(firstAutoAttack.CurrentTarget, Is.Null);
            Assert.That(secondAutoAttack.CurrentTarget, Is.Null);
            Assert.That(firstAutoAttack.HasActiveSwingTimer, Is.False);
            Assert.That(secondAutoAttack.HasActiveSwingTimer, Is.False);
        }

        [Test]
        public void SharedSessionSnapshot_PreservesTradeAndDuelStateForLateJoiners()
        {
            MMOSharedSessionState.Reset();
            MMOPlayerInteractionState.Upsert(new MMODuelSessionSnapshot
            {
                duelId = "late_duel",
                sessionId = "session",
                challengerCharacterId = "first",
                challengedCharacterId = "second",
                status = MMODuelSessionStatus.Active,
                revision = 3
            });
            MMOPlayerInteractionState.Upsert(new MMOTradeSessionSnapshot
            {
                tradeId = "late_trade",
                sessionId = "session",
                initiatorCharacterId = "first",
                recipientCharacterId = "second",
                status = MMOTradeSessionStatus.Open,
                initiatorAccepted = true,
                revision = 4
            });

            string snapshotJson = MMOSharedSessionState.CreateNetworkSnapshotJson();
            MMOSharedSessionState.Reset();
            MMOSharedSessionState.ApplyNetworkSnapshot(snapshotJson);

            Assert.That(MMOPlayerInteractionState.FindDuel("late_duel")?.status, Is.EqualTo(MMODuelSessionStatus.Active));
            Assert.That(MMOPlayerInteractionState.FindTrade("late_trade")?.initiatorAccepted, Is.True);
        }

        [Test]
        public void AuthoritativeTradeSettlement_PersistsAnIndependentInventoryCopy()
        {
            MMOSharedSessionState.Reset();
            MMOSharedSessionState.UpsertParticipant(new MMOSessionParticipantSnapshot
            {
                sessionId = "session",
                characterId = "first",
                characterData = CreateCharacter("first", 10)
            });
            MMOSharedSessionState.UpsertParticipant(new MMOSessionParticipantSnapshot
            {
                sessionId = "session",
                characterId = "second",
                characterData = CreateCharacter("second", 20)
            });
            List<MMOInventorySlotSaveData> firstResult = new()
            {
                new() { slotIndex = 2, itemId = "settled_item", quantity = 3 }
            };

            MMOSharedSessionState.ApplyAuthoritativeTradeSettlement(
                "first", firstResult, 7, "second", new List<MMOInventorySlotSaveData>(), 23);
            firstResult[0].quantity = 99;

            IReadOnlyList<MMOSessionParticipantSnapshot> participants =
                MMOSharedSessionState.GetParticipants("session");
            MMOSessionParticipantSnapshot first = null;
            foreach (MMOSessionParticipantSnapshot participant in participants)
            {
                if (participant.characterId == "first")
                {
                    first = participant;
                    break;
                }
            }

            Assert.That(first, Is.Not.Null);
            Assert.That(first.characterData.inventory, Has.Count.EqualTo(1));
            Assert.That(first.characterData.inventory[0].quantity, Is.EqualTo(3));
            Assert.That(first.characterData.copper, Is.EqualTo(7));
        }

        private MMOItemDefinition CreateItem(string id, string displayName, MMOItemType type, int maxStack)
        {
            MMOItemDefinition item = ScriptableObject.CreateInstance<MMOItemDefinition>();
            item.Configure(id, displayName, string.Empty, type, MMOItemQuality.Common, maxStack, 0);
            createdObjects.Add(item);
            return item;
        }

        private static MMOCharacterSaveData CreateCharacter(
            string characterId,
            int copper,
            params (int index, MMOItemDefinition item, int quantity)[] items)
        {
            MMOCharacterSaveData character = new() { characterId = characterId, copper = copper };
            foreach ((int index, MMOItemDefinition item, int quantity) in items)
            {
                character.inventory.Add(new MMOInventorySlotSaveData
                {
                    slotIndex = index,
                    itemId = item.ItemId,
                    quantity = quantity
                });
            }

            return character;
        }

        private MMOCombatant CreateCombatant(string displayName, string characterId)
        {
            GameObject gameObject = new(displayName);
            createdObjects.Add(gameObject);
            MMOCombatant combatant = gameObject.AddComponent<MMOCombatant>();
            combatant.Identity.Configure(
                displayName,
                1,
                null,
                Color.white,
                MMOEntityFaction.Player,
                true,
                new MMOCharacterStats(),
                100,
                100);
            MMOGameplaySessionService.Players.Register(new MMOPlayerParticipant(
                characterId,
                characterId,
                false,
                false,
                combatant.Identity));
            return combatant;
        }

        private static int Count(List<MMOInventorySlotSaveData> inventory, string itemId)
        {
            int count = 0;
            foreach (MMOInventorySlotSaveData slot in inventory)
            {
                if (slot != null && slot.itemId == itemId)
                {
                    count += slot.quantity;
                }
            }

            return count;
        }
    }
}
#endif
