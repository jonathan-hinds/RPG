using UnityEngine;

namespace RPGClone.Vfx.Warrior
{
    internal static class ThunderClapVFXUtility
    {
        public static void StopAndClear(ParticleSystem system)
        {
            system?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public static void EmitAt(
            ParticleSystem system,
            Vector3 position,
            int count,
            float size,
            Color color,
            System.Random random)
        {
            if (system == null || count <= 0)
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                ParticleSystem.EmitParams emit = new()
                {
                    position = position,
                    startSize = size * Mathf.Lerp(0.78f, 1.22f, MMOProceduralVfxUtility.Next01(random)),
                    startColor = color,
                    rotation = MMOProceduralVfxUtility.Next01(random) * Mathf.PI * 2f
                };
                system.Emit(emit, 1);
            }
        }

        public static void EmitRadial(
            ParticleSystem system,
            Vector3 center,
            int count,
            float horizontalSpeed,
            float verticalSpeed,
            float size,
            Color color,
            System.Random random,
            float spawnRadius = 0.2f)
        {
            if (system == null || count <= 0)
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                float angle = MMOProceduralVfxUtility.Next01(random) * Mathf.PI * 2f;
                Vector3 radial = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                ParticleSystem.EmitParams emit = new()
                {
                    position = center + radial * MMOProceduralVfxUtility.Next01(random) * spawnRadius,
                    velocity = radial * horizontalSpeed * Mathf.Lerp(0.58f, 1.18f, MMOProceduralVfxUtility.Next01(random))
                        + Vector3.up * verticalSpeed * Mathf.Lerp(0.35f, 1.1f, MMOProceduralVfxUtility.Next01(random)),
                    startSize = size * Mathf.Lerp(0.72f, 1.28f, MMOProceduralVfxUtility.Next01(random)),
                    startColor = color,
                    rotation = MMOProceduralVfxUtility.Next01(random) * Mathf.PI * 2f
                };
                system.Emit(emit, 1);
            }
        }

        public static void EmitRing(
            ParticleSystem system,
            Vector3 center,
            int count,
            float radius,
            float outwardSpeed,
            float lift,
            float size,
            Color color,
            System.Random random)
        {
            if (system == null || count <= 0)
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                float angle = (i + MMOProceduralVfxUtility.Next01(random) * 0.35f) / count * Mathf.PI * 2f;
                Vector3 radial = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                ParticleSystem.EmitParams emit = new()
                {
                    position = center + radial * radius,
                    velocity = radial * outwardSpeed + Vector3.up * lift * MMOProceduralVfxUtility.Next01(random),
                    startSize = size * Mathf.Lerp(0.74f, 1.26f, MMOProceduralVfxUtility.Next01(random)),
                    startColor = color,
                    rotation = MMOProceduralVfxUtility.Next01(random) * Mathf.PI * 2f
                };
                system.Emit(emit, 1);
            }
        }

        public static void ConfigureMain(ParticleSystem system, float lifetime, float size, Color color)
        {
            if (system == null)
            {
                return;
            }

            ParticleSystem.MainModule main = system.main;
            main.startLifetime = Mathf.Max(0.04f, lifetime);
            main.startSize = Mathf.Max(0.01f, size);
            main.startColor = color;
        }
    }
}
