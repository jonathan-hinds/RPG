using System.Threading.Tasks;
using RPGClone.CharacterSelection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RPGClone.Social
{
    public static class MMOSocialPresenceController
    {
        public static Task RegisterSelectedCharacterNameAsync()
        {
            if (!MMOCharacterSession.HasSelectedCharacter)
            {
                return Task.CompletedTask;
            }

            return RegisterCharacterNameAsync(MMOCharacterSession.SelectedCharacter);
        }

        public static async Task RegisterCharacterNameAsync(MMOCharacterSaveData character)
        {
            if (character == null || string.IsNullOrWhiteSpace(character.characterId))
            {
                return;
            }

            EnsureCharacterNameData(character);
            if (!await ReconcileExistingCharacterNameAsync(character))
            {
                return;
            }

            await MMOSocialServices.CharacterNames.RegisterOrUpdateAsync(new MMOCharacterNameRecord
            {
                playerId = MMOSocialIdentityService.AccountId,
                characterId = character.characterId,
                characterName = character.characterName,
                normalizedCharacterName = character.normalizedCharacterName
            });
        }

        public static async Task SetSelectedCharacterPresenceAsync(MMOCharacterPresenceStatus status, bool joinsAllowed)
        {
            if (!MMOCharacterSession.HasSelectedCharacter)
            {
                return;
            }

            MMOCharacterSaveData character = MMOCharacterSession.SelectedCharacter;
            EnsureCharacterNameData(character);
            if (!await ReconcileExistingCharacterNameAsync(character))
            {
                return;
            }

            await MMOSocialServices.Presence.UpdatePresenceAsync(new MMOCharacterPresenceRecord
            {
                playerId = MMOSocialIdentityService.AccountId,
                characterId = character.characterId,
                characterName = character.characterName,
                normalizedCharacterName = character.normalizedCharacterName,
                status = status,
                sessionId = joinsAllowed ? RPGClone.Services.MMOGameplaySessionService.SessionId : string.Empty,
                currentSceneName = SceneManager.GetActiveScene().name,
                joinsAllowed = joinsAllowed
            });
        }

        public static async Task AdvertiseSelectedLocalSessionAsync()
        {
            if (!MMOCharacterSession.HasSelectedCharacter)
            {
                return;
            }

            MMOCharacterSaveData character = MMOCharacterSession.SelectedCharacter;
            EnsureCharacterNameData(character);
            if (!await ReconcileExistingCharacterNameAsync(character))
            {
                return;
            }

            if (!RPGClone.Services.MMOGameplaySessionService.IsLocalHostedSession)
            {
                await SetSelectedCharacterPresenceAsync(MMOCharacterPresenceStatus.OnlineInWorld, false);
                return;
            }

            IActiveGameplaySession activeSession = MMOLocalHostedGameplaySession.FromCurrentGameplaySession();
            MMOSessionPresenceRecord sessionRecord = activeSession.CreatePresenceRecord(
                MMOSocialIdentityService.AccountId,
                character.characterId,
                character.characterName);
            await MMOSocialServices.Sessions.AdvertiseSessionAsync(sessionRecord);
            await SetSelectedCharacterPresenceAsync(MMOCharacterPresenceStatus.HostingJoinableSession, activeSession.JoinsAllowed);
        }

        public static async Task SetSelectedCharacterOfflineAsync()
        {
            if (!MMOCharacterSession.HasSelectedCharacter)
            {
                return;
            }

            string characterId = MMOCharacterSession.SelectedCharacter.characterId;
            await MMOSocialServices.Presence.SetOfflineAsync(characterId);
            await MMOSocialServices.Sessions.ClearHostedSessionAsync(characterId);
        }

        public static void EnsureCharacterNameData(MMOCharacterSaveData character)
        {
            if (character == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(character.characterId))
            {
                character.characterId = System.Guid.NewGuid().ToString("N");
            }

            if (MMOSocialIdentityService.IsAuthenticated)
            {
                character.accountId = MMOSocialIdentityService.AccountId;
            }

            if (string.IsNullOrWhiteSpace(character.characterName))
            {
                character.characterName = MMOCharacterNameUtility.CreateFallbackName($"{character.race}{character.characterClass}", character.characterId);
            }

            if (!MMOCharacterNameUtility.TryValidate(character.characterName, out string displayName, out string normalizedName, out _))
            {
                displayName = MMOCharacterNameUtility.CreateFallbackName($"{character.race}{character.characterClass}", character.characterId);
                normalizedName = MMOCharacterNameUtility.NormalizeLookupName(displayName);
            }

            character.characterName = displayName;
            character.normalizedCharacterName = normalizedName;
        }

        private static async Task<bool> ReconcileExistingCharacterNameAsync(MMOCharacterSaveData character)
        {
            MMOCharacterNameRecord existing = await MMOSocialServices.CharacterNames.FindByCharacterIdAsync(character.characterId);
            if (existing == null)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(existing.playerId)
                && !string.IsNullOrWhiteSpace(MMOSocialIdentityService.AccountId)
                && !string.Equals(existing.playerId, MMOSocialIdentityService.AccountId, System.StringComparison.Ordinal))
            {
                Debug.LogWarning($"Character {character.characterId} is registered to another account; keeping the existing owner.");
                return false;
            }

            if (!MMOCharacterNameUtility.TryValidate(
                    existing.characterName,
                    out string displayName,
                    out string normalizedName,
                    out _))
            {
                return true;
            }

            character.characterName = displayName;
            character.normalizedCharacterName = normalizedName;
            return true;
        }
    }
}
