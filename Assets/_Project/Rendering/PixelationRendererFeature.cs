using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace RPGClone.Rendering
{
    public sealed class PixelationRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader shader;
        [SerializeField] private RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;

        private Material material;
        private PixelationPass pass;

        public override void Create()
        {
            shader ??= Shader.Find("Hidden/RPG Clone/Pixelation Post Process");

            if (shader == null)
            {
                pass = null;
                return;
            }

            CoreUtils.Destroy(material);
            material = CoreUtils.CreateEngineMaterial(shader);
            pass = new PixelationPass(material)
            {
                renderPassEvent = renderPassEvent,
                requiresIntermediateTexture = true
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (pass == null || material == null)
            {
                return;
            }

            CameraType cameraType = renderingData.cameraData.cameraType;
            if (cameraType != CameraType.Game && cameraType != CameraType.SceneView)
            {
                return;
            }

            PixelationVolume settings = VolumeManager.instance.stack.GetComponent<PixelationVolume>();
            if (settings == null || !settings.IsActive())
            {
                return;
            }

            pass.Setup(settings.pixelAmount.value);
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
            material = null;
            pass = null;
        }

        private sealed class PixelationPass : ScriptableRenderPass
        {
            private static readonly int PixelAmountId = Shader.PropertyToID("_PixelAmount");
            private const string TargetName = "_PixelationColor";

            private readonly Material material;
            private int pixelAmount = 4;

            public PixelationPass(Material material)
            {
                this.material = material;
                profilingSampler = new ProfilingSampler("Pixelation");
            }

            public void Setup(int pixelAmount)
            {
                this.pixelAmount = Mathf.Max(1, pixelAmount);
            }

            private sealed class PassData
            {
                public TextureHandle source;
                public Material material;
                public int pixelAmount;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (material == null || pixelAmount <= 1)
                {
                    return;
                }

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                {
                    return;
                }

                TextureHandle source = resourceData.cameraColor;
                TextureHandle destination = renderGraph.CreateTexture(source, TargetName);

                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>("Pixelation", out PassData passData, profilingSampler))
                {
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                    builder.UseTexture(source, AccessFlags.Read);

                    passData.source = source;
                    passData.material = material;
                    passData.pixelAmount = pixelAmount;

                    builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                    {
                        data.material.SetFloat(PixelAmountId, data.pixelAmount);

                        RTHandle sourceHandle = data.source;
                        Vector2 viewportScale = sourceHandle.useScaling
                            ? new Vector2(sourceHandle.rtHandleProperties.rtHandleScale.x, sourceHandle.rtHandleProperties.rtHandleScale.y)
                            : Vector2.one;

                        Blitter.BlitTexture(context.cmd, sourceHandle, viewportScale, data.material, 0);
                    });
                }

                resourceData.cameraColor = destination;
            }
        }
    }
}
