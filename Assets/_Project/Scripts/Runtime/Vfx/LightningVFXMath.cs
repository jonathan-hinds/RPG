using UnityEngine;

namespace RPGClone.Vfx.Shaman
{
    internal static class LightningVFXMath
    {
        public static void BuildJaggedPath(
            Vector3[] points,
            Vector3 start,
            Vector3 end,
            float amplitude,
            int largeBends,
            System.Random random,
            float phase = 0f)
        {
            MMOProceduralVfxUtility.BuildJaggedPath(points, start, end, amplitude, largeBends, random, phase);
        }

        public static void SetLine(LineRenderer line, Vector3[] positions, float width, Color color, float alpha, float scroll, float tiling)
        {
            MMOProceduralVfxUtility.SetLine(line, positions, width, color, alpha, scroll, tiling);
        }

        public static Color Brighten(Color color, float brightness)
        {
            color.r *= brightness;
            color.g *= brightness;
            color.b *= brightness;
            return color;
        }

        public static Vector3 ResolveHitPoint(Transform target, Vector3 fallback, MMOAbilityVfxDefinition definition)
        {
            if (target == null)
            {
                return fallback;
            }

            MMOAbilityVfxAnchors anchors = target.GetComponent<MMOAbilityVfxAnchors>();
            return anchors != null ? anchors.ResolveHitPosition(definition) : target.TransformPoint(new Vector3(0f, 1.05f, 0f));
        }

        public static float Next01(System.Random random) => MMOProceduralVfxUtility.Next01(random);
        public static float NextSigned(System.Random random) => MMOProceduralVfxUtility.NextSigned(random);
    }
}
