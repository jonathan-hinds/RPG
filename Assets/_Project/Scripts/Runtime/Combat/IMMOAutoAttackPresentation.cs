using RPGClone.Abilities;
using RPGClone.Characters;

namespace RPGClone.Combat
{
    public interface IMMOAutoAttackPresentation
    {
        float GetAutoAttackLeadSeconds(float swingDurationSeconds);

        void NotifyAutoAttackWindup(
            MMOAutoAttackController controller,
            MMOAbilityDefinition ability,
            MMOCharacterIdentity target,
            float swingDurationSeconds,
            float impactTime);
    }
}
