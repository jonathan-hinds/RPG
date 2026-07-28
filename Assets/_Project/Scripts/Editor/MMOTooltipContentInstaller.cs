#if UNITY_EDITOR
using RPGClone.Abilities;
using RPGClone.UI;
using UnityEditor;
using UnityEngine;

namespace RPGClone.EditorTools
{
    public static class MMOTooltipContentInstaller
    {
        private const string TexturePath = "Assets/Resources/RPGClone/UI/Tooltip/TooltipPanel.png";
        private const string ThemePath = "Assets/Resources/RPGClone/UI/Tooltip/DefaultTooltipTheme.asset";

        [MenuItem("Tools/RPG Clone/UI/Install Tooltip Theme")]
        public static void Install()
        {
            ConfigureTexture();

            MMOTooltipTheme theme = AssetDatabase.LoadAssetAtPath<MMOTooltipTheme>(ThemePath);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<MMOTooltipTheme>();
                AssetDatabase.CreateAsset(theme, ThemePath);
            }

            Sprite panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexturePath);
            theme.ConfigurePanel(panelSprite);
            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Tooltip theme installed with a scalable nine-slice panel.");
        }

        [MenuItem("Tools/RPG Clone/UI/Preview Fireball Tooltip")]
        public static void PreviewFireball()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Enter Play Mode before previewing the runtime tooltip.");
                return;
            }

            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(
                "Assets/_Project/Configs/Abilities/Mage_Fireball.asset");
            if (ability == null)
            {
                throw new MissingReferenceException("Mage Fireball ability asset was not found.");
            }

            MMOGameTooltipPresenter.ShowAbility(
                ability,
                new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
        }

        private static void ConfigureTexture()
        {
            if (AssetImporter.GetAtPath(TexturePath) is not TextureImporter importer)
            {
                throw new MissingReferenceException($"Tooltip texture not found at {TexturePath}.");
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = new Vector4(6f, 6f, 6f, 6f);
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 512;
            importer.compressionQuality = 100;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }
    }
}
#endif
