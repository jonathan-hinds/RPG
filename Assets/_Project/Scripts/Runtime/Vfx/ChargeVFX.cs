using System;
using RPGClone.Abilities;
using RPGClone.Characters;
using UnityEngine;

namespace RPGClone.Vfx.Warrior
{
    [DisallowMultipleComponent]
    public sealed class ChargeVFX : MonoBehaviour, IChargeVFX, IMMOAbilityVfxInstance
    {
        [Header("Configuration")]
        [SerializeField] private ChargeVFXProfile profile;

        [Header("World-Space Launch")]
        [SerializeField] private ParticleSystem launchHeavyDust;
        [SerializeField] private ParticleSystem launchFineDust;
        [SerializeField] private ParticleSystem launchDirt;
        [SerializeField] private ParticleSystem launchRocks;
        [SerializeField] private ParticleSystem launchShockwave;

        [Header("World-Space Trail")]
        [SerializeField] private ParticleSystem trailHeavyDust;
        [SerializeField] private ParticleSystem trailFineDust;
        [SerializeField] private ParticleSystem trailDirt;
        [SerializeField] private ParticleSystem scrapeDust;
        [SerializeField] private ParticleSystem scrapeDebris;

        [Header("Character-Attached Motion")]
        [SerializeField] private ParticleSystem speedStreaks;
        [SerializeField] private ParticleSystem airCompression;
        [SerializeField] private ParticleSystem armorGlints;

        [Header("World-Space Collision")]
        [SerializeField] private ParticleSystem contactFlash;
        [SerializeField] private ParticleSystem impactHeavyDust;
        [SerializeField] private ParticleSystem impactFineDust;
        [SerializeField] private ParticleSystem impactGroundBurst;
        [SerializeField] private ParticleSystem impactShards;
        [SerializeField] private ParticleSystem impactDirt;
        [SerializeField] private ParticleSystem impactRocks;
        [SerializeField] private ParticleSystem impactShockwave;
        [SerializeField] private ParticleSystem recoveryDust;

        private ParticleSystem[] allParticles = Array.Empty<ParticleSystem>();
        private MMOAbilityVfxContext context;
        private MMOAbilitySystem sourceSystem;
        private MMOAbilityDefinition ability;
        private Transform source;
        private Transform target;
        private float startedAt;
        private float dustAccumulator;
        private float dirtAccumulator;
        private float scrapeAccumulator;
        private float lastTrailTime;
        private Vector3 lastTrailPosition;
        private bool initialized;
        private bool impactPlayed;
        private bool isPlaying;
        private bool isRecovering;

        public bool IsPlaying => isPlaying;
        public bool IsRecovering => isRecovering;
        public ChargeVFXProfile Profile => profile;

        private void Awake()
        {
            CacheParticles();
            StopAll(false);
        }

        private void OnDisable()
        {
            Unsubscribe();
            StopAll(false);
        }

        private void Update()
        {
            if (!isPlaying || profile == null)
            {
                return;
            }

            if (isRecovering)
            {
                return;
            }

            if (source == null || Time.time - startedAt >= profile.MaximumTravelDuration)
            {
                BeginRecovery();
                return;
            }

            DepositWorldTrail();
        }

        public void Initialize(MMOAbilityVfxContext newContext)
        {
            if (profile == null)
            {
                Debug.LogError($"{nameof(ChargeVFX)} on '{name}' requires a profile.", this);
                Destroy(gameObject);
                return;
            }

            context = newContext;
            sourceSystem = context.SourceSystem;
            ability = context.Ability;
            source = context.Source;
            target = context.Target;
            initialized = true;
            Subscribe();
            Play();
        }

        public void StopImmediate()
        {
            Unsubscribe();
            StopAll(true);
            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
        }

        public void ConfigureAuthoring(
            ChargeVFXProfile newProfile,
            ParticleSystem[] launch,
            ParticleSystem[] trail,
            ParticleSystem[] attached,
            ParticleSystem[] impact)
        {
            if (launch == null || launch.Length != 5 || trail == null || trail.Length != 5
                || attached == null || attached.Length != 3 || impact == null || impact.Length != 9)
            {
                throw new ArgumentException("ChargeVFX authoring arrays must contain 5 launch, 5 trail, 3 attached, and 9 impact systems.");
            }

            profile = newProfile;
            launchHeavyDust = launch[0];
            launchFineDust = launch[1];
            launchDirt = launch[2];
            launchRocks = launch[3];
            launchShockwave = launch[4];
            trailHeavyDust = trail[0];
            trailFineDust = trail[1];
            trailDirt = trail[2];
            scrapeDust = trail[3];
            scrapeDebris = trail[4];
            speedStreaks = attached[0];
            airCompression = attached[1];
            armorGlints = attached[2];
            contactFlash = impact[0];
            impactHeavyDust = impact[1];
            impactFineDust = impact[2];
            impactGroundBurst = impact[3];
            impactShards = impact[4];
            impactDirt = impact[5];
            impactRocks = impact[6];
            impactShockwave = impact[7];
            recoveryDust = impact[8];
            CacheParticles();
        }

        private void Play()
        {
            StopAll(false);
            ApplyProfile();
            isPlaying = true;
            isRecovering = false;
            impactPlayed = false;
            startedAt = Time.time;
            lastTrailTime = Time.time;
            lastTrailPosition = GroundPosition(source != null ? source.position : transform.position);
            transform.localScale = Vector3.one * profile.OverallScale;

            EmitAt(launchHeavyDust, lastTrailPosition, profile.LaunchDustAmount);
            EmitAt(launchFineDust, lastTrailPosition, Mathf.Max(1, profile.LaunchDustAmount / 2));
            EmitAt(launchDirt, lastTrailPosition, profile.LaunchDirtAmount);
            EmitAt(launchRocks, lastTrailPosition, profile.LaunchRockAmount);
            EmitAt(launchShockwave, lastTrailPosition + Vector3.up * 0.035f, 2);

            // Seed the first trail event immediately. At Charge speeds, waiting even one
            // spacing interval makes the launch read as a stationary puff instead of a
            // violent transfer of momentum into the ground behind the Warrior.
            Vector3 momentumKick = lastTrailPosition - transform.forward * 0.32f;
            EmitAt(trailHeavyDust, momentumKick, 5);
            EmitAt(trailFineDust, momentumKick + Vector3.up * 0.1f, 4);
            EmitAt(trailDirt, momentumKick, 3);
            EmitAt(scrapeDust, momentumKick, 5);
            EmitAt(scrapeDebris, momentumKick, 2);

            PlayAttached(speedStreaks);
            PlayAttached(airCompression);
            PlayAttached(armorGlints);
            EmitAttached(speedStreaks, Mathf.Max(3, Mathf.RoundToInt(5f * profile.SpeedStreakIntensity)));
            EmitAttached(airCompression, 2);
            EmitAttached(armorGlints, 1);
        }

        private void ApplyProfile()
        {
            Color heavy = Brighten(profile.HeavyDustColor);
            Color fine = Brighten(profile.FineDustColor);
            Color dirt = Brighten(profile.DirtColor);
            Color rock = Brighten(profile.RockColor);
            Color ring = Brighten(profile.ShockwaveColor);
            Color air = Brighten(profile.CompressedAirColor);
            Color streak = Brighten(profile.SpeedStreakColor);
            Color impact = Brighten(profile.ImpactColor);
            Color metal = Brighten(profile.MetallicColor);

            Configure(launchHeavyDust, profile.HeavyDustLifetime, profile.LaunchDustSize, heavy, 1.5f, -0.25f);
            Configure(launchFineDust, profile.FineDustLifetime, profile.LaunchDustSize * 0.58f, fine, 1.05f, profile.DustRiseSpeed);
            Configure(launchDirt, 1.35f, profile.DirtChunkSize * 1.3f, dirt, 4.8f, 3.4f);
            Configure(launchRocks, 1.55f, profile.DirtChunkSize * 1.15f, rock, 4.2f, 3.8f);
            ConfigureShockwave(launchShockwave, profile.ShockwaveSize, profile.ShockwaveSpeed, profile.ShockwaveLifetime, ring);

            Configure(trailHeavyDust, profile.HeavyDustLifetime, profile.HeavyDustSize, heavy, profile.TrailSpreadSpeed, 0.08f);
            Configure(trailFineDust, profile.FineDustLifetime, profile.FineDustSize, fine, profile.TrailSpreadSpeed * 0.55f, profile.DustRiseSpeed);
            Configure(trailDirt, 1.25f, profile.DirtChunkSize, dirt, 3.2f, 2.8f);
            Configure(scrapeDust, profile.HeavyDustLifetime * 0.65f, profile.HeavyDustSize * 0.42f, heavy, 0.75f, 0.12f);
            Configure(scrapeDebris, 0.9f, profile.DirtChunkSize * 0.42f, dirt, 2.3f, 1.8f);

            Configure(speedStreaks, 0.22f, 0.14f * profile.SpeedStreakIntensity, streak, profile.SpeedStreakLength * 6f, 0f);
            ConfigureEmission(speedStreaks, 15f * profile.SpeedStreakIntensity);
            Configure(airCompression, 0.34f, 1.15f, air, 1.1f, 0f);
            ConfigureEmission(airCompression, profile.AirCompressionFrequency);
            Configure(armorGlints, 0.18f, 0.2f, metal, 0.2f, 0f);
            ConfigureEmission(armorGlints, profile.ArmorGlintFrequency);

            Configure(contactFlash, 0.14f, profile.CollisionBurstSize * 0.72f, impact, 0f, 0f);
            Configure(impactHeavyDust, profile.CollisionDustLifetime, profile.CollisionBurstSize, heavy, 2.4f, 0.5f);
            Configure(impactFineDust, profile.CollisionDustLifetime * 1.15f, profile.CollisionBurstSize * 0.58f, fine, 1.25f, profile.DustRiseSpeed);
            Configure(impactGroundBurst, 0.46f, profile.CollisionBurstSize, dirt, 2.4f, 0.35f);
            Configure(impactShards, 0.55f, profile.CollisionBurstSize * 0.76f, impact, 5f, 1.5f);
            Configure(impactDirt, 1.55f, profile.DirtChunkSize * 1.35f, dirt, 5.8f, 4.2f);
            Configure(impactRocks, 1.75f, profile.DirtChunkSize * 1.2f, rock, 5.1f, 4.6f);
            ConfigureShockwave(impactShockwave, profile.ShockwaveSize * 1.25f, profile.ShockwaveSpeed * 1.1f, profile.ShockwaveLifetime * 1.2f, ring);
            Configure(recoveryDust, profile.RecoveryDuration, profile.CollisionBurstSize * 0.62f, fine, 0.65f, profile.DustRiseSpeed * 0.5f);
        }

        private void DepositWorldTrail()
        {
            Vector3 position = GroundPosition(source.position);
            float delta = Mathf.Max(0.0001f, Time.time - lastTrailTime);
            dustAccumulator += delta * profile.DustEventSpawnRate;
            dirtAccumulator += delta * profile.DirtChunkFrequency;
            scrapeAccumulator += delta * profile.GroundScrapeFrequency;
            bool movedEnough = Vector3.SqrMagnitude(position - lastTrailPosition) >= profile.TrailSpacing * profile.TrailSpacing;

            if (dustAccumulator >= 1f && movedEnough)
            {
                int events = Mathf.Min(3, Mathf.FloorToInt(dustAccumulator));
                dustAccumulator -= events;
                EmitAt(trailHeavyDust, position, events * 2);
                EmitAt(trailFineDust, position + Vector3.up * 0.12f, events * 2);
                lastTrailPosition = position;
            }

            if (dirtAccumulator >= 1f && movedEnough)
            {
                int amount = Mathf.Min(3, Mathf.FloorToInt(dirtAccumulator));
                dirtAccumulator -= amount;
                EmitAt(trailDirt, position, amount);
            }

            if (scrapeAccumulator >= 1f && movedEnough)
            {
                int amount = Mathf.Min(4, Mathf.FloorToInt(scrapeAccumulator));
                scrapeAccumulator -= amount;
                EmitAt(scrapeDust, position, amount);
                EmitAt(scrapeDebris, position, Mathf.Max(1, amount / 2));
            }

            lastTrailTime = Time.time;
        }

        private void OnChargeImpactStarted(MMOAbilitySystem eventSource, MMOAbilityDefinition eventAbility, MMOCharacterIdentity eventTarget, float delay)
        {
            if (!Matches(eventSource, eventAbility))
            {
                return;
            }

            target = eventTarget != null ? eventTarget.transform : target;
            PlayImpact();
        }

        private void OnChargeCompleted(MMOAbilitySystem eventSource, MMOAbilityDefinition eventAbility, MMOCharacterIdentity eventTarget)
        {
            if (!Matches(eventSource, eventAbility))
            {
                return;
            }

            target = eventTarget != null ? eventTarget.transform : target;
            if (!impactPlayed)
            {
                PlayImpact();
            }

            BeginRecovery();
        }

        private bool Matches(MMOAbilitySystem eventSource, MMOAbilityDefinition eventAbility)
        {
            return initialized && eventSource == sourceSystem && eventAbility == ability;
        }

        private void PlayImpact()
        {
            if (impactPlayed || profile == null)
            {
                return;
            }

            impactPlayed = true;
            StopAttached();
            Vector3 point = ResolveImpactPoint();
            EmitAt(contactFlash, point + Vector3.up * 0.9f, 2);
            EmitAt(impactHeavyDust, point, profile.CollisionDustAmount);
            EmitAt(impactFineDust, point + Vector3.up * 0.18f, Mathf.Max(1, profile.CollisionDustAmount / 2));
            EmitAt(impactGroundBurst, point + Vector3.up * 0.06f, Mathf.Max(4, profile.CollisionShardAmount / 2));
            EmitAt(impactShards, point + Vector3.up * 0.06f, profile.CollisionShardAmount);
            EmitAt(impactDirt, point, profile.CollisionDebrisAmount);
            EmitAt(impactRocks, point, Mathf.Max(1, profile.CollisionDebrisAmount / 3));
            EmitAt(impactShockwave, point + Vector3.up * 0.04f, 2);
            EmitAt(recoveryDust, point + Vector3.up * 0.1f, profile.RecoveryDustAmount);
        }

        private void BeginRecovery()
        {
            if (!isPlaying || isRecovering)
            {
                return;
            }

            isRecovering = true;
            StopAttached();
            Unsubscribe();
            Invoke(nameof(Complete), profile != null ? profile.RecoveryDuration : 0.5f);
        }

        private void Complete()
        {
            isPlaying = false;
            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
        }

        private Vector3 ResolveImpactPoint()
        {
            Vector3 sourcePosition = source != null ? source.position : transform.position;
            if (target == null)
            {
                return GroundPosition(sourcePosition);
            }

            Vector3 targetPosition = target.position;
            Vector3 direction = targetPosition - sourcePosition;
            direction.y = 0f;
            Vector3 point = sourcePosition;
            if (direction.sqrMagnitude > 0.001f)
            {
                point += direction.normalized * Mathf.Min(0.7f, direction.magnitude * 0.5f);
            }

            return GroundPosition(point);
        }

        private static Vector3 GroundPosition(Vector3 position)
        {
            return position + Vector3.up * 0.035f;
        }

        private static void EmitAt(ParticleSystem system, Vector3 position, int count)
        {
            if (system == null || count <= 0)
            {
                return;
            }

            system.transform.position = position;
            system.Emit(count);
        }

        private static void PlayAttached(ParticleSystem system)
        {
            if (system != null)
            {
                system.Play(true);
            }
        }

        private static void EmitAttached(ParticleSystem system, int count)
        {
            if (system != null && count > 0)
            {
                system.Emit(count);
            }
        }

        private void StopAttached()
        {
            Stop(speedStreaks);
            Stop(airCompression);
            Stop(armorGlints);
        }

        private static void Stop(ParticleSystem system)
        {
            if (system != null)
            {
                system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private void Configure(ParticleSystem system, float lifetime, float size, Color color, float speed, float verticalSpeed)
        {
            if (system == null)
            {
                return;
            }

            ParticleSystem.MainModule main = system.main;
            main.startLifetime = Mathf.Max(0.05f, lifetime);
            float safeSize = Mathf.Max(0.01f, size);
            main.startSize = new ParticleSystem.MinMaxCurve(safeSize * 0.84f, safeSize * 1.18f);
            main.startColor = color;
            main.startSpeed = Mathf.Max(0f, speed);
            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = Mathf.Abs(verticalSpeed) > 0.001f;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = verticalSpeed;
        }

        private static void ConfigureEmission(ParticleSystem system, float rate)
        {
            if (system == null)
            {
                return;
            }

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = Mathf.Max(0f, rate);
        }

        private static void ConfigureShockwave(ParticleSystem system, float size, float speed, float maximumLifetime, Color color)
        {
            if (system == null)
            {
                return;
            }

            float lifetime = Mathf.Min(Mathf.Max(0.08f, maximumLifetime), Mathf.Max(0.08f, size / Mathf.Max(0.1f, speed)));
            ParticleSystem.MainModule main = system.main;
            main.startLifetime = lifetime;
            main.startSize = Mathf.Max(0.1f, size);
            main.startSpeed = 0f;
            main.startColor = color;
        }

        private Color Brighten(Color color)
        {
            color.r *= profile.OverallBrightness;
            color.g *= profile.OverallBrightness;
            color.b *= profile.OverallBrightness;
            return color;
        }

        private void Subscribe()
        {
            if (sourceSystem == null)
            {
                return;
            }

            sourceSystem.ChargeImpactStarted -= OnChargeImpactStarted;
            sourceSystem.ChargeImpactStarted += OnChargeImpactStarted;
            sourceSystem.ChargeCompleted -= OnChargeCompleted;
            sourceSystem.ChargeCompleted += OnChargeCompleted;
        }

        private void Unsubscribe()
        {
            if (sourceSystem == null)
            {
                return;
            }

            sourceSystem.ChargeImpactStarted -= OnChargeImpactStarted;
            sourceSystem.ChargeCompleted -= OnChargeCompleted;
        }

        private void CacheParticles()
        {
            allParticles = GetComponentsInChildren<ParticleSystem>(true);
        }

        private void StopAll(bool clear)
        {
            CancelInvoke();
            foreach (ParticleSystem particle in allParticles)
            {
                if (particle == null)
                {
                    continue;
                }

                particle.Stop(true, clear ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            isPlaying = false;
            isRecovering = false;
        }
    }
}
