using System.Reflection;
using NUnit.Framework;
using RPGClone.Abilities;
using RPGClone.Characters;
using RPGClone.Combat;
using RPGClone.Vfx;
using RPGClone.Vfx.Physical;
using UnityEditor;
using UnityEngine;

namespace RPGClone.EditorTests
{
    public sealed class GougeVFXTests
    {
        private const string Root = "Assets/_Project/VFX/Gouge";
        private const string AbilityPath = "Assets/_Project/Configs/Abilities/Warrior_Gouge.asset";
        private const string DefinitionPath = "Assets/_Project/VFX/Definitions/Warrior_Gouge_VFX.asset";

        [Test]
        public void GougeAbilityUsesPooledZeroDelayPresentationPrefabs()
        {
            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AbilityPath);
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(DefinitionPath);
            GameObject castPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/GougeCastVFX.prefab");
            GameObject hitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/GougeVFX.prefab");

            Assert.That(ability, Is.Not.Null);
            Assert.That(definition, Is.Not.Null);
            Assert.That(ability.VisualEffects, Is.SameAs(definition));
            Assert.That(definition.CastPrefab, Is.SameAs(castPrefab));
            Assert.That(definition.HitPrefab, Is.SameAs(hitPrefab));
            Assert.That(definition.AttachHitToTarget, Is.True);
            Assert.That(definition.HitDelaySeconds, Is.Zero, "The listener must exist before the same-frame critical result arrives.");
            Assert.That(castPrefab.GetComponent<GougeCastVFX>(), Is.Not.Null);
            Assert.That(castPrefab.GetComponent<MMOAbilityVfxPoolable>(), Is.Not.Null);
            Assert.That(hitPrefab.GetComponent<GougeVFX>(), Is.Not.Null);
            Assert.That(hitPrefab.GetComponent<MMOAbilityVfxPoolable>(), Is.Not.Null);
        }

        [Test]
        public void GougePrefabContainsEveryReusableVisualSection()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/GougeVFX.prefab");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.transform.Find("Target Attached Layers/GougeImpactVFX"), Is.Not.Null);
            Assert.That(prefab.transform.Find("Target Attached Layers/GougeBleedVFX"), Is.Not.Null);
            Assert.That(prefab.transform.Find("Target Attached Layers/GougeBleedTickVFX"), Is.Not.Null);
            Assert.That(prefab.transform.Find("Target Attached Layers/GougeStackIncreaseVFX"), Is.Not.Null);
            Assert.That(prefab.transform.Find("Target Attached Layers/GougeCriticalResetVFX"), Is.Not.Null);
            Assert.That(prefab.transform.Find("Target Attached Layers/GougeExpirationVFX"), Is.Not.Null);
            Assert.That(prefab.transform.Find("Ground Force Reaction"), Is.Not.Null);
        }

        [Test]
        public void ProfileScalesReadabilityAcrossThreeStacks()
        {
            GougeVFXProfile profile = AssetDatabase.LoadAssetAtPath<GougeVFXProfile>(Root + "/Profiles/GougeVFX_Default.asset");
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.BleedDuration, Is.EqualTo(9f).Within(0.01f));
            Assert.That(profile.StackWoundScale(2), Is.GreaterThan(profile.StackWoundScale(1)));
            Assert.That(profile.StackWoundScale(3), Is.GreaterThan(profile.StackWoundScale(2)));
            Assert.That(profile.StackTickIntensity(3), Is.GreaterThan(profile.StackTickIntensity(1)));
            Assert.That(profile.StackMist(3), Is.GreaterThan(profile.StackMist(1)));
        }

        [Test]
        public void GeneratedTexturesPreserveAlphaAndClampEdges()
        {
            string[] paths =
            {
                Root + "/Textures/Gouge_Wound_Atlas_v2.png",
                Root + "/Textures/Gouge_Weapon_Trail.png",
                Root + "/Textures/Gouge_Tearing_Trail.png",
                Root + "/Textures/Gouge_Contact_Flash_Atlas_v2.png",
                Root + "/Textures/Gouge_Blood_Atlas_v2.png",
                Root + "/Textures/Gouge_Debris_Spark_Atlas.png",
                Root + "/Textures/Gouge_Critical_Reset_Ring.png",
                Root + "/Textures/Gouge_Wound_Mist_Atlas.png"
            };

            foreach (string path in paths)
            {
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.That(importer, Is.Not.Null, path);
                Assert.That(importer.alphaSource, Is.EqualTo(TextureImporterAlphaSource.FromInput), path);
                Assert.That(importer.alphaIsTransparency, Is.True, path);
                Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp), path);
            }
        }

        [Test]
        public void GougePrefabsUseTheExistingPhysicalParticleWorkflow()
        {
            string[] paths =
            {
                Root + "/Prefabs/GougeCastVFX.prefab",
                Root + "/Prefabs/GougeVFX.prefab"
            };

            foreach (string path in paths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, path);
                Assert.That(prefab.GetComponentsInChildren<MeshRenderer>(true), Is.Empty,
                    "Physical ability VFX should use the same ParticleSystem workflow as Bash and Charge.");
                ParticleSystemRenderer[] particleRenderers = prefab.GetComponentsInChildren<ParticleSystemRenderer>(true);
                Assert.That(particleRenderers, Is.Not.Empty);
                foreach (ParticleSystemRenderer renderer in particleRenderers)
                {
                    Assert.That(renderer.sharedMaterial, Is.Not.Null, renderer.name);
                    bool chargeEnvironment = renderer.name == "Charge Ground Burst"
                        || renderer.name == "Environmental Heavy Dust"
                        || renderer.name == "Environmental Fine Dust"
                        || renderer.name == "Ground Debris";
                    Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo(chargeEnvironment
                        ? "RPG Clone/VFX/Charge Sprite Unlit"
                        : "RPG Clone/VFX/Gouge Sprite Unlit"), renderer.name);
                    Assert.That(renderer.sharedMaterial.GetTexture("_BaseMap"), Is.Not.Null, renderer.name);
                }
            }
        }

        [Test]
        public void PersistentWoundsSelectSinglePaddedAtlasCellsOnTheResolvedSurface()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/GougeVFX.prefab");
            Assert.That(prefab, Is.Not.Null);
            for (int state = 1; state <= 3; state++)
            {
                Transform wound = prefab.transform.Find($"Target Attached Layers/GougeBleedVFX/Wound State {state}/Dark Wound Base");
                Assert.That(wound, Is.Not.Null);
                ParticleSystem particles = wound.GetComponent<ParticleSystem>();
                ParticleSystem.TextureSheetAnimationModule sheet = particles.textureSheetAnimation;
                Assert.That(sheet.enabled, Is.True);
                Assert.That(sheet.numTilesX, Is.EqualTo(2));
                Assert.That(sheet.numTilesY, Is.EqualTo(2));
                Assert.That(sheet.frameOverTime.constant, Is.Zero,
                    "Persistent wounds must select one atlas cell rather than animating or projecting the whole sheet.");
                Assert.That(particles.GetComponent<ParticleSystemRenderer>().alignment, Is.EqualTo(ParticleSystemRenderSpace.View),
                    "Persistent wounds must use the same view alignment as Fireball's particle billboards.");
            }
        }

        [Test]
        public void FreshInitializationMakesParticlesAndPersistentWoundVisible()
        {
            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AbilityPath);
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(DefinitionPath);
            GougeVFXProfile profile = AssetDatabase.LoadAssetAtPath<GougeVFXProfile>(Root + "/Profiles/GougeVFX_Default.asset");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/GougeVFX.prefab");
            GameObject source = new("Fresh Gouge Source");
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            GameObject cameraObject = new("Gouge Test Camera");
            GameObject instance = Object.Instantiate(prefab, target.transform);
            try
            {
                source.transform.position = new Vector3(0f, 1f, -2f);
                Camera camera = cameraObject.AddComponent<Camera>();
                cameraObject.transform.position = new Vector3(0f, 1f, -5f);
                target.AddComponent<MMOCharacterIdentity>();
                target.AddComponent<MMOCombatant>();
                Vector3 requestedHitPosition = target.transform.TransformPoint(new Vector3(0f, 0.35f, 0f));
                MMOAbilityVfxContext context = new(
                    null,
                    ability,
                    definition,
                    source.transform,
                    target.transform,
                    source.transform.position,
                    requestedHitPosition,
                    false,
                    null);

                GougeVFX effect = instance.GetComponent<GougeVFX>();
                effect.Initialize(context);
                typeof(GougeVFX).GetField("cachedCamera", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(effect, camera);
                MethodInfo lateUpdate = typeof(GougeVFX).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
                lateUpdate?.Invoke(effect, null);

                Transform woundTransform = instance.transform.Find("Target Attached Layers/GougeBleedVFX/Wound State 1/Dark Wound Base");
                Assert.That(woundTransform, Is.Not.Null);
                ParticleSystem wound = woundTransform.GetComponent<ParticleSystem>();
                ParticleSystemRenderer woundRenderer = woundTransform.GetComponent<ParticleSystemRenderer>();
                Assert.That(wound.particleCount, Is.GreaterThan(0), "A fresh Gouge activation must emit its persistent wound immediately.");
                ParticleSystem.Particle[] particles = new ParticleSystem.Particle[1];
                Assert.That(wound.GetParticles(particles), Is.EqualTo(1));
                Assert.That(particles[0].remainingLifetime, Is.GreaterThanOrEqualTo(profile.BleedDuration),
                    "The wound must remain visible for the gameplay bleed instead of behaving like an impact-only burst.");
                Assert.That(woundRenderer.enabled, Is.True);
                Assert.That(woundRenderer.alignment, Is.EqualTo(ParticleSystemRenderSpace.View),
                    "The target-attached wound must use Fireball's view-aligned billboard convention.");
                Assert.That(woundRenderer.sharedMaterial.GetTexture("_BaseMap"), Is.Not.Null);
                Transform attached = instance.transform.Find("Target Attached Layers");
                Assert.That(attached.position.z, Is.LessThan(target.transform.position.z),
                    "The wound must move to the camera-facing side of the target.");
                Assert.That(attached.position.y,
                    Is.EqualTo(requestedHitPosition.y).Within(0.08f),
                    "The wound must retain the supplied hit height instead of snapping to an arbitrary renderer-bounds center.");
                cameraObject.transform.position = new Vector3(5f, 1f, 0f);
                lateUpdate?.Invoke(effect, null);
                Assert.That(attached.position.x, Is.GreaterThan(target.transform.position.x + 0.2f),
                    "The attached wound must follow an oblique camera around the target instead of remaining hidden on the original side.");
                Vector3 expectedFacing = (cameraObject.transform.position - attached.position).normalized;
                Assert.That(Vector3.Dot(attached.forward, expectedFacing), Is.GreaterThan(0.99f),
                    "The wound root must use Fireball's explicit camera-facing LookRotation behavior.");
                Transform ground = instance.transform.Find("Ground Force Reaction");
                Assert.That(ground.position.y, Is.EqualTo(target.GetComponent<Collider>().bounds.min.y + 0.025f).Within(0.04f),
                    "The physical kick-up must originate at the target's feet, not from the torso wound anchor.");
                ParticleSystem heavyDust = ground.Find("Environmental Heavy Dust").GetComponent<ParticleSystem>();
                ParticleSystem fineDust = ground.Find("Environmental Fine Dust").GetComponent<ParticleSystem>();
                Assert.That(heavyDust.isPlaying, Is.True, "A fresh Gouge impact must emit Charge's heavy environmental dust.");
                Assert.That(fineDust.isPlaying, Is.True, "A fresh Gouge impact must emit Charge's fine environmental dust.");
                Assert.That(heavyDust.GetComponent<ParticleSystemRenderer>().sharedMaterial.name, Is.EqualTo("Charge_HeavyDust"));
                Assert.That(fineDust.GetComponent<ParticleSystemRenderer>().sharedMaterial.name, Is.EqualTo("Charge_FineDust"));
                Assert.That(heavyDust.shape.shapeType, Is.EqualTo(ParticleSystemShapeType.Hemisphere));
                Assert.That(heavyDust.emission.GetBurst(0).count.constant, Is.GreaterThanOrEqualTo(30));
            }
            finally
            {
                Object.DestroyImmediate(instance);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void ReceiverLocalRelayRetainsReplicatedCriticalResultWithoutRemoteClockMath()
        {
            GameObject sourceObject = new("Gouge Relay Source");
            GameObject targetObject = new("Gouge Relay Target");
            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AbilityPath);
            try
            {
                sourceObject.AddComponent<MMOCharacterIdentity>();
                targetObject.AddComponent<MMOCharacterIdentity>();
                MMOCombatant source = sourceObject.AddComponent<MMOCombatant>();
                MMOCombatant target = targetObject.AddComponent<MMOCombatant>();
                CombatEventRecord record = CombatEventRecord.Create(CombatEventType.DamageResolved);
                record.abilityId = GougeVFXEventRelay.AbilityId;
                record.damageAmount = 10;
                record.isCritical = true;

                GougeVFXEventRelay.TryGetRecent(target, 0f, out _);
                MMOCombatEventStream.PublishCombatEvent(record, source, target, ability);

                Assert.That(GougeVFXEventRelay.TryGetRecent(target, 1f, out GougeVFXEventRelay.DamagePresentation recent), Is.True);
                Assert.That(recent.Record.isCritical, Is.True);
                Assert.That(recent.Target, Is.SameAs(target));
                Assert.That(recent.ReceivedAt, Is.GreaterThanOrEqualTo(0f));
            }
            finally
            {
                Object.DestroyImmediate(sourceObject);
                Object.DestroyImmediate(targetObject);
            }
        }
    }
}
