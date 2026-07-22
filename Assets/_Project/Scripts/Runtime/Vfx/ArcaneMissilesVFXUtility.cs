using UnityEngine;

namespace RPGClone.Vfx.ArcaneMissiles
{
    internal static class ArcaneMissilesVFXUtility
    {
        internal static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        internal static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
        internal static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
        internal static readonly int TintId = Shader.PropertyToID("_Tint");

        internal static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        internal static void SetRenderer(Renderer renderer, MaterialPropertyBlock block, float opacity, float brightness, float dissolve, Color tint)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.GetPropertyBlock(block);
            block.SetFloat(OpacityId, Mathf.Clamp01(opacity));
            block.SetFloat(BrightnessId, Mathf.Max(0f, brightness));
            block.SetFloat(DissolveId, Mathf.Clamp01(dissolve));
            block.SetColor(TintId, tint);
            renderer.SetPropertyBlock(block);
        }

        internal static void ConfigureBurst(ParticleSystem particles, int count, Color color, float sizeMultiplier = 1f)
        {
            if (particles == null)
            {
                return;
            }

            ParticleSystem.MainModule main = particles.main;
            main.startColor = color;
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f * sizeMultiplier, 0.11f * sizeMultiplier);
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(count, 0, short.MaxValue)) });
        }

        internal static Vector3 SafeTargetPosition(Transform target, Vector3 fallback, MMOAbilityVfxDefinition definition)
        {
            if (target == null)
            {
                return fallback;
            }

            MMOAbilityVfxAnchors anchors = target.GetComponent<MMOAbilityVfxAnchors>();
            return anchors != null ? anchors.ResolveHitPosition(definition) : target.TransformPoint(new Vector3(0f, 1.05f, 0f));
        }
    }
}
