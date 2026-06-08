using RPGClone.Player;
using RPGClone.World;
using UnityEngine;

namespace RPGClone.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class MMOMapPlayerIndicator : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private MMOInputReader inputReader;
        [SerializeField] private MMOThirdPersonCamera gameplayCameraController;
        [SerializeField] private MMOZoneService zoneService;
        [SerializeField] private bool useLargeMapPosition;
        [SerializeField] private float largeMapSize = 720f;
        [SerializeField] private float headingOffsetDegrees;

        private RectTransform rectTransform;

        private void Awake()
        {
            rectTransform = (RectTransform)transform;
            ResolveReferences();
        }

        private void OnEnable()
        {
            UpdateIndicator();
        }

        private void LateUpdate()
        {
            UpdateIndicator();
        }

        public void Configure(
            Transform newPlayer,
            MMOInputReader newInputReader,
            MMOThirdPersonCamera newGameplayCameraController,
            MMOZoneService newZoneService,
            bool shouldUseLargeMapPosition,
            float newLargeMapSize,
            float newHeadingOffsetDegrees)
        {
            player = newPlayer;
            inputReader = newInputReader;
            gameplayCameraController = newGameplayCameraController;
            zoneService = newZoneService;
            useLargeMapPosition = shouldUseLargeMapPosition;
            largeMapSize = Mathf.Max(1f, newLargeMapSize);
            headingOffsetDegrees = newHeadingOffsetDegrees;
            UpdateIndicator();
        }

        private void ResolveReferences()
        {
            if (player == null)
            {
                GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
                player = playerObject != null ? playerObject.transform : null;
            }

            if (inputReader == null && player != null)
            {
                inputReader = player.GetComponent<MMOInputReader>();
            }

            if (gameplayCameraController == null && Camera.main != null)
            {
                gameplayCameraController = Camera.main.GetComponent<MMOThirdPersonCamera>();
            }

            if (zoneService == null)
            {
                zoneService = FindAnyObjectByType<MMOZoneService>();
            }
        }

        private void UpdateIndicator()
        {
            rectTransform ??= (RectTransform)transform;
            ResolveReferences();
            if (player == null)
            {
                return;
            }

            rectTransform.anchoredPosition = useLargeMapPosition
                ? WorldToLargeMapPosition(ResolveMapBounds(), player.position)
                : Vector2.zero;
            rectTransform.localRotation = Quaternion.Euler(0f, 0f, -ResolvePlayerHeadingDegrees());
        }

        private float ResolvePlayerHeadingDegrees()
        {
            float heading = player.eulerAngles.y;
            if (gameplayCameraController != null && inputReader != null && inputReader.Current.IsMouseLooking)
            {
                heading = gameplayCameraController.transform.eulerAngles.y;
            }

            return heading + headingOffsetDegrees;
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

        private Vector2 WorldToLargeMapPosition(Bounds bounds, Vector3 worldPosition)
        {
            Vector3 min = bounds.min;
            Vector3 size = bounds.size;
            float x = size.x > Mathf.Epsilon ? Mathf.Clamp01((worldPosition.x - min.x) / size.x) : 0.5f;
            float y = size.z > Mathf.Epsilon ? Mathf.Clamp01((worldPosition.z - min.z) / size.z) : 0.5f;
            return new Vector2((x - 0.5f) * largeMapSize, (y - 0.5f) * largeMapSize);
        }
    }
}
