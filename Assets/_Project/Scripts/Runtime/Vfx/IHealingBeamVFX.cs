using UnityEngine;

namespace RPGClone.Vfx.Healing
{
    /// <summary>
    /// Visual-only contract for a pooled, single-target healing beam.
    /// Gameplay systems own cast state and healing ticks; this contract only mirrors them visually.
    /// </summary>
    public interface IHealingBeamVFX
    {
        bool IsPlaying { get; }
        bool ReadyForPool { get; }

        void SetAttachmentPoints(Transform casterAttachment, Transform targetAttachment);
        void Play();
        void TriggerHealingTick();
        void Stop();
        void StopImmediate();
        void ResetForPool();
    }
}
