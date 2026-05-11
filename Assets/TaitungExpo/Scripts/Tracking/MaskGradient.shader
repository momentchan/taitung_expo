Shader "TaitungExpo/MaskGradient"
{
    Properties
    {
        _MainTex ("Mask", 2D) = "white" {}
        _GradientScale ("Gradient Scale", Float) = 1
        _MagnitudeScale ("Magnitude Scale", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Lighting Off
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;

            half _GradientScale;
            half _MagnitudeScale;

            // R-float / grayscale mask: sample red (other channels unused on scalar RTs)
            half sampleMask(float2 uv)
            {
                return tex2D(_MainTex, uv).r;
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 texel = _MainTex_TexelSize.xy;
                float tx = texel.x;
                float ty = texel.y;
                float2 uv = i.uv;

                float s00 = sampleMask(uv + float2(-tx, -ty));
                float s01 = sampleMask(uv + float2(0, -ty));
                float s02 = sampleMask(uv + float2(tx, -ty));
                float s10 = sampleMask(uv + float2(-tx, 0));
                float s12 = sampleMask(uv + float2(tx, 0));
                float s20 = sampleMask(uv + float2(-tx, ty));
                float s21 = sampleMask(uv + float2(0, ty));
                float s22 = sampleMask(uv + float2(tx, ty));

                float gx = -s00 + s02 - 2.0 * s10 + 2.0 * s12 - s20 + s22;
                float gy = -s00 - 2.0 * s01 - s02 + s20 + 2.0 * s21 + s22;

                // Signed gx, gy in RG so flat regions are (0,0) → black when viewed as RGB (no 0.5 neuter)
                half2 grad = half2(gx, gy) * (half)_GradientScale;
                half mag = saturate(length(grad) * (half)_MagnitudeScale);
                return half4(grad.xy, mag, 1);
            }
            ENDCG
        }
    }
}
