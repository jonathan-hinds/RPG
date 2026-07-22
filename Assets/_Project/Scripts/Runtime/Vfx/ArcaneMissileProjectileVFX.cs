using System.Collections;
using UnityEngine;

namespace RPGClone.Vfx.ArcaneMissiles
{
    [DisallowMultipleComponent]
    public sealed class ArcaneMissileProjectileVFX : MonoBehaviour, IMMOAbilityVfxPoolReset
    {
        [Header("Layered projectile")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Renderer whiteCore;
        [SerializeField] private Renderer blueBody;
        [SerializeField] private Renderer purpleFlares;
        [SerializeField] private Renderer paleShell;
        [SerializeField] private Transform runeRoot;
        [SerializeField] private Renderer runeRenderer;
        [SerializeField] private TrailRenderer coreTrail;
        [SerializeField] private TrailRenderer blueTrail;
        [SerializeField] private TrailRenderer firstPurpleTrail;
        [SerializeField] private TrailRenderer secondPurpleTrail;
        [SerializeField] private ParticleSystem fragments;
        [SerializeField] private ParticleSystem vapor;
        [SerializeField] private ParticleSystem motes;

        private MaterialPropertyBlock propertyBlock;
        private MaterialPropertyBlock PropertyBlock => propertyBlock ??= new MaterialPropertyBlock();
        private ArcaneMissilesVFXProfile profile;
        private Transform target;
        private MMOAbilityVfxDefinition definition;
        private Vector3 fallbackTarget;
        private Vector3 velocity;
        private Vector3 curveAxis;
        private float currentSpeed;
        private float startedAt;
        private float randomPhase;
        private int missileIndex;
        private bool finalMissile;
        private bool playing;
        private bool confirmed;
        private Coroutine releaseRoutine;

        public bool IsPlaying => playing;
        public int MissileIndex => missileIndex;

        private void Update()
        {
            if (!playing || profile == null)
            {
                return;
            }

            Vector3 targetPosition = ArcaneMissilesVFXUtility.SafeTargetPosition(target, fallbackTarget, definition);
            Vector3 toTarget = targetPosition - transform.position;
            float distance = toTarget.magnitude;
            float finalMultiplier = finalMissile ? profile.FinalScaleMultiplier : 1f;
            float pulse = 0.9f + Mathf.Sin((Time.time - startedAt) * 18f + missileIndex) * 0.1f;
            visualRoot.localScale = new Vector3(
                profile.ProjectileBodyThickness,
                profile.ProjectileBodyThickness,
                profile.ProjectileScale * finalMultiplier * pulse) * profile.OverallScale;

            float brightness = profile.OverallBrightness * (finalMissile ? profile.FinalBrightnessMultiplier : 1f);
            ArcaneMissilesVFXUtility.SetRenderer(whiteCore, PropertyBlock, 1f, profile.ProjectileCoreBrightness * brightness, 0f, Color.white);
            ArcaneMissilesVFXUtility.SetRenderer(blueBody, PropertyBlock, 0.96f, 1.7f * brightness, 0f, profile.BlueColor);
            ArcaneMissilesVFXUtility.SetRenderer(purpleFlares, PropertyBlock, 0.8f, 1.45f * brightness, 0f, profile.PurpleColor);
            ArcaneMissilesVFXUtility.SetRenderer(paleShell, PropertyBlock, 0.68f, 1.1f * brightness, 0f, new Color(0.65f, 0.88f, 1.2f, 1f));
            ArcaneMissilesVFXUtility.SetRenderer(runeRenderer, PropertyBlock, profile.ProjectileRuneVisibility, 2.2f * brightness, 0f, Color.white);

            if (runeRoot != null)
            {
                runeRoot.Rotate(Vector3.forward, profile.RuneSpeed * 2.2f * Time.deltaTime, Space.Self);
            }

            if (distance <= 0.08f)
            {
                transform.position = targetPosition;
                if (!confirmed)
                {
                    velocity = Vector3.zero;
                }
                return;
            }

            currentSpeed += profile.ProjectileAcceleration * Time.deltaTime;
            Vector3 desired = toTarget.normalized;
            float homing = 1f - Mathf.Exp(-profile.HomingStrength * Time.deltaTime);
            Vector3 currentDirection = velocity.sqrMagnitude > 0.001f ? velocity.normalized : desired;
            Vector3 direction = Vector3.Slerp(currentDirection, desired, homing).normalized;
            float life = Time.time - startedAt;
            float curve = Mathf.Sin(Mathf.Clamp01(life / Mathf.Max(0.1f, profile.MaximumLaunchLeadSeconds)) * Mathf.PI) * profile.CurveAmount;
            float spiral = Mathf.Sin(life * 18f + randomPhase) * profile.SpiralAmount;
            Vector3 offsetVelocity = curveAxis * (curve + spiral) * 4f;
            velocity = direction * currentSpeed + offsetVelocity;

            float step = velocity.magnitude * Time.deltaTime;
            if (!confirmed && distance <= Mathf.Max(0.11f, step * 1.35f))
            {
                transform.position = targetPosition - desired * 0.08f;
                velocity = Vector3.zero;
                return;
            }

            transform.position += velocity * Time.deltaTime;
            if (velocity.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(velocity.normalized, Vector3.up);
            }
        }

        public void Play(
            ArcaneMissilesVFXProfile newProfile,
            MMOAbilityVfxDefinition newDefinition,
            Vector3 sourcePosition,
            Transform newTarget,
            Vector3 newFallbackTarget,
            int newMissileIndex,
            bool isFinal)
        {
            ResetForPool();
            profile = newProfile;
            definition = newDefinition;
            target = newTarget;
            fallbackTarget = newFallbackTarget;
            missileIndex = newMissileIndex;
            finalMissile = isFinal;
            startedAt = Time.time;
            randomPhase = newMissileIndex * 1.731f;
            transform.position = sourcePosition;

            Vector3 initialDirection = (ArcaneMissilesVFXUtility.SafeTargetPosition(target, fallbackTarget, definition) - sourcePosition).normalized;
            if (initialDirection.sqrMagnitude < 0.001f) initialDirection = transform.forward;
            curveAxis = Vector3.Cross(initialDirection, Vector3.up).normalized * (newMissileIndex % 2 == 0 ? 1f : -1f);
            currentSpeed = profile.ProjectileSpeed;
            velocity = (initialDirection + curveAxis * profile.CurveAmount * 0.35f).normalized * currentSpeed;
            playing = true;
            confirmed = false;
            gameObject.SetActive(true);
            ConfigureTrails();
            ConfigureParticles();
        }

        public void ConfirmImpact(Vector3 impactPosition)
        {
            if (!playing || confirmed)
            {
                return;
            }

            confirmed = true;
            fallbackTarget = impactPosition;
            transform.position = impactPosition;
            if (visualRoot != null) visualRoot.gameObject.SetActive(false);
            StopEmission();
            if (releaseRoutine != null) StopCoroutine(releaseRoutine);
            releaseRoutine = StartCoroutine(ReleaseAfterTrails());
        }

        public void Dissolve(float duration)
        {
            if (!playing)
            {
                return;
            }

            if (releaseRoutine != null) StopCoroutine(releaseRoutine);
            releaseRoutine = StartCoroutine(DissolveRoutine(Mathf.Max(0.05f, duration)));
        }

        public void ResetForPool()
        {
            if (releaseRoutine != null)
            {
                StopCoroutine(releaseRoutine);
                releaseRoutine = null;
            }

            playing = false;
            confirmed = false;
            profile = null;
            target = null;
            definition = null;
            velocity = Vector3.zero;
            if (visualRoot != null) visualRoot.gameObject.SetActive(true);
            ClearTrails();
            fragments?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            vapor?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            motes?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public void ConfigureAuthoring(
            Transform newVisualRoot,
            Renderer newWhiteCore,
            Renderer newBlueBody,
            Renderer newPurpleFlares,
            Renderer newPaleShell,
            Transform newRuneRoot,
            Renderer newRuneRenderer,
            TrailRenderer newCoreTrail,
            TrailRenderer newBlueTrail,
            TrailRenderer newFirstPurpleTrail,
            TrailRenderer newSecondPurpleTrail,
            ParticleSystem newFragments,
            ParticleSystem newVapor,
            ParticleSystem newMotes)
        {
            visualRoot = newVisualRoot;
            whiteCore = newWhiteCore;
            blueBody = newBlueBody;
            purpleFlares = newPurpleFlares;
            paleShell = newPaleShell;
            runeRoot = newRuneRoot;
            runeRenderer = newRuneRenderer;
            coreTrail = newCoreTrail;
            blueTrail = newBlueTrail;
            firstPurpleTrail = newFirstPurpleTrail;
            secondPurpleTrail = newSecondPurpleTrail;
            fragments = newFragments;
            vapor = newVapor;
            motes = newMotes;
        }

        private void ConfigureTrails()
        {
            float multiplier = finalMissile ? profile.FinalTrailMultiplier : 1f;
            ConfigureTrail(coreTrail, profile.TrailCoreWidth * multiplier, profile.TrailLifetime * multiplier);
            ConfigureTrail(blueTrail, profile.TrailBlueRibbonWidth * multiplier, profile.TrailLifetime * multiplier);
            ConfigureTrail(firstPurpleTrail, profile.TrailBlueRibbonWidth * 0.56f * multiplier, profile.TrailLifetime * 1.08f * multiplier);
            ConfigureTrail(secondPurpleTrail, profile.TrailBlueRibbonWidth * 0.44f * multiplier, profile.TrailLifetime * 0.92f * multiplier);
            if (secondPurpleTrail != null) secondPurpleTrail.enabled = profile.PurpleRibbonCount > 1 || finalMissile;
        }

        private static void ConfigureTrail(TrailRenderer trail, float width, float lifetime)
        {
            if (trail == null) return;
            trail.widthMultiplier = width;
            trail.time = lifetime;
            trail.Clear();
            trail.emitting = true;
        }

        private void ConfigureParticles()
        {
            int finalBonus = finalMissile ? 5 : 0;
            ConfigureContinuous(fragments, profile.TrailFragmentAmount + finalBonus, profile.PurpleColor);
            ConfigureContinuous(vapor, profile.TrailVaporAmount + finalBonus, new Color(profile.PurpleColor.r, profile.PurpleColor.g, profile.PurpleColor.b, 0.38f));
            ConfigureContinuous(motes, Mathf.Max(3, profile.TrailFragmentAmount / 2) + finalBonus, profile.BlueColor);
        }

        private static void ConfigureContinuous(ParticleSystem particles, int rate, Color color)
        {
            if (particles == null) return;
            ParticleSystem.MainModule main = particles.main;
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = Mathf.Max(0, rate);
            particles.Play(true);
        }

        private IEnumerator DissolveRoutine(float duration)
        {
            float started = Time.time;
            while (Time.time - started < duration)
            {
                float progress = (Time.time - started) / duration;
                ArcaneMissilesVFXUtility.SetRenderer(whiteCore, PropertyBlock, 1f - progress, 2f, progress, Color.white);
                ArcaneMissilesVFXUtility.SetRenderer(blueBody, PropertyBlock, 1f - progress, 1.3f, progress, profile != null ? profile.BlueColor : Color.cyan);
                ArcaneMissilesVFXUtility.SetRenderer(purpleFlares, PropertyBlock, 1f - progress, 1.2f, progress, profile != null ? profile.PurpleColor : Color.magenta);
                yield return null;
            }

            StopEmission();
            yield return new WaitForSeconds(0.08f);
            Release();
        }

        private IEnumerator ReleaseAfterTrails()
        {
            float wait = profile != null ? profile.TrailLifetime * (finalMissile ? profile.FinalTrailMultiplier : 1f) : 0.5f;
            yield return new WaitForSeconds(wait + 0.05f);
            Release();
        }

        private void StopEmission()
        {
            if (coreTrail != null) coreTrail.emitting = false;
            if (blueTrail != null) blueTrail.emitting = false;
            if (firstPurpleTrail != null) firstPurpleTrail.emitting = false;
            if (secondPurpleTrail != null) secondPurpleTrail.emitting = false;
            fragments?.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            vapor?.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            motes?.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private void ClearTrails()
        {
            foreach (TrailRenderer trail in new[] { coreTrail, blueTrail, firstPurpleTrail, secondPurpleTrail })
            {
                if (trail == null) continue;
                trail.emitting = false;
                trail.Clear();
            }
        }

        private void Release()
        {
            releaseRoutine = null;
            playing = false;
            MMOAbilityVfxPool.Release(gameObject);
        }
    }
}
