#if UNITY_EDITOR
using System.Reflection;
using NUnit.Framework;
using RPGClone.Abilities;
using RPGClone.Combat;
using RPGClone.Vfx;
using RPGClone.Vfx.Fire;
using UnityEditor;
using UnityEngine;

namespace RPGClone.EditorTests
{
    public sealed class FlamestrikeVFXTests
    {
        private const string AbilityPath = "Assets/_Project/Configs/Abilities/Mage_Flamestrike.asset";
        private const string DefinitionPath = "Assets/_Project/VFX/Definitions/Mage_Flamestrike_VFX.asset";
        private const string TargetingPath = "Assets/_Project/VFX/Flamestrike/Prefabs/FlamestrikeTargetingVFX.prefab";
        private const string CastingPath = "Assets/_Project/VFX/Flamestrike/Prefabs/FlamestrikeCastVFX.prefab";
        private const string HitPath = "Assets/_Project/VFX/Flamestrike/Prefabs/FlamestrikeVFX.prefab";
        private const string TubeFlowMaskPath = "Assets/_Project/VFX/Flamestrike/Textures/Flamestrike_TubeFlowMask.png";

        [Test]
        public void Package_IsWiredForWorldSpaceMultiplayerPresentation()
        {
            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AbilityPath);
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(DefinitionPath);
            GameObject targeting = AssetDatabase.LoadAssetAtPath<GameObject>(TargetingPath);
            GameObject casting = AssetDatabase.LoadAssetAtPath<GameObject>(CastingPath);
            GameObject hit = AssetDatabase.LoadAssetAtPath<GameObject>(HitPath);

            Assert.That(ability, Is.Not.Null);
            Assert.That(definition, Is.Not.Null);
            Assert.That(ability.VisualEffects, Is.SameAs(definition));
            Assert.That(definition.TargetingPrefab, Is.SameAs(targeting));
            Assert.That(definition.CastingPrefab, Is.SameAs(casting));
            Assert.That(definition.HitPrefab, Is.SameAs(hit));
            Assert.That(definition.CastPrefab, Is.Null);
            Assert.That(definition.AttachHitToTarget, Is.False, "The burning field must remain at the selected world position.");
            Assert.That(definition.UseHandCastingAnchors, Is.False, "One composed cast controller owns both hand anchors and the target buildup.");
            Assert.That(targeting.GetComponent<FlamestrikeTargetingVFX>(), Is.Not.Null);
            Assert.That(casting.GetComponent<FlamestrikeCastVFX>(), Is.Not.Null);
            Assert.That(hit.GetComponent<FlamestrikeVFX>(), Is.Not.Null);
        }

        [Test]
        public void Package_UsesCircularAndVolumetricMeshes_WithSharedChargeDust()
        {
            GameObject targeting = AssetDatabase.LoadAssetAtPath<GameObject>(TargetingPath);
            GameObject hit = AssetDatabase.LoadAssetAtPath<GameObject>(HitPath);

            Assert.That(targeting.GetComponentsInChildren<MeshFilter>(true), Has.Length.GreaterThanOrEqualTo(3));
            Assert.That(System.Array.Exists(targeting.GetComponentsInChildren<MeshFilter>(true), filter => filter.sharedMesh != null && filter.sharedMesh.name == "Quad"), Is.False,
                "The ground-targeting indicator must not expose square plane edges.");

            Transform mainColumn = hit.transform.Find("Initial Impact/Main Vertical Fire Column");
            Assert.That(mainColumn, Is.Not.Null);
            FlamestrikeTubeShellVFX[] impactShells = mainColumn.GetComponentsInChildren<FlamestrikeTubeShellVFX>(true);
            Assert.That(impactShells, Has.Length.EqualTo(4), "The dominant pillar must use only a few large centered shells.");
            foreach (FlamestrikeTubeShellVFX shell in impactShells)
            {
                Assert.That(shell.ShellRenderer.sharedMaterial.shader.name, Is.EqualTo("RPG Clone/VFX/Flamestrike Procedural Tube"));
                Assert.That(new Vector2(shell.AuthoredPosition.x, shell.AuthoredPosition.z).magnitude, Is.LessThan(0.2f));
                Assert.That(shell.Loops, Is.False, "Tube visibility must never bob or restart.");
            }
            Transform lingeringVortex = hit.transform.Find("Persistent Burning Ground/Lingering Fire Vortex");
            Assert.That(lingeringVortex, Is.Not.Null);
            FlamestrikeTubeShellVFX[] lingeringShells = lingeringVortex.GetComponentsInChildren<FlamestrikeTubeShellVFX>(true);
            Assert.That(lingeringShells, Has.Length.EqualTo(4));
            foreach (FlamestrikeTubeShellVFX shell in lingeringShells)
            {
                Assert.That(new Vector2(shell.AuthoredPosition.x, shell.AuthoredPosition.z).magnitude, Is.LessThan(0.5f));
                Assert.That(shell.Loops, Is.False);
            }
            FlamestrikeExpandingRingVFX[] rings = hit.GetComponentsInChildren<FlamestrikeExpandingRingVFX>(true);
            Assert.That(rings, Has.Length.EqualTo(3));
            System.Array.Sort(rings, (left, right) => left.StartDelay.CompareTo(right.StartDelay));
            for (int i = 0; i < rings.Length; i++)
            {
                Assert.That(rings[i].EndDiameter, Is.EqualTo(10f).Within(0.01f), "Every vortex ring must reach the burning-ground perimeter.");
                Assert.That(rings[i].HeightAtPerimeter, Is.Zero, "Vortex shells must collapse completely as their radius expands.");
                Assert.That(rings[i].RingRenderer.GetComponent<MeshFilter>().sharedMesh.name, Is.EqualTo("Flamestrike_TubeShell"), "The collapsing layer must have visible vertical height, not torus thickness.");
                if (i > 0) Assert.That(rings[i].StartDelay, Is.GreaterThan(rings[i - 1].StartDelay));
            }
            GameObject animatedRingObject = Object.Instantiate(rings[0].gameObject);
            try
            {
                FlamestrikeExpandingRingVFX animatedRing = animatedRingObject.GetComponent<FlamestrikeExpandingRingVFX>();
                animatedRing.Animate(4f, 0.5f, 1f, Color.white);
                Assert.That(animatedRing.transform.localScale.x, Is.GreaterThan(animatedRing.AuthoredScale.x));
                Assert.That(animatedRing.transform.localScale.x, Is.LessThan(animatedRing.EndDiameter));
                Assert.That(animatedRing.transform.localScale.y, Is.InRange(0.001f, animatedRing.AuthoredScale.y - 0.001f));
                Assert.That(animatedRing.CurrentOpacity, Is.InRange(0.001f, 0.999f));
                animatedRing.Animate(8f, 1f, 1f, Color.white);
                Assert.That(animatedRing.transform.localScale.x, Is.EqualTo(10f).Within(0.01f));
                Assert.That(animatedRing.transform.localScale.y, Is.Zero.Within(0.0001f));
                Assert.That(animatedRing.CurrentOpacity, Is.Zero.Within(0.0001f));
                Assert.That(animatedRing.RingRenderer.enabled, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(animatedRingObject);
            }
            ParticleSystem smokeCrown = hit.transform.Find("Initial Impact/Smoke Crown").GetComponent<ParticleSystem>();
            Assert.That(smokeCrown.main.loop, Is.True, "The smoke crown must continue for the field duration.");
            Assert.That(smokeCrown.emission.rateOverTime.constant, Is.GreaterThan(0f));
            Assert.That(System.Array.Exists(hit.GetComponentsInChildren<MeshFilter>(true), filter => filter.sharedMesh != null && filter.sharedMesh.name == "Quad"), Is.False,
                "The hit effect must not contain static atlas cards.");

            TextureImporter flowMaskImporter = AssetImporter.GetAtPath(TubeFlowMaskPath) as TextureImporter;
            Assert.That(flowMaskImporter, Is.Not.Null);
            Assert.That(flowMaskImporter.wrapMode, Is.EqualTo(TextureWrapMode.Mirror), "The animated flow mask must cross tile boundaries without a visible seam.");

            AssertDustMaterial(hit.transform.Find("Initial Impact/Smoke Crown"), "Charge_HeavyDust");
            AssertDustMaterial(hit.transform.Find("Persistent Burning Ground/Rising Smoke"), "Charge_HeavyDust");
            AssertDustMaterial(hit.transform.Find("Persistent Burning Ground/Ash Haze"), "Charge_FineDust");
        }

        [Test]
        public void ReplicatedDamageEvents_GroupAreaPulseAndDeduplicateTargetReaction()
        {
            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AbilityPath);
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(DefinitionPath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HitPath);
            GameObject sourceObject = new("Flamestrike Test Source");
            GameObject targetObject = new("Flamestrike Test Target");
            GameObject instance = null;
            try
            {
                MMOCombatant source = sourceObject.AddComponent<MMOCombatant>();
                MMOCombatant target = targetObject.AddComponent<MMOCombatant>();
                targetObject.transform.position = Vector3.right;
                instance = Object.Instantiate(prefab);
                FlamestrikeVFX vfx = instance.GetComponent<FlamestrikeVFX>();
                vfx.Initialize(new MMOAbilityVfxContext(null, ability, definition, sourceObject.transform, null, Vector3.zero, Vector3.zero, true, null));

                FieldInfo startedAt = typeof(FlamestrikeVFX).GetField("startedAt", BindingFlags.Instance | BindingFlags.NonPublic);
                startedAt.SetValue(vfx, Time.time - 2f);
                CombatEventRecord record = CombatEventRecord.Create(CombatEventType.DamageResolved);
                record.abilityId = ability.AbilityId;
                record.damageAmount = 1;
                MMOCombatEventStream.PublishCombatEvent(record, source, target, ability);
                MMOCombatEventStream.PublishCombatEvent(record, source, target, ability);

                FieldInfo pulseIndex = typeof(FlamestrikeVFX).GetField("pulseIndex", BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo reactionPool = typeof(FlamestrikeVFX).GetField("targetReactionPool", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That((int)pulseIndex.GetValue(vfx), Is.EqualTo(1), "Same-tick multi-record damage must create one area pulse.");
                FlamestrikeTargetReactionVFX[] reactions = (FlamestrikeTargetReactionVFX[])reactionPool.GetValue(vfx);
                Assert.That(System.Array.FindAll(reactions, reaction => reaction != null && reaction.IsPlaying), Has.Length.EqualTo(1));
            }
            finally
            {
                if (instance != null) Object.DestroyImmediate(instance);
                Object.DestroyImmediate(targetObject);
                Object.DestroyImmediate(sourceObject);
            }
        }

        [Test]
        public void ReplicatedCast_PreservesGroundTargetForRemoteBuildup()
        {
            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AbilityPath);
            GameObject caster = new("Replicated Flamestrike Caster");
            try
            {
                MMOAbilitySystem system = caster.AddComponent<MMOAbilitySystem>();
                Vector3 targetPosition = new(12.5f, 0.25f, -7f);
                system.PlayReplicatedCastStarted(ability, null, 2f, targetPosition, true);

                Assert.That(system.CurrentCastHasGroundTarget, Is.True);
                Assert.That(system.CurrentCastGroundTargetPosition, Is.EqualTo(targetPosition));
                Assert.That(system.CurrentCastNormalized, Is.InRange(0f, 1f));
                system.PlayReplicatedCastInterrupted(ability, null, "Test complete.");
            }
            finally
            {
                Object.DestroyImmediate(caster);
            }
        }

        private static void AssertDustMaterial(Transform transform, string expectedMaterialName)
        {
            Assert.That(transform, Is.Not.Null);
            ParticleSystemRenderer renderer = transform.GetComponent<ParticleSystemRenderer>();
            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.sharedMaterial, Is.Not.Null);
            Assert.That(renderer.sharedMaterial.name, Is.EqualTo(expectedMaterialName));
            ParticleSystem.TextureSheetAnimationModule sheet = transform.GetComponent<ParticleSystem>().textureSheetAnimation;
            Assert.That(sheet.enabled, Is.True);
            Assert.That(sheet.numTilesX, Is.EqualTo(4));
            Assert.That(sheet.numTilesY, Is.EqualTo(2));
        }
    }
}
#endif
