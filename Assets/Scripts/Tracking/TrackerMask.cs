using mj.gist;
using UnityEngine;

namespace TaitungExpo
{
    public class TrackerMask : MonoBehaviour
    {
        public RenderTexture Mask => mask;

        // Smoothed depth map for Parallax occlusion
        public RenderTexture MaskDepth => depthOutput != null ? depthOutput : ownedDepthOutput;

        [SerializeField] private float radius = 0.05f;
        [Tooltip("Multiply last frame mask by this each update before adding new tracker splats (0 clears trails, 1 keeps them indefinitely).")]
        [SerializeField] private float historyDecay = 0.92f;

        [SerializeField] private RenderTexture mask;
        [SerializeField] private Shader shader;

        [Header("Mask Depth (Bevel)")]
        [SerializeField] private bool computeMaskDepth = true;
        [SerializeField] private Shader maskDepthShader;
        [Tooltip("If unset, a render texture matching the mask is created at runtime.")]
        [SerializeField] private RenderTexture depthOutput;
        [SerializeField] private float blurRadius = 5f;
        [SerializeField] private float depthMultiplier = 5f;

        private Material mat;
        private Material depthMat;
        private PingPongRenderTexture pingPong;
        private RenderTexture ownedDepthOutput;

        void Awake()
        {
            mat = new Material(shader);
            if (maskDepthShader != null)
                depthMat = new Material(maskDepthShader);
        }

        void OnDestroy()
        {
            DisposePingPong();
            ReleaseOwnedDepthOutput();

            if (mat != null)
                Destroy(mat);
            if (depthMat != null)
                Destroy(depthMat);
        }

        void Update()
        {
            if (mask == null || shader == null || TrackerManager.Instance == null)
                return;

            EnsurePingPong();

            mat.SetFloat("_Radius", radius);
            mat.SetFloat("_HistoryDecay", Mathf.Clamp01(historyDecay));
            var cam = Camera.main;
            if (cam != null)
                mat.SetFloat("_Aspect", cam.aspect);
            mat.SetBuffer("_TrackerBuffer", TrackerManager.Instance.TrackerBuffer);
            mat.SetInt("_TrackerNum", TrackerManager.Instance.TotalTrackerNum);

            Graphics.Blit(pingPong.Read, pingPong.Write, mat);
            pingPong.Swap();
            Graphics.Blit(pingPong.Read, mask);

            if (computeMaskDepth && depthMat != null)
            {
                EnsureDepthOutput();
                var target = depthOutput != null ? depthOutput : ownedDepthOutput;
                if (target != null)
                {
                    depthMat.SetFloat("_BlurRadius", blurRadius);
                    depthMat.SetFloat("_DepthMultiplier", depthMultiplier);
                    Graphics.Blit(mask, target, depthMat);

                    // Global depth mask for fullscreen / render-graph passes (e.g. masked frame blend).
                    Shader.SetGlobalTexture("_TrackerDepthMask", target);
                }
            }
        }

        void EnsurePingPong()
        {
            if (pingPong != null && PingPongMatchesMaskFormat())
                return;

            DisposePingPong();

            pingPong = new PingPongRenderTexture(mask);
            Graphics.Blit(Texture2D.blackTexture, pingPong.Read);
            Graphics.Blit(Texture2D.blackTexture, pingPong.Write);
        }

        bool PingPongMatchesMaskFormat()
        {
            var r = pingPong.Read;
            return r != null && r.width == mask.width && r.height == mask.height && r.format == mask.format;
        }

        void EnsureDepthOutput()
        {
            if (depthOutput != null)
                return;

            if (ownedDepthOutput != null
                && ownedDepthOutput.width == mask.width
                && ownedDepthOutput.height == mask.height
                && ownedDepthOutput.format == mask.format)
                return;

            ReleaseOwnedDepthOutput();

            ownedDepthOutput = new RenderTexture(mask.width, mask.height, mask.depth, mask.format)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "TrackerMask_Depth"
            };
            ownedDepthOutput.Create();
        }

        void ReleaseOwnedDepthOutput()
        {
            if (ownedDepthOutput == null) return;
            ownedDepthOutput.Release();
            Destroy(ownedDepthOutput);
            ownedDepthOutput = null;
        }

        void DisposePingPong()
        {
            if (pingPong == null) return;
            pingPong.Dispose();
            pingPong = null;
        }
    }
}
