namespace RPGClone.Vfx.Warrior
{
    /// <summary>
    /// Presentation-only contract for the Warrior Charge lifecycle.
    /// Gameplay and movement authority remain owned by MMOAbilitySystem.
    /// </summary>
    public interface IChargeVFX
    {
        bool IsPlaying { get; }
        bool IsRecovering { get; }
        void StopImmediate();
    }
}
