using System;
using UnityEngine;

namespace RPGClone.Vfx.Fire
{
    public interface IFireballVFX
    {
        event Action<FireballVFX> Completed;

        bool IsPlaying { get; }
        bool ReadyForPool { get; }
        FireballVFXProfile Profile { get; }

        void SetCastPoint(Transform castPoint);
        void AttachToProjectile(Transform projectile);
        void PlayCasting();
        void ReleaseCasting();
        void PlayProjectile();
        void TriggerImpact(Vector3 position, Vector3 surfaceNormal);
        void StopImmediate();
        void ResetForPool();
    }
}
