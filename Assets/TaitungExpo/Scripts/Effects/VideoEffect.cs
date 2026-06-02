using mj.gist;
using PrefsGUI;
using PrefsGUI.RapidGUI;
using UnityEngine;

namespace TaitungExpo
{
    /// <summary>
    /// Drives <c>Unlit/DitherBitonal</c> material floats via PrefsGUI (textures stay on the material).
    /// </summary>
    public class VideoEffect : MonoBehaviour, IGUIUser
    {
        #region IGUIUser

        public string GetName() => "VideoEffect";

        public void ShowGUI()
        {
            depthRangeMin.DoGUISlider(0f, 1f, "Depth Range Min");
            depthRangeMax.DoGUISlider(0f, 1f, "Depth Range Max");
            shadowScale.DoGUISlider(0f, 1f, "Dither Dark Scale");
            highlightScale.DoGUISlider(0f, 2f, "Dither Light Scale");
            fbmScale.DoGUISlider(0f, 200f, "FBM UV Scale");
            fbmTimeScale.DoGUISlider(0f, 10f, "FBM Time Scale");
            fbmPhase.DoGUISlider(-10f, 10f, "FBM Phase");
            lumaBias.DoGUISlider(-0.5f, 0.5f, "Luma Bias");
            ditherMix.DoGUISlider(0f, 1f, "Dither Mix");
            depthSplit.DoGUISlider(0.01f, 0.99f, "Dither / Thermal Split");
        }

        public void SetupGUI()
        {
            depthRangeMin = new PrefsFloat($"{GetName()}_depthRangeMin", 0.371f);
            depthRangeMax = new PrefsFloat($"{GetName()}_depthRangeMax", 1f);
            shadowScale = new PrefsFloat($"{GetName()}_shadowScale", 0.052f);
            highlightScale = new PrefsFloat($"{GetName()}_highlightScale", 1.47f);
            fbmScale = new PrefsFloat($"{GetName()}_fbmScale", 195f);
            fbmTimeScale = new PrefsFloat($"{GetName()}_fbmTimeScale", 1.7f);
            fbmPhase = new PrefsFloat($"{GetName()}_fbmPhase", 0f);
            lumaBias = new PrefsFloat($"{GetName()}_lumaBias", -0.124f);
            ditherMix = new PrefsFloat($"{GetName()}_ditherMix", 1f);
            depthSplit = new PrefsFloat($"{GetName()}_depthSplit", 0.5f);
        }

        #endregion

        [SerializeField] private Material material;

        private PrefsFloat depthRangeMin;
        private PrefsFloat depthRangeMax;
        private PrefsFloat shadowScale;
        private PrefsFloat highlightScale;
        private PrefsFloat fbmScale;
        private PrefsFloat fbmTimeScale;
        private PrefsFloat fbmPhase;
        private PrefsFloat lumaBias;
        private PrefsFloat ditherMix;
        private PrefsFloat depthSplit;

        void Update()
        {
            if (material == null)
                return;

            material.SetFloat("_DepthRangeMin", depthRangeMin);
            material.SetFloat("_DepthRangeMax", depthRangeMax);
            material.SetFloat("_ShadowScale", shadowScale);
            material.SetFloat("_HighlightScale", highlightScale);
            material.SetFloat("_FbmScale", fbmScale);
            material.SetFloat("_FbmTimeScale", fbmTimeScale);
            material.SetFloat("_FbmPhase", fbmPhase);
            material.SetFloat("_LumaBias", lumaBias);
            material.SetFloat("_DitherMix", ditherMix);
            material.SetFloat("_DepthSplit", depthSplit);
        }
    }
}
