using System.Collections;
using UnityEngine;

namespace RPGClone.Vfx.ArcaneMissiles
{
    [DisallowMultipleComponent]
    public sealed class ArcaneMissilesInterruptVFX : MonoBehaviour, IMMOAbilityVfxPoolReset
    {
        [SerializeField] private Transform collapseCore;
        [SerializeField] private Renderer collapseCoreRenderer;
        [SerializeField] private Transform snapRing;
        [SerializeField] private Renderer snapRingRenderer;
        [SerializeField] private ParticleSystem runeFragments;
        [SerializeField] private ParticleSystem dimSparks;
        [SerializeField] private ParticleSystem connectionSnaps;

        private MaterialPropertyBlock propertyBlock;
        private MaterialPropertyBlock PropertyBlock => propertyBlock ??= new MaterialPropertyBlock();
        private ArcaneMissilesVFXProfile profile;
        private Coroutine routine;

        public void Play(ArcaneMissilesVFXProfile newProfile, Vector3 center)
        {
            ResetForPool();
            profile = newProfile;
            transform.position = center;
            gameObject.SetActive(true);
            ArcaneMissilesVFXUtility.ConfigureBurst(runeFragments, profile.InterruptRuneFragments, profile.PurpleColor, 1.15f);
            ArcaneMissilesVFXUtility.ConfigureBurst(dimSparks, Mathf.Max(8, profile.InterruptRuneFragments / 2), profile.BlueColor * 0.65f, 0.75f);
            ArcaneMissilesVFXUtility.ConfigureBurst(connectionSnaps, Mathf.Max(6, profile.InterruptRuneFragments / 3), profile.MagentaAccent, profile.ConnectionSnapIntensity);
            runeFragments?.Play(true);
            dimSparks?.Play(true);
            connectionSnaps?.Play(true);
            routine = StartCoroutine(AnimateCollapse());
        }

        public void ResetForPool()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }

            profile = null;
            runeFragments?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            dimSparks?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            connectionSnaps?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (collapseCore != null) collapseCore.localScale = Vector3.zero;
            if (snapRing != null) snapRing.localScale = Vector3.zero;
        }

        public void ConfigureAuthoring(
            Transform newCollapseCore,
            Renderer newCollapseCoreRenderer,
            Transform newSnapRing,
            Renderer newSnapRingRenderer,
            ParticleSystem newRuneFragments,
            ParticleSystem newDimSparks,
            ParticleSystem newConnectionSnaps)
        {
            collapseCore = newCollapseCore;
            collapseCoreRenderer = newCollapseCoreRenderer;
            snapRing = newSnapRing;
            snapRingRenderer = newSnapRingRenderer;
            runeFragments = newRuneFragments;
            dimSparks = newDimSparks;
            connectionSnaps = newConnectionSnaps;
        }

        private IEnumerator AnimateCollapse()
        {
            float duration = profile.InterruptCollapseDuration;
            float started = Time.time;
            while (Time.time - started < duration)
            {
                float progress = Mathf.Clamp01((Time.time - started) / duration);
                float inverse = 1f - ArcaneMissilesVFXUtility.Smooth01(progress);
                collapseCore.localScale = Vector3.one * profile.CentralCoreSize * profile.OverallScale * inverse;
                snapRing.localScale = Vector3.one * Mathf.Lerp(0.2f, 1.6f, progress) * profile.ConnectionSnapIntensity;
                ArcaneMissilesVFXUtility.SetRenderer(collapseCoreRenderer, PropertyBlock, inverse, 3.5f * profile.OverallBrightness, progress, Color.white);
                ArcaneMissilesVFXUtility.SetRenderer(snapRingRenderer, PropertyBlock, inverse, 1.8f * profile.OverallBrightness, progress, profile.MagentaAccent);
                yield return null;
            }

            yield return new WaitForSeconds(0.25f);
            routine = null;
            MMOAbilityVfxPool.Release(gameObject);
        }
    }
}
