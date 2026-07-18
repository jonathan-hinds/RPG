using System.Collections.Generic;
using RPGClone.Characters;
using RPGClone.Inventory;
using RPGClone.Player;
using UnityEngine;

namespace RPGClone.CharacterSelection
{
    public static class MMOCharacterPreviewActor
    {
        public static GameObject Create(
            GameObject playerVisualPrefab,
            Transform parent,
            MMOPlayableRace race,
            MMOPlayableClass characterClass,
            IEnumerable<MMOItemDefinition> equipmentItems,
            MMOCharacterAppearanceCatalog appearanceCatalog,
            string hairstyleId,
            string headStyleId = null)
        {
            if (playerVisualPrefab == null || parent == null)
            {
                return null;
            }

            GameObject actor = Object.Instantiate(playerVisualPrefab, parent);
            actor.name = "Selected Character Preview";
            actor.tag = "Untagged";
            actor.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 180f, 0f));

            foreach (MonoBehaviour behaviour in actor.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour is not MMOPlayerEquipmentVisuals && behaviour is not MMOCharacterAppearanceVisuals)
                {
                    behaviour.enabled = false;
                }
            }

            foreach (CharacterController controller in actor.GetComponentsInChildren<CharacterController>(true))
            {
                controller.enabled = false;
            }

            MMOCharacterCustomization customization = actor.GetComponent<MMOCharacterCustomization>()
                ?? actor.AddComponent<MMOCharacterCustomization>();
            customization.Configure(race, characterClass);

            MMOCharacterEquipment equipment = actor.GetComponent<MMOCharacterEquipment>()
                ?? actor.AddComponent<MMOCharacterEquipment>();
            equipment.EnsureDefaultSlots();
            equipment.ClearEquipment(false);
            List<MMOItemDefinition> equippedPreviewItems = new();
            if (equipmentItems != null)
            {
                foreach (MMOItemDefinition item in equipmentItems)
                {
                    if (item != null && equipment.TryEquip(item))
                    {
                        equippedPreviewItems.Add(item);
                    }
                }
            }

            MMOPlayerEquipmentVisuals equipmentVisuals = actor.GetComponent<MMOPlayerEquipmentVisuals>();
            if (equipmentVisuals != null)
            {
                equipmentVisuals.Configure(equipment, null);
                equipmentVisuals.SetAttachmentPresentationOverride(MMOEquipmentAttachmentPresentationState.Ready);
                foreach (MMOItemDefinition item in equippedPreviewItems)
                {
                    if (item.IsShield && item.EquipmentVisual != null)
                    {
                        equipmentVisuals.SetAttachmentPresentationOverride(
                            item.EquipmentVisual,
                            MMOEquipmentAttachmentPresentationState.CombatMovement);
                    }
                }
            }

            MMOCharacterAppearanceVisuals appearanceVisuals = actor.GetComponent<MMOCharacterAppearanceVisuals>()
                ?? actor.AddComponent<MMOCharacterAppearanceVisuals>();
            appearanceVisuals.enabled = true;
            appearanceVisuals.Configure(appearanceCatalog, headStyleId, hairstyleId);

            foreach (Animator animator in actor.GetComponentsInChildren<Animator>(true))
            {
                if (animator.GetComponentInParent<MMOAppearanceVisualInstanceMarker>() != null
                    || animator.GetComponentInParent<MMOEquipmentVisualInstanceMarker>() != null)
                {
                    continue;
                }

                animator.enabled = true;
                animator.applyRootMotion = false;
                animator.Rebind();
                animator.Update(0f);
            }

            return actor;
        }
    }
}
