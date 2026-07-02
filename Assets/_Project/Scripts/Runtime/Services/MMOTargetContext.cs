using RPGClone.Characters;
using RPGClone.Combat;

namespace RPGClone.Services
{
    public readonly struct MMOTargetContext
    {
        public MMOTargetContext(MMOCharacterIdentity identity)
        {
            Identity = identity;
            Combatant = identity != null ? identity.GetComponent<MMOCombatant>() : null;
            IsPlayerCharacter = identity != null && MMOGameplaySessionService.Players.Contains(identity);
            IsLocalPlayer = identity != null && MMOGameplaySessionService.LocalPlayer.Identity == identity;
        }

        public MMOCharacterIdentity Identity { get; }
        public MMOCombatant Combatant { get; }
        public bool IsPlayerCharacter { get; }
        public bool IsLocalPlayer { get; }
        public bool IsValid => Identity != null;
    }
}
