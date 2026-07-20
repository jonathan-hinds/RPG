using System.Collections.Generic;
using RPGClone.Player;
using UnityEngine;

namespace RPGClone.Characters
{
    [DisallowMultipleComponent]
    public sealed class MMOCharacterAppearanceVisuals : MonoBehaviour
    {
        private static readonly int BaseMapPropertyId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexturePropertyId = Shader.PropertyToID("_MainTex");
        private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");

        [SerializeField] private MMOCharacterAppearanceCatalog appearanceCatalog;
        [SerializeField] private string headStyleId = "head_1";
        [SerializeField] private string faceId = "face_1";
        [SerializeField] private string hairstyleId = "hair_1";
        [SerializeField] private string hairColorId = "hair_black";

        private GameObject activeHeadStyle;
        private GameObject activeHairstyle;
        private bool headStyleBound;
        private Animator productionAnimator;
        private AnimatorCullingMode productionAnimatorOriginalCullingMode;
        private bool hasProductionAnimatorCullingOverride;

        public string HeadStyleId => appearanceCatalog != null
            ? appearanceCatalog.NormalizeHeadStyleId(headStyleId)
            : headStyleId;

        public string FaceId => appearanceCatalog != null
            ? appearanceCatalog.NormalizeFaceId(faceId)
            : faceId;

        public string HairstyleId => appearanceCatalog != null
            ? appearanceCatalog.NormalizeHairstyleId(hairstyleId)
            : hairstyleId;

        public string HairColorId => appearanceCatalog != null
            ? appearanceCatalog.NormalizeHairColorId(hairColorId)
            : hairColorId;

        private void OnEnable()
        {
            Rebuild();
        }

        private void OnDisable()
        {
            ClearGeneratedVisuals();
            headStyleBound = false;
            RefreshBaseHeadVisibility();
            RestoreProductionAnimatorCulling();
        }

        public void Configure(MMOCharacterAppearanceCatalog newAppearanceCatalog, string newHairstyleId)
        {
            Configure(
                newAppearanceCatalog,
                newAppearanceCatalog?.DefaultHeadStyleId,
                newAppearanceCatalog?.DefaultFaceId,
                newHairstyleId);
        }

        public void Configure(
            MMOCharacterAppearanceCatalog newAppearanceCatalog,
            string newHeadStyleId,
            string newHairstyleId)
        {
            Configure(newAppearanceCatalog, newHeadStyleId, newAppearanceCatalog?.DefaultFaceId, newHairstyleId);
        }

        public void Configure(
            MMOCharacterAppearanceCatalog newAppearanceCatalog,
            string newHeadStyleId,
            string newFaceId,
            string newHairstyleId)
        {
            Configure(
                newAppearanceCatalog,
                newHeadStyleId,
                newFaceId,
                newHairstyleId,
                newAppearanceCatalog?.DefaultHairColorId);
        }

        public void Configure(
            MMOCharacterAppearanceCatalog newAppearanceCatalog,
            string newHeadStyleId,
            string newFaceId,
            string newHairstyleId,
            string newHairColorId)
        {
            appearanceCatalog = newAppearanceCatalog;
            headStyleId = appearanceCatalog != null
                ? appearanceCatalog.NormalizeHeadStyleId(newHeadStyleId)
                : newHeadStyleId;
            faceId = appearanceCatalog != null
                ? appearanceCatalog.NormalizeFaceId(newFaceId)
                : newFaceId;
            hairstyleId = appearanceCatalog != null
                ? appearanceCatalog.NormalizeHairstyleId(newHairstyleId)
                : newHairstyleId;
            hairColorId = appearanceCatalog != null
                ? appearanceCatalog.NormalizeHairColorId(newHairColorId)
                : newHairColorId;
            Rebuild();
        }

        private void Rebuild()
        {
            ClearGeneratedVisuals();
            headStyleBound = false;

            if (!isActiveAndEnabled)
            {
                RefreshBaseHeadVisibility();
                return;
            }

            Dictionary<string, Transform> liveSkeleton = MMOSkinnedVisualBindingUtility.BuildSkeletonLookup(
                transform,
                candidate => candidate != null
                    && (candidate.GetComponentInParent<MMOAppearanceVisualInstanceMarker>() != null
                        || candidate.GetComponentInParent<MMOEquipmentVisualInstanceMarker>() != null));

            MMOHeadStyleDefinition headStyle = appearanceCatalog != null
                ? appearanceCatalog.FindHeadStyle(headStyleId)
                : null;
            MMOFaceDefinition face = appearanceCatalog != null
                ? appearanceCatalog.FindFace(faceId)
                : null;
            if (headStyle?.ModelPrefab != null)
            {
                headStyleBound = TryCreateSkinnedVisual(
                    headStyle.ModelPrefab,
                    headStyle.DisplayName,
                    liveSkeleton,
                    face?.AlbedoTexture,
                    null,
                    out activeHeadStyle);
            }

            RefreshBaseHeadVisibility();

            MMOHairstyleDefinition hairstyle = appearanceCatalog != null
                ? appearanceCatalog.FindHairstyle(hairstyleId)
                : null;
            MMOHairColorDefinition hairColor = appearanceCatalog != null
                ? appearanceCatalog.FindHairColor(hairColorId)
                : null;
            if (hairstyle?.ModelPrefab != null)
            {
                TryCreateSkinnedVisual(
                    hairstyle.ModelPrefab,
                    hairstyle.DisplayName,
                    liveSkeleton,
                    hairstyle.ColorMask,
                    hairColor?.Color ?? Color.white,
                    out activeHairstyle);
            }

            ApplyProductionAnimatorCullingPolicy();
        }

        public bool ReplacesBodyPart(MMOCharacterBodyPart bodyPart)
        {
            return bodyPart == MMOCharacterBodyPart.Head && headStyleBound && activeHeadStyle != null;
        }

        private bool TryCreateSkinnedVisual(
            GameObject modelPrefab,
            string displayName,
            IReadOnlyDictionary<string, Transform> liveSkeleton,
            Texture2D albedoOverride,
            Color? colorOverride,
            out GameObject instance)
        {
            instance = Instantiate(modelPrefab, transform);
            instance.name = modelPrefab.name;
            instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.transform.localScale = Vector3.one;
            instance.AddComponent<MMOAppearanceVisualInstanceMarker>();
            if (!Application.isPlaying)
            {
                instance.hideFlags |= HideFlags.DontSaveInEditor;
            }

            foreach (Animator importedAnimator in instance.GetComponentsInChildren<Animator>(true))
            {
                importedAnimator.enabled = false;
            }

            foreach (Camera importedCamera in instance.GetComponentsInChildren<Camera>(true))
            {
                importedCamera.enabled = false;
            }

            foreach (Light importedLight in instance.GetComponentsInChildren<Light>(true))
            {
                importedLight.enabled = false;
            }

            bool reboundAnyRenderer = false;
            foreach (Renderer importedRenderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (importedRenderer is not SkinnedMeshRenderer skinnedRenderer)
                {
                    importedRenderer.enabled = false;
                    continue;
                }

                if (MMOSkinnedVisualBindingUtility.TryRebind(skinnedRenderer, liveSkeleton, out List<string> missingBoneNames))
                {
                    ApplyCharacterSurface(skinnedRenderer);
                    ApplySurfaceOverrides(skinnedRenderer, albedoOverride, colorOverride);
                    skinnedRenderer.enabled = true;
                    reboundAnyRenderer = true;
                }
                else
                {
                    skinnedRenderer.enabled = false;
                    Debug.LogWarning(
                        $"Appearance style '{displayName}' could not bind '{skinnedRenderer.name}' to '{name}'. " +
                        $"Missing player bones: {string.Join(", ", missingBoneNames)}.",
                        this);
                }
            }

            if (!reboundAnyRenderer)
            {
                Debug.LogWarning($"Appearance style '{displayName}' did not contain a compatible skinned renderer.", this);
                ClearActiveVisual(ref instance);
            }

            return reboundAnyRenderer;
        }

        private static void ApplySurfaceOverrides(
            Renderer renderer,
            Texture2D albedoTexture,
            Color? colorOverride)
        {
            if (renderer == null || (albedoTexture == null && !colorOverride.HasValue))
            {
                return;
            }

            MaterialPropertyBlock propertyBlock = new();
            renderer.GetPropertyBlock(propertyBlock);
            if (albedoTexture != null)
            {
                propertyBlock.SetTexture(BaseMapPropertyId, albedoTexture);
                propertyBlock.SetTexture(MainTexturePropertyId, albedoTexture);
            }

            if (colorOverride.HasValue)
            {
                propertyBlock.SetColor(BaseColorPropertyId, colorOverride.Value);
                propertyBlock.SetColor(ColorPropertyId, colorOverride.Value);
            }

            renderer.SetPropertyBlock(propertyBlock);
        }

        private void ApplyCharacterSurface(Renderer renderer)
        {
            MMOPlayerEquipmentVisuals equipmentVisuals = GetComponent<MMOPlayerEquipmentVisuals>();
            if (equipmentVisuals != null)
            {
                equipmentVisuals.ApplyCharacterSurface(renderer);
            }
        }

        private void RefreshBaseHeadVisibility()
        {
            MMOPlayerEquipmentVisuals equipmentVisuals = GetComponent<MMOPlayerEquipmentVisuals>();
            if (equipmentVisuals != null)
            {
                equipmentVisuals.RefreshBaseBodyPartVisibility(MMOCharacterBodyPart.Head);
            }
        }

        private void ApplyProductionAnimatorCullingPolicy()
        {
            if (activeHeadStyle == null && activeHairstyle == null)
            {
                RestoreProductionAnimatorCulling();
                return;
            }

            productionAnimator = ResolveProductionAnimator();
            if (productionAnimator == null)
            {
                return;
            }

            if (!hasProductionAnimatorCullingOverride)
            {
                productionAnimatorOriginalCullingMode = productionAnimator.cullingMode;
                hasProductionAnimatorCullingOverride = true;
            }

            // Runtime appearance renderers are rebound to the production skeleton but
            // are not part of the Animator's originally imported renderer set. If all
            // authored body renderers are hidden by modular armor/appearance, Unity's
            // renderer-based culling can otherwise freeze the skeleton on frame zero.
            productionAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        private Animator ResolveProductionAnimator()
        {
            foreach (Animator candidate in GetComponentsInChildren<Animator>(true))
            {
                if (candidate != null
                    && candidate.GetComponentInParent<MMOAppearanceVisualInstanceMarker>() == null
                    && candidate.GetComponentInParent<MMOEquipmentVisualInstanceMarker>() == null)
                {
                    return candidate;
                }
            }

            return null;
        }

        private void RestoreProductionAnimatorCulling()
        {
            if (hasProductionAnimatorCullingOverride && productionAnimator != null)
            {
                productionAnimator.cullingMode = productionAnimatorOriginalCullingMode;
            }

            productionAnimator = null;
            hasProductionAnimatorCullingOverride = false;
        }

        private static void ClearActiveVisual(ref GameObject activeVisual)
        {
            if (activeVisual != null)
            {
                activeVisual.SetActive(false);
                if (Application.isPlaying)
                {
                    Destroy(activeVisual);
                }
                else
                {
                    DestroyImmediate(activeVisual);
                }

                activeVisual = null;
            }
        }

        private void ClearGeneratedVisuals()
        {
            // Remote avatars are cloned from the live local player, so generated
            // children can be inherited without their non-serialized owner fields.
            // Markers form the ownership boundary and make rebuilding idempotent.
            MMOAppearanceVisualInstanceMarker[] generatedVisuals =
                GetComponentsInChildren<MMOAppearanceVisualInstanceMarker>(true);
            foreach (MMOAppearanceVisualInstanceMarker generatedVisual in generatedVisuals)
            {
                if (generatedVisual == null || generatedVisual.transform == transform)
                {
                    continue;
                }

                GameObject visualObject = generatedVisual.gameObject;
                visualObject.SetActive(false);
                if (Application.isPlaying)
                {
                    Destroy(visualObject);
                }
                else
                {
                    DestroyImmediate(visualObject);
                }
            }

            activeHeadStyle = null;
            activeHairstyle = null;
        }
    }

    public sealed class MMOAppearanceVisualInstanceMarker : MonoBehaviour
    {
    }
}
