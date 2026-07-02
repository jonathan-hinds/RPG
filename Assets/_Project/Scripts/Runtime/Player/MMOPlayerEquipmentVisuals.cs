using System;
using System.Collections.Generic;
using RPGClone.Characters;
using RPGClone.Inventory;
using UnityEngine;

namespace RPGClone.Player
{
    [DisallowMultipleComponent]
    public sealed class MMOPlayerEquipmentVisuals : MonoBehaviour
    {
        private const string EditorOnlyTag = "EditorOnly";

        [SerializeField] private MMOCharacterEquipment equipment;
        [SerializeField] private List<MMOBodyPartRendererSlot> bodyPartSlots = new();

        private readonly List<GameObject> activeVisualInstances = new();
        private readonly List<Material> activeMaterialInstances = new();
        private readonly Dictionary<Renderer, Material[]> originalMaterials = new();
        private MMOCharacterEquipment subscribedEquipment;
        private int lastEquipmentSignature;

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
            RebuildEquipmentVisuals();
        }

        private void OnDisable()
        {
            if (subscribedEquipment != null)
            {
                subscribedEquipment.Changed -= OnEquipmentChanged;
                subscribedEquipment = null;
            }

            ClearRuntimeVisuals();
            RestoreBaseBody();
        }

        private void LateUpdate()
        {
            EnsureReferences();
            SubscribeToEquipment();
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
            bodyPartSlots = newBodyPartSlots != null
                ? new List<MMOBodyPartRendererSlot>(newBodyPartSlots)
                : new List<MMOBodyPartRendererSlot>();
            EnsureBodyPartSlots();
            CacheOriginalMaterials();
            SubscribeToEquipment();
            RebuildEquipmentVisuals();
        }

        private void OnEquipmentChanged(MMOCharacterEquipment changedEquipment)
        {
            if (changedEquipment == equipment)
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
            RestoreBaseBody();
            lastEquipmentSignature = CalculateEquipmentSignature();

            if (equipment == null)
            {
                return;
            }

            foreach (MMOEquippedItemSlot equippedItem in equipment.EquippedItems)
            {
                MMOEquipmentVisualDefinition visualDefinition = equippedItem?.Item != null
                    ? equippedItem.Item.EquipmentVisual
                    : null;
                if (visualDefinition != null && IsVisualCompatibleWithSlot(visualDefinition, equippedItem.SlotType))
                {
                    ApplyVisualDefinition(visualDefinition);
                }
            }
        }

        private void ApplyVisualDefinition(MMOEquipmentVisualDefinition visualDefinition)
        {
            if (visualDefinition.BindingMode == MMOEquipmentVisualBindingMode.AttachmentSocket)
            {
                ApplyAttachmentVisualDefinition(visualDefinition);
                return;
            }

            MMOBodyPartRendererSlot slot = FindSlot(visualDefinition.BodyPart);
            if (slot == null)
            {
                return;
            }

            if (visualDefinition.HideBaseBodyPart)
            {
                SetRenderersEnabled(slot.Renderers, false);
            }

            ApplyMaterialOverride(slot.Renderers, visualDefinition);

            if (visualDefinition.ModelPrefab == null)
            {
                return;
            }

            Transform anchor = slot.Anchor != null ? slot.Anchor : transform;
            GameObject instance = Instantiate(visualDefinition.ModelPrefab, anchor);
            instance.name = visualDefinition.ModelPrefab.name;
            instance.transform.localPosition = visualDefinition.LocalPosition;
            instance.transform.localRotation = Quaternion.Euler(visualDefinition.LocalEulerAngles);
            instance.transform.localScale = visualDefinition.LocalScale;
            StripEditorOnlyChildren(instance);
            activeVisualInstances.Add(instance);
        }

        private void ApplyAttachmentVisualDefinition(MMOEquipmentVisualDefinition visualDefinition)
        {
            if (visualDefinition.ModelPrefab == null)
            {
                return;
            }

            Transform socket = FindDeepChildByName(transform, visualDefinition.SocketName);
            if (socket == null)
            {
                Debug.LogWarning(
                    $"Equipment visual '{visualDefinition.name}' could not find attachment socket '{visualDefinition.SocketName}' under '{name}'.",
                    this);
                return;
            }

            GameObject instance = Instantiate(visualDefinition.ModelPrefab, socket);
            instance.name = visualDefinition.ModelPrefab.name;
            instance.transform.localPosition = visualDefinition.LocalPosition;
            instance.transform.localRotation = Quaternion.Euler(visualDefinition.LocalEulerAngles);
            instance.transform.localScale = visualDefinition.LocalScale;
            StripEditorOnlyChildren(instance);
            activeVisualInstances.Add(instance);
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
                    Destroy(child.gameObject);
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

        private void RestoreBaseBody()
        {
            foreach (MMOBodyPartRendererSlot slot in bodyPartSlots)
            {
                if (slot == null)
                {
                    continue;
                }

                SetRenderersEnabled(slot.Renderers, true);
                foreach (Renderer renderer in slot.Renderers)
                {
                    if (renderer != null && originalMaterials.TryGetValue(renderer, out Material[] materials))
                    {
                        renderer.sharedMaterials = materials;
                    }
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
                return baseMaterial;
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
                    Destroy(instance);
                }
            }

            activeVisualInstances.Clear();

            for (int i = activeMaterialInstances.Count - 1; i >= 0; i--)
            {
                Material material = activeMaterialInstances[i];
                if (material != null)
                {
                    Destroy(material);
                }
            }

            activeMaterialInstances.Clear();
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

        private static Transform FindDeepChildByName(Transform root, string childName)
        {
            if (root == null || string.IsNullOrWhiteSpace(childName))
            {
                return null;
            }

            string normalizedChildName = NormalizeName(childName);
            if (NormalizeName(root.name) == normalizedChildName)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeepChildByName(root.GetChild(i), childName);
                if (found != null)
                {
                    return found;
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

        private int CalculateEquipmentSignature()
        {
            if (equipment == null)
            {
                return 0;
            }

            unchecked
            {
                int hash = 17;
                foreach (MMOEquippedItemSlot equippedItem in equipment.EquippedItems)
                {
                    if (equippedItem == null)
                    {
                        continue;
                    }

                    hash = hash * 31 + (int)equippedItem.SlotType;
                    hash = hash * 31 + (equippedItem.Item != null ? equippedItem.Item.GetInstanceID() : 0);
                }

                return hash;
            }
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
