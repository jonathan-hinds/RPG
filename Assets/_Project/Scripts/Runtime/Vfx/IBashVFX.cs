using System;
using UnityEngine;

namespace RPGClone.Vfx.Warrior
{
    public interface IBashVFX
    {
        event Action<BashVFX> Completed;
        bool IsPlaying { get; }
        bool ReadyForPool { get; }
        void Play(bool stunApplied);
        void SetImpactDirection(Vector3 direction);
        void StopImmediate();
        void ResetForPool();
    }
}
