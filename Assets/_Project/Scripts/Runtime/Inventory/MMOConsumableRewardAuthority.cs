using System;
using RPGClone.Characters;
using RPGClone.Multiplayer;
using RPGClone.Services;
using UnityEngine;

namespace RPGClone.Inventory
{
    [Serializable]
    public sealed class MMOConsumableUseRequest
    {
        public string requestId;
        public string sessionId;
        public string characterId;
        public string itemId;
    }

    public static class MMOConsumableRewardAuthority
    {
        public static bool TryConsumeExperience(MMOItemDefinition item, MMOCharacterIdentity consumer)
        {
            if (!TryGetExperienceReward(item, out int experienceAmount)
                || consumer == null
                || consumer.gameObject != MMOGameplaySessionService.LocalPlayer.PlayerObject)
            {
                return false;
            }

            string sessionId = MMOGameplaySessionService.SessionId;
            string characterId = MMOGameplaySessionService.LocalPlayer.CharacterId;
            if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(characterId))
            {
                return false;
            }

            MMOExperienceComponent experience = consumer.GetComponent<MMOExperienceComponent>();
            if (experience == null || experience.IsAtMaxLevel)
            {
                return false;
            }

            if (!MMOGameplaySessionService.IsHostAuthority)
            {
                return MMONetcodeSharedSessionTransport.TrySubmitToHost(new MMOSharedSessionNetworkOperation
                {
                    kind = MMOSharedSessionNetworkOperationKind.RequestConsumableUse,
                    consumableUseRequest = new MMOConsumableUseRequest
                    {
                        requestId = Guid.NewGuid().ToString("N"),
                        sessionId = sessionId,
                        characterId = characterId,
                        itemId = item.ItemId
                    }
                });
            }

            experience.AddExperience(experienceAmount);
            MMOSharedSessionState.PublishExperienceRewardEvent(
                sessionId,
                characterId,
                item.ItemId,
                experienceAmount,
                characterId);
            return true;
        }

        public static bool TryProcessHostRequest(MMOConsumableUseRequest request)
        {
            if (!MMOGameplaySessionService.IsHostAuthority
                || request == null
                || string.IsNullOrWhiteSpace(request.characterId)
                || string.IsNullOrWhiteSpace(request.itemId)
                || request.itemId.Length > 128
                || !string.Equals(request.sessionId, MMOGameplaySessionService.SessionId, StringComparison.Ordinal))
            {
                return false;
            }

            MMOItemDefinition item = MMOItemCatalog.FindLoadedById(request.itemId);
            if (!TryGetExperienceReward(item, out int experienceAmount))
            {
                return false;
            }

            MMOSharedSessionState.PublishExperienceRewardEvent(
                request.sessionId,
                request.characterId,
                item.ItemId,
                experienceAmount,
                string.Empty);
            return true;
        }

        public static bool TryGetExperienceReward(MMOItemDefinition item, out int experienceAmount)
        {
            experienceAmount = item != null ? item.ExperienceRewardAmount : 0;
            return item != null
                && item.IsConsumable
                && item.ConsumableType == MMOConsumableType.Experience
                && experienceAmount > 0;
        }
    }
}
