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

    public static class MMOGroundingUtility
    {
        private const float GroundSkin = 0.02f;

        public static bool SnapTransformToGround(Transform target, Collider collider)
        {
            if (target == null)
            {
                return false;
            }

            if (!TryGetGroundHeight(target, out float groundY))
            {
                return false;
            }

            float bottomOffset = collider != null ? collider.bounds.min.y - target.position.y : 0f;
            Vector3 position = target.position;
            position.y = groundY - bottomOffset + GroundSkin;
            target.position = position;
            return true;
        }

        private static bool TryGetGroundHeight(Transform target, out float groundY)
        {
            Vector3 position = target.position;
            Terrain terrain = FindTerrainAt(position);
            if (terrain != null)
            {
                groundY = terrain.SampleHeight(position) + terrain.transform.position.y;
                return true;
            }

            return TryRaycastGround(target, out groundY);
        }

        private static Terrain FindTerrainAt(Vector3 position)
        {
            Terrain[] terrains = Terrain.activeTerrains;
            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i];
                if (terrain == null || terrain.terrainData == null)
                {
                    continue;
                }

                Vector3 terrainPosition = terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                if (position.x >= terrainPosition.x
                    && position.z >= terrainPosition.z
                    && position.x <= terrainPosition.x + size.x
                    && position.z <= terrainPosition.z + size.z)
                {
                    return terrain;
                }
            }

            return Terrain.activeTerrain;
        }

        private static bool TryRaycastGround(Transform target, out float groundY)
        {
            Vector3 position = target.position;
            Ray ray = new(position + Vector3.up * 50f, Vector3.down);
            RaycastHit[] hits = Physics.RaycastAll(ray, 120f, ~0, QueryTriggerInteraction.Ignore);
            float closestDistance = float.PositiveInfinity;
            groundY = 0f;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null
                    || hit.collider.transform.IsChildOf(target)
                    || hit.distance >= closestDistance)
                {
                    continue;
                }

                closestDistance = hit.distance;
                groundY = hit.point.y;
            }

            return closestDistance < float.PositiveInfinity;
        }
    }
}
