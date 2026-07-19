using UnityEngine;

namespace RPGClone.Vfx.Water
{
    public enum WaterShieldReactionMode
    {
        Absorb,
        ManaRestore
    }

    [DisallowMultipleComponent]
    public sealed class WaterShieldReactionVFX : MonoBehaviour
    {
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
        private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");

        [SerializeField] private WaterShieldVFXProfile profile;
        [SerializeField] private WaterShieldReactionMode mode;
        [SerializeField] private bool destroyOnComplete = true;
        [SerializeField] private Renderer protectiveArc;
        [SerializeField] private Renderer chestPulse;
        [SerializeField] private LineRenderer manaStream;
        [SerializeField] private ParticleSystem splash;
        [SerializeField] private ParticleSystem droplets;
        [SerializeField] private ParticleSystem manaMotes;

        private MaterialPropertyBlock propertyBlock;
        private Transform sourceOrb;
        private Transform caster;
        private Vector3 incomingDirection;
        private float startedAt;
        private bool playing;

        public bool IsPlaying => playing;

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            StopImmediate();
        }

        private void LateUpdate()
        {
            if (!playing || profile == null)
            {
                return;
            }

            float elapsed = Time.time - startedAt;
            float duration = mode == WaterShieldReactionMode.Absorb ? 0.46f : 0.62f;
            float normalized = Mathf.Clamp01(elapsed / duration);
            float pulse = Mathf.Sin(normalized * Mathf.PI);

            if (mode == WaterShieldReactionMode.Absorb)
            {
                AnimateAbsorb(normalized, pulse);
            }
            else
            {
                AnimateMana(normalized, pulse);
            }

            if (elapsed >= duration)
            {
                Complete();
            }
        }

        public void Play(WaterShieldVFXProfile newProfile, Transform newSourceOrb, Transform newCaster, Vector3 newIncomingDirection)
        {
            profile = newProfile != null ? newProfile : profile;
            sourceOrb = newSourceOrb;
            caster = newCaster;
            incomingDirection = newIncomingDirection.sqrMagnitude > 0.001f ? newIncomingDirection.normalized : Vector3.forward;
            if (profile == null || caster == null)
            {
                StopImmediate();
                return;
            }

            transform.position = mode == WaterShieldReactionMode.Absorb
                ? caster.position + Vector3.up * 1.05f - incomingDirection * 0.62f
                : sourceOrb != null ? sourceOrb.position : caster.position + Vector3.up;
            transform.rotation = Quaternion.LookRotation(-incomingDirection, Vector3.up);
            PlayBurst(splash);
            PlayBurst(droplets);
            PlayBurst(manaMotes);
            startedAt = Time.time;
            playing = true;
        }

        public void StopImmediate()
        {
            playing = false;
            StopAndClear(splash);
            StopAndClear(droplets);
            StopAndClear(manaMotes);
            if (protectiveArc != null) protectiveArc.gameObject.SetActive(false);
            if (chestPulse != null) chestPulse.gameObject.SetActive(false);
            if (manaStream != null) manaStream.enabled = false;
        }

        public void ConfigureAuthoring(
            WaterShieldVFXProfile newProfile,
            WaterShieldReactionMode newMode,
            bool newDestroyOnComplete,
            Renderer newProtectiveArc,
            Renderer newChestPulse,
            LineRenderer newManaStream,
            ParticleSystem newSplash,
            ParticleSystem newDroplets,
            ParticleSystem newManaMotes)
        {
            profile = newProfile;
            mode = newMode;
            destroyOnComplete = newDestroyOnComplete;
            protectiveArc = newProtectiveArc;
            chestPulse = newChestPulse;
            manaStream = newManaStream;
            splash = newSplash;
            droplets = newDroplets;
            manaMotes = newManaMotes;
        }

        private void AnimateAbsorb(float normalized, float pulse)
        {
            if (protectiveArc != null)
            {
                protectiveArc.gameObject.SetActive(true);
                protectiveArc.transform.localScale = Vector3.one * profile.AbsorbReactionScale * Mathf.Lerp(0.55f, 1.18f, Smooth01(normalized));
            }

            SetRenderer(protectiveArc, profile.Colors.PaleCyan, pulse, profile.AbsorbFlashIntensity * profile.OverallBrightness, normalized);
        }

        private void AnimateMana(float normalized, float pulse)
        {
            if (sourceOrb == null || caster == null)
            {
                return;
            }

            Vector3 start = sourceOrb.position;
            Vector3 end = caster.position + Vector3.up * 1.05f;
            if (manaStream != null)
            {
                manaStream.enabled = true;
                Vector3 side = Vector3.Cross(Vector3.up, end - start).normalized * 0.22f;
                for (int i = 0; i < manaStream.positionCount; i++)
                {
                    float t = i / (float)(manaStream.positionCount - 1);
                    float travelHead = Mathf.Clamp01(normalized * profile.ManaTransferSpeed - t * 0.7f);
                    Vector3 point = Vector3.Lerp(start, end, t) + side * Mathf.Sin(t * Mathf.PI * 2f + normalized * 7f) * Mathf.Sin(t * Mathf.PI);
                    manaStream.SetPosition(i, Vector3.Lerp(start, point, travelHead));
                }

                Color color = profile.Colors.Aqua;
                color.a = pulse;
                manaStream.startColor = color;
                color = profile.Colors.ManaViolet;
                color.a = 0f;
                manaStream.endColor = color;
            }

            if (chestPulse != null)
            {
                chestPulse.gameObject.SetActive(true);
                chestPulse.transform.position = end;
                chestPulse.transform.localScale = Vector3.one * Mathf.Lerp(0.18f, 0.75f, pulse);
            }

            SetRenderer(chestPulse, profile.Colors.ManaViolet, pulse, profile.ChestPulseIntensity * profile.OverallBrightness, 1f - pulse);
        }

        private void SetRenderer(Renderer renderer, Color tint, float opacity, float brightness, float dissolve)
        {
            if (renderer == null) return;
            propertyBlock ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(TintId, tint);
            propertyBlock.SetFloat(OpacityId, Mathf.Clamp01(opacity));
            propertyBlock.SetFloat(BrightnessId, Mathf.Max(0f, brightness));
            propertyBlock.SetFloat(DissolveId, Mathf.Clamp01(dissolve));
            renderer.SetPropertyBlock(propertyBlock);
            propertyBlock.Clear();
        }

        private void Complete()
        {
            playing = false;
            if (destroyOnComplete && Application.isPlaying)
            {
                Destroy(gameObject, 0.12f);
            }
            else
            {
                StopImmediate();
            }
        }

        private static void PlayBurst(ParticleSystem particles)
        {
            if (particles == null) return;
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.Play(true);
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
