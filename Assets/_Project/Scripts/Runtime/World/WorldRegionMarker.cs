using UnityEngine;

namespace RPGClone.World
{
    public enum WorldRegionMarkerType
    {
        PlayerSpawn,
        QuestHub,
        QuestObjective,
        CombatArea,
        Landmark,
        TravelPath
    }

    public sealed class WorldRegionMarker : MonoBehaviour
    {
        [SerializeField] private WorldRegionMarkerType markerType;
        [SerializeField] private string markerId;
        [SerializeField] private float radius = 8f;

        public WorldRegionMarkerType MarkerType => markerType;
        public string MarkerId => markerId;
        public float Radius => radius;

        public void Configure(WorldRegionMarkerType type, string id, float markerRadius)
        {
            markerType = type;
            markerId = id;
            radius = Mathf.Max(0f, markerRadius);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = MarkerColor(markerType);
            Gizmos.DrawWireSphere(transform.position, radius);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 4f);
        }

        private static Color MarkerColor(WorldRegionMarkerType type)
        {
            return type switch
            {
                WorldRegionMarkerType.PlayerSpawn => Color.cyan,
                WorldRegionMarkerType.QuestHub => Color.yellow,
                WorldRegionMarkerType.QuestObjective => Color.magenta,
                WorldRegionMarkerType.CombatArea => Color.red,
                WorldRegionMarkerType.Landmark => Color.green,
                WorldRegionMarkerType.TravelPath => Color.white,
                _ => Color.gray
            };
        }
    }
}
