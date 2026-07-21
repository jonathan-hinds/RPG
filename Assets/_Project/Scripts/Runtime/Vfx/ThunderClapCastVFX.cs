using UnityEngine;

namespace RPGClone.Vfx.Warrior
{
    [DisallowMultipleComponent]
    public sealed class ThunderClapCastVFX : MonoBehaviour
    {
        [SerializeField] private ParticleSystem liftingDust;
        [SerializeField] private ParticleSystem vibratingStones;
        [SerializeField] private ParticleSystem attachedSparks;

        private ThunderClapVFXProfile profile;
        private Transform caster;
        private float startedAt;
        private bool playing;

        public bool IsPlaying => playing;

        public void ConfigureAuthoring(
            ParticleSystem newLiftingDust,
            ParticleSystem newVibratingStones,
            ParticleSystem newAttachedSparks)
        {
            liftingDust = newLiftingDust;
            vibratingStones = newVibratingStones;
            attachedSparks = newAttachedSparks;
        }

        public void Play(ThunderClapVFXProfile newProfile, Transform newCaster, Vector3 impactPosition)
        {
            profile = newProfile;
            caster = newCaster;
            startedAt = Time.time;
            playing = profile != null;
            gameObject.SetActive(playing);
            if (!playing)
            {
                return;
            }

            Vector3 feet = ResolveFeetPosition(impactPosition);
            transform.position = feet;
            System.Random random = new(GetHashCode() ^ System.Environment.TickCount);
            ThunderClapVFXUtility.EmitRing(liftingDust, feet, 10, 0.48f * profile.OverallScale, -0.12f, 0.45f, 0.28f, profile.WarmDustColor, random);
            ThunderClapVFXUtility.EmitRing(vibratingStones, feet, 7, 0.58f * profile.OverallScale, 0.08f, 0.42f, 0.08f, profile.StoneColor, random);
            ThunderClapVFXUtility.EmitAt(attachedSparks, Vector3.up * 0.9f, 8, 0.15f, profile.LightningColor, random);
        }

        public void StopAnticipation()
        {
            if (!playing)
            {
                return;
            }

            playing = false;
            liftingDust?.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            vibratingStones?.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            attachedSparks?.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        public void ResetForPool()
        {
            playing = false;
            caster = null;
            ThunderClapVFXUtility.StopAndClear(liftingDust);
            ThunderClapVFXUtility.StopAndClear(vibratingStones);
            ThunderClapVFXUtility.StopAndClear(attachedSparks);
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!playing || profile == null)
            {
                return;
            }

            transform.position = ResolveFeetPosition(transform.position);
            if (Time.time - startedAt >= profile.AnticipationDuration)
            {
                StopAnticipation();
            }
        }

        private Vector3 ResolveFeetPosition(Vector3 fallback)
        {
            return caster != null ? caster.position + Vector3.up * 0.035f : fallback;
        }
    }
}
