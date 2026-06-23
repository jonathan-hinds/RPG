using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RPGClone.Rendering.Editor
{
    public static class PixelationInstaller
    {
        private const string ShaderPath = "Assets/_Project/Rendering/Shaders/PixelationPostProcess.shader";
        private const string ProfilePath = "Assets/Settings/OrcishStarterValley_PolishProfile.asset";

        [MenuItem("Tools/RPG Clone/Rendering/Install Pixelation")]
        public static void Install()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);

            InstallOnRenderer("Assets/Settings/PC_Renderer.asset", shader);
            InstallOnRenderer("Assets/Settings/Mobile_Renderer.asset", shader);
            InstallOnProfile();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void InstallOnRenderer(string rendererPath, Shader shader)
        {
            ScriptableRendererData rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(rendererPath);
            if (rendererData == null)
            {
                Debug.LogWarning($"Pixelation installer could not find renderer data at {rendererPath}.");
                return;
            }

            SerializedObject serializedRenderer = new(rendererData);
            SerializedProperty features = serializedRenderer.FindProperty("m_RendererFeatures");
            SerializedProperty featureMap = serializedRenderer.FindProperty("m_RendererFeatureMap");

            RemoveRendererFeatureType(features, featureMap, typeof(PixelationRendererFeature));

            PixelationRendererFeature feature = ScriptableObject.CreateInstance<PixelationRendererFeature>();
            feature.name = "Pixelation";

            SerializedObject serializedFeature = new(feature);
            serializedFeature.FindProperty("shader").objectReferenceValue = shader;
            serializedFeature.ApplyModifiedPropertiesWithoutUndo();

            if (EditorUtility.IsPersistent(rendererData))
            {
                AssetDatabase.AddObjectToAsset(feature, rendererData);
            }

            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);

            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;

            featureMap.arraySize++;
            featureMap.GetArrayElementAtIndex(featureMap.arraySize - 1).longValue = localId;

            serializedRenderer.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rendererData);
            EditorUtility.SetDirty(feature);
        }

        private static void RemoveRendererFeatureType(SerializedProperty features, SerializedProperty featureMap, Type type)
        {
            for (int i = features.arraySize - 1; i >= 0; i--)
            {
                ScriptableRendererFeature feature = features.GetArrayElementAtIndex(i).objectReferenceValue as ScriptableRendererFeature;
                if (feature == null || feature.GetType() != type)
                {
                    continue;
                }

                features.DeleteArrayElementAtIndex(i);
                if (i < featureMap.arraySize)
                {
                    featureMap.DeleteArrayElementAtIndex(i);
                }

                UnityEngine.Object.DestroyImmediate(feature, true);
            }
        }

        private static void InstallOnProfile()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                Debug.LogWarning($"Pixelation installer could not find volume profile at {ProfilePath}.");
                return;
            }

            RemoveVolumeComponent<PixelationVolume>(profile);
            profile.components.RemoveAll(component => component == null);

            PixelationVolume pixelation = profile.Add<PixelationVolume>(true);
            pixelation.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            pixelation.name = nameof(PixelationVolume);

            if (!AssetDatabase.Contains(pixelation))
            {
                AssetDatabase.AddObjectToAsset(pixelation, profile);
            }

            pixelation.active = true;
            pixelation.pixelAmount.overrideState = true;
            pixelation.pixelAmount.value = 4;

            profile.Reset();
            EditorUtility.SetDirty(profile);
        }

        private static void RemoveVolumeComponent<T>(VolumeProfile profile) where T : VolumeComponent
        {
            foreach (T component in profile.components.OfType<T>().ToArray())
            {
                profile.components.Remove(component);
                UnityEngine.Object.DestroyImmediate(component, true);
            }
        }
    }
}
