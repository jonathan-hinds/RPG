using UnityEngine;

namespace RPGClone.Vfx
{
    public static class MMOProceduralVfxUtility
    {
        private static readonly int BaseMapSt = Shader.PropertyToID("_BaseMap_ST");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly MaterialPropertyBlock SharedPropertyBlock = new();

        public static void BuildJaggedPath(
            Vector3[] points,
            Vector3 start,
            Vector3 end,
            float amplitude,
            int largeBends,
            System.Random random,
            float phase = 0f)
        {
            if (points == null || points.Length == 0)
            {
                return;
            }

            Vector3 direction = end - start;
            Vector3 forward = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            Vector3 side = Vector3.Cross(forward, Vector3.up);
            if (side.sqrMagnitude < 0.001f)
            {
                side = Vector3.right;
            }

            side.Normalize();
            Vector3 up = Vector3.Cross(side, forward).normalized;
            int safeBends = Mathf.Max(1, largeBends);
            for (int i = 0; i < points.Length; i++)
            {
                float t = points.Length <= 1 ? 0f : i / (float)(points.Length - 1);
                float envelope = Mathf.Sin(t * Mathf.PI);
                float broad = Mathf.Sin((t * safeBends + phase) * Mathf.PI * 2f) * 0.48f;
                points[i] = Vector3.Lerp(start, end, t)
                    + side * amplitude * envelope * (broad + NextSigned(random) * 0.52f)
                    + up * amplitude * envelope * NextSigned(random) * 0.42f;
            }

            points[0] = start;
            points[points.Length - 1] = end;
        }

        public static void SetLine(
            LineRenderer line,
            Vector3[] positions,
            float width,
            Color color,
            float alpha,
            float scroll,
            float tiling)
        {
            if (line == null || positions == null || positions.Length == 0)
            {
                return;
            }

            line.positionCount = positions.Length;
            line.SetPositions(positions);
            line.widthMultiplier = Mathf.Max(0.001f, width);
            Color tinted = color;
            tinted.a *= Mathf.Clamp01(alpha);
            line.startColor = tinted;
            line.endColor = tinted;
            line.textureScale = new Vector2(Mathf.Max(0.1f, tiling), 1f);

            MaterialPropertyBlock block = SharedPropertyBlock;
            block.Clear();
            line.GetPropertyBlock(block);
            block.SetVector(BaseMapSt, new Vector4(Mathf.Max(0.1f, tiling), 1f, scroll, 0f));
            block.SetColor(BaseColor, tinted);
            block.SetColor(ColorId, tinted);
            block.SetColor(TintId, tinted);
            line.SetPropertyBlock(block);
            line.enabled = alpha > 0.001f;
        }

        public static float Next01(System.Random random) => random != null ? (float)random.NextDouble() : Random.value;
        public static float NextSigned(System.Random random) => Next01(random) * 2f - 1f;
    }
}
