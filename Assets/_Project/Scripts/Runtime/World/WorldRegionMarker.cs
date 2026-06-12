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

    public readonly struct MMOGroundingProbeSettings
    {
        public static MMOGroundingProbeSettings Default => new(
            Physics.DefaultRaycastLayers,
            0.6f,
            12f,
            0.35f,
            true);

        public MMOGroundingProbeSettings(
            LayerMask groundMask,
            float maxSnapUp,
            float maxSnapDown,
            float minSurfaceNormalY,
            bool includeTerrain)
        {
            GroundMask = groundMask;
            MaxSnapUp = Mathf.Max(0f, maxSnapUp);
            MaxSnapDown = Mathf.Max(0f, maxSnapDown);
            MinSurfaceNormalY = Mathf.Clamp01(minSurfaceNormalY);
            IncludeTerrain = includeTerrain;
        }

        public LayerMask GroundMask { get; }
        public float MaxSnapUp { get; }
        public float MaxSnapDown { get; }
        public float MinSurfaceNormalY { get; }
        public bool IncludeTerrain { get; }
    }

    public static class MMOGroundingUtility
    {
        private const float GroundSkin = 0.02f;
        private const float ProbeSkin = 0.05f;
        private const float CandidateEpsilon = 0.001f;

        public static bool SnapTransformToGround(Transform target, Collider collider)
        {
            return SnapTransformToGround(target, collider, MMOGroundingProbeSettings.Default);
        }

        public static bool SnapTransformToGround(Transform target, Collider collider, MMOGroundingProbeSettings settings)
        {
            if (!TryGetGroundedPosition(target, collider, settings, out Vector3 groundedPosition))
            {
                return false;
            }

            target.position = groundedPosition;
            return true;
        }

        public static bool TryGetGroundedPosition(Transform target, Collider collider, out Vector3 groundedPosition)
        {
            return TryGetGroundedPosition(target, collider, MMOGroundingProbeSettings.Default, out groundedPosition);
        }

        public static bool TryGetGroundedPosition(Transform target, Collider collider, MMOGroundingProbeSettings settings, out Vector3 groundedPosition)
        {
            if (target == null)
            {
                groundedPosition = default;
                return false;
            }

            groundedPosition = target.position;
            float bottomOffset = GetBottomOffset(target, collider);
            float footY = target.position.y + bottomOffset;
            if (!TryGetGroundHeight(target, collider, footY, settings, out float groundY))
            {
                return false;
            }

            groundedPosition.y = groundY - bottomOffset + GroundSkin;
            return true;
        }

        private static bool TryGetGroundHeight(Transform target, Collider collider, float footY, MMOGroundingProbeSettings settings, out float groundY)
        {
            Vector3 position = target.position;
            GroundCandidate bestCandidate = default;
            bool foundGround = false;

            if (settings.IncludeTerrain)
            {
                Terrain terrain = FindTerrainAt(position);
                if (terrain != null)
                {
                    float terrainGroundY = terrain.SampleHeight(position) + terrain.transform.position.y;
                    TryAcceptGroundCandidate(terrainGroundY, footY, settings, ref bestCandidate, ref foundGround);
                }
            }

            TryFindRaycastGround(target, collider, footY, settings, ref bestCandidate, ref foundGround);

            groundY = foundGround ? bestCandidate.Height : 0f;
            return foundGround;
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

        private static void TryFindRaycastGround(
            Transform target,
            Collider targetCollider,
            float footY,
            MMOGroundingProbeSettings settings,
            ref GroundCandidate bestCandidate,
            ref bool foundGround)
        {
            Vector3 origin = target.position;
            origin.y = footY + settings.MaxSnapUp + ProbeSkin;
            float probeDistance = settings.MaxSnapUp + settings.MaxSnapDown + ProbeSkin * 2f;
            if (probeDistance <= 0f)
            {
                return;
            }

            Ray ray = new(origin, Vector3.down);
            RaycastHit[] hits = Physics.RaycastAll(ray, probeDistance, settings.GroundMask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null
                    || hit.collider == targetCollider
                    || hit.collider.transform.IsChildOf(target)
                    || hit.normal.y < settings.MinSurfaceNormalY)
                {
                    continue;
                }

                TryAcceptGroundCandidate(hit.point.y, footY, settings, ref bestCandidate, ref foundGround);
            }
        }

        private static float GetBottomOffset(Transform target, Collider collider)
        {
            return collider != null ? collider.bounds.min.y - target.position.y : 0f;
        }

        private static void TryAcceptGroundCandidate(
            float candidateY,
            float footY,
            MMOGroundingProbeSettings settings,
            ref GroundCandidate bestCandidate,
            ref bool foundGround)
        {
            float verticalDelta = candidateY - footY;
            if (verticalDelta > settings.MaxSnapUp + CandidateEpsilon
                || verticalDelta < -settings.MaxSnapDown - CandidateEpsilon)
            {
                return;
            }

            float score = Mathf.Abs(verticalDelta);
            if (!foundGround
                || score < bestCandidate.Score - CandidateEpsilon
                || (Mathf.Abs(score - bestCandidate.Score) <= CandidateEpsilon && candidateY < bestCandidate.Height))
            {
                bestCandidate = new GroundCandidate(candidateY, score);
                foundGround = true;
            }
        }

        private readonly struct GroundCandidate
        {
            public GroundCandidate(float height, float score)
            {
                Height = height;
                Score = score;
            }

            public float Height { get; }
            public float Score { get; }
        }
    }
}
