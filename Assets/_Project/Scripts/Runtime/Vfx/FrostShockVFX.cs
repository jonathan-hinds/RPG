using UnityEngine;

namespace RPGClone.Vfx.Shaman
{
    /// <summary>Authoring marker for the complete Frost Shock cast/projectile/impact/debuff package.</summary>
    public sealed class FrostShockVFX : MonoBehaviour
    {
        [SerializeField] private FrostShockVFXProfile profile;
        [SerializeField] private GameObject castProjectilePrefab;
        [SerializeField] private GameObject impactSlowPrefab;
        [SerializeField] private GameObject expirationPrefab;

        public FrostShockVFXProfile Profile => profile;
        public GameObject CastProjectilePrefab => castProjectilePrefab;
        public GameObject ImpactSlowPrefab => impactSlowPrefab;
        public GameObject ExpirationPrefab => expirationPrefab;

        public void ConfigureAuthoring(FrostShockVFXProfile newProfile, GameObject newCastProjectilePrefab, GameObject newImpactSlowPrefab, GameObject newExpirationPrefab)
        {
            profile = newProfile;
            castProjectilePrefab = newCastProjectilePrefab;
            impactSlowPrefab = newImpactSlowPrefab;
            expirationPrefab = newExpirationPrefab;
        }
    }
}
