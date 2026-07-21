using System;
using UnityEngine;

namespace RPGClone.Vfx.Warrior
{
    [DisallowMultipleComponent]
    public sealed class ThunderClapTargetReactionVFX : MonoBehaviour
    {
        [SerializeField] private ParticleSystem bodyFlash;
        [SerializeField] private ParticleSystem footBurst;
        [SerializeField] private ParticleSystem debuffBands;
        [SerializeField] private ParticleSystem breakSparks;
        [SerializeField] private LineRenderer[] bodyArcs = Array.Empty<LineRenderer>();

        private readonly Vector3[][] paths = new Vector3[12][];
        private ThunderClapVFXProfile profile;
        private Transform target;
        private System.Random random;
        private Action<ThunderClapTargetReactionVFX> completed;
        private float startedAt;
        private float nextRefresh;
        private bool playing;

        public bool IsPlaying => playing;

        public void ConfigureAuthoring(ParticleSystem[] systems, LineRenderer[] newBodyArcs)
        {
            if (systems == null || systems.Length != 4)
            {
                throw new ArgumentException("Thunder Clap target reaction requires exactly four particle layers.");
            }

            bodyFlash = systems[0];
            footBurst = systems[1];
            debuffBands = systems[2];
            breakSparks = systems[3];
            bodyArcs = newBodyArcs ?? Array.Empty<LineRenderer>();
        }

        public void Play(
            ThunderClapVFXProfile newProfile,
            Transform newTarget,
            Action<ThunderClapTargetReactionVFX> onCompleted)
        {
            profile = newProfile;
            target = newTarget;
            completed = onCompleted;
            playing = profile != null && target != null;
            gameObject.SetActive(playing);
            if (!playing)
            {
                return;
            }

            random = new System.Random(GetHashCode() ^ System.Environment.TickCount);
            startedAt = Time.time;
            nextRefresh = 0f;
            transform.position = target.position;
            for (int i = 0; i < paths.Length; i++) paths[i] ??= new Vector3[6];
            SetLines(false);

            Vector3 torso = ResolveTorso();
            Vector3 localTorso = transform.InverseTransformPoint(torso);
            ThunderClapVFXUtility.EmitAt(bodyFlash, localTorso, 1, 0.82f * profile.OverallScale, profile.LightningCoreColor, random);
            ThunderClapVFXUtility.EmitRadial(footBurst, target.position + Vector3.up * 0.04f, 10, 1.7f, 0.65f, 0.32f, profile.WarmDustColor, random, 0.24f);
            ThunderClapVFXUtility.EmitAt(debuffBands, Vector3.up * 0.72f, 2, profile.DebuffRingSize, profile.LightningColor, random);
            ThunderClapVFXUtility.EmitRadial(breakSparks, localTorso, 14, 2.8f, 1.2f, 0.08f, profile.LightningCoreColor, random, 0.16f);
        }

        public void ResetForPool()
        {
            playing = false;
            target = null;
            profile = null;
            completed = null;
            SetLines(false);
            foreach (ParticleSystem system in GetComponentsInChildren<ParticleSystem>(true))
            {
                ThunderClapVFXUtility.StopAndClear(system);
            }

            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!playing || profile == null || target == null)
            {
                return;
            }

            transform.position = target.position;
            float elapsed = Time.time - startedAt;
            if (Time.time >= nextRefresh)
            {
                RefreshArcs(elapsed);
                nextRefresh = Time.time + 0.045f;
            }

            float fade = 1f - Mathf.SmoothStep(0f, 1f, elapsed / profile.ArcLifetime);
            int active = Mathf.Min(profile.BodyArcCount, bodyArcs.Length);
            for (int i = 0; i < bodyArcs.Length; i++)
            {
                if (i >= active)
                {
                    bodyArcs[i].enabled = false;
                    continue;
                }

                float flicker = Mathf.Repeat(elapsed * 18f + i * 0.27f, 1f) > 0.2f ? 1f : 0f;
                Color color = i % 3 == 2 ? profile.LightningVioletColor : profile.LightningColor;
                MMOProceduralVfxUtility.SetLine(bodyArcs[i], paths[i], profile.RingThickness * 0.7f, color, fade * flicker, -elapsed * 9f, 1.15f);
            }

            if (elapsed >= Mathf.Max(0.62f, profile.ArcLifetime + 0.18f))
            {
                playing = false;
                SetLines(false);
                completed?.Invoke(this);
            }
        }

        private void RefreshArcs(float elapsed)
        {
            Vector3 torso = ResolveTorso();
            int active = Mathf.Min(profile.BodyArcCount, bodyArcs.Length);
            for (int i = 0; i < active; i++)
            {
                Vector3 a = torso + new Vector3(MMOProceduralVfxUtility.NextSigned(random) * 0.36f, MMOProceduralVfxUtility.NextSigned(random) * 0.62f, MMOProceduralVfxUtility.NextSigned(random) * 0.3f);
                Vector3 b = torso + new Vector3(MMOProceduralVfxUtility.NextSigned(random) * 0.44f, MMOProceduralVfxUtility.NextSigned(random) * 0.78f, MMOProceduralVfxUtility.NextSigned(random) * 0.34f);
                MMOProceduralVfxUtility.BuildJaggedPath(paths[i], a, b, 0.11f, 2, random, elapsed + i * 0.24f);
            }
        }

        private Vector3 ResolveTorso()
        {
            return target != null ? target.position + Vector3.up * 1.02f : transform.position + Vector3.up;
        }

        private void SetLines(bool enabled)
        {
            foreach (LineRenderer line in bodyArcs)
            {
                if (line != null) line.enabled = enabled;
            }
        }
    }
}
