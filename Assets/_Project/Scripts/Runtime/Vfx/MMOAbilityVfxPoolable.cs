using UnityEngine;

namespace RPGClone.Vfx
{
    [DisallowMultipleComponent]
    public sealed class MMOAbilityVfxPoolable : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maxRetainedInstances = 16;

        private int prefabKey;

        public int MaxRetainedInstances => Mathf.Max(1, maxRetainedInstances);
        internal int PrefabKey => prefabKey;

        internal void ConfigureRuntimeKey(int key)
        {
            prefabKey = key;
        }

        public void ConfigureAuthoring(int newMaxRetainedInstances)
        {
            maxRetainedInstances = Mathf.Max(1, newMaxRetainedInstances);
        }
    }
}
