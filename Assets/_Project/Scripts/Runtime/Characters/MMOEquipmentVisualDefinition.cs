using RPGClone.Inventory;
using UnityEngine;

namespace RPGClone.Characters
{
    public enum MMOEquipmentVisualBindingMode
    {
        BodyPart,
        AttachmentSocket
    }

    public enum MMOEquipmentAttachmentPresentationState
    {
        Stowed,
        Ready,
        CombatMovement
    }

    public static class MMOEquipmentAttachmentPresentationResolver
    {
        public static MMOEquipmentAttachmentPresentationState Resolve(
            bool isInCombat,
            bool isAirborne,
            float planarSpeed,
            float movementSpeedThreshold)
        {
            if (!isInCombat)
            {
                return MMOEquipmentAttachmentPresentationState.Stowed;
            }

            return isAirborne || planarSpeed > Mathf.Max(0f, movementSpeedThreshold)
                ? MMOEquipmentAttachmentPresentationState.CombatMovement
                : MMOEquipmentAttachmentPresentationState.Ready;
        }
    }

    [CreateAssetMenu(menuName = "RPG Clone/Characters/Equipment Visual", fileName = "EquipmentVisual")]
    public sealed class MMOEquipmentVisualDefinition : ScriptableObject
    {
        [Header("Binding")]
        [SerializeField] private MMOEquipmentVisualBindingMode bindingMode = MMOEquipmentVisualBindingMode.BodyPart;
        [SerializeField] private MMOEquipmentSlotType equipmentSlot = MMOEquipmentSlotType.Chest;
        [SerializeField] private MMOCharacterBodyPart bodyPart = MMOCharacterBodyPart.Torso;
        [SerializeField] private bool hideBaseBodyPart = true;

        [Header("Replacement")]
        [SerializeField] private GameObject modelPrefab;
        [Tooltip("Optional prefab used for attachment visuals while stowed/out of combat. Falls back to Model Prefab when empty.")]
        [SerializeField] private GameObject stowedModelPrefab;
        [Tooltip("Optional prefab used for attachment visuals while moving or airborne in combat. Falls back to Model Prefab when empty.")]
        [SerializeField] private GameObject combatMovementModelPrefab;
        [SerializeField] private Material materialOverride;
        [SerializeField] private bool useColorOverride;
        [SerializeField] private Color colorOverride = Color.white;
        [SerializeField] private Texture2D diffuseTexture;
        [SerializeField] private Texture2D normalTexture;

        [Header("Attachment")]
        [Tooltip("Skeleton transform name used while the item is drawn/in combat.")]
        [SerializeField] private string socketName = "cc_weapon_r";
        [Tooltip("Skeleton transform name used while the item is stowed/out of combat.")]
        [SerializeField] private string stowedSocketName = "cc_hip.l";
        [Tooltip("Optional skeleton transform name used while moving or airborne in combat. Falls back to Socket Name when empty.")]
        [SerializeField] private string combatMovementSocketName;

        [Header("Placement")]
        [SerializeField] private Vector3 localPosition;
        [SerializeField] private Vector3 localEulerAngles;
        [SerializeField] private Vector3 localScale = Vector3.one;

        public MMOEquipmentVisualBindingMode BindingMode => bindingMode;
        public MMOEquipmentSlotType EquipmentSlot => equipmentSlot;
        public MMOCharacterBodyPart BodyPart => bodyPart;
        public bool HideBaseBodyPart => hideBaseBodyPart;
        public GameObject ModelPrefab => modelPrefab;
        public GameObject StowedModelPrefab => stowedModelPrefab;
        public GameObject CombatMovementModelPrefab => combatMovementModelPrefab;
        public Material MaterialOverride => materialOverride;
        public bool UseColorOverride => useColorOverride;
        public Color ColorOverride => colorOverride;
        public Texture2D DiffuseTexture => diffuseTexture;
        public Texture2D NormalTexture => normalTexture;
        public string SocketName => string.IsNullOrWhiteSpace(socketName) ? "cc_weapon_r" : socketName;
        public string StowedSocketName => string.IsNullOrWhiteSpace(stowedSocketName) ? SocketName : stowedSocketName;
        public string CombatMovementSocketName => string.IsNullOrWhiteSpace(combatMovementSocketName) ? SocketName : combatMovementSocketName;
        public Vector3 LocalPosition => localPosition;
        public Vector3 LocalEulerAngles => localEulerAngles;
        public Vector3 LocalScale => localScale == Vector3.zero ? Vector3.one : localScale;
        public GameObject GetAttachmentModelPrefab(bool isInCombat)
        {
            return GetAttachmentModelPrefab(
                isInCombat
                    ? MMOEquipmentAttachmentPresentationState.Ready
                    : MMOEquipmentAttachmentPresentationState.Stowed);
        }

        public GameObject GetAttachmentModelPrefab(MMOEquipmentAttachmentPresentationState presentationState)
        {
            return presentationState switch
            {
                MMOEquipmentAttachmentPresentationState.Stowed when stowedModelPrefab != null => stowedModelPrefab,
                MMOEquipmentAttachmentPresentationState.CombatMovement when combatMovementModelPrefab != null => combatMovementModelPrefab,
                _ => modelPrefab
            };
        }

        public string GetAttachmentSocketName(MMOEquipmentAttachmentPresentationState presentationState)
        {
            return presentationState switch
            {
                MMOEquipmentAttachmentPresentationState.Stowed => StowedSocketName,
                MMOEquipmentAttachmentPresentationState.CombatMovement => CombatMovementSocketName,
                _ => SocketName
            };
        }

        public void Configure(
            MMOEquipmentSlotType newEquipmentSlot,
            MMOCharacterBodyPart newBodyPart,
            bool newHideBaseBodyPart,
            GameObject newModelPrefab,
            Material newMaterialOverride,
            bool newUseColorOverride,
            Color newColorOverride,
            Texture2D newDiffuseTexture,
            Texture2D newNormalTexture,
            Vector3 newLocalPosition,
            Vector3 newLocalEulerAngles,
            Vector3 newLocalScale)
        {
            bindingMode = MMOEquipmentVisualBindingMode.BodyPart;
            equipmentSlot = newEquipmentSlot;
            bodyPart = newBodyPart;
            hideBaseBodyPart = newHideBaseBodyPart;
            modelPrefab = newModelPrefab;
            stowedModelPrefab = null;
            combatMovementModelPrefab = null;
            materialOverride = newMaterialOverride;
            useColorOverride = newUseColorOverride;
            colorOverride = newColorOverride;
            diffuseTexture = newDiffuseTexture;
            normalTexture = newNormalTexture;
            localPosition = newLocalPosition;
            localEulerAngles = newLocalEulerAngles;
            localScale = newLocalScale == Vector3.zero ? Vector3.one : newLocalScale;
        }

        public void ConfigureAttachment(
            MMOEquipmentSlotType newEquipmentSlot,
            string newSocketName,
            string newStowedSocketName,
            GameObject newModelPrefab,
            GameObject newStowedModelPrefab,
            Vector3 newLocalPosition,
            Vector3 newLocalEulerAngles,
            Vector3 newLocalScale)
        {
            ConfigureAttachment(
                newEquipmentSlot,
                newSocketName,
                newStowedSocketName,
                string.Empty,
                newModelPrefab,
                newStowedModelPrefab,
                null,
                newLocalPosition,
                newLocalEulerAngles,
                newLocalScale);
        }

        public void ConfigureAttachment(
            MMOEquipmentSlotType newEquipmentSlot,
            string newSocketName,
            string newStowedSocketName,
            string newCombatMovementSocketName,
            GameObject newModelPrefab,
            GameObject newStowedModelPrefab,
            GameObject newCombatMovementModelPrefab,
            Vector3 newLocalPosition,
            Vector3 newLocalEulerAngles,
            Vector3 newLocalScale)
        {
            bindingMode = MMOEquipmentVisualBindingMode.AttachmentSocket;
            equipmentSlot = newEquipmentSlot;
            socketName = string.IsNullOrWhiteSpace(newSocketName) ? "cc_weapon_r" : newSocketName;
            stowedSocketName = string.IsNullOrWhiteSpace(newStowedSocketName) ? socketName : newStowedSocketName;
            combatMovementSocketName = string.IsNullOrWhiteSpace(newCombatMovementSocketName) ? socketName : newCombatMovementSocketName;
            modelPrefab = newModelPrefab;
            stowedModelPrefab = newStowedModelPrefab;
            combatMovementModelPrefab = newCombatMovementModelPrefab;
            localPosition = newLocalPosition;
            localEulerAngles = newLocalEulerAngles;
            localScale = newLocalScale == Vector3.zero ? Vector3.one : newLocalScale;
            hideBaseBodyPart = false;
            materialOverride = null;
            useColorOverride = false;
            diffuseTexture = null;
            normalTexture = null;
        }

        public void ConfigureAttachment(
            MMOEquipmentSlotType newEquipmentSlot,
            string newSocketName,
            GameObject newModelPrefab,
            Vector3 newLocalPosition,
            Vector3 newLocalEulerAngles,
            Vector3 newLocalScale)
        {
            ConfigureAttachment(
                newEquipmentSlot,
                newSocketName,
                string.Empty,
                newModelPrefab,
                null,
                newLocalPosition,
                newLocalEulerAngles,
                newLocalScale);
        }

        public void ConfigureAttachment(
            MMOEquipmentSlotType newEquipmentSlot,
            string newSocketName,
            string newStowedSocketName,
            GameObject newModelPrefab,
            Vector3 newLocalPosition,
            Vector3 newLocalEulerAngles,
            Vector3 newLocalScale)
        {
            ConfigureAttachment(
                newEquipmentSlot,
                newSocketName,
                newStowedSocketName,
                newModelPrefab,
                null,
                newLocalPosition,
                newLocalEulerAngles,
                newLocalScale);
        }
    }
}
