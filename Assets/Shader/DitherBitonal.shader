Shader "Unlit/DitherBitonal"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _MaskTex ("Dither Mask", 2D) = "white" {}

        [Header(Tone from source color)]
        _ShadowScale ("Dither Dark Scale", Range(0, 1)) = 0.35
        _HighlightScale ("Dither Light Scale", Range(0, 2)) = 1

        [Header(Noise threshold)]
        _FbmScale ("FBM UV Scale", Float) = 90
        _FbmTimeScale ("FBM Time Scale", Float) = 0
        _FbmPhase ("FBM Phase", Float) = 0
        _LumaBias ("Luma Bias", Range(-0.5, 0.5)) = 0
        _DitherMix ("Dither Mix (0 smooth, 1 hard)", Range(0, 1)) = 1
        _MaskWeight ("Mask Strength", Range(0, 1)) = 1
        [Header(Mask ramp)]
        _MaskRampMid ("Mask Mid (dither and thermal split)", Range(0.01, 0.99)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "../Packages/unity-gist/Cginc/Fbm.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float2 uvMask : TEXCOORD1;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _MaskTex;
            float4 _MaskTex_ST;

            half _ShadowScale;
            half _HighlightScale;
            float _FbmScale;
            float _FbmTimeScale;
            float _FbmPhase;
            half _LumaBias;
            half _DitherMix;
            half _MaskWeight;
            half _MaskRampMid;

            // Same palette as ThermalEffect.hlsl (luminance -> false color).
            half3 ThermalEffectRGB(half3 InColor)
            {
                half lum = dot(InColor, half3(0.299h, 0.587h, 0.114h));
                half3 c_black = half3(0, 0, 0);
                half3 c_blue = half3(0, 0.5, 1);
                half3 c_green = half3(0, 1, 0);
                half3 c_yellow = half3(1, 1, 0);
                half3 c_orange = half3(1, 0.5, 0);
                half3 c_red = half3(1, 0, 0);
                half3 color = c_black;
                color = lerp(color, c_blue, saturate(lum * 6.0));
                color = lerp(color, c_green, saturate((lum - 0.15) * 6.0));
                color = lerp(color, c_yellow, saturate((lum - 0.45) * 6.0));
                color = lerp(color, c_orange, saturate((lum - 0.7) * 6.0));
                return lerp(color, c_red, saturate((lum - 0.9) * 10.0));
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.uvMask = TRANSFORM_TEX(v.uv, _MaskTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                half4 samp = tex2D(_MainTex, i.uv);
                half3 rgb = samp.rgb;
                half lum01 = saturate(dot(rgb, half3(0.299h, 0.587h, 0.114h)) + _LumaBias);

                float f = fbm2(i.uv * _FbmScale, _Time.y * _FbmTimeScale + _FbmPhase);
                half n01 = saturate((half)f * 0.5h + 0.5h);

                // Light pixel where luminance exceeds the per-pixel noise threshold (organic dither).
                half hardBit = step(n01, lum01);
                half t = lerp(lum01, hardBit, saturate(_DitherMix));
                half3 darkC = rgb * _ShadowScale;
                half3 lightC = rgb * _HighlightScale;
                half3 dithered = lerp(darkC, lightC, t);

                half maskRaw = saturate(tex2D(_MaskTex, i.uvMask).r * _MaskWeight);
                half mid = saturate(_MaskRampMid);
                // First half of mask: original -> dithered. Second half: that result -> thermal(dithered).
                half toDither = saturate(maskRaw / mid);
                half toThermal = saturate((maskRaw - mid) / (1.0 - mid + 1e-4));
                half3 blended = lerp(rgb, dithered, toDither);
                half3 thermalOnDither = ThermalEffectRGB(dithered);
                half3 outRgb = lerp(blended, thermalOnDither, toThermal);

                return half4(outRgb, samp.a);
            }
            ENDCG
        }
    }
}
