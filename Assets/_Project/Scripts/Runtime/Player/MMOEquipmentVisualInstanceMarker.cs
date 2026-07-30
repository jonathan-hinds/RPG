using RPGClone.Characters;
using RPGClone.Inventory;
using UnityEngine;

namespace RPGClone.Player
{
    [DisallowMultipleComponent]
    public sealed class MMOEquipmentVisualInstanceMarker : MonoBehaviour
    {
        [SerializeField] private MMOEquipmentVisualDefinition visualDefinition;
        [SerializeField] private MMOEquipmentSlotType equipmentSlot;
        [SerializeField] private MMOEquipmentAttachmentPresentationState presentationState;

        public MMOEquipmentVisualDefinition VisualDefinition => visualDefinition;
        public MMOEquipmentSlotType EquipmentSlot => equipmentSlot;
        public MMOEquipmentAttachmentPresentationState PresentationState => presentationState;

        public void Configure(
            MMOEquipmentVisualDefinition newVisualDefinition,
            MMOEquipmentAttachmentPresentationState newPresentationState)
        {
            visualDefinition = newVisualDefinition;
            equipmentSlot = newVisualDefinition != null
                ? newVisualDefinition.EquipmentSlot
                : default;
            presentationState = newPresentationState;
        }
    }
}
