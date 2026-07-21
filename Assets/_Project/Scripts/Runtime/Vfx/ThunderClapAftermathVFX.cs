using System;
using UnityEngine;

namespace RPGClone.Vfx.Warrior
{
    [DisallowMultipleComponent]
    public sealed class ThunderClapAftermathVFX : MonoBehaviour
    {
        [SerializeField] private ParticleSystem rollingDust;
        [SerializeField] private ParticleSystem suspendedDust;
        [SerializeField] private ParticleSystem settlingDebris;
        [SerializeField] private ParticleSystem settlingSmoke;
        [SerializeField] private ParticleSystem residualFlickers;
        [SerializeField] private LineRenderer[] residualArcs = Array.Empty<LineRenderer>();

        private readonly Vector3[][] paths = new Vector3[8][];
        private ThunderClapVFXProfile profile;
        private System.Random random;
        private Vector3 center;
        private float startedAt;
        private float nextRefresh;
        private bool playing;

        public bool IsPlaying => playing;

        public void ConfigureAuthoring(ParticleSystem[] systems, LineRenderer[] newResidualArcs)
        {
            if (systems == null || systems.Length != 5)
            {
                throw new ArgumentException("Thunder Clap aftermath requires exactly five particle layers.");
            }

            rollingDust = systems[0];
            suspendedDust = systems[1];
            settlingDebris = systems[2];
            settlingSmoke = systems[3];
            residualFlickers = systems[4];
            residualArcs = newResidualArcs ?? Array.Empty<LineRenderer>();
        }

        public void Play(ThunderClapVFXProfile newProfile, Vector3 position)
        {
            profile = newProfile;
            if (profile == null)
            {
                return;
            }

            gameObject.SetActive(true);
            transform.position = position;
            center = position;
            random = new System.Random(GetHashCode() ^ System.Environment.TickCount);
            startedAt = Time.time;
            nextRefresh = 0f;
            playing = true;
            for (int i = 0; i < paths.Length; i++) paths[i] ??= new Vector3[6];
            SetLines(false);

            ThunderClapVFXUtility.EmitRing(rollingDust, center, Mathf.Max(8, profile.HeavyDustAmount / 2), profile.RingRadius * 0.45f, 0.65f, 0.38f, 0.62f * profile.OverallScale, profile.WarmDustColor, random);
            ThunderClapVFXUtility.EmitRing(suspendedDust, center + Vector3.up * 0.08f, Mathf.Max(10, profile.FineDustAmount / 2), profile.RingRadius * 0.34f, 0.28f, 0.7f, 0.36f * profile.OverallScale, profile.WarmDustColor, random);
            ThunderClapVFXUtility.EmitRing(settlingDebris, center, Mathf.Max(5, profile.RockCount / 2), profile.RingRadius * 0.28f, 0.35f, 0.8f, 0.14f * profile.OverallScale, profile.StoneColor, random);
            ThunderClapVFXUtility.EmitRing(settlingSmoke, center + Vector3.up * 0.12f, Mathf.Max(4, profile.HeavyDustAmount / 7), profile.RingRadius * 0.2f, 0.18f, 0.52f, 0.54f * profile.OverallScale, new Color(0.28f, 0.24f, 0.22f, 0.48f), random);
            ThunderClapVFXUtility.EmitRing(residualFlickers, center + Vector3.up * 0.04f, Mathf.Max(4, profile.BranchCount), profile.RingRadius * 0.48f, 0.08f, 0.1f, 0.12f * profile.OverallScale, profile.LightningColor, random);
        }

        public void ResetForPool()
        {
            playing = false;
            profile = null;
            SetLines(false);
            foreach (ParticleSystem system in GetComponentsInChildren<ParticleSystem>(true))
            {
                ThunderClapVFXUtility.StopAndClear(system);
            }

            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!playing || profile == null)
            {
                return;
            }

            float elapsed = Time.time - startedAt;
            if (Time.time >= nextRefresh)
            {
                RefreshResidualArcs(elapsed);
                nextRefresh = Time.time + 0.11f;
            }

            float normalized = Mathf.Clamp01(elapsed / profile.AftermathDuration);
            int active = Mathf.Min(3, residualArcs.Length);
            for (int i = 0; i < residualArcs.Length; i++)
            {
                if (i >= active)
                {
                    residualArcs[i].enabled = false;
                    continue;
                }

                float pulse = Mathf.Repeat(elapsed * 5.2f + i * 0.31f, 1f) < 0.14f ? 1f : 0f;
                MMOProceduralVfxUtility.SetLine(residualArcs[i], paths[i], profile.RingThickness * 0.45f, profile.LightningColor, (1f - normalized) * pulse * 0.55f, elapsed * 4f, 1.25f);
            }

            if (normalized >= 1f)
            {
                playing = false;
                SetLines(false);
            }
        }

        private void RefreshResidualArcs(float elapsed)
        {
            int active = Mathf.Min(3, residualArcs.Length);
            for (int i = 0; i < active; i++)
            {
                float angle = MMOProceduralVfxUtility.Next01(random) * Mathf.PI * 2f;
                Vector3 radial = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                float radius = Mathf.Lerp(profile.RingRadius * 0.18f, profile.RingRadius * 0.78f, MMOProceduralVfxUtility.Next01(random));
                Vector3 start = center + radial * radius + Vector3.up * 0.045f;
                Vector3 tangent = Vector3.Cross(Vector3.up, radial);
                Vector3 end = start + tangent * Mathf.Lerp(0.45f, 1.1f, MMOProceduralVfxUtility.Next01(random)) + radial * 0.3f;
                MMOProceduralVfxUtility.BuildJaggedPath(paths[i], start, end, 0.08f, 2, random, elapsed + i * 0.2f);
            }
        }

        private void SetLines(bool enabled)
        {
            foreach (LineRenderer line in residualArcs)
            {
                if (line != null) line.enabled = enabled;
            }
        }
    }
}
