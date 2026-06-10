using System.Collections.Generic;
using RPGClone.Player;
using RPGClone.Quests;
using RPGClone.Services;
using RPGClone.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace RPGClone.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class MMOMapHudPresenter : MonoBehaviour
    {
        private const int MinimapTextureSize = 512;
        private const int LargeMapTextureSize = 1024;
        private const float MinimapDiameter = 190f;
        private const float MinimapDefaultZoom = 72f;
        private const float MinimapMinZoom = 36f;
        private const float MinimapMaxZoom = 180f;
        private const float MinimapZoomStep = 18f;
        private const float LargeMapSize = 720f;
        private const float MarkerRefreshSeconds = 0.35f;

        [SerializeField] private MMOZoneService zoneService;
        [SerializeField] private MMOQuestLog questLog;
        [SerializeField] private Transform player;
        [SerializeField] private MMOInputReader inputReader;
        [SerializeField] private MMOThirdPersonCamera gameplayCameraController;
        [SerializeField] private Key largeMapToggleKey = Key.M;
        [SerializeField, Min(24f)] private float minimapZoom = MinimapDefaultZoom;
        [SerializeField] private float playerArrowHeadingOffsetDegrees;

        private RectTransform root;
        private RectTransform minimapRoot;
        private RectTransform minimapMask;
        private RectTransform minimapMarkerLayer;
        private RectTransform minimapPlayerArrow;
        private Text minimapZoneText;
        private RawImage minimapRawImage;
        private RectTransform largeMapRoot;
        private RectTransform largeMapImageRect;
        private RectTransform largeMapMarkerLayer;
        private RectTransform largeMapPlayerArrow;
        private Text largeMapZoneText;
        private RawImage largeMapRawImage;
        private Camera minimapCamera;
        private Camera largeMapCamera;
        private RenderTexture minimapTexture;
        private RenderTexture largeMapTexture;
        private Sprite circleSprite;
        private Sprite ringSprite;
        private Sprite triangleSprite;
        private Sprite diamondSprite;
        private bool built;
        private bool updatingLiveMapState;
        private int lastMapRenderFrame = -1;
        private float nextMarkerRefreshTime;
        private readonly List<MMOMapMarkerData> markers = new();
        private readonly List<MapMarkerView> minimapMarkerViews = new();
        private readonly List<MapMarkerView> largeMapMarkerViews = new();

        private void Awake()
        {
            ResolveReferences();
            BuildIfNeeded();
            EnsureMapCameras();
            RefreshMarkers(true);
            UpdateLiveMapState(true);
        }

        private void OnEnable()
        {
            Canvas.willRenderCanvases -= OnWillRenderCanvases;
            Canvas.willRenderCanvases += OnWillRenderCanvases;
            ResolveReferences();
            if (questLog != null)
            {
                questLog.Changed -= OnQuestLogChanged;
                questLog.Changed += OnQuestLogChanged;
            }

            if (zoneService != null)
            {
                zoneService.ZoneChanged -= OnZoneChanged;
                zoneService.ZoneChanged += OnZoneChanged;
            }
        }

        private void OnDisable()
        {
            Canvas.willRenderCanvases -= OnWillRenderCanvases;
            if (questLog != null)
            {
                questLog.Changed -= OnQuestLogChanged;
            }

            if (zoneService != null)
            {
                zoneService.ZoneChanged -= OnZoneChanged;
            }
        }

        private void OnDestroy()
        {
            ReleaseTextures();
        }

        private void Update()
        {
            ResolveReferences();
            BuildIfNeeded();
            BindExistingVisualsIfNeeded();
            EnsureMapCameras();
            HandleInput();
            RefreshMarkers(false);
            UpdateZoneLabels();
        }

        private void LateUpdate()
        {
            UpdateLiveMapState(true);
        }

        private void OnWillRenderCanvases()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            UpdateLiveMapState(false);
        }

        public void Configure(MMOZoneService newZoneService, Transform newPlayer, MMOQuestLog newQuestLog)
        {
            zoneService = newZoneService;
            player = newPlayer;
            questLog = newQuestLog;
            if (Application.isPlaying)
            {
                RebuildVisuals();
            }
            else
            {
                built = false;
            }
        }

        public void RebuildVisuals()
        {
            built = false;
            root = (RectTransform)transform;
            ClearGeneratedVisuals();
            BuildIfNeeded();
            EnsureMapCameras();
            RefreshMarkers(true);
            UpdateLiveMapState(true);
        }

        private void ClearGeneratedVisuals()
        {
            minimapRoot = null;
            minimapMask = null;
            minimapMarkerLayer = null;
            minimapPlayerArrow = null;
            minimapZoneText = null;
            minimapRawImage = null;
            largeMapRoot = null;
            largeMapImageRect = null;
            largeMapMarkerLayer = null;
            largeMapPlayerArrow = null;
            largeMapZoneText = null;
            largeMapRawImage = null;
            minimapMarkerViews.Clear();
            largeMapMarkerViews.Clear();

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = transform.GetChild(i).gameObject;
                child.SetActive(false);
                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }
        }

        private void UpdateLiveMapState(bool renderSurface)
        {
            if (updatingLiveMapState)
            {
                return;
            }

            updatingLiveMapState = true;
            try
            {
                ResolveReferences();
                BuildIfNeeded();
                BindExistingVisualsIfNeeded();
                EnsureMapCameras();

                if (renderSurface)
                {
                    UpdateMapSurface();
                }
                else
                {
                    UpdateZoneLabels();
                }

                UpdateMarkerPositions();
                UpdatePlayerIndicators();
            }
            finally
            {
                updatingLiveMapState = false;
            }
        }

        private void ResolveReferences()
        {
            if (player == null)
            {
                player = MMORuntimeSceneReferences.PlayerTransform;
            }

            if (questLog == null && player != null)
            {
                MMORuntimeSceneReferences.TryGetPlayerComponent(out questLog);
            }

            if (inputReader == null && player != null)
            {
                MMORuntimeSceneReferences.TryGetPlayerComponent(out inputReader);
            }

            Camera mainCamera = MMORuntimeSceneReferences.MainCamera;
            if (gameplayCameraController == null && mainCamera != null)
            {
                gameplayCameraController = mainCamera.GetComponent<MMOThirdPersonCamera>();
            }

            if (zoneService == null)
            {
                zoneService = FindAnyObjectByType<MMOZoneService>();
            }
        }

        private void BuildIfNeeded()
        {
            if (built)
            {
                return;
            }

            root = (RectTransform)transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            root.pivot = new Vector2(0.5f, 0.5f);
            ClearGeneratedVisuals();

            circleSprite ??= CreateCircleSprite(128, false);
            ringSprite ??= CreateCircleSprite(128, true);
            triangleSprite ??= CreateTriangleSprite(64);
            diamondSprite ??= CreateDiamondSprite(48);

            BuildMinimap();
            BuildLargeMap();
            BindExistingVisualsIfNeeded();
            built = true;
        }

        private void BindExistingVisualsIfNeeded()
        {
            if (minimapRawImage == null)
            {
                Transform minimapImage = transform.Find("Minimap/Minimap Mask/Minimap Image");
                minimapRawImage = minimapImage != null ? minimapImage.GetComponent<RawImage>() : null;
            }

            if (largeMapRawImage == null)
            {
                Transform largeMapImage = transform.Find("Large Zone Map/Map Image");
                largeMapRawImage = largeMapImage != null ? largeMapImage.GetComponent<RawImage>() : null;
            }

            if (minimapMarkerLayer == null)
            {
                minimapMarkerLayer = transform.Find("Minimap/Minimap Markers") as RectTransform;
            }

            if (minimapPlayerArrow == null)
            {
                minimapPlayerArrow = transform.Find("Minimap/Minimap Markers/Player Arrow") as RectTransform;
            }

            if (largeMapMarkerLayer == null)
            {
                largeMapMarkerLayer = transform.Find("Large Zone Map/Large Map Markers") as RectTransform;
            }

            if (largeMapPlayerArrow == null)
            {
                largeMapPlayerArrow = transform.Find("Large Zone Map/Large Map Markers/Player Arrow") as RectTransform;
            }

            if (largeMapRoot == null)
            {
                largeMapRoot = transform.Find("Large Zone Map") as RectTransform;
            }
        }

        private void BuildMinimap()
        {
            minimapRoot = MMOUiFactory.CreateRect("Minimap", transform);
            minimapRoot.anchorMin = new Vector2(1f, 1f);
            minimapRoot.anchorMax = new Vector2(1f, 1f);
            minimapRoot.pivot = new Vector2(1f, 1f);
            minimapRoot.anchoredPosition = new Vector2(-30f, -28f);
            minimapRoot.sizeDelta = new Vector2(238f, 238f);

            Image maskImage = MMOUiFactory.CreateImage("Minimap Mask", minimapRoot, Color.white, false);
            maskImage.sprite = circleSprite;
            minimapMask = maskImage.rectTransform;
            minimapMask.anchorMin = new Vector2(0.5f, 1f);
            minimapMask.anchorMax = new Vector2(0.5f, 1f);
            minimapMask.pivot = new Vector2(0.5f, 1f);
            minimapMask.anchoredPosition = new Vector2(-20f, 0f);
            minimapMask.sizeDelta = new Vector2(MinimapDiameter, MinimapDiameter);
            Mask mask = maskImage.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            RectTransform minimapImageRect = MMOUiFactory.CreateRect("Minimap Image", minimapMask);
            MMOUiFactory.Stretch(minimapImageRect);
            minimapRawImage = minimapImageRect.gameObject.AddComponent<RawImage>();
            minimapRawImage.color = Color.white;
            minimapRawImage.raycastTarget = false;
            CreateMinimapRenderFeed();

            Image border = MMOUiFactory.CreateImage("Minimap Ring", minimapRoot, new Color(0.77f, 0.65f, 0.38f, 1f), false);
            border.sprite = ringSprite;
            border.rectTransform.anchorMin = minimapMask.anchorMin;
            border.rectTransform.anchorMax = minimapMask.anchorMax;
            border.rectTransform.pivot = minimapMask.pivot;
            border.rectTransform.anchoredPosition = minimapMask.anchoredPosition;
            border.rectTransform.sizeDelta = new Vector2(MinimapDiameter + 10f, MinimapDiameter + 10f);

            minimapMarkerLayer = MMOUiFactory.CreateRect("Minimap Markers", minimapRoot);
            minimapMarkerLayer.anchorMin = minimapMask.anchorMin;
            minimapMarkerLayer.anchorMax = minimapMask.anchorMax;
            minimapMarkerLayer.pivot = minimapMask.pivot;
            minimapMarkerLayer.anchoredPosition = minimapMask.anchoredPosition;
            minimapMarkerLayer.sizeDelta = minimapMask.sizeDelta;

            minimapPlayerArrow = CreateIcon("Player Arrow", minimapMarkerLayer, triangleSprite, new Color(1f, 0.95f, 0.72f, 1f), new Vector2(18f, 22f));
            minimapPlayerArrow.anchoredPosition = Vector2.zero;
            ConfigurePlayerIndicator(minimapPlayerArrow, false);

            Button zoomIn = MMOUiFactory.CreateTextButton("Zoom In", minimapRoot, "+", new Vector2(25f, 25f), new Color(0.06f, 0.052f, 0.04f, 0.94f));
            zoomIn.onClick.AddListener(ZoomIn);
            SetButtonTransform(zoomIn, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-2f, -18f));

            Button zoomOut = MMOUiFactory.CreateTextButton("Zoom Out", minimapRoot, "-", new Vector2(25f, 25f), new Color(0.06f, 0.052f, 0.04f, 0.94f));
            zoomOut.onClick.AddListener(ZoomOut);
            SetButtonTransform(zoomOut, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-2f, -47f));

            Button mapButton = MMOUiFactory.CreateTextButton("Open Map", minimapRoot, "M", new Vector2(25f, 25f), new Color(0.06f, 0.052f, 0.04f, 0.94f));
            mapButton.onClick.AddListener(ToggleLargeMap);
            SetButtonTransform(mapButton, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-2f, -76f));

            minimapZoneText = MMOUiFactory.CreateText("Zone Name", minimapRoot, 12, FontStyle.Bold, TextAnchor.MiddleCenter);
            minimapZoneText.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            minimapZoneText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            minimapZoneText.rectTransform.pivot = new Vector2(0.5f, 1f);
            minimapZoneText.rectTransform.anchoredPosition = new Vector2(-20f, -MinimapDiameter - 10f);
            minimapZoneText.rectTransform.sizeDelta = new Vector2(190f, 22f);
            minimapZoneText.color = new Color(1f, 0.86f, 0.42f, 1f);
        }

        private void BuildLargeMap()
        {
            largeMapRoot = MMOUiFactory.CreateRect("Large Zone Map", transform);
            largeMapRoot.anchorMin = new Vector2(0.5f, 0.5f);
            largeMapRoot.anchorMax = new Vector2(0.5f, 0.5f);
            largeMapRoot.pivot = new Vector2(0.5f, 0.5f);
            largeMapRoot.anchoredPosition = Vector2.zero;
            largeMapRoot.sizeDelta = new Vector2(860f, 820f);
            largeMapRoot.gameObject.SetActive(false);

            Image dim = MMOUiFactory.CreateImage("Backdrop", largeMapRoot, new Color(0.01f, 0.01f, 0.012f, 0.72f), false);
            dim.rectTransform.sizeDelta = new Vector2(2200f, 1400f);
            dim.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            dim.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            dim.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            dim.rectTransform.SetAsFirstSibling();

            Image frame = MMOUiFactory.CreateImage("Map Frame", largeMapRoot, new Color(0.045f, 0.036f, 0.027f, 0.97f), false);
            MMOUiFactory.Stretch(frame.rectTransform);

            largeMapImageRect = MMOUiFactory.CreateRect("Map Image", largeMapRoot);
            largeMapImageRect.anchorMin = new Vector2(0.5f, 0.5f);
            largeMapImageRect.anchorMax = new Vector2(0.5f, 0.5f);
            largeMapImageRect.pivot = new Vector2(0.5f, 0.5f);
            largeMapImageRect.anchoredPosition = new Vector2(0f, -18f);
            largeMapImageRect.sizeDelta = new Vector2(LargeMapSize, LargeMapSize);
            largeMapRawImage = largeMapImageRect.gameObject.AddComponent<RawImage>();
            largeMapRawImage.color = Color.white;
            largeMapRawImage.raycastTarget = false;
            CreateLargeMapRenderFeed();

            Image mapBorder = MMOUiFactory.CreateImage("Map Border", largeMapRoot, new Color(0.75f, 0.62f, 0.34f, 1f), false);
            mapBorder.rectTransform.anchorMin = largeMapImageRect.anchorMin;
            mapBorder.rectTransform.anchorMax = largeMapImageRect.anchorMax;
            mapBorder.rectTransform.pivot = largeMapImageRect.pivot;
            mapBorder.rectTransform.anchoredPosition = largeMapImageRect.anchoredPosition;
            mapBorder.rectTransform.sizeDelta = new Vector2(LargeMapSize + 8f, LargeMapSize + 8f);
            mapBorder.transform.SetAsFirstSibling();

            largeMapMarkerLayer = MMOUiFactory.CreateRect("Large Map Markers", largeMapRoot);
            largeMapMarkerLayer.anchorMin = largeMapImageRect.anchorMin;
            largeMapMarkerLayer.anchorMax = largeMapImageRect.anchorMax;
            largeMapMarkerLayer.pivot = largeMapImageRect.pivot;
            largeMapMarkerLayer.anchoredPosition = largeMapImageRect.anchoredPosition;
            largeMapMarkerLayer.sizeDelta = largeMapImageRect.sizeDelta;

            largeMapPlayerArrow = CreateIcon("Player Arrow", largeMapMarkerLayer, triangleSprite, new Color(1f, 0.96f, 0.72f, 1f), new Vector2(22f, 28f));
            ConfigurePlayerIndicator(largeMapPlayerArrow, true);

            largeMapZoneText = MMOUiFactory.CreateText("Zone Name", largeMapRoot, 20, FontStyle.Bold, TextAnchor.MiddleCenter);
            largeMapZoneText.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            largeMapZoneText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            largeMapZoneText.rectTransform.pivot = new Vector2(0.5f, 1f);
            largeMapZoneText.rectTransform.anchoredPosition = new Vector2(0f, -20f);
            largeMapZoneText.rectTransform.sizeDelta = new Vector2(500f, 34f);
            largeMapZoneText.color = new Color(1f, 0.84f, 0.38f, 1f);

            Button close = MMOUiFactory.CreateTextButton("Close", largeMapRoot, "X", new Vector2(30f, 30f), new Color(0.08f, 0.055f, 0.04f, 0.96f));
            close.onClick.AddListener(CloseLargeMap);
            SetButtonTransform(close, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -16f));
            close.transform.SetAsLastSibling();
        }

        private static void SetButtonTransform(Button button, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition)
        {
            if (button == null)
            {
                return;
            }

            if (button.targetGraphic == null && button.TryGetComponent(out Graphic graphic))
            {
                button.targetGraphic = graphic;
            }

            if (button.transform is not RectTransform rectTransform)
            {
                return;
            }

            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = pivot;
            rectTransform.anchoredPosition = anchoredPosition;
        }

        private void EnsureMapCameras()
        {
            BindExistingVisualsIfNeeded();
            bool minimapNeedsTexture = minimapTexture == null || !minimapTexture.IsCreated();
            bool largeMapNeedsTexture = largeMapTexture == null || !largeMapTexture.IsCreated();
            bool largeMapVisible = largeMapRoot != null && largeMapRoot.gameObject.activeInHierarchy;
            if (minimapNeedsTexture)
            {
                CreateMinimapRenderFeed();
            }

            if (largeMapNeedsTexture && largeMapVisible)
            {
                CreateLargeMapRenderFeed();
            }

            if (minimapRawImage != null)
            {
                minimapRawImage.texture = minimapTexture;
                minimapRawImage.uvRect = new Rect(0f, 0f, 1f, 1f);
            }

            if (largeMapRawImage != null && largeMapTexture != null)
            {
                largeMapRawImage.texture = largeMapTexture;
                largeMapRawImage.uvRect = new Rect(0f, 0f, 1f, 1f);
            }

            RemoveMapCameraFollower(minimapCamera);
            RemoveMapCameraFollower(largeMapCamera);

            if (minimapRawImage != null && minimapRawImage.texture == null)
            {
                Debug.LogError("Map HUD could not assign the minimap render texture.");
            }

            if (largeMapVisible && largeMapRawImage != null && largeMapRawImage.texture == null)
            {
                Debug.LogError("Map HUD could not assign the large map render texture.");
            }
        }

        private void CreateMinimapRenderFeed()
        {
            ReleaseRenderTexture(ref minimapTexture);
            minimapTexture = CreateRenderTexture("RPGClone_Minimap", MinimapTextureSize);
            minimapCamera = EnsureCamera("RPG Clone Minimap Camera", minimapTexture);
            RemoveMapCameraFollower(minimapCamera);
            if (minimapRawImage != null)
            {
                minimapRawImage.texture = minimapTexture;
                minimapRawImage.uvRect = new Rect(0f, 0f, 1f, 1f);
            }

            PositionAndRenderMapCameras();
            lastMapRenderFrame = Time.frameCount;
        }

        private void CreateLargeMapRenderFeed()
        {
            ReleaseRenderTexture(ref largeMapTexture);
            largeMapTexture = CreateRenderTexture("RPGClone_LargeZoneMap", LargeMapTextureSize);
            largeMapCamera = EnsureCamera("RPG Clone Large Map Camera", largeMapTexture);
            RemoveMapCameraFollower(largeMapCamera);
            if (largeMapRawImage != null)
            {
                largeMapRawImage.texture = largeMapTexture;
                largeMapRawImage.uvRect = new Rect(0f, 0f, 1f, 1f);
            }

            PositionAndRenderMapCameras();
            lastMapRenderFrame = Time.frameCount;
        }

        private Camera EnsureCamera(string cameraName, RenderTexture targetTexture)
        {
            GameObject cameraObject = GameObject.Find(cameraName);
            if (cameraObject == null)
            {
                cameraObject = new GameObject(cameraName);
            }

            Camera camera = cameraObject.GetComponent<Camera>();
            if (camera == null)
            {
                camera = cameraObject.AddComponent<Camera>();
            }

            camera.enabled = false;
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.1f, 0.08f, 1f);
            camera.cullingMask = ~0;
            camera.depth = -100f;
            camera.targetTexture = targetTexture;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1600f;
            camera.useOcclusionCulling = false;
            camera.allowHDR = false;
            camera.allowMSAA = false;

            AudioListener listener = cameraObject.GetComponent<AudioListener>();
            if (listener != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(listener);
                }
                else
                {
                    DestroyImmediate(listener);
                }
            }

            return camera;
        }

        private static void RemoveMapCameraFollower(Camera targetCamera)
        {
            if (targetCamera == null)
            {
                return;
            }

            MMOMapCameraFollower follower = targetCamera.GetComponent<MMOMapCameraFollower>();
            if (follower == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(follower);
            }
            else
            {
                DestroyImmediate(follower);
            }
        }

        private static RenderTexture CreateRenderTexture(string textureName, int size)
        {
            RenderTexture texture = new(size, size, 24, RenderTextureFormat.ARGB32)
            {
                name = textureName,
                useMipMap = false,
                autoGenerateMips = false,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            texture.Create();
            return texture;
        }

        private void UpdateMapSurface()
        {
            if (lastMapRenderFrame != Time.frameCount)
            {
                PositionAndRenderMapCameras();
                lastMapRenderFrame = Time.frameCount;
            }

            UpdateZoneLabels();
        }

        private void UpdateZoneLabels()
        {
            MMOZoneDefinition zone = zoneService != null ? zoneService.CurrentZone : null;
            string zoneName = zone != null ? zone.DisplayName : "Current Zone";
            if (minimapZoneText != null)
            {
                minimapZoneText.text = zoneName;
            }

            if (largeMapZoneText != null)
            {
                largeMapZoneText.text = zoneName;
            }
        }

        private void PositionAndRenderMapCameras()
        {
            ResolveReferences();
            if (player == null)
            {
                return;
            }

            Bounds mapBounds = ResolveMapBounds();
            float cameraHeight = Mathf.Max(mapBounds.max.y + 260f, player.position.y + 260f);

            if (minimapCamera != null)
            {
                minimapCamera.transform.SetPositionAndRotation(
                    new Vector3(player.position.x, cameraHeight, player.position.z),
                    Quaternion.Euler(90f, 0f, 0f));
                minimapCamera.orthographicSize = Mathf.Clamp(minimapZoom, MinimapMinZoom, MinimapMaxZoom);
                minimapCamera.Render();
            }

            if (largeMapCamera != null)
            {
                largeMapCamera.transform.SetPositionAndRotation(
                    new Vector3(mapBounds.center.x, cameraHeight, mapBounds.center.z),
                    Quaternion.Euler(90f, 0f, 0f));
                largeMapCamera.orthographicSize = Mathf.Max(20f, Mathf.Max(mapBounds.size.x, mapBounds.size.z) * 0.5f);

                if (largeMapRoot == null || largeMapRoot.gameObject.activeInHierarchy)
                {
                    largeMapCamera.Render();
                }
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

        private void HandleInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            KeyControl key = keyboard[largeMapToggleKey];
            if (largeMapRoot != null && largeMapRoot.gameObject.activeSelf && keyboard.escapeKey.wasPressedThisFrame)
            {
                CloseLargeMap();
                return;
            }

            if ((key != null && key.wasPressedThisFrame) || keyboard.mKey.wasPressedThisFrame)
            {
                ToggleLargeMap();
            }
        }

        private void RefreshMarkers(bool force)
        {
            if (!force && Time.unscaledTime < nextMarkerRefreshTime)
            {
                return;
            }

            nextMarkerRefreshTime = Time.unscaledTime + MarkerRefreshSeconds;
            markers.Clear();
            markers.AddRange(MMOQuestMapMarkerProvider.BuildTrackedQuestMarkers(questLog));
            RebuildMarkerLayer(minimapMarkerLayer, true);
            RebuildMarkerLayer(largeMapMarkerLayer, false);
            UpdateLiveMapState(false);
        }

        private void RebuildMarkerLayer(RectTransform layer, bool minimap)
        {
            if (layer == null)
            {
                return;
            }

            if (minimap)
            {
                minimapMarkerViews.Clear();
            }
            else
            {
                largeMapMarkerViews.Clear();
            }

            RectTransform playerArrow = minimap ? minimapPlayerArrow : largeMapPlayerArrow;
            for (int i = layer.childCount - 1; i >= 0; i--)
            {
                Transform child = layer.GetChild(i);
                if (child != playerArrow)
                {
                    Destroy(child.gameObject);
                }
            }

            foreach (MMOMapMarkerData marker in markers)
            {
                if (minimap)
                {
                    AddMinimapMarker(marker);
                }
                else
                {
                    AddLargeMapMarker(marker);
                }
            }

            playerArrow?.SetAsLastSibling();
        }

        private void AddMinimapMarker(MMOMapMarkerData marker)
        {
            if (player == null || minimapMarkerLayer == null)
            {
                return;
            }

            float half = MinimapDiameter * 0.5f;
            Vector2 local = WorldToMinimapPosition(marker.WorldPosition, half, out bool clamped);
            RectTransform area = null;
            if (marker.IsArea && !clamped)
            {
                float radiusPixels = Mathf.Clamp(marker.Radius / minimapZoom * half, 8f, MinimapDiameter);
                area = CreateIcon($"Area {marker.MarkerId}", minimapMarkerLayer, circleSprite, WithAlpha(marker.Color, 0.2f), new Vector2(radiusPixels * 2f, radiusPixels * 2f));
                area.anchoredPosition = local;
            }

            RectTransform icon = CreateIcon($"Marker {marker.MarkerId}", minimapMarkerLayer, diamondSprite, clamped ? WithAlpha(marker.Color, 0.72f) : marker.Color, new Vector2(15f, 15f));
            icon.anchoredPosition = local;
            minimapMarkerViews.Add(new MapMarkerView(marker, icon, area, null));
        }

        private void AddLargeMapMarker(MMOMapMarkerData marker)
        {
            if (largeMapMarkerLayer == null)
            {
                return;
            }

            Bounds mapBounds = ResolveMapBounds();
            Vector2 local = WorldToLargeMapPosition(mapBounds, marker.WorldPosition);
            RectTransform area = null;
            if (marker.IsArea)
            {
                float zoneScale = LargeMapSize / Mathf.Max(mapBounds.size.x, mapBounds.size.z, 1f);
                float radiusPixels = Mathf.Clamp(marker.Radius * zoneScale, 14f, LargeMapSize);
                area = CreateIcon($"Area {marker.MarkerId}", largeMapMarkerLayer, circleSprite, WithAlpha(marker.Color, 0.18f), new Vector2(radiusPixels * 2f, radiusPixels * 2f));
                area.anchoredPosition = local;
            }

            RectTransform icon = CreateIcon($"Marker {marker.MarkerId}", largeMapMarkerLayer, diamondSprite, marker.Color, new Vector2(18f, 18f));
            icon.anchoredPosition = local;

            Text label = MMOUiFactory.CreateText($"Label {marker.MarkerId}", largeMapMarkerLayer, 11, FontStyle.Bold, TextAnchor.MiddleLeft);
            label.text = marker.Label;
            label.color = new Color(1f, 0.94f, 0.76f, 1f);
            label.rectTransform.pivot = new Vector2(0f, 0.5f);
            label.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            label.rectTransform.anchoredPosition = local + new Vector2(13f, 0f);
            label.rectTransform.sizeDelta = new Vector2(220f, 20f);
            largeMapMarkerViews.Add(new MapMarkerView(marker, icon, area, label.rectTransform));
        }

        private Vector2 WorldToMinimapPosition(Vector3 worldPosition, float halfSize, out bool clamped)
        {
            Vector2 delta = new(worldPosition.x - player.position.x, worldPosition.z - player.position.z);
            Vector2 local = delta / Mathf.Max(1f, minimapZoom) * halfSize;
            float max = halfSize - 10f;
            clamped = local.magnitude > max;
            return clamped ? local.normalized * max : local;
        }

        private static Vector2 WorldToLargeMapPosition(MMOZoneDefinition zone, Vector3 worldPosition)
        {
            Vector2 normalized = zone.WorldToNormalized(worldPosition);
            return new Vector2((normalized.x - 0.5f) * LargeMapSize, (normalized.y - 0.5f) * LargeMapSize);
        }

        private static Vector2 WorldToLargeMapPosition(Bounds bounds, Vector3 worldPosition)
        {
            Vector3 min = bounds.min;
            Vector3 size = bounds.size;
            float x = size.x > Mathf.Epsilon ? Mathf.Clamp01((worldPosition.x - min.x) / size.x) : 0.5f;
            float y = size.z > Mathf.Epsilon ? Mathf.Clamp01((worldPosition.z - min.z) / size.z) : 0.5f;
            return new Vector2((x - 0.5f) * LargeMapSize, (y - 0.5f) * LargeMapSize);
        }

        private void UpdateMarkerPositions()
        {
            if (player == null)
            {
                return;
            }

            float half = MinimapDiameter * 0.5f;
            for (int i = 0; i < minimapMarkerViews.Count; i++)
            {
                UpdateMinimapMarkerPosition(minimapMarkerViews[i], half);
            }

            Bounds mapBounds = ResolveMapBounds();
            for (int i = 0; i < largeMapMarkerViews.Count; i++)
            {
                UpdateLargeMapMarkerPosition(largeMapMarkerViews[i], mapBounds);
            }
        }

        private void UpdateMinimapMarkerPosition(MapMarkerView markerView, float halfSize)
        {
            if (markerView.Icon == null)
            {
                return;
            }

            Vector2 local = WorldToMinimapPosition(markerView.Marker.WorldPosition, halfSize, out bool clamped);
            markerView.Icon.anchoredPosition = local;
            Image iconImage = markerView.Icon.GetComponent<Image>();
            if (iconImage != null)
            {
                iconImage.color = clamped ? WithAlpha(markerView.Marker.Color, 0.72f) : markerView.Marker.Color;
            }

            if (markerView.Area == null)
            {
                return;
            }

            bool showArea = markerView.Marker.IsArea && !clamped;
            markerView.Area.gameObject.SetActive(showArea);
            if (!showArea)
            {
                return;
            }

            float radiusPixels = Mathf.Clamp(markerView.Marker.Radius / Mathf.Max(1f, minimapZoom) * halfSize, 8f, MinimapDiameter);
            markerView.Area.anchoredPosition = local;
            markerView.Area.sizeDelta = new Vector2(radiusPixels * 2f, radiusPixels * 2f);
        }

        private static void UpdateLargeMapMarkerPosition(MapMarkerView markerView, Bounds mapBounds)
        {
            if (markerView.Icon == null)
            {
                return;
            }

            Vector2 local = WorldToLargeMapPosition(mapBounds, markerView.Marker.WorldPosition);
            markerView.Icon.anchoredPosition = local;

            if (markerView.Area != null)
            {
                float zoneScale = LargeMapSize / Mathf.Max(mapBounds.size.x, mapBounds.size.z, 1f);
                float radiusPixels = Mathf.Clamp(markerView.Marker.Radius * zoneScale, 14f, LargeMapSize);
                markerView.Area.anchoredPosition = local;
                markerView.Area.sizeDelta = new Vector2(radiusPixels * 2f, radiusPixels * 2f);
            }

            if (markerView.Label != null)
            {
                markerView.Label.anchoredPosition = local + new Vector2(13f, 0f);
            }
        }

        private void UpdatePlayerIndicators()
        {
            if (player == null)
            {
                return;
            }

            Quaternion arrowRotation = Quaternion.Euler(0f, 0f, -ResolvePlayerHeadingDegrees());
            if (minimapPlayerArrow != null)
            {
                minimapPlayerArrow.anchoredPosition = Vector2.zero;
                minimapPlayerArrow.localRotation = arrowRotation;
            }

            if (largeMapPlayerArrow != null)
            {
                Vector2 largeMapPosition = WorldToLargeMapPosition(ResolveMapBounds(), player.position);
                largeMapPlayerArrow.anchoredPosition = largeMapPosition;
                largeMapPlayerArrow.localRotation = arrowRotation;
            }
        }

        private float ResolvePlayerHeadingDegrees()
        {
            float heading = player.eulerAngles.y;
            if (gameplayCameraController != null && inputReader != null && inputReader.Current.IsMouseLooking)
            {
                heading = gameplayCameraController.transform.eulerAngles.y;
            }

            return heading + playerArrowHeadingOffsetDegrees;
        }

        private RectTransform CreateIcon(string objectName, Transform parent, Sprite sprite, Color color, Vector2 size)
        {
            Image image = MMOUiFactory.CreateImage(objectName, parent, color, false);
            image.sprite = sprite;
            image.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            image.rectTransform.sizeDelta = size;
            return image.rectTransform;
        }

        private void ConfigurePlayerIndicator(RectTransform indicator, bool useLargeMapPosition)
        {
            if (indicator == null)
            {
                return;
            }

            MMOMapPlayerIndicator playerIndicator = indicator.GetComponent<MMOMapPlayerIndicator>();
            if (playerIndicator == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(playerIndicator);
            }
            else
            {
                DestroyImmediate(playerIndicator);
            }
        }

        private void ToggleLargeMap()
        {
            if (largeMapRoot != null)
            {
                if (largeMapRoot.gameObject.activeSelf)
                {
                    CloseLargeMap();
                }
                else
                {
                    OpenLargeMap();
                }
            }
        }

        private void OpenLargeMap()
        {
            if (largeMapRoot == null)
            {
                return;
            }

            largeMapRoot.gameObject.SetActive(true);
            EnsureMapCameras();
            RefreshMarkers(true);
            UpdateLiveMapState(true);
        }

        private void CloseLargeMap()
        {
            if (largeMapRoot == null)
            {
                return;
            }

            largeMapRoot.gameObject.SetActive(false);
        }

        private void ZoomIn()
        {
            minimapZoom = Mathf.Clamp(minimapZoom - MinimapZoomStep, MinimapMinZoom, MinimapMaxZoom);
            RefreshMarkers(true);
        }

        private void ZoomOut()
        {
            minimapZoom = Mathf.Clamp(minimapZoom + MinimapZoomStep, MinimapMinZoom, MinimapMaxZoom);
            RefreshMarkers(true);
        }

        private void OnQuestLogChanged(MMOQuestLog changedQuestLog)
        {
            RefreshMarkers(true);
        }

        private void OnZoneChanged(MMOZoneDefinition zone)
        {
            RefreshMarkers(true);
        }

        private void ReleaseTextures()
        {
            ReleaseRenderTexture(ref minimapTexture);
            ReleaseRenderTexture(ref largeMapTexture);
        }

        private static void ReleaseRenderTexture(ref RenderTexture texture)
        {
            if (texture == null)
            {
                return;
            }

            texture.Release();
            if (Application.isPlaying)
            {
                Destroy(texture);
            }
            else
            {
                DestroyImmediate(texture);
            }

            texture = null;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }

        private readonly struct MapMarkerView
        {
            public MapMarkerView(MMOMapMarkerData marker, RectTransform icon, RectTransform area, RectTransform label)
            {
                Marker = marker;
                Icon = icon;
                Area = area;
                Label = label;
            }

            public MMOMapMarkerData Marker { get; }
            public RectTransform Icon { get; }
            public RectTransform Area { get; }
            public RectTransform Label { get; }
        }

        private static Sprite CreateCircleSprite(int size, bool ring)
        {
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
            {
                name = ring ? "Generated UI Ring" : "Generated UI Circle",
                hideFlags = HideFlags.HideAndDontSave
            };

            float center = (size - 1) * 0.5f;
            float outer = center;
            float inner = ring ? center - size * 0.075f : 0f;
            Color clear = new(1f, 1f, 1f, 0f);
            Color solid = Color.white;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    bool visible = distance <= outer && distance >= inner;
                    texture.SetPixel(x, y, visible ? solid : clear);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite CreateTriangleSprite(int size)
        {
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
            {
                name = "Generated UI Triangle",
                hideFlags = HideFlags.HideAndDontSave
            };

            Color clear = new(1f, 1f, 1f, 0f);
            for (int y = 0; y < size; y++)
            {
                float normalizedY = y / (float)(size - 1);
                float halfWidth = Mathf.Lerp(size * 0.08f, size * 0.42f, 1f - normalizedY);
                float center = (size - 1) * 0.5f;
                for (int x = 0; x < size; x++)
                {
                    bool visible = Mathf.Abs(x - center) <= halfWidth && normalizedY > 0.08f;
                    texture.SetPixel(x, y, visible ? Color.white : clear);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite CreateDiamondSprite(int size)
        {
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
            {
                name = "Generated UI Diamond",
                hideFlags = HideFlags.HideAndDontSave
            };

            float center = (size - 1) * 0.5f;
            float radius = size * 0.42f;
            Color clear = new(1f, 1f, 1f, 0f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Mathf.Abs(x - center) + Mathf.Abs(y - center);
                    texture.SetPixel(x, y, distance <= radius ? Color.white : clear);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
