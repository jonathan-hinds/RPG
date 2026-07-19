using RPGClone.Vfx;
using RPGClone.Vfx.Water;
using UnityEditor;
using UnityEngine;

namespace RPGClone.EditorTools
{
    [InitializeOnLoad]
    internal static class WaterShieldVFXPreviewer
    {
        private const string PrefabPath = "Assets/_Project/VFX/WaterShield/Prefabs/WaterShieldVFX.prefab";
        private const string PreviewName = "__WaterShieldVFX_Preview";
        private const string PendingKey = "RPGClone.WaterShieldVFXPreview.Pending";

        static WaterShieldVFXPreviewer()
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        [MenuItem("Tools/RPG Clone/VFX/Preview Water Shield VFX", priority = 321)]
        private static void Preview()
        {
            if (!EditorApplication.isPlaying)
            {
                SessionState.SetBool(PendingKey, true);
                EditorApplication.EnterPlaymode();
                return;
            }

            SpawnPreview();
        }

        [MenuItem("Tools/RPG Clone/VFX/Trigger Water Shield Absorb", priority = 322)]
        private static void TriggerAbsorb()
        {
            WaterShieldVFX controller = FindPreviewController();
            if (controller == null)
            {
                Debug.LogWarning("Start the Water Shield VFX preview before triggering its absorb reaction.");
                return;
            }

            Vector3 incoming = SceneView.lastActiveSceneView != null
                ? SceneView.lastActiveSceneView.camera.transform.forward
                : Vector3.forward;
            controller.ReactToAbsorb(incoming, 12);
        }

        [MenuItem("Tools/RPG Clone/VFX/Expire Water Shield Preview", priority = 323)]
        private static void Expire()
        {
            WaterShieldVFX controller = FindPreviewController();
            if (controller == null)
            {
                Debug.LogWarning("Start the Water Shield VFX preview before previewing expiration.");
                return;
            }

            controller.Expire();
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingKey, false))
            {
                return;
            }

            SessionState.EraseBool(PendingKey);
            EditorApplication.delayCall += SpawnPreview;
        }

        private static void SpawnPreview()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            GameObject existing = GameObject.Find(PreviewName);
            if (existing != null)
            {
                Object.Destroy(existing);
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"Water Shield preview prefab is missing at {PrefabPath}.");
                return;
            }

            SceneView sceneView = SceneView.lastActiveSceneView;
            Vector3 previewPosition = sceneView != null ? sceneView.pivot + Vector3.up * 1.15f : new Vector3(0f, 2f, 0f);
            GameObject root = new GameObject(PreviewName);
            root.transform.position = previewPosition;

            CreateCasterSilhouette(root.transform);

            GameObject vfxObject = Object.Instantiate(prefab, root.transform);
            vfxObject.name = "WaterShieldVFX Runtime Preview";
            WaterShieldVFX controller = vfxObject.GetComponent<WaterShieldVFX>();
            controller.Initialize(new MMOAbilityVfxContext(
                null,
                null,
                null,
                root.transform,
                root.transform,
                previewPosition,
                previewPosition,
                false,
                null));

            Selection.activeGameObject = root;
            if (sceneView != null)
            {
                sceneView.LookAt(previewPosition + Vector3.up * 0.2f, Quaternion.Euler(12f, 205f, 0f), 2.35f);
                sceneView.Repaint();
            }

            Debug.Log("Water Shield VFX runtime preview started. Use the adjacent VFX menu commands to trigger absorb and expiration reactions.", controller);
        }

        private static void CreateCasterSilhouette(Transform parent)
        {
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Preview Caster";
            body.transform.SetParent(parent, false);
            body.transform.localPosition = Vector3.up;
            body.transform.localScale = new Vector3(0.72f, 1f, 0.72f);

            Collider collider = body.GetComponent<Collider>();
            if (collider != null)
            {
                Object.Destroy(collider);
            }

            Renderer renderer = body.GetComponent<Renderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (renderer != null && shader != null)
            {
                Material material = new Material(shader)
                {
                    name = "Water Shield Preview Silhouette",
                    color = new Color(0.075f, 0.105f, 0.14f, 1f),
                    hideFlags = HideFlags.DontSave
                };
                renderer.sharedMaterial = material;
            }
        }

        private static WaterShieldVFX FindPreviewController()
        {
            GameObject preview = GameObject.Find(PreviewName);
            return preview != null ? preview.GetComponentInChildren<WaterShieldVFX>(true) : null;
        }
    }
}
