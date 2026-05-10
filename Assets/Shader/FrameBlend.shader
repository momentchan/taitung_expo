Shader "Hidden/URP/FrameBlend"
{
    Properties
    {
        // 移除了 _MainTex，因為 Blitter 會自動在底層處理
        _HistoryTex ("History Texture", 2D) = "black" {}
        _BlendFactor ("Blend Factor", Range(0, 1)) = 0.9
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

            // Previous frame (assigned by our C# script)
            TEXTURE2D(_HistoryTex); 
            SAMPLER(sampler_HistoryTex);

            float _BlendFactor;

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // 核心修正：使用 _BlitTexture 與 sampler_LinearClamp
                // 這是 URP Blitter API 專用的讀取方式
                half4 currentColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
                
                // 讀取我們自己儲存的歷史畫面
                half4 historyColor = SAMPLE_TEXTURE2D(_HistoryTex, sampler_HistoryTex, input.texcoord);

                // 混合兩者
                return lerp(currentColor, historyColor, _BlendFactor);
            }
            ENDHLSL
        }
    }
}