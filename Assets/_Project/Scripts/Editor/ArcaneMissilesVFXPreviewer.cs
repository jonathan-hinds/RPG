using RPGClone.Abilities;
using RPGClone.Combat;
using RPGClone.Vfx;
using RPGClone.Vfx.ArcaneMissiles;
using UnityEditor;
using UnityEngine;

namespace RPGClone.EditorTools
{
    [InitializeOnLoad]
    internal static class ArcaneMissilesVFXPreviewer
    {
        private const string PrefabPath = "Assets/_Project/VFX/ArcaneMissiles/Prefabs/ArcaneMissilesVFX.prefab";
        private const string AbilityPath = "Assets/_Project/Configs/Abilities/Mage_Arcane_Missile.asset";
        private const string PreviewName = "__ArcaneMissilesVFX_Preview";
        private const string PendingKey = "RPGClone.ArcaneMissilesVFXPreview.Pending";

        private static ArcaneMissilesVFX controller;
        private static MMOCombatant sourceCombatant;
        private static MMOCombatant targetCombatant;
        private static MMOAbilityDefinition ability;
        private static double nextTickAt;
        private static int tickIndex;

        static ArcaneMissilesVFXPreviewer()
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        [MenuItem("Tools/RPG Clone/VFX/Preview Arcane Missiles VFX", priority = 324)]
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

        [MenuItem("Tools/RPG Clone/VFX/Interrupt Arcane Missiles Preview", priority = 325)]
        private static void Interrupt()
        {
            ArcaneMissilesVFX active = FindPreviewController();
            if (active == null)
            {
                Debug.LogWarning("Start the Arcane Missiles VFX preview before triggering interruption.");
                return;
            }

            active.Release(true);
            EditorApplication.update -= TickPreview;
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PendingKey, false))
            {
                SessionState.EraseBool(PendingKey);
                EditorApplication.delayCall += SpawnPreview;
            }

            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.update -= TickPreview;
                ClearReferences();
            }
        }

        private static void SpawnPreview()
        {
            if (!EditorApplication.isPlaying) return;
            GameObject existing = GameObject.Find(PreviewName);
            if (existing != null) Object.Destroy(existing);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AbilityPath);
            if (prefab == null || ability == null)
            {
                Debug.LogError("Install Arcane Missiles VFX before starting its preview.");
                return;
            }

            SceneView sceneView = SceneView.lastActiveSceneView;
            Vector3 origin = sceneView != null ? sceneView.pivot : Vector3.zero;
            GameObject preview = new(PreviewName);
            preview.transform.position = origin;
            Object.DontDestroyOnLoad(preview);

            GameObject caster = new("Arcane Missiles Preview Caster");
            caster.transform.SetParent(preview.transform, false);
            sourceCombatant = caster.AddComponent<MMOCombatant>();
            CreateSilhouette(caster.transform, new Color(0.055f, 0.07f, 0.13f, 1f));

            GameObject target = new("Arcane Missiles Preview Target");
            target.transform.SetParent(preview.transform, false);
            target.transform.localPosition = new Vector3(0f, 0f, 6f);
            targetCombatant = target.AddComponent<MMOCombatant>();
            CreateSilhouette(target.transform, new Color(0.11f, 0.035f, 0.14f, 1f));

            GameObject vfxObject = Object.Instantiate(prefab, caster.transform);
            vfxObject.name = "ArcaneMissilesVFX Runtime Preview";
            controller = vfxObject.GetComponent<ArcaneMissilesVFX>();
            Vector3 targetPosition = target.transform.TransformPoint(new Vector3(0f, 1.05f, 0f));
            controller.Initialize(new MMOAbilityVfxContext(
                null,
                ability,
                ability.VisualEffects,
                caster.transform,
                target.transform,
                caster.transform.position,
                targetPosition,
                false,
                null));

            tickIndex = 0;
            nextTickAt = EditorApplication.timeSinceStartup + 1.0;
            EditorApplication.update -= TickPreview;
            EditorApplication.update += TickPreview;
            if (sceneView != null)
            {
                sceneView.LookAt(origin + new Vector3(0f, 1.25f, 3f), Quaternion.Euler(15f, 180f, 0f), 4.2f);
                sceneView.Repaint();
            }

            Selection.activeGameObject = null;

            Debug.Log("Arcane Missiles runtime preview started. Five real presentation damage events will fire at one-second intervals; use the adjacent menu command to preview movement interruption.", controller);
        }

        private static void TickPreview()
        {
            if (!EditorApplication.isPlaying || controller == null || targetCombatant == null || sourceCombatant == null || ability == null)
            {
                EditorApplication.update -= TickPreview;
                return;
            }

            if (EditorApplication.timeSinceStartup < nextTickAt) return;
            targetCombatant.ApplyResolvedDamage(sourceCombatant, ability, 1, false, false);
            tickIndex++;
            nextTickAt += 1.0;
            if (tickIndex < 5) return;
            controller.Release(false);
            EditorApplication.update -= TickPreview;
        }

        private static void CreateSilhouette(Transform parent, Color color)
        {
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Preview Silhouette";
            body.transform.SetParent(parent, false);
            body.transform.localPosition = Vector3.up;
            body.transform.localScale = new Vector3(0.72f, 1f, 0.72f);
            Object.Destroy(body.GetComponent<Collider>());
            Renderer renderer = body.GetComponent<Renderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (renderer == null || shader == null) return;
            renderer.sharedMaterial = new Material(shader)
            {
                name = "Arcane Missiles Preview Silhouette",
                color = color,
                hideFlags = HideFlags.DontSave
            };
        }

        private static ArcaneMissilesVFX FindPreviewController()
        {
            GameObject preview = GameObject.Find(PreviewName);
            return preview != null ? preview.GetComponentInChildren<ArcaneMissilesVFX>(true) : null;
        }

        private static void ClearReferences()
        {
            controller = null;
            sourceCombatant = null;
            targetCombatant = null;
            ability = null;
            tickIndex = 0;
        }
    }
}
