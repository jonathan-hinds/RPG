namespace RPGClone.Characters
{
    public interface IMMOHostileActionReceiver
    {
        bool CanReceiveHostileActions { get; }
    }

    public enum MMOEntityFaction
    {
        Neutral = 0,
        Player = 1,
        Friendly = 2,
        Hostile = 3
    }

    public static class MMOFactionRules
    {
        public static bool CanDamage(MMOCharacterIdentity source, MMOCharacterIdentity target)
        {
            if (source == null || target == null || source == target)
            {
                return false;
            }

            IMMOHostileActionReceiver receiver = target.GetComponent<IMMOHostileActionReceiver>();
            if (receiver != null && !receiver.CanReceiveHostileActions)
            {
                return false;
            }

            return AreHostile(source.Faction, target.Faction)
                || RPGClone.PlayerInteraction.MMOPlayerInteractionService.CanPlayersDamageEachOther(source, target);
        }

        public static bool CanAssist(MMOCharacterIdentity source, MMOCharacterIdentity target)
        {
            if (source == null || target == null)
            {
                return false;
            }

            return source == target
                || (!RPGClone.PlayerInteraction.MMOPlayerInteractionService.CanPlayersDamageEachOther(source, target)
                    && !AreHostile(source.Faction, target.Faction));
        }

        public static bool AreHostile(MMOEntityFaction left, MMOEntityFaction right)
        {
            if (left == MMOEntityFaction.Neutral || right == MMOEntityFaction.Neutral)
            {
                return false;
            }

            return left != right
                && (left == MMOEntityFaction.Hostile || right == MMOEntityFaction.Hostile);
        }
    }
}
