using System.Collections.Generic;
using UnityEngine;

namespace RPGClone.UI
{
    public enum MMOWindowPrefabId
    {
        Generic,
        Quest,
        Merchant,
        Training,
        Character,
        Spellbook,
        QuestLog
    }

    public static class MMOWindowPrefabResolver
    {
        private static readonly Dictionary<MMOWindowPrefabId, string> Paths = new()
        {
            { MMOWindowPrefabId.Generic, "RPGClone/UI/Windows/GenericWindow" },
            { MMOWindowPrefabId.Quest, "RPGClone/UI/Windows/QuestWindow" },
            { MMOWindowPrefabId.Merchant, "RPGClone/UI/Windows/MerchantWindow" },
            { MMOWindowPrefabId.Training, "RPGClone/UI/Windows/TrainingWindow" },
            { MMOWindowPrefabId.Character, "RPGClone/UI/Windows/CharacterWindow" },
            { MMOWindowPrefabId.Spellbook, "RPGClone/UI/Windows/SpellbookWindow" },
            { MMOWindowPrefabId.QuestLog, "RPGClone/UI/Windows/QuestLogWindow" }
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
