using System.Collections.Generic;
using UnityEngine;

namespace RPGClone.Vfx
{
    public interface IMMOAbilityVfxPoolReset
    {
        void ResetForPool();
    }

    /// <summary>Opt-in pool used by high-frequency ability presentation prefabs.</summary>
    public static class MMOAbilityVfxPool
    {
        private static readonly Dictionary<int, Stack<GameObject>> Instances = new();
        private static Transform poolRoot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instances.Clear();
            poolRoot = null;
        }

        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
        {
            if (prefab == null)
            {
                return null;
            }

            MMOAbilityVfxPoolable prefabMarker = prefab.GetComponent<MMOAbilityVfxPoolable>();
            if (prefabMarker == null)
            {
                return Object.Instantiate(prefab, position, rotation, parent);
            }

            int key = prefab.GetInstanceID();
            GameObject instance = null;
            if (Instances.TryGetValue(key, out Stack<GameObject> pool))
            {
                while (pool.Count > 0 && instance == null)
                {
                    instance = pool.Pop();
                }
            }

            if (instance == null)
            {
                instance = Object.Instantiate(prefab);
                instance.GetComponent<MMOAbilityVfxPoolable>().ConfigureRuntimeKey(key);
            }

            instance.transform.SetParent(parent, false);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);
            return instance;
        }

        public static void Release(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            MMOAbilityVfxPoolable marker = instance.GetComponent<MMOAbilityVfxPoolable>();
            if (marker == null || marker.PrefabKey == 0)
            {
                Object.Destroy(instance);
                return;
            }

            foreach (MonoBehaviour behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour is IMMOAbilityVfxPoolReset resettable)
                {
                    resettable.ResetForPool();
                }
            }

            if (!Instances.TryGetValue(marker.PrefabKey, out Stack<GameObject> pool))
            {
                pool = new Stack<GameObject>();
                Instances.Add(marker.PrefabKey, pool);
            }

            if (pool.Count >= marker.MaxRetainedInstances)
            {
                Object.Destroy(instance);
                return;
            }

            instance.SetActive(false);
            instance.transform.SetParent(ResolvePoolRoot(), false);
            pool.Push(instance);
        }

        private static Transform ResolvePoolRoot()
        {
            if (poolRoot != null)
            {
                return poolRoot;
            }

            GameObject root = new("Ability VFX Pool");
            Object.DontDestroyOnLoad(root);
            poolRoot = root.transform;
            return poolRoot;
        }
    }
}
