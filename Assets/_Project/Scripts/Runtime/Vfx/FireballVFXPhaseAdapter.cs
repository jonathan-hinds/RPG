using System.Collections;
using UnityEngine;

namespace RPGClone.Vfx.Fire
{
    [DisallowMultipleComponent]
    public sealed class FireballVFXPhaseAdapter : MonoBehaviour, IMMOAbilityVfxInstance, IMMOAbilityVfxReleaseHandler
    {
        public enum Phase
        {
            Casting,
            Projectile,
            Impact
        }

        [SerializeField] private Phase phase;
        [SerializeField] private FireballVFX fireball;
        private bool released;

        public void Initialize(MMOAbilityVfxContext context)
        {
            released = false;
            EnsureReference();
            if (fireball == null) return;

            switch (phase)
            {
                case Phase.Casting:
                    fireball.SetCastPoint(transform);
                    fireball.PlayCasting();
                    break;
                case Phase.Projectile:
                    fireball.AttachToProjectile(transform);
                    fireball.PlayProjectile();
                    break;
                case Phase.Impact:
                    fireball.TriggerImpact(transform.position, Vector3.up);
                    StartCoroutine(DestroyAfterImpact());
                    break;
            }
        }

        public void Release(bool immediate)
        {
            if (released || phase != Phase.Casting) return;
            released = true;
            EnsureReference();
            if (immediate)
            {
                fireball?.StopImmediate();
                Destroy(gameObject);
                return;
            }

            fireball?.ReleaseCasting();
            StartCoroutine(DestroyAfter(fireball != null && fireball.Profile != null ? fireball.Profile.CastReleaseDuration + 0.05f : 0.45f));
        }

        public void ConfigureAuthoring(Phase newPhase, FireballVFX newFireball)
        {
            phase = newPhase;
            fireball = newFireball;
        }

        private IEnumerator DestroyAfterImpact()
        {
            FireballVFXProfile activeProfile = fireball != null ? fireball.Profile : null;
            float duration = activeProfile != null
                ? Mathf.Max(activeProfile.AftermathDuration, activeProfile.EnableScorch ? activeProfile.ScorchDuration : 0f) + 0.1f
                : 2f;
            yield return new WaitForSeconds(duration);
            Destroy(gameObject);
        }

        private IEnumerator DestroyAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            Destroy(gameObject);
        }

        private void EnsureReference()
        {
            if (fireball == null) fireball = GetComponentInChildren<FireballVFX>(true);
        }
    }
}
