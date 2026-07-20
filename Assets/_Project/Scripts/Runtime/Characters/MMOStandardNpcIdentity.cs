using UnityEngine;

namespace RPGClone.Characters
{
    [RequireComponent(typeof(MMOCharacterIdentity))]
    public sealed class MMOStandardNpcIdentity : MonoBehaviour
    {
        [SerializeField] private MMOCharacterProfile profile;
        [SerializeField] private MMONpcIdentityRole role = MMONpcIdentityRole.Friendly;
        [SerializeField] private string displayNameOverride;
        [SerializeField] private string titleOverride;
        [SerializeField] private bool applyOnAwake = true;

        private MMOCharacterIdentity identity;

        public MMOCharacterIdentity Identity
        {
            get
            {
                EnsureReference();
                return identity;
            }
        }

        public MMOCharacterProfile Profile => profile;
        public MMONpcIdentityRole Role => role;
        public string DisplayName => string.IsNullOrWhiteSpace(displayNameOverride) ? gameObject.name : displayNameOverride;
        public string Title => string.IsNullOrWhiteSpace(titleOverride) ? MMONpcIdentityStandards.GetDefaultTitle(role) : titleOverride;

        private void Awake()
        {
            if (applyOnAwake)
            {
                Apply(true);
            }
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                EnsureReference();
            }
        }

        public void Configure(MMOCharacterProfile newProfile, string newDisplayName, MMONpcIdentityRole newRole, bool resetResources)
        {
            Configure(newProfile, newDisplayName, MMONpcIdentityStandards.GetDefaultTitle(newRole), newRole, resetResources);
        }

        public void Configure(MMOCharacterProfile newProfile, string newDisplayName, string newTitle, MMONpcIdentityRole newRole, bool resetResources)
        {
            profile = newProfile;
            displayNameOverride = string.IsNullOrWhiteSpace(newDisplayName) ? gameObject.name : newDisplayName;
            titleOverride = newTitle;
            role = newRole;
            Apply(resetResources);
        }

        public void SetDisplayName(string newDisplayName, bool resetResources = false)
        {
            displayNameOverride = string.IsNullOrWhiteSpace(newDisplayName) ? gameObject.name : newDisplayName;
            Apply(resetResources);
        }

        public void SetTitle(string newTitle)
        {
            titleOverride = newTitle;
        }

        public void Apply(bool resetResources)
        {
            EnsureReference();
            MMONpcIdentityStandards.Apply(identity, profile, DisplayName, role, resetResources);
        }

        private void EnsureReference()
        {
            if (identity == null)
            {
                identity = GetComponent<MMOCharacterIdentity>();
            }
        }
    }
}
