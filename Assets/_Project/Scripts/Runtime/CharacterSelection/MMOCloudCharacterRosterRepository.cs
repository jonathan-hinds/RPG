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

        private static string RosterKey => $"{BaseRosterKey}_{MMOSocialIdentityService.AccountId}";

        public async Task<MMOCharacterRosterSaveData> LoadAsync()
        {
            await PrepareCloudSaveAsync("load");
            Dictionary<string, Unity.Services.CloudSave.Models.Item> data =
                await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { RosterKey });
            if (!data.TryGetValue(RosterKey, out Unity.Services.CloudSave.Models.Item item))
            {
                return new MMOCharacterRosterSaveData();
            }

            string json = item.Value.GetAs<string>();
            MMOCharacterRosterSaveData roster = string.IsNullOrWhiteSpace(json)
                ? new MMOCharacterRosterSaveData()
                : JsonUtility.FromJson<MMOCharacterRosterSaveData>(json) ?? new MMOCharacterRosterSaveData();
            roster.characters ??= new List<MMOCharacterSaveData>();
            return roster;
        }

        public async Task SaveAsync(MMOCharacterRosterSaveData roster)
        {
            roster ??= new MMOCharacterRosterSaveData();
            roster.characters ??= new List<MMOCharacterSaveData>();
            await PrepareCloudSaveAsync("save");
            string json = JsonUtility.ToJson(roster, true);
            await CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, object>
            {
                { RosterKey, json }
            });
        }

        private static async Task PrepareCloudSaveAsync(string operation)
        {
            if (!MMOSocialIdentityService.IsAuthenticated)
            {
                throw new InvalidOperationException($"Cloud character roster {operation} requires an authenticated account.");
            }

            await MMOUnityServicesBootstrap.InitializeAsync();
            MMOUnityServicesBootstrap.RefreshAuthenticationState();
            if (!MMOUnityServicesBootstrap.IsInitialized || !AuthenticationService.Instance.IsSignedIn)
            {
                throw new InvalidOperationException($"Cloud character roster {operation} requires an active Unity Authentication session.");
            }
        }
    }
}
