using UnityEngine;

namespace RPGClone.Vfx.ArcaneMissiles
{
    public enum ArcaneMissilesParticleQuality
    {
        Low,
        Medium,
        High
    }

    [CreateAssetMenu(menuName = "RPG Clone/VFX/Arcane Missiles Profile", fileName = "ArcaneMissilesVFXProfile")]
    public sealed class ArcaneMissilesVFXProfile : ScriptableObject
    {
        private const int FabricatorCount = 3;
        private const int MissileCount = 5;

        [Header("Channel")]
        [SerializeField, Min(0.1f)] private float channelDuration = 5f;
        [SerializeField, Min(0f)] private float handBrightness = 3.6f;
        [SerializeField, Min(0.01f)] private float centralCoreSize = 0.34f;
        [SerializeField, Min(0.01f)] private float runeCircleScale = 1.8f;
        [SerializeField, Min(0)] private int channelParticleAmount = 24;

        [Header("Fabricators")]
        [SerializeField] private Vector3[] orbOffsets =
        {
            new(-0.72f, 1.48f, 0.15f),
            new(0.72f, 1.48f, 0.15f),
            new(0f, 1.95f, -0.18f)
        };
        [SerializeField, Min(0f)] private float formationDelay = 0.16f;
        [SerializeField, Min(0.05f)] private float formationDuration = 0.38f;
        [SerializeField, Min(0.01f)] private float orbScale = 0.46f;
        [SerializeField, Min(0.01f)] private float runeScale = 0.74f;
        [SerializeField] private float runeSpeed = 28f;
        [SerializeField, Range(1, 2)] private int ringCount = 2;
        [SerializeField] private Vector2 ringSpeed = new(46f, -31f);
        [SerializeField, Min(0)] private int fragmentCount = 14;
        [SerializeField, Min(0.05f)] private float rebuildDuration = 0.62f;
        [SerializeField, Min(0.001f)] private float energyConnectionWidth = 0.035f;
        [SerializeField] private int[] firingOrder = { 0, 1, 2, 0, 2 };

        [Header("Projectile")]
        [SerializeField, Min(0.1f)] private float projectileSpeed = 13f;
        [SerializeField, Min(0f)] private float projectileAcceleration = 28f;
        [SerializeField, Min(0f)] private float homingStrength = 12f;
        [SerializeField, Min(0f)] private float curveAmount = 0.24f;
        [SerializeField, Min(0.01f)] private float projectileScale = 0.34f;
        [SerializeField, Min(0f)] private float projectileCoreBrightness = 4.8f;
        [SerializeField, Min(0.01f)] private float projectileBodyThickness = 0.28f;
        [SerializeField, Range(0f, 1f)] private float projectileRuneVisibility = 0.95f;
        [SerializeField, Min(0f)] private float spiralAmount = 0.075f;
        [SerializeField, Range(0.05f, 0.9f)] private float minimumLaunchLeadSeconds = 0.24f;
        [SerializeField, Range(0.1f, 1.2f)] private float maximumLaunchLeadSeconds = 0.68f;

        [Header("Trail")]
        [SerializeField, Min(0.001f)] private float trailCoreWidth = 0.055f;
        [SerializeField, Min(0.001f)] private float trailBlueRibbonWidth = 0.24f;
        [SerializeField, Range(1, 2)] private int purpleRibbonCount = 2;
        [SerializeField, Min(0.05f)] private float trailLifetime = 0.44f;
        [SerializeField, Min(0)] private int trailFragmentAmount = 12;
        [SerializeField, Min(0)] private int trailVaporAmount = 8;

        [Header("Impact")]
        [SerializeField, Min(0.01f)] private float impactFlashSize = 0.68f;
        [SerializeField, Min(0.01f)] private float impactExplosionScale = 1f;
        [SerializeField, Range(1, 2)] private int shockRingCount = 1;
        [SerializeField, Min(1)] private int spikeCount = 7;
        [SerializeField, Min(0)] private int impactSparkAmount = 26;
        [SerializeField, Min(0f)] private float targetWrapIntensity = 1f;
        [SerializeField, Range(0.35f, 0.65f)] private float impactDuration = 0.52f;

        [Header("Final Missile")]
        [SerializeField, Range(1.15f, 1.3f)] private float finalScaleMultiplier = 1.24f;
        [SerializeField, Min(1f)] private float finalBrightnessMultiplier = 1.28f;
        [SerializeField, Min(1f)] private float finalTrailMultiplier = 1.25f;
        [SerializeField, Min(1f)] private float finalImpactMultiplier = 1.28f;

        [Header("Interruption")]
        [SerializeField, Range(0.1f, 1f)] private float interruptCollapseDuration = 0.42f;
        [SerializeField, Min(0)] private int interruptRuneFragments = 30;
        [SerializeField, Min(0f)] private float connectionSnapIntensity = 1.25f;
        [SerializeField, Range(0.05f, 0.6f)] private float unfinishedMissileDissolve = 0.22f;

        [Header("Global")]
        [SerializeField, Min(0.01f)] private float overallScale = 1f;
        [SerializeField, Min(0f)] private float overallBrightness = 1f;
        [SerializeField, ColorUsage(true, true)] private Color blueColor = new(0.08f, 0.66f, 1.35f, 1f);
        [SerializeField, ColorUsage(true, true)] private Color purpleColor = new(0.52f, 0.12f, 1.15f, 1f);
        [SerializeField, ColorUsage(true, true)] private Color magentaAccent = new(1.1f, 0.08f, 0.78f, 1f);
        [SerializeField] private ArcaneMissilesParticleQuality particleQuality = ArcaneMissilesParticleQuality.High;
        [SerializeField, Range(0.3f, 0.7f)] private float completionCleanupDuration = 0.52f;

        public float ChannelDuration => channelDuration;
        public float HandBrightness => handBrightness;
        public float CentralCoreSize => centralCoreSize;
        public float RuneCircleScale => runeCircleScale;
        public int ChannelParticleAmount => ScaleCount(channelParticleAmount);
        public float FormationDelay => formationDelay;
        public float FormationDuration => formationDuration;
        public float OrbScale => orbScale;
        public float RuneScale => runeScale;
        public float RuneSpeed => runeSpeed;
        public int RingCount => ringCount;
        public Vector2 RingSpeed => ringSpeed;
        public int FragmentCount => ScaleCount(fragmentCount);
        public float RebuildDuration => rebuildDuration;
        public float EnergyConnectionWidth => energyConnectionWidth;
        public float ProjectileSpeed => projectileSpeed;
        public float ProjectileAcceleration => projectileAcceleration;
        public float HomingStrength => homingStrength;
        public float CurveAmount => curveAmount;
        public float ProjectileScale => projectileScale;
        public float ProjectileCoreBrightness => projectileCoreBrightness;
        public float ProjectileBodyThickness => projectileBodyThickness;
        public float ProjectileRuneVisibility => projectileRuneVisibility;
        public float SpiralAmount => spiralAmount;
        public float MinimumLaunchLeadSeconds => minimumLaunchLeadSeconds;
        public float MaximumLaunchLeadSeconds => maximumLaunchLeadSeconds;
        public float TrailCoreWidth => trailCoreWidth;
        public float TrailBlueRibbonWidth => trailBlueRibbonWidth;
        public int PurpleRibbonCount => purpleRibbonCount;
        public float TrailLifetime => trailLifetime;
        public int TrailFragmentAmount => ScaleCount(trailFragmentAmount);
        public int TrailVaporAmount => ScaleCount(trailVaporAmount);
        public float ImpactFlashSize => impactFlashSize;
        public float ImpactExplosionScale => impactExplosionScale;
        public int ShockRingCount => shockRingCount;
        public int SpikeCount => ScaleCount(spikeCount);
        public int ImpactSparkAmount => ScaleCount(impactSparkAmount);
        public float TargetWrapIntensity => targetWrapIntensity;
        public float ImpactDuration => impactDuration;
        public float FinalScaleMultiplier => finalScaleMultiplier;
        public float FinalBrightnessMultiplier => finalBrightnessMultiplier;
        public float FinalTrailMultiplier => finalTrailMultiplier;
        public float FinalImpactMultiplier => finalImpactMultiplier;
        public float InterruptCollapseDuration => interruptCollapseDuration;
        public int InterruptRuneFragments => ScaleCount(interruptRuneFragments);
        public float ConnectionSnapIntensity => connectionSnapIntensity;
        public float UnfinishedMissileDissolve => unfinishedMissileDissolve;
        public float OverallScale => overallScale;
        public float OverallBrightness => overallBrightness;
        public Color BlueColor => blueColor;
        public Color PurpleColor => purpleColor;
        public Color MagentaAccent => magentaAccent;
        public ArcaneMissilesParticleQuality ParticleQuality => particleQuality;
        public float CompletionCleanupDuration => completionCleanupDuration;
        public int MissileCountValue => MissileCount;

        public Vector3 GetOrbOffset(int index)
        {
            return orbOffsets != null && orbOffsets.Length == FabricatorCount
                ? orbOffsets[Mathf.Clamp(index, 0, FabricatorCount - 1)]
                : index switch
                {
                    0 => new Vector3(-0.72f, 1.48f, 0.15f),
                    1 => new Vector3(0.72f, 1.48f, 0.15f),
                    _ => new Vector3(0f, 1.95f, -0.18f)
                };
        }

        public int GetFiringOrb(int missileIndex)
        {
            int fallback = missileIndex switch { 0 => 0, 1 => 1, 2 => 2, 3 => 0, _ => 2 };
            return firingOrder != null && firingOrder.Length == MissileCount
                ? Mathf.Clamp(firingOrder[Mathf.Clamp(missileIndex, 0, MissileCount - 1)], 0, FabricatorCount - 1)
                : fallback;
        }

        public float QualityMultiplier => particleQuality switch
        {
            ArcaneMissilesParticleQuality.Low => 0.48f,
            ArcaneMissilesParticleQuality.Medium => 0.72f,
            _ => 1f
        };

        private int ScaleCount(int value)
        {
            return Mathf.Max(value > 0 ? 1 : 0, Mathf.RoundToInt(value * QualityMultiplier));
        }

        private void OnValidate()
        {
            if (orbOffsets == null || orbOffsets.Length != FabricatorCount)
            {
                orbOffsets = new[]
                {
                    new Vector3(-0.72f, 1.48f, 0.15f),
                    new Vector3(0.72f, 1.48f, 0.15f),
                    new Vector3(0f, 1.95f, -0.18f)
                };
            }

            if (firingOrder == null || firingOrder.Length != MissileCount)
            {
                firingOrder = new[] { 0, 1, 2, 0, 2 };
            }
        }
    }
}
