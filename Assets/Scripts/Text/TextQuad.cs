using UnityEngine;

public class TextQuad : MonoBehaviour
{
    [SerializeField] private Material material;
    [Tooltip("Composite (UI + blooms) is written here each frame.")]
    [SerializeField] private RenderTexture output;

    [SerializeField] private Texture ui;
    [SerializeField] private Texture uiBloomSmall;
    [SerializeField] private Texture uiBloomLarge;

    public RenderTexture Output => output;

    void Update()
    {
        if (material == null || output == null || ui == null)
            return;

        material.SetTexture("_MainTex", ui);
        if (uiBloomSmall != null)
            material.SetTexture("_BloomSmallTex", uiBloomSmall);
        if (uiBloomLarge != null)
            material.SetTexture("_BloomLargeTex", uiBloomLarge);

        Graphics.Blit(ui, output, material);
    }
}
