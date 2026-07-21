using UnityEngine;

namespace RPGClone.Vfx.Fire
{
    [DisallowMultipleComponent]
    public sealed class FlamestrikeTubeShellVFX : MonoBehaviour
    {
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int PhaseId = Shader.PropertyToID("_Phase");

        [SerializeField] private Renderer shellRenderer;
        [SerializeField] private Vector3 authoredScale = Vector3.one;
        [SerializeField] private Vector3 authoredPosition;
        [SerializeField, Range(0f, 1f)] private float phase;
        [SerializeField, Min(0.1f)] private float cycleSpeed = 1f;
        [SerializeField, Range(0f, 1.5f)] private float radialExpansion = 0.35f;
        [SerializeField, Range(0f, 1f)] private float verticalExpansion = 0.16f;
        [SerializeField, Min(0f)] private float lift = 0.18f;
        [SerializeField, Range(0f, 2f)] private float opacityMultiplier = 1f;
        [SerializeField, ColorUsage(true, true)] private Color tintMultiplier = Color.white;

        private MaterialPropertyBlock properties;

        public Renderer ShellRenderer => shellRenderer;
        public bool Loops => false;
        public Vector3 AuthoredPosition => authoredPosition;

        public void ConfigureAuthoring(
            Renderer renderer,
            Vector3 scale,
            float phaseOffset,
            float speed,
            float expansion,
            float verticalGrowth,
            float upwardLift,
            float opacity,
            bool shouldLoop,
            Color colorMultiplier)
        {
            shellRenderer = renderer;
            authoredScale = scale;
            authoredPosition = renderer != null ? renderer.transform.localPosition : Vector3.zero;
            phase = Mathf.Repeat(phaseOffset, 1f);
            cycleSpeed = Mathf.Max(0.1f, speed);
            radialExpansion = Mathf.Max(0f, expansion);
            verticalExpansion = Mathf.Max(0f, verticalGrowth);
            lift = Mathf.Max(0f, upwardLift);
            opacityMultiplier = Mathf.Max(0f, opacity);
            // Visibility never loops. The shader flow remains continuous while the
            // shell itself expands once and receives a monotonic fade from its owner.
            _ = shouldLoop;
            tintMultiplier = colorMultiplier;
        }

        public void SetAuthoringOffset(Vector3 offset)
        {
            authoredPosition = offset;
            transform.localPosition = offset;
        }

        public void Animate(float time, float opacity, Color tint, float pulse)
        {
            if (shellRenderer == null) return;
            float expansionProgress = Mathf.Clamp01(time * cycleSpeed);
            float reveal = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.1f, expansionProgress));
            float radial = 1f + expansionProgress * radialExpansion + pulse * 0.08f;
            float vertical = 1f + expansionProgress * verticalExpansion + pulse * 0.04f;
            transform.localScale = Vector3.Scale(authoredScale, new Vector3(radial, vertical, radial));
            transform.localPosition = authoredPosition + Vector3.up * expansionProgress * lift;

            properties ??= new MaterialPropertyBlock();
            shellRenderer.GetPropertyBlock(properties);
            properties.SetFloat(OpacityId, Mathf.Clamp01(opacity * opacityMultiplier * reveal));
            properties.SetFloat(PhaseId, phase * 11.73f);
            properties.SetColor(TintId, Multiply(tint, tintMultiplier));
            shellRenderer.SetPropertyBlock(properties);
            shellRenderer.enabled = opacity * reveal > 0.001f;
        }

        private static Color Multiply(Color a, Color b) => new(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a);
    }
}
