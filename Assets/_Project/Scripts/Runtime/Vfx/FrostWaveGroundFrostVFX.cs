using UnityEngine;

namespace RPGClone.Vfx.Mage
{
    [DisallowMultipleComponent]
    public sealed class FrostWaveGroundFrostVFX : MonoBehaviour
    {
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
        private static readonly int RevealId = Shader.PropertyToID("_Reveal");
        private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
        private static readonly int ScrollId = Shader.PropertyToID("_Scroll");

        [SerializeField] private Renderer primaryPattern;
        [SerializeField] private Renderer secondaryPattern;
        [SerializeField] private Renderer rune;

        private MaterialPropertyBlock properties;
        private FrostWaveVFXProfile profile;
        private float startedAt;
        private bool playing;

        private void Awake()
        {
            properties = new MaterialPropertyBlock();
        }

        public void ConfigureAuthoring(Renderer newPrimaryPattern, Renderer newSecondaryPattern, Renderer newRune)
        {
            primaryPattern = newPrimaryPattern;
            secondaryPattern = newSecondaryPattern;
            rune = newRune;
        }

        public void Play(FrostWaveVFXProfile newProfile, float radius)
        {
            profile = newProfile;
            startedAt = Time.time;
            playing = profile != null;
            float diameter = Mathf.Max(0.1f, radius * 2f);
            if (primaryPattern != null) primaryPattern.transform.localScale = new Vector3(diameter, 1f, diameter);
            if (secondaryPattern != null) secondaryPattern.transform.localScale = new Vector3(diameter * 0.94f, 1f, diameter * 0.94f);
            if (rune != null) rune.transform.localScale = new Vector3(Mathf.Min(3.1f, diameter * 0.25f), 1f, Mathf.Min(3.1f, diameter * 0.25f));
            SetEnabled(playing);
            if (playing) Animate(0f);
        }

        public void ResetForPool()
        {
            playing = false;
            SetEnabled(false);
        }

        private void Update()
        {
            if (!playing || profile == null)
            {
                return;
            }

            float elapsed = Time.time - startedAt;
            Animate(elapsed);
            if (elapsed >= profile.GroundRevealDelay + profile.GroundFrostDuration)
            {
                playing = false;
                SetEnabled(false);
            }
        }

        private void Animate(float elapsed)
        {
            float localTime = elapsed - profile.GroundRevealDelay;
            if (localTime < 0f)
            {
                SetRenderer(primaryPattern, profile.SaturatedBlue, 0f, 0f, 0f, 0f);
                SetRenderer(secondaryPattern, profile.PaleCyan, 0f, 0f, 0f, 0f);
                SetRenderer(rune, profile.PaleCyan, Mathf.Clamp01(1f - elapsed / 0.28f) * 0.32f, 1.2f, 1f, elapsed / 0.3f);
                return;
            }

            float normalized = Mathf.Clamp01(localTime / Mathf.Max(0.01f, profile.GroundFrostDuration));
            float reveal = Mathf.Clamp01(localTime / Mathf.Max(0.01f, profile.RingExpansionDuration));
            float wake = Mathf.Exp(-Mathf.Pow((reveal - 0.82f) * 3.4f, 2f));
            float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.48f, 1f, normalized));
            SetRenderer(primaryPattern, profile.SaturatedBlue, fade * (0.28f + wake * 0.34f), 1.15f, reveal, normalized * 0.82f);
            SetRenderer(secondaryPattern, profile.PaleCyan, fade * (0.12f + wake * 0.24f), 1.45f, Mathf.Clamp01(reveal * 1.08f), normalized);
            SetRenderer(rune, profile.WhiteHot, Mathf.Clamp01(1f - elapsed / 0.34f) * 0.27f, 1.5f, 1f, Mathf.Clamp01(elapsed / 0.32f));
        }

        private void SetRenderer(Renderer renderer, Color tint, float opacity, float brightness, float reveal, float dissolve)
        {
            if (renderer == null)
            {
                return;
            }

            properties ??= new MaterialPropertyBlock();
            renderer.enabled = opacity > 0.001f;
            renderer.GetPropertyBlock(properties);
            properties.SetColor(TintId, tint);
            properties.SetFloat(OpacityId, Mathf.Clamp01(opacity));
            properties.SetFloat(BrightnessId, brightness * profile.OverallIntensity);
            properties.SetFloat(RevealId, Mathf.Clamp01(reveal));
            properties.SetFloat(DissolveId, Mathf.Clamp01(dissolve));
            properties.SetVector(ScrollId, new Vector4(0.006f, -0.004f, 0.025f, -0.02f));
            renderer.SetPropertyBlock(properties);
        }

        private void SetEnabled(bool enabled)
        {
            if (primaryPattern != null) primaryPattern.enabled = enabled;
            if (secondaryPattern != null) secondaryPattern.enabled = enabled;
            if (rune != null) rune.enabled = enabled;
        }
    }
}
