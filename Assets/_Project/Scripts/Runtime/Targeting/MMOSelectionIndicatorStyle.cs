using UnityEngine;
using UnityEngine.Serialization;

namespace RPGClone.Targeting
{
    [CreateAssetMenu(
        fileName = "SelectionIndicatorStyle",
        menuName = "RPG Clone/Targeting/Selection Indicator Style")]
    public sealed class MMOSelectionIndicatorStyle : ScriptableObject
    {
        [Header("Rendering")]
        [SerializeField] private Shader indicatorShader;
        [SerializeField] private Texture2D groundMask;
        [SerializeField] private Texture2D reticleMask;

        [Header("Relationship Colors")]
        [SerializeField, ColorUsage(true, true)] private Color playerColor = new(0.15f, 1.15f, 0.35f, 0.92f);
        [SerializeField, ColorUsage(true, true)] private Color npcColor = new(1.2f, 0.78f, 0.08f, 0.94f);
        [SerializeField, ColorUsage(true, true)] private Color hostileColor = new(1.35f, 0.08f, 0.05f, 0.96f);

        [Header("Ground Sigil")]
        [SerializeField, Min(0.1f)] private float minimumGroundRadius = 1.1f;
        [SerializeField, Min(0.1f)] private float boundsRadiusMultiplier = 1.65f;
        [SerializeField, Min(0f)] private float groundOffset = 0.045f;
        [SerializeField, Range(0f, 1f)] private float groundGlowOpacity = 0.34f;
        [SerializeField, Range(0f, 1f)] private float groundCoreOpacity = 0.96f;
        [SerializeField, Min(0f)] private float groundRotationDegreesPerSecond = 5f;

        [Header("Floating Reticle")]
        [SerializeField] private string reticleLayerName = "UI";
        [SerializeField, Min(0.05f)] private float reticleWorldSize = 0.86f;
        [SerializeField, Min(0f)] private float reticleCameraOffset = 0.08f;
        [FormerlySerializedAs("reticleColor")]
        [SerializeField, ColorUsage(true, true)] private Color orbColor = new(2.2f, 2.2f, 2.2f, 1f);
        [SerializeField, Range(0.1f, 1f)] private float orbScale = 0.46f;
        [SerializeField, Range(0f, 1f)] private float orbOpacity = 0.9f;
        [SerializeField, Range(0f, 4f)] private float orbIntensity = 3.4f;
        [SerializeField, Range(0f, 1f)] private float reticleGlowOpacity = 0.58f;
        [SerializeField, Range(0f, 1f)] private float reticleCoreOpacity = 1f;
        [SerializeField] private float reticleRotationDegreesPerSecond = -4f;
        [SerializeField, Min(0f)] private float pulseFrequency = 1.2f;
        [SerializeField, Range(0f, 0.25f)] private float pulseScaleAmount = 0.04f;
        [SerializeField] private bool hideForLocalPlayer = true;

        public Shader IndicatorShader => indicatorShader;
        public Texture2D GroundMask => groundMask;
        public Texture2D ReticleMask => reticleMask;
        public Color PlayerColor => playerColor;
        public Color NpcColor => npcColor;
        public Color HostileColor => hostileColor;
        public float MinimumGroundRadius => minimumGroundRadius;
        public float BoundsRadiusMultiplier => boundsRadiusMultiplier;
        public float GroundOffset => groundOffset;
        public float GroundGlowOpacity => groundGlowOpacity;
        public float GroundCoreOpacity => groundCoreOpacity;
        public float GroundRotationDegreesPerSecond => groundRotationDegreesPerSecond;
        public string ReticleLayerName => reticleLayerName;
        public float ReticleWorldSize => reticleWorldSize;
        public float ReticleCameraOffset => reticleCameraOffset;
        public Color OrbColor => orbColor;
        public float OrbScale => orbScale;
        public float OrbOpacity => orbOpacity;
        public float OrbIntensity => orbIntensity;
        public float ReticleGlowOpacity => reticleGlowOpacity;
        public float ReticleCoreOpacity => reticleCoreOpacity;
        public float ReticleRotationDegreesPerSecond => reticleRotationDegreesPerSecond;
        public float PulseFrequency => pulseFrequency;
        public float PulseScaleAmount => pulseScaleAmount;
        public bool HideForLocalPlayer => hideForLocalPlayer;
    }
}
