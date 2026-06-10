using UnityEngine;

namespace RPGClone.World.Atmosphere
{
    [DisallowMultipleComponent]
    public sealed class MMOAmbientDustFollower : MonoBehaviour
    {
        [SerializeField] private MMOAmbientDustProfile profile;
        [SerializeField] private Transform followTarget;
        [SerializeField] private ParticleSystem dustParticles;

        public MMOAmbientDustProfile Profile
        {
            get => profile;
            set
            {
                profile = value;
                ApplyProfile();
            }
        }

        public Transform FollowTarget
        {
            get => followTarget;
            set => followTarget = value;
        }

        private void Reset()
        {
            dustParticles = GetComponent<ParticleSystem>();
        }

        private void Awake()
        {
            ResolveReferences();
            ApplyProfile();

            if (dustParticles != null && !dustParticles.isPlaying)
            {
                dustParticles.Play();
            }
        }

        private void LateUpdate()
        {
            if (profile == null)
            {
                return;
            }

            if (followTarget == null)
            {
                ResolveFollowTarget();
            }

            if (followTarget != null)
            {
                transform.position = followTarget.position + profile.localOffset;
            }
        }

        private void OnValidate()
        {
            ResolveReferences();
            ApplyProfile();
        }

        private void ResolveReferences()
        {
            if (dustParticles == null)
            {
                dustParticles = GetComponent<ParticleSystem>();
            }

            ResolveFollowTarget();
        }

        private void ResolveFollowTarget()
        {
            if (followTarget != null)
            {
                return;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                followTarget = mainCamera.transform;
            }
        }

        public void ApplyProfile()
        {
            if (profile == null || dustParticles == null)
            {
                return;
            }

            Vector2 lifetime = SortRange(profile.lifetimeRange, 0.1f);
            Vector2 speed = SortRange(profile.speedRange, 0f);
            Vector2 size = SortRange(profile.sizeRange, 0.001f);

            ParticleSystem.MainModule main = dustParticles.main;
            main.loop = true;
            main.prewarm = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Max(1, profile.maxParticles);
            main.gravityModifier = 0f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startColor = profile.dustColor;

            ParticleSystem.EmissionModule emission = dustParticles.emission;
            emission.enabled = true;
            emission.rateOverTime = Mathf.Max(0f, profile.emissionRate);

            ParticleSystem.ShapeModule shape = dustParticles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = profile.emitterBoxSize;

            ParticleSystem.VelocityOverLifetimeModule velocity = dustParticles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = profile.driftVelocity.x;
            velocity.y = profile.driftVelocity.y;
            velocity.z = profile.driftVelocity.z;

            ParticleSystem.NoiseModule noise = dustParticles.noise;
            noise.enabled = profile.turbulenceStrength > 0f;
            noise.strength = profile.turbulenceStrength;
            noise.frequency = profile.turbulenceFrequency;
            noise.scrollSpeed = profile.turbulenceScrollSpeed;
            noise.damping = true;
            noise.quality = ParticleSystemNoiseQuality.Low;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = dustParticles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new();
            Color clear = profile.dustColor;
            clear.a = 0f;
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(profile.dustColor, 0f),
                    new GradientColorKey(profile.dustColor, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(profile.dustColor.a, 0.18f),
                    new GradientAlphaKey(profile.dustColor.a * 0.78f, 0.72f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = gradient;

            ParticleSystemRenderer renderer = dustParticles.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.sortingFudge = -0.2f;
                renderer.minParticleSize = 0.001f;
                renderer.maxParticleSize = 0.08f;
            }
        }

        private static Vector2 SortRange(Vector2 range, float minimum)
        {
            float min = Mathf.Max(minimum, Mathf.Min(range.x, range.y));
            float max = Mathf.Max(min, Mathf.Max(range.x, range.y));
            return new Vector2(min, max);
        }
    }
}
