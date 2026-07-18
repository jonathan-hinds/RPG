namespace RPGClone.Vfx.Warrior
{
    public interface IBerzerkitisVFX
    {
        bool IsPlaying { get; }
        bool ReadyForPool { get; }
        void PulseHands();
        void FadeOut();
        void StopImmediate();
        void ResetForPool();
    }
}
