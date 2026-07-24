using UnityEngine;

namespace RPGClone.Vfx.Shaman
{
    internal static class EarthquakeVFXUtility
    {
        public static void StopAndClear(ParticleSystem system)
        {
            system?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public static void EmitAt(ParticleSystem system, Vector3 position, int count, float size, Color color, System.Random random)
        {
            if (system == null || count <= 0) return;
            for (int i = 0; i < count; i++)
            {
                ParticleSystem.EmitParams emit = new()
                {
                    position = position,
                    startSize = size * Mathf.Lerp(0.72f, 1.28f, MMOProceduralVfxUtility.Next01(random)),
                    startColor = color,
                    rotation = MMOProceduralVfxUtility.Next01(random) * Mathf.PI * 2f
                };
                system.Emit(emit, 1);
            }
        }

        public static void EmitRadial(
            ParticleSystem system, Vector3 center, int count, float speed, float maxLift,
            float size, float lifetime, Color color, System.Random random, float spawnRadius = 0.25f,
            Vector3 surfaceNormal = default)
        {
            if (system == null || count <= 0) return;
            Vector3 normal = surfaceNormal.sqrMagnitude > 0.5f ? surfaceNormal.normalized : Vector3.up;
            Vector3 tangent = Vector3.Cross(normal, Vector3.forward);
            if (tangent.sqrMagnitude < 0.01f) tangent = Vector3.Cross(normal, Vector3.right);
            tangent.Normalize();
            Vector3 bitangent = Vector3.Cross(normal, tangent).normalized;
            for (int i = 0; i < count; i++)
            {
                float angle = MMOProceduralVfxUtility.Next01(random) * Mathf.PI * 2f;
                Vector3 radial = tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle);
                float irregularity = Mathf.Lerp(0.72f, 1.22f, MMOProceduralVfxUtility.Next01(random));
                ParticleSystem.EmitParams emit = new()
                {
                    position = center + radial * spawnRadius * MMOProceduralVfxUtility.Next01(random),
                    velocity = radial * speed * irregularity + normal * maxLift * MMOProceduralVfxUtility.Next01(random),
                    startSize = size * Mathf.Lerp(0.68f, 1.32f, MMOProceduralVfxUtility.Next01(random)),
                    startLifetime = lifetime * Mathf.Lerp(0.82f, 1.18f, MMOProceduralVfxUtility.Next01(random)),
                    startColor = color,
                    rotation = MMOProceduralVfxUtility.Next01(random) * Mathf.PI * 2f
                };
                system.Emit(emit, 1);
            }
        }

        public static void EmitRingParticle(ParticleSystem system, Vector3 center, float diameter, float lifetime, Color color,
            Vector3 surfaceNormal = default)
        {
            if (system == null) return;
            Vector3 normal = surfaceNormal.sqrMagnitude > 0.5f ? surfaceNormal.normalized : Vector3.up;
            ParticleSystem.EmitParams emit = new()
            {
                position = center,
                startSize = diameter,
                startLifetime = lifetime,
                startColor = color,
                rotation3D = Quaternion.FromToRotation(Vector3.up, normal).eulerAngles * Mathf.Deg2Rad
            };
            system.Emit(emit, 1);
        }
    }
}
