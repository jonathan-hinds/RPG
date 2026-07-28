namespace RPGClone.Enemies
{
    /// <summary>
    /// Baseline outdoor-creature pacing. Individual enemy definitions remain free to override
    /// this value for named, rare, instanced, or intentionally fast-respawning content.
    /// </summary>
    public static class MMOClassicRespawnDefaults
    {
        public const float StandardOutdoorSeconds = 300f;
    }
}
