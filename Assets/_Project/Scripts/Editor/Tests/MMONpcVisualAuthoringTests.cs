using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using RPGClone.Characters;
using RPGClone.EditorTools;
using RPGClone.Inventory;
using RPGClone.Player;
using UnityEditor;
using UnityEngine;

namespace RPGClone.EditorTests
{
    public sealed class MMONpcVisualAuthoringTests
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
        public void EmptyAppearanceChoices_ResolveToSharedPlayerDefaults()
        {
            MMOCharacterAppearanceCatalog catalog = CreateCatalog();
            GameObject npc = Track(new GameObject("NPC"));
            MMONpcVisualAuthoring authoring = npc.AddComponent<MMONpcVisualAuthoring>();

            authoring.Configure(catalog, string.Empty, string.Empty, null, null, null, null);

            Assert.That(authoring.HairstyleId, Is.EqualTo("hair_default"));
            Assert.That(authoring.FaceId, Is.EqualTo("face_default"));
            Assert.That(authoring.ChestArmor, Is.Null);
            Assert.That(authoring.Gloves, Is.Null);
            Assert.That(authoring.Pants, Is.Null);
            Assert.That(authoring.Boots, Is.Null);
        }

        [Test]
        public void AuthoredArmor_UsesSharedEquipmentVisualRenderer()
        {
            MMOCharacterAppearanceCatalog catalog = CreateCatalog();
            MMOEquipmentVisualDefinition chest = CreateArmor(MMOEquipmentSlotType.Chest, MMOCharacterBodyPart.Torso);
            MMOEquipmentVisualDefinition gloves = CreateArmor(MMOEquipmentSlotType.Hands, MMOCharacterBodyPart.Hands);
            MMOEquipmentVisualDefinition pants = CreateArmor(MMOEquipmentSlotType.Legs, MMOCharacterBodyPart.Legs);
            MMOEquipmentVisualDefinition boots = CreateArmor(MMOEquipmentSlotType.Feet, MMOCharacterBodyPart.Feet);
            GameObject npc = Track(new GameObject("NPC"));
            MMONpcVisualAuthoring authoring = npc.AddComponent<MMONpcVisualAuthoring>();

            authoring.Configure(catalog, "hair_alt", "face_alt", chest, gloves, pants, boots);

            MMOPlayerEquipmentVisuals renderer = npc.GetComponent<MMOPlayerEquipmentVisuals>();
            FieldInfo directVisualsField = typeof(MMOPlayerEquipmentVisuals).GetField(
                "directVisualDefinitions",
                BindingFlags.Instance | BindingFlags.NonPublic);
            List<MMOEquipmentVisualDefinition> directVisuals =
                directVisualsField?.GetValue(renderer) as List<MMOEquipmentVisualDefinition>;

            Assert.That(authoring.HairstyleId, Is.EqualTo("hair_alt"));
            Assert.That(authoring.FaceId, Is.EqualTo("face_alt"));
            Assert.That(directVisuals, Is.EquivalentTo(new[] { chest, gloves, pants, boots }));
        }

        [Test]
        public void NpcLocomotionSource_IsAlwaysGroundedAndStationary()
        {
            GameObject npc = Track(new GameObject("NPC"));
            IMMOPlayerLocomotionSource locomotion = npc.AddComponent<MMONpcVisualAuthoring>();

            Assert.That(locomotion.CurrentPlanarSpeed, Is.Zero);
            Assert.That(locomotion.CurrentPlanarVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(locomotion.CurrentLocalPlanarVelocity, Is.EqualTo(Vector2.zero));
            Assert.That(locomotion.VerticalVelocity, Is.Zero);
            Assert.That(locomotion.IsGrounded, Is.True);
            Assert.That(locomotion.HasGroundContact, Is.True);
            Assert.That(locomotion.IsAirborne, Is.False);
        }

        [Test]
        public void MixedNpcSelection_RepaintPreservesDistinctAuthoredValues()
        {
            MMOCharacterAppearanceCatalog catalog = CreateCatalog();
            MMOEquipmentVisualDefinition leather = CreateArmor(
                MMOEquipmentSlotType.Chest,
                MMOCharacterBodyPart.Torso);
            MMOEquipmentVisualDefinition mail = CreateArmor(
                MMOEquipmentSlotType.Chest,
                MMOCharacterBodyPart.Torso);
            GameObject firstNpc = Track(new GameObject("Leather NPC"));
            GameObject secondNpc = Track(new GameObject("Mail NPC"));
            MMONpcVisualAuthoring first = firstNpc.AddComponent<MMONpcVisualAuthoring>();
            MMONpcVisualAuthoring second = secondNpc.AddComponent<MMONpcVisualAuthoring>();
            first.Configure(catalog, "hair_alt", "face_alt", leather, null, null, null);
            second.Configure(catalog, string.Empty, string.Empty, mail, null, null, null);

            SerializedObject mixedSelection = new(new Object[] { first, second });
            SerializedProperty hair = mixedSelection.FindProperty("hairstyleId");
            SerializedProperty chest = mixedSelection.FindProperty("chestArmor");
            Assert.That(hair.hasMultipleDifferentValues, Is.True);
            Assert.That(chest.hasMultipleDifferentValues, Is.True);

            MMONpcVisualPopupAssignment.SetStringIfChanged(hair, false, string.Empty);
            MMONpcVisualPopupAssignment.SetObjectIfChanged(chest, false, null);
            mixedSelection.ApplyModifiedProperties();

            Assert.That(first.HairstyleId, Is.EqualTo("hair_alt"));
            Assert.That(second.HairstyleId, Is.EqualTo("hair_default"));
            Assert.That(first.ChestArmor, Is.SameAs(leather));
            Assert.That(second.ChestArmor, Is.SameAs(mail));
        }

        private MMOCharacterAppearanceCatalog CreateCatalog()
        {
            MMOCharacterAppearanceCatalog catalog = Track(
                ScriptableObject.CreateInstance<MMOCharacterAppearanceCatalog>());
            MMOHeadStyleDefinition head = new();
            head.Configure("head_default", "Default Head", null);
            MMOFaceDefinition defaultFace = new();
            defaultFace.Configure("face_default", "Default Face", null);
            MMOFaceDefinition alternateFace = new();
            alternateFace.Configure("face_alt", "Alternate Face", null);
            MMOHairstyleDefinition defaultHair = new();
            defaultHair.Configure("hair_default", "Default Hair", null);
            MMOHairstyleDefinition alternateHair = new();
            alternateHair.Configure("hair_alt", "Alternate Hair", null);
            catalog.Configure(
                new[] { head },
                new[] { defaultFace, alternateFace },
                new[] { defaultHair, alternateHair });
            return catalog;
        }

        private MMOEquipmentVisualDefinition CreateArmor(
            MMOEquipmentSlotType slot,
            MMOCharacterBodyPart bodyPart)
        {
            MMOEquipmentVisualDefinition visual = Track(
                ScriptableObject.CreateInstance<MMOEquipmentVisualDefinition>());
            visual.Configure(
                slot,
                bodyPart,
                true,
                null,
                null,
                false,
                Color.white,
                null,
                null,
                Vector3.zero,
                Vector3.zero,
                Vector3.one);
            return visual;
        }

        private T Track<T>(T value) where T : Object
        {
            createdObjects.Add(value);
            return value;
        }
    }
}
