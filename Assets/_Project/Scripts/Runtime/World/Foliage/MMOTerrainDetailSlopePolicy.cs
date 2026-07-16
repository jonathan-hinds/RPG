using UnityEngine;

namespace RPGClone.World.Foliage
{
    /// <summary>
    /// Centralized terrain-slope rules shared by procedural generation and editor painting.
    /// </summary>
    public static class MMOTerrainDetailSlopePolicy
    {
        public const float DefaultMaximumSlopeDegrees = 38f;
        public const float DefaultSlopeFadeRangeDegrees = 30f;

        public static float EvaluateDensityMultiplier(
            TerrainData terrainData,
            float normalizedX,
            float normalizedZ,
            float maximumSlopeDegrees,
            float fadeRangeDegrees)
        {
            if (terrainData == null)
            {
                return 0f;
            }

            float slopeDegrees = terrainData.GetSteepness(
                Mathf.Clamp01(normalizedX),
                Mathf.Clamp01(normalizedZ));
            return EvaluateDensityMultiplier(slopeDegrees, maximumSlopeDegrees, fadeRangeDegrees);
        }

        public static float EvaluateDensityMultiplier(
            float slopeDegrees,
            float maximumSlopeDegrees,
            float fadeRangeDegrees)
        {
            float maximumSlope = Mathf.Clamp(maximumSlopeDegrees, 0f, 90f);
            if (slopeDegrees > maximumSlope)
            {
                return 0f;
            }

            float fadeRange = Mathf.Clamp(fadeRangeDegrees, 0f, maximumSlope);
            if (fadeRange <= Mathf.Epsilon)
            {
                return 1f;
            }

            float fadeStart = maximumSlope - fadeRange;
            return 1f - Mathf.InverseLerp(fadeStart, maximumSlope, Mathf.Max(0f, slopeDegrees));
        }
    }
}
