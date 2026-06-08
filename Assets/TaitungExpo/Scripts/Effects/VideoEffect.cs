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
            depthSplit.DoGUISlider(0.01f, 0.99f, "Dither / Thermal Split");
            scatterScale.DoGUISlider(0f, 2000f, "Scatter Noise Scale");
            scatterRadius.DoGUISlider(0f, 0.5f, "Scatter Radius");
            ditherBoost.DoGUISlider(0f, 4f, "Dither Color Boost");
        }

        public void SetupGUI()
        {
            depthRangeMin = new PrefsFloat($"{GetName()}_depthRangeMin", 0.371f);
            depthRangeMax = new PrefsFloat($"{GetName()}_depthRangeMax", 1f);
            depthSplit = new PrefsFloat($"{GetName()}_depthSplit", 0.5f);
            scatterScale = new PrefsFloat($"{GetName()}_scatterScale", 500f);
            scatterRadius = new PrefsFloat($"{GetName()}_scatterRadius", 0.1f);
            ditherBoost = new PrefsFloat($"{GetName()}_ditherBoost", 2f);
        }

        #endregion

        [SerializeField] private Material material;
        [SerializeField] [Range(0f, 1f)] private float outputRatio = 1f;

        PrefsFloat depthRangeMin;
        PrefsFloat depthRangeMax;
        PrefsFloat depthSplit;
        PrefsFloat scatterScale;
        PrefsFloat scatterRadius;
        PrefsFloat ditherBoost;

        public float OutputRatio => outputRatio;

        public void SetRatio(float ratio)
        {
            outputRatio = Mathf.Clamp01(ratio);
            if (material != null)
                material.SetFloat("_Ratio", outputRatio);
        }

        void Update()
        {
            if (material == null)
                return;

            material.SetFloat("_DepthRangeMin", depthRangeMin);
            material.SetFloat("_DepthRangeMax", depthRangeMax);
            material.SetFloat("_DepthSplit", depthSplit);
            material.SetFloat("_ScatterScale", scatterScale);
            material.SetFloat("_ScatterRadius", scatterRadius);
            material.SetFloat("_DitherBoost", ditherBoost);
            material.SetFloat("_Ratio", outputRatio);
        }
    }
}
