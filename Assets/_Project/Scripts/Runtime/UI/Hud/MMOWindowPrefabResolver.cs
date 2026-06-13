using System.Collections.Generic;
using UnityEngine;

namespace RPGClone.UI
{
    public enum MMOWindowPrefabId
    {
        Generic,
        Quest,
        Training,
        Merchant
    }

    public static class MMOWindowPrefabResolver
    {
        private static readonly Dictionary<MMOWindowPrefabId, string> Paths = new()
        {
            { MMOWindowPrefabId.Generic, "RPGClone/UI/Windows/GenericWindow" },
            { MMOWindowPrefabId.Quest, "RPGClone/UI/Windows/QuestWindow" },
            { MMOWindowPrefabId.Training, "RPGClone/UI/Windows/TrainingWindow" },
            { MMOWindowPrefabId.Merchant, "RPGClone/UI/Windows/MerchantWindow" }
        };

        public static GameObject Instantiate(MMOWindowPrefabId prefabId, Transform parent, string fallbackName)
        {
            Paths.TryGetValue(prefabId, out string resourcePath);
            GameObject prefab = !string.IsNullOrWhiteSpace(resourcePath) ? Resources.Load<GameObject>(resourcePath) : null;
            GameObject instance = prefab != null
                ? Object.Instantiate(prefab, parent, false)
                : new GameObject(fallbackName, typeof(RectTransform));

            instance.name = fallbackName;
            if (instance.transform.parent != parent)
            {
                instance.transform.SetParent(parent, false);
            }

            return instance;
        }
    }
}
