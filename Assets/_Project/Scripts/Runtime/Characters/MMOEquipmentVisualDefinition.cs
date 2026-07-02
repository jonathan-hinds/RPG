using RPGClone.Inventory;
using UnityEngine;

namespace RPGClone.Characters
{
    public enum MMOEquipmentVisualBindingMode
    {
        BodyPart,
        AttachmentSocket
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
        [SerializeField] private Material materialOverride;
        [SerializeField] private bool useColorOverride;
        [SerializeField] private Color colorOverride = Color.white;
        [SerializeField] private Texture2D diffuseTexture;
        [SerializeField] private Texture2D normalTexture;

        [Header("Attachment")]
        [Tooltip("Skeleton transform name used when Binding Mode is Attachment Socket.")]
        [SerializeField] private string socketName = "cc_weapon_r";

        [Header("Placement")]
        [SerializeField] private Vector3 localPosition;
        [SerializeField] private Vector3 localEulerAngles;
        [SerializeField] private Vector3 localScale = Vector3.one;

        public MMOEquipmentVisualBindingMode BindingMode => bindingMode;
        public MMOEquipmentSlotType EquipmentSlot => equipmentSlot;
        public MMOCharacterBodyPart BodyPart => bodyPart;
        public bool HideBaseBodyPart => hideBaseBodyPart;
        public GameObject ModelPrefab => modelPrefab;
        public Material MaterialOverride => materialOverride;
        public bool UseColorOverride => useColorOverride;
        public Color ColorOverride => colorOverride;
        public Texture2D DiffuseTexture => diffuseTexture;
        public Texture2D NormalTexture => normalTexture;
        public string SocketName => string.IsNullOrWhiteSpace(socketName) ? "cc_weapon_r" : socketName;
        public Vector3 LocalPosition => localPosition;
        public Vector3 LocalEulerAngles => localEulerAngles;
        public Vector3 LocalScale => localScale == Vector3.zero ? Vector3.one : localScale;

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
            GameObject newModelPrefab,
            Vector3 newLocalPosition,
            Vector3 newLocalEulerAngles,
            Vector3 newLocalScale)
        {
            bindingMode = MMOEquipmentVisualBindingMode.AttachmentSocket;
            equipmentSlot = newEquipmentSlot;
            socketName = string.IsNullOrWhiteSpace(newSocketName) ? "cc_weapon_r" : newSocketName;
            modelPrefab = newModelPrefab;
            localPosition = newLocalPosition;
            localEulerAngles = newLocalEulerAngles;
            localScale = newLocalScale == Vector3.zero ? Vector3.one : newLocalScale;
            hideBaseBodyPart = false;
            materialOverride = null;
            useColorOverride = false;
            diffuseTexture = null;
            normalTexture = null;
        }
    }
}
