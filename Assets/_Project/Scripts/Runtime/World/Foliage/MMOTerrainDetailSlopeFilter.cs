using UnityEngine;

namespace RPGClone.World.Foliage
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Terrain))]
    public sealed class MMOTerrainDetailSlopeFilter : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Shared foliage settings that provide the maximum allowed Terrain detail slope.")]
        private MMOClassicGrassFoliageProfile foliageProfile;

        [Header("Random Detail Thinning")]
        [SerializeField]
        [Min(0)]
        [Tooltip("Terrain detail prototype index targeted by the random thinning editor utility.")]
        private int detailThinningPrototypeIndex;

        [SerializeField]
        [Range(0f, 100f)]
        [Tooltip("Percentage of the selected detail type removed when the thinning utility is explicitly run.")]
        private float detailThinningRemovalPercentage = 10f;

        [SerializeField]
        [Tooltip("Seed used for repeatable random thinning. Change this value to generate a different distribution.")]
        private int detailThinningRandomSeed = 12345;

        public MMOClassicGrassFoliageProfile FoliageProfile => foliageProfile;

        public float MaximumAllowedSlopeDegrees => foliageProfile != null
            ? foliageProfile.maximumDetailSlopeDegrees
            : MMOTerrainDetailSlopePolicy.DefaultMaximumSlopeDegrees;

        public void Configure(MMOClassicGrassFoliageProfile profile)
        {
            foliageProfile = profile;
        }
    }
}
