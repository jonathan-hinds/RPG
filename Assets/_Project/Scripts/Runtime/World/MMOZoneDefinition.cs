using UnityEngine;

namespace RPGClone.World
{
    [CreateAssetMenu(menuName = "RPG Clone/World/Zone", fileName = "Zone")]
    public sealed class MMOZoneDefinition : ScriptableObject
    {
        [SerializeField] private string zoneId = "zone";
        [SerializeField] private string displayName = "Zone";
        [SerializeField] private Bounds worldBounds = new(Vector3.zero, new Vector3(512f, 160f, 512f));

        public string ZoneId => string.IsNullOrWhiteSpace(zoneId) ? name : zoneId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? ZoneId : displayName;
        public Bounds WorldBounds => worldBounds;
        public Vector3 Center => worldBounds.center;
        public Vector3 Size => worldBounds.size;
        public float MapHalfSize => Mathf.Max(worldBounds.size.x, worldBounds.size.z) * 0.5f;

        public void Configure(string newZoneId, string newDisplayName, Bounds newWorldBounds)
        {
            zoneId = string.IsNullOrWhiteSpace(newZoneId) ? name : newZoneId.Trim();
            displayName = string.IsNullOrWhiteSpace(newDisplayName) ? zoneId : newDisplayName.Trim();
            worldBounds = newWorldBounds;
        }

        public bool Contains(Vector3 worldPosition)
        {
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;
            return worldPosition.x >= min.x
                && worldPosition.x <= max.x
                && worldPosition.z >= min.z
                && worldPosition.z <= max.z;
        }

        public Vector2 WorldToNormalized(Vector3 worldPosition)
        {
            Vector3 min = worldBounds.min;
            Vector3 size = worldBounds.size;
            float x = size.x > Mathf.Epsilon ? (worldPosition.x - min.x) / size.x : 0.5f;
            float y = size.z > Mathf.Epsilon ? (worldPosition.z - min.z) / size.z : 0.5f;
            return new Vector2(Mathf.Clamp01(x), Mathf.Clamp01(y));
        }

        public Vector3 NormalizedToWorld(Vector2 normalizedPosition, float y)
        {
            Vector3 min = worldBounds.min;
            Vector3 size = worldBounds.size;
            return new Vector3(
                min.x + Mathf.Clamp01(normalizedPosition.x) * size.x,
                y,
                min.z + Mathf.Clamp01(normalizedPosition.y) * size.z);
        }
    }
}
