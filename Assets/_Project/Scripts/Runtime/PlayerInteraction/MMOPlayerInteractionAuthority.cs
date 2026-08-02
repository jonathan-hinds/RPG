using System;
using System.Collections.Generic;
using RPGClone.CharacterSelection;
using RPGClone.Combat;
using RPGClone.Multiplayer;
using RPGClone.Services;
using UnityEngine;

namespace RPGClone.PlayerInteraction
{
    public static class MMOPlayerInteractionAuthority
    {
        public const int TradeSlotCount = 6;
        public const float InteractionRange = 12f;
        public const float DuelBoundaryRange = 50f;
        public const float DuelCountdownSeconds = 3f;
        private static readonly long TerminalRetentionTicks = TimeSpan.FromSeconds(20).Ticks;
        private static readonly long DuelRequestTimeoutTicks = TimeSpan.FromSeconds(30).Ticks;
        private const int MaximumRememberedRequests = 4096;
        private static readonly HashSet<string> ProcessedRequestIds = new();
        private static readonly Queue<string> ProcessedRequestOrder = new();

        public static bool TryProcessHostRequest(MMOPlayerInteractionRequest request, out string failureReason)
        {
            failureReason = string.Empty;
            if (!MMOGameplaySessionService.IsHostAuthority)
            {
                failureReason = "Only the session host can resolve player interactions.";
                return false;
            }

            if (request == null
                || string.IsNullOrWhiteSpace(request.requestId)
                || request.sessionId != MMOGameplaySessionService.SessionId
                || string.IsNullOrWhiteSpace(request.actorCharacterId))
            {
                failureReason = "The player interaction request is invalid.";
                return false;
            }

            if (!TryRememberRequest(request.requestId))
            {
                failureReason = "The player interaction request was already processed.";
                return false;
            }

            return request.kind switch
            {
                MMOPlayerInteractionRequestKind.RequestDuel => RequestDuel(request, out failureReason),
                MMOPlayerInteractionRequestKind.RespondToDuel => RespondToDuel(request, out failureReason),
                MMOPlayerInteractionRequestKind.CancelDuel => CancelDuel(request, out failureReason),
                MMOPlayerInteractionRequestKind.RequestTrade => RequestTrade(request, out failureReason),
                MMOPlayerInteractionRequestKind.SetTradeOffer => SetTradeOffer(request, out failureReason),
                MMOPlayerInteractionRequestKind.SetTradeCopper => SetTradeCopper(request, out failureReason),
                MMOPlayerInteractionRequestKind.SetTradeAccepted => SetTradeAccepted(request, out failureReason),
                MMOPlayerInteractionRequestKind.CancelTrade => CancelTrade(request, out failureReason),
                MMOPlayerInteractionRequestKind.AcknowledgeTradeSettlement => AcknowledgeTrade(request, out failureReason),
                _ => false
            };
        }

        public static void TickHost()
        {
            if (!MMOGameplaySessionService.IsHostAuthority)
            {
                return;
            }

            long now = DateTime.UtcNow.Ticks;
            foreach (MMODuelSessionSnapshot duel in new List<MMODuelSessionSnapshot>(MMOPlayerInteractionState.DuelSessions))
            {
                if (duel == null)
                {
                    continue;
                }

                if (duel.status == MMODuelSessionStatus.Countdown && now >= duel.countdownEndsUtcTicks)
                {
                    if (!TryResolvePair(duel.challengerCharacterId, duel.challengedCharacterId, out MMOPlayerParticipant challenger, out MMOPlayerParticipant challenged)
                        || !WithinRange(challenger, challenged, DuelBoundaryRange))
                    {
                        EndDuel(duel, MMODuelSessionStatus.Cancelled, string.Empty, string.Empty, "Duel cancelled: a player is unavailable or out of range.");
                    }
                    else
                    {
                        duel.status = MMODuelSessionStatus.Active;
                        TouchAndPublish(duel);
                    }
                }
                else if (duel.status == MMODuelSessionStatus.Pending
                    && now - duel.stateChangedUtcTicks >= DuelRequestTimeoutTicks)
                {
                    EndDuel(duel, MMODuelSessionStatus.Cancelled, string.Empty, string.Empty, "Duel request expired.");
                }
                else if (duel.status == MMODuelSessionStatus.Active
                    && (!TryResolvePair(duel.challengerCharacterId, duel.challengedCharacterId, out MMOPlayerParticipant first, out MMOPlayerParticipant second)
                        || !WithinRange(first, second, DuelBoundaryRange)))
                {
                    string winner = first.IsValid && !second.IsValid
                        ? duel.challengerCharacterId
                        : second.IsValid && !first.IsValid
                            ? duel.challengedCharacterId
                            : string.Empty;
                    string loser = string.IsNullOrWhiteSpace(winner) ? string.Empty : duel.Other(winner);
                    MMODuelSessionStatus result = string.IsNullOrWhiteSpace(winner)
                        ? MMODuelSessionStatus.Cancelled
                        : MMODuelSessionStatus.Won;
                    EndDuel(duel, result, winner, loser, "Duel ended because a player left the duel area.");
                }
            }

            foreach (MMOTradeSessionSnapshot trade in new List<MMOTradeSessionSnapshot>(MMOPlayerInteractionState.TradeSessions))
            {
                if (trade?.status != MMOTradeSessionStatus.Open)
                {
                    continue;
                }

                if (!TryResolvePair(trade.initiatorCharacterId, trade.recipientCharacterId, out MMOPlayerParticipant first, out MMOPlayerParticipant second)
                    || !WithinRange(first, second, InteractionRange))
                {
                    trade.status = MMOTradeSessionStatus.Cancelled;
                    trade.endReason = "Trade cancelled: a player is unavailable or out of range.";
                    TouchAndPublish(trade);
                }
            }

            MMOPlayerInteractionState.Prune(now, TerminalRetentionTicks);
        }

        public static bool TryResolveDuelDamage(MMOCombatant source, MMOCombatant target, int requestedAmount, out int permittedAmount)
        {
            permittedAmount = requestedAmount;
            if (source == null || target == null || requestedAmount <= 0
                || !TryGetCharacterId(source.Identity, out string sourceId)
                || !TryGetCharacterId(target.Identity, out string targetId))
            {
                return false;
            }

            MMODuelSessionSnapshot duel = MMOPlayerInteractionState.FindCurrentDuel(sourceId);
            if (duel == null || duel.status != MMODuelSessionStatus.Active || !duel.Includes(targetId))
            {
                return false;
            }

            int currentHealth = target.Identity.Health.CurrentValue;
            permittedAmount = Mathf.Clamp(requestedAmount, 0, Mathf.Max(0, currentHealth - 1));
            return true;
        }

        public static void CompleteDuelAfterDamage(MMOCombatant source, MMOCombatant target)
        {
            if (!MMOGameplaySessionService.IsHostAuthority
                || source == null || target == null || target.Identity.Health.CurrentValue > 1
                || !TryGetCharacterId(source.Identity, out string sourceId)
                || !TryGetCharacterId(target.Identity, out string targetId))
            {
                return;
            }

            MMODuelSessionSnapshot duel = MMOPlayerInteractionState.FindCurrentDuel(sourceId);
            if (duel != null && duel.status == MMODuelSessionStatus.Active && duel.Includes(targetId))
            {
                EndDuel(duel, MMODuelSessionStatus.Won, sourceId, targetId, $"{source.Identity.DisplayName} won the duel.");
            }
        }

        private static bool RequestDuel(MMOPlayerInteractionRequest request, out string failureReason)
        {
            if (!ValidateNewInteraction(request, out MMOPlayerParticipant challenger, out MMOPlayerParticipant challenged, out failureReason))
            {
                return false;
            }

            if (MMOPlayerInteractionState.FindCurrentDuel(request.actorCharacterId) != null
                || MMOPlayerInteractionState.FindCurrentDuel(request.targetCharacterId) != null)
            {
                failureReason = "One of the players is already involved in a duel.";
                return false;
            }

            MMODuelSessionSnapshot duel = new()
            {
                duelId = Guid.NewGuid().ToString("N"),
                sessionId = request.sessionId,
                challengerCharacterId = request.actorCharacterId,
                challengedCharacterId = request.targetCharacterId,
                status = MMODuelSessionStatus.Pending
            };
            TouchAndPublish(duel);
            return true;
        }

        private static bool RespondToDuel(MMOPlayerInteractionRequest request, out string failureReason)
        {
            failureReason = string.Empty;
            MMODuelSessionSnapshot duel = MMOPlayerInteractionState.FindDuel(request.interactionId);
            if (duel == null || duel.status != MMODuelSessionStatus.Pending || duel.challengedCharacterId != request.actorCharacterId)
            {
                failureReason = "That duel request is no longer available.";
                return false;
            }

            if (!request.accepted)
            {
                EndDuel(duel, MMODuelSessionStatus.Declined, string.Empty, string.Empty, "Duel request declined.");
                return true;
            }

            if (!TryResolvePair(duel.challengerCharacterId, duel.challengedCharacterId, out MMOPlayerParticipant challenger, out MMOPlayerParticipant challenged)
                || !WithinRange(challenger, challenged, InteractionRange)
                || !BothAlive(challenger, challenged))
            {
                failureReason = "Both players must be alive, available, and nearby to begin a duel.";
                return false;
            }

            duel.status = MMODuelSessionStatus.Countdown;
            duel.countdownEndsUtcTicks = DateTime.UtcNow.AddSeconds(DuelCountdownSeconds).Ticks;
            TouchAndPublish(duel);
            return true;
        }

        private static bool CancelDuel(MMOPlayerInteractionRequest request, out string failureReason)
        {
            failureReason = string.Empty;
            MMODuelSessionSnapshot duel = MMOPlayerInteractionState.FindDuel(request.interactionId);
            if (duel == null || !duel.Includes(request.actorCharacterId))
            {
                failureReason = "That duel is no longer available.";
                return false;
            }

            if (duel.status == MMODuelSessionStatus.Active)
            {
                EndDuel(duel, MMODuelSessionStatus.Won, duel.Other(request.actorCharacterId), request.actorCharacterId, "Duel forfeited.");
            }
            else
            {
                EndDuel(duel, MMODuelSessionStatus.Cancelled, string.Empty, string.Empty, "Duel cancelled.");
            }

            return true;
        }

        private static bool RequestTrade(MMOPlayerInteractionRequest request, out string failureReason)
        {
            if (!ValidateNewInteraction(request, out _, out _, out failureReason))
            {
                return false;
            }

            if (MMOPlayerInteractionState.FindCurrentTrade(request.actorCharacterId) != null
                || MMOPlayerInteractionState.FindCurrentTrade(request.targetCharacterId) != null)
            {
                failureReason = "One of the players is already trading.";
                return false;
            }

            MMOTradeSessionSnapshot trade = new()
            {
                tradeId = Guid.NewGuid().ToString("N"),
                sessionId = request.sessionId,
                initiatorCharacterId = request.actorCharacterId,
                recipientCharacterId = request.targetCharacterId,
                status = MMOTradeSessionStatus.Open
            };
            TouchAndPublish(trade);
            return true;
        }

        private static bool SetTradeOffer(MMOPlayerInteractionRequest request, out string failureReason)
        {
            failureReason = string.Empty;
            MMOTradeSessionSnapshot trade = MMOPlayerInteractionState.FindTrade(request.interactionId);
            if (!ValidateOpenTradeActor(trade, request.actorCharacterId, out failureReason)
                || request.offerSlotIndex < 0 || request.offerSlotIndex >= TradeSlotCount)
            {
                failureReason = string.IsNullOrWhiteSpace(failureReason) ? "That trade slot is invalid." : failureReason;
                return false;
            }

            List<MMOTradeOfferEntry> offers = trade.OffersFor(request.actorCharacterId);
            offers.RemoveAll(candidate => candidate != null && candidate.offerSlotIndex == request.offerSlotIndex);
            if (request.quantity > 0)
            {
                if (!TryGetParticipantSnapshot(request.actorCharacterId, out MMOSessionParticipantSnapshot participant)
                    || participant.characterData == null)
                {
                    failureReason = "The host could not validate the offered inventory.";
                    return false;
                }

                MMOInventorySlotSaveData savedSlot = participant.characterData.inventory?.Find(candidate =>
                    candidate != null && candidate.slotIndex == request.inventorySlotIndex);
                if (savedSlot == null || savedSlot.quantity < request.quantity)
                {
                    failureReason = "The offered item is no longer in that inventory slot.";
                    return false;
                }

                offers.RemoveAll(candidate => candidate != null && candidate.sourceInventorySlotIndex == request.inventorySlotIndex);
                offers.Add(new MMOTradeOfferEntry
                {
                    offerSlotIndex = request.offerSlotIndex,
                    sourceInventorySlotIndex = request.inventorySlotIndex,
                    itemId = savedSlot.itemId,
                    quantity = request.quantity
                });
            }

            trade.initiatorAccepted = false;
            trade.recipientAccepted = false;
            TouchAndPublish(trade);
            return true;
        }

        private static bool SetTradeAccepted(MMOPlayerInteractionRequest request, out string failureReason)
        {
            failureReason = string.Empty;
            MMOTradeSessionSnapshot trade = MMOPlayerInteractionState.FindTrade(request.interactionId);
            if (!ValidateOpenTradeActor(trade, request.actorCharacterId, out failureReason))
            {
                return false;
            }

            if (request.actorCharacterId == trade.initiatorCharacterId)
            {
                trade.initiatorAccepted = request.accepted;
            }
            else
            {
                trade.recipientAccepted = request.accepted;
            }

            if (!trade.initiatorAccepted || !trade.recipientAccepted)
            {
                TouchAndPublish(trade);
                return true;
            }

            if (!TryGetParticipantSnapshot(trade.initiatorCharacterId, out MMOSessionParticipantSnapshot initiator)
                || !TryGetParticipantSnapshot(trade.recipientCharacterId, out MMOSessionParticipantSnapshot recipient)
                || !MMOTradeTransaction.TryBuildSettlement(
                    initiator.characterData,
                    recipient.characterData,
                    trade,
                    out List<MMOInventorySlotSaveData> initiatorInventory,
                    out List<MMOInventorySlotSaveData> recipientInventory,
                    out int initiatorCopper,
                    out int recipientCopper,
                    out failureReason))
            {
                trade.initiatorAccepted = false;
                trade.recipientAccepted = false;
                trade.endReason = failureReason;
                TouchAndPublish(trade);
                return false;
            }

            trade.initiatorInventoryResult = initiatorInventory;
            trade.recipientInventoryResult = recipientInventory;
            trade.initiatorCopperResult = initiatorCopper;
            trade.recipientCopperResult = recipientCopper;
            trade.status = MMOTradeSessionStatus.Completed;
            trade.endReason = string.Empty;
            MMOSharedSessionState.ApplyAuthoritativeTradeSettlement(
                trade.initiatorCharacterId,
                initiatorInventory,
                initiatorCopper,
                trade.recipientCharacterId,
                recipientInventory,
                recipientCopper);
            TouchAndPublish(trade);
            MMOPlayerInteractionService.TryApplyLocalTradeSettlement(trade);
            return true;
        }

        private static bool SetTradeCopper(MMOPlayerInteractionRequest request, out string failureReason)
        {
            failureReason = string.Empty;
            MMOTradeSessionSnapshot trade = MMOPlayerInteractionState.FindTrade(request.interactionId);
            if (!ValidateOpenTradeActor(trade, request.actorCharacterId, out failureReason)
                || !TryGetParticipantSnapshot(request.actorCharacterId, out MMOSessionParticipantSnapshot participant))
            {
                return false;
            }

            int offered = Mathf.Max(0, request.offeredCopper);
            if (participant.characterData == null || participant.characterData.copper < offered)
            {
                failureReason = "You do not have that much money.";
                return false;
            }

            if (request.actorCharacterId == trade.initiatorCharacterId)
            {
                trade.initiatorCopper = offered;
            }
            else
            {
                trade.recipientCopper = offered;
            }

            trade.initiatorAccepted = false;
            trade.recipientAccepted = false;
            TouchAndPublish(trade);
            return true;
        }

        private static bool CancelTrade(MMOPlayerInteractionRequest request, out string failureReason)
        {
            failureReason = string.Empty;
            MMOTradeSessionSnapshot trade = MMOPlayerInteractionState.FindTrade(request.interactionId);
            if (!ValidateOpenTradeActor(trade, request.actorCharacterId, out failureReason))
            {
                return false;
            }

            trade.status = MMOTradeSessionStatus.Cancelled;
            trade.endReason = "Trade cancelled.";
            TouchAndPublish(trade);
            return true;
        }

        private static bool AcknowledgeTrade(MMOPlayerInteractionRequest request, out string failureReason)
        {
            failureReason = string.Empty;
            MMOTradeSessionSnapshot trade = MMOPlayerInteractionState.FindTrade(request.interactionId);
            if (trade == null || trade.status != MMOTradeSessionStatus.Completed || !trade.Includes(request.actorCharacterId))
            {
                failureReason = "That completed trade is no longer available.";
                return false;
            }

            if (!trade.settlementAppliedCharacterIds.Contains(request.actorCharacterId))
            {
                trade.settlementAppliedCharacterIds.Add(request.actorCharacterId);
                TouchAndPublish(trade);
            }

            return true;
        }

        private static bool ValidateNewInteraction(
            MMOPlayerInteractionRequest request,
            out MMOPlayerParticipant actor,
            out MMOPlayerParticipant target,
            out string failureReason)
        {
            failureReason = string.Empty;
            actor = default;
            target = default;
            if (request.actorCharacterId == request.targetCharacterId
                || !TryResolvePair(request.actorCharacterId, request.targetCharacterId, out actor, out target))
            {
                failureReason = "Both players must be available in this session.";
                return false;
            }

            if (!WithinRange(actor, target, InteractionRange))
            {
                failureReason = "That player is too far away.";
                return false;
            }

            if (!BothAlive(actor, target))
            {
                failureReason = "Both players must be alive.";
                return false;
            }

            return true;
        }

        private static bool ValidateOpenTradeActor(MMOTradeSessionSnapshot trade, string actorId, out string failureReason)
        {
            failureReason = string.Empty;
            if (trade == null || trade.status != MMOTradeSessionStatus.Open || !trade.Includes(actorId))
            {
                failureReason = "That trade is no longer open.";
                return false;
            }

            if (!TryResolvePair(trade.initiatorCharacterId, trade.recipientCharacterId, out MMOPlayerParticipant first, out MMOPlayerParticipant second)
                || !WithinRange(first, second, InteractionRange))
            {
                failureReason = "The other player is unavailable or too far away.";
                return false;
            }

            return true;
        }

        private static void EndDuel(
            MMODuelSessionSnapshot duel,
            MMODuelSessionStatus status,
            string winnerId,
            string loserId,
            string reason)
        {
            if (duel == null)
            {
                return;
            }

            duel.status = status;
            duel.winnerCharacterId = winnerId ?? string.Empty;
            duel.loserCharacterId = loserId ?? string.Empty;
            duel.endReason = reason ?? string.Empty;
            TouchAndPublish(duel);
            MMOPlayerInteractionService.CleanupDuelEffects(duel);
        }

        private static void TouchAndPublish(MMODuelSessionSnapshot duel)
        {
            duel.revision++;
            duel.stateChangedUtcTicks = DateTime.UtcNow.Ticks;
            MMOPlayerInteractionState.Upsert(duel);
            MMONetcodeSharedSessionTransport.BroadcastOperationIfHost(new MMOSharedSessionNetworkOperation
            {
                kind = MMOSharedSessionNetworkOperationKind.UpsertDuelSession,
                duelSession = duel
            });
        }

        private static void TouchAndPublish(MMOTradeSessionSnapshot trade)
        {
            trade.revision++;
            trade.stateChangedUtcTicks = DateTime.UtcNow.Ticks;
            MMOPlayerInteractionState.Upsert(trade);
            MMONetcodeSharedSessionTransport.BroadcastOperationIfHost(new MMOSharedSessionNetworkOperation
            {
                kind = MMOSharedSessionNetworkOperationKind.UpsertTradeSession,
                tradeSession = trade
            });
        }

        private static bool TryResolvePair(string firstId, string secondId, out MMOPlayerParticipant first, out MMOPlayerParticipant second)
        {
            bool foundFirst = MMOGameplaySessionService.Players.TryGetParticipantByCharacterId(firstId, out first);
            bool foundSecond = MMOGameplaySessionService.Players.TryGetParticipantByCharacterId(secondId, out second);
            return foundFirst && foundSecond && first.IsValid && second.IsValid;
        }

        private static bool WithinRange(MMOPlayerParticipant first, MMOPlayerParticipant second, float range)
        {
            return first.IsValid && second.IsValid
                && (first.Identity.transform.position - second.Identity.transform.position).sqrMagnitude <= range * range;
        }

        private static bool BothAlive(MMOPlayerParticipant first, MMOPlayerParticipant second)
        {
            MMOCombatant firstCombatant = first.Identity != null ? first.Identity.GetComponent<MMOCombatant>() : null;
            MMOCombatant secondCombatant = second.Identity != null ? second.Identity.GetComponent<MMOCombatant>() : null;
            return firstCombatant != null && secondCombatant != null && firstCombatant.IsAlive && secondCombatant.IsAlive;
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

        private static bool TryGetParticipantSnapshot(string characterId, out MMOSessionParticipantSnapshot participant)
        {
            foreach (MMOSessionParticipantSnapshot candidate in MMOSharedSessionState.GetParticipants(MMOGameplaySessionService.SessionId))
            {
                if (candidate != null && candidate.characterId == characterId)
                {
                    participant = candidate;
                    return true;
                }
            }

            participant = null;
            return false;
        }

        private static bool TryRememberRequest(string requestId)
        {
            if (!ProcessedRequestIds.Add(requestId))
            {
                return false;
            }

            ProcessedRequestOrder.Enqueue(requestId);
            while (ProcessedRequestOrder.Count > MaximumRememberedRequests)
            {
                ProcessedRequestIds.Remove(ProcessedRequestOrder.Dequeue());
            }

            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRequestHistory()
        {
            ProcessedRequestIds.Clear();
            ProcessedRequestOrder.Clear();
        }
    }
}
