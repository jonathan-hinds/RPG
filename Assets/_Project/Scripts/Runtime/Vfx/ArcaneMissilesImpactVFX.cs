using System.Collections;
using UnityEngine;

namespace RPGClone.Vfx.ArcaneMissiles
{
    [DisallowMultipleComponent]
    public sealed class ArcaneMissilesImpactVFX : MonoBehaviour, IMMOAbilityVfxPoolReset
    {
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Renderer contactFlash;
        [SerializeField] private Renderer explosion;
        [SerializeField] private Transform firstShockRing;
        [SerializeField] private Renderer firstShockRingRenderer;
        [SerializeField] private Transform secondShockRing;
        [SerializeField] private Renderer secondShockRingRenderer;
        [SerializeField] private Transform targetWrap;
        [SerializeField] private Renderer targetWrapRenderer;
        [SerializeField] private ParticleSystem spikes;
        [SerializeField] private ParticleSystem runeFragments;
        [SerializeField] private ParticleSystem sparks;

        private MaterialPropertyBlock propertyBlock;
        private MaterialPropertyBlock PropertyBlock => propertyBlock ??= new MaterialPropertyBlock();
        private ArcaneMissilesVFXProfile profile;
        private Coroutine playRoutine;
        private bool finalImpact;
        private int variation;

        public void Play(ArcaneMissilesVFXProfile newProfile, bool isFinal, int newVariation)
        {
            ResetForPool();
            profile = newProfile;
            finalImpact = isFinal;
            variation = newVariation;
            gameObject.SetActive(true);
            ConfigureParticles();
            spikes?.Play(true);
            runeFragments?.Play(true);
            sparks?.Play(true);
            playRoutine = StartCoroutine(AnimateImpact());
        }

        public void ResetForPool()
        {
            if (playRoutine != null)
            {
                StopCoroutine(playRoutine);
                playRoutine = null;
            }

            profile = null;
            spikes?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            runeFragments?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            sparks?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (visualRoot != null) visualRoot.localScale = Vector3.zero;
        }

        public void ConfigureAuthoring(
            Transform newVisualRoot,
            Renderer newContactFlash,
            Renderer newExplosion,
            Transform newFirstShockRing,
            Renderer newFirstShockRingRenderer,
            Transform newSecondShockRing,
            Renderer newSecondShockRingRenderer,
            Transform newTargetWrap,
            Renderer newTargetWrapRenderer,
            ParticleSystem newSpikes,
            ParticleSystem newRuneFragments,
            ParticleSystem newSparks)
        {
            visualRoot = newVisualRoot;
            contactFlash = newContactFlash;
            explosion = newExplosion;
            firstShockRing = newFirstShockRing;
            firstShockRingRenderer = newFirstShockRingRenderer;
            secondShockRing = newSecondShockRing;
            secondShockRingRenderer = newSecondShockRingRenderer;
            targetWrap = newTargetWrap;
            targetWrapRenderer = newTargetWrapRenderer;
            spikes = newSpikes;
            runeFragments = newRuneFragments;
            sparks = newSparks;
        }

        private IEnumerator AnimateImpact()
        {
            float duration = profile.ImpactDuration;
            float finalMultiplier = finalImpact ? profile.FinalImpactMultiplier : 1f;
            float started = Time.time;
            Quaternion variationRotation = Quaternion.Euler(0f, 0f, variation * 47f);
            while (Time.time - started < duration)
            {
                float progress = Mathf.Clamp01((Time.time - started) / duration);
                float eased = ArcaneMissilesVFXUtility.Smooth01(progress);
                float fade = 1f - eased;
                float flashEnvelope = 1f - Mathf.Clamp01(progress / 0.34f);
                float explosionEnvelope = Mathf.Sin(progress * Mathf.PI);
                float baseScale = profile.ImpactExplosionScale * finalMultiplier * profile.OverallScale;
                visualRoot.localScale = Vector3.one * baseScale;
                ArcaneMissilesVFXUtility.SetRenderer(contactFlash, PropertyBlock, flashEnvelope, 5f * profile.OverallBrightness, progress, Color.white);
                ArcaneMissilesVFXUtility.SetRenderer(explosion, PropertyBlock, explosionEnvelope, 2.1f * profile.OverallBrightness, progress * 0.72f, variation % 2 == 0 ? profile.BlueColor : profile.PurpleColor);
                ArcaneMissilesVFXUtility.SetRenderer(firstShockRingRenderer, PropertyBlock, fade, 1.8f * profile.OverallBrightness, progress, profile.BlueColor);
                ArcaneMissilesVFXUtility.SetRenderer(secondShockRingRenderer, PropertyBlock, fade * (finalImpact || profile.ShockRingCount > 1 ? 1f : 0f), 1.6f * profile.OverallBrightness, progress, profile.PurpleColor);
                ArcaneMissilesVFXUtility.SetRenderer(targetWrapRenderer, PropertyBlock, fade * profile.TargetWrapIntensity, 1.3f * profile.OverallBrightness, progress, profile.MagentaAccent);

                if (firstShockRing != null) firstShockRing.localScale = Vector3.one * Mathf.Lerp(0.2f, 1.55f, eased);
                if (secondShockRing != null) secondShockRing.localScale = Vector3.one * Mathf.Lerp(0.08f, 2f, eased);
                if (targetWrap != null)
                {
                    targetWrap.localScale = new Vector3(1.1f + eased * 0.35f, 1.45f, 1.1f + eased * 0.35f);
                    targetWrap.localRotation = variationRotation * Quaternion.Euler(0f, progress * 150f, 0f);
                }

                yield return null;
            }

            yield return new WaitForSeconds(0.08f);
            playRoutine = null;
            MMOAbilityVfxPool.Release(gameObject);
        }

        private void ConfigureParticles()
        {
            float size = finalImpact ? profile.FinalImpactMultiplier : 1f;
            int finalBonus = finalImpact ? Mathf.RoundToInt(profile.ImpactSparkAmount * 0.4f) : 0;
            ArcaneMissilesVFXUtility.ConfigureBurst(spikes, profile.SpikeCount + (finalImpact ? 3 : 0), profile.BlueColor, size);
            ArcaneMissilesVFXUtility.ConfigureBurst(runeFragments, Mathf.Max(6, profile.ImpactSparkAmount / 2) + finalBonus, profile.PurpleColor, size);
            ArcaneMissilesVFXUtility.ConfigureBurst(sparks, profile.ImpactSparkAmount + finalBonus, Color.white, size);
        }
    }
}
