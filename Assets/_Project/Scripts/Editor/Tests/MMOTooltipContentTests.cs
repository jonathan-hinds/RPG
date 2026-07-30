#if UNITY_EDITOR
using System.IO;
using System.Linq;
using NUnit.Framework;
using RPGClone.Abilities;
using RPGClone.Characters;
using RPGClone.Combat;
using RPGClone.Inventory;
using RPGClone.UI;
using UnityEngine;
using UnityEngine.UI;

namespace RPGClone.EditorTests
{
    public sealed class MMOTooltipContentTests
    {
        private const string TooltipTexturePath =
            "Assets/Resources/RPGClone/UI/Tooltip/TooltipPanel.png";

        [Test]
        public void AbilityTooltip_RecalculatesSpellDamageForEachCasterBuild()
        {
            GameObject firstCasterObject = new("First Mage");
            GameObject secondCasterObject = new("Second Mage");
            MMOAbilityDefinition ability = ScriptableObject.CreateInstance<MMOAbilityDefinition>();

            try
            {
                MMOCharacterIdentity firstCaster = firstCasterObject.AddComponent<MMOCharacterIdentity>();
                MMOCharacterIdentity secondCaster = secondCasterObject.AddComponent<MMOCharacterIdentity>();
                firstCaster.Stats.Configure(10, 0, 0, 10, 0, 0, 0, 10, 1f, 2f, 2f, 3f);
                secondCaster.Stats.Configure(10, 0, 0, 20, 0, 0, 0, 40, 1f, 2f, 2f, 3f);

                MMOAbilityEffectDefinition damage = new();
                damage.Configure(
                    MMOAbilityEffectType.Damage,
                    MMOAbilityAmountSource.SpellPower,
                    MMODamageSchool.Fire,
                    10f,
                    0.5f);
                ability.Configure(
                    "test_fire",
                    "Test Fire",
                    "Burns a hostile target.",
                    MMOAbilityTargetType.Hostile,
                    false,
                    false,
                    30f,
                    0f,
                    10,
                    new[] { damage });

                string firstText = string.Join("\n", MMOTooltipContentBuilder
                    .BuildAbility(ability, firstCaster)
                    .Lines
                    .Select(line => line.Text));
                string secondText = string.Join("\n", MMOTooltipContentBuilder
                    .BuildAbility(ability, secondCaster)
                    .Lines
                    .Select(line => line.Text));

                Assert.That(firstText, Does.Contain("Deals 15 Fire damage."));
                Assert.That(secondText, Does.Contain("Deals 30 Fire damage."));
                Assert.That(secondText, Is.Not.EqualTo(firstText));
            }
            finally
            {
                Object.DestroyImmediate(ability);
                Object.DestroyImmediate(secondCasterObject);
                Object.DestroyImmediate(firstCasterObject);
            }
        }

        [Test]
        public void WeaponDamagePreview_UsesCombatRangeWithoutMutatingCasterComponents()
        {
            GameObject casterObject = new("Warrior");
            try
            {
                MMOCharacterIdentity caster = casterObject.AddComponent<MMOCharacterIdentity>();
                caster.Stats.Configure(10, 10, 0, 0, 0, 0, 8, 0, 1f, 2f, 2f, 3f);
                MMOAbilityEffectDefinition damage = new();
                damage.Configure(
                    MMOAbilityEffectType.Damage,
                    MMOAbilityAmountSource.WeaponDamage,
                    MMODamageSchool.Physical,
                    2f,
                    1f);

                MMOAbilityAmountRange amount = MMOCombatResolver.CalculateWeaponDamageRange(caster, damage);

                Assert.That(amount.Maximum, Is.GreaterThan(amount.Minimum));
                Assert.That(caster.GetComponent<MMOWeaponSkillController>(), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(casterObject);
            }
        }

        [Test]
        public void TooltipPresenter_GrowsFromMeasuredContentWithinThemeBounds()
        {
            GameObject canvasObject = new("Tooltip Test Canvas", typeof(RectTransform), typeof(Canvas));
            GameObject tooltipObject = new("Tooltip", typeof(RectTransform));
            tooltipObject.transform.SetParent(canvasObject.transform, false);

            try
            {
                RectTransform canvasRect = (RectTransform)canvasObject.transform;
                canvasRect.sizeDelta = new Vector2(1280f, 720f);
                MMOGameTooltipPresenter presenter = tooltipObject.AddComponent<MMOGameTooltipPresenter>();
                MMOTooltipTheme theme = MMOTooltipTheme.LoadDefault();

                MMOTooltipContent compact = new("Short", Color.white);
                compact.Add("One line.", theme.BodyFontSize, FontStyle.Normal, Color.white);
                presenter.Show(compact, new Vector2(200f, 200f), null);
                Vector2 compactSize = presenter.CurrentSize;

                MMOTooltipContent detailed = new("Detailed Ability", Color.white);
                detailed.AddDouble("35 Mana", "30 yd range", theme.BodyFontSize, FontStyle.Normal, Color.white);
                detailed.AddDouble("2.5 sec cast", "12 sec cooldown", theme.BodyFontSize, FontStyle.Normal, Color.white);
                detailed.Add(
                    "Calls down a pillar of flame at the targeted area and burns every afflicted enemy over time with a value calculated from the current character build.",
                    theme.BodyFontSize,
                    FontStyle.Normal,
                    theme.DescriptionText,
                    theme.SectionSpacing);
                presenter.Show(detailed, new Vector2(200f, 200f), null);
                Vector2 detailedSize = presenter.CurrentSize;

                Assert.That(compactSize.x, Is.EqualTo(theme.MinimumWidth).Within(0.5f));
                Assert.That(detailedSize.x, Is.GreaterThan(compactSize.x));
                Assert.That(detailedSize.x, Is.LessThanOrEqualTo(theme.MaximumWidth + 0.5f));
                Assert.That(detailedSize.y, Is.GreaterThan(compactSize.y));
            }
            finally
            {
                Object.DestroyImmediate(tooltipObject);
                Object.DestroyImmediate(canvasObject);
            }
        }

        [Test]
        public void DefaultTheme_UsesScalableNineSliceArtwork()
        {
            MMOTooltipTheme theme = MMOTooltipTheme.LoadDefault();

            Assert.That(theme, Is.Not.Null);
            Assert.That(theme.PanelSprite, Is.Not.Null);
            Assert.That(theme.PanelSprite.border.sqrMagnitude, Is.GreaterThan(0f));
            Assert.That(theme.MinimumWidth, Is.LessThan(theme.MaximumWidth));
        }

        [Test]
        public void TooltipPanel_HasOpaqueEdgeAndTranslucentVignetteCenter()
        {
            Texture2D texture = new(2, 2);
            try
            {
                byte[] pngBytes = File.ReadAllBytes(Path.GetFullPath(TooltipTexturePath));
                Assert.That(ImageConversion.LoadImage(texture, pngBytes), Is.True);

                Color center = texture.GetPixel(texture.width / 2, texture.height / 2);
                Color topEdge = texture.GetPixel(texture.width / 2, texture.height - 1);
                Color leftEdge = texture.GetPixel(0, texture.height / 2);

                Assert.That(center.a, Is.InRange(0.5f, 0.7f));
                Assert.That(topEdge.a, Is.GreaterThanOrEqualTo(0.98f));
                Assert.That(leftEdge.a, Is.GreaterThanOrEqualTo(0.98f));
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void EquipmentTooltip_ShowsAuthoredBonusesWithoutDerivedStatInflation()
        {
            MMOItemDefinition item = ScriptableObject.CreateInstance<MMOItemDefinition>();
            MMOCharacterStats bonuses = new();
            bonuses.Configure(3, 4, 5, 6, 0, 7, 8, 9, 0f, 0f, 2f, 3f);

            try
            {
                item.ConfigureEquipment(
                    "test_robe",
                    "Test Robe",
                    string.Empty,
                    MMOItemQuality.Uncommon,
                    MMOEquipmentSlotType.Chest,
                    MMOArmorWeight.Cloth,
                    bonuses,
                    10);

                string text = string.Join("\n", MMOTooltipContentBuilder
                    .BuildItem(item)
                    .Lines
                    .Select(line => line.Text));

                Assert.That(text, Does.Contain("7 Armor"));
                Assert.That(text, Does.Contain("+8 Attack Power"));
                Assert.That(text, Does.Contain("+9 Spell Power"));
                Assert.That(text, Does.Not.Contain("17 Armor"));
            }
            finally
            {
                Object.DestroyImmediate(item);
            }
        }

        [Test]
        public void EquipmentTooltip_ShowsRequiredLevelAndHighlightsUnmetRequirement()
        {
            MMOItemDefinition item = ScriptableObject.CreateInstance<MMOItemDefinition>();
            MMOCharacterStats bonuses = new();

            try
            {
                item.ConfigureEquipment(
                    "test_level_robe",
                    "Aspirant Robe",
                    string.Empty,
                    MMOItemQuality.Uncommon,
                    MMOEquipmentSlotType.Chest,
                    MMOArmorWeight.Cloth,
                    bonuses,
                    10);
                item.SetRequiredLevel(8);

                MMOTooltipTheme theme = MMOTooltipTheme.LoadDefault();
                MMOTooltipLine belowLevel = MMOTooltipContentBuilder
                    .BuildItem(item, theme: theme, viewerLevel: 7)
                    .Lines
                    .Single(line => line.Text == "Requires Level 8");
                MMOTooltipLine atLevel = MMOTooltipContentBuilder
                    .BuildItem(item, theme: theme, viewerLevel: 8)
                    .Lines
                    .Single(line => line.Text == "Requires Level 8");

                Assert.That(belowLevel.Color, Is.EqualTo(theme.NegativeText));
                Assert.That(atLevel.Color, Is.EqualTo(theme.PrimaryText));
            }
            finally
            {
                Object.DestroyImmediate(item);
            }
        }
    }
}
#endif
