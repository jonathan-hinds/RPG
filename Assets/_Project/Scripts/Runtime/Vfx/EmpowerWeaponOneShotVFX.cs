using UnityEngine;

namespace RPGClone.Vfx.Shaman
{
    [DisallowMultipleComponent]
    public sealed class EmpowerWeaponOneShotVFX : MonoBehaviour, IMMOAbilityVfxPoolReset
    {
        [SerializeField, Min(0.05f)] private float duration = 0.5f;
        [SerializeField] private ParticleSystem[] particles = System.Array.Empty<ParticleSystem>();
        [SerializeField] private TrailRenderer[] trails = System.Array.Empty<TrailRenderer>();

        private float releaseAt;
        private bool playing;

        private void OnEnable()
        {
            Play();
        }

        private void Update()
        {
            if (playing && Time.time >= releaseAt)
            {
                playing = false;
                MMOAbilityVfxPool.Release(gameObject);
            }
        }

        public void Play(float newDuration = -1f)
        {
            float resolvedDuration = newDuration > 0f ? newDuration : duration;
            releaseAt = Time.time + Mathf.Max(0.05f, resolvedDuration);
            playing = true;
            foreach (TrailRenderer trail in trails)
            {
                if (trail == null) continue;
                trail.Clear();
                trail.emitting = true;
            }

            foreach (ParticleSystem particle in particles)
            {
                if (particle == null) continue;
                particle.Clear(true);
                particle.Play(true);
            }
        }

        public void ConfigureAuthoring(
            float newDuration,
            ParticleSystem[] newParticles,
            TrailRenderer[] newTrails)
        {
            duration = Mathf.Max(0.05f, newDuration);
            particles = newParticles ?? System.Array.Empty<ParticleSystem>();
            trails = newTrails ?? System.Array.Empty<TrailRenderer>();
        }

        public void ResetForPool()
        {
            playing = false;
            foreach (TrailRenderer trail in trails)
            {
                if (trail == null) continue;
                trail.emitting = false;
                trail.Clear();
            }

            foreach (ParticleSystem particle in particles)
            {
                particle?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }
}
