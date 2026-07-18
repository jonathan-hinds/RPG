using UnityEngine;

namespace RPGClone.Vfx.Warrior
{
    public enum BerzerkitisHandSide
    {
        Left,
        Right
    }

    [DisallowMultipleComponent]
    public sealed class BerzerkitisHandVFX : MonoBehaviour
    {
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
        private static readonly int ScrollSpeedId = Shader.PropertyToID("_ScrollSpeed");
        private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");

        [Header("Configuration")]
        [SerializeField] private BerzerkitisVFXProfile profile;
        [SerializeField] private BerzerkitisHandSide side;

        [Header("Layers")]
        [SerializeField] private Renderer coreGlow;
        [SerializeField] private ParticleSystem mainFlames;
        [SerializeField] private ParticleSystem outerFlames;
        [SerializeField] private Renderer wristRibbon;
        [SerializeField] private ParticleSystem embers;
        [SerializeField] private ParticleSystem attackSparks;
        [SerializeField] private TrailRenderer motionTrail;

        private MaterialPropertyBlock propertyBlock;
        private float startedAt;
        private float attackPulseStartedAt = float.NegativeInfinity;
        private float fadeStartedAt = float.NegativeInfinity;
        private Vector3 lastWorldPosition;
        private bool playing;
        private bool fading;

        public BerzerkitisHandSide Side => side;
        public bool IsPlaying => playing;

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            StopImmediate();
        }

        private void OnDisable()
        {
            playing = false;
            fading = false;
        }

        private void LateUpdate()
        {
            if (!playing || profile == null)
            {
                return;
            }

            float elapsed = Time.time - startedAt;
            float pulse = EvaluateAttackPulse();
            float fade = EvaluateFade();
            float loop = 0.92f + Mathf.Sin(elapsed * 7.1f + (side == BerzerkitisHandSide.Left ? 0f : 1.9f)) * 0.08f;
            float intensity = Mathf.Max(0f, loop + pulse * profile.AttackPulseIntensity) * fade;

            ApplyLayer(coreGlow, profile.Colors.WhiteHot, intensity, profile.CoreBrightness * profile.OverallBrightness * (1f + pulse), Vector2.zero, 1f - fade);
            ApplyLayer(wristRibbon, profile.Colors.BloodRed, intensity * 0.68f, profile.OverallBrightness * 0.9f, new Vector2(-0.7f, 0f), 1f - fade);

            if (coreGlow != null)
            {
                float coreScale = profile.HandFlameScale * (1f + pulse * 0.34f) * fade;
                coreGlow.transform.localScale = Vector3.one * Mathf.Max(0.001f, coreScale);
                float direction = side == BerzerkitisHandSide.Left ? -1f : 1f;
                coreGlow.transform.Rotate(Vector3.up, direction * 24f * Time.deltaTime, Space.Self);
            }

            if (wristRibbon != null)
            {
                float direction = side == BerzerkitisHandSide.Left ? -1f : 1f;
                wristRibbon.transform.Rotate(Vector3.forward, direction * profile.WristRibbonSpeed * Time.deltaTime, Space.Self);
            }

            ConfigureParticleIntensity(mainFlames, profile.FlameDensity, intensity, profile.FlameHeight, profile.Colors.HotYellow);
            ConfigureParticleIntensity(outerFlames, Mathf.Max(1, profile.FlameDensity / 2), intensity * profile.OuterFlameIntensity, profile.FlameHeight * 1.12f, profile.Colors.BloodRed);
            ConfigureEmbers(fade);
            UpdateMotionTrail(fade);

            if (fading && fade <= 0f)
            {
                StopImmediate();
            }
        }

        public void Play(BerzerkitisVFXProfile newProfile)
        {
            profile = newProfile != null ? newProfile : profile;
            if (profile == null)
            {
                Debug.LogError("BerzerkitisHandVFX requires a profile.", this);
                return;
            }

            transform.localPosition = side == BerzerkitisHandSide.Left
                ? profile.LeftHandPositionOffset
                : profile.RightHandPositionOffset;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            startedAt = Time.time;
            attackPulseStartedAt = float.NegativeInfinity;
            fadeStartedAt = float.NegativeInfinity;
            lastWorldPosition = transform.position;
            fading = false;
            playing = true;

            PlayParticles(mainFlames);
            PlayParticles(outerFlames);
            PlayParticles(embers);
            if (attackSparks != null)
            {
                attackSparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            if (motionTrail != null)
            {
                motionTrail.Clear();
                motionTrail.time = profile.MotionTrailLifetime;
                motionTrail.widthMultiplier = profile.MotionTrailWidth;
                motionTrail.emitting = false;
            }
        }

        public void PulseAttack()
        {
            if (!playing || fading || profile == null)
            {
                return;
            }

            attackPulseStartedAt = Time.time;
            if (attackSparks != null)
            {
                attackSparks.Emit(Mathf.Clamp(profile.FlameDensity, 3, 12));
            }
        }

        public void FadeOut()
        {
            if (!playing || fading || profile == null)
            {
                return;
            }

            fading = true;
            fadeStartedAt = Time.time;
            StopEmission(mainFlames);
            StopEmission(outerFlames);
            StopEmission(embers);
            if (motionTrail != null)
            {
                motionTrail.emitting = false;
            }
        }

        public void StopImmediate()
        {
            playing = false;
            fading = false;
            StopAndClear(mainFlames);
            StopAndClear(outerFlames);
            StopAndClear(embers);
            StopAndClear(attackSparks);
            if (motionTrail != null)
            {
                motionTrail.emitting = false;
                motionTrail.Clear();
            }

            ApplyLayer(coreGlow, Color.white, 0f, 0f, Vector2.zero, 1f);
            ApplyLayer(wristRibbon, Color.white, 0f, 0f, Vector2.zero, 1f);
        }

        public void ConfigureAuthoring(
            BerzerkitisVFXProfile newProfile,
            BerzerkitisHandSide newSide,
            Renderer newCoreGlow,
            ParticleSystem newMainFlames,
            ParticleSystem newOuterFlames,
            Renderer newWristRibbon,
            ParticleSystem newEmbers,
            ParticleSystem newAttackSparks,
            TrailRenderer newMotionTrail)
        {
            profile = newProfile;
            side = newSide;
            coreGlow = newCoreGlow;
            mainFlames = newMainFlames;
            outerFlames = newOuterFlames;
            wristRibbon = newWristRibbon;
            embers = newEmbers;
            attackSparks = newAttackSparks;
            motionTrail = newMotionTrail;
        }

        private float EvaluateAttackPulse()
        {
            float duration = Mathf.Max(0.01f, profile.AttackPulseDuration);
            float normalized = (Time.time - attackPulseStartedAt) / duration;
            return normalized is >= 0f and < 1f ? Mathf.Sin(normalized * Mathf.PI) : 0f;
        }

        private float EvaluateFade()
        {
            if (!fading)
            {
                return 1f;
            }

            return 1f - Smooth01((Time.time - fadeStartedAt) / Mathf.Max(0.01f, profile.BuffFadeOutDuration));
        }

        private void UpdateMotionTrail(float fade)
        {
            if (motionTrail == null)
            {
                return;
            }

            float speed = Vector3.Distance(transform.position, lastWorldPosition) / Mathf.Max(0.0001f, Time.deltaTime);
            motionTrail.emitting = !fading && speed > 2.2f;
            motionTrail.widthMultiplier = profile.MotionTrailWidth * fade;
            lastWorldPosition = transform.position;
        }

        private void ConfigureEmbers(float fade)
        {
            if (embers == null)
            {
                return;
            }

            ParticleSystem.EmissionModule emission = embers.emission;
            emission.rateOverTime = fading ? 0f : profile.EmberSpawnRate * fade;
        }

        private static void ConfigureParticleIntensity(ParticleSystem particles, int density, float intensity, float height, Color color)
        {
            if (particles == null)
            {
                return;
            }

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = Mathf.Max(0f, density * 2.5f * intensity);
            ParticleSystem.MainModule main = particles.main;
            main.startSize = new ParticleSystem.MinMaxCurve(height * 0.42f, height * 0.72f * (1f + Mathf.Max(0f, intensity - 1f) * 0.25f));
            main.startColor = color;
        }

        private void ApplyLayer(Renderer renderer, Color tint, float opacity, float brightness, Vector2 scrollSpeed, float dissolve)
        {
            if (renderer == null)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(TintId, tint);
            propertyBlock.SetFloat(OpacityId, Mathf.Clamp01(opacity));
            propertyBlock.SetFloat(BrightnessId, Mathf.Max(0f, brightness));
            propertyBlock.SetVector(ScrollSpeedId, scrollSpeed);
            propertyBlock.SetFloat(DissolveId, Mathf.Clamp01(dissolve));
            renderer.SetPropertyBlock(propertyBlock);
            propertyBlock.Clear();
        }

        private static void PlayParticles(ParticleSystem particles)
        {
            if (particles == null)
            {
                return;
            }

            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.Play(true);
        }

        private static void StopEmission(ParticleSystem particles)
        {
            particles?.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private static void StopAndClear(ParticleSystem particles)
        {
            particles?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }
    }
}
