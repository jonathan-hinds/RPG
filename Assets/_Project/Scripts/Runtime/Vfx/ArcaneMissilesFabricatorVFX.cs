using UnityEngine;

namespace RPGClone.Vfx.ArcaneMissiles
{
    [DisallowMultipleComponent]
    public sealed class ArcaneMissilesFabricatorVFX : MonoBehaviour
    {
        [Header("Layered orb")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Renderer whiteCore;
        [SerializeField] private Renderer blueBody;
        [SerializeField] private Renderer purpleEnergy;
        [SerializeField] private Renderer paleShell;
        [SerializeField] private Transform runeRoot;
        [SerializeField] private Renderer internalRune;
        [SerializeField] private Transform firstRing;
        [SerializeField] private Renderer firstRingRenderer;
        [SerializeField] private Transform secondRing;
        [SerializeField] private Renderer secondRingRenderer;
        [SerializeField] private ParticleSystem fragments;
        [SerializeField] private ParticleSystem sparks;
        [SerializeField] private ParticleSystem recoil;

        private MaterialPropertyBlock propertyBlock;
        private MaterialPropertyBlock PropertyBlock => propertyBlock ??= new MaterialPropertyBlock();
        private ArcaneMissilesVFXProfile profile;
        private float formation;
        private float fabrication = 1f;
        private float fade = 1f;
        private float launchPulse;
        private float collapse;
        private int index;

        public Vector3 LaunchPosition => whiteCore != null ? whiteCore.bounds.center : transform.position;
        public bool IsFormed => formation >= 0.99f;

        private void Update()
        {
            if (profile == null || visualRoot == null)
            {
                return;
            }

            float pulse = launchPulse > 0f ? Mathf.Sin(Mathf.Clamp01(launchPulse / 0.18f) * Mathf.PI) : 0f;
            launchPulse = Mathf.Max(0f, launchPulse - Time.deltaTime);
            float formed = ArcaneMissilesVFXUtility.Smooth01(formation);
            float collapseScale = 1f - ArcaneMissilesVFXUtility.Smooth01(collapse);
            float hollow = Mathf.Clamp01(fabrication);
            float scale = profile.OrbScale * profile.OverallScale * formed * collapseScale * (1f + pulse * 0.18f);
            visualRoot.localScale = Vector3.one * scale;

            float brightness = profile.OverallBrightness * (1f + pulse * 1.6f);
            float visible = fade * formed * collapseScale;
            ArcaneMissilesVFXUtility.SetRenderer(whiteCore, PropertyBlock, visible * Mathf.Lerp(0.2f, 1f, hollow), brightness * 4.2f, collapse, Color.white);
            ArcaneMissilesVFXUtility.SetRenderer(blueBody, PropertyBlock, visible * Mathf.Lerp(0.35f, 1f, hollow), brightness * 1.45f, collapse, profile.BlueColor);
            ArcaneMissilesVFXUtility.SetRenderer(purpleEnergy, PropertyBlock, visible * Mathf.Lerp(0.5f, 1f, hollow), brightness * 1.25f, collapse, profile.PurpleColor);
            ArcaneMissilesVFXUtility.SetRenderer(paleShell, PropertyBlock, visible * 0.74f, brightness, Mathf.Clamp01(collapse + (1f - hollow) * 0.16f), new Color(0.64f, 0.88f, 1.25f, 1f));
            ArcaneMissilesVFXUtility.SetRenderer(internalRune, PropertyBlock, visible * Mathf.Lerp(0.12f, 1f, hollow), brightness * Mathf.Lerp(0.45f, 2.2f, hollow), collapse, Color.white);
            ArcaneMissilesVFXUtility.SetRenderer(firstRingRenderer, PropertyBlock, visible * 0.82f, brightness * (1f + pulse), collapse, profile.BlueColor);
            ArcaneMissilesVFXUtility.SetRenderer(secondRingRenderer, PropertyBlock, visible * 0.66f * (profile.RingCount > 1 ? 1f : 0f), brightness, collapse, profile.PurpleColor);

            float direction = index % 2 == 0 ? 1f : -1f;
            if (runeRoot != null) runeRoot.Rotate(Vector3.forward, profile.RuneSpeed * direction * Time.deltaTime, Space.Self);
            if (firstRing != null) firstRing.Rotate(Vector3.forward, profile.RingSpeed.x * direction * Time.deltaTime, Space.Self);
            if (secondRing != null) secondRing.Rotate(Vector3.forward, profile.RingSpeed.y * direction * Time.deltaTime, Space.Self);
        }

        public void Play(ArcaneMissilesVFXProfile newProfile, int newIndex)
        {
            profile = newProfile;
            index = newIndex;
            formation = 0f;
            fabrication = 1f;
            fade = 1f;
            launchPulse = 0f;
            collapse = 0f;
            gameObject.SetActive(true);
            StopParticles();
            ConfigureParticles();
        }

        public void SetFormationProgress(float progress)
        {
            float previous = formation;
            formation = Mathf.Clamp01(progress);
            if (previous < 0.02f && formation >= 0.02f)
            {
                fragments?.Play(true);
                sparks?.Play(true);
            }
        }

        public void SetFabricationProgress(float progress)
        {
            fabrication = Mathf.Clamp01(progress);
        }

        public void SetHighlighted(bool highlighted)
        {
            if (highlighted)
            {
                fabrication = Mathf.Max(fabrication, 0.72f);
            }
        }

        public void FaceTarget(Vector3 targetPosition)
        {
            if (runeRoot == null)
            {
                return;
            }

            Vector3 direction = targetPosition - runeRoot.position;
            if (direction.sqrMagnitude > 0.001f)
            {
                runeRoot.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
        }

        public void PulseLaunch(bool finalMissile)
        {
            launchPulse = finalMissile ? 0.24f : 0.18f;
            fabrication = 0.05f;
            if (recoil != null)
            {
                ParticleSystem.MainModule main = recoil.main;
                main.startSizeMultiplier = finalMissile ? 1.25f : 1f;
                recoil.Play(true);
            }
        }

        public void SetFade(float value)
        {
            fade = Mathf.Clamp01(value);
        }

        public void SetCollapse(float value)
        {
            float previous = collapse;
            collapse = Mathf.Clamp01(value);
            if (previous < 0.05f && collapse >= 0.05f)
            {
                fragments?.Play(true);
                sparks?.Play(true);
            }
        }

        public void StopImmediate()
        {
            StopParticles();
            profile = null;
            gameObject.SetActive(false);
        }

        public void ConfigureAuthoring(
            Transform newVisualRoot,
            Renderer newWhiteCore,
            Renderer newBlueBody,
            Renderer newPurpleEnergy,
            Renderer newPaleShell,
            Transform newRuneRoot,
            Renderer newInternalRune,
            Transform newFirstRing,
            Renderer newFirstRingRenderer,
            Transform newSecondRing,
            Renderer newSecondRingRenderer,
            ParticleSystem newFragments,
            ParticleSystem newSparks,
            ParticleSystem newRecoil)
        {
            visualRoot = newVisualRoot;
            whiteCore = newWhiteCore;
            blueBody = newBlueBody;
            purpleEnergy = newPurpleEnergy;
            paleShell = newPaleShell;
            runeRoot = newRuneRoot;
            internalRune = newInternalRune;
            firstRing = newFirstRing;
            firstRingRenderer = newFirstRingRenderer;
            secondRing = newSecondRing;
            secondRingRenderer = newSecondRingRenderer;
            fragments = newFragments;
            sparks = newSparks;
            recoil = newRecoil;
        }

        private void ConfigureParticles()
        {
            if (profile == null) return;
            ArcaneMissilesVFXUtility.ConfigureBurst(fragments, profile.FragmentCount, profile.PurpleColor, 1f);
            ArcaneMissilesVFXUtility.ConfigureBurst(sparks, Mathf.Max(4, profile.FragmentCount / 2), profile.BlueColor, 0.65f);
            ArcaneMissilesVFXUtility.ConfigureBurst(recoil, Mathf.Max(4, profile.FragmentCount / 2), profile.MagentaAccent, 0.85f);
        }

        private void StopParticles()
        {
            fragments?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            sparks?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            recoil?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
