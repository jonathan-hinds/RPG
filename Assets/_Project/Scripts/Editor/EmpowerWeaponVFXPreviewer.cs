#if UNITY_EDITOR
using System.Reflection;
using RPGClone.Characters;
using RPGClone.Inventory;
using RPGClone.Player;
using RPGClone.Vfx.Shaman;
using UnityEditor;
using UnityEngine;

namespace RPGClone.EditorTools
{
    public static class EmpowerWeaponVFXPreviewer
    {
        private const string PreviewName = "__EmpowerWeaponVFXPreview";
        private const string PersistentPath = "Assets/_Project/VFX/EmpowerWeapon/Prefabs/EmpowerWeaponPersistentVFX.prefab";
        private const string PreviewWeaponPath = "Assets/_Project/Equipment/Weapons/One-Handed Swords/Millguard Saber/PF_Millguard_Saber_Attachment.prefab";
        private const string PreviewWeaponDefinitionPath = "Assets/_Project/Equipment/Weapons/One-Handed Swords/Millguard Saber/EV_Millguard_Saber.asset";

        [MenuItem("Tools/RPG Clone/VFX/Preview Empower Weapon VFX")]
        public static void Preview()
        {
            Clear();
            GameObject root = new(PreviewName) { hideFlags = HideFlags.DontSaveInEditor };
            root.transform.position = new Vector3(0f, 500f, 0f);
            CreateGround(root.transform);
            MMOEquipmentVisualInstanceMarker marker = CreateWeapon(root.transform);
            CreatePersistent(marker);
            CreatePreviewCamera(root.transform);
            Selection.activeGameObject = root;
            SceneView view = SceneView.lastActiveSceneView;
            if (view != null)
            {
                view.pivot = root.transform.TransformPoint(new Vector3(0f, 1.15f, 0f));
                view.rotation = Quaternion.Euler(9f, -32f, 0f);
                view.size = 2.25f;
                view.Repaint();
            }

            Debug.Log("Empower Weapon VFX editor preview staged. It is diagnostic, unsaved, and uses the production prefabs/materials.", root);
        }

        [MenuItem("Tools/RPG Clone/VFX/Clear Empower Weapon VFX Preview")]
        public static void Clear()
        {
            GameObject existing = GameObject.Find(PreviewName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }
        }

        private static void CreateGround(Transform parent)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Preview Ground";
            ground.transform.SetParent(parent);
            ground.transform.localScale = new Vector3(0.42f, 1f, 0.42f);
            Material material = new(Shader.Find("Universal Render Pipeline/Lit"))
            {
                color = new Color(0.13f, 0.11f, 0.08f, 1f),
                hideFlags = HideFlags.HideAndDontSave
            };
            ground.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(ground.GetComponent<Collider>());
        }

        private static void CreatePreviewCamera(Transform parent)
        {
            GameObject cameraObject = new("Empower Weapon Preview Camera");
            cameraObject.transform.SetParent(parent);
            cameraObject.transform.localPosition = new Vector3(2.25f, 2.15f, -3.25f);
            cameraObject.transform.LookAt(parent.TransformPoint(new Vector3(0f, 1.76f, 0f)));
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.035f, 0.025f, 1f);
            camera.fieldOfView = 34f;
            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = 30f;
            camera.enabled = false;

            GameObject lightObject = new("Empower Weapon Preview Light");
            lightObject.transform.SetParent(parent);
            lightObject.transform.localRotation = Quaternion.Euler(42f, -35f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.86f, 0.92f, 0.78f, 1f);
            light.intensity = 1.15f;
            light.shadows = LightShadows.None;
        }

        private static MMOEquipmentVisualInstanceMarker CreateWeapon(Transform parent)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PreviewWeaponPath);
            MMOEquipmentVisualDefinition definition =
                AssetDatabase.LoadAssetAtPath<MMOEquipmentVisualDefinition>(PreviewWeaponDefinitionPath);
            if (prefab == null || definition == null)
            {
                throw new MissingReferenceException("Empower Weapon preview weapon assets are missing.");
            }

            GameObject weapon = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            weapon.name = "Preview Main Hand Millguard Saber";
            weapon.transform.SetParent(parent);
            weapon.transform.localPosition = new Vector3(-0.08f, 1.28f, 0f);
            weapon.transform.localRotation = Quaternion.Euler(0f, -8f, -58f);
            weapon.transform.localScale = Vector3.one * 3.1f;
            foreach (Collider collider in weapon.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(collider);
            }

            MMOEquipmentVisualInstanceMarker marker =
                weapon.GetComponent<MMOEquipmentVisualInstanceMarker>()
                ?? weapon.AddComponent<MMOEquipmentVisualInstanceMarker>();
            marker.Configure(definition, MMOEquipmentAttachmentPresentationState.Ready);
            return marker;
        }

        private static void CreatePersistent(MMOEquipmentVisualInstanceMarker marker)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PersistentPath);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, marker.transform);
            EmpowerWeaponPersistentVFX controller = instance.GetComponent<EmpowerWeaponPersistentVFX>();
            controller.Attach(marker);
            MethodInfo applyIntensity = typeof(EmpowerWeaponPersistentVFX).GetMethod(
                "ApplyIntensity",
                BindingFlags.Instance | BindingFlags.NonPublic);
            applyIntensity?.Invoke(controller, new object[] { 1f });
            Simulate(instance, 0.9f);
        }

        private static void Simulate(GameObject root, float time)
        {
            foreach (ParticleSystem particles in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                particles.Play(true);
                particles.Simulate(time, true, false, true);
                particles.Pause(true);
            }
        }
    }
}
#endif
