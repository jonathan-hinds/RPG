using System.Collections.Generic;
using RPGClone.Buffs;
using RPGClone.CharacterSelection;
using RPGClone.Combat;
using RPGClone.Inventory;
using RPGClone.Multiplayer;
using RPGClone.Quests;
using RPGClone.Services;
using UnityEngine;

namespace RPGClone.PlayerInteraction
{
    public static class MMOPlayerInteractionService
    {
        private static readonly HashSet<string> AppliedTradeSettlements = new();

        public static string LastFailureReason { get; private set; } = string.Empty;

        internal static void ResetRuntimeState()
        {
            AppliedTradeSettlements.Clear();
            LastFailureReason = string.Empty;
        }

        public static bool RequestDuel(string targetCharacterId)
        {
            return Submit(MMOPlayerInteractionRequest.Create(
                MMOPlayerInteractionRequestKind.RequestDuel,
                LocalCharacterId,
                targetCharacterId));
        }

        public static bool RespondToDuel(string duelId, bool accept)
        {
            MMOPlayerInteractionRequest request = MMOPlayerInteractionRequest.Create(
                MMOPlayerInteractionRequestKind.RespondToDuel,
                LocalCharacterId,
                interactionId: duelId);
            request.accepted = accept;
            return Submit(request);
        }

        public static bool CancelDuel(string duelId)
        {
            return Submit(MMOPlayerInteractionRequest.Create(
                MMOPlayerInteractionRequestKind.CancelDuel,
                LocalCharacterId,
                interactionId: duelId));
        }

        public static bool RequestTrade(string targetCharacterId)
        {
            return Submit(MMOPlayerInteractionRequest.Create(
                MMOPlayerInteractionRequestKind.RequestTrade,
                LocalCharacterId,
                targetCharacterId));
        }

        public static bool SetTradeOffer(string tradeId, int offerSlotIndex, int inventorySlotIndex, int quantity)
        {
            MMOPlayerInteractionRequest request = MMOPlayerInteractionRequest.Create(
                MMOPlayerInteractionRequestKind.SetTradeOffer,
                LocalCharacterId,
                interactionId: tradeId);
            request.offerSlotIndex = offerSlotIndex;
            request.inventorySlotIndex = inventorySlotIndex;
            request.quantity = Mathf.Max(0, quantity);
            return Submit(request);
        }

        public static bool SetTradeAccepted(string tradeId, bool accepted)
        {
            MMOPlayerInteractionRequest request = MMOPlayerInteractionRequest.Create(
                MMOPlayerInteractionRequestKind.SetTradeAccepted,
                LocalCharacterId,
                interactionId: tradeId);
            request.accepted = accepted;
            return Submit(request);
        }

        public static bool SetTradeCopper(string tradeId, int copper)
        {
            MMOPlayerInteractionRequest request = MMOPlayerInteractionRequest.Create(
                MMOPlayerInteractionRequestKind.SetTradeCopper,
                LocalCharacterId,
                interactionId: tradeId);
            request.offeredCopper = Mathf.Max(0, copper);
            return Submit(request);
        }

        public static bool CancelTrade(string tradeId)
        {
            return Submit(MMOPlayerInteractionRequest.Create(
                MMOPlayerInteractionRequestKind.CancelTrade,
                LocalCharacterId,
                interactionId: tradeId));
        }

        public static bool TryHandleInventoryRightClickForTrade(MMOInventoryContainer inventory, int inventorySlotIndex)
        {
            string characterId = LocalCharacterId;
            MMOTradeSessionSnapshot trade = MMOPlayerInteractionState.FindCurrentTrade(characterId);
            if (trade == null)
            {
                return false;
            }

            LastFailureReason = string.Empty;
            MMOItemStack stack = inventory != null ? inventory.GetSlot(inventorySlotIndex) : null;
            if (stack == null || stack.IsEmpty)
            {
                LastFailureReason = "That inventory slot is empty.";
                return true;
            }

            if (!MMOGameplaySessionService.LocalPlayer.TryGetComponent(out MMOInventoryContainer localInventory)
                || inventory != localInventory)
            {
                LastFailureReason = "Only items from your inventory can be offered.";
                return true;
            }

            if (stack.Item.ItemType == MMOItemType.Quest)
            {
                LastFailureReason = $"{stack.Item.DisplayName} cannot be traded.";
                return true;
            }

            List<MMOTradeOfferEntry> offers = trade.OffersFor(characterId);
            int offerSlotIndex = -1;
            for (int i = 0; i < offers.Count; i++)
            {
                MMOTradeOfferEntry offer = offers[i];
                if (offer != null && offer.sourceInventorySlotIndex == inventorySlotIndex)
                {
                    offerSlotIndex = offer.offerSlotIndex;
                    break;
                }
            }

            for (int i = 0; offerSlotIndex < 0 && i < MMOPlayerInteractionAuthority.TradeSlotCount; i++)
            {
                bool occupied = offers.Exists(candidate => candidate != null && candidate.offerSlotIndex == i);
                if (!occupied)
                {
                    offerSlotIndex = i;
                }
            }

            if (offerSlotIndex < 0)
            {
                LastFailureReason = "All trade slots are already occupied.";
                return true;
            }

            SetTradeOffer(trade.tradeId, offerSlotIndex, inventorySlotIndex, stack.Quantity);
            return true;
        }

        public static void Tick()
        {
            MMOPlayerInteractionAuthority.TickHost();
            ProcessAuthoritativeState();
        }

        public static bool CanPlayersDamageEachOther(
            Characters.MMOCharacterIdentity source,
            Characters.MMOCharacterIdentity target)
        {
            if (!TryGetCharacterId(source, out string sourceId) || !TryGetCharacterId(target, out string targetId))
            {
                return false;
            }

            return MMOPlayerInteractionState.AreActivelyDueling(sourceId, targetId);
        }

        public static void CleanupDuelEffects(MMODuelSessionSnapshot duel)
        {
            if (duel == null
                || duel.status == MMODuelSessionStatus.Pending
                || duel.status == MMODuelSessionStatus.Countdown
                || duel.status == MMODuelSessionStatus.Active)
            {
                return;
            }

            MMOGameplaySessionService.Players.TryGetParticipantByCharacterId(duel.challengerCharacterId, out MMOPlayerParticipant challenger);
            MMOGameplaySessionService.Players.TryGetParticipantByCharacterId(duel.challengedCharacterId, out MMOPlayerParticipant challenged);
            MMOCombatant challengerCombatant = challenger.Identity != null ? challenger.Identity.GetComponent<MMOCombatant>() : null;
            MMOCombatant challengedCombatant = challenged.Identity != null ? challenged.Identity.GetComponent<MMOCombatant>() : null;
            challenger.Identity?.GetComponent<MMOCharacterBuffController>()?.RemoveHarmfulEffectsFrom(challengedCombatant);
            challenged.Identity?.GetComponent<MMOCharacterBuffController>()?.RemoveHarmfulEffectsFrom(challengerCombatant);
            challenger.Identity?.GetComponent<MMOAutoAttackController>()?.StopAutoAttackAgainst(challenged.Identity);
            challenged.Identity?.GetComponent<MMOAutoAttackController>()?.StopAutoAttackAgainst(challenger.Identity);
            challengerCombatant?.DisengageCombatWith(challengedCombatant);
        }

        public static bool TryApplyLocalTradeSettlement(MMOTradeSessionSnapshot trade)
        {
            string characterId = LocalCharacterId;
            if (trade == null || trade.status != MMOTradeSessionStatus.Completed || !trade.Includes(characterId)
                || AppliedTradeSettlements.Contains(trade.tradeId)
                || trade.settlementAppliedCharacterIds.Contains(characterId))
            {
                return false;
            }

            if (!MMOGameplaySessionService.LocalPlayer.TryGetComponent(out MMOInventoryContainer inventory)
                || !MMOGameplaySessionService.LocalPlayer.TryGetComponent(out MMOCurrencyWallet wallet))
            {
                return false;
            }

            List<MMOInventorySlotSaveData> result = trade.initiatorCharacterId == characterId
                ? trade.initiatorInventoryResult
                : trade.recipientInventoryResult;
            int copperResult = trade.initiatorCharacterId == characterId
                ? trade.initiatorCopperResult
                : trade.recipientCopperResult;

            inventory.Clear();
            foreach (MMOInventorySlotSaveData slot in result ?? new List<MMOInventorySlotSaveData>())
            {
                MMOItemDefinition item = MMOItemCatalog.FindLoadedById(slot.itemId);
                if (item == null)
                {
                    foreach (MMOItemCatalog catalog in Resources.LoadAll<MMOItemCatalog>(string.Empty))
                    {
                        item = catalog != null ? catalog.FindById(slot.itemId) : null;
                        if (item != null)
                        {
                            break;
                        }
                    }
                }

                if (item != null)
                {
                    inventory.SetSlot(slot.slotIndex, item, slot.quantity);
                }
            }

            wallet.SetCopper(copperResult);
            AppliedTradeSettlements.Add(trade.tradeId);
            if (MMOGameplaySessionService.LocalPlayer.TryGetComponent(out MMOCharacterPersistenceAgent persistence))
            {
                _ = persistence.SaveCurrentCharacterAsync();
            }

            MMOPlayerInteractionRequest acknowledgement = MMOPlayerInteractionRequest.Create(
                MMOPlayerInteractionRequestKind.AcknowledgeTradeSettlement,
                characterId,
                interactionId: trade.tradeId);
            Submit(acknowledgement);
            return true;
        }

        public static string GetPlayerName(string characterId)
        {
            return MMOGameplaySessionService.Players.TryGetParticipantByCharacterId(characterId, out MMOPlayerParticipant participant)
                && participant.Identity != null
                ? participant.Identity.DisplayName
                : "Player";
        }

        private static bool Submit(MMOPlayerInteractionRequest request)
        {
            LastFailureReason = string.Empty;
            if (request == null || string.IsNullOrWhiteSpace(request.actorCharacterId))
            {
                LastFailureReason = "No local player is registered in the session.";
                return false;
            }

            if (MMOGameplaySessionService.IsHostAuthority)
            {
                bool succeeded = MMOPlayerInteractionAuthority.TryProcessHostRequest(request, out string failureReason);
                LastFailureReason = failureReason;
                return succeeded;
            }

            bool submitted = MMONetcodeSharedSessionTransport.TrySubmitToHost(new MMOSharedSessionNetworkOperation
            {
                kind = MMOSharedSessionNetworkOperationKind.SubmitPlayerInteractionRequest,
                playerInteractionRequest = request
            });
            if (!submitted)
            {
                LastFailureReason = "The interaction request could not be sent to the session host.";
            }

            return submitted;
        }

        private static void ProcessAuthoritativeState()
        {
            foreach (MMODuelSessionSnapshot duel in MMOPlayerInteractionState.DuelSessions)
            {
                CleanupDuelEffects(duel);
            }

            foreach (MMOTradeSessionSnapshot trade in MMOPlayerInteractionState.TradeSessions)
            {
                TryApplyLocalTradeSettlement(trade);
            }
        }

        private static bool TryGetCharacterId(Characters.MMOCharacterIdentity identity, out string characterId)
        {
            if (MMOGameplaySessionService.Players.TryGetParticipant(identity, out MMOPlayerParticipant participant))
            {
                characterId = participant.CharacterId;
                return !string.IsNullOrWhiteSpace(characterId);
            }

            characterId = string.Empty;
            return false;
        }

        private static string LocalCharacterId => MMOGameplaySessionService.LocalPlayer.CharacterId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            AppliedTradeSettlements.Clear();
            LastFailureReason = string.Empty;
        }
    }
}
