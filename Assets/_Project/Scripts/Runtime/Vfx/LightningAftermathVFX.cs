using UnityEngine;

namespace RPGClone.Vfx.Shaman
{
    [DisallowMultipleComponent]
    public sealed class LightningAftermathVFX : MonoBehaviour, IMMOAbilityVfxInstance
    {
        [SerializeField] private LightningVFXProfile profile;
        [SerializeField] private LineRenderer residualArc;

        private MMOAbilityVfxContext context;
        private Vector3[] path = new Vector3[7];
        private System.Random random;
        private float startedAt;
        private float nextRefresh;
        private bool initialized;

        private void Awake()
        {
            if (residualArc != null) residualArc.enabled = false;
        }

        public void Initialize(MMOAbilityVfxContext newContext)
        {
            context = newContext;
            random = new System.Random(GetHashCode() ^ 0x5a71);
            startedAt = Time.time;
            initialized = true;
        }

        public void ConfigureAuthoring(LightningVFXProfile newProfile, LineRenderer newResidualArc)
        {
            profile = newProfile;
            residualArc = newResidualArc;
        }

        private void Update()
        {
            if (!initialized || profile == null || residualArc == null)
            {
                return;
            }

            float elapsed = Time.time - startedAt;
            float local = elapsed - profile.BeamDuration;
            if (local < 0f)
            {
                residualArc.enabled = false;
                return;
            }

            if (local >= profile.AftermathDuration)
            {
                residualArc.enabled = false;
                initialized = false;
                Destroy(gameObject);
                return;
            }

            float pulse = Mathf.Repeat(local * 9f, 1f);
            bool visible = (local < 0.08f || (local > profile.AftermathDuration * 0.48f && local < profile.AftermathDuration * 0.6f)) && pulse < 0.72f;
            if (!visible)
            {
                residualArc.enabled = false;
                return;
            }

            if (Time.time >= nextRefresh)
            {
                Vector3 start = context.SourcePosition;
                if (context.Source != null)
                {
                    MMOAbilityVfxAnchors anchors = context.Source.GetComponent<MMOAbilityVfxAnchors>();
                    if (anchors != null) start = anchors.ResolveCastOriginPosition(context.Definition);
                }

                Vector3 end = LightningVFXMath.ResolveHitPoint(context.Target, context.TargetPosition, context.Definition);
                LightningVFXMath.BuildJaggedPath(path, start, end, Mathf.Clamp(Vector3.Distance(start, end) * 0.035f, 0.16f, 0.7f), 2, random, local * 3f);
                nextRefresh = Time.time + 0.045f;
            }

            float alpha = (1f - local / profile.AftermathDuration) * 0.62f;
            LightningVFXMath.SetLine(residualArc, path, profile.CoreWidth * 0.45f, profile.CyanColor, alpha, -local * 5f, 2f);
        }
    }
}
