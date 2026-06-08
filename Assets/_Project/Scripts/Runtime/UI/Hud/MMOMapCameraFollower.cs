using RPGClone.World;
using UnityEngine;

namespace RPGClone.UI
{
    [RequireComponent(typeof(Camera))]
    public sealed class MMOMapCameraFollower : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private MMOZoneService zoneService;
        [SerializeField] private RectTransform visibilityRoot;
        [SerializeField] private bool followPlayer = true;
        [SerializeField] private bool renderWhenHidden = true;
        [SerializeField, Min(1f)] private float orthographicSize = 72f;
        [SerializeField] private float heightPadding = 260f;

        private Camera mapCamera;

        private void Awake()
        {
            mapCamera = GetComponent<Camera>();
            ResolveReferences();
        }

        private void OnEnable()
        {
            UpdateCamera();
        }

        private void LateUpdate()
        {
            UpdateCamera();
        }

        public void Configure(
            Transform newPlayer,
            MMOZoneService newZoneService,
            bool shouldFollowPlayer,
            float newOrthographicSize,
            RectTransform newVisibilityRoot,
            bool shouldRenderWhenHidden)
        {
            player = newPlayer;
            zoneService = newZoneService;
            followPlayer = shouldFollowPlayer;
            orthographicSize = Mathf.Max(1f, newOrthographicSize);
            visibilityRoot = newVisibilityRoot;
            renderWhenHidden = shouldRenderWhenHidden;
            UpdateCamera();
        }

        public void SetOrthographicSize(float newOrthographicSize)
        {
            orthographicSize = Mathf.Max(1f, newOrthographicSize);
        }

        private void ResolveReferences()
        {
            if (player == null)
            {
                GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
                player = playerObject != null ? playerObject.transform : null;
            }

            if (zoneService == null)
            {
                zoneService = FindAnyObjectByType<MMOZoneService>();
            }
        }

        private void UpdateCamera()
        {
            mapCamera ??= GetComponent<Camera>();
            ResolveReferences();
            if (mapCamera == null || player == null)
            {
                return;
            }

            Bounds mapBounds = ResolveMapBounds();
            Vector3 center = followPlayer ? player.position : mapBounds.center;
            float cameraHeight = Mathf.Max(mapBounds.max.y + heightPadding, player.position.y + heightPadding);
            mapCamera.transform.SetPositionAndRotation(
                new Vector3(center.x, cameraHeight, center.z),
                Quaternion.Euler(90f, 0f, 0f));

            mapCamera.orthographicSize = followPlayer
                ? orthographicSize
                : Mathf.Max(20f, Mathf.Max(mapBounds.size.x, mapBounds.size.z) * 0.5f);

            if (mapCamera.targetTexture != null && (renderWhenHidden || visibilityRoot == null || visibilityRoot.gameObject.activeInHierarchy))
            {
                mapCamera.Render();
            }
        }

        private Bounds ResolveMapBounds()
        {
            MMOZoneDefinition zone = zoneService != null ? zoneService.CurrentZone : null;
            if (zone != null)
            {
                return zone.WorldBounds;
            }

            Terrain terrain = Terrain.activeTerrain;
            if (terrain != null && terrain.terrainData != null)
            {
                Vector3 size = terrain.terrainData.size;
                return new Bounds(terrain.transform.position + size * 0.5f, new Vector3(size.x, Mathf.Max(size.y, 180f), size.z));
            }

            Vector3 center = player != null ? player.position : Vector3.zero;
            return new Bounds(center, new Vector3(520f, 180f, 520f));
        }
    }
}
