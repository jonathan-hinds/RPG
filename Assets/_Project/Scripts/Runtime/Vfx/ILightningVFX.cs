namespace RPGClone.Vfx.Shaman
{
    public interface ILightningVFX
    {
        bool IsPlaying { get; }
        LightningVFXProfile Profile { get; }
        void StopImmediate();
    }
}
