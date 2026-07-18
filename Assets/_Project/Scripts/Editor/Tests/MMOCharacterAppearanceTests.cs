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
                MMOHairstyleDefinition first = new();
                first.Configure("hair_1", "Hairstyle 1", null);
                MMOHairstyleDefinition second = new();
                second.Configure("hair_2", "Hairstyle 2", null);
                catalog.Configure(new[] { head }, new[] { first, second });

                Assert.That(catalog.NormalizeHeadStyleId(string.Empty), Is.EqualTo("head_1"));
                Assert.That(catalog.NormalizeHeadStyleId("unknown"), Is.EqualTo("head_1"));
                Assert.That(catalog.NormalizeHairstyleId(string.Empty), Is.EqualTo("hair_1"));
                Assert.That(catalog.NormalizeHairstyleId("unknown"), Is.EqualTo("hair_1"));
                Assert.That(catalog.NormalizeHairstyleId("hair_2"), Is.EqualTo("hair_2"));
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
            Assert.That(saveData.hairstyleId, Is.EqualTo("hair_1"));
        }

        [Test]
        public void SharedSessionClone_PreservesHeadStyleForRemotePlayers()
        {
            MMOCharacterSaveData source = new() { headStyleId = "head_custom" };
            MethodInfo cloneMethod = typeof(MMOSharedSessionState).GetMethod(
                "CloneCharacter",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(cloneMethod, Is.Not.Null);
            MMOCharacterSaveData clone = cloneMethod.Invoke(null, new object[] { source }) as MMOCharacterSaveData;

            Assert.That(clone, Is.Not.Null);
            Assert.That(clone.headStyleId, Is.EqualTo("head_custom"));
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
            Assert.That(catalog.Hairstyles, Has.Count.EqualTo(3));

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
            MMOItemDefinition chest = AssetDatabase.LoadAssetAtPath<MMOItemDefinition>(
                "Assets/_Project/Configs/Items/Ashguard_Vest_Mail.asset");
            MMOCharacterAppearanceCatalog catalog = AssetDatabase.LoadAssetAtPath<MMOCharacterAppearanceCatalog>(
                "Assets/Resources/RPGClone/Character_Appearance_Catalog.asset");
            Assert.That(playerPrefab, Is.Not.Null);
            Assert.That(chest, Is.Not.Null);
            Assert.That(catalog, Is.Not.Null);

            GameObject root = new("Character Preview Binding Test Root");
            try
            {
                GameObject actor = MMOCharacterPreviewActor.Create(
                    playerPrefab,
                    root.transform,
                    MMOPlayableRace.Orc,
                    MMOPlayableClass.Warrior,
                    new[] { chest },
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
                }

                AssertGeneratedSkinnedMeshesUseProductionBones(
                    actor.GetComponentsInChildren<MMOEquipmentVisualInstanceMarker>(true),
                    "chest armor");
                AssertGeneratedSkinnedMeshesUseProductionBones(
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
                "Assets/_Project/Configs/Items/Recruits_Shortsword.asset");
            MMOItemDefinition shield = AssetDatabase.LoadAssetAtPath<MMOItemDefinition>(
                "Assets/_Project/Configs/Items/Recruits_Shield.asset");
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
                bool foundReadySword = false;
                bool foundMobilityShield = false;
                foreach (MMOEquipmentVisualInstanceMarker marker in actor.GetComponentsInChildren<MMOEquipmentVisualInstanceMarker>(true))
                {
                    foundReadySword |= marker.name == sword.EquipmentVisual.ModelPrefab.name;
                    foundMobilityShield |= marker.name == shield.EquipmentVisual.CombatMovementModelPrefab.name;
                }

                Assert.That(foundReadySword, Is.True, "The weapon preview should retain its ready-position prefab.");
                Assert.That(foundMobilityShield, Is.True, "The shield preview should use its mobility-positioned prefab.");
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

        [TestCase("Assets/_Project/Configs/Archetypes/Orc_Warrior.asset", MMOArmorWeight.Mail)]
        [TestCase("Assets/_Project/Configs/Archetypes/Orc_Mage.asset", MMOArmorWeight.Cloth)]
        [TestCase("Assets/_Project/Configs/Archetypes/Orc_Shaman.asset", MMOArmorWeight.Leather)]
        public void CreationPreview_UsesClassArmorAndIncludesWeapon(string archetypePath, MMOArmorWeight expectedArmorWeight)
        {
            MMOCharacterArchetypeDefinition archetype = AssetDatabase.LoadAssetAtPath<MMOCharacterArchetypeDefinition>(archetypePath);
            Assert.That(archetype, Is.Not.Null);
            Assert.That(archetype.CreationPreviewEquipment, Is.Not.Empty);

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
                    Assert.That(item.ArmorWeight, Is.EqualTo(expectedArmorWeight), item.DisplayName);
                }
            }

            Assert.That(hasWeapon, Is.True, $"{archetype.DisplayName} preview has no weapon.");
        }
    }
}
