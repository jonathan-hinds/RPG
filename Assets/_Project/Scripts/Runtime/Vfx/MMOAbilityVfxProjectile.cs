using UnityEngine;

namespace RPGClone.Vfx
{
    public sealed class MMOAbilityVfxProjectile : MonoBehaviour, IMMOAbilityVfxInstance
    {
        [SerializeField, Min(0.1f)] private float speed = 18f;
        [SerializeField, Min(0f)] private float arcHeight;
        [SerializeField, Min(0.1f)] private float maxLifetimeSeconds = 4f;
        [SerializeField] private bool requestHitOnArrival = true;

        private MMOAbilityVfxContext context;
        private Vector3 startPosition;
        private float startTime;
        private bool initialized;
        private bool requestedHit;

        public void Configure(float newSpeed, float newArcHeight, float newMaxLifetimeSeconds, bool newRequestHitOnArrival)
        {
            speed = Mathf.Max(0.1f, newSpeed);
            arcHeight = Mathf.Max(0f, newArcHeight);
            maxLifetimeSeconds = Mathf.Max(0.1f, newMaxLifetimeSeconds);
            requestHitOnArrival = newRequestHitOnArrival;
        }

        public void Initialize(MMOAbilityVfxContext newContext)
        {
            context = newContext;
            startPosition = newContext.SourcePosition;
            startTime = Time.time;
            initialized = true;
            requestedHit = false;
            transform.position = startPosition;
        }

        private void Update()
        {
            if (!initialized)
            {
                return;
            }

            Vector3 targetPosition = ResolveTargetPosition();
            Vector3 toTarget = targetPosition - transform.position;
            float step = speed * Time.deltaTime;
            if (toTarget.sqrMagnitude <= step * step || Time.time - startTime >= maxLifetimeSeconds)
            {
                transform.position = targetPosition;
                if (requestHitOnArrival)
                {
                    RequestHitOnce();
                }

                Destroy(gameObject);
                return;
            }

            Vector3 nextPosition = Vector3.MoveTowards(transform.position, targetPosition, step);
            if (arcHeight > 0f)
            {
                float totalDistance = Mathf.Max(0.01f, Vector3.Distance(startPosition, targetPosition));
                float traveled = Vector3.Distance(startPosition, nextPosition);
                nextPosition.y += Mathf.Sin(Mathf.Clamp01(traveled / totalDistance) * Mathf.PI) * arcHeight;
            }

            if ((targetPosition - transform.position).sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation((targetPosition - transform.position).normalized, Vector3.up);
            }

            transform.position = nextPosition;
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
    }
}
