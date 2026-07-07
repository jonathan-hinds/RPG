using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RPGClone.Services;
using RPGClone.Social;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using UnityEngine;

namespace RPGClone.CharacterSelection
{
    public sealed class MMOCloudCharacterRosterRepository : MMOCharacterRosterRepository
    {
        private const string BaseRosterKey = "character_roster_json";

        private readonly MMOLocalCharacterRosterRepository localMirror = new();

        private static string RosterKey => MMOSocialIdentityService.IsAuthenticated
            ? $"{BaseRosterKey}_{MMOSocialIdentityService.AccountId}"
            : BaseRosterKey;

        public async Task<MMOCharacterRosterSaveData> LoadAsync()
        {
            MMOCharacterRosterSaveData localRoster = await localMirror.LoadAsync();
            (bool cloudLoaded, MMOCharacterRosterSaveData cloudRoster) = await TryLoadCloudRosterAsync();
            if (!cloudLoaded)
            {
                return localRoster;
            }

            if (HasCharacters(cloudRoster))
            {
                await localMirror.SaveAsync(cloudRoster);
                return cloudRoster;
            }

            if (HasCharacters(localRoster))
            {
                await TrySaveCloudRosterAsync(localRoster);
                return localRoster;
            }

            return cloudRoster ?? new MMOCharacterRosterSaveData();
        }

        public async Task SaveAsync(MMOCharacterRosterSaveData roster)
        {
            roster ??= new MMOCharacterRosterSaveData();
            roster.characters ??= new List<MMOCharacterSaveData>();
            await localMirror.SaveAsync(roster);
            await TrySaveCloudRosterAsync(roster);
        }

        private static async Task<(bool succeeded, MMOCharacterRosterSaveData roster)> TryLoadCloudRosterAsync()
        {
            MMOCharacterRosterSaveData roster = new();
            if (!await TryPrepareCloudSaveAsync("load"))
            {
                return (false, roster);
            }

            try
            {
                Dictionary<string, Unity.Services.CloudSave.Models.Item> data =
                    await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { RosterKey });
                if (!data.TryGetValue(RosterKey, out Unity.Services.CloudSave.Models.Item item))
                {
                    return (true, roster);
                }

                string json = item.Value.GetAs<string>();
                roster = string.IsNullOrWhiteSpace(json)
                    ? new MMOCharacterRosterSaveData()
                    : JsonUtility.FromJson<MMOCharacterRosterSaveData>(json) ?? new MMOCharacterRosterSaveData();
                roster.characters ??= new List<MMOCharacterSaveData>();
                return (true, roster);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Cloud character roster load failed. Using local mirror. {exception.Message}");
                return (false, roster);
            }
        }

        private static async Task<bool> TrySaveCloudRosterAsync(MMOCharacterRosterSaveData roster)
        {
            if (!await TryPrepareCloudSaveAsync("save"))
            {
                return false;
            }

            try
            {
                string json = JsonUtility.ToJson(roster ?? new MMOCharacterRosterSaveData(), true);
                await CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, object>
                {
                    { RosterKey, json }
                });
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Cloud character roster save failed. Local mirror remains saved. {exception.Message}");
                return false;
            }
        }

        private static async Task<bool> TryPrepareCloudSaveAsync(string operation)
        {
            if (!MMOSocialIdentityService.IsAuthenticated)
            {
                Debug.LogWarning($"Cloud character roster {operation} skipped because no account is authenticated.");
                return false;
            }

            await MMOUnityServicesBootstrap.InitializeAsync();
            MMOUnityServicesBootstrap.RefreshAuthenticationState();
            if (!MMOUnityServicesBootstrap.IsInitialized || !AuthenticationService.Instance.IsSignedIn)
            {
                Debug.LogWarning($"Cloud character roster {operation} skipped because Unity Authentication is not signed in.");
                return false;
            }

            return true;
        }

        private static bool HasCharacters(MMOCharacterRosterSaveData roster)
        {
            return roster?.characters != null && roster.characters.Count > 0;
        }
    }
}
