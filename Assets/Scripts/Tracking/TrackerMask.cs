using mj.gist;
using UnityEngine;

namespace TaitungExpo
{
    public class TrackerMask : MonoBehaviour
    {
        public RenderTexture Mask => mask;

        // Sobel gradient of Mask: signed d/dx,d/dy in RG, magnitude in B. Uses runtime RT if gradientOutput is unset.
        public RenderTexture MaskGradient => gradientOutput != null ? gradientOutput : ownedGradientOutput;

        [SerializeField] private float radius = 0.05f;
        [Tooltip("Multiply last frame mask by this each update before adding new tracker splats (0 clears trails, 1 keeps them indefinitely).")]
        [SerializeField] private float historyDecay = 0.92f;

        [SerializeField] private RenderTexture mask;
        [SerializeField] private Shader shader;

        [Header("Mask gradient")]
        [SerializeField] private bool computeMaskGradient = true;
        [SerializeField] private Shader maskGradientShader;
        [Tooltip("If unset, a render texture matching the mask is created at runtime.")]
        [SerializeField] private RenderTexture gradientOutput;
        [SerializeField] private float gradientScale = 1f;
        [SerializeField] private float gradientMagnitudeScale = 1f;

        private Material mat;
        private Material gradientMat;
        private PingPongRenderTexture pingPong;
        private RenderTexture ownedGradientOutput;

        void Awake()
        {
            mat = new Material(shader);
            if (maskGradientShader != null)
                gradientMat = new Material(maskGradientShader);
        }

        void OnDestroy()
        {
            DisposePingPong();
            ReleaseOwnedGradientOutput();

            if (mat != null)
                Destroy(mat);
            if (gradientMat != null)
                Destroy(gradientMat);
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

            if (computeMaskGradient && gradientMat != null)
            {
                EnsureGradientOutput();
                var target = gradientOutput != null ? gradientOutput : ownedGradientOutput;
                if (target != null)
                {
                    gradientMat.SetFloat("_GradientScale", gradientScale);
                    gradientMat.SetFloat("_MagnitudeScale", gradientMagnitudeScale);
                    Graphics.Blit(mask, target, gradientMat);
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

        void EnsureGradientOutput()
        {
            if (gradientOutput != null)
                return;

            if (ownedGradientOutput != null
                && ownedGradientOutput.width == mask.width
                && ownedGradientOutput.height == mask.height
                && ownedGradientOutput.format == mask.format)
                return;

            ReleaseOwnedGradientOutput();

            ownedGradientOutput = new RenderTexture(mask.width, mask.height, mask.depth, mask.format)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "TrackerMask_Gradient"
            };
            ownedGradientOutput.Create();
        }

        void ReleaseOwnedGradientOutput()
        {
            if (ownedGradientOutput == null) return;
            ownedGradientOutput.Release();
            Destroy(ownedGradientOutput);
            ownedGradientOutput = null;
        }

        void DisposePingPong()
        {
            if (pingPong == null) return;
            pingPong.Dispose();
            pingPong = null;
        }
    }
}
