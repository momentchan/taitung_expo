using mj.gist;
using Klak.Hap;
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
        const string TextUnderStrengthProperty = "_TextUnderStrength";
        const string TextUnderDistanceMaskRangeProperty = "_TextUnderDistanceMaskRange";
        const float TextUnderDistanceMaskFixedY = 0.4f;

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
            bloomSmallStrength.DoGUISlider(0f, 4f, "Bloom Small Strength");
            bloomLargeStrength.DoGUISlider(0f, 4f, "Bloom Large Strength");
            textUnderStrength.DoGUISlider(0f, 2f, "Text Under Strength");

            GUILayout.Label("HDR Tint");
            hdrTintWhite.DoGUISlider(Vector4.zero, new Vector4(12f, 12f, 12f, 1f), "HDR Tint White");
            hdrTintRed.DoGUISlider(Vector4.zero, new Vector4(12f, 12f, 12f, 1f), "HDR Tint Red");
            hdrTintOrange.DoGUISlider(Vector4.zero, new Vector4(12f, 12f, 12f, 1f), "HDR Tint Orange");
            hdrTintYellow.DoGUISlider(Vector4.zero, new Vector4(12f, 12f, 12f, 1f), "HDR Tint Yellow");
            hdrTintRedWidth.DoGUISlider(0.02f, 0.5f, "HDR Tint Red Width");
            hdrTintYellowStart.DoGUISlider(0.1f, 1f, "HDR Tint Yellow Start");
            hdrTintRadiusScale.DoGUISlider(0f, 4f, "HDR Tint Radius Scale");
            hdrTintNoiseScale.DoGUISlider(0.1f, 20f, "HDR Tint Noise Scale");
            hdrTintNoiseStrength.DoGUISlider(0f, 1f, "HDR Tint Noise Strength");
            hdrTintDiagonalStrength.DoGUISlider(0f, 1f, "HDR Tint Diagonal Strength");
            hdrTintRadiusStrength.DoGUISlider(0f, 1f, "HDR Tint Radius Strength");
            hdrTintBaseOffset.DoGUISlider(0f, 1f, "HDR Tint Base Offset");
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
            bloomSmallStrength = new PrefsFloat($"{GetName()}_bloomSmallStrength", 1f);
            bloomLargeStrength = new PrefsFloat($"{GetName()}_bloomLargeStrength", 1f);
            textUnderStrength = new PrefsFloat($"{GetName()}_textUnderStrength", DefaultFloat(TextUnderStrengthProperty, 1f));
            hdrTintWhite = new PrefsVector4($"{GetName()}_hdrTintWhite", DefaultColorVector("_HdrTintWhite", new Color(5f, 5f, 5f, 1f)));
            hdrTintRed = new PrefsVector4($"{GetName()}_hdrTintRed", DefaultColorVector("_HdrTint1", new Color(4f, 0.35f, 0f, 1f)));
            hdrTintOrange = new PrefsVector4($"{GetName()}_hdrTintOrange", DefaultColorVector("_HdrTint2", new Color(7f, 2.25f, 0f, 1f)));
            hdrTintYellow = new PrefsVector4($"{GetName()}_hdrTintYellow", DefaultColorVector("_HdrTint3", new Color(7f, 5.5f, 0.35f, 1f)));
            hdrTintRedWidth = new PrefsFloat($"{GetName()}_hdrTintRedWidth", DefaultFloat("_HdrTintRedWidth", 0.12f));
            hdrTintYellowStart = new PrefsFloat($"{GetName()}_hdrTintYellowStart", DefaultFloat("_HdrTintYellowStart", 0.68f));
            hdrTintRadiusScale = new PrefsFloat($"{GetName()}_hdrTintRadiusScale", DefaultFloat("_HdrTintRadiusScale", 1.85f));
            hdrTintNoiseScale = new PrefsFloat($"{GetName()}_hdrTintNoiseScale", DefaultFloat("_HdrTintNoiseScale", 4.8f));
            hdrTintNoiseStrength = new PrefsFloat($"{GetName()}_hdrTintNoiseStrength", DefaultFloat("_HdrTintNoiseStrength", 0.62f));
            hdrTintDiagonalStrength = new PrefsFloat($"{GetName()}_hdrTintDiagonalStrength", DefaultFloat("_HdrTintDiagonalStrength", 0.52f));
            hdrTintRadiusStrength = new PrefsFloat($"{GetName()}_hdrTintRadiusStrength", DefaultFloat("_HdrTintRadiusStrength", 0.22f));
            hdrTintBaseOffset = new PrefsFloat($"{GetName()}_hdrTintBaseOffset", DefaultFloat("_HdrTintBaseOffset", 0.18f));
        }

        #endregion


        [SerializeField] private Material material;
        [Tooltip("Composite (UI + blooms + optional frame blend) is written here each frame.")]
        [SerializeField] private RenderTexture output;
        [SerializeField] private Texture ui;
        [SerializeField] [Range(0f, 1f)] private float hdrTintBlend;
        [SerializeField] [Range(0f, 1f)] private float depthInteractionRatio = 1f;

        [Header("Fullscreen transition HAP effect")]
        [Tooltip("Restarts when SongManager enters Transition mode.")]
        [SerializeField] private HapPlayer transitionEffectPlayer;

        [Header("Frame blend (text only)")]
        [Tooltip("Matches FrameBlendFeature: weight on previous frame (0 = off, high = long trails).")]

        private RenderTexture _history;
        private bool _historyValid;
        private SongManager _songManager;
        private global::LyricRingsPrefabMarker _lyricRingsMarker;

        public RenderTexture Output => output;

        private PrefsFloat frameBlendFactor;
        private PrefsFloat depthRangeMin;
        private PrefsFloat depthRangeMax;
        private PrefsFloat uvDistortStrength;
        private PrefsFloat uvDistortNoDepthStrength;
        private PrefsFloat uvDistortFbmScale;
        private PrefsFloat uvDistortFbmTime;
        private PrefsFloat bloomSmallStrength;
        private PrefsFloat bloomLargeStrength;
        private PrefsFloat textUnderStrength;
        private PrefsVector4 hdrTintWhite;
        private PrefsVector4 hdrTintRed;
        private PrefsVector4 hdrTintOrange;
        private PrefsVector4 hdrTintYellow;
        private PrefsFloat hdrTintRedWidth;
        private PrefsFloat hdrTintYellowStart;
        private PrefsFloat hdrTintRadiusScale;
        private PrefsFloat hdrTintNoiseScale;
        private PrefsFloat hdrTintNoiseStrength;
        private PrefsFloat hdrTintDiagonalStrength;
        private PrefsFloat hdrTintRadiusStrength;
        private PrefsFloat hdrTintBaseOffset;

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

        void Start()
        {
            BindSongManager();
        }

        void OnEnable()
        {
            BindSongManager();
        }

        void OnDestroy()
        {
            UnbindSongManager();
            ReleaseHistory();
        }

        void OnDisable()
        {
            UnbindSongManager();
            ReleaseHistory();
        }

        void Update()
        {
            if (_songManager == null)
                BindSongManager();

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
            material.SetFloat("_BloomSmallStrength", bloomSmallStrength);
            material.SetFloat("_BloomLargeStrength", bloomLargeStrength);
            SetFloat(TextUnderStrengthProperty, textUnderStrength);
            ApplyHdrTintPrefs();
            material.SetFloat("_HdrTintBlend", hdrTintBlend);
            ApplyTextUnderDistanceMaskRange();
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

        void BindSongManager()
        {
            if (_songManager != null)
                return;

            var manager = FindFirstObjectByType<SongManager>();
            if (manager == null)
                return;

            manager.OnPlaybackModeChanged -= OnPlaybackModeChanged;
            manager.OnPlaybackModeChanged += OnPlaybackModeChanged;
            _songManager = manager;
        }

        void UnbindSongManager()
        {
            if (_songManager == null)
                return;

            _songManager.OnPlaybackModeChanged -= OnPlaybackModeChanged;
            _songManager = null;
        }

        void OnPlaybackModeChanged(SongPlaybackMode mode)
        {
            ApplyTextUnderDistanceMaskRange();

            if (mode == SongPlaybackMode.Transition)
                RestartTransitionEffect();
        }

        void RestartTransitionEffect()
        {
            if (transitionEffectPlayer == null)
                return;

            transitionEffectPlayer.enabled = true;
            transitionEffectPlayer.time = 0f;
            transitionEffectPlayer.UpdateNow();
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

        void ApplyHdrTintPrefs()
        {
            SetColor("_HdrTintWhite", hdrTintWhite);
            SetColor("_HdrTint1", hdrTintRed);
            SetColor("_HdrTint2", hdrTintOrange);
            SetColor("_HdrTint3", hdrTintYellow);
            SetFloat("_HdrTintRedWidth", hdrTintRedWidth);
            SetFloat("_HdrTintYellowStart", hdrTintYellowStart);
            SetFloat("_HdrTintRadiusScale", hdrTintRadiusScale);
            SetFloat("_HdrTintNoiseScale", hdrTintNoiseScale);
            SetFloat("_HdrTintNoiseStrength", hdrTintNoiseStrength);
            SetFloat("_HdrTintDiagonalStrength", hdrTintDiagonalStrength);
            SetFloat("_HdrTintRadiusStrength", hdrTintRadiusStrength);
            SetFloat("_HdrTintBaseOffset", hdrTintBaseOffset);
        }

        void ApplyTextUnderDistanceMaskRange()
        {
            if (material == null || !material.HasProperty(TextUnderDistanceMaskRangeProperty))
                return;

            float x = TextUnderMaskXForRingCount(ResolveLyricRingCount());
            material.SetVector(
                TextUnderDistanceMaskRangeProperty,
                new Vector4(x, TextUnderDistanceMaskFixedY, 0f, 0f));
        }

        int ResolveLyricRingCount()
        {
            int markerCount = ResolveActiveLyricRingCount();
            if (markerCount > 0)
                return markerCount;

            Song song = _songManager != null ? _songManager.CurrentSong : null;
            if (song == null)
                return 0;

            string[] lyricLines = song.GetLyricLines();
            return lyricLines != null ? lyricLines.Length : 0;
        }

        int ResolveActiveLyricRingCount()
        {
            if (_lyricRingsMarker == null)
                _lyricRingsMarker = FindFirstObjectByType<global::LyricRingsPrefabMarker>();

            return _lyricRingsMarker != null && _lyricRingsMarker.LyricTexts != null
                ? _lyricRingsMarker.LyricTexts.Length
                : 0;
        }

        static float TextUnderMaskXForRingCount(int lyricRingCount)
        {
            if (lyricRingCount <= 0)
                return TextUnderDistanceMaskFixedY;

            int clampedCount = Mathf.Clamp(lyricRingCount, 1, 6);
            return 1f - clampedCount * 0.1f;
        }

        void SetColor(string propertyName, PrefsVector4 prefs)
        {
            if (material == null || prefs == null || !material.HasProperty(propertyName))
                return;

            Vector4 v = prefs.Get();
            material.SetColor(propertyName, new Color(v.x, v.y, v.z, v.w));
        }

        void SetFloat(string propertyName, PrefsFloat prefs)
        {
            if (material == null || prefs == null || !material.HasProperty(propertyName))
                return;

            material.SetFloat(propertyName, prefs.Get());
        }

        Vector4 DefaultColorVector(string propertyName, Color fallback)
        {
            Color color = material != null && material.HasProperty(propertyName)
                ? material.GetColor(propertyName)
                : fallback;

            return new Vector4(color.r, color.g, color.b, color.a);
        }

        float DefaultFloat(string propertyName, float fallback)
        {
            return material != null && material.HasProperty(propertyName)
                ? material.GetFloat(propertyName)
                : fallback;
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
