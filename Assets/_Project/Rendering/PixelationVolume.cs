using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RPGClone.Rendering
{
    [Serializable]
    [VolumeComponentMenu("Post-processing/RPG Clone/Pixelation")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public sealed class PixelationVolume : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("Screen pixel block size. 1 disables the effect; higher values create chunkier indie-style pixels.")]
        public ClampedIntParameter pixelAmount = new(4, 1, 32);

        public bool IsActive()
        {
            return active && pixelAmount.value > 1;
        }

        [Obsolete("Unused by URP.")]
        public bool IsTileCompatible() => false;
    }
}
