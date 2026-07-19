using UnityEngine;

namespace RPGClone.Vfx.Water
{
    public interface IWaterShieldVFX
    {
        bool IsPlaying { get; }
        bool ReadyForPool { get; }
        void ReactToAbsorb(Vector3 incomingDirection, int manaRestored);
        void Expire();
        void StopImmediate();
        void ResetForPool();
    }
}
