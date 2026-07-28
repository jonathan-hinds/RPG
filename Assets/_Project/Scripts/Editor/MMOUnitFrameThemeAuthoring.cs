using RPGClone.UI;
using UnityEditor;
using UnityEngine;

namespace RPGClone.EditorTools
{
    public static class MMOUnitFrameThemeAuthoring
    {
        private const string ThemeFolder = "Assets/Resources/RPGClone/UI/UnitFrames";
        private const string ThemePath = ThemeFolder + "/ClassicUnitFrameTheme.asset";

        [MenuItem("Tools/RPG Clone/UI/Build Classic Unit Frame Theme")]
        public static void BuildTheme()
        {
            EnsureFolder(ThemeFolder);

            ConfigureSpriteImporter(
                ThemeFolder + "/UnitFrame_Backplate.png",
                1024,
                TextureImporterCompression.CompressedHQ,
                new Vector4(90f, 40f, 90f, 40f));
            ConfigureSpriteImporter(
                ThemeFolder + "/UnitFrame_PortraitBezel.png",
                512,
                TextureImporterCompression.Uncompressed,
                Vector4.zero);
            ConfigureSpriteImporter(
                ThemeFolder + "/UnitFrame_PortraitMask.png",
                128,
                TextureImporterCompression.Uncompressed,
                Vector4.zero);
            ConfigureSpriteImporter(
                ThemeFolder + "/UnitFrame_Nameplate.png",
                512,
                TextureImporterCompression.Uncompressed,
                new Vector4(52f, 20f, 52f, 20f));
            ConfigureSpriteImporter(
                ThemeFolder + "/UnitFrame_BarWell.png",
                512,
                TextureImporterCompression.Uncompressed,
                new Vector4(52f, 20f, 52f, 20f));
            ConfigureSpriteImporter(
                ThemeFolder + "/UnitFrame_LevelMedallion.png",
                128,
                TextureImporterCompression.Uncompressed,
                Vector4.zero);

            MMOUnitFrameTheme theme = AssetDatabase.LoadAssetAtPath<MMOUnitFrameTheme>(ThemePath);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<MMOUnitFrameTheme>();
                AssetDatabase.CreateAsset(theme, ThemePath);
            }

            theme.ConfigureArtwork(
                LoadRequiredSprite(ThemeFolder + "/UnitFrame_Backplate.png"),
                LoadRequiredSprite(ThemeFolder + "/UnitFrame_PortraitBezel.png"),
                LoadRequiredSprite(ThemeFolder + "/UnitFrame_PortraitMask.png"),
                LoadRequiredSprite(ThemeFolder + "/UnitFrame_Nameplate.png"),
                LoadRequiredSprite(ThemeFolder + "/UnitFrame_BarWell.png"),
                LoadRequiredSprite(ThemeFolder + "/UnitFrame_LevelMedallion.png"));
            theme.ConfigureLayouts(
                CreatePrimaryLayout(MMOUnitFramePortraitSide.Left),
                CreatePrimaryLayout(MMOUnitFramePortraitSide.Right),
                CreatePartyLayout());

            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = theme;
            Debug.Log($"Built layered Classic unit-frame theme at {ThemePath}.");
        }

        private static MMOUnitFrameLayout CreatePrimaryLayout(MMOUnitFramePortraitSide portraitSide)
        {
            return new MMOUnitFrameLayout().Configure(
                new Vector2(330f, 96f),
                portraitSide,
                88f,
                61f,
                3f,
                76f,
                10f,
                10f,
                10f,
                24f,
                22f,
                14f,
                2f,
                29f,
                new Vector2(31f, -31f),
                15,
                11);
        }

        private static MMOUnitFrameLayout CreatePartyLayout()
        {
            return new MMOUnitFrameLayout().Configure(
                new Vector2(250f, 68f),
                MMOUnitFramePortraitSide.Left,
                62f,
                43f,
                2f,
                54f,
                8f,
                6f,
                6f,
                18f,
                17f,
                11f,
                2f,
                23f,
                new Vector2(22f, -21f),
                13,
                9);
        }

        private static Sprite LoadRequiredSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                throw new UnityException($"Required unit-frame sprite is missing or not imported as Sprite: {path}");
            }

            return sprite;
        }

        private static void ConfigureSpriteImporter(
            string path,
            int maxTextureSize,
            TextureImporterCompression compression,
            Vector4 border)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                throw new UnityException($"Required unit-frame texture is missing: {path}");
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = border;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = maxTextureSize;
            importer.textureCompression = compression;
            importer.compressionQuality = 100;
            importer.SaveAndReimport();
        }

        private static void EnsureFolder(string folderPath)
        {
            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}
