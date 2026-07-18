#if UNITY_EDITOR
using NUnit.Framework;
using RPGClone.Vfx.Warrior;
using UnityEngine;

namespace RPGClone.EditorTests
{
    public sealed class BerzerkitisVFXProfileTests
    {
        [Test]
        public void Defaults_MatchAuthoredActivationAndFadeWindows()
        {
            BerzerkitisVFXProfile profile = ScriptableObject.CreateInstance<BerzerkitisVFXProfile>();
            try
            {
                Assert.That(profile.ActivationDuration, Is.InRange(1f, 1.5f));
                Assert.That(profile.BuffFadeOutDuration, Is.InRange(0.3f, 0.5f));
                Assert.That(profile.FlameColumnCount, Is.GreaterThan(0));
                Assert.That(profile.ActivationEmberCount, Is.GreaterThan(profile.FlameColumnCount));
                Assert.That(profile.Colors.WhiteHot.maxColorComponent, Is.GreaterThan(profile.Colors.BloodRed.maxColorComponent));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void HandOffsets_AreIndependentlyAuthored()
        {
            BerzerkitisVFXProfile profile = ScriptableObject.CreateInstance<BerzerkitisVFXProfile>();
            try
            {
                Assert.That(profile.LeftHandPositionOffset, Is.Not.EqualTo(profile.RightHandPositionOffset));
                Assert.That(profile.MotionTrailLifetime, Is.GreaterThan(0f));
                Assert.That(profile.AttackPulseDuration, Is.GreaterThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }
    }
}
#endif
