using UnityEngine;

namespace RPGClone.Vfx.Mage
{
    [DisallowMultipleComponent]
    public sealed class FrostWaveRingVFX : MonoBehaviour
    {
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
        private static readonly int RevealId = Shader.PropertyToID("_Reveal");
        private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
        private static readonly int ScrollId = Shader.PropertyToID("_Scroll");

        [SerializeField] private Renderer primaryRing;
        [SerializeField] private Renderer secondaryRing;
        [SerializeField] private Renderer mistRing;

        private MaterialPropertyBlock properties;
        private FrostWaveVFXProfile profile;
        private float radius;
        private float startedAt;
        private bool playing;

        private void Awake()
        {
            properties = new MaterialPropertyBlock();
        }

        public void ConfigureAuthoring(Renderer newPrimaryRing, Renderer newSecondaryRing, Renderer newMistRing)
        {
            primaryRing = newPrimaryRing;
            secondaryRing = newSecondaryRing;
            mistRing = newMistRing;
        }

        public void Play(FrostWaveVFXProfile newProfile, float newRadius)
        {
            profile = newProfile;
            radius = Mathf.Max(0.1f, newRadius);
            startedAt = Time.time;
            playing = profile != null;
            gameObject.SetActive(playing);
            SetActive(primaryRing, playing);
            SetActive(secondaryRing, playing);
            SetActive(mistRing, playing);
            if (playing)
            {
                Animate(0f);
            }
        }

        public void ResetForPool()
        {
            playing = false;
            SetActive(primaryRing, false);
            SetActive(secondaryRing, false);
            SetActive(mistRing, false);
        }

        private void Update()
        {
            if (!playing || profile == null)
            {
                return;
            }

            float elapsed = Time.time - startedAt;
            Animate(elapsed);
            if (elapsed >= profile.SecondaryRingDelay + profile.RingExpansionDuration + 0.55f)
            {
                playing = false;
                SetActive(primaryRing, false);
                SetActive(secondaryRing, false);
                SetActive(mistRing, false);
            }
        }

        private void Animate(float elapsed)
        {
            AnimateRing(
                primaryRing,
                elapsed - profile.PrimaryRingDelay,
                1f,
                profile.PaleCyan,
                2.25f * profile.OverallIntensity,
                1f);
            AnimateRing(
                secondaryRing,
                elapsed - profile.SecondaryRingDelay,
                0.975f,
                profile.WhiteHot,
                3.2f * profile.OverallIntensity,
                0.82f);
            AnimateRing(
                mistRing,
                elapsed - 0.08f,
                0.94f,
                profile.MistTint,
                0.9f * profile.OverallIntensity,
                0.52f);
        }

        private void AnimateRing(
            Renderer renderer,
            float localTime,
            float radiusScale,
            Color tint,
            float brightness,
            float opacityScale)
        {
            if (renderer == null)
            {
                return;
            }

            properties ??= new MaterialPropertyBlock();

            if (localTime < 0f)
            {
                renderer.enabled = false;
                return;
            }

            float expansion = Mathf.Clamp01(localTime / Mathf.Max(0.01f, profile.RingExpansionDuration));
            float eased = 1f - Mathf.Pow(1f - expansion, 3f);
            float fade = expansion < 0.82f
                ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(localTime / 0.055f))
                : 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.82f, 1.45f, expansion));
            float diameter = Mathf.Lerp(0.35f, radius * 2f * radiusScale, eased);
            renderer.transform.localScale = new Vector3(diameter, 1f, diameter);
            renderer.enabled = fade > 0.002f;
            renderer.GetPropertyBlock(properties);
            properties.SetColor(TintId, tint);
            properties.SetFloat(OpacityId, fade * opacityScale);
            properties.SetFloat(BrightnessId, brightness * (1f + Mathf.Sin(localTime * 19f) * 0.08f));
            properties.SetFloat(RevealId, Mathf.Clamp01(expansion * 1.18f));
            properties.SetFloat(DissolveId, Mathf.Clamp01(Mathf.InverseLerp(0.36f, 0.72f, localTime)));
            properties.SetVector(ScrollId, new Vector4(0.025f, -0.018f, 0.09f, -0.07f));
            renderer.SetPropertyBlock(properties);
        }

        private static void SetActive(Renderer renderer, bool active)
        {
            if (renderer != null)
            {
                renderer.enabled = active;
            }
        }
    }
}
