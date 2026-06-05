using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using RPGClone.Services;
using UnityEngine;

namespace RPGClone.CharacterSelection
{
    public sealed class MMOCloudCharacterRosterRepository : MMOCharacterRosterRepository
    {
        private const string RosterKey = "character_roster_json";

        private readonly MMOCharacterRosterRepository fallback = new MMOLocalCharacterRosterRepository();

        public async Task<MMOCharacterRosterSaveData> LoadAsync()
        {
            try
            {
                await MMOUnityServicesBootstrap.InitializeAsync();
                if (!MMOUnityServicesBootstrap.IsInitialized)
                {
                    return await fallback.LoadAsync();
                }

                object playerData = GetCloudSavePlayerData();
                MethodInfo loadAsync = playerData?.GetType().GetMethod("LoadAsync", new[] { typeof(HashSet<string>) });
                if (loadAsync == null)
                {
                    return await fallback.LoadAsync();
                }

                Task loadTask = (Task)loadAsync.Invoke(playerData, new object[] { new HashSet<string> { RosterKey } });
                await loadTask;

                object result = loadTask.GetType().GetProperty("Result")?.GetValue(loadTask);
                object item = TryGetCloudSaveItem(result, RosterKey);
                if (item == null)
                {
                    return new MMOCharacterRosterSaveData();
                }

                object value = item?.GetType().GetProperty("Value")?.GetValue(item);
                MethodInfo getAs = value?.GetType().GetMethod("GetAs")?.MakeGenericMethod(typeof(string));
                string json = getAs?.Invoke(value, null) as string;
                return string.IsNullOrWhiteSpace(json)
                    ? new MMOCharacterRosterSaveData()
                    : JsonUtility.FromJson<MMOCharacterRosterSaveData>(json) ?? new MMOCharacterRosterSaveData();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Cloud character roster load failed; using local roster. {exception.Message}");
                return await fallback.LoadAsync();
            }
        }

        public async Task SaveAsync(MMOCharacterRosterSaveData roster)
        {
            await fallback.SaveAsync(roster);

            try
            {
                await MMOUnityServicesBootstrap.InitializeAsync();
                if (!MMOUnityServicesBootstrap.IsInitialized)
                {
                    return;
                }

                object playerData = GetCloudSavePlayerData();
                MethodInfo saveAsync = playerData?.GetType().GetMethod("SaveAsync", new[] { typeof(Dictionary<string, object>) });
                if (saveAsync == null)
                {
                    return;
                }

                string json = JsonUtility.ToJson(roster ?? new MMOCharacterRosterSaveData(), true);
                Task saveTask = (Task)saveAsync.Invoke(playerData, new object[]
                {
                    new Dictionary<string, object> { { RosterKey, json } }
                });
                await saveTask;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Cloud character roster save failed; local roster was saved. {exception.Message}");
            }
        }

        private static object GetCloudSavePlayerData()
        {
            Type cloudSaveType = Type.GetType("Unity.Services.CloudSave.CloudSaveService, Unity.Services.CloudSave");
            object cloudSave = cloudSaveType?.GetProperty("Instance")?.GetValue(null);
            object data = cloudSave?.GetType().GetProperty("Data")?.GetValue(cloudSave);
            return data?.GetType().GetProperty("Player")?.GetValue(data);
        }

        private static object TryGetCloudSaveItem(object result, string key)
        {
            if (result == null)
            {
                return null;
            }

            MethodInfo tryGetValue = result.GetType().GetMethod("TryGetValue");
            if (tryGetValue == null)
            {
                return null;
            }

            object[] arguments = { key, null };
            bool found = (bool)tryGetValue.Invoke(result, arguments);
            return found ? arguments[1] : null;
        }
    }
}
