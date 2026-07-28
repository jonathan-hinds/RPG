using System.Text;
using System.Linq;
using NUnit.Framework;
using RPGClone.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RPGClone.EditorTests
{
    public sealed class MMOUnitFrameThemeTests
    {
        private const string ThemePath = "RPGClone/UI/UnitFrames/ClassicUnitFrameTheme";
        private const string PlayerPrefabPath =
            "Assets/Resources/RPGClone/UI/UnitFrames/PlayerUnitFrame.prefab";
        private const string TargetPrefabPath =
            "Assets/Resources/RPGClone/UI/UnitFrames/TargetUnitFrame.prefab";
        private const string PartyPrefabPath =
            "Assets/Resources/RPGClone/UI/UnitFrames/PartyUnitFrame.prefab";
        private const string ScenePath = "Assets/Scenes/OrcishStarterValley.unity";

        [Test]
        public void ClassicTheme_ContainsEveryModularLayer()
        {
            MMOUnitFrameTheme theme = Resources.Load<MMOUnitFrameTheme>(ThemePath);

            Assert.That(theme, Is.Not.Null);
            Assert.That(theme.Backplate, Is.Not.Null);
            Assert.That(theme.PortraitBezel, Is.Not.Null);
            Assert.That(theme.PortraitMask, Is.Not.Null);
            Assert.That(theme.Nameplate, Is.Not.Null);
            Assert.That(theme.BarWell, Is.Not.Null);
            Assert.That(theme.LevelMedallion, Is.Not.Null);
        }

        [TestCase(PlayerPrefabPath, MMOUnitFrameStyle.Player)]
        [TestCase(TargetPrefabPath, MMOUnitFrameStyle.Target)]
        [TestCase(PartyPrefabPath, MMOUnitFrameStyle.Party)]
        public void ConfigureStyle_PreservesAuthoredPrefabHierarchy(
            string prefabPath,
            MMOUnitFrameStyle style)
        {
            MMOUnitFrameTheme theme = Resources.Load<MMOUnitFrameTheme>(ThemePath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            GameObject instance = Object.Instantiate(prefab);
            instance.SetActive(false);

            try
            {
                MMOUnitFrameView view = instance.GetComponent<MMOUnitFrameView>();
                Assert.That(view.RebindAuthoredHierarchy(out string bindingError), Is.True, bindingError);
                string before = CaptureAuthoredHierarchy(instance.transform);

                view.ConfigureStyle(style, theme);

                Assert.That(CaptureAuthoredHierarchy(instance.transform), Is.EqualTo(before));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [TestCase(MMOUnitFrameStyle.Player)]
        [TestCase(MMOUnitFrameStyle.Target)]
        [TestCase(MMOUnitFrameStyle.Party)]
        public void PortraitMask_FitsInsideBezel(MMOUnitFrameStyle style)
        {
            MMOUnitFrameTheme theme = Resources.Load<MMOUnitFrameTheme>(ThemePath);
            MMOUnitFrameLayout layout = theme.GetLayout(style);

            Assert.That(layout.PortraitMaskSize, Is.LessThan(layout.PortraitBezelSize * 0.75f));
            Assert.That(layout.PortraitMaskSize, Is.GreaterThan(layout.PortraitBezelSize * 0.65f));
        }

        [Test]
        public void Layouts_ReserveUsefulNameWidths()
        {
            MMOUnitFrameTheme theme = Resources.Load<MMOUnitFrameTheme>(ThemePath);

            foreach (MMOUnitFrameStyle style in System.Enum.GetValues(typeof(MMOUnitFrameStyle)))
            {
                MMOUnitFrameLayout layout = theme.GetLayout(style);
                float nameWidth = layout.FrameSize.x
                    - layout.ContentPortraitInset
                    - layout.ContentOuterInset;
                Assert.That(nameWidth, Is.GreaterThanOrEqualTo(style == MMOUnitFrameStyle.Party ? 180f : 230f));
            }
        }

        [Test]
        public void PartyFrame_IsMoreCompactThanPrimaryFrames()
        {
            MMOUnitFrameTheme theme = Resources.Load<MMOUnitFrameTheme>(ThemePath);

            Assert.That(theme.GetFrameSize(MMOUnitFrameStyle.Party).x,
                Is.LessThan(theme.GetFrameSize(MMOUnitFrameStyle.Player).x));
            Assert.That(theme.GetFrameSize(MMOUnitFrameStyle.Party).y,
                Is.LessThan(theme.GetFrameSize(MMOUnitFrameStyle.Target).y));
        }

        [TestCase(PlayerPrefabPath, MMOUnitFrameStyle.Player)]
        [TestCase(TargetPrefabPath, MMOUnitFrameStyle.Target)]
        [TestCase(PartyPrefabPath, MMOUnitFrameStyle.Party)]
        public void EditablePrefab_ContainsAuthoredFrameHierarchy(
            string prefabPath,
            MMOUnitFrameStyle expectedStyle)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            Assert.That(prefab, Is.Not.Null, $"Missing editable unit-frame prefab: {prefabPath}");
            MMOUnitFrameView view = prefab.GetComponent<MMOUnitFrameView>();
            Assert.That(view, Is.Not.Null);
            Assert.That(view.FrameStyle, Is.EqualTo(expectedStyle));
            Assert.That(prefab.transform.Find("Content/Health Bar"), Is.Not.Null);
            Assert.That(prefab.transform.Find("Content/Resource Bar"), Is.Not.Null);
            Assert.That(prefab.transform.Find("Portrait Mask/Portrait"), Is.Not.Null);
        }

        [Test]
        public void StarterScene_UsesEditablePrefabsAndConfiguredMargins()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedForTest = !scene.IsValid() || !scene.isLoaded;
            if (openedForTest)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            }

            try
            {
                MMOUnitFramePresenter presenter = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<MMOUnitFramePresenter>(true))
                    .FirstOrDefault();
                Assert.That(presenter, Is.Not.Null);

                SerializedObject serializedPresenter = new(presenter);
                MMOUnitFrameView player = serializedPresenter
                    .FindProperty("playerFrame").objectReferenceValue as MMOUnitFrameView;
                MMOUnitFrameView target = serializedPresenter
                    .FindProperty("targetFrame").objectReferenceValue as MMOUnitFrameView;
                MMOUnitFrameView party = serializedPresenter
                    .FindProperty("partyFramePrefab").objectReferenceValue as MMOUnitFrameView;

                Assert.That(
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(player.gameObject),
                    Is.EqualTo(PlayerPrefabPath));
                Assert.That(
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(target.gameObject),
                    Is.EqualTo(TargetPrefabPath));
                Assert.That(AssetDatabase.GetAssetPath(party), Is.EqualTo(PartyPrefabPath));
                Assert.That(HasAppearanceOverride(player), Is.False);
                Assert.That(HasAppearanceOverride(target), Is.False);
                Assert.That(
                    serializedPresenter.FindProperty("primaryFrameSpacing").floatValue,
                    Is.GreaterThanOrEqualTo(24f));
                Assert.That(
                    serializedPresenter.FindProperty("partyFrameSpacing").floatValue,
                    Is.GreaterThanOrEqualTo(12f));
            }
            finally
            {
                if (openedForTest)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static bool HasAppearanceOverride(MMOUnitFrameView frame)
        {
            PropertyModification[] modifications =
                PrefabUtility.GetPropertyModifications(frame.gameObject);
            Object sourceGameObject =
                PrefabUtility.GetCorrespondingObjectFromSource(frame.gameObject);
            Object sourceRect =
                PrefabUtility.GetCorrespondingObjectFromSource(frame.transform);
            return modifications != null
                && modifications.Any(modification =>
                    !IsScenePlacementOverride(modification, sourceGameObject, sourceRect));
        }

        private static bool IsScenePlacementOverride(
            PropertyModification modification,
            Object sourceGameObject,
            Object sourceRect)
        {
            if (modification.target == sourceGameObject)
            {
                return modification.propertyPath == "m_Name"
                    || modification.propertyPath == "m_IsActive";
            }

            if (modification.target != sourceRect)
            {
                return false;
            }

            string path = modification.propertyPath;
            return path.StartsWith("m_Anchor")
                || path.StartsWith("m_Pivot")
                || path.StartsWith("m_AnchoredPosition")
                || path.StartsWith("m_LocalPosition")
                || path.StartsWith("m_LocalRotation")
                || path.StartsWith("m_LocalEulerAnglesHint")
                || path.StartsWith("m_LocalScale")
                || path == "m_ConstrainProportionsScale";
        }

        private static string CaptureAuthoredHierarchy(Transform root)
        {
            StringBuilder snapshot = new();
            AppendHierarchy(root, string.Empty, snapshot);
            return snapshot.ToString();
        }

        private static void AppendHierarchy(Transform current, string parentPath, StringBuilder snapshot)
        {
            string path = string.IsNullOrEmpty(parentPath)
                ? current.name
                : $"{parentPath}/{current.name}";
            snapshot.Append(path);
            snapshot.Append('|');
            snapshot.Append(current.GetSiblingIndex());

            if (current is RectTransform rect)
            {
                snapshot.Append('|');
                snapshot.Append(rect.anchorMin);
                snapshot.Append('|');
                snapshot.Append(rect.anchorMax);
                snapshot.Append('|');
                snapshot.Append(rect.pivot);
                snapshot.Append('|');
                snapshot.Append(rect.anchoredPosition);
                snapshot.Append('|');
                snapshot.Append(rect.sizeDelta);
            }

            snapshot.AppendLine();
            for (int index = 0; index < current.childCount; index++)
            {
                AppendHierarchy(current.GetChild(index), path, snapshot);
            }
        }
    }
}
