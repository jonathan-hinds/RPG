using NUnit.Framework;
using RPGClone.UI;
using UnityEngine;
using UnityEngine.UI;

namespace RPGClone.EditorTests
{
    public sealed class MMOUnitFrameThemeTests
    {
        private const string ThemePath = "RPGClone/UI/UnitFrames/ClassicUnitFrameTheme";

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

        [TestCase(MMOUnitFrameStyle.Player)]
        [TestCase(MMOUnitFrameStyle.Target)]
        [TestCase(MMOUnitFrameStyle.Party)]
        public void RebuildVisuals_CreatesLayeredFrameHierarchy(MMOUnitFrameStyle style)
        {
            MMOUnitFrameTheme theme = Resources.Load<MMOUnitFrameTheme>(ThemePath);
            GameObject root = new("Unit Frame Test", typeof(RectTransform));
            root.SetActive(false);

            try
            {
                MMOUnitFrameView view = root.AddComponent<MMOUnitFrameView>();
                view.ConfigureStyle(style, theme);

                Assert.That(root.transform.Find("Frame Shadow"), Is.Not.Null);
                Transform backplate = root.transform.Find("Backplate");
                Assert.That(backplate, Is.Not.Null);
                Assert.That(backplate.GetComponent<Image>().sprite, Is.EqualTo(theme.Backplate));
                Assert.That(root.transform.Find("Portrait Mask/Portrait"), Is.Not.Null);
                Assert.That(root.transform.Find("Portrait Bezel"), Is.Not.Null);
                Assert.That(root.transform.Find("Content/Nameplate/Name"), Is.Not.Null);
                Assert.That(root.transform.Find("Content/Health Bar/Fill Area/Fill/Highlight"), Is.Not.Null);
                Assert.That(root.transform.Find("Content/Resource Bar/Fill Area/Fill/Highlight"), Is.Not.Null);
                Assert.That(root.transform.Find("Level Badge/Level"), Is.Not.Null);
                Assert.That(root.transform.Find("Buffs"), Is.Not.Null);
                Assert.That(((RectTransform)root.transform).sizeDelta, Is.EqualTo(theme.GetFrameSize(style)));
            }
            finally
            {
                Object.DestroyImmediate(root);
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

        [TestCase(MMOUnitFrameStyle.Player, 0f)]
        [TestCase(MMOUnitFrameStyle.Party, 0f)]
        [TestCase(MMOUnitFrameStyle.Target, 1f)]
        public void PortraitSide_MatchesFrameRole(MMOUnitFrameStyle style, float expectedAnchorX)
        {
            MMOUnitFrameTheme theme = Resources.Load<MMOUnitFrameTheme>(ThemePath);
            GameObject root = new("Unit Frame Test", typeof(RectTransform));
            root.SetActive(false);

            try
            {
                MMOUnitFrameView view = root.AddComponent<MMOUnitFrameView>();
                view.ConfigureStyle(style, theme);

                RectTransform bezel = (RectTransform)root.transform.Find("Portrait Bezel");
                Assert.That(bezel.anchorMin.x, Is.EqualTo(expectedAnchorX));
                Assert.That(bezel.anchorMax.x, Is.EqualTo(expectedAnchorX));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [TestCase(MMOUnitFrameStyle.Player)]
        [TestCase(MMOUnitFrameStyle.Target)]
        [TestCase(MMOUnitFrameStyle.Party)]
        public void StatusFills_AreInsetInsideAuthoredWells(MMOUnitFrameStyle style)
        {
            MMOUnitFrameTheme theme = Resources.Load<MMOUnitFrameTheme>(ThemePath);
            GameObject root = new("Unit Frame Test", typeof(RectTransform));
            root.SetActive(false);

            try
            {
                MMOUnitFrameView view = root.AddComponent<MMOUnitFrameView>();
                view.ConfigureStyle(style, theme);

                RectTransform healthArea = (RectTransform)root.transform.Find("Content/Health Bar/Fill Area");
                RectTransform resourceArea = (RectTransform)root.transform.Find("Content/Resource Bar/Fill Area");
                Assert.That(healthArea.offsetMin.x, Is.GreaterThan(0f));
                Assert.That(healthArea.offsetMax.x, Is.LessThan(0f));
                Assert.That(resourceArea.offsetMin.y, Is.GreaterThan(0f));
                Assert.That(resourceArea.offsetMax.y, Is.LessThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
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
    }
}
