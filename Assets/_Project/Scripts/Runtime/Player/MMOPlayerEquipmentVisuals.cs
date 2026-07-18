using System;
using System.Collections.Generic;
using RPGClone.Characters;
using RPGClone.Combat;
using RPGClone.Inventory;
using UnityEngine;

namespace RPGClone.Player
{
    [DisallowMultipleComponent]
    public sealed class MMOPlayerEquipmentVisuals : MonoBehaviour
    {
        private const string EditorOnlyTag = "EditorOnly";

        [SerializeField] private MMOCharacterEquipment equipment;
        [SerializeField] private MMOCombatant combatant;
        [SerializeField] private List<MMOBodyPartRendererSlot> bodyPartSlots = new();
        [Header("Attachment Presentation")]
        [SerializeField, Min(0f)] private float attachmentMovementSpeedThreshold = 0.05f;

        private readonly List<GameObject> activeVisualInstances = new();
        private readonly List<Material> activeMaterialInstances = new();
        private readonly Dictionary<Renderer, Material[]> originalMaterials = new();
        private MMOCharacterEquipment subscribedEquipment;
        private MMOCombatant subscribedCombatant;
        private IMMOPlayerLocomotionSource locomotionSource;
        private int lastEquipmentSignature;
        private bool hasAttachmentPresentationOverride;
        private MMOEquipmentAttachmentPresentationState attachmentPresentationOverride;
        private readonly Dictionary<MMOEquipmentVisualDefinition, MMOEquipmentAttachmentPresentationState>
            attachmentPresentationOverridesByVisual = new();
        private readonly HashSet<MMOCharacterBodyPart> equipmentHiddenBodyParts = new();

        private void Awake()
        {
            EnsureReferences();
            EnsureBodyPartSlots();
            CacheOriginalMaterials();
        }

        private void OnEnable()
        {
            EnsureReferences();
            EnsureBodyPartSlots();
            CacheOriginalMaterials();

            SubscribeToEquipment();
            SubscribeToCombatant();
            RebuildEquipmentVisuals();
        }

        private void OnDisable()
        {
            if (subscribedEquipment != null)
            {
                subscribedEquipment.Changed -= OnEquipmentChanged;
                subscribedEquipment = null;
            }

            if (subscribedCombatant != null)
            {
                subscribedCombatant.CombatStateChanged -= OnCombatStateChanged;
                subscribedCombatant = null;
            }

            ClearRuntimeVisuals();
            equipmentHiddenBodyParts.Clear();
            RestoreBaseBody();
        }

        private void LateUpdate()
        {
            EnsureReferences();
            SubscribeToEquipment();
            SubscribeToCombatant();
            int equipmentSignature = CalculateEquipmentSignature();
            if (equipmentSignature != lastEquipmentSignature)
            {
                RebuildEquipmentVisuals();
            }
        }

        public void Configure(MMOCharacterEquipment newEquipment, IEnumerable<MMOBodyPartRendererSlot> newBodyPartSlots)
        {
            if (subscribedEquipment != null)
            {
                subscribedEquipment.Changed -= OnEquipmentChanged;
                subscribedEquipment = null;
            }

            equipment = newEquipment;
            combatant = GetComponent<MMOCombatant>();
            locomotionSource = GetComponent<MMOPlayerMotor>();
            // A null collection means "keep the prefab's authored bindings". Preview
            // actors only replace the equipment source; clearing these bindings makes
            // the automatic fallback discover already-instantiated gear as body parts.
            if (newBodyPartSlots != null)
            {
                bodyPartSlots = new List<MMOBodyPartRendererSlot>(newBodyPartSlots);
            }
            EnsureBodyPartSlots();
            CacheOriginalMaterials();
            SubscribeToEquipment();
            SubscribeToCombatant();
            RebuildEquipmentVisuals();
        }

        public void ApplyCharacterSurface(Renderer renderer)
        {
            MMOCharacterUnlitMaterialUtility.ApplySurface(renderer);
        }

        public void ApplyCharacterSurfaces(IEnumerable<Renderer> renderers)
        {
            if (renderers == null)
            {
                return;
            }

            foreach (Renderer renderer in renderers)
            {
                ApplyCharacterSurface(renderer);
            }
        }

        public void SetAttachmentPresentationOverride(MMOEquipmentAttachmentPresentationState? presentationState)
        {
            hasAttachmentPresentationOverride = presentationState.HasValue;
            attachmentPresentationOverride = presentationState.GetValueOrDefault();
            RebuildEquipmentVisuals();
        }

        public void SetAttachmentPresentationOverride(
            MMOEquipmentVisualDefinition visualDefinition,
            MMOEquipmentAttachmentPresentationState? presentationState)
        {
            if (visualDefinition == null)
            {
                return;
            }

            if (presentationState.HasValue)
            {
                attachmentPresentationOverridesByVisual[visualDefinition] = presentationState.Value;
            }
            else
            {
                attachmentPresentationOverridesByVisual.Remove(visualDefinition);
            }

            RebuildEquipmentVisuals();
        }

        private void OnEquipmentChanged(MMOCharacterEquipment changedEquipment)
        {
            if (changedEquipment == equipment)
            {
                RebuildEquipmentVisuals();
            }
        }

        private void OnCombatStateChanged(MMOCombatant changedCombatant, bool isInCombat)
        {
            if (changedCombatant == combatant)
            {
                RebuildEquipmentVisuals();
            }
        }

        private void RebuildEquipmentVisuals()
        {
            EnsureReferences();
            EnsureBodyPartSlots();
            CacheOriginalMaterials();
            ClearRuntimeVisuals();
            equipmentHiddenBodyParts.Clear();
            RestoreBaseBody();
            lastEquipmentSignature = CalculateEquipmentSignature();

            if (equipment == null)
            {
                return;
            }

            Dictionary<string, Transform> liveSkeleton = BuildLiveSkeletonLookup();
            foreach (MMOEquippedItemSlot equippedItem in equipment.EquippedItems)
            {
                MMOEquipmentVisualDefinition visualDefinition = equippedItem?.Item != null
                    ? equippedItem.Item.EquipmentVisual
                    : null;
                if (visualDefinition != null && IsVisualCompatibleWithSlot(visualDefinition, equippedItem.SlotType))
                {
                    ApplyVisualDefinition(visualDefinition, liveSkeleton);
                }
            }
        }

        private void ApplyVisualDefinition(
            MMOEquipmentVisualDefinition visualDefinition,
            IReadOnlyDictionary<string, Transform> liveSkeleton)
        {
            if (visualDefinition.BindingMode == MMOEquipmentVisualBindingMode.AttachmentSocket)
            {
                ApplyAttachmentVisualDefinition(visualDefinition, liveSkeleton);
                return;
            }

            MMOBodyPartRendererSlot slot = FindSlot(visualDefinition.BodyPart);
            if (slot == null)
            {
                return;
            }

            if (visualDefinition.ModelPrefab == null)
            {
                ApplyMaterialOverride(slot.Renderers, visualDefinition);

                if (visualDefinition.HideBaseBodyPart)
                {
                    SetBaseBodyPartHidden(slot.BodyPart, true);
                }

                return;
            }

            Transform anchor = slot.Anchor != null ? slot.Anchor : transform;
            GameObject instance = Instantiate(visualDefinition.ModelPrefab, anchor);
            instance.name = visualDefinition.ModelPrefab.name;
            instance.transform.localPosition = visualDefinition.LocalPosition;
            instance.transform.localRotation = Quaternion.Euler(visualDefinition.LocalEulerAngles);
            instance.transform.localScale = visualDefinition.LocalScale;
            MarkRuntimeVisual(instance);
            StripEditorOnlyChildren(instance);

            if (!PrepareSkinnedBodyPartVisual(instance, liveSkeleton, visualDefinition))
            {
                DestroyVisualObject(instance);
                return;
            }

            if (visualDefinition.HideBaseBodyPart)
            {
                SetBaseBodyPartHidden(slot.BodyPart, true);
            }

            activeVisualInstances.Add(instance);
        }

        private Dictionary<string, Transform> BuildLiveSkeletonLookup()
        {
            return MMOSkinnedVisualBindingUtility.BuildSkeletonLookup(
                transform,
                candidate => candidate == null
                    || candidate.GetComponentInParent<MMOEquipmentVisualInstanceMarker>() != null
                    || candidate.GetComponentInParent<MMOAppearanceVisualInstanceMarker>() != null);
        }

        private bool PrepareSkinnedBodyPartVisual(
            GameObject instance,
            IReadOnlyDictionary<string, Transform> liveSkeleton,
            MMOEquipmentVisualDefinition visualDefinition)
        {
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

            foreach (Renderer importedRenderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (importedRenderer is not SkinnedMeshRenderer)
                {
                    importedRenderer.enabled = false;
                }
            }

            bool reboundAnyRenderer = false;
            foreach (SkinnedMeshRenderer skinnedRenderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (TryRebindSkinnedRenderer(skinnedRenderer, liveSkeleton, visualDefinition))
                {
                    ApplyMaterialOverride(new Renderer[] { skinnedRenderer }, visualDefinition);
                    ApplyCharacterSurface(skinnedRenderer);
                    skinnedRenderer.enabled = true;
                    reboundAnyRenderer = true;
                }
                else
                {
                    skinnedRenderer.enabled = false;
                }
            }

            if (!reboundAnyRenderer)
            {
                Debug.LogWarning(
                    $"Equipment visual '{visualDefinition.name}' did not contain a skinned mesh that could bind to '{name}'. " +
                    "The base body part will remain visible.",
                    this);
            }

            return reboundAnyRenderer;
        }

        private bool TryRebindSkinnedRenderer(
            SkinnedMeshRenderer skinnedRenderer,
            IReadOnlyDictionary<string, Transform> liveSkeleton,
            MMOEquipmentVisualDefinition visualDefinition)
        {
            if (!MMOSkinnedVisualBindingUtility.TryRebind(skinnedRenderer, liveSkeleton, out List<string> missingBoneNames))
            {
                Debug.LogWarning(
                    $"Equipment visual '{visualDefinition.name}' could not bind skinned mesh '{skinnedRenderer.name}'. " +
                    $"Missing player bones: {string.Join(", ", missingBoneNames)}.",
                    this);
                return false;
            }
            return true;
        }

        private void ApplyAttachmentVisualDefinition(
            MMOEquipmentVisualDefinition visualDefinition,
            IReadOnlyDictionary<string, Transform> liveSkeleton)
        {
            MMOEquipmentAttachmentPresentationState presentationState = ResolveAttachmentPresentationState(visualDefinition);
            GameObject modelPrefab = visualDefinition.GetAttachmentModelPrefab(presentationState);
            if (modelPrefab == null)
            {
                return;
            }

            string socketName = visualDefinition.GetAttachmentSocketName(presentationState);
            Transform socket = FindLiveSkeletonTransform(liveSkeleton, socketName);
            if (socket == null)
            {
                Debug.LogWarning(
                    $"Equipment visual '{visualDefinition.name}' could not find attachment socket '{socketName}' " +
                    $"on the live skeleton under '{name}'.",
                    this);
                return;
            }

            GameObject instance = Instantiate(modelPrefab, socket);
            instance.name = modelPrefab.name;
            instance.transform.localPosition = visualDefinition.LocalPosition;
            instance.transform.localRotation = Quaternion.Euler(visualDefinition.LocalEulerAngles);
            instance.transform.localScale = visualDefinition.LocalScale;
            MarkRuntimeVisual(instance);
            StripEditorOnlyChildren(instance);
            ApplyAttachmentSurfacePolicy(instance);
            activeVisualInstances.Add(instance);
        }

        private void ApplyAttachmentSurfacePolicy(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            // Trail, line, and particle renderers retain their purpose-built VFX
            // materials. Visible weapon and shield geometry uses mesh renderers.
            MMOCharacterUnlitMaterialUtility.ApplyVisibleMeshSurfaces(instance.transform);
        }

        private static void MarkRuntimeVisual(GameObject instance)
        {
            if (instance != null && instance.GetComponent<MMOEquipmentVisualInstanceMarker>() == null)
            {
                instance.AddComponent<MMOEquipmentVisualInstanceMarker>();
            }
        }

        private static void StripEditorOnlyChildren(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            Transform[] children = instance.GetComponentsInChildren<Transform>(true);
            for (int i = children.Length - 1; i >= 0; i--)
            {
                Transform child = children[i];
                if (child != null && child != instance.transform && child.CompareTag(EditorOnlyTag))
                {
                    DestroyVisualObject(child.gameObject);
                }
            }
        }

        private MMOBodyPartRendererSlot FindSlot(MMOCharacterBodyPart bodyPart)
        {
            foreach (MMOBodyPartRendererSlot slot in bodyPartSlots)
            {
                if (slot != null && slot.BodyPart == bodyPart)
                {
                    return slot;
                }
            }

            return null;
        }

        public void RefreshBaseBodyPartVisibility(MMOCharacterBodyPart bodyPart)
        {
            EnsureBodyPartSlots();
            MMOBodyPartRendererSlot slot = FindSlot(bodyPart);
            if (slot != null)
            {
                SetRenderersEnabled(slot.Renderers, !ShouldHideBaseBodyPart(bodyPart));
            }
        }

        private void SetBaseBodyPartHidden(MMOCharacterBodyPart bodyPart, bool hidden)
        {
            if (hidden)
            {
                equipmentHiddenBodyParts.Add(bodyPart);
            }
            else
            {
                equipmentHiddenBodyParts.Remove(bodyPart);
            }

            RefreshBaseBodyPartVisibility(bodyPart);
        }

        private bool ShouldHideBaseBodyPart(MMOCharacterBodyPart bodyPart)
        {
            if (equipmentHiddenBodyParts.Contains(bodyPart))
            {
                return true;
            }

            MMOCharacterAppearanceVisuals appearanceVisuals = GetComponent<MMOCharacterAppearanceVisuals>();
            return appearanceVisuals != null && appearanceVisuals.ReplacesBodyPart(bodyPart);
        }

        private void RestoreBaseBody()
        {
            foreach (MMOBodyPartRendererSlot slot in bodyPartSlots)
            {
                if (slot == null)
                {
                    continue;
                }

                SetRenderersEnabled(slot.Renderers, !ShouldHideBaseBodyPart(slot.BodyPart));
                foreach (Renderer renderer in slot.Renderers)
                {
                    if (renderer != null && originalMaterials.TryGetValue(renderer, out Material[] materials))
                    {
                        renderer.sharedMaterials = materials;
                    }
                    ApplyCharacterSurface(renderer);
                }
            }
        }

        private void ApplyMaterialOverride(Renderer[] renderers, MMOEquipmentVisualDefinition visualDefinition)
        {
            if (renderers == null || renderers.Length == 0)
            {
                return;
            }

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                Material[] sourceMaterials = renderer.sharedMaterials;
                Material[] replacementMaterials = new Material[sourceMaterials.Length];
                for (int i = 0; i < sourceMaterials.Length; i++)
                {
                    replacementMaterials[i] = CreateReplacementMaterial(sourceMaterials[i], visualDefinition);
                }

                renderer.sharedMaterials = replacementMaterials;
            }
        }

        private Material CreateReplacementMaterial(Material sourceMaterial, MMOEquipmentVisualDefinition visualDefinition)
        {
            bool hasTextureOverride = visualDefinition.DiffuseTexture != null || visualDefinition.NormalTexture != null;
            bool hasColorOverride = visualDefinition.UseColorOverride;
            Material baseMaterial = visualDefinition.MaterialOverride != null ? visualDefinition.MaterialOverride : sourceMaterial;
            if (!hasTextureOverride && !hasColorOverride)
            {
                return MMOCharacterUnlitMaterialUtility.GetOrCreateSharedVariant(baseMaterial);
            }

            Material materialInstance = baseMaterial != null
                ? new Material(baseMaterial)
                : new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            if (hasColorOverride)
            {
                SetColorIfPresent(materialInstance, "_BaseColor", visualDefinition.ColorOverride);
                SetColorIfPresent(materialInstance, "_Color", visualDefinition.ColorOverride);
                if (visualDefinition.DiffuseTexture == null)
                {
                    SetTextureIfPresent(materialInstance, "_BaseMap", null);
                    SetTextureIfPresent(materialInstance, "_MainTex", null);
                }
            }

            SetTextureIfPresent(materialInstance, "_BaseMap", visualDefinition.DiffuseTexture);
            SetTextureIfPresent(materialInstance, "_MainTex", visualDefinition.DiffuseTexture);
            SetTextureIfPresent(materialInstance, "_BumpMap", visualDefinition.NormalTexture);
            if (visualDefinition.NormalTexture != null)
            {
                materialInstance.EnableKeyword("_NORMALMAP");
            }

            MMOCharacterUnlitMaterialUtility.ConvertToUnlit(materialInstance);
            activeMaterialInstances.Add(materialInstance);
            return materialInstance;
        }

        private static bool IsVisualCompatibleWithSlot(MMOEquipmentVisualDefinition visualDefinition, MMOEquipmentSlotType equippedSlot)
        {
            return visualDefinition == null || visualDefinition.EquipmentSlot == equippedSlot;
        }

        private static void SetColorIfPresent(Material material, string propertyName, Color color)
        {
            if (material != null && material.HasProperty(propertyName))
            {
                material.SetColor(propertyName, color);
            }
        }

        private static void SetTextureIfPresent(Material material, string propertyName, Texture texture)
        {
            if (material != null && material.HasProperty(propertyName))
            {
                material.SetTexture(propertyName, texture);
            }
        }

        private void ClearRuntimeVisuals()
        {
            for (int i = activeVisualInstances.Count - 1; i >= 0; i--)
            {
                GameObject instance = activeVisualInstances[i];
                if (instance != null)
                {
                    DestroyVisualObject(instance);
                }
            }

            activeVisualInstances.Clear();
            ClearOrphanRuntimeVisuals();

            for (int i = activeMaterialInstances.Count - 1; i >= 0; i--)
            {
                Material material = activeMaterialInstances[i];
                if (material != null)
                {
                    DestroyVisualObject(material);
                }
            }

            activeMaterialInstances.Clear();
        }

        private void ClearOrphanRuntimeVisuals()
        {
            MMOEquipmentVisualInstanceMarker[] markers = GetComponentsInChildren<MMOEquipmentVisualInstanceMarker>(true);
            for (int i = markers.Length - 1; i >= 0; i--)
            {
                if (markers[i] != null)
                {
                    DestroyVisualObject(markers[i].gameObject);
                }
            }
        }

        private static void DestroyVisualObject(UnityEngine.Object visualObject)
        {
            if (visualObject == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(visualObject);
            }
            else
            {
                DestroyImmediate(visualObject);
            }
        }

        private void EnsureBodyPartSlots()
        {
            if (HasUsableBodyPartSlots())
            {
                return;
            }

            Dictionary<MMOCharacterBodyPart, List<Renderer>> renderersByPart = new();
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                {
                    continue;
                }

                MMOCharacterBodyPart bodyPart = ResolveBodyPart(renderer);
                if (!renderersByPart.TryGetValue(bodyPart, out List<Renderer> renderers))
                {
                    renderers = new List<Renderer>();
                    renderersByPart[bodyPart] = renderers;
                }

                renderers.Add(renderer);
            }

            bodyPartSlots = new List<MMOBodyPartRendererSlot>();
            foreach (KeyValuePair<MMOCharacterBodyPart, List<Renderer>> pair in renderersByPart)
            {
                Renderer firstRenderer = pair.Value.Count > 0 ? pair.Value[0] : null;
                Transform anchor = firstRenderer != null ? firstRenderer.transform : transform;
                bodyPartSlots.Add(new MMOBodyPartRendererSlot(pair.Key, anchor, pair.Value.ToArray()));
            }
        }

        private bool HasUsableBodyPartSlots()
        {
            if (bodyPartSlots == null || bodyPartSlots.Count == 0)
            {
                return false;
            }

            foreach (MMOBodyPartRendererSlot slot in bodyPartSlots)
            {
                if (slot == null || !slot.HasRendererBinding)
                {
                    return false;
                }
            }

            return true;
        }

        private void CacheOriginalMaterials()
        {
            originalMaterials.Clear();
            foreach (MMOBodyPartRendererSlot slot in bodyPartSlots)
            {
                if (slot == null || slot.Renderers == null)
                {
                    continue;
                }

                foreach (Renderer renderer in slot.Renderers)
                {
                    if (renderer != null)
                    {
                        originalMaterials[renderer] = renderer.sharedMaterials;
                        ApplyCharacterSurface(renderer);
                    }
                }
            }
        }

        private static MMOCharacterBodyPart ResolveBodyPart(Renderer renderer)
        {
            if (TryResolveBodyPartName(renderer != null ? renderer.name : null, out MMOCharacterBodyPart rendererPart))
            {
                return rendererPart;
            }

            if (renderer != null)
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    Texture texture = material != null ? material.mainTexture : null;
                    if (TryResolveBodyPartName(texture != null ? texture.name : null, out MMOCharacterBodyPart texturePart))
                    {
                        return texturePart;
                    }

                    if (TryResolveBodyPartName(material != null ? material.name : null, out MMOCharacterBodyPart materialPart))
                    {
                        return materialPart;
                    }
                }
            }

            return MMOCharacterBodyPart.Torso;
        }

        private static bool TryResolveBodyPartName(string candidate, out MMOCharacterBodyPart bodyPart)
        {
            string normalizedName = NormalizeName(candidate);
            if (normalizedName.Contains("head"))
            {
                bodyPart = MMOCharacterBodyPart.Head;
                return true;
            }

            if (normalizedName.Contains("hand"))
            {
                bodyPart = MMOCharacterBodyPart.Hands;
                return true;
            }

            if (normalizedName.Contains("torso") || normalizedName.Contains("chest"))
            {
                bodyPart = MMOCharacterBodyPart.Torso;
                return true;
            }

            if (normalizedName.Contains("leg"))
            {
                bodyPart = MMOCharacterBodyPart.Legs;
                return true;
            }

            if (normalizedName.Contains("feet") || normalizedName.Contains("foot") || normalizedName.Contains("boot"))
            {
                bodyPart = MMOCharacterBodyPart.Feet;
                return true;
            }

            bodyPart = MMOCharacterBodyPart.Torso;
            return false;
        }

        private static string NormalizeName(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Replace(" ", string.Empty)
                    .Replace("-", string.Empty)
                    .Replace("_", string.Empty)
                    .Replace(".", string.Empty)
                    .ToLowerInvariant();
        }

        private static Transform FindLiveSkeletonTransform(
            IReadOnlyDictionary<string, Transform> liveSkeleton,
            string transformName)
        {
            if (liveSkeleton == null || string.IsNullOrWhiteSpace(transformName))
            {
                return null;
            }

            if (liveSkeleton.TryGetValue(transformName, out Transform exactMatch) && exactMatch != null)
            {
                return exactMatch;
            }

            string normalizedTransformName = NormalizeName(transformName);
            foreach (KeyValuePair<string, Transform> candidate in liveSkeleton)
            {
                if (candidate.Value != null && NormalizeName(candidate.Key) == normalizedTransformName)
                {
                    return candidate.Value;
                }
            }

            return null;
        }

        private void EnsureReferences()
        {
            if (equipment == null)
            {
                equipment = GetComponent<MMOCharacterEquipment>();
            }

            if (combatant == null)
            {
                combatant = GetComponent<MMOCombatant>();
            }

            if (!IsLocomotionSourceAvailable(locomotionSource))
            {
                locomotionSource = ResolveLocomotionSource();
            }
        }

        private void SubscribeToEquipment()
        {
            if (subscribedEquipment == equipment)
            {
                return;
            }

            if (subscribedEquipment != null)
            {
                subscribedEquipment.Changed -= OnEquipmentChanged;
            }

            subscribedEquipment = equipment;
            if (subscribedEquipment != null)
            {
                subscribedEquipment.Changed -= OnEquipmentChanged;
                subscribedEquipment.Changed += OnEquipmentChanged;
            }
        }

        private void SubscribeToCombatant()
        {
            if (subscribedCombatant == combatant)
            {
                return;
            }

            if (subscribedCombatant != null)
            {
                subscribedCombatant.CombatStateChanged -= OnCombatStateChanged;
            }

            subscribedCombatant = combatant;
            if (subscribedCombatant != null)
            {
                subscribedCombatant.CombatStateChanged -= OnCombatStateChanged;
                subscribedCombatant.CombatStateChanged += OnCombatStateChanged;
            }
        }

        private int CalculateEquipmentSignature()
        {
            MMOEquipmentAttachmentPresentationState presentationState = ResolveAttachmentPresentationState();
            int combatState = (int)presentationState;
            if (equipment == null)
            {
                return combatState;
            }

            unchecked
            {
                int hash = 17;
                hash = hash * 31 + combatState;
                foreach (MMOEquippedItemSlot equippedItem in equipment.EquippedItems)
                {
                    if (equippedItem == null)
                    {
                        continue;
                    }

                    hash = hash * 31 + (int)equippedItem.SlotType;
                    hash = hash * 31 + (equippedItem.Item != null ? equippedItem.Item.ItemId.GetHashCode() : 0);
                    hash = hash * 31 + (int)ResolveAttachmentPresentationState(equippedItem.Item?.EquipmentVisual);
                }

                return hash;
            }
        }

        private bool IsInCombat()
        {
            return combatant != null && combatant.IsInCombat;
        }

        private MMOEquipmentAttachmentPresentationState ResolveAttachmentPresentationState(
            MMOEquipmentVisualDefinition visualDefinition = null)
        {
            if (visualDefinition != null
                && attachmentPresentationOverridesByVisual.TryGetValue(visualDefinition, out MMOEquipmentAttachmentPresentationState visualOverride))
            {
                return visualOverride;
            }

            if (hasAttachmentPresentationOverride)
            {
                return attachmentPresentationOverride;
            }

            return MMOEquipmentAttachmentPresentationResolver.Resolve(
                IsInCombat(),
                locomotionSource != null && locomotionSource.IsAirborne,
                locomotionSource != null ? locomotionSource.CurrentPlanarSpeed : 0f,
                attachmentMovementSpeedThreshold);
        }

        private IMMOPlayerLocomotionSource ResolveLocomotionSource()
        {
            MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IMMOPlayerLocomotionSource source && behaviours[i].isActiveAndEnabled)
                {
                    return source;
                }
            }

            return null;
        }

        private static bool IsLocomotionSourceAvailable(IMMOPlayerLocomotionSource source)
        {
            return source is MonoBehaviour behaviour && behaviour != null && behaviour.isActiveAndEnabled;
        }

        private static void SetRenderersEnabled(Renderer[] renderers, bool enabled)
        {
            if (renderers == null)
            {
                return;
            }

            foreach (Renderer renderer in renderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = enabled;
                }
            }
        }

        private void OnValidate()
        {
            EnsureReferences();
        }
    }

    [Serializable]
    public sealed class MMOBodyPartRendererSlot
    {
        [SerializeField] private MMOCharacterBodyPart bodyPart;
        [SerializeField] private Transform anchor;
        [SerializeField] private Renderer[] renderers = Array.Empty<Renderer>();

        public MMOCharacterBodyPart BodyPart => bodyPart;
        public Transform Anchor => anchor;
        public Renderer[] Renderers => renderers ?? Array.Empty<Renderer>();
        public bool HasRendererBinding
        {
            get
            {
                foreach (Renderer renderer in Renderers)
                {
                    if (renderer != null)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public MMOBodyPartRendererSlot(MMOCharacterBodyPart bodyPart, Transform anchor, Renderer[] renderers)
        {
            this.bodyPart = bodyPart;
            this.anchor = anchor;
            this.renderers = renderers ?? Array.Empty<Renderer>();
        }
    }
}
