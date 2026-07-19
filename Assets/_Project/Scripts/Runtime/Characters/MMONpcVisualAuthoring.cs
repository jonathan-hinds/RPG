using System;
using System.Collections.Generic;
using RPGClone.Inventory;
using RPGClone.Player;
using UnityEngine;

namespace RPGClone.Characters
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MMOCharacterAppearanceVisuals))]
    [RequireComponent(typeof(MMOPlayerEquipmentVisuals))]
    public sealed class MMONpcVisualAuthoring : MonoBehaviour, IMMOPlayerLocomotionSource
    {
        [Header("Shared Player Appearance")]
        [SerializeField] private MMOCharacterAppearanceCatalog appearanceCatalog;
        [SerializeField] private string hairstyleId;
        [SerializeField] private string faceId;

        [Header("Armor (None Uses Default Player Skin)")]
        [SerializeField] private MMOEquipmentVisualDefinition chestArmor;
        [SerializeField] private MMOEquipmentVisualDefinition gloves;
        [SerializeField] private MMOEquipmentVisualDefinition pants;
        [SerializeField] private MMOEquipmentVisualDefinition boots;

        [HideInInspector, SerializeField] private MMOCharacterAppearanceVisuals appearanceVisuals;
        [HideInInspector, SerializeField] private MMOPlayerEquipmentVisuals equipmentVisuals;
        [HideInInspector, SerializeField] private MMOPlayerLocomotionAnimator locomotionAnimator;

        private readonly List<MMOEquipmentVisualDefinition> selectedArmor = new(4);

        public MMOCharacterAppearanceCatalog AppearanceCatalog => appearanceCatalog;
        public string HairstyleId => string.IsNullOrWhiteSpace(hairstyleId)
            ? appearanceCatalog?.DefaultHairstyleId
            : appearanceCatalog?.NormalizeHairstyleId(hairstyleId) ?? hairstyleId;
        public string FaceId => string.IsNullOrWhiteSpace(faceId)
            ? appearanceCatalog?.DefaultFaceId
            : appearanceCatalog?.NormalizeFaceId(faceId) ?? faceId;
        public MMOEquipmentVisualDefinition ChestArmor => chestArmor;
        public MMOEquipmentVisualDefinition Gloves => gloves;
        public MMOEquipmentVisualDefinition Pants => pants;
        public MMOEquipmentVisualDefinition Boots => boots;

        public float CurrentPlanarSpeed => 0f;
        public Vector3 CurrentPlanarVelocity => Vector3.zero;
        public float VerticalVelocity => 0f;
        public bool IsGrounded => true;
        public bool IsAirborne => false;
        public bool HasGroundContact => true;
        public Vector2 CurrentLocalPlanarVelocity => Vector2.zero;

        public event Action Jumped
        {
            add { }
            remove { }
        }

        public event Action BecameAirborne
        {
            add { }
            remove { }
        }

        public event Action Landed
        {
            add { }
            remove { }
        }

        private void Awake()
        {
            ApplySelections();
        }

        private void OnEnable()
        {
            ApplySelections();
        }

        private void OnValidate()
        {
            EnsureReferences();
        }

        public void Configure(
            MMOCharacterAppearanceCatalog newAppearanceCatalog,
            string newHairstyleId,
            string newFaceId,
            MMOEquipmentVisualDefinition newChestArmor,
            MMOEquipmentVisualDefinition newGloves,
            MMOEquipmentVisualDefinition newPants,
            MMOEquipmentVisualDefinition newBoots)
        {
            appearanceCatalog = newAppearanceCatalog;
            hairstyleId = NormalizeOptionalId(newHairstyleId, appearanceCatalog?.FindHairstyle(newHairstyleId) != null);
            faceId = NormalizeOptionalId(newFaceId, appearanceCatalog?.FindFace(newFaceId) != null);
            chestArmor = ValidateArmorSlot(newChestArmor, MMOEquipmentSlotType.Chest);
            gloves = ValidateArmorSlot(newGloves, MMOEquipmentSlotType.Hands);
            pants = ValidateArmorSlot(newPants, MMOEquipmentSlotType.Legs);
            boots = ValidateArmorSlot(newBoots, MMOEquipmentSlotType.Feet);
            ApplySelections();
        }

        public void ApplySelections()
        {
            EnsureReferences();

            if (appearanceVisuals != null)
            {
                appearanceVisuals.Configure(
                    appearanceCatalog,
                    appearanceCatalog?.DefaultHeadStyleId,
                    FaceId,
                    HairstyleId);
            }

            if (equipmentVisuals != null)
            {
                selectedArmor.Clear();
                AddSelectedArmor(chestArmor, MMOEquipmentSlotType.Chest);
                AddSelectedArmor(gloves, MMOEquipmentSlotType.Hands);
                AddSelectedArmor(pants, MMOEquipmentSlotType.Legs);
                AddSelectedArmor(boots, MMOEquipmentSlotType.Feet);
                equipmentVisuals.SetDirectVisualDefinitions(selectedArmor);
            }

            locomotionAnimator?.SetLocomotionSource(this);
        }

        private void EnsureReferences()
        {
            appearanceVisuals ??= GetComponent<MMOCharacterAppearanceVisuals>();
            equipmentVisuals ??= GetComponent<MMOPlayerEquipmentVisuals>();
            locomotionAnimator ??= GetComponent<MMOPlayerLocomotionAnimator>();
        }

        private void AddSelectedArmor(MMOEquipmentVisualDefinition visualDefinition, MMOEquipmentSlotType slot)
        {
            MMOEquipmentVisualDefinition validated = ValidateArmorSlot(visualDefinition, slot);
            if (validated != null)
            {
                selectedArmor.Add(validated);
            }
        }

        private MMOEquipmentVisualDefinition ValidateArmorSlot(
            MMOEquipmentVisualDefinition visualDefinition,
            MMOEquipmentSlotType expectedSlot)
        {
            if (visualDefinition == null || visualDefinition.EquipmentSlot == expectedSlot)
            {
                return visualDefinition;
            }

            Debug.LogWarning(
                $"NPC visual '{visualDefinition.name}' is assigned to {visualDefinition.EquipmentSlot}, " +
                $"but '{name}' expects {expectedSlot}. The incompatible visual was ignored.",
                this);
            return null;
        }

        private static string NormalizeOptionalId(string value, bool existsInCatalog)
        {
            return string.IsNullOrWhiteSpace(value) || !existsInCatalog ? string.Empty : value.Trim();
        }
    }
}
