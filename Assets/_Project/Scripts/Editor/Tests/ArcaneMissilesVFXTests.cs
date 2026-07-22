#if UNITY_EDITOR
using NUnit.Framework;
using RPGClone.Abilities;
using RPGClone.Combat;
using RPGClone.Vfx;
using RPGClone.Vfx.ArcaneMissiles;
using UnityEditor;
using UnityEngine;

namespace RPGClone.EditorTests
{
    public sealed class ArcaneMissilesVFXTests
    {
        private const string Root = "Assets/_Project/VFX/ArcaneMissiles";

        [Test]
        public void Profile_DefaultsDescribeFiveDistinctTimedMissiles()
        {
            ArcaneMissilesVFXProfile profile = AssetDatabase.LoadAssetAtPath<ArcaneMissilesVFXProfile>($"{Root}/Profiles/ArcaneMissilesVFX_Default.asset");
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.ChannelDuration, Is.EqualTo(5f).Within(0.001f));
            Assert.That(profile.MissileCountValue, Is.EqualTo(5));
            Assert.That(profile.GetOrbOffset(0), Is.Not.EqualTo(profile.GetOrbOffset(1)));
            Assert.That(profile.GetOrbOffset(1), Is.Not.EqualTo(profile.GetOrbOffset(2)));
            Assert.That(new[]
            {
                profile.GetFiringOrb(0),
                profile.GetFiringOrb(1),
                profile.GetFiringOrb(2),
                profile.GetFiringOrb(3),
                profile.GetFiringOrb(4)
            }, Is.EqualTo(new[] { 0, 1, 2, 0, 2 }));
            Assert.That(profile.FinalScaleMultiplier, Is.InRange(1.15f, 1.3f));
        }

        [Test]
        public void InstalledPrefabs_AreLayeredReusableAndPoolable()
        {
            GameObject casting = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Prefabs/ArcaneMissilesVFX.prefab");
            GameObject projectile = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Prefabs/ArcaneMissileProjectileVFX.prefab");
            GameObject impact = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Prefabs/ArcaneMissilesImpactVFX.prefab");
            GameObject interrupt = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Prefabs/ArcaneMissilesInterruptVFX.prefab");

            Assert.That(casting.GetComponent<ArcaneMissilesVFX>(), Is.Not.Null);
            Assert.That(casting.GetComponent<MMOAbilityVfxPoolable>(), Is.Not.Null);
            Assert.That(casting.GetComponentsInChildren<ArcaneMissilesFabricatorVFX>(true), Has.Length.EqualTo(3));
            Assert.That(casting.GetComponentsInChildren<LineRenderer>(true), Has.Length.EqualTo(4));
            Assert.That(projectile.GetComponent<ArcaneMissileProjectileVFX>(), Is.Not.Null);
            Assert.That(projectile.GetComponent<MMOAbilityVfxPoolable>(), Is.Not.Null);
            Assert.That(projectile.GetComponentsInChildren<TrailRenderer>(true), Has.Length.EqualTo(4));
            Assert.That(impact.GetComponent<ArcaneMissilesImpactVFX>(), Is.Not.Null);
            Assert.That(interrupt.GetComponent<ArcaneMissilesInterruptVFX>(), Is.Not.Null);
        }

        [Test]
        public void AbilityBinding_UsesDedicatedCastingPackageAndNoGenericReleaseEffects()
        {
            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>("Assets/_Project/Configs/Abilities/Mage_Arcane_Missile.asset");
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>("Assets/_Project/VFX/Definitions/Mage_Arcane_Missile_VFX.asset");
            GameObject casting = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Prefabs/ArcaneMissilesVFX.prefab");

            Assert.That(ability.VisualEffects, Is.SameAs(definition));
            Assert.That(ability.IsChanneled, Is.True);
            Assert.That(ability.InterruptOnMovement, Is.True);
            Assert.That(definition.CastingPrefab, Is.SameAs(casting));
            Assert.That(definition.CastPrefab, Is.Null);
            Assert.That(definition.HitPrefab, Is.Null);
            Assert.That(definition.UseHandCastingAnchors, Is.False);
            Assert.That(typeof(IMMOAbilityVfxInstance).IsAssignableFrom(typeof(ArcaneMissilesVFX)), Is.True);
            Assert.That(typeof(IMMOAbilityVfxReleaseHandler).IsAssignableFrom(typeof(ArcaneMissilesVFX)), Is.True);
            Assert.That(typeof(IMMOAbilityVfxPoolReset).IsAssignableFrom(typeof(ArcaneMissileProjectileVFX)), Is.True);
        }

        [Test]
        public void DetachedParticles_UseWorldSimulationSpace()
        {
            foreach (string prefabName in new[] { "ArcaneMissilesVFX", "ArcaneMissileProjectileVFX", "ArcaneMissilesImpactVFX", "ArcaneMissilesInterruptVFX" })
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Prefabs/{prefabName}.prefab");
                foreach (ParticleSystem particles in prefab.GetComponentsInChildren<ParticleSystem>(true))
                {
                    Assert.That(particles.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World), $"{prefabName}/{particles.name}");
                }
            }
        }

        [Test]
        public void ReplicatedDamagePath_ExposesAbilityScopedImpactEvent()
        {
            GameObject sourceObject = new("Arcane Missiles Replicated Source");
            GameObject targetObject = new("Arcane Missiles Replicated Target");
            try
            {
                MMOCombatant source = sourceObject.AddComponent<MMOCombatant>();
                MMOCombatant target = targetObject.AddComponent<MMOCombatant>();
                MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>("Assets/_Project/Configs/Abilities/Mage_Arcane_Missile.asset");
                int observedTicks = 0;
                target.Damaged += (eventSource, eventTarget, eventAbility, amount) =>
                {
                    Assert.That(eventSource, Is.SameAs(source));
                    Assert.That(eventTarget, Is.SameAs(target));
                    Assert.That(eventAbility, Is.SameAs(ability));
                    observedTicks++;
                };

                target.ApplyResolvedDamage(source, ability, 1, false, false);

                Assert.That(observedTicks, Is.EqualTo(1), "Remote DamageResolved playback must expose the same impact event consumed by ArcaneMissilesVFX.");
            }
            finally
            {
                Object.DestroyImmediate(targetObject);
                Object.DestroyImmediate(sourceObject);
            }
        }
    }
}
#endif
