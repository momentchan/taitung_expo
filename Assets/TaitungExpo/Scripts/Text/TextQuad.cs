using mj.gist;
using UnityEngine;
using PrefsGUI;
using PrefsGUI.RapidGUI;


namespace TaitungExpo
{
    /// <summary>
    /// Composites UI + bloom textures through <c>Unlit/TextQuad</c> into <see cref="output"/>,
    /// with optional frame-blend history.
    /// </summary>
    public class TextQuad : MonoBehaviour, IGUIUser
    {
        #region IGUIUser
        public string GetName() => "TextEffect";

        public void ShowGUI()
        {
            frameBlendFactor.DoGUISlider(0f, 1f, "Frame Blend Factor");
            depthRangeMin.DoGUISlider(0f, 1f, "Depth Range Min");
            depthRangeMax.DoGUISlider(0f, 1f, "Depth Range Max");
            uvDistortStrength.DoGUISlider(0f, 1f, "UV Distort Strength");
            uvDistortNoDepthStrength.DoGUISlider(0f, 1f, "No Depth Distort Strength");
            uvDistortFbmScale.DoGUISlider(0f, 50f, "Distort FBM Scale");
            uvDistortFbmTime.DoGUISlider(0f, 5f, "Distort FBM Time");
        }

        public void SetupGUI()
        {
            frameBlendFactor = new PrefsFloat($"{GetName()}_frameBlendFactor", 0.918f);
            depthRangeMin = new PrefsFloat($"{GetName()}_depthRangeMin", 0.32f);
            depthRangeMax = new PrefsFloat($"{GetName()}_depthRangeMax", 1f);
            uvDistortStrength = new PrefsFloat($"{GetName()}_uvDistortStrength", 0.2f);
            uvDistortNoDepthStrength = new PrefsFloat($"{GetName()}_uvDistortNoDepthStrength", 0.05f);
            uvDistortFbmScale = new PrefsFloat($"{GetName()}_uvDistortFbmScale", 2f);
            uvDistortFbmTime = new PrefsFloat($"{GetName()}_uvDistortFbmTime", 0.2f);
        }

        #endregion


        [SerializeField] private Material material;
        [Tooltip("Composite (UI + blooms + optional frame blend) is written here each frame.")]
        [SerializeField] private RenderTexture output;
        [SerializeField] private Texture ui;
        [SerializeField] [Range(0f, 1f)] private float hdrTintBlend;
        [SerializeField] [Range(0f, 1f)] private float depthInteractionRatio = 1f;

        [Header("Frame blend (text only)")]
        [Tooltip("Matches FrameBlendFeature: weight on previous frame (0 = off, high = long trails).")]

        private RenderTexture _history;
        private bool _historyValid;

        public RenderTexture Output => output;

        private PrefsFloat frameBlendFactor;
        private PrefsFloat depthRangeMin;
        private PrefsFloat depthRangeMax;
        private PrefsFloat uvDistortStrength;
        private PrefsFloat uvDistortNoDepthStrength;
        private PrefsFloat uvDistortFbmScale;
        private PrefsFloat uvDistortFbmTime;

        public float HdrTintBlend => hdrTintBlend;
        public float DepthInteractionRatio => depthInteractionRatio;

        public void SetHdrTintBlend(float blend)
        {
            hdrTintBlend = Mathf.Clamp01(blend);
            if (material != null)
                material.SetFloat("_HdrTintBlend", hdrTintBlend);
        }

        public void SetDepthInteractionRatio(float ratio)
        {
            depthInteractionRatio = Mathf.Clamp01(ratio);
            if (material != null)
                material.SetFloat("_DepthInteractionRatio", depthInteractionRatio);
        }

        void OnDestroy()
        {
            ReleaseHistory();
        }

        void OnDisable()
        {
            ReleaseHistory();
        }

        void Update()
        {
            if (material == null || output == null || ui == null)
                return;

            EnsureHistoryMatchesOutput();

            material.SetFloat("_FrameBlendFactor", frameBlendFactor);
            material.SetFloat("_DepthRangeMin", depthRangeMin);
            material.SetFloat("_DepthRangeMax", depthRangeMax);
            material.SetFloat("_DepthInteractionRatio", depthInteractionRatio);
            material.SetFloat("_UvDistortStrength", uvDistortStrength);
            material.SetFloat("_UvDistortNoDepthStrength", uvDistortNoDepthStrength);
            material.SetFloat("_UvDistortFbmScale", uvDistortFbmScale);
            material.SetFloat("_UvDistortFbmTime", uvDistortFbmTime);
            material.SetFloat("_HdrTintBlend", hdrTintBlend);
            material.SetFloat("_FrameBlendHistoryValid", _historyValid && _history != null ? 1f : 0f);

            if (_history != null)
                material.SetTexture("_HistoryTex", _history);

            Graphics.Blit(ui, output, material);

            if (_history != null && frameBlendFactor > 0f)
            {
                Graphics.Blit(output, _history);
                _historyValid = true;
            }
            else
            {
                _historyValid = false;
            }
        }

        void EnsureHistoryMatchesOutput()
        {
            if (output == null)
                return;

            if (frameBlendFactor <= 0f)
            {
                ReleaseHistory();
                return;
            }

            var desc = output.descriptor;
            desc.depthBufferBits = 0;
            if (_history != null &&
                _history.width == desc.width &&
                _history.height == desc.height &&
                _history.graphicsFormat == desc.graphicsFormat)
                return;

            ReleaseHistory();
            _history = new RenderTexture(desc);
            _history.name = "TextQuad_FrameHistory";
            _history.Create();
            ClearRenderTarget(_history, Color.clear);
            _historyValid = false;
        }

        static void ClearRenderTarget(RenderTexture rt, Color clearColor)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(false, true, clearColor);
            RenderTexture.active = prev;
        }

        void ReleaseHistory()
        {
            if (_history != null)
            {
                _history.Release();
                Destroy(_history);
                _history = null;
            }
            _historyValid = false;
        }
    }
}
