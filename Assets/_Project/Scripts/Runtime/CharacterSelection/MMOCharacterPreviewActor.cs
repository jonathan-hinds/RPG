using System.Collections.Generic;
using RPGClone.Characters;
using RPGClone.Inventory;
using RPGClone.Player;
using UnityEngine;

namespace RPGClone.CharacterSelection
{
    public static class MMOCharacterPreviewActor
    {
        public const float DefaultScaleMultiplier = 1.5f;

        public static GameObject Create(
            GameObject playerVisualPrefab,
            Transform parent,
            MMOPlayableRace race,
            MMOPlayableClass characterClass,
            IEnumerable<MMOItemDefinition> equipmentItems,
            MMOCharacterAppearanceCatalog appearanceCatalog,
            string hairstyleId,
            string headStyleId = null,
            string faceId = null,
            Camera previewCamera = null,
            float scaleMultiplier = DefaultScaleMultiplier)
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
            appearanceVisuals.Configure(appearanceCatalog, headStyleId, faceId, hairstyleId);

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

            ApplyPresentation(actor, previewCamera, scaleMultiplier);

            return actor;
        }

        private static void ApplyPresentation(GameObject actor, Camera previewCamera, float scaleMultiplier)
        {
            if (actor == null || !TryGetCharacterBounds(actor, out Bounds originalBounds))
            {
                return;
            }

            float resolvedScaleMultiplier = Mathf.Max(0.01f, scaleMultiplier);
            actor.transform.localScale *= resolvedScaleMultiplier;

            if (!TryGetCharacterBounds(actor, out Bounds scaledBounds))
            {
                return;
            }

            actor.transform.position += originalBounds.center - scaledBounds.center;
            if (previewCamera == null || !TryGetCharacterBounds(actor, out Bounds centeredBounds))
            {
                return;
            }

            float cameraDepth = Vector3.Dot(
                centeredBounds.center - previewCamera.transform.position,
                previewCamera.transform.forward);
            if (cameraDepth <= previewCamera.nearClipPlane)
            {
                return;
            }

            Vector3 viewportCenter = previewCamera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, cameraDepth));
            actor.transform.position += viewportCenter - centeredBounds.center;
        }

        private static bool TryGetCharacterBounds(GameObject actor, out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;
            foreach (Renderer renderer in actor.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null
                    || !renderer.enabled
                    || !renderer.gameObject.activeInHierarchy
                    || renderer.GetComponentInParent<MMOEquipmentVisualInstanceMarker>() != null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }
    }
}
