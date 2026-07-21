using UnityEngine;

namespace RPGClone.Vfx.Fire
{
    [DisallowMultipleComponent]
    public sealed class FlamestrikeExpandingRingVFX : MonoBehaviour
    {
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int TintId = Shader.PropertyToID("_Tint");

        [SerializeField] private Renderer ringRenderer;
        [SerializeField] private Vector3 authoredScale = Vector3.one;
        [SerializeField] private Quaternion authoredRotation = Quaternion.identity;
        [SerializeField, Range(0f, 0.8f)] private float startDelay;
        [SerializeField, Min(1f)] private float expansionMultiplier = 1.5f;
        [SerializeField, Range(0f, 1f)] private float heightAtPerimeter;
        [SerializeField] private float rotationSpeed = 18f;
        [SerializeField, Range(0f, 2f)] private float opacityMultiplier = 1f;

        private MaterialPropertyBlock properties;

        public Renderer RingRenderer => ringRenderer;
        public float StartDelay => startDelay;
        public float EndDiameter => authoredScale.x * expansionMultiplier;
        public float HeightAtPerimeter => heightAtPerimeter;
        public Vector3 AuthoredScale => authoredScale;
        public float CurrentProgress { get; private set; }
        public float CurrentOpacity { get; private set; }

        public void ConfigureAuthoring(Renderer renderer, Vector3 scale, float delay, float expansion, float finalHeight, float degreesPerSecond, float opacity)
        {
            ringRenderer = renderer;
            authoredScale = scale;
            authoredRotation = renderer != null ? renderer.transform.localRotation : Quaternion.identity;
            startDelay = Mathf.Clamp(delay, 0f, 0.8f);
            expansionMultiplier = Mathf.Max(1f, expansion);
            heightAtPerimeter = Mathf.Clamp01(finalHeight);
            rotationSpeed = degreesPerSecond;
            opacityMultiplier = Mathf.Max(0f, opacity);
        }

        public void Animate(float elapsed, float lifetimeProgress, float opacity, Color tint)
        {
            if (ringRenderer == null) return;
            float progress = Mathf.Clamp01(Mathf.InverseLerp(startDelay, 1f, lifetimeProgress));
            float reveal = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.06f, progress));
            float evaporate = 1f - progress;
            float size = Mathf.Lerp(1f, expansionMultiplier, progress);
            float height = Mathf.Lerp(1f, heightAtPerimeter, progress);
            CurrentProgress = progress;
            CurrentOpacity = Mathf.Clamp01(opacity * opacityMultiplier * reveal * evaporate);
            transform.localScale = Vector3.Scale(authoredScale, new Vector3(size, height, size));
            transform.localRotation = authoredRotation * Quaternion.Euler(0f, elapsed * rotationSpeed, 0f);

            properties ??= new MaterialPropertyBlock();
            ringRenderer.GetPropertyBlock(properties);
            properties.SetFloat(OpacityId, CurrentOpacity);
            properties.SetColor(TintId, tint);
            ringRenderer.SetPropertyBlock(properties);
            ringRenderer.enabled = CurrentOpacity > 0.001f;
        }
    }
}
