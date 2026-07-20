using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using RPGClone.CharacterSelection;
using RPGClone.Characters;
using RPGClone.Inventory;
using RPGClone.Multiplayer;
using RPGClone.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RPGClone.EditorTests
{
    public sealed class MMOCharacterAppearanceTests
    {
        [Test]
        public void AppearanceCatalog_NormalizesMissingAndUnknownStylesToFirstEntry()
        {
            MMOCharacterAppearanceCatalog catalog = ScriptableObject.CreateInstance<MMOCharacterAppearanceCatalog>();
            try
            {
                MMOHeadStyleDefinition head = new();
                head.Configure("head_1", "Default Head", null);
                MMOFaceDefinition firstFace = new();
                firstFace.Configure("face_1", "Face 1", null);
                MMOFaceDefinition secondFace = new();
                secondFace.Configure("face_2", "Face 2", null);
                MMOHairstyleDefinition first = new();
                first.Configure("hair_1", "Hairstyle 1", null);
                MMOHairstyleDefinition second = new();
                second.Configure("hair_2", "Hairstyle 2", null);
                MMOHairColorDefinition black = new();
                black.Configure("hair_black", "Black", Color.black);
                MMOHairColorDefinition brown = new();
                brown.Configure("hair_brown", "Brown", new Color(0.4f, 0.2f, 0.1f));
                catalog.Configure(
                    new[] { head },
                    new[] { firstFace, secondFace },
                    new[] { first, second },
                    new[] { black, brown });

                Assert.That(catalog.NormalizeHeadStyleId(string.Empty), Is.EqualTo("head_1"));
                Assert.That(catalog.NormalizeHeadStyleId("unknown"), Is.EqualTo("head_1"));
                Assert.That(catalog.NormalizeFaceId(string.Empty), Is.EqualTo("face_1"));
                Assert.That(catalog.NormalizeFaceId("unknown"), Is.EqualTo("face_1"));
                Assert.That(catalog.NormalizeFaceId("face_2"), Is.EqualTo("face_2"));
                Assert.That(catalog.NormalizeHairstyleId(string.Empty), Is.EqualTo("hair_1"));
                Assert.That(catalog.NormalizeHairstyleId("unknown"), Is.EqualTo("hair_1"));
                Assert.That(catalog.NormalizeHairstyleId("hair_2"), Is.EqualTo("hair_2"));
                Assert.That(catalog.NormalizeHairColorId(string.Empty), Is.EqualTo("hair_black"));
                Assert.That(catalog.NormalizeHairColorId("unknown"), Is.EqualTo("hair_black"));
                Assert.That(catalog.NormalizeHairColorId("hair_brown"), Is.EqualTo("hair_brown"));
            }
            finally
            {
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void NewCharacterSaveData_UsesAppearanceMigrationDefaults()
        {
            MMOCharacterSaveData saveData = new();

            Assert.That(saveData.headStyleId, Is.EqualTo("head_1"));
            Assert.That(saveData.faceId, Is.EqualTo("face_1"));
            Assert.That(saveData.hairstyleId, Is.EqualTo("hair_1"));
            Assert.That(saveData.hairColorId, Is.EqualTo("hair_black"));
        }

        [Test]
        public void CharacterSurface_UsesUnlitTextureAndGuaranteesShadowCasting()
        {
            Material sourceMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/PlayerWeaponStow/Material.003 1.mat");
            Assert.That(sourceMaterial, Is.Not.Null);
            Assert.That(sourceMaterial.mainTexture, Is.Not.Null);

            GameObject root = new("Body Part Lighting Policy Test Root");
            try
            {
                MMOPlayerEquipmentVisuals equipmentVisuals = root.AddComponent<MMOPlayerEquipmentVisuals>();
                MeshRenderer renderer = root.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = sourceMaterial;
                renderer.receiveShadows = true;
                renderer.shadowCastingMode = ShadowCastingMode.TwoSided;

                equipmentVisuals.ApplyCharacterSurface(renderer);

                Assert.That(renderer.receiveShadows, Is.False);
                Assert.That(renderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.TwoSided));
                Assert.That(renderer.sharedMaterial, Is.Not.SameAs(sourceMaterial));
                Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo(MMOCharacterUnlitMaterialUtility.UnlitShaderName));
                Assert.That(renderer.sharedMaterial.mainTexture, Is.SameAs(sourceMaterial.mainTexture));
                Assert.That(
                    Vector4.Distance(renderer.sharedMaterial.color, sourceMaterial.color),
                    Is.LessThan(0.0001f));

                renderer.shadowCastingMode = ShadowCastingMode.Off;
                equipmentVisuals.ApplyCharacterSurface(renderer);
                Assert.That(renderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.On));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SharedSessionClone_PreservesAppearanceForRemotePlayers()
        {
            MMOCharacterSaveData source = new()
            {
                headStyleId = "head_custom",
                faceId = "face_custom",
                hairstyleId = "hair_custom",
                hairColorId = "hair_color_custom"
            };
            MethodInfo cloneMethod = typeof(MMOSharedSessionState).GetMethod(
                "CloneCharacter",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(cloneMethod, Is.Not.Null);
            MMOCharacterSaveData clone = cloneMethod.Invoke(null, new object[] { source }) as MMOCharacterSaveData;

            Assert.That(clone, Is.Not.Null);
            Assert.That(clone.headStyleId, Is.EqualTo("head_custom"));
            Assert.That(clone.faceId, Is.EqualTo("face_custom"));
            Assert.That(clone.hairstyleId, Is.EqualTo("hair_custom"));
            Assert.That(clone.hairColorId, Is.EqualTo("hair_color_custom"));
        }

        [Test]
        public void AppearanceRebuild_RemovesUntrackedGeneratedVisualsInheritedByRuntimeClone()
        {
            GameObject actor = new("Runtime Appearance Clone Test");
            try
            {
                MMOCharacterAppearanceVisuals appearance = actor.AddComponent<MMOCharacterAppearanceVisuals>();
                GameObject inheritedHairstyle = new("Inherited Local Hairstyle");
                inheritedHairstyle.transform.SetParent(actor.transform, false);
                inheritedHairstyle.AddComponent<MMOAppearanceVisualInstanceMarker>();

                appearance.Configure(null, "head_remote", "face_remote", "hair_remote", "hair_color_remote");

                Assert.That(
                    actor.GetComponentsInChildren<MMOAppearanceVisualInstanceMarker>(true),
                    Is.Empty,
                    "A remote clone must not retain generated appearance instances inherited from the local player.");
            }
            finally
            {
                Object.DestroyImmediate(actor);
            }
        }

        [Test]
        public void ProductionAppearanceCatalog_ContainsMasksAndCuratedHairColors()
        {
            MMOCharacterAppearanceCatalog catalog = AssetDatabase.LoadAssetAtPath<MMOCharacterAppearanceCatalog>(
                "Assets/Resources/RPGClone/Character_Appearance_Catalog.asset");

            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.Hairstyles, Has.Count.EqualTo(6));
            Assert.That(catalog.HairColors, Has.Count.EqualTo(10));
            Assert.That(catalog.Hairstyles, Has.All.Matches<MMOHairstyleDefinition>(style => style.ColorMask != null));
            Assert.That(catalog.HairColors, Has.All.Matches<MMOHairColorDefinition>(color => !string.IsNullOrWhiteSpace(color.HairColorId)));
        }

        [Test]
        public void CreationPreviewEquipment_DoesNotModifyStartingEquipment()
        {
            MMOCharacterArchetypeDefinition archetype = ScriptableObject.CreateInstance<MMOCharacterArchetypeDefinition>();
            try
            {
                archetype.ConfigureStartingItems(null, null, null);
                archetype.ConfigureCreationPreview(new RPGClone.Inventory.MMOItemDefinition[] { null });

                Assert.That(archetype.StartingEquipment, Is.Empty);
                Assert.That(archetype.CreationPreviewEquipment, Has.Count.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(archetype);
            }
        }

        [Test]
        public void HairstyleModels_BindToProductionPlayerSkeleton()
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/PlayerCapsule.prefab");
            MMOCharacterAppearanceCatalog catalog = AssetDatabase.LoadAssetAtPath<MMOCharacterAppearanceCatalog>(
                "Assets/Resources/RPGClone/Character_Appearance_Catalog.asset");
            Assert.That(playerPrefab, Is.Not.Null);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.HeadStyles, Has.Count.EqualTo(1));
            Assert.That(catalog.Hairstyles, Has.Count.EqualTo(6));

            GameObject root = new("Character Preview Test Root");
            try
            {
                foreach (MMOHairstyleDefinition hairstyle in catalog.Hairstyles)
                {
                    GameObject actor = MMOCharacterPreviewActor.Create(
                        playerPrefab,
                        root.transform,
                        MMOPlayableRace.Orc,
                        MMOPlayableClass.Warrior,
                        null,
                        catalog,
                        hairstyle.HairstyleId);
                    Assert.That(actor, Is.Not.Null, hairstyle.DisplayName);

                    MMOAppearanceVisualInstanceMarker marker = System.Array.Find(
                        actor.GetComponentsInChildren<MMOAppearanceVisualInstanceMarker>(true),
                        candidate => candidate.name == hairstyle.ModelPrefab.name);
                    Assert.That(marker, Is.Not.Null, $"{hairstyle.DisplayName} was not instantiated.");
                    bool hasEnabledSkinnedRenderer = false;
                    foreach (SkinnedMeshRenderer renderer in marker.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        hasEnabledSkinnedRenderer |= renderer.enabled;
                        if (!renderer.enabled)
                        {
                            continue;
                        }

                        MaterialPropertyBlock propertyBlock = new();
                        renderer.GetPropertyBlock(propertyBlock);
                        Assert.That(
                            propertyBlock.GetTexture(Shader.PropertyToID("_BaseMap")),
                            Is.SameAs(hairstyle.ColorMask),
                            $"{hairstyle.DisplayName} did not receive its grayscale color mask.");
                        Assert.That(
                            Vector4.Distance(
                                propertyBlock.GetColor(Shader.PropertyToID("_BaseColor")),
                                catalog.HairColors[0].Color),
                            Is.LessThan(0.0001f),
                            $"{hairstyle.DisplayName} did not receive the selected hair color.");
                    }

                    Assert.That(hasEnabledSkinnedRenderer, Is.True, $"{hairstyle.DisplayName} did not bind to the player skeleton.");
                    Object.DestroyImmediate(actor);
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DefaultHead_BindsToProductionSkeletonAndReplacesOnlyBaseHeadRenderer()
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/PlayerCapsule.prefab");
            MMOCharacterAppearanceCatalog catalog = AssetDatabase.LoadAssetAtPath<MMOCharacterAppearanceCatalog>(
                "Assets/Resources/RPGClone/Character_Appearance_Catalog.asset");
            Assert.That(playerPrefab, Is.Not.Null);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.HeadStyles, Has.Count.EqualTo(1));

            GameObject root = new("Default Head Preview Test Root");
            try
            {
                GameObject actor = MMOCharacterPreviewActor.Create(
                    playerPrefab,
                    root.transform,
                    MMOPlayableRace.Orc,
                    MMOPlayableClass.Warrior,
                    null,
                    catalog,
                    catalog.DefaultHairstyleId,
                    catalog.DefaultHeadStyleId);

                MMOHeadStyleDefinition headStyle = catalog.HeadStyles[0];
                MMOAppearanceVisualInstanceMarker marker = System.Array.Find(
                    actor.GetComponentsInChildren<MMOAppearanceVisualInstanceMarker>(true),
                    candidate => candidate.name == headStyle.ModelPrefab.name);
                Assert.That(marker, Is.Not.Null, "The configured default head was not instantiated.");
                AssertGeneratedSkinnedMeshesUseProductionBones(new[] { marker }, "default head");
                AssertBodyPartLightingPolicy(new[] { marker }, "default head");

                SkinnedMeshRenderer baseHead = System.Array.Find(
                    actor.GetComponentsInChildren<SkinnedMeshRenderer>(true),
                    renderer => renderer.name == "Head.001"
                        && renderer.GetComponentInParent<MMOAppearanceVisualInstanceMarker>() == null);
                Assert.That(baseHead, Is.Not.Null);
                Assert.That(baseHead.enabled, Is.False, "The old base head should be hidden after the replacement binds.");

                Animator productionAnimator = System.Array.Find(
                    actor.GetComponentsInChildren<Animator>(true),
                    animator => animator.GetComponentInParent<MMOAppearanceVisualInstanceMarker>() == null);
                Assert.That(productionAnimator, Is.Not.Null);
                Assert.That(productionAnimator.enabled, Is.True, "The production Animator must remain enabled.");
                Assert.That(
                    productionAnimator.cullingMode,
                    Is.EqualTo(AnimatorCullingMode.AlwaysAnimate),
                    "Runtime-bound appearance meshes require the production skeleton to keep updating when base renderers are hidden.");

                MMOPlayerLocomotionAnimator locomotionAnimator = actor.GetComponent<MMOPlayerLocomotionAnimator>();
                MMOPlayerCombatAnimator combatAnimator = actor.GetComponent<MMOPlayerCombatAnimator>();
                FieldInfo locomotionAnimatorField = typeof(MMOPlayerLocomotionAnimator).GetField(
                    "animator",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo combatAnimatorField = typeof(MMOPlayerCombatAnimator).GetField(
                    "animator",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(locomotionAnimatorField?.GetValue(locomotionAnimator), Is.SameAs(productionAnimator));
                Assert.That(combatAnimatorField?.GetValue(combatAnimator), Is.SameAs(productionAnimator));

                foreach (Animator importedAnimator in marker.GetComponentsInChildren<Animator>(true))
                {
                    Assert.That(importedAnimator.enabled, Is.False, "The imported head Animator must never drive gameplay bones.");
                }

                MMOCharacterAppearanceVisuals appearanceVisuals = actor.GetComponent<MMOCharacterAppearanceVisuals>();
                FieldInfo originalCullingField = typeof(MMOCharacterAppearanceVisuals).GetField(
                    "productionAnimatorOriginalCullingMode",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                AnimatorCullingMode originalCullingMode = (AnimatorCullingMode)originalCullingField.GetValue(appearanceVisuals);
                MethodInfo onDisableMethod = typeof(MMOCharacterAppearanceVisuals).GetMethod(
                    "OnDisable",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                onDisableMethod.Invoke(appearanceVisuals, null);
                Assert.That(
                    productionAnimator.cullingMode,
                    Is.EqualTo(originalCullingMode),
                    "Disabling modular appearance should restore the prefab's authored culling policy.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DefaultHead_UsesAuthoredMaterialAndTexture()
        {
            MMOCharacterAppearanceCatalog catalog = AssetDatabase.LoadAssetAtPath<MMOCharacterAppearanceCatalog>(
                "Assets/Resources/RPGClone/Character_Appearance_Catalog.asset");
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.HeadStyles, Has.Count.EqualTo(1));

            GameObject headModel = catalog.HeadStyles[0].ModelPrefab;
            Assert.That(headModel, Is.Not.Null);
            SkinnedMeshRenderer renderer = headModel.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.sharedMaterial, Is.Not.Null);
            Assert.That(
                AssetDatabase.GetAssetPath(renderer.sharedMaterial),
                Is.EqualTo("Assets/PlayerWeaponStow/Material.003 1.mat"));
            Assert.That(renderer.sharedMaterial.mainTexture, Is.Not.Null);
            Assert.That(
                AssetDatabase.GetAssetPath(renderer.sharedMaterial.mainTexture),
                Is.EqualTo("Assets/PlayerWeaponStow/styledhead.png"));
        }

        [Test]
        public void FaceTextures_AreCataloguedAndAppliedWithoutDuplicatingHeadGeometry()
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/PlayerCapsule.prefab");
            MMOCharacterAppearanceCatalog catalog = AssetDatabase.LoadAssetAtPath<MMOCharacterAppearanceCatalog>(
                "Assets/Resources/RPGClone/Character_Appearance_Catalog.asset");
            Assert.That(playerPrefab, Is.Not.Null);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.Faces, Has.Count.EqualTo(6));

            string[] expectedFacePaths =
            {
                "Assets/PlayerWeaponStow/styledhead.png",
                "Assets/PlayerWeaponStow/styledhead2.png",
                "Assets/PlayerWeaponStow/styledhead3.png",
                "Assets/_Project/New Equipment/Faces/Head4.png",
                "Assets/_Project/New Equipment/Faces/Head5.png",
                "Assets/_Project/New Equipment/Faces/Head6.png"
            };

            GameObject root = new("Face Texture Preview Test Root");
            try
            {
                for (int index = 0; index < catalog.Faces.Count; index++)
                {
                    MMOFaceDefinition face = catalog.Faces[index];
                    Assert.That(face, Is.Not.Null);
                    Assert.That(face.AlbedoTexture, Is.Not.Null, face.DisplayName);
                    Assert.That(
                        AssetDatabase.GetAssetPath(face.AlbedoTexture),
                        Is.EqualTo(expectedFacePaths[index]));

                    GameObject actor = MMOCharacterPreviewActor.Create(
                        playerPrefab,
                        root.transform,
                        MMOPlayableRace.Orc,
                        MMOPlayableClass.Warrior,
                        null,
                        catalog,
                        catalog.DefaultHairstyleId,
                        catalog.DefaultHeadStyleId,
                        face.FaceId);
                    MMOCharacterAppearanceVisuals appearance = actor.GetComponent<MMOCharacterAppearanceVisuals>();
                    Assert.That(appearance.FaceId, Is.EqualTo(face.FaceId));

                    MMOHeadStyleDefinition headStyle = catalog.HeadStyles[0];
                    MMOAppearanceVisualInstanceMarker headMarker = System.Array.Find(
                        actor.GetComponentsInChildren<MMOAppearanceVisualInstanceMarker>(true),
                        candidate => candidate.name == headStyle.ModelPrefab.name);
                    Assert.That(headMarker, Is.Not.Null);
                    SkinnedMeshRenderer renderer = headMarker.GetComponentInChildren<SkinnedMeshRenderer>(true);
                    Assert.That(renderer, Is.Not.Null);
                    MaterialPropertyBlock propertyBlock = new();
                    renderer.GetPropertyBlock(propertyBlock);
                    Assert.That(
                        propertyBlock.GetTexture(Shader.PropertyToID("_BaseMap")),
                        Is.SameAs(face.AlbedoTexture));

                    Object.DestroyImmediate(actor);
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PreviewActor_IsFiftyPercentLargerAndCenteredOnCamera()
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/PlayerCapsule.prefab");
            MMOCharacterAppearanceCatalog catalog = AssetDatabase.LoadAssetAtPath<MMOCharacterAppearanceCatalog>(
                "Assets/Resources/RPGClone/Character_Appearance_Catalog.asset");
            Assert.That(playerPrefab, Is.Not.Null);
            Assert.That(catalog, Is.Not.Null);

            GameObject root = new("Centered Preview Test Root");
            GameObject cameraObject = new("Centered Preview Test Camera");
            try
            {
                cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 1.8f, -8f), Quaternion.Euler(8f, 0f, 0f));
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.fieldOfView = 42f;
                GameObject actor = MMOCharacterPreviewActor.Create(
                    playerPrefab,
                    root.transform,
                    MMOPlayableRace.Orc,
                    MMOPlayableClass.Warrior,
                    null,
                    catalog,
                    catalog.DefaultHairstyleId,
                    catalog.DefaultHeadStyleId,
                    catalog.DefaultFaceId,
                    camera);

                Assert.That(
                    Vector3.Distance(actor.transform.localScale, playerPrefab.transform.localScale * 1.5f),
                    Is.LessThan(0.0001f));

                bool hasBounds = false;
                Bounds characterBounds = default;
                foreach (Renderer renderer in actor.GetComponentsInChildren<Renderer>(true))
                {
                    if (!renderer.enabled
                        || !renderer.gameObject.activeInHierarchy
                        || renderer.GetComponentInParent<MMOEquipmentVisualInstanceMarker>() != null)
                    {
                        continue;
                    }

                    if (!hasBounds)
                    {
                        characterBounds = renderer.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        characterBounds.Encapsulate(renderer.bounds);
                    }
                }

                Assert.That(hasBounds, Is.True);
                Vector3 viewportCenter = camera.WorldToViewportPoint(characterBounds.center);
                Assert.That(viewportCenter.z, Is.GreaterThan(0f));
                Assert.That(Mathf.Abs(viewportCenter.x - 0.5f), Is.LessThan(0.001f));
                Assert.That(Mathf.Abs(viewportCenter.y - 0.5f), Is.LessThan(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void GameplayAppearance_KeepsProductionAnimatorClockAdvancingWithoutPreviewRepair()
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/PlayerCapsule.prefab");
            MMOCharacterAppearanceCatalog catalog = AssetDatabase.LoadAssetAtPath<MMOCharacterAppearanceCatalog>(
                "Assets/Resources/RPGClone/Character_Appearance_Catalog.asset");
            Assert.That(playerPrefab, Is.Not.Null);
            Assert.That(catalog, Is.Not.Null);

            GameObject actor = Object.Instantiate(playerPrefab);
            try
            {
                Animator productionAnimator = actor.GetComponentInChildren<Animator>(true);
                Assert.That(productionAnimator, Is.Not.Null);
                productionAnimator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                MMOCharacterAppearanceVisuals appearanceVisuals = actor.AddComponent<MMOCharacterAppearanceVisuals>();
                appearanceVisuals.Configure(catalog, catalog.DefaultHeadStyleId, catalog.DefaultHairstyleId);

                Assert.That(productionAnimator.enabled, Is.True);
                Assert.That(productionAnimator.speed, Is.EqualTo(1f));
                Assert.That(productionAnimator.cullingMode, Is.EqualTo(AnimatorCullingMode.AlwaysAnimate));

                productionAnimator.Update(0f);
                Assert.That(productionAnimator.isInitialized, Is.True);
                float before = productionAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                productionAnimator.Update(0.25f);
                float after = productionAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime;

                Assert.That(after, Is.GreaterThan(before), "The production Animator remained frozen on its first frame.");
            }
            finally
            {
                Object.DestroyImmediate(actor);
            }
        }

        [Test]
        public void PreviewActor_PreservesBodyBindingsAndKeepsGeneratedMeshesOnProductionSkeleton()
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/PlayerCapsule.prefab");
            MMOItemDefinition[] startingArmor =
            {
                AssetDatabase.LoadAssetAtPath<MMOItemDefinition>("Assets/_Project/Equipment/Armor/Cloth/Threadbare Tunic/Threadbare_Cloth_Tunic.asset"),
                AssetDatabase.LoadAssetAtPath<MMOItemDefinition>("Assets/_Project/Equipment/Armor/Cloth/Threadbare Gloves/Threadbare_Cloth_Gloves.asset"),
                AssetDatabase.LoadAssetAtPath<MMOItemDefinition>("Assets/_Project/Equipment/Armor/Cloth/Threadbare Pants/Threadbare_Cloth_Pants.asset"),
                AssetDatabase.LoadAssetAtPath<MMOItemDefinition>("Assets/_Project/Equipment/Armor/Cloth/Threadbare Shoes/Threadbare_Cloth_Shoes.asset")
            };
            MMOCharacterAppearanceCatalog catalog = AssetDatabase.LoadAssetAtPath<MMOCharacterAppearanceCatalog>(
                "Assets/Resources/RPGClone/Character_Appearance_Catalog.asset");
            Assert.That(playerPrefab, Is.Not.Null);
            Assert.That(startingArmor, Has.None.Null);
            Assert.That(catalog, Is.Not.Null);

            GameObject root = new("Character Preview Binding Test Root");
            try
            {
                GameObject actor = MMOCharacterPreviewActor.Create(
                    playerPrefab,
                    root.transform,
                    MMOPlayableRace.Orc,
                    MMOPlayableClass.Warrior,
                    startingArmor,
                    catalog,
                    "hair_1");
                MMOPlayerEquipmentVisuals equipmentVisuals = actor.GetComponent<MMOPlayerEquipmentVisuals>();
                FieldInfo slotsField = typeof(MMOPlayerEquipmentVisuals).GetField(
                    "bodyPartSlots",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                List<MMOBodyPartRendererSlot> slots = slotsField?.GetValue(equipmentVisuals) as List<MMOBodyPartRendererSlot>;
                MMOBodyPartRendererSlot torso = slots?.Find(slot => slot != null && slot.BodyPart == MMOCharacterBodyPart.Torso);

                Assert.That(torso, Is.Not.Null, "The authored torso renderer binding was lost.");
                foreach (Renderer renderer in torso.Renderers)
                {
                    Assert.That(renderer, Is.Not.Null);
                    Assert.That(renderer.GetComponentInParent<MMOEquipmentVisualInstanceMarker>(), Is.Null);
                    Assert.That(renderer.GetComponentInParent<MMOAppearanceVisualInstanceMarker>(), Is.Null);
                    Assert.That(renderer.receiveShadows, Is.False, "Base body parts should use baked texture shading.");
                    AssertRendererUsesCharacterUnlitMaterials(renderer, "base body part");
                }

                AssertGeneratedSkinnedMeshesUseProductionBones(
                    actor.GetComponentsInChildren<MMOEquipmentVisualInstanceMarker>(true),
                    "shared starting armor");
                AssertBodyPartLightingPolicy(
                    actor.GetComponentsInChildren<MMOEquipmentVisualInstanceMarker>(true),
                    "shared starting armor");
                AssertGeneratedSkinnedMeshesUseProductionBones(
                    actor.GetComponentsInChildren<MMOAppearanceVisualInstanceMarker>(true),
                    "hairstyle");
                AssertBodyPartLightingPolicy(
                    actor.GetComponentsInChildren<MMOAppearanceVisualInstanceMarker>(true),
                    "hairstyle");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PreviewActor_UsesMobilityPrefabForShieldsOnly()
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/PlayerCapsule.prefab");
            MMOItemDefinition sword = AssetDatabase.LoadAssetAtPath<MMOItemDefinition>(
                "Assets/_Project/Equipment/Weapons/One-Handed Swords/Recruit's Shortsword/Recruits_Shortsword.asset");
            MMOItemDefinition shield = AssetDatabase.LoadAssetAtPath<MMOItemDefinition>(
                "Assets/_Project/Equipment/Weapons/Shields/Recruit's Shield/Recruits_Shield.asset");
            Assert.That(playerPrefab, Is.Not.Null);
            Assert.That(sword, Is.Not.Null);
            Assert.That(shield, Is.Not.Null);
            Assert.That(shield.EquipmentVisual.CombatMovementModelPrefab, Is.Not.Null);

            GameObject root = new("Character Preview Shield Test Root");
            try
            {
                GameObject actor = MMOCharacterPreviewActor.Create(
                    playerPrefab,
                    root.transform,
                    MMOPlayableRace.Orc,
                    MMOPlayableClass.Warrior,
                    new[] { sword, shield },
                    null,
                    string.Empty);
                MMOEquipmentVisualInstanceMarker readySword = null;
                MMOEquipmentVisualInstanceMarker mobilityShield = null;
                foreach (MMOEquipmentVisualInstanceMarker marker in actor.GetComponentsInChildren<MMOEquipmentVisualInstanceMarker>(true))
                {
                    if (marker.name == sword.EquipmentVisual.ModelPrefab.name)
                    {
                        readySword = marker;
                    }

                    if (marker.name == shield.EquipmentVisual.CombatMovementModelPrefab.name)
                    {
                        mobilityShield = marker;
                    }
                }

                Assert.That(readySword, Is.Not.Null, "The weapon preview should retain its ready-position prefab.");
                Assert.That(mobilityShield, Is.Not.Null, "The shield preview should use its mobility-positioned prefab.");
                AssertAttachmentSurfacePolicy(readySword, "ready weapon");
                AssertAttachmentSurfacePolicy(mobilityShield, "movement shield");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void AssertGeneratedSkinnedMeshesUseProductionBones<TMarker>(
            IReadOnlyList<TMarker> markers,
            string visualName)
            where TMarker : Component
        {
            bool foundEnabledRenderer = false;
            foreach (TMarker marker in markers)
            {
                foreach (SkinnedMeshRenderer renderer in marker.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (!renderer.enabled)
                    {
                        continue;
                    }

                    foundEnabledRenderer = true;
                    foreach (Transform bone in renderer.bones)
                    {
                        Assert.That(bone, Is.Not.Null, $"{visualName} has an unbound bone.");
                        Assert.That(bone.GetComponentInParent<MMOEquipmentVisualInstanceMarker>(), Is.Null,
                            $"{visualName} bound to a generated equipment skeleton.");
                        Assert.That(bone.GetComponentInParent<MMOAppearanceVisualInstanceMarker>(), Is.Null,
                            $"{visualName} bound to a generated appearance skeleton.");
                    }
                }
            }

            Assert.That(foundEnabledRenderer, Is.True, $"No enabled skinned renderer was found for {visualName}.");
        }

        private static void AssertBodyPartLightingPolicy<TMarker>(
            IReadOnlyList<TMarker> markers,
            string visualName)
            where TMarker : Component
        {
            bool foundEnabledRenderer = false;
            foreach (TMarker marker in markers)
            {
                foreach (SkinnedMeshRenderer renderer in marker.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (!renderer.enabled)
                    {
                        continue;
                    }

                    foundEnabledRenderer = true;
                    Assert.That(renderer.receiveShadows, Is.False, $"{visualName} should use baked texture shading.");
                    AssertRendererUsesCharacterUnlitMaterials(renderer, visualName);
                }
            }

            Assert.That(foundEnabledRenderer, Is.True, $"No enabled skinned renderer was found for {visualName}.");
        }

        private static void AssertRendererUsesCharacterUnlitMaterials(Renderer renderer, string visualName)
        {
            Assert.That(renderer.sharedMaterials, Is.Not.Empty, $"{visualName} has no material.");
            foreach (Material material in renderer.sharedMaterials)
            {
                Assert.That(material, Is.Not.Null, $"{visualName} has a missing material.");
                Assert.That(
                    material.shader.name,
                    Is.EqualTo(MMOCharacterUnlitMaterialUtility.UnlitShaderName),
                    $"{visualName} should render its authored texture without scene-lighting variation.");
                Assert.That(
                    material.FindPass("ShadowCaster"),
                    Is.GreaterThanOrEqualTo(0),
                    $"{visualName}'s Unlit shader must retain a ShadowCaster pass.");
            }
        }

        private static void AssertAttachmentSurfacePolicy(
            MMOEquipmentVisualInstanceMarker marker,
            string visualName)
        {
            bool foundVisibleSurface = false;
            foreach (Renderer renderer in marker.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || renderer is not (MeshRenderer or SkinnedMeshRenderer))
                {
                    continue;
                }

                foundVisibleSurface = true;
                Assert.That(renderer.receiveShadows, Is.False, $"{visualName} should not receive scene shadows.");
                Assert.That(
                    renderer.shadowCastingMode,
                    Is.Not.EqualTo(ShadowCastingMode.Off),
                    $"{visualName} should cast a world shadow.");
                Assert.That(
                    renderer.shadowCastingMode,
                    Is.Not.EqualTo(ShadowCastingMode.ShadowsOnly),
                    $"{visualName} should remain visible while casting its shadow.");
                AssertRendererUsesCharacterUnlitMaterials(renderer, visualName);
            }

            Assert.That(foundVisibleSurface, Is.True, $"No visible mesh renderer was found for {visualName}.");
        }

        [TestCase("Assets/_Project/Configs/Archetypes/Orc_Warrior.asset")]
        [TestCase("Assets/_Project/Configs/Archetypes/Orc_Mage.asset")]
        [TestCase("Assets/_Project/Configs/Archetypes/Orc_Shaman.asset")]
        [TestCase("Assets/_Project/Configs/Archetypes/Troll_Warrior.asset")]
        [TestCase("Assets/_Project/Configs/Archetypes/Troll_Mage.asset")]
        [TestCase("Assets/_Project/Configs/Archetypes/Troll_Shaman.asset")]
        public void StartingEquipment_UsesSharedPoorClothArmor(string archetypePath)
        {
            MMOCharacterArchetypeDefinition archetype = AssetDatabase.LoadAssetAtPath<MMOCharacterArchetypeDefinition>(archetypePath);
            Assert.That(archetype, Is.Not.Null);
            Assert.That(archetype.StartingEquipment, Is.Not.Empty);

            HashSet<string> expectedArmorIds = new()
            {
                "threadbare_cloth_tunic",
                "threadbare_cloth_gloves",
                "threadbare_cloth_pants",
                "threadbare_cloth_shoes"
            };
            MMOItemCatalog itemCatalog = AssetDatabase.LoadAssetAtPath<MMOItemCatalog>(
                "Assets/_Project/Configs/Items/Starter_Item_Catalog.asset");
            Assert.That(itemCatalog, Is.Not.Null);
            foreach (string itemId in expectedArmorIds)
            {
                Assert.That(itemCatalog.FindById(itemId), Is.Not.Null, $"{itemId} is missing from the runtime item catalog.");
            }

            HashSet<string> startingArmorIds = new();
            foreach (MMOItemDefinition item in archetype.StartingEquipment)
            {
                Assert.That(item, Is.Not.Null);
                if (item.EquipmentSlot is MMOEquipmentSlotType.Chest
                    or MMOEquipmentSlotType.Hands
                    or MMOEquipmentSlotType.Legs
                    or MMOEquipmentSlotType.Feet)
                {
                    Assert.That(item.ArmorWeight, Is.EqualTo(MMOArmorWeight.Cloth), item.DisplayName);
                    Assert.That(item.Quality, Is.EqualTo(MMOItemQuality.Poor), item.DisplayName);
                    startingArmorIds.Add(item.ItemId);
                }
            }

            Assert.That(startingArmorIds.SetEquals(expectedArmorIds), Is.True, archetype.DisplayName);
        }

        [TestCase("Assets/_Project/Configs/Archetypes/Orc_Warrior.asset", MMOArmorWeight.Mail)]
        [TestCase("Assets/_Project/Configs/Archetypes/Orc_Mage.asset", MMOArmorWeight.Cloth)]
        [TestCase("Assets/_Project/Configs/Archetypes/Orc_Shaman.asset", MMOArmorWeight.Leather)]
        [TestCase("Assets/_Project/Configs/Archetypes/Troll_Warrior.asset", MMOArmorWeight.Mail)]
        [TestCase("Assets/_Project/Configs/Archetypes/Troll_Mage.asset", MMOArmorWeight.Cloth)]
        [TestCase("Assets/_Project/Configs/Archetypes/Troll_Shaman.asset", MMOArmorWeight.Leather)]
        public void CreationPreview_UsesClassTierArmorAndIncludesWeapon(
            string archetypePath,
            MMOArmorWeight expectedArmorWeight)
        {
            MMOCharacterArchetypeDefinition archetype = AssetDatabase.LoadAssetAtPath<MMOCharacterArchetypeDefinition>(archetypePath);
            Assert.That(archetype, Is.Not.Null);
            Assert.That(archetype.CreationPreviewEquipment, Is.Not.Empty);

            int armorPieceCount = 0;
            bool hasWeapon = false;
            foreach (MMOItemDefinition item in archetype.CreationPreviewEquipment)
            {
                Assert.That(item, Is.Not.Null);
                hasWeapon |= item.IsWeapon;
                if (item.EquipmentSlot is MMOEquipmentSlotType.Chest
                    or MMOEquipmentSlotType.Hands
                    or MMOEquipmentSlotType.Legs
                    or MMOEquipmentSlotType.Feet)
                {
                    armorPieceCount++;
                    Assert.That(item.ArmorWeight, Is.EqualTo(expectedArmorWeight), item.DisplayName);
                    Assert.That(item.ItemId, Does.Not.StartWith("threadbare_cloth_"), item.DisplayName);
                }
            }

            Assert.That(armorPieceCount, Is.EqualTo(4), $"{archetype.DisplayName} preview should show its four-piece class tier set.");
            Assert.That(hasWeapon, Is.True, $"{archetype.DisplayName} preview has no weapon.");
        }
    }
}
