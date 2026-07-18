using UnityEngine;

namespace RPGClone.Vfx.Arcane
{
    public interface IMageArmorApplyVFX
    {
        bool IsPlaying { get; }
        bool ReadyForPool { get; }

        void Play(Transform caster, Transform torsoAttachment = null);
        void StopImmediate();
        void ResetForPool();
    }
}
