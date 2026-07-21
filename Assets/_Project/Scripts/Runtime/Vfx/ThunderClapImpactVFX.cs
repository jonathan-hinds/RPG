using UnityEngine;

namespace RPGClone.Vfx.Warrior
{
    [DisallowMultipleComponent]
    public sealed class ThunderClapImpactVFX : MonoBehaviour
    {
        [SerializeField] private ParticleSystem groundCompression;
        [SerializeField] private ParticleSystem centralFlash;
        [SerializeField] private ParticleSystem heavyDust;
        [SerializeField] private ParticleSystem fineDust;
        [SerializeField] private ParticleSystem dirtChunks;
        [SerializeField] private ParticleSystem rockFragments;
        [SerializeField] private ParticleSystem electricalSparks;

        public void ConfigureAuthoring(ParticleSystem[] systems)
        {
            if (systems == null || systems.Length != 7)
            {
                throw new System.ArgumentException("Thunder Clap impact requires exactly seven particle layers.");
            }

            groundCompression = systems[0];
            centralFlash = systems[1];
            heavyDust = systems[2];
            fineDust = systems[3];
            dirtChunks = systems[4];
            rockFragments = systems[5];
            electricalSparks = systems[6];
        }

        public void Play(ThunderClapVFXProfile profile, Vector3 position)
        {
            if (profile == null)
            {
                return;
            }

            gameObject.SetActive(true);
            transform.position = position;
            System.Random random = new(GetHashCode() ^ System.Environment.TickCount);
            float scale = profile.OverallScale;
            Color hot = profile.LightningCoreColor * profile.FlashBrightness * profile.OverallBrightness;
            hot.a = profile.LightningCoreColor.a;
            ThunderClapVFXUtility.EmitAt(groundCompression, position, 1, 1.25f * scale, profile.EarthColor, random);
            ThunderClapVFXUtility.EmitAt(centralFlash, position + Vector3.up * 0.08f, 2, 1.15f * scale, hot, random);
            ThunderClapVFXUtility.EmitRadial(heavyDust, position, profile.HeavyDustAmount, 3.6f * scale, 1.8f * scale, 0.86f * profile.EarthExplosionSize * scale, profile.WarmDustColor, random, 0.36f * scale);
            ThunderClapVFXUtility.EmitRadial(fineDust, position, profile.FineDustAmount, 2.55f * scale, 2.6f * scale, 0.42f * profile.EarthExplosionSize * scale, profile.WarmDustColor, random, 0.42f * scale);
            ThunderClapVFXUtility.EmitRadial(dirtChunks, position, profile.DirtChunkCount, profile.DebrisVelocity, 3.8f * scale, 0.2f * scale, profile.EarthColor, random, 0.26f * scale);
            ThunderClapVFXUtility.EmitRadial(rockFragments, position, profile.RockCount, profile.DebrisVelocity * 0.62f, 2.6f * scale, 0.28f * scale, profile.StoneColor, random, 0.3f * scale);
            ThunderClapVFXUtility.EmitRadial(electricalSparks, position + Vector3.up * 0.1f, profile.SparkAmount / 3, 6.8f * scale, 2.2f * scale, 0.12f * scale, profile.LightningCoreColor, random, 0.2f * scale);
        }

        public void ResetForPool()
        {
            foreach (ParticleSystem system in GetComponentsInChildren<ParticleSystem>(true))
            {
                ThunderClapVFXUtility.StopAndClear(system);
            }

            gameObject.SetActive(false);
        }
    }
}
