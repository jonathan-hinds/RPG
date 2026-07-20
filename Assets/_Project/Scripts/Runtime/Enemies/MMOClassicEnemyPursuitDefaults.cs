namespace RPGClone.Enemies
{
    /// <summary>
    /// Shared authoring baselines derived from WoW Classic creature movement and leash behavior.
    /// Enemy definitions remain the runtime source of truth and can override these values per creature.
    /// </summary>
    public static class MMOClassicEnemyPursuitDefaults
    {
        public const float ReferencePlayerRunSpeed = 7f;
        public const float ReferenceCreatureRunSpeed = 8f;
        public const float CreatureToPlayerRunSpeedRatio = ReferenceCreatureRunSpeed / ReferencePlayerRunSpeed;

        // The RPG Clone player runs at 7.25 units/second, so the Classic 8:7 relationship is 8.286.
        public const float ProjectPlayerRunSpeed = 7.25f;
        public const float StandardChaseSpeed = ProjectPlayerRunSpeed * CreatureToPlayerRunSpeedRatio;
        public const float FastBeastChaseSpeed = 8.7f;
        public const float StandardLeashGraceSeconds = 15f;
    }
}
