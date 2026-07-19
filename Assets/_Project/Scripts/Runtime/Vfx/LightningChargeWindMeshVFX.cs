using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace RPGClone.Vfx.Shaman
{
    /// <summary>
    /// Presents the Lightning Bolt charge pressure as contracting, seamless torus meshes.
    /// This avoids camera-facing billboard intersections and keeps the wind volume coherent
    /// from every viewing angle.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LightningChargeWindMeshVFX : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
        private static readonly int MainTexStId = Shader.PropertyToID("_MainTex_ST");

        [SerializeField] private LightningVFXProfile profile;
        [SerializeField] private MeshRenderer[] windRings = Array.Empty<MeshRenderer>();

        private MaterialPropertyBlock[] propertyBlocks = Array.Empty<MaterialPropertyBlock>();
        private bool playing;

        public int RingCount => windRings?.Length ?? 0;

        private void Awake()
        {
            EnsurePropertyBlocks();
            SetVisible(false);
        }

        public void ConfigureAuthoring(LightningVFXProfile newProfile, MeshRenderer[] newWindRings)
        {
            profile = newProfile;
            windRings = newWindRings ?? Array.Empty<MeshRenderer>();
            propertyBlocks = Array.Empty<MaterialPropertyBlock>();
        }

        public void Begin()
        {
            EnsurePropertyBlocks();
            playing = profile != null && windRings.Length > 0;
            SetVisible(playing);
        }

        public void UpdatePresentation(Vector3 groundPosition, float chargeProgress, float elapsed)
        {
            if (!playing || profile == null)
            {
                return;
            }

            transform.position = groundPosition + Vector3.up * 0.06f;
            float strength = Mathf.Clamp01(profile.PressureFieldStrength);
            float chargeFade = Mathf.SmoothStep(0.25f, 1f, chargeProgress);

            for (int i = 0; i < windRings.Length; i++)
            {
                MeshRenderer renderer = windRings[i];
                if (renderer == null)
                {
                    continue;
                }

                // Each closed ring repeatedly contracts into the caster. Offsetting the phases
                // creates a continuous inward current without scaling a camera-facing sprite.
                float phase = Mathf.Repeat(elapsed * (0.48f + i * 0.055f) + i / (float)windRings.Length, 1f);
                float eased = 1f - Mathf.Pow(1f - phase, 1.35f);
                // Keep the compressed inner size while tightening the outer envelope to half
                // of the original 2.65m radius so the charge stays close to the caster.
                float radius = Mathf.Lerp(1.325f, 0.48f, eased) * profile.OverallScale;
                float verticalScale = Mathf.Lerp(0.72f, 0.34f, eased);
                float alphaEnvelope = Mathf.Sin(phase * Mathf.PI);
                float electricalFlicker = 0.68f + 0.32f * Mathf.Abs(Mathf.Sin(elapsed * (31f + i * 7f) + i * 1.73f));
                float alpha = alphaEnvelope * chargeFade * strength * electricalFlicker * (0.5f + i * 0.07f);

                Transform ring = renderer.transform;
                ring.localPosition = new Vector3(0f, Mathf.Sin(phase * Mathf.PI) * (0.16f + i * 0.025f), 0f);
                ring.localRotation = Quaternion.Euler(0f, elapsed * (24f + i * 11f) * (i % 2 == 0 ? 1f : -1f), 0f);
                ring.localScale = new Vector3(radius, verticalScale, radius);

                Color color = Color.Lerp(profile.ElectricBlueColor, profile.CyanColor, 0.35f + i * 0.2f);
                color.a = alpha;
                float direction = i % 2 == 0 ? 1f : -1f;
                float tiling = 1.35f + i * 0.45f;
                float crawl = Mathf.Repeat(elapsed * (0.82f + i * 0.27f) * direction + i * 0.19f, 1f);
                Vector4 textureTransform = new(tiling, 1f, crawl, 0.25f);
                MaterialPropertyBlock block = propertyBlocks[i];
                block.Clear();
                block.SetColor(BaseColorId, color);
                block.SetColor(ColorId, color);
                block.SetVector(BaseMapStId, textureTransform);
                block.SetVector(MainTexStId, textureTransform);
                renderer.SetPropertyBlock(block);
            }
        }

        public void Stop()
        {
            playing = false;
            SetVisible(false);
        }

        private void EnsurePropertyBlocks()
        {
            if (propertyBlocks.Length == windRings.Length)
            {
                return;
            }

            propertyBlocks = new MaterialPropertyBlock[windRings.Length];
            for (int i = 0; i < propertyBlocks.Length; i++)
            {
                propertyBlocks[i] = new MaterialPropertyBlock();
            }
        }

        private void SetVisible(bool visible)
        {
            foreach (MeshRenderer renderer in windRings)
            {
                if (renderer != null)
                {
                    renderer.enabled = visible;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
            }
        }
    }
}
