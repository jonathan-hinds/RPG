namespace RPGClone.Vfx
{
    /// <summary>
    /// Optional lifecycle hook for casting VFX that need to finish procedural animation before destruction.
    /// </summary>
    public interface IMMOAbilityVfxReleaseHandler
    {
        void Release(bool immediate);
    }
}
