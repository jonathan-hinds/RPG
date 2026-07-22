using RPGClone.Buffs;
using UnityEngine;

namespace RPGClone.Vfx.Shaman
{
    /// <summary>
    /// Visual-only listener for the receiver-local replicated Frost Shock buff. It never owns debuff timing or movement.
    /// </summary>
    public sealed class FrostShockSlowDebuffVFX : MonoBehaviour, IMMOAbilityVfxInstance
    {
        public const string FrostShockBuffId = "shaman_frost_shock";

        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
        private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
        private static readonly int ScrollId = Shader.PropertyToID("_Scroll");

        [SerializeField] private FrostShockVFXProfile profile;
        [SerializeField] private Transform slowRoot;
        [SerializeField] private Transform expirationRoot;
        [SerializeField] private Renderer[] footIceLayers;
        [SerializeField] private Renderer[] lowerLegFrost;
        [SerializeField] private Renderer[] bodyFrostPatches;
        [SerializeField] private Renderer[] energyBands;
        [SerializeField] private Renderer[] crackFlickers;
        [SerializeField] private Renderer[] expirationFragments;
        [SerializeField] private ParticleSystem persistentMist;
        [SerializeField] private ParticleSystem persistentSnow;
        [SerializeField] private ParticleSystem movementTrail;
        [SerializeField] private ParticleSystem crackParticles;
        [SerializeField] private ParticleSystem shatterFragments;
        [SerializeField] private ParticleSystem finalMist;
        [SerializeField] private ParticleSystem finalSnow;

        private MaterialPropertyBlock propertyBlock;
        private Transform target;
        private Transform ownerRoot;
        private MMOCharacterBuffController buffController;
        private MMOActiveBuff activeBuff;
        private float initializedAt;
        private float expirationStartedAt;
        private float nextCrackAt;
        private float movementPulse;
        private Vector3 lastTargetPosition;
        private Vector3 previousVelocity;
        private Vector3[] footBaseScales;
        private bool active;
        private bool expiring;

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        public void Initialize(MMOAbilityVfxContext context)
        {
            target = context.Target;
            ownerRoot = transform.parent != null && transform.parent.GetComponent<FrostShockImpactVFX>() != null ? transform.parent : transform;
            initializedAt = Time.time;
            expirationStartedAt = 0f;
            nextCrackAt = Time.time + Random.Range(0.25f, 0.7f);
            active = false;
            expiring = false;
            slowRoot.gameObject.SetActive(false);
            expirationRoot.gameObject.SetActive(false);

            if (target == null)
            {
                Destroy(ownerRoot.gameObject, 1.1f);
                return;
            }

            FrostShockSlowDebuffVFX[] existing = target.GetComponentsInChildren<FrostShockSlowDebuffVFX>(true);
            foreach (FrostShockSlowDebuffVFX effect in existing)
            {
                if (effect != null && effect != this)
                {
                    effect.CancelImmediate();
                }
            }

            buffController = target.GetComponent<MMOCharacterBuffController>();
            lastTargetPosition = target.position;
            footBaseScales = new Vector3[footIceLayers.Length];
            for (int i = 0; i < footIceLayers.Length; i++)
            {
                footBaseScales[i] = footIceLayers[i].transform.localScale;
            }
            TryBeginFromReplicatedBuff();
        }

        private void LateUpdate()
        {
            if (profile == null || target == null)
            {
                return;
            }

            if (expiring)
            {
                AnimateExpiration(Time.time - expirationStartedAt);
                return;
            }

            if (!active)
            {
                if (TryBeginFromReplicatedBuff())
                {
                    return;
                }

                if (Time.time - initializedAt > 1.5f)
                {
                    Destroy(ownerRoot.gameObject);
                }

                return;
            }

            activeBuff = buffController != null ? buffController.FindBuff(FrostShockBuffId) : null;
            if (activeBuff == null || activeBuff.IsExpired)
            {
                BeginExpiration();
                return;
            }

            Vector3 displacement = target.position - lastTargetPosition;
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 velocity = displacement / dt;
            velocity.y = 0f;
            float speed = velocity.magnitude;
            float directionChange = previousVelocity.sqrMagnitude > 0.01f && velocity.sqrMagnitude > 0.01f
                ? 1f - Mathf.Clamp01(Vector3.Dot(previousVelocity.normalized, velocity.normalized))
                : 0f;
            if ((previousVelocity.sqrMagnitude <= 0.04f && speed > 0.25f) || directionChange > 0.55f)
            {
                movementPulse = 1f;
            }

            movementPulse = Mathf.MoveTowards(movementPulse, 0f, Time.deltaTime * 3.6f);
            previousVelocity = velocity;
            lastTargetPosition = target.position;
            UpdateMovementEmission(speed);
            AnimateSlow(activeBuff.NormalizedRemaining, speed);
        }

        public void ConfigureAuthoring(
            FrostShockVFXProfile newProfile,
            Transform newSlowRoot,
            Transform newExpirationRoot,
            Renderer[] newFootIceLayers,
            Renderer[] newLowerLegFrost,
            Renderer[] newBodyFrostPatches,
            Renderer[] newEnergyBands,
            Renderer[] newCrackFlickers,
            Renderer[] newExpirationFragments,
            ParticleSystem newPersistentMist,
            ParticleSystem newPersistentSnow,
            ParticleSystem newMovementTrail,
            ParticleSystem newCrackParticles,
            ParticleSystem newShatterFragments,
            ParticleSystem newFinalMist,
            ParticleSystem newFinalSnow)
        {
            profile = newProfile;
            slowRoot = newSlowRoot;
            expirationRoot = newExpirationRoot;
            footIceLayers = newFootIceLayers;
            lowerLegFrost = newLowerLegFrost;
            bodyFrostPatches = newBodyFrostPatches;
            energyBands = newEnergyBands;
            crackFlickers = newCrackFlickers;
            expirationFragments = newExpirationFragments;
            persistentMist = newPersistentMist;
            persistentSnow = newPersistentSnow;
            movementTrail = newMovementTrail;
            crackParticles = newCrackParticles;
            shatterFragments = newShatterFragments;
            finalMist = newFinalMist;
            finalSnow = newFinalSnow;
        }

        public void CancelImmediate()
        {
            active = false;
            expiring = false;
            StopPersistentParticles(true);
            if (ownerRoot != null)
            {
                Destroy(ownerRoot.gameObject);
            }
        }

        private bool TryBeginFromReplicatedBuff()
        {
            if (buffController == null && target != null)
            {
                buffController = target.GetComponent<MMOCharacterBuffController>();
            }

            activeBuff = buffController != null ? buffController.FindBuff(FrostShockBuffId) : null;
            if (activeBuff == null || activeBuff.IsExpired)
            {
                return false;
            }

            active = true;
            slowRoot.gameObject.SetActive(true);
            expirationRoot.gameObject.SetActive(false);
            slowRoot.localScale = Vector3.one * profile.OverallScale;
            ConfigurePersistentParticles();
            persistentMist?.Play(true);
            persistentSnow?.Play(true);
            movementTrail?.Play(true);
            AnimateSlow(activeBuff.NormalizedRemaining, 0f);
            return true;
        }

        private void AnimateSlow(float normalizedRemaining, float speed)
        {
            float authoritativePresentationDuration = activeBuff != null ? activeBuff.DurationSeconds : profile.DebuffDuration;
            float elapsed = 1f - Mathf.Clamp01(normalizedRemaining);
            float formation = Smooth01(Mathf.Clamp01(elapsed / Mathf.Max(0.01f, 0.5f / authoritativePresentationDuration)));
            float finalSecondStart = Mathf.Clamp01(1f - 1f / authoritativePresentationDuration);
            float ending = elapsed <= finalSecondStart ? 0f : Smooth01(Mathf.InverseLerp(finalSecondStart, 1f, elapsed));
            float stableAlpha = formation * (1f - ending * 0.72f);
            float pulseScale = 1f + movementPulse * profile.MovementPulseIntensity;

            for (int i = 0; i < footIceLayers.Length; i++)
            {
                Renderer renderer = footIceLayers[i];
                float layerScale = profile.FootIceScale * Mathf.Lerp(0.28f, 1f, formation) * pulseScale;
                renderer.transform.localScale = footBaseScales[i] * layerScale;
                Color tint = i % 4 == 0 ? profile.DeepBlueColor : i % 4 == 1 ? profile.SaturatedBlueColor : i % 4 == 2 ? profile.PaleCyanColor : profile.WhiteHotColor;
                SetRenderer(renderer, tint, stableAlpha * (0.95f - i * 0.045f), profile.FootIceBrightness * profile.OverallBrightness * (1f + movementPulse * 0.45f), ending * 0.55f, new Vector4(-0.035f * (i + 1), 0.02f, 0.01f, -0.015f));
            }

            for (int i = 0; i < lowerLegFrost.Length; i++)
            {
                float offset = i * 0.17f;
                float alpha = stableAlpha * profile.LowerLegFrostCoverage * (0.72f + 0.18f * Mathf.Sin(Time.time * 1.4f + offset));
                SetRenderer(lowerLegFrost[i], i % 2 == 0 ? profile.SaturatedBlueColor : profile.PaleCyanColor, alpha, profile.OverallBrightness * 1.35f, ending * 0.68f, new Vector4(0.02f, 0.035f + i * 0.004f, -0.02f, 0.01f));
            }

            for (int i = 0; i < bodyFrostPatches.Length; i++)
            {
                float reform = 0.58f + Mathf.Sin(Time.time * (0.62f + i * 0.07f) + i * 1.71f) * 0.22f;
                SetRenderer(bodyFrostPatches[i], profile.PaleCyanColor, stableAlpha * profile.BodyFrostCoverage * reform, profile.OverallBrightness, ending * 0.8f, new Vector4(-0.012f, 0.018f, 0.008f, -0.015f));
            }

            for (int i = 0; i < energyBands.Length; i++)
            {
                Renderer band = energyBands[i];
                band.transform.localRotation = Quaternion.Euler(0f, Time.time * profile.EnergyBandSpeed * (i == 0 ? 1f : -0.72f), 0f);
                float bandPulse = 0.72f + Mathf.Sin(Time.time * 1.2f + i * 2.1f) * 0.16f;
                SetRenderer(band, i == 0 ? profile.PaleCyanColor : profile.VioletBlueAccent, stableAlpha * bandPulse * 0.5f, profile.OverallBrightness * 1.45f, ending, new Vector4(-0.24f + i * 0.08f, 0f, 0.05f, -0.03f));
            }

            UpdateCrackFlicker(stableAlpha);
        }

        private void UpdateCrackFlicker(float stableAlpha)
        {
            if (Time.time >= nextCrackAt)
            {
                nextCrackAt = Time.time + Random.Range(profile.CrackFlickerFrequency * 0.7f, profile.CrackFlickerFrequency * 1.45f);
                crackParticles?.Emit(Mathf.Max(1, Mathf.RoundToInt(3f * profile.QualityMultiplier)));
            }

            for (int i = 0; i < crackFlickers.Length; i++)
            {
                float wave = Mathf.Repeat(Time.time * (1.2f + i * 0.13f) + i * 0.29f, Mathf.Max(0.12f, profile.CrackFlickerFrequency));
                float alpha = wave < 0.16f ? Mathf.Sin(wave / 0.16f * Mathf.PI) : 0f;
                SetRenderer(crackFlickers[i], profile.WhiteHotColor, alpha * stableAlpha * 0.75f, profile.OverallBrightness * 2.3f, 0.2f, Vector4.zero);
            }
        }

        private void BeginExpiration()
        {
            if (expiring)
            {
                return;
            }

            active = false;
            expiring = true;
            expirationStartedAt = Time.time;
            expirationRoot.gameObject.SetActive(true);
            StopPersistentParticles(false);
            ConfigureBurst(shatterFragments, Mathf.RoundToInt(profile.ShatterFragmentCount * profile.QualityMultiplier));
            ConfigureBurst(finalMist, Mathf.RoundToInt(profile.FinalMistAmount * profile.QualityMultiplier));
            ConfigureBurst(finalSnow, Mathf.RoundToInt(profile.FinalSnowAmount * profile.QualityMultiplier));
            shatterFragments?.Play(true);
            finalMist?.Play(true);
            finalSnow?.Play(true);
        }

        private void AnimateExpiration(float elapsed)
        {
            float fracture = Mathf.Clamp01(elapsed / Mathf.Max(0.05f, profile.FractureDuration));
            float dissolve = Mathf.Clamp01((elapsed - profile.FractureDuration * 0.35f) / Mathf.Max(0.05f, profile.FrostDissolveDuration));
            for (int i = 0; i < footIceLayers.Length; i++)
            {
                SetRenderer(footIceLayers[i], profile.PaleCyanColor, 1f - Smooth01(dissolve), profile.OverallBrightness * Mathf.Lerp(2.3f, 0.35f, dissolve), dissolve, Vector4.zero);
            }

            foreach (Renderer renderer in lowerLegFrost)
            {
                SetRenderer(renderer, profile.SaturatedBlueColor, (1f - dissolve) * 0.62f, profile.OverallBrightness, dissolve, Vector4.zero);
            }

            foreach (Renderer renderer in bodyFrostPatches)
            {
                SetRenderer(renderer, profile.PaleCyanColor, (1f - dissolve) * 0.3f, profile.OverallBrightness, dissolve, Vector4.zero);
            }

            for (int i = 0; i < expirationFragments.Length; i++)
            {
                Renderer fragment = expirationFragments[i];
                float delay = i * 0.018f;
                float progress = Mathf.Clamp01((elapsed - delay) / 0.58f);
                fragment.transform.localPosition += fragment.transform.localRotation * Vector3.forward * (profile.ShatterVelocity * Time.deltaTime * (1f - progress));
                fragment.transform.Rotate(new Vector3(83f + i * 7f, 116f - i * 5f, 62f) * Time.deltaTime, Space.Self);
                SetRenderer(fragment, i % 2 == 0 ? profile.PaleCyanColor : profile.SaturatedBlueColor, 1f - Smooth01(progress), profile.OverallBrightness * 1.4f, progress, Vector4.zero);
            }

            if (elapsed >= profile.FractureDuration + profile.FrostDissolveDuration + 0.32f)
            {
                expiring = false;
                Destroy(ownerRoot.gameObject);
            }
        }

        private void ConfigurePersistentParticles()
        {
            SetRate(persistentMist, Mathf.RoundToInt(profile.MistEmissionRate * profile.QualityMultiplier));
            SetRate(persistentSnow, Mathf.RoundToInt(profile.SnowEmissionRate * profile.QualityMultiplier));
            SetRate(movementTrail, 0);
        }

        private void UpdateMovementEmission(float speed)
        {
            int rate = speed > 0.15f ? Mathf.RoundToInt(profile.MovementTrailDensity * profile.QualityMultiplier * Mathf.Clamp(speed, 0.35f, 1.6f)) : 0;
            SetRate(movementTrail, rate);
            if (persistentMist != null)
            {
                SetRate(persistentMist, Mathf.RoundToInt(profile.MistEmissionRate * profile.QualityMultiplier * (1f + movementPulse * 0.5f)));
            }
        }

        private void StopPersistentParticles(bool clear)
        {
            ParticleSystemStopBehavior behavior = clear ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting;
            persistentMist?.Stop(true, behavior);
            persistentSnow?.Stop(true, behavior);
            movementTrail?.Stop(true, behavior);
            crackParticles?.Stop(true, behavior);
        }

        private static void SetRate(ParticleSystem system, int rate)
        {
            if (system == null)
            {
                return;
            }

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = Mathf.Max(0, rate);
        }

        private static void ConfigureBurst(ParticleSystem system, int count)
        {
            if (system == null)
            {
                return;
            }

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(count > 0 ? new[] { new ParticleSystem.Burst(0f, (short)count) } : System.Array.Empty<ParticleSystem.Burst>());
        }

        private void SetRenderer(Renderer renderer, Color tint, float opacity, float brightness, float dissolve, Vector4 scroll)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(TintId, tint);
            propertyBlock.SetFloat(OpacityId, Mathf.Clamp01(opacity));
            propertyBlock.SetFloat(BrightnessId, Mathf.Max(0f, brightness));
            propertyBlock.SetFloat(DissolveId, Mathf.Clamp01(dissolve));
            propertyBlock.SetVector(ScrollId, scroll);
            renderer.SetPropertyBlock(propertyBlock);
            propertyBlock.Clear();
            renderer.enabled = opacity > 0.001f;
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }
    }
}
