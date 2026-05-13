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
            #include "../../Packages/unity-gist/Cginc/Fbm.cginc"

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

            // High-contrast 10-stop thermal (sync with ThermalEffect.hlsl).
            half3 ThermalEffectRGB(half3 InColor)
            {
                // High-contrast luminance (Rec. 709 luma weights)
                half lum = dot(InColor, half3(0.299h, 0.587h, 0.114h));

                // Contrast mapping: widens mid-tone bands along the ramp
                half k = 0.15h;
                half t = (lum / (k + max(lum, 1e-5h))) * (k + 1.0h);
                t = saturate(pow(t, 0.80h));

                // 10 colors — image_0 reference palette
                const half3 c0  = half3(0.00h, 0.00h, 0.00h);
                const half3 c1  = half3(0.01h, 0.00h, 0.03h);
                const half3 c2  = half3(0.04h, 0.01h, 0.40h);
                const half3 c3  = half3(0.00h, 0.75h, 1.00h);
                const half3 c4  = half3(0.00h, 1.00h, 0.20h);
                const half3 c5  = half3(0.65h, 1.00h, 0.00h);
                const half3 c6  = half3(1.00h, 1.00h, 0.00h);
                const half3 c7  = half3(1.00h, 0.60h, 0.00h);
                const half3 c8  = half3(1.00h, 0.00h, 0.00h);
                const half3 c9  = half3(1.00h, 0.80h, 0.60h);

                const half seg = 1.0h / 9.0h;

                if (t <= seg * 1.0h) return lerp(c0, c1, smoothstep(0.0h, seg * 1.0h, t));
                if (t <= seg * 2.0h) return lerp(c1, c2, smoothstep(seg * 1.0h, seg * 2.0h, t));
                if (t <= seg * 3.0h) return lerp(c2, c3, smoothstep(seg * 2.0h, seg * 3.0h, t));
                if (t <= seg * 4.0h) return lerp(c3, c4, smoothstep(seg * 3.0h, seg * 4.0h, t));
                if (t <= seg * 5.0h) return lerp(c4, c5, smoothstep(seg * 4.0h, seg * 5.0h, t));
                if (t <= seg * 6.0h) return lerp(c5, c6, smoothstep(seg * 5.0h, seg * 6.0h, t));
                if (t <= seg * 7.0h) return lerp(c6, c7, smoothstep(seg * 6.0h, seg * 7.0h, t));
                if (t <= seg * 8.0h) return lerp(c7, c8, smoothstep(seg * 7.0h, seg * 8.0h, t));
                return lerp(c8, c9, smoothstep(seg * 8.0h, 1.0h, t));
            }

            // Smooth-source thermal with FBM band snapping: exact 10 palette colors, hard-edge stippling between adjacent stops.
            half3 ThermalEffectDithered(half3 InColor, half noise01, half ditherMix)
            {
                half lum = dot(InColor, half3(0.299h, 0.587h, 0.114h));
                half k = 0.15h;
                half t = (lum / (k + max(lum, 1e-5h))) * (k + 1.0h);
                t = saturate(pow(t, 0.80h));

                half scaledT = t * 9.0h;
                half bandIndex = floor(scaledT);
                half bandFrac = frac(scaledT);

                half hardStep = step(noise01, bandFrac);
                half ditheredIndex = clamp(bandIndex + hardStep, 0.0h, 9.0h);
                half smoothIndex = scaledT;
                half finalIndex = lerp(smoothIndex, ditheredIndex, saturate(ditherMix));

                const half3 c0  = half3(0.00h, 0.00h, 0.00h);
                const half3 c1  = half3(0.01h, 0.00h, 0.03h);
                const half3 c2  = half3(0.04h, 0.01h, 0.40h);
                const half3 c3  = half3(0.00h, 0.75h, 1.00h);
                const half3 c4  = half3(0.00h, 1.00h, 0.20h);
                const half3 c5  = half3(0.65h, 1.00h, 0.00h);
                const half3 c6  = half3(1.00h, 1.00h, 0.00h);
                const half3 c7  = half3(1.00h, 0.60h, 0.00h);
                const half3 c8  = half3(1.00h, 0.00h, 0.00h);
                const half3 c9  = half3(1.00h, 0.80h, 0.60h);

                if (finalIndex <= 1.0h) return lerp(c0, c1, finalIndex);
                if (finalIndex <= 2.0h) return lerp(c1, c2, finalIndex - 1.0h);
                if (finalIndex <= 3.0h) return lerp(c2, c3, finalIndex - 2.0h);
                if (finalIndex <= 4.0h) return lerp(c3, c4, finalIndex - 3.0h);
                if (finalIndex <= 5.0h) return lerp(c4, c5, finalIndex - 4.0h);
                if (finalIndex <= 6.0h) return lerp(c5, c6, finalIndex - 5.0h);
                if (finalIndex <= 7.0h) return lerp(c6, c7, finalIndex - 6.0h);
                if (finalIndex <= 8.0h) return lerp(c7, c8, finalIndex - 7.0h);
                return lerp(c8, c9, finalIndex - 8.0h);
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
                half toDither = saturate(maskRaw / mid);
                half toThermal = saturate((maskRaw - mid) / (1.0 - mid + 1e-4));
                half3 blended = lerp(rgb, dithered, toDither);
                // Smooth source RGB + same FBM as bitonal dither; _DitherMix blends smooth vs noise-snapped thermal bands.
                half3 thermalOnDither = ThermalEffectDithered(rgb, n01, _DitherMix);
                half3 outRgb = lerp(blended, thermalOnDither, toThermal);

                return half4(outRgb, samp.a);
            }
            ENDCG
        }
    }
}
