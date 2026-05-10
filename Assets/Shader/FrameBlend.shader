Shader "Hidden/URP/FrameBlend"
{
    Properties
    {
        _HistoryTex ("History Texture", 2D) = "black" {}
        _BlendFactor ("Base Blend Factor", Range(0, 1)) = 0.9
        [HideInInspector] _TrackerDepthMask ("Global Depth Mask", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_HistoryTex);
            SAMPLER(sampler_HistoryTex);

            TEXTURE2D(_TrackerDepthMask);
            SAMPLER(sampler_TrackerDepthMask);

            float _BlendFactor;

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 currentColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
                half4 historyColor = SAMPLE_TEXTURE2D(_HistoryTex, sampler_HistoryTex, input.texcoord);

                float maskValue = SAMPLE_TEXTURE2D(_TrackerDepthMask, sampler_TrackerDepthMask, input.texcoord).r;
                float effectiveBlend = _BlendFactor;

                return lerp(currentColor, historyColor, effectiveBlend);
            }
            ENDHLSL
        }
    }
}
