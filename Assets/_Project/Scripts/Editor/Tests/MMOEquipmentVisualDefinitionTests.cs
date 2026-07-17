using System.Collections.Generic;
using NUnit.Framework;
using RPGClone.Characters;
using RPGClone.Inventory;
using UnityEngine;

namespace RPGClone.Tests
{
    public sealed class MMOEquipmentVisualDefinitionTests
    {
        private readonly List<Object> createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                {
                    Object.DestroyImmediate(createdObjects[i]);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void AttachmentPresentation_UsesConfiguredStatePrefabsAndSockets()
        {
            MMOEquipmentVisualDefinition definition = Track(ScriptableObject.CreateInstance<MMOEquipmentVisualDefinition>());
            GameObject readyPrefab = Track(new GameObject("Ready"));
            GameObject stowedPrefab = Track(new GameObject("Stowed"));
            GameObject movementPrefab = Track(new GameObject("Movement"));

            definition.ConfigureAttachment(
                MMOEquipmentSlotType.OffHand,
                "ready_socket",
                "stowed_socket",
                "movement_socket",
                readyPrefab,
                stowedPrefab,
                movementPrefab,
                Vector3.zero,
                Vector3.zero,
                Vector3.one);

            Assert.That(definition.GetAttachmentModelPrefab(MMOEquipmentAttachmentPresentationState.Ready), Is.SameAs(readyPrefab));
            Assert.That(definition.GetAttachmentModelPrefab(MMOEquipmentAttachmentPresentationState.Stowed), Is.SameAs(stowedPrefab));
            Assert.That(definition.GetAttachmentModelPrefab(MMOEquipmentAttachmentPresentationState.CombatMovement), Is.SameAs(movementPrefab));
            Assert.That(definition.GetAttachmentSocketName(MMOEquipmentAttachmentPresentationState.Ready), Is.EqualTo("ready_socket"));
            Assert.That(definition.GetAttachmentSocketName(MMOEquipmentAttachmentPresentationState.Stowed), Is.EqualTo("stowed_socket"));
            Assert.That(definition.GetAttachmentSocketName(MMOEquipmentAttachmentPresentationState.CombatMovement), Is.EqualTo("movement_socket"));
        }

        [Test]
        public void AttachmentPresentation_FallsBackToReadyPrefabAndSocket()
        {
            MMOEquipmentVisualDefinition definition = Track(ScriptableObject.CreateInstance<MMOEquipmentVisualDefinition>());
            GameObject readyPrefab = Track(new GameObject("Ready"));

            definition.ConfigureAttachment(
                MMOEquipmentSlotType.OffHand,
                "ready_socket",
                string.Empty,
                string.Empty,
                readyPrefab,
                null,
                null,
                Vector3.zero,
                Vector3.zero,
                Vector3.one);

            Assert.That(definition.GetAttachmentModelPrefab(MMOEquipmentAttachmentPresentationState.Stowed), Is.SameAs(readyPrefab));
            Assert.That(definition.GetAttachmentModelPrefab(MMOEquipmentAttachmentPresentationState.CombatMovement), Is.SameAs(readyPrefab));
            Assert.That(definition.GetAttachmentSocketName(MMOEquipmentAttachmentPresentationState.Stowed), Is.EqualTo("ready_socket"));
            Assert.That(definition.GetAttachmentSocketName(MMOEquipmentAttachmentPresentationState.CombatMovement), Is.EqualTo("ready_socket"));
        }

        [TestCase(false, false, 0f, MMOEquipmentAttachmentPresentationState.Stowed)]
        [TestCase(false, true, 4f, MMOEquipmentAttachmentPresentationState.Stowed)]
        [TestCase(true, false, 0f, MMOEquipmentAttachmentPresentationState.Ready)]
        [TestCase(true, false, 0.051f, MMOEquipmentAttachmentPresentationState.CombatMovement)]
        [TestCase(true, true, 0f, MMOEquipmentAttachmentPresentationState.CombatMovement)]
        public void AttachmentPresentation_ResolvesCombatLocomotionState(
            bool isInCombat,
            bool isAirborne,
            float planarSpeed,
            MMOEquipmentAttachmentPresentationState expectedState)
        {
            MMOEquipmentAttachmentPresentationState state = MMOEquipmentAttachmentPresentationResolver.Resolve(
                isInCombat,
                isAirborne,
                planarSpeed,
                0.05f);

            Assert.That(state, Is.EqualTo(expectedState));
        }

        private T Track<T>(T value) where T : Object
        {
            createdObjects.Add(value);
            return value;
        }
    }
}
