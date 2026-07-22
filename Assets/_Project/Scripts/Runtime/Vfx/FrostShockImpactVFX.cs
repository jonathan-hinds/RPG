using UnityEngine;

namespace RPGClone.Vfx.Shaman
{
    public sealed class FrostShockImpactVFX : MonoBehaviour, IMMOAbilityVfxInstance
    {
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
        private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");

        [SerializeField] private FrostShockVFXProfile profile;
        [SerializeField] private Transform impactRoot;
        [SerializeField] private Transform groundRoot;
        [SerializeField] private Renderer contactFlash;
        [SerializeField] private Renderer freezeShell;
        [SerializeField] private Renderer[] explosionLayers;
        [SerializeField] private Renderer[] shockRings;
        [SerializeField] private Renderer groundPatch;
        [SerializeField] private Renderer[] radialShards;
        [SerializeField] private Renderer[] frostCracks;
        [SerializeField] private ParticleSystem secondaryFragments;
        [SerializeField] private ParticleSystem impactMist;
        [SerializeField] private ParticleSystem snowBurst;

        private MaterialPropertyBlock propertyBlock;
        private float startedAt;
        private bool playing;

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        public void Initialize(MMOAbilityVfxContext context)
        {
            if (profile == null)
            {
                return;
            }

            startedAt = Time.time;
            playing = true;
            impactRoot.localScale = Vector3.one * profile.OverallScale;
            if (groundRoot != null)
            {
                Vector3 groundPosition = context.Target != null ? context.Target.position + Vector3.up * 0.035f : context.TargetPosition;
                groundRoot.SetParent(null, true);
                groundRoot.position = groundPosition;
                groundRoot.rotation = Quaternion.identity;
                Destroy(groundRoot.gameObject, 1.35f);
            }

            ConfigureBurst(secondaryFragments, Mathf.RoundToInt(profile.SecondaryFragmentCount * profile.QualityMultiplier));
            ConfigureBurst(impactMist, Mathf.RoundToInt(profile.ImpactMistAmount * profile.QualityMultiplier));
            ConfigureBurst(snowBurst, Mathf.RoundToInt(profile.SnowBurstAmount * profile.QualityMultiplier));
            secondaryFragments?.Play(true);
            impactMist?.Play(true);
            snowBurst?.Play(true);
            Animate(0f);
        }

        private void LateUpdate()
        {
            if (!playing || profile == null)
            {
                return;
            }

            float elapsed = Time.time - startedAt;
            Animate(elapsed);
            if (elapsed >= 0.82f)
            {
                playing = false;
                if (impactRoot != null)
                {
                    impactRoot.gameObject.SetActive(false);
                }
            }
        }

        public void ConfigureAuthoring(
            FrostShockVFXProfile newProfile,
            Transform newImpactRoot,
            Transform newGroundRoot,
            Renderer newContactFlash,
            Renderer newFreezeShell,
            Renderer[] newExplosionLayers,
            Renderer[] newShockRings,
            Renderer newGroundPatch,
            Renderer[] newRadialShards,
            Renderer[] newFrostCracks,
            ParticleSystem newSecondaryFragments,
            ParticleSystem newImpactMist,
            ParticleSystem newSnowBurst)
        {
            profile = newProfile;
            impactRoot = newImpactRoot;
            groundRoot = newGroundRoot;
            contactFlash = newContactFlash;
            freezeShell = newFreezeShell;
            explosionLayers = newExplosionLayers;
            shockRings = newShockRings;
            groundPatch = newGroundPatch;
            radialShards = newRadialShards;
            frostCracks = newFrostCracks;
            secondaryFragments = newSecondaryFragments;
            impactMist = newImpactMist;
            snowBurst = newSnowBurst;
        }

        private void Animate(float elapsed)
        {
            float flash = Pulse(elapsed, 0f, 0.025f, 0.19f);
            contactFlash.transform.localScale = Vector3.one * profile.ContactFlashScale * Mathf.Lerp(0.22f, 1.18f, Mathf.Clamp01(elapsed / 0.13f));
            SetRenderer(contactFlash, profile.WhiteHotColor, flash, profile.ContactFlashBrightness, 0f);

            float freeze = Pulse(elapsed, 0.018f, 0.08f, 0.48f);
            freezeShell.transform.localScale = new Vector3(1.05f, 1.45f, 1.05f) * Mathf.Lerp(0.72f, 1.08f, Mathf.Clamp01(elapsed / 0.22f));
            SetRenderer(freezeShell, profile.PaleCyanColor, freeze * 0.58f, profile.OverallBrightness * 1.6f, Mathf.Clamp01(elapsed / 0.52f));

            for (int i = 0; i < explosionLayers.Length; i++)
            {
                float delay = i * 0.025f;
                float value = Pulse(elapsed, delay, delay + 0.06f, 0.48f + i * 0.08f);
                float progress = Mathf.Clamp01((elapsed - delay) / Mathf.Max(0.1f, 0.48f + i * 0.08f));
                explosionLayers[i].transform.localScale = Vector3.one * profile.FrostExplosionScale * Mathf.Lerp(0.35f, 1.25f + i * 0.12f, Smooth01(progress));
                explosionLayers[i].transform.localRotation = Quaternion.Euler(0f, elapsed * (i % 2 == 0 ? 74f : -58f), i * 19f);
                SetRenderer(explosionLayers[i], i == 0 ? profile.WhiteHotColor : profile.SaturatedBlueColor, value * (1f - i * 0.17f), profile.OverallBrightness * (2.4f - i * 0.35f), progress * 0.72f);
            }

            for (int i = 0; i < shockRings.Length; i++)
            {
                float delay = 0.035f + i * 0.065f;
                float progress = Mathf.Clamp01((elapsed - delay) / 0.42f);
                float alpha = elapsed >= delay ? 1f - Smooth01(progress) : 0f;
                shockRings[i].transform.localScale = Vector3.one * Mathf.Lerp(0.32f, profile.ShockRingSize * (1f + i * 0.12f), Smooth01(progress));
                shockRings[i].transform.localRotation = Quaternion.Euler(90f, elapsed * (i == 0 ? 115f : -82f), 0f);
                SetRenderer(shockRings[i], i == 0 ? profile.PaleCyanColor : profile.SaturatedBlueColor, alpha * 0.78f, profile.OverallBrightness * 1.7f, progress * 0.82f);
            }

            for (int i = 0; i < radialShards.Length; i++)
            {
                float delay = i * 0.008f;
                float progress = Mathf.Clamp01((elapsed - delay) / 0.58f);
                float alpha = elapsed >= delay ? 1f - Smooth01(progress) : 0f;
                Transform shard = radialShards[i].transform;
                shard.localPosition = shard.localRotation * Vector3.forward * Mathf.Lerp(0.08f, 1.2f, Smooth01(progress));
                shard.localScale = new Vector3(profile.MainShardSize * 0.34f, profile.MainShardSize * 0.34f, profile.MainShardSize * Mathf.Lerp(0.25f, 1.1f, progress));
                SetRenderer(radialShards[i], i % 3 == 0 ? profile.WhiteHotColor : profile.SaturatedBlueColor, alpha, profile.OverallBrightness * 1.45f, progress * 0.7f);
            }

            for (int i = 0; i < frostCracks.Length; i++)
            {
                float delay = 0.04f + i * 0.035f;
                float alpha = Pulse(elapsed, delay, delay + 0.05f, delay + 0.36f);
                SetRenderer(frostCracks[i], profile.PaleCyanColor, alpha * 0.82f, profile.OverallBrightness * 2.1f, Mathf.Clamp01((elapsed - delay) / 0.4f));
            }

            if (groundPatch != null)
            {
                float groundProgress = Mathf.Clamp01(elapsed / 0.32f);
                groundPatch.transform.localScale = Vector3.one * profile.GroundFrostRadius * Mathf.Lerp(0.15f, 1f, Smooth01(groundProgress));
                SetRenderer(groundPatch, profile.PaleCyanColor, (1f - Mathf.Clamp01((elapsed - 0.55f) / 0.6f)) * 0.72f, profile.OverallBrightness, Mathf.Clamp01((elapsed - 0.5f) / 0.7f));
            }
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

        private void SetRenderer(Renderer renderer, Color tint, float opacity, float brightness, float dissolve)
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
            renderer.SetPropertyBlock(propertyBlock);
            propertyBlock.Clear();
            renderer.enabled = opacity > 0.001f;
        }

        private static float Pulse(float value, float start, float peak, float end)
        {
            if (value < start || value >= end)
            {
                return 0f;
            }

            return value <= peak ? Smooth01(Mathf.InverseLerp(start, peak, value)) : 1f - Smooth01(Mathf.InverseLerp(peak, end, value));
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }
    }
}
