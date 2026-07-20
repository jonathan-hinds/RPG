using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGClone.Animation;
using RPGClone.EditorTools;
using RPGClone.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace RPGClone.EditorTests
{
    public sealed class MMOLayeredAnimationTests
    {
        private static readonly string[] RequiredUpperBodyStates =
        {
            "Empty",
            "Attack1",
            "Attack2",
            "Damage",
            "Attack1H",
            "Attack2H",
            "AttackUnarmed",
            "CombatDamage",
            "Casting",
            "Cast"
        };

        private static readonly ControllerExpectation[] ControllerExpectations =
        {
            new ControllerExpectation(
                MMOLayeredAnimationInstaller.PlayerControllerPath,
                "Assets/Player/Animations/Clips/CharacterTest_UpperBody.mask",
                "root/root.x",
                "root/root.x/spine_01.x"),
            new ControllerExpectation(
                MMOLayeredAnimationInstaller.AshCanyonControllerPath,
                "Assets/Characters/AshCanyonCreature/Animations/Controllers/AshCanyonCreature_UpperBody.mask",
                "root/CC_Base_Hip",
                "root/CC_Base_Hip/CC_Base_Waist/CC_Base_Spine01"),
            new ControllerExpectation(
                MMOLayeredAnimationInstaller.AshGeneralControllerPath,
                "Assets/Characters/AshLeader/Animations/Controllers/AshGeneral_UpperBody.mask",
                "root/CC_Base_Hip",
                "root/CC_Base_Hip/CC_Base_Waist/CC_Base_Spine01"),
            new ControllerExpectation(
                MMOLayeredAnimationInstaller.BristlebackControllerPath,
                "Assets/Characters/Bristleback/Animations/Controllers/Bristleback_UpperBody.mask",
                "root/CC_Base_Hip",
                "root/CC_Base_Hip/CC_Base_Waist/CC_Base_Spine01"),
            new ControllerExpectation(
                MMOLayeredAnimationInstaller.TrogControllerPath,
                "Assets/Characters/Trog/Animations/Controllers/Trog_UpperBody.mask",
                "root/root.x",
                "root/root.x/spine_01.x"),
            new ControllerExpectation(
                MMOLayeredAnimationInstaller.OgreControllerPath,
                "Assets/Characters/Ogre/Animations/Controllers/Ogre_UpperBody.mask",
                "root/root.x",
                "root/root.x/spine_01.x"),
            new ControllerExpectation(
                MMOLayeredAnimationInstaller.WolfControllerPath,
                "Assets/Characters/Wolf/Animations/Controllers/Wolf_UpperBody.mask",
                "rig/c_pos/c_traj/c_root_master.x",
                "rig/c_pos/c_traj/c_root_master.x/c_spine_01.x")
        };

        [Test]
        public void ControllersContainMaskedOverrideActionLayer()
        {
            foreach (ControllerExpectation expectation in ControllerExpectations)
            {
                AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(expectation.ControllerPath);
                Assert.That(controller, Is.Not.Null, expectation.ControllerPath);

                AnimatorControllerLayer layer = controller.layers
                    .SingleOrDefault(candidate => candidate.name == MMOLayeredActionPlayer.UpperBodyLayerName);
                Assert.That(layer, Is.Not.Null, expectation.ControllerPath);
                Assert.That(layer.avatarMask, Is.Not.Null, expectation.ControllerPath);
                Assert.That(AssetDatabase.GetAssetPath(layer.avatarMask), Is.EqualTo(expectation.MaskPath));
                Assert.That(layer.blendingMode, Is.EqualTo(AnimatorLayerBlendingMode.Override));
                Assert.That(layer.defaultWeight, Is.EqualTo(0f));

                HashSet<string> stateNames = layer.stateMachine.states
                    .Select(child => child.state.name)
                    .ToHashSet();
                CollectionAssert.IsSubsetOf(RequiredUpperBodyStates, stateNames, expectation.ControllerPath);
            }
        }

        [Test]
        public void MasksExcludeLocomotionRootAndIncludeUpperBodyBoundary()
        {
            foreach (ControllerExpectation expectation in ControllerExpectations)
            {
                AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(expectation.MaskPath);
                Assert.That(mask, Is.Not.Null, expectation.MaskPath);
                Assert.That(GetTransformWeight(mask, expectation.ExcludedPath), Is.False, expectation.ExcludedPath);
                Assert.That(GetTransformWeight(mask, expectation.IncludedPath), Is.True, expectation.IncludedPath);
            }
        }

        [Test]
        public void AnimationSetsReferenceTheirRigSpecificControllers()
        {
            AssertController<MMOPlayerLocomotionAnimationSet>(
                "Assets/Player/Animations/Clips/CharacterTest_PlayerLocomotion.asset",
                MMOLayeredAnimationInstaller.PlayerControllerPath,
                set => set.BaseController);
            AssertController<MMOPlayerCombatAnimationSet>(
                "Assets/Player/Animations/Clips/CharacterTest_PlayerCombat.asset",
                MMOLayeredAnimationInstaller.PlayerControllerPath,
                set => set.BaseController);
            AssertController<MMOCreatureAnimationSet>(
                "Assets/Characters/AshCanyonCreature/Animations/Clips/AshCanyonCreature_AnimationSet.asset",
                MMOLayeredAnimationInstaller.AshCanyonControllerPath,
                set => set.BaseController);
            AssertController<MMOCreatureAnimationSet>(
                "Assets/Characters/AshLeader/Animations/Clips/AshGeneral_AnimationSet.asset",
                MMOLayeredAnimationInstaller.AshGeneralControllerPath,
                set => set.BaseController);
            AssertController<MMOCreatureAnimationSet>(
                "Assets/Characters/Shared/CreatureCombatAnimations/Clips/StandardCreature_AnimationSet.asset",
                MMOLayeredAnimationInstaller.BristlebackControllerPath,
                set => set.BaseController);
            AssertController<MMOCreatureAnimationSet>(
                "Assets/Characters/Trog/Animations/Clips/Trog_AnimationSet.asset",
                MMOLayeredAnimationInstaller.TrogControllerPath,
                set => set.BaseController);
            AssertController<MMOCreatureAnimationSet>(
                "Assets/Characters/Ogre/Animations/Clips/Ogre_AnimationSet.asset",
                MMOLayeredAnimationInstaller.OgreControllerPath,
                set => set.BaseController);
            AssertController<MMOCreatureAnimationSet>(
                "Assets/Characters/Wolf/Animations/Clips/Wolf_AnimationSet.asset",
                MMOLayeredAnimationInstaller.WolfControllerPath,
                set => set.BaseController);
        }

        private static bool GetTransformWeight(AvatarMask mask, string path)
        {
            for (int i = 0; i < mask.transformCount; i++)
            {
                if (mask.GetTransformPath(i) == path)
                {
                    return mask.GetTransformActive(i);
                }
            }

            Assert.Fail($"Mask {AssetDatabase.GetAssetPath(mask)} does not contain transform {path}.");
            return false;
        }

        private static void AssertController<T>(
            string animationSetPath,
            string expectedControllerPath,
            System.Func<T, RuntimeAnimatorController> getController)
            where T : ScriptableObject
        {
            T set = AssetDatabase.LoadAssetAtPath<T>(animationSetPath);
            Assert.That(set, Is.Not.Null, animationSetPath);
            Assert.That(AssetDatabase.GetAssetPath(getController(set)), Is.EqualTo(expectedControllerPath));
        }

        private sealed class ControllerExpectation
        {
            public ControllerExpectation(string controllerPath, string maskPath, string excludedPath, string includedPath)
            {
                ControllerPath = controllerPath;
                MaskPath = maskPath;
                ExcludedPath = excludedPath;
                IncludedPath = includedPath;
            }

            public string ControllerPath { get; }
            public string MaskPath { get; }
            public string ExcludedPath { get; }
            public string IncludedPath { get; }
        }
    }
}
