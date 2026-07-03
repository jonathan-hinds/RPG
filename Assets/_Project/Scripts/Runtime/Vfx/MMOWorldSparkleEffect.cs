using System.Collections.Generic;
using RPGClone.Services;
using UnityEngine;

namespace RPGClone.Vfx
{
    [DisallowMultipleComponent]
    public sealed class MMOWorldSparkleEffect : MonoBehaviour
    {
        private const int StarCount = 5;

        [SerializeField] private Color sparkleColor = new(1f, 0.86f, 0.22f, 1f);
        [SerializeField, Min(0.1f)] private float radius = 0.42f;
        [SerializeField, Min(0.01f)] private float lineWidth = 0.035f;
        [SerializeField, Min(0.1f)] private float pulseSpeed = 2.7f;
        [SerializeField, Min(0.1f)] private float spinSpeed = 26f;

        private readonly List<LineRenderer> lines = new();
        private Material lineMaterial;
        private Camera cachedCamera;

        private void Awake()
        {
            BuildIfNeeded();
        }

        private void OnEnable()
        {
            BuildIfNeeded();
            SetLinesVisible(true);
        }

        private void OnDisable()
        {
            SetLinesVisible(false);
        }

        private void LateUpdate()
        {
            BuildIfNeeded();
            cachedCamera ??= MMORuntimeSceneReferences.MainCamera != null ? MMORuntimeSceneReferences.MainCamera : Camera.main;
            if (cachedCamera != null)
            {
                transform.rotation = Quaternion.LookRotation(transform.position - cachedCamera.transform.position, Vector3.up);
            }

            float pulse = 0.7f + Mathf.PingPong(Time.unscaledTime * pulseSpeed, 1f) * 0.3f;
            float spin = Time.unscaledTime * spinSpeed;
            for (int i = 0; i < lines.Count; i++)
            {
                LineRenderer line = lines[i];
                if (line == null)
                {
                    continue;
                }

                float angle = (spin + i * (180f / StarCount)) * Mathf.Deg2Rad;
                Vector3 direction = new(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                float length = radius * pulse * (i == 0 ? 1.25f : 0.82f);
                line.startWidth = lineWidth * pulse;
                line.endWidth = lineWidth * 0.25f * pulse;
                line.SetPosition(0, -direction * length);
                line.SetPosition(1, direction * length);
            }
        }

        public void Configure(Color color, float newRadius)
        {
            sparkleColor = color;
            radius = Mathf.Max(0.1f, newRadius);
            lineMaterial = MMOParticleMaterialUtility.GetLineMaterial(sparkleColor);
            foreach (LineRenderer line in lines)
            {
                if (line != null)
                {
                    line.sharedMaterial = lineMaterial;
                    line.startColor = sparkleColor;
                    line.endColor = new Color(sparkleColor.r, sparkleColor.g, sparkleColor.b, 0f);
                }
            }
        }

        private void BuildIfNeeded()
        {
            if (lines.Count == StarCount)
            {
                return;
            }

            lineMaterial = MMOParticleMaterialUtility.GetLineMaterial(sparkleColor);
            while (lines.Count < StarCount)
            {
                GameObject lineObject = new($"Spark Streak {lines.Count + 1}");
                lineObject.transform.SetParent(transform, false);
                LineRenderer line = lineObject.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.positionCount = 2;
                line.numCapVertices = 4;
                line.numCornerVertices = 2;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.sharedMaterial = lineMaterial;
                line.startColor = sparkleColor;
                line.endColor = new Color(sparkleColor.r, sparkleColor.g, sparkleColor.b, 0f);
                line.textureMode = LineTextureMode.Stretch;
                lines.Add(line);
            }
        }

        private void SetLinesVisible(bool visible)
        {
            foreach (LineRenderer line in lines)
            {
                if (line != null)
                {
                    line.enabled = visible;
                }
            }
        }
    }
}
