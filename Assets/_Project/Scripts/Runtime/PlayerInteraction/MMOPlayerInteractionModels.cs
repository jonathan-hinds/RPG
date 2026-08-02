using System;
using System.Collections.Generic;
using RPGClone.CharacterSelection;

namespace RPGClone.PlayerInteraction
{
    public enum MMOPlayerInteractionRequestKind
    {
        RequestDuel,
        RespondToDuel,
        CancelDuel,
        RequestTrade,
        SetTradeOffer,
        SetTradeCopper,
        SetTradeAccepted,
        CancelTrade,
        AcknowledgeTradeSettlement
    }

    public enum MMODuelSessionStatus
    {
        Pending,
        Countdown,
        Active,
        Won,
        Declined,
        Cancelled
    }

    public enum MMOTradeSessionStatus
    {
        Open,
        Completed,
        Cancelled
    }

    [Serializable]
    public sealed class MMOPlayerInteractionRequest
    {
        public string requestId;
        public string sessionId;
        public string actorCharacterId;
        public string targetCharacterId;
        public string interactionId;
        public MMOPlayerInteractionRequestKind kind;
        public bool accepted;
        public int offerSlotIndex = -1;
        public int inventorySlotIndex = -1;
        public int quantity;
        public int offeredCopper;
        public long requestedUtcTicks;

        public static MMOPlayerInteractionRequest Create(
            MMOPlayerInteractionRequestKind kind,
            string actorCharacterId,
            string targetCharacterId = "",
            string interactionId = "")
        {
            return new MMOPlayerInteractionRequest
            {
                requestId = Guid.NewGuid().ToString("N"),
                sessionId = Services.MMOGameplaySessionService.SessionId ?? string.Empty,
                actorCharacterId = actorCharacterId ?? string.Empty,
                targetCharacterId = targetCharacterId ?? string.Empty,
                interactionId = interactionId ?? string.Empty,
                kind = kind,
                requestedUtcTicks = DateTime.UtcNow.Ticks
            };
        }
    }

    [Serializable]
    public sealed class MMODuelSessionSnapshot
    {
        public string duelId;
        public string sessionId;
        public string challengerCharacterId;
        public string challengedCharacterId;
        public MMODuelSessionStatus status;
        public string winnerCharacterId;
        public string loserCharacterId;
        public string endReason;
        public long stateChangedUtcTicks;
        public long countdownEndsUtcTicks;
        public int revision;

        public bool Includes(string characterId)
        {
            return !string.IsNullOrWhiteSpace(characterId)
                && (challengerCharacterId == characterId || challengedCharacterId == characterId);
        }

        public string Other(string characterId)
        {
            return challengerCharacterId == characterId ? challengedCharacterId : challengerCharacterId;
        }
    }

    [Serializable]
    public sealed class MMOTradeOfferEntry
    {
        public int offerSlotIndex;
        public int sourceInventorySlotIndex;
        public string itemId;
        public int quantity;
    }

    [Serializable]
    public sealed class MMOTradeSessionSnapshot
    {
        public string tradeId;
        public string sessionId;
        public string initiatorCharacterId;
        public string recipientCharacterId;
        public MMOTradeSessionStatus status;
        public List<MMOTradeOfferEntry> initiatorOffers = new();
        public List<MMOTradeOfferEntry> recipientOffers = new();
        public int initiatorCopper;
        public int recipientCopper;
        public bool initiatorAccepted;
        public bool recipientAccepted;
        public List<MMOInventorySlotSaveData> initiatorInventoryResult = new();
        public List<MMOInventorySlotSaveData> recipientInventoryResult = new();
        public int initiatorCopperResult;
        public int recipientCopperResult;
        public List<string> settlementAppliedCharacterIds = new();
        public string endReason;
        public long stateChangedUtcTicks;
        public int revision;

        public bool Includes(string characterId)
        {
            return !string.IsNullOrWhiteSpace(characterId)
                && (initiatorCharacterId == characterId || recipientCharacterId == characterId);
        }

        public string Other(string characterId)
        {
            return initiatorCharacterId == characterId ? recipientCharacterId : initiatorCharacterId;
        }

        public List<MMOTradeOfferEntry> OffersFor(string characterId)
        {
            return initiatorCharacterId == characterId ? initiatorOffers : recipientOffers;
        }

        public bool IsAcceptedBy(string characterId)
        {
            return initiatorCharacterId == characterId ? initiatorAccepted : recipientAccepted;
        }

        public int CopperFor(string characterId)
        {
            return initiatorCharacterId == characterId ? initiatorCopper : recipientCopper;
        }
    }

    [Serializable]
    internal sealed class MMOPlayerInteractionNetworkSnapshot
    {
        public List<MMODuelSessionSnapshot> duels = new();
        public List<MMOTradeSessionSnapshot> trades = new();
    }
}
