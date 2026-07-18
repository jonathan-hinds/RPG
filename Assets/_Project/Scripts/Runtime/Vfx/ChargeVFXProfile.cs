using UnityEngine;

namespace RPGClone.Vfx.Warrior
{
    [CreateAssetMenu(menuName = "RPG Clone/VFX/Warrior/Charge Profile", fileName = "ChargeVFXProfile")]
    public sealed class ChargeVFXProfile : ScriptableObject
    {
        [Header("Global")]
        [SerializeField, Range(0.1f, 3f)] private float overallScale = 1f;
        [SerializeField, Range(0f, 3f)] private float overallBrightness = 1.08f;
        [SerializeField, Min(1f)] private float maximumTravelDuration = 6f;

        [Header("Launch")]
        [SerializeField, Range(0, 64)] private int launchDustAmount = 36;
        [SerializeField, Range(0, 32)] private int launchDirtAmount = 14;
        [SerializeField, Range(0, 24)] private int launchRockAmount = 8;
        [SerializeField, Min(0.05f)] private float launchDustSize = 1.42f;

        [Header("World-Space Trail")]
        [SerializeField, Range(1f, 60f)] private float dustEventSpawnRate = 26f;
        [SerializeField, Min(0.05f)] private float heavyDustLifetime = 1.65f;
        [SerializeField, Min(0.05f)] private float fineDustLifetime = 2.25f;
        [SerializeField, Range(0f, 30f)] private float dirtChunkFrequency = 9f;
        [SerializeField, Min(0.01f)] private float dirtChunkSize = 0.27f;
        [SerializeField, Range(0f, 60f)] private float groundScrapeFrequency = 32f;
        [SerializeField, Min(0.05f)] private float trailSpacing = 0.28f;
        [SerializeField, Min(0.01f)] private float heavyDustSize = 0.9f;
        [SerializeField, Min(0.01f)] private float fineDustSize = 0.54f;
        [SerializeField, Min(0f)] private float dustRiseSpeed = 0.72f;
        [SerializeField, Min(0f)] private float trailSpreadSpeed = 1.3f;

        [Header("Ground Shockwaves")]
        [SerializeField, Min(0.1f)] private float shockwaveSize = 5.9f;
        [SerializeField, Min(0.1f)] private float shockwaveSpeed = 13.5f;
        [SerializeField, Min(0.05f)] private float shockwaveLifetime = 0.5f;

        [Header("Character Motion Layers")]
        [SerializeField, Range(0f, 3f)] private float speedStreakIntensity = 1.25f;
        [SerializeField, Range(0f, 40f)] private float airCompressionFrequency = 12f;
        [SerializeField, Range(0f, 40f)] private float armorGlintFrequency = 7f;
        [SerializeField, Min(0.05f)] private float speedStreakLength = 1.85f;

        [Header("Collision")]
        [SerializeField, Min(0.1f)] private float collisionBurstSize = 2.5f;
        [SerializeField, Range(0, 96)] private int collisionDustAmount = 52;
        [SerializeField, Range(0, 64)] private int collisionShardAmount = 22;
        [SerializeField, Range(0, 48)] private int collisionDebrisAmount = 24;
        [SerializeField, Min(0.05f)] private float collisionDustLifetime = 2.4f;

        [Header("Recovery")]
        [SerializeField, Min(0.1f)] private float recoveryDuration = 3.8f;
        [SerializeField, Range(0, 32)] private int recoveryDustAmount = 16;

        [Header("Palette")]
        [SerializeField] private Color heavyDustColor = new(0.62f, 0.48f, 0.31f, 0.72f);
        [SerializeField] private Color fineDustColor = new(0.82f, 0.7f, 0.5f, 0.48f);
        [SerializeField] private Color dirtColor = new(0.34f, 0.22f, 0.13f, 0.94f);
        [SerializeField] private Color rockColor = new(0.38f, 0.37f, 0.35f, 0.96f);
        [SerializeField] private Color shockwaveColor = new(0.9f, 0.78f, 0.57f, 0.58f);
        [SerializeField] private Color compressedAirColor = new(1f, 0.93f, 0.78f, 0.3f);
        [SerializeField] private Color speedStreakColor = new(1f, 0.9f, 0.7f, 0.48f);
        [SerializeField] private Color impactColor = new(1f, 0.76f, 0.38f, 0.92f);
        [SerializeField] private Color metallicColor = new(1f, 0.94f, 0.76f, 0.82f);

        public float OverallScale => overallScale;
        public float OverallBrightness => overallBrightness;
        public float MaximumTravelDuration => maximumTravelDuration;
        public int LaunchDustAmount => launchDustAmount;
        public int LaunchDirtAmount => launchDirtAmount;
        public int LaunchRockAmount => launchRockAmount;
        public float LaunchDustSize => launchDustSize;
        public float DustEventSpawnRate => dustEventSpawnRate;
        public float HeavyDustLifetime => heavyDustLifetime;
        public float FineDustLifetime => fineDustLifetime;
        public float DirtChunkFrequency => dirtChunkFrequency;
        public float DirtChunkSize => dirtChunkSize;
        public float GroundScrapeFrequency => groundScrapeFrequency;
        public float TrailSpacing => trailSpacing;
        public float HeavyDustSize => heavyDustSize;
        public float FineDustSize => fineDustSize;
        public float DustRiseSpeed => dustRiseSpeed;
        public float TrailSpreadSpeed => trailSpreadSpeed;
        public float ShockwaveSize => shockwaveSize;
        public float ShockwaveSpeed => shockwaveSpeed;
        public float ShockwaveLifetime => shockwaveLifetime;
        public float SpeedStreakIntensity => speedStreakIntensity;
        public float AirCompressionFrequency => airCompressionFrequency;
        public float ArmorGlintFrequency => armorGlintFrequency;
        public float SpeedStreakLength => speedStreakLength;
        public float CollisionBurstSize => collisionBurstSize;
        public int CollisionDustAmount => collisionDustAmount;
        public int CollisionShardAmount => collisionShardAmount;
        public int CollisionDebrisAmount => collisionDebrisAmount;
        public float CollisionDustLifetime => collisionDustLifetime;
        public float RecoveryDuration => recoveryDuration;
        public int RecoveryDustAmount => recoveryDustAmount;
        public Color HeavyDustColor => heavyDustColor;
        public Color FineDustColor => fineDustColor;
        public Color DirtColor => dirtColor;
        public Color RockColor => rockColor;
        public Color ShockwaveColor => shockwaveColor;
        public Color CompressedAirColor => compressedAirColor;
        public Color SpeedStreakColor => speedStreakColor;
        public Color ImpactColor => impactColor;
        public Color MetallicColor => metallicColor;
    }
}
