using UnityEngine;

namespace RPGClone.Vfx
{
    public sealed class MMOAbilityVfxLifetime : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float lifetimeSeconds = 1f;
        [SerializeField] private bool destroyAutomatically = true;
        [SerializeField] private bool stopParticlesBeforeDestroy = true;

        private bool released;

        public void Configure(float newLifetimeSeconds, bool newDestroyAutomatically, bool newStopParticlesBeforeDestroy)
        {
            lifetimeSeconds = Mathf.Max(0f, newLifetimeSeconds);
            destroyAutomatically = newDestroyAutomatically;
            stopParticlesBeforeDestroy = newStopParticlesBeforeDestroy;
        }

        private void OnEnable()
        {
            released = false;
            if (destroyAutomatically && lifetimeSeconds > 0f)
            {
                Destroy(gameObject, lifetimeSeconds);
            }
        }

        public void StopAndRelease(float releaseDelaySeconds = 0.35f)
        {
            if (released)
            {
                return;
            }

            released = true;
            if (stopParticlesBeforeDestroy)
            {
                ParticleSystem[] particleSystems = GetComponentsInChildren<ParticleSystem>(true);
                foreach (ParticleSystem particleSystem in particleSystems)
                {
                    particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }

            Destroy(gameObject, Mathf.Max(0f, releaseDelaySeconds));
        }
    }
}
