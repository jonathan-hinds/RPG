using UnityEngine;

namespace RPGClone.Vfx.Fire
{
    public sealed class FlamestrikeTargetReactionVFX : MonoBehaviour
    {
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int TintId = Shader.PropertyToID("_Tint");

        [SerializeField] private Renderer flash;
        [SerializeField] private ParticleSystem sparks;
        [SerializeField] private ParticleSystem smoke;

        private MaterialPropertyBlock properties;
        private Transform target;
        private FlamestrikeVFXProfile profile;
        private float startedAt;
        private float duration;
        private float scale;
        private bool playing;

        public bool IsPlaying => playing;

        public void ConfigureAuthoring(Renderer newFlash, ParticleSystem newSparks, ParticleSystem newSmoke)
        {
            flash = newFlash;
            sparks = newSparks;
            smoke = newSmoke;
            ResetForPool();
        }

        public void Play(FlamestrikeVFXProfile newProfile, Transform newTarget, bool initial)
        {
            if (newProfile == null || newTarget == null) return;
            profile = newProfile;
            target = newTarget;
            duration = initial ? 0.52f : 0.28f;
            scale = initial ? profile.InitialHitFlashScale : profile.InitialHitFlashScale * 0.55f;
            startedAt = Time.time;
            playing = true;
            gameObject.SetActive(true);
            ConfigureBurst(sparks, initial ? profile.TickSparkAmount * 2 : profile.TickSparkAmount);
            ConfigureBurst(smoke, initial ? profile.TickSmokeAmount * 2 : profile.TickSmokeAmount);
        }

        public void ResetForPool()
        {
            playing = false;
            target = null;
            if (flash != null) flash.enabled = false;
            sparks?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            smoke?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!playing || target == null || profile == null)
            {
                if (playing) ResetForPool();
                return;
            }

            float t = Mathf.Clamp01((Time.time - startedAt) / duration);
            float alpha = Mathf.Sin(t * Mathf.PI);
            transform.position = target.position + Vector3.up * (t < 0.5f ? 0.85f : 0.45f);
            transform.localScale = Vector3.one * scale * Mathf.Lerp(0.65f, 1.2f, t);
            Camera camera = Camera.main;
            if (camera != null) transform.rotation = Quaternion.LookRotation(transform.position - camera.transform.position, Vector3.up);
            if (flash != null)
            {
                properties ??= new MaterialPropertyBlock();
                flash.enabled = alpha > 0.001f;
                flash.GetPropertyBlock(properties);
                properties.SetFloat(OpacityId, alpha);
                properties.SetColor(TintId, profile.HotColor * (1f + profile.TickReactionBrightness * 0.25f));
                flash.SetPropertyBlock(properties);
            }

            if (t >= 1f) ResetForPool();
        }

        private static void ConfigureBurst(ParticleSystem particleSystem, int count)
        {
            if (particleSystem == null) return;
            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(count, 0, short.MaxValue)) });
            particleSystem.Clear(true);
            particleSystem.Play(true);
        }
    }
}
