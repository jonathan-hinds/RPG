using UnityEngine;

namespace RPGClone.Vfx.Fire
{
    [DisallowMultipleComponent]
    public sealed class FlamestrikeCastVFX : MonoBehaviour, IMMOAbilityVfxInstance, IMMOAbilityVfxReleaseHandler
    {
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int ScrollId = Shader.PropertyToID("_Scroll");

        [SerializeField] private FlamestrikeVFXProfile profile;
        [SerializeField] private Transform leftHandFlame;
        [SerializeField] private Transform rightHandFlame;
        [SerializeField] private Transform coreRoot;
        [SerializeField] private Renderer[] flameRenderers;
        [SerializeField] private LineRenderer[] conductionRibbons;
        [SerializeField] private LineRenderer[] orbitRibbons;
        [SerializeField] private Transform targetBuildupRoot;
        [SerializeField] private Renderer[] targetBuildupRenderers;
        [SerializeField] private ParticleSystem castEmbers;

        private MaterialPropertyBlock properties;
        private readonly Vector3[] linePoints = new Vector3[12];
        private MMOAbilityVfxContext context;
        private MMOAbilityVfxAnchors anchors;
        private float startedAt;
        private float releaseStartedAt = float.NegativeInfinity;
        private bool initialized;

        public FlamestrikeVFXProfile Profile => profile;

        public void ConfigureAuthoring(
            FlamestrikeVFXProfile newProfile,
            Transform newLeftHandFlame,
            Transform newRightHandFlame,
            Transform newCoreRoot,
            Renderer[] newFlameRenderers,
            LineRenderer[] newConductionRibbons,
            LineRenderer[] newOrbitRibbons,
            Transform newTargetBuildupRoot,
            Renderer[] newTargetBuildupRenderers,
            ParticleSystem newCastEmbers)
        {
            profile = newProfile;
            leftHandFlame = newLeftHandFlame;
            rightHandFlame = newRightHandFlame;
            coreRoot = newCoreRoot;
            flameRenderers = newFlameRenderers;
            conductionRibbons = newConductionRibbons;
            orbitRibbons = newOrbitRibbons;
            targetBuildupRoot = newTargetBuildupRoot;
            targetBuildupRenderers = newTargetBuildupRenderers;
            castEmbers = newCastEmbers;
        }

        public void Initialize(MMOAbilityVfxContext newContext)
        {
            if (profile == null)
            {
                Debug.LogError("FlamestrikeCastVFX requires a profile.", this);
                Destroy(gameObject);
                return;
            }

            context = newContext;
            anchors = context.Source != null ? context.Source.GetComponent<MMOAbilityVfxAnchors>() : null;
            transform.SetParent(null, true);
            startedAt = Time.time;
            releaseStartedAt = float.NegativeInfinity;
            initialized = true;
            if (castEmbers != null)
            {
                ParticleSystem.EmissionModule emission = castEmbers.emission;
                emission.rateOverTime = profile.CastEmberAmount;
                castEmbers.Play(true);
            }
        }

        public void Release(bool immediate)
        {
            if (immediate)
            {
                Destroy(gameObject);
                return;
            }

            releaseStartedAt = Time.time;
            castEmbers?.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private void Update()
        {
            if (!initialized || profile == null)
            {
                return;
            }

            float progress = context.SourceSystem != null && context.SourceSystem.IsCasting
                ? context.SourceSystem.CurrentCastNormalized
                : Mathf.Clamp01((Time.time - startedAt) / profile.CastDuration);
            float fade = float.IsNegativeInfinity(releaseStartedAt)
                ? 1f
                : 1f - Mathf.Clamp01((Time.time - releaseStartedAt) / 0.28f);
            UpdateHandAndCorePositions(progress, fade);
            UpdateRibbons(progress, fade);
            UpdateTargetBuildup(progress, fade);
            UpdateRendererProperties(progress, fade);

            if (fade <= 0f)
            {
                Destroy(gameObject);
            }
        }

        private void UpdateHandAndCorePositions(float progress, float fade)
        {
            Vector3 sourcePosition = context.Source != null ? context.Source.position + Vector3.up * 1.15f : context.SourcePosition;
            Vector3 left = anchors != null ? anchors.ResolveLeftHandCastingPosition(context.Definition) : sourcePosition + Vector3.left * 0.32f;
            Vector3 right = anchors != null ? anchors.ResolveRightHandCastingPosition(context.Definition) : sourcePosition + Vector3.right * 0.32f;
            if (leftHandFlame != null)
            {
                leftHandFlame.position = left;
                leftHandFlame.localScale = Vector3.one * profile.HandFlameScale * Mathf.Lerp(0.35f, 1f, progress) * fade;
            }

            if (rightHandFlame != null)
            {
                rightHandFlame.position = right;
                rightHandFlame.localScale = Vector3.one * profile.HandFlameScale * Mathf.Lerp(0.35f, 1f, progress) * fade;
            }

            if (coreRoot != null)
            {
                Vector3 forward = context.Source != null ? context.Source.forward : Vector3.forward;
                coreRoot.position = Vector3.Lerp(left, right, 0.5f) + forward * 0.18f;
                float compression = progress > 0.925f ? Mathf.Lerp(1f, 0.72f, (progress - 0.925f) / 0.075f) : 1f;
                float pulse = 1f + Mathf.Sin(Time.time * Mathf.Lerp(7f, 18f, progress)) * 0.08f * progress;
                coreRoot.localScale = Vector3.one * profile.FireCoreScale * Mathf.SmoothStep(0.08f, 1f, progress) * compression * pulse * fade;
                coreRoot.Rotate(Vector3.up, (90f + profile.CasterRibbonSpeed * 60f) * Time.deltaTime, Space.World);
            }
        }

        private void UpdateRibbons(float progress, float fade)
        {
            Vector3 left = leftHandFlame != null ? leftHandFlame.position : context.SourcePosition;
            Vector3 right = rightHandFlame != null ? rightHandFlame.position : context.SourcePosition;
            for (int ribbonIndex = 0; ribbonIndex < conductionRibbons.Length; ribbonIndex++)
            {
                LineRenderer ribbon = conductionRibbons[ribbonIndex];
                if (ribbon == null) continue;
                Vector3 delta = right - left;
                Vector3 side = Vector3.Cross(delta.normalized, Vector3.up);
                float phase = Time.time * (4.5f + ribbonIndex) + ribbonIndex * 2.1f;
                for (int i = 0; i < linePoints.Length; i++)
                {
                    float t = i / (float)(linePoints.Length - 1);
                    float envelope = Mathf.Sin(t * Mathf.PI);
                    linePoints[i] = Vector3.Lerp(left, right, t)
                        + Vector3.up * Mathf.Sin(t * Mathf.PI * 2f + phase) * 0.12f * envelope
                        + side * Mathf.Cos(t * Mathf.PI * 3f + phase) * 0.09f * envelope;
                }
                ribbon.positionCount = linePoints.Length;
                ribbon.SetPositions(linePoints);
                ribbon.widthMultiplier = Mathf.Lerp(0.012f, 0.07f, progress) * profile.HandConductionAmount * fade;
            }

            Vector3 center = coreRoot != null ? coreRoot.position : Vector3.Lerp(left, right, 0.5f);
            for (int ribbonIndex = 0; ribbonIndex < orbitRibbons.Length; ribbonIndex++)
            {
                LineRenderer ribbon = orbitRibbons[ribbonIndex];
                if (ribbon == null) continue;
                for (int i = 0; i < linePoints.Length; i++)
                {
                    float t = i / (float)(linePoints.Length - 1);
                    float angle = t * Mathf.PI * 2f + Time.time * profile.CasterRibbonSpeed * (ribbonIndex % 2 == 0 ? 1f : -1f) + ribbonIndex;
                    float radius = Mathf.Lerp(0.18f, 0.62f, t) * Mathf.Lerp(0.25f, 1f, progress);
                    linePoints[i] = center + new Vector3(Mathf.Cos(angle) * radius, (t - 0.5f) * 1.15f, Mathf.Sin(angle) * radius);
                }
                ribbon.positionCount = linePoints.Length;
                ribbon.SetPositions(linePoints);
                ribbon.widthMultiplier = 0.045f * progress * fade;
            }
        }

        private void UpdateTargetBuildup(float progress, float fade)
        {
            if (targetBuildupRoot == null || !context.HasGroundTarget)
            {
                if (targetBuildupRoot != null) targetBuildupRoot.gameObject.SetActive(false);
                return;
            }

            targetBuildupRoot.gameObject.SetActive(true);
            targetBuildupRoot.position = context.TargetPosition + Vector3.up * 0.045f;
            targetBuildupRoot.rotation = Quaternion.Euler(0f, Time.time * 18f, 0f);
            float size = Mathf.Max(0.1f, context.Ability != null ? context.Ability.AreaRadius : profile.AreaRadius) * 2f;
            targetBuildupRoot.localScale = new Vector3(size, 1f, size);
            foreach (Renderer renderer in targetBuildupRenderers)
            {
                SetRenderer(renderer, Mathf.Lerp(0.06f, 0.58f, progress) * profile.TargetBuildupIntensity * fade, progress);
            }
        }

        private void UpdateRendererProperties(float progress, float fade)
        {
            float peak = progress > 0.85f ? 1f + (progress - 0.85f) * 2.5f : 1f;
            foreach (Renderer renderer in flameRenderers)
            {
                SetRenderer(renderer, Mathf.SmoothStep(0f, 1f, progress) * peak * fade, progress);
            }
        }

        private void SetRenderer(Renderer renderer, float opacity, float progress)
        {
            if (renderer == null) return;
            properties ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            properties.SetFloat(OpacityId, Mathf.Clamp01(opacity));
            properties.SetColor(TintId, profile.FlameColor * profile.OverallBrightness);
            properties.SetVector(ScrollId, new Vector4(0f, Time.time * -0.08f, 0f, 0f));
            renderer.SetPropertyBlock(properties);
            renderer.enabled = opacity > 0.001f;
        }

    }
}
