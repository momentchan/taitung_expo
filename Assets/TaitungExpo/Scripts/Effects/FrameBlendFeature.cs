using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class FrameBlendFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        public Material blendMaterial;
        [Range(0f, 1f)]
        public float blendFactor = 0.9f;
    }

    public Settings settings = new Settings();
    private FrameBlendPass blendPass;

    public override void Create()
    {
        blendPass = new FrameBlendPass(settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.blendMaterial == null) return;
        
        // Only apply to the main Game camera
        if (renderingData.cameraData.cameraType != CameraType.Game) return;

        blendPass.ConfigureInput(ScriptableRenderPassInput.Color);
        renderer.EnqueuePass(blendPass);
    }

    protected override void Dispose(bool disposing)
    {
        blendPass?.Dispose();
    }

    // --- The Render Graph Pass ---
    class FrameBlendPass : ScriptableRenderPass
    {
        private FrameBlendFeature.Settings settings;
        private RTHandle[] pingPongRTs = new RTHandle[2];
        private int readIndex = 0;
        private int writeIndex = 1;

        // PassData holds references needed during the actual render execution
        private class PassData
        {
            public TextureHandle source;
            public TextureHandle historyRead;
            public Material material;
        }

        public FrameBlendPass(FrameBlendFeature.Settings settings)
        {
            this.settings = settings;
            this.renderPassEvent = settings.renderPassEvent;
        }

        // Initialize persistent RTHandles
        private void EnsureRTs(RenderTextureDescriptor desc)
        {
            desc.depthBufferBits = 0; // Color only

            for (int i = 0; i < 2; i++)
            {
                if (pingPongRTs[i] == null || pingPongRTs[i].rt.width != desc.width || pingPongRTs[i].rt.height != desc.height)
                {
                    if (pingPongRTs[i] != null) pingPongRTs[i].Release();
                    
                    pingPongRTs[i] = RTHandles.Alloc(
                        desc.width, desc.height, 1, 
                        DepthBits.None, desc.graphicsFormat, 
                        FilterMode.Bilinear, TextureWrapMode.Clamp, 
                        name: $"FrameBlend_RT_{i}"
                    );
                    
                    ClearRT(pingPongRTs[i]);
                }
            }
        }

        private void ClearRT(RTHandle handle)
        {
            var cmd = CommandBufferPool.Get();
            cmd.SetRenderTarget(handle);
            cmd.ClearRenderTarget(false, true, Color.black);
            Graphics.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            if (resourceData.isActiveTargetBackBuffer) return;

            // 1. Ensure our persistent RTHandles match the camera resolution
            EnsureRTs(cameraData.cameraTargetDescriptor);

            // 2. Prepare material properties
            settings.blendMaterial.SetFloat("_BlendFactor", settings.blendFactor);

            // 3. Import Ping-Pong buffers into the Render Graph
            TextureHandle historyReadHandle = renderGraph.ImportTexture(pingPongRTs[readIndex]);
            TextureHandle historyWriteHandle = renderGraph.ImportTexture(pingPongRTs[writeIndex]);
            
            // 4. Get the camera's active color target
            TextureHandle cameraColorTarget = resourceData.activeColorTexture;

            // --- PASS 1: Blend into History Write Buffer ---
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Frame Blend Pass", out var passData))
            {
                passData.source = cameraColorTarget;
                passData.historyRead = historyReadHandle;
                passData.material = settings.blendMaterial;

                // Declare inputs
                builder.UseTexture(passData.source, AccessFlags.Read);
                builder.UseTexture(passData.historyRead, AccessFlags.Read);
                
                // Declare output (Write directly to the next history buffer)
                builder.SetRenderAttachment(historyWriteHandle, 0, AccessFlags.Write);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    data.material.SetTexture("_HistoryTex", data.historyRead);
                    Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, 0);
                });
            }

            // --- PASS 2: Copy History Write Buffer back to Camera ---
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Frame Blend Copy Back", out var passData))
            {
                passData.source = historyWriteHandle;

                // Declare input
                builder.UseTexture(passData.source, AccessFlags.Read);
                
                // Declare output (Write back to the screen)
                builder.SetRenderAttachment(cameraColorTarget, 0, AccessFlags.Write);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    // A standard blit without a custom material performs a direct copy
                    Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), 0.0f, false);
                });
            }

            // 5. Swap indices for the next frame
            SwapIndices();
        }

        private void SwapIndices()
        {
            readIndex = (readIndex + 1) % 2;
            writeIndex = (writeIndex + 1) % 2;
        }

        public void Dispose()
        {
            for (int i = 0; i < 2; i++)
            {
                if (pingPongRTs[i] != null)
                {
                    pingPongRTs[i].Release();
                    pingPongRTs[i] = null;
                }
            }
        }
    }
}