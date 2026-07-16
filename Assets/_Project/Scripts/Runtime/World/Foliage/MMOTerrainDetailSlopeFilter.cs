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
