using UnityEngine;

namespace RPGClone.Vfx.Shaman
{
    [DisallowMultipleComponent]
    public sealed class LightningVFX : MonoBehaviour
    {
        [SerializeField] private LightningVFXProfile profile;
        [SerializeField] private GameObject castPrefab;
        [SerializeField] private GameObject beamPrefab;
        [SerializeField] private GameObject impactPrefab;
        [SerializeField] private GameObject aftermathPrefab;

        public LightningVFXProfile Profile => profile;
        public GameObject CastPrefab => castPrefab;
        public GameObject BeamPrefab => beamPrefab;
        public GameObject ImpactPrefab => impactPrefab;
        public GameObject AftermathPrefab => aftermathPrefab;

        public void Configure(
            LightningVFXProfile newProfile,
            GameObject newCastPrefab,
            GameObject newBeamPrefab,
            GameObject newImpactPrefab,
            GameObject newAftermathPrefab)
        {
            profile = newProfile;
            castPrefab = newCastPrefab;
            beamPrefab = newBeamPrefab;
            impactPrefab = newImpactPrefab;
            aftermathPrefab = newAftermathPrefab;
        }
    }
}
