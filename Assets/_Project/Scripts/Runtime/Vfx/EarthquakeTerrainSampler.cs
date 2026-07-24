using RPGClone.Characters;
using UnityEngine;

namespace RPGClone.Vfx.Shaman
{
    public readonly struct EarthquakeTerrainSample
    {
        public readonly Vector3 Position;
        public readonly Vector3 Normal;
        public readonly Color Tint;
        public readonly int SurfaceFrame;
        public readonly Texture SurfaceTexture;
        public readonly Vector4 SurfaceTransform;

        public EarthquakeTerrainSample(
            Vector3 position,
            Vector3 normal,
            Color tint,
            int surfaceFrame,
            Texture surfaceTexture = null,
            Vector4 surfaceTransform = default)
        {
            Position = position;
            Normal = normal;
            Tint = tint;
            SurfaceFrame = Mathf.Clamp(surfaceFrame, 0, 3);
            SurfaceTexture = surfaceTexture;
            SurfaceTransform = surfaceTransform == default ? new Vector4(1f, 1f, 0f, 0f) : surfaceTransform;
        }
    }

    public static class EarthquakeTerrainSampler
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly RaycastHit[] Hits = new RaycastHit[24];

        public static EarthquakeTerrainSample Sample(Vector3 position, Transform ignoredCharacterRoot = null)
        {
            Vector3 origin = position + Vector3.up * 4f;
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                Hits,
                12f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            int bestIndex = -1;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit candidate = Hits[i];
                Collider collider = candidate.collider;
                if (collider == null || candidate.normal.y < 0.12f || IsCharacterCollider(collider, ignoredCharacterRoot)) continue;
                if (candidate.distance >= bestDistance) continue;
                bestDistance = candidate.distance;
                bestIndex = i;
            }

            if (bestIndex < 0)
            {
                return new EarthquakeTerrainSample(position, Vector3.up, new Color(0.66f, 0.49f, 0.3f, 1f), 0);
            }

            RaycastHit hit = Hits[bestIndex];

            if (hit.collider.TryGetComponent(out Terrain terrain))
            {
                return SampleTerrain(terrain, hit);
            }

            Renderer renderer = hit.collider.GetComponentInParent<Renderer>();
            Material material = renderer != null ? renderer.sharedMaterial : null;
            string surfaceName = material != null ? material.name : hit.collider.name;
            Color tint = ReadMaterialColor(material, new Color(0.66f, 0.49f, 0.3f, 1f));
            Texture texture = material != null && material.HasProperty(BaseMapId) ? material.GetTexture(BaseMapId) : null;
            Vector4 transform = material != null && material.HasProperty(BaseMapId)
                ? ToTransform(material.GetTextureScale(BaseMapId), material.GetTextureOffset(BaseMapId))
                : new Vector4(1f, 1f, 0f, 0f);
            return new EarthquakeTerrainSample(hit.point, hit.normal, tint, Classify(surfaceName), texture, transform);
        }

        public static Vector4 AtlasTransform(int frame)
        {
            int clamped = Mathf.Clamp(frame, 0, 3);
            float x = (clamped & 1) == 0 ? 0f : 0.5f;
            float y = clamped < 2 ? 0.5f : 0f;
            return new Vector4(0.5f, 0.5f, x, y);
        }

        private static EarthquakeTerrainSample SampleTerrain(Terrain terrain, RaycastHit hit)
        {
            TerrainData data = terrain.terrainData;
            Vector3 local = hit.point - terrain.transform.position;
            float normalizedX = Mathf.Clamp01(local.x / Mathf.Max(0.01f, data.size.x));
            float normalizedZ = Mathf.Clamp01(local.z / Mathf.Max(0.01f, data.size.z));
            int mapX = Mathf.Clamp(Mathf.RoundToInt(normalizedX * (data.alphamapWidth - 1)), 0, data.alphamapWidth - 1);
            int mapZ = Mathf.Clamp(Mathf.RoundToInt(normalizedZ * (data.alphamapHeight - 1)), 0, data.alphamapHeight - 1);
            float[,,] mix = data.GetAlphamaps(mapX, mapZ, 1, 1);
            int dominant = 0;
            float weight = -1f;
            for (int i = 0; i < mix.GetLength(2); i++)
            {
                if (mix[0, 0, i] <= weight) continue;
                dominant = i;
                weight = mix[0, 0, i];
            }

            TerrainLayer[] layers = data.terrainLayers;
            TerrainLayer layer = dominant >= 0 && dominant < layers.Length ? layers[dominant] : null;
            string layerName = layer != null ? layer.name : terrain.name;
            int frame = Classify(layerName);
            Color tint = frame == 1 ? new Color(0.54f, 0.53f, 0.5f, 1f)
                : frame == 2 ? new Color(0.86f, 0.68f, 0.4f, 1f)
                : frame == 3 ? new Color(0.43f, 0.38f, 0.22f, 1f)
                : new Color(0.66f, 0.49f, 0.3f, 1f);
            Texture texture = layer != null ? layer.diffuseTexture : null;
            Vector4 transform = layer != null
                ? new Vector4(
                    Mathf.Clamp(data.size.x / Mathf.Max(0.1f, layer.tileSize.x), 0.1f, 256f),
                    Mathf.Clamp(data.size.z / Mathf.Max(0.1f, layer.tileSize.y), 0.1f, 256f),
                    layer.tileOffset.x / Mathf.Max(0.1f, layer.tileSize.x),
                    layer.tileOffset.y / Mathf.Max(0.1f, layer.tileSize.y))
                : new Vector4(1f, 1f, 0f, 0f);
            transform.z += normalizedX * transform.x;
            transform.w += normalizedZ * transform.y;
            transform.x = Mathf.Clamp(transform.x * 0.035f, 0.12f, 1.5f);
            transform.y = Mathf.Clamp(transform.y * 0.035f, 0.12f, 1.5f);
            return new EarthquakeTerrainSample(hit.point, hit.normal, tint, frame, texture, transform);
        }

        private static bool IsCharacterCollider(Collider collider, Transform ignoredCharacterRoot)
        {
            Transform colliderTransform = collider.transform;
            if (ignoredCharacterRoot != null
                && (colliderTransform == ignoredCharacterRoot
                    || colliderTransform.IsChildOf(ignoredCharacterRoot)
                    || ignoredCharacterRoot.IsChildOf(colliderTransform)))
            {
                return true;
            }

            return collider.GetComponentInParent<MMOCharacterIdentity>() != null;
        }

        private static Vector4 ToTransform(Vector2 scale, Vector2 offset)
        {
            return new Vector4(scale.x, scale.y, offset.x, offset.y);
        }

        private static Color ReadMaterialColor(Material material, Color fallback)
        {
            if (material == null) return fallback;
            if (material.HasProperty(BaseColorId)) return material.GetColor(BaseColorId);
            if (material.HasProperty(ColorId)) return material.GetColor(ColorId);
            return fallback;
        }

        private static int Classify(string name)
        {
            string lower = string.IsNullOrWhiteSpace(name) ? string.Empty : name.ToLowerInvariant();
            if (lower.Contains("grass") || lower.Contains("moss")) return 3;
            if (lower.Contains("sand") || lower.Contains("desert")) return 2;
            if (lower.Contains("stone") || lower.Contains("rock") || lower.Contains("basalt") || lower.Contains("cliff")) return 1;
            return 0;
        }
    }
}
