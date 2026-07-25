using System.IO;
using RPGClone.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RPGClone.EditorTools
{
    public static class MMOSlotFrameworkAuthoring
    {
        private const string AssetFolder = "Assets/Resources/RPGClone/UI/SlotFramework";
        private const string SharedSlotPrefabPath = AssetFolder + "/SharedSlot.prefab";

        [MenuItem("Tools/RPG Clone/UI/Rebuild Shared Slot Assets")]
        public static void RebuildSharedSlotAssets()
        {
            ConfigureSpriteImporters();
            BuildSharedSlotPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Shared slot assets rebuilt at {SharedSlotPrefabPath}.");
        }

        private static void ConfigureSpriteImporters()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { AssetFolder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                {
                    continue;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.spritePixelsPerUnit = 100f;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 1024;
                importer.spriteBorder = ResolveBorder(Path.GetFileNameWithoutExtension(path));
                importer.SaveAndReimport();
            }

            MMOSlotSkin.ClearCache();
        }

        private static Vector4 ResolveBorder(string assetName)
        {
            if (assetName == "Panel_Frame_Default")
            {
                return new Vector4(36f, 36f, 36f, 36f);
            }

            if (assetName == "Panel_Header_Default"
                || assetName == "BagPanel_CurrencyBar")
            {
                return new Vector4(48f, 30f, 48f, 30f);
            }

            if (assetName == "ActionBar_Background_Center")
            {
                return new Vector4(42f, 28f, 42f, 28f);
            }

            return Vector4.zero;
        }

        private static void BuildSharedSlotPrefab()
        {
            GameObject root = new("Shared Slot", typeof(RectTransform), typeof(Image));
            try
            {
                RectTransform rect = (RectTransform)root.transform;
                rect.sizeDelta = new Vector2(48f, 48f);
                Image raycastImage = root.GetComponent<Image>();
                raycastImage.color = new Color(1f, 1f, 1f, 0.001f);

                MMOSlotView view = MMOSlotView.Attach(root);
                view.Present(MMOSlotPresentation.Empty());
                PrefabUtility.SaveAsPrefabAsset(root, SharedSlotPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
