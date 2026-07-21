using UnityEngine;

namespace RPGClone.Vfx.Fire
{
    [DisallowMultipleComponent]
    public sealed class FlamestrikeTargetingVFX : MonoBehaviour, IMMOGroundTargetingVfx
    {
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int ScrollId = Shader.PropertyToID("_Scroll");

        [SerializeField] private FlamestrikeVFXProfile profile;
        [SerializeField] private Renderer groundTint;
        [SerializeField] private Renderer boundary;
        [SerializeField] private Transform runeRoot;
        [SerializeField] private Renderer[] runes;
        [SerializeField] private Transform centerMarker;
        [SerializeField] private ParticleSystem embers;

        private MaterialPropertyBlock properties;

        public FlamestrikeVFXProfile Profile => profile;

        public void ConfigureAuthoring(
            FlamestrikeVFXProfile newProfile,
            Renderer newGroundTint,
            Renderer newBoundary,
            Transform newRuneRoot,
            Renderer[] newRunes,
            Transform newCenterMarker,
            ParticleSystem newEmbers)
        {
            profile = newProfile;
            groundTint = newGroundTint;
            boundary = newBoundary;
            runeRoot = newRuneRoot;
            runes = newRunes;
            centerMarker = newCenterMarker;
            embers = newEmbers;
        }

        public void UpdatePreview(Vector3 position, Vector3 normal, float radius, bool isValid)
        {
            if (profile == null) return;
            transform.SetPositionAndRotation(position + normal * 0.045f, Quaternion.FromToRotation(Vector3.up, normal));
            transform.localScale = Vector3.one * (radius / Mathf.Max(0.1f, profile.TargetRadius));
            Color tint = isValid ? profile.ValidTargetColor : profile.InvalidTargetColor;
            SetRenderer(groundTint, tint, profile.GroundTintOpacity);
            SetRenderer(boundary, tint * profile.RingBrightness, 0.92f);
            foreach (Renderer rune in runes)
            {
                SetRenderer(rune, tint, 0.36f);
            }

            if (runeRoot != null)
            {
                runeRoot.Rotate(Vector3.up, profile.RuneRotationSpeed * Time.deltaTime, Space.Self);
            }

            if (centerMarker != null)
            {
                float pulse = 1f + Mathf.Sin(Time.time * 4.8f) * 0.08f;
                centerMarker.localScale = Vector3.one * profile.CenterMarkerSize * pulse;
            }

            if (embers != null)
            {
                ParticleSystem.EmissionModule emission = embers.emission;
                emission.rateOverTime = profile.TargetingEmberRate;
                if (!embers.isPlaying) embers.Play(true);
            }
        }

        private void SetRenderer(Renderer renderer, Color color, float opacity)
        {
            if (renderer == null) return;
            properties ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            properties.SetColor(TintId, color);
            properties.SetFloat(OpacityId, opacity);
            properties.SetVector(ScrollId, new Vector4(Time.time * 0.015f, 0f, 0f, 0f));
            renderer.SetPropertyBlock(properties);
        }
    }
}
