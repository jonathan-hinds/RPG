using UnityEngine;

namespace RPGClone.Vfx.Warrior
{
    [DisallowMultipleComponent]
    public sealed class ThunderClapVFXPackage : MonoBehaviour
    {
        [SerializeField] private ThunderClapVFXProfile profile;
        [SerializeField] private GameObject castPrefab;
        [SerializeField] private GameObject impactPrefab;
        [SerializeField] private GameObject shockwavePrefab;
        [SerializeField] private GameObject targetReactionPrefab;
        [SerializeField] private GameObject aftermathPrefab;
        [SerializeField] private GameObject completePrefab;

        public ThunderClapVFXProfile Profile => profile;
        public GameObject CastPrefab => castPrefab;
        public GameObject ImpactPrefab => impactPrefab;
        public GameObject ShockwavePrefab => shockwavePrefab;
        public GameObject TargetReactionPrefab => targetReactionPrefab;
        public GameObject AftermathPrefab => aftermathPrefab;
        public GameObject CompletePrefab => completePrefab;

        public void Configure(
            ThunderClapVFXProfile newProfile,
            GameObject newCastPrefab,
            GameObject newImpactPrefab,
            GameObject newShockwavePrefab,
            GameObject newTargetReactionPrefab,
            GameObject newAftermathPrefab,
            GameObject newCompletePrefab)
        {
            profile = newProfile;
            castPrefab = newCastPrefab;
            impactPrefab = newImpactPrefab;
            shockwavePrefab = newShockwavePrefab;
            targetReactionPrefab = newTargetReactionPrefab;
            aftermathPrefab = newAftermathPrefab;
            completePrefab = newCompletePrefab;
        }
    }
}
