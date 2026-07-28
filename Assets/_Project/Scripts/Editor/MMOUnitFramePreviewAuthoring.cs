using System.Collections.Generic;
using System.IO;
using RPGClone.Characters;
using RPGClone.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RPGClone.EditorTools
{
    public static class MMOUnitFramePreviewAuthoring
    {
        private const int PreviewWidth = 1600;
        private const int PreviewHeight = 640;
        private const int Supersample = 4;
        private const int PortraitSize = 256;

        [MenuItem("Tools/RPG Clone/UI/Render Classic Unit Frame Preview")]
        public static void RenderPreview()
        {
            Scene previewScene = EditorSceneManager.NewPreviewScene();
            RenderTexture renderTexture = null;
            RenderTexture downsampledTexture = null;
            Texture2D capture = null;
            Camera renderCamera = null;
            List<Object> transientPortraitAssets = new();

            try
            {
                renderCamera = CreateCamera(previewScene);
                CreateMainLight(previewScene);
                renderTexture = new RenderTexture(
                    PreviewWidth * Supersample,
                    PreviewHeight * Supersample,
                    24,
                    RenderTextureFormat.ARGB32)
                {
                    name = "Unit Frame Preview Supersampled",
                    filterMode = FilterMode.Bilinear
                };
                renderTexture.Create();
                renderCamera.targetTexture = renderTexture;

                Canvas canvas = CreateCanvas(previewScene, renderCamera);
                MMOUnitFrameTheme theme = Resources.Load<MMOUnitFrameTheme>(
                    "RPGClone/UI/UnitFrames/ClassicUnitFrameTheme");
                if (theme == null)
                {
                    throw new UnityException("ClassicUnitFrameTheme is missing. Build the theme first.");
                }

                Sprite playerPortrait = CapturePrefabPortrait(
                    "Assets/_Project/Prefabs/Player/PlayerCapsule.prefab",
                    new Color(0.12f, 0.16f, 0.18f, 1f),
                    transientPortraitAssets);
                Sprite targetPortrait = CapturePrefabPortrait(
                    "Assets/Characters/AshLeader/Prefabs/AshGeneralEnemy.prefab",
                    new Color(0.19f, 0.07f, 0.045f, 1f),
                    transientPortraitAssets);
                Sprite partyPortraitOne = CapturePrefabPortrait(
                    "Assets/Characters/Trog/Prefabs/TrogEnemy.prefab",
                    new Color(0.08f, 0.14f, 0.18f, 1f),
                    transientPortraitAssets);
                Sprite partyPortraitTwo = CapturePrefabPortrait(
                    "Assets/Characters/Wolf/Prefabs/WolfEnemy.prefab",
                    new Color(0.11f, 0.16f, 0.10f, 1f),
                    transientPortraitAssets);

                CreatePreviewFrame(
                    canvas.transform,
                    previewScene,
                    theme,
                    MMOUnitFrameStyle.Player,
                    new Vector2(42f, -48f),
                    "Kargath Ironbound",
                    24,
                    MMOEntityFaction.Friendly,
                    playerPortrait,
                    new Color(0.34f, 0.18f, 0.10f, 1f),
                    742,
                    853,
                    498,
                    610);
                CreatePreviewFrame(
                    canvas.transform,
                    previewScene,
                    theme,
                    MMOUnitFrameStyle.Target,
                    new Vector2(-42f, -48f),
                    "Razormane Warlord",
                    23,
                    MMOEntityFaction.Hostile,
                    targetPortrait,
                    new Color(0.45f, 0.12f, 0.08f, 1f),
                    830,
                    1250,
                    0,
                    0);
                CreatePreviewFrame(
                    canvas.transform,
                    previewScene,
                    theme,
                    MMOUnitFrameStyle.Party,
                    new Vector2(42f, -184f),
                    "Mira Stoneweaver",
                    24,
                    MMOEntityFaction.Friendly,
                    partyPortraitOne,
                    new Color(0.23f, 0.35f, 0.46f, 1f),
                    837,
                    900,
                    832,
                    860);
                CreatePreviewFrame(
                    canvas.transform,
                    previewScene,
                    theme,
                    MMOUnitFrameStyle.Party,
                    new Vector2(42f, -266f),
                    "Thorn",
                    22,
                    MMOEntityFaction.Friendly,
                    partyPortraitTwo,
                    new Color(0.31f, 0.43f, 0.19f, 1f),
                    615,
                    720,
                    92,
                    100);

                Canvas.ForceUpdateCanvases();
                renderCamera.Render();

                downsampledTexture = new RenderTexture(
                    PreviewWidth,
                    PreviewHeight,
                    0,
                    RenderTextureFormat.ARGB32)
                {
                    name = "Unit Frame Preview True Resolution",
                    filterMode = FilterMode.Bilinear
                };
                downsampledTexture.Create();
                Graphics.Blit(renderTexture, downsampledTexture);
                capture = ReadTexture(downsampledTexture, PreviewWidth, PreviewHeight);

                string outputPath = Path.GetFullPath(
                    Path.Combine(Application.dataPath, "../ArtSource/UI/UnitFrames/ClassicUnitFramePreview.png"));
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                File.WriteAllBytes(outputPath, capture.EncodeToPNG());
                Debug.Log(
                    $"Rendered non-pixelated layered unit-frame preview to {outputPath} "
                    + $"({Supersample}x supersampling counters the gameplay pixelation renderer).");
            }
            finally
            {
                foreach (Object asset in transientPortraitAssets)
                {
                    if (asset != null)
                    {
                        Object.DestroyImmediate(asset);
                    }
                }

                if (capture != null)
                {
                    Object.DestroyImmediate(capture);
                }

                ReleaseRenderTexture(renderCamera, ref downsampledTexture);
                ReleaseRenderTexture(renderCamera, ref renderTexture);
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static Camera CreateCamera(Scene scene)
        {
            GameObject cameraObject = new("Unit Frame Preview Camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.066f, 0.071f, 1f);
            camera.orthographic = true;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
            return camera;
        }

        private static void CreateMainLight(Scene scene)
        {
            GameObject lightObject = new("Directional Light", typeof(Light));
            SceneManager.MoveGameObjectToScene(lightObject, scene);
            Light light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
        }

        private static Canvas CreateCanvas(Scene scene, Camera camera)
        {
            GameObject canvasObject = new(
                "Preview Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasObject, scene);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(PreviewWidth, PreviewHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static void CreatePreviewFrame(
            Transform canvas,
            Scene scene,
            MMOUnitFrameTheme theme,
            MMOUnitFrameStyle style,
            Vector2 anchoredPosition,
            string displayName,
            int level,
            MMOEntityFaction faction,
            Sprite portrait,
            Color portraitTint,
            int currentHealth,
            int maxHealth,
            int currentMana,
            int maxMana)
        {
            GameObject identityObject = new(displayName, typeof(MMOCharacterIdentity));
            SceneManager.MoveGameObjectToScene(identityObject, scene);
            MMOCharacterIdentity identity = identityObject.GetComponent<MMOCharacterIdentity>();
            identity.Configure(
                displayName,
                level,
                portrait,
                portraitTint,
                faction,
                true,
                new MMOCharacterStats(),
                maxHealth,
                maxMana);
            identity.Health.SetCurrent(currentHealth);
            identity.Mana.SetCurrent(currentMana);

            string prefabPath = style switch
            {
                MMOUnitFrameStyle.Player => MMOUnitFramePrefabAuthoring.PlayerPrefabPath,
                MMOUnitFrameStyle.Target => MMOUnitFramePrefabAuthoring.TargetPrefabPath,
                _ => MMOUnitFramePrefabAuthoring.PartyPrefabPath
            };
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                throw new UnityException($"Unit-frame prefab is missing at '{prefabPath}'.");
            }

            GameObject frameObject = Object.Instantiate(prefab, canvas, false);
            frameObject.name = $"{style} Frame";
            RectTransform rect = (RectTransform)frameObject.transform;
            rect.anchorMin = style == MMOUnitFrameStyle.Target ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = new Vector2(style == MMOUnitFrameStyle.Target ? 1f : 0f, 1f);
            rect.anchoredPosition = anchoredPosition;

            MMOUnitFrameView view = frameObject.GetComponent<MMOUnitFrameView>();
            if (view == null)
            {
                throw new UnityException($"Unit-frame prefab '{prefabPath}' has no MMOUnitFrameView.");
            }

            view.ConfigureStyle(style, theme);
            view.Bind(identity);
        }

        private static Sprite CapturePrefabPortrait(
            string prefabPath,
            Color backgroundColor,
            ICollection<Object> transientAssets)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                return null;
            }

            Scene portraitScene = EditorSceneManager.NewPreviewScene();
            RenderTexture supersampled = null;
            RenderTexture downsampled = null;
            Camera camera = null;

            try
            {
                camera = CreatePortraitCamera(portraitScene, backgroundColor);
                CreateMainLight(portraitScene);

                GameObject instance = Object.Instantiate(prefab);
                instance.name = "Portrait Subject";
                SceneManager.MoveGameObjectToScene(instance, portraitScene);
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

                foreach (MonoBehaviour behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    behaviour.enabled = false;
                }

                Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
                if (!TryGetRendererBounds(renderers, out Bounds bounds))
                {
                    return null;
                }

                Vector3 focus = bounds.center + Vector3.up * bounds.extents.y * 0.52f;
                camera.orthographicSize = Mathf.Max(
                    bounds.extents.y * 0.40f,
                    Mathf.Min(bounds.extents.x * 0.72f, bounds.extents.y * 0.48f));
                camera.transform.position = focus + Vector3.forward * (bounds.extents.magnitude * 4f + 2f);
                camera.transform.LookAt(focus);

                int sourceSize = PortraitSize * Supersample;
                supersampled = new RenderTexture(sourceSize, sourceSize, 24, RenderTextureFormat.ARGB32)
                {
                    filterMode = FilterMode.Bilinear
                };
                supersampled.Create();
                camera.targetTexture = supersampled;
                camera.Render();

                downsampled = new RenderTexture(PortraitSize, PortraitSize, 0, RenderTextureFormat.ARGB32)
                {
                    filterMode = FilterMode.Bilinear
                };
                downsampled.Create();
                Graphics.Blit(supersampled, downsampled);
                Texture2D texture = ReadTexture(downsampled, PortraitSize, PortraitSize);
                texture.name = Path.GetFileNameWithoutExtension(prefabPath) + " Preview Portrait";
                Sprite sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, PortraitSize, PortraitSize),
                    new Vector2(0.5f, 0.5f),
                    100f);
                sprite.name = texture.name;
                transientAssets.Add(sprite);
                transientAssets.Add(texture);
                return sprite;
            }
            finally
            {
                ReleaseRenderTexture(camera, ref downsampled);
                ReleaseRenderTexture(camera, ref supersampled);
                EditorSceneManager.ClosePreviewScene(portraitScene);
            }
        }

        private static Camera CreatePortraitCamera(Scene scene, Color backgroundColor)
        {
            GameObject cameraObject = new("Portrait Camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = backgroundColor;
            camera.orthographic = true;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
            return camera;
        }

        private static bool TryGetRendererBounds(Renderer[] renderers, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return found;
        }

        private static Texture2D ReadTexture(RenderTexture source, int width, int height)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = source;
            Texture2D texture = new(width, height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
            texture.Apply(false, false);
            RenderTexture.active = previous;
            return texture;
        }

        private static void ReleaseRenderTexture(Camera camera, ref RenderTexture texture)
        {
            if (texture == null)
            {
                return;
            }

            if (camera != null && camera.targetTexture == texture)
            {
                camera.targetTexture = null;
            }

            texture.Release();
            Object.DestroyImmediate(texture);
            texture = null;
        }
    }
}
