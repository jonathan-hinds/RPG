using UnityEngine;

namespace RPGClone.World.Atmosphere
{
    [CreateAssetMenu(menuName = "RPG Clone/World/Ambient Dust Profile")]
    public sealed class MMOAmbientDustProfile : ScriptableObject
    {
        [Header("Follow")]
        public Vector3 localOffset = new(0f, 7.5f, 0f);
        public Vector3 emitterBoxSize = new(46f, 16f, 46f);

        [Header("Emission")]
        [Min(1)]
        public int maxParticles = 260;

        [Min(0f)]
        public float emissionRate = 22f;

        public Vector2 lifetimeRange = new(7f, 13f);
        public Vector2 speedRange = new(0.025f, 0.12f);
        public Vector2 sizeRange = new(0.035f, 0.105f);
        public Color dustColor = new(1f, 0.73f, 0.42f, 0.2f);

        [Header("Drift")]
        public Vector3 driftVelocity = new(0.08f, 0.015f, -0.035f);

        [Min(0f)]
        public float turbulenceStrength = 0.12f;

        [Min(0.001f)]
        public float turbulenceFrequency = 0.045f;

        [Min(0f)]
        public float turbulenceScrollSpeed = 0.08f;
    }
}
