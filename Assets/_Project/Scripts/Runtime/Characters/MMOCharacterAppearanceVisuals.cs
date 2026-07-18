using System.Collections.Generic;
using RPGClone.Player;
using UnityEngine;

namespace RPGClone.Characters
{
    [DisallowMultipleComponent]
    public sealed class MMOCharacterAppearanceVisuals : MonoBehaviour
    {
        [SerializeField] private MMOCharacterAppearanceCatalog appearanceCatalog;
        [SerializeField] private string hairstyleId = "hair_1";

        private GameObject activeHairstyle;

        public string HairstyleId => appearanceCatalog != null
            ? appearanceCatalog.NormalizeHairstyleId(hairstyleId)
            : hairstyleId;

        private void OnEnable()
        {
            Rebuild();
        }

        private void OnDisable()
        {
            ClearActiveHairstyle();
        }

        public void Configure(MMOCharacterAppearanceCatalog newAppearanceCatalog, string newHairstyleId)
        {
            appearanceCatalog = newAppearanceCatalog;
            hairstyleId = appearanceCatalog != null
                ? appearanceCatalog.NormalizeHairstyleId(newHairstyleId)
                : newHairstyleId;
            Rebuild();
        }

        private void Rebuild()
        {
            ClearActiveHairstyle();
            MMOHairstyleDefinition hairstyle = appearanceCatalog != null
                ? appearanceCatalog.FindHairstyle(hairstyleId)
                : null;
            if (!isActiveAndEnabled || hairstyle?.ModelPrefab == null)
            {
                return;
            }

            Dictionary<string, Transform> liveSkeleton = MMOSkinnedVisualBindingUtility.BuildSkeletonLookup(
                transform,
                candidate => candidate != null
                    && (candidate.GetComponentInParent<MMOAppearanceVisualInstanceMarker>() != null
                        || candidate.GetComponentInParent<MMOEquipmentVisualInstanceMarker>() != null));
            activeHairstyle = Instantiate(hairstyle.ModelPrefab, transform);
            activeHairstyle.name = hairstyle.ModelPrefab.name;
            activeHairstyle.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            activeHairstyle.transform.localScale = Vector3.one;
            activeHairstyle.AddComponent<MMOAppearanceVisualInstanceMarker>();

            foreach (Animator importedAnimator in activeHairstyle.GetComponentsInChildren<Animator>(true))
            {
                importedAnimator.enabled = false;
            }

            foreach (Camera importedCamera in activeHairstyle.GetComponentsInChildren<Camera>(true))
            {
                importedCamera.enabled = false;
            }

            foreach (Light importedLight in activeHairstyle.GetComponentsInChildren<Light>(true))
            {
                importedLight.enabled = false;
            }

            bool reboundAnyRenderer = false;
            foreach (Renderer importedRenderer in activeHairstyle.GetComponentsInChildren<Renderer>(true))
            {
                if (importedRenderer is not SkinnedMeshRenderer skinnedRenderer)
                {
                    importedRenderer.enabled = false;
                    continue;
                }

                if (MMOSkinnedVisualBindingUtility.TryRebind(skinnedRenderer, liveSkeleton, out List<string> missingBoneNames))
                {
                    skinnedRenderer.enabled = true;
                    reboundAnyRenderer = true;
                }
                else
                {
                    skinnedRenderer.enabled = false;
                    Debug.LogWarning(
                        $"Hairstyle '{hairstyle.DisplayName}' could not bind '{skinnedRenderer.name}' to '{name}'. " +
                        $"Missing player bones: {string.Join(", ", missingBoneNames)}.",
                        this);
                }
            }

            if (!reboundAnyRenderer)
            {
                Debug.LogWarning($"Hairstyle '{hairstyle.DisplayName}' did not contain a compatible skinned renderer.", this);
            }
        }

        private void ClearActiveHairstyle()
        {
            if (activeHairstyle != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(activeHairstyle);
                }
                else
                {
                    DestroyImmediate(activeHairstyle);
                }

                activeHairstyle = null;
            }
        }
    }

    public sealed class MMOAppearanceVisualInstanceMarker : MonoBehaviour
    {
    }
}
