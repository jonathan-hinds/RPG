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
        [SerializeField] private MMOCharacterEquipment equipment;
        [SerializeField] private List<MMOBodyPartRendererSlot> bodyPartSlots = new();

        private readonly List<GameObject> activeVisualInstances = new();
        private readonly List<Material> activeMaterialInstances = new();
        private readonly Dictionary<Renderer, Material[]> originalMaterials = new();

        private void Awake()
        {
            EnsureReferences();
            CacheOriginalMaterials();
        }

        private void OnEnable()
        {
            EnsureReferences();
            if (equipment != null)
            {
                equipment.Changed -= OnEquipmentChanged;
                equipment.Changed += OnEquipmentChanged;
            }

            RebuildEquipmentVisuals();
        }

        private void OnDisable()
        {
            if (equipment != null)
            {
                equipment.Changed -= OnEquipmentChanged;
            }

            ClearRuntimeVisuals();
            RestoreBaseBody();
        }

        public void Configure(MMOCharacterEquipment newEquipment, IEnumerable<MMOBodyPartRendererSlot> newBodyPartSlots)
        {
            equipment = newEquipment;
            bodyPartSlots = newBodyPartSlots != null
                ? new List<MMOBodyPartRendererSlot>(newBodyPartSlots)
                : new List<MMOBodyPartRendererSlot>();
            CacheOriginalMaterials();
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
            ClearRuntimeVisuals();
            RestoreBaseBody();

            if (equipment == null)
            {
                return;
            }

            foreach (MMOEquippedItemSlot equippedItem in equipment.EquippedItems)
            {
                MMOEquipmentVisualDefinition visualDefinition = equippedItem?.Item != null
                    ? equippedItem.Item.EquipmentVisual
                    : null;
                if (visualDefinition != null)
                {
                    ApplyVisualDefinition(visualDefinition);
                }
            }
        }

        private void ApplyVisualDefinition(MMOEquipmentVisualDefinition visualDefinition)
        {
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
            activeVisualInstances.Add(instance);
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
            Material baseMaterial = visualDefinition.MaterialOverride != null ? visualDefinition.MaterialOverride : sourceMaterial;
            if (!hasTextureOverride)
            {
                return baseMaterial;
            }

            Material materialInstance = baseMaterial != null
                ? new Material(baseMaterial)
                : new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
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

        private static void SetTextureIfPresent(Material material, string propertyName, Texture texture)
        {
            if (texture != null && material.HasProperty(propertyName))
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

        private void EnsureReferences()
        {
            if (equipment == null)
            {
                equipment = GetComponent<MMOCharacterEquipment>();
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

        public MMOBodyPartRendererSlot(MMOCharacterBodyPart bodyPart, Transform anchor, Renderer[] renderers)
        {
            this.bodyPart = bodyPart;
            this.anchor = anchor;
            this.renderers = renderers ?? Array.Empty<Renderer>();
        }
    }
}
