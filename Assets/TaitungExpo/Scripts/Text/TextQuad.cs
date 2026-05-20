using UnityEngine;

namespace TaitungExpo
{
    /// <summary>
    /// Composites UI + bloom textures through <c>Unlit/TextQuad</c> into <see cref="output"/>,
    /// with optional frame-blend history.
    /// </summary>
    public class TextQuad : MonoBehaviour
    {
    [SerializeField] private Material material;
    [Tooltip("Composite (UI + blooms + optional frame blend) is written here each frame.")]
    [SerializeField] private RenderTexture output;

    [SerializeField] private Texture ui;
    [SerializeField] private Texture uiBloomSmall;
    [SerializeField] private Texture uiBloomLarge;

    [Header("Frame blend (text only)")]
    [Tooltip("Matches FrameBlendFeature: weight on previous frame (0 = off, high = long trails).")]
    [Range(0f, 1f)]
    [SerializeField] private float frameBlendFactor;

    private RenderTexture _history;
    private bool _historyValid;

    public RenderTexture Output => output;

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

        material.SetTexture("_MainTex", ui);
        if (uiBloomSmall != null)
            material.SetTexture("_BloomSmallTex", uiBloomSmall);
        if (uiBloomLarge != null)
            material.SetTexture("_BloomLargeTex", uiBloomLarge);

        material.SetFloat("_FrameBlendFactor", frameBlendFactor);
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
