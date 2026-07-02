using UnityEngine;

namespace RPGClone.Vfx
{
    [RequireComponent(typeof(LineRenderer))]
    public sealed class MMOAbilityVfxBeam : MonoBehaviour, IMMOAbilityVfxInstance
    {
        [SerializeField, Min(0.05f)] private float durationSeconds = 0.45f;
        [SerializeField, Range(2, 12)] private int pointCount = 6;
        [SerializeField, Min(0f)] private float noiseAmplitude = 0.08f;
        [SerializeField, Min(0f)] private float noiseFrequency = 24f;
        [SerializeField] private bool requestHitOnStart = true;

        private LineRenderer lineRenderer;
        private MMOAbilityVfxContext context;
        private float startTime;
        private bool initialized;
        private bool requestedHit;

        public void Configure(float newDurationSeconds, int newPointCount, float newNoiseAmplitude, float newNoiseFrequency, bool newRequestHitOnStart)
        {
            durationSeconds = Mathf.Max(0.05f, newDurationSeconds);
            pointCount = Mathf.Clamp(newPointCount, 2, 12);
            noiseAmplitude = Mathf.Max(0f, newNoiseAmplitude);
            noiseFrequency = Mathf.Max(0f, newNoiseFrequency);
            requestHitOnStart = newRequestHitOnStart;
        }

        private void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();
        }

        public void Initialize(MMOAbilityVfxContext newContext)
        {
            context = newContext;
            startTime = Time.time;
            initialized = true;
            requestedHit = false;
            EnsureLineRenderer();
            lineRenderer.positionCount = Mathf.Max(2, pointCount);
            if (requestHitOnStart)
            {
                RequestHitOnce();
            }

            UpdateLine();
        }

        private void Update()
        {
            if (!initialized)
            {
                return;
            }

            UpdateLine();
            if (Time.time - startTime >= durationSeconds)
            {
                Destroy(gameObject);
            }
        }

        private void UpdateLine()
        {
            EnsureLineRenderer();
            Vector3 start = context.Source != null ? context.SourcePosition : transform.position;
            Vector3 end = ResolveTargetPosition();
            Vector3 direction = end - start;
            Vector3 side = Vector3.Cross(direction.sqrMagnitude > 0.01f ? direction.normalized : Vector3.forward, Vector3.up);
            if (side.sqrMagnitude <= 0.001f)
            {
                side = Vector3.right;
            }

            side.Normalize();
            int count = Mathf.Max(2, pointCount);
            lineRenderer.positionCount = count;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)(count - 1);
                float noise = Mathf.Sin((Time.time + i * 0.37f) * noiseFrequency) * noiseAmplitude * Mathf.Sin(t * Mathf.PI);
                Vector3 lift = Vector3.up * (noiseAmplitude * 0.5f * Mathf.Sin((Time.time * noiseFrequency * 0.35f) + i));
                lineRenderer.SetPosition(i, Vector3.Lerp(start, end, t) + side * noise + lift);
            }
        }

        private Vector3 ResolveTargetPosition()
        {
            if (context.Target != null)
            {
                MMOAbilityVfxAnchors targetAnchors = context.Target.GetComponent<MMOAbilityVfxAnchors>();
                if (targetAnchors != null)
                {
                    return targetAnchors.ResolveHitPosition(context.Definition);
                }
            }

            return context.TargetPosition;
        }

        private void RequestHitOnce()
        {
            if (requestedHit)
            {
                return;
            }

            requestedHit = true;
            context.RequestHit?.Invoke();
        }

        private void EnsureLineRenderer()
        {
            if (lineRenderer == null)
            {
                lineRenderer = GetComponent<LineRenderer>();
            }
        }
    }
}
