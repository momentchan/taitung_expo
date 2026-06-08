Shader "Unlit/DitherBitonal"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _DepthMap ("Depth Map", 2D) = "white" {}

        [Header(Depth range)]
        _DepthRangeMin ("Depth Range Min", Range(0, 1)) = 0
        _DepthRangeMax ("Depth Range Max", Range(0, 1)) = 1
        _DepthSplit ("Dither / Thermal Split", Range(0.01, 0.99)) = 0.5

        [Header(Scatter jitter)]
        _ScatterScale ("Scatter Noise Scale", Float) = 500
        _ScatterRadius ("Scatter Radius", Range(0, 0.5)) = 0.1

        [Header(Dither blend)]
        _DitherBoost ("Dither Color Boost", Range(0, 4)) = 2

        [Header(Output)]
        _Ratio ("Output Ratio", Range(0, 1)) = 1
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
            #include "../../Packages/unity-gist/Cginc/Random.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float2 uvDepth : TEXCOORD1;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _DepthMap;
            float4 _DepthMap_ST;

            half _DepthRangeMin;
            half _DepthRangeMax;
            half _DepthSplit;
            float _ScatterScale;
            half _ScatterRadius;
            half _DitherBoost;
            half _Ratio;

            half3 ThermalEffectRGB(half3 InColor)
            {
                half lum = dot(InColor, half3(0.299h, 0.587h, 0.114h));

                half k = 0.15h;
                half t = (lum / (k + max(lum, 1e-5h))) * (k + 1.0h);
                t = saturate(pow(t, 0.80h));

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

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.uvDepth = TRANSFORM_TEX(v.uv, _DepthMap);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                half2 uv = (i.uv - 0.5) * float2(0.5625, 1) * 0.97 + 0.5;
                half2 depthJitter = scatter(i.uv, _ScatterScale, _ScatterRadius);
                half2 colorJitter = scatter(uv, _ScatterScale, _ScatterRadius);

                half depthSample = tex2D(_DepthMap, i.uvDepth + depthJitter).r;
                half depthSpan = max(_DepthRangeMax - _DepthRangeMin, 1e-4h);
                half depthRaw = saturate((depthSample - _DepthRangeMin) / depthSpan);
                half toDither = saturate(depthRaw / _DepthSplit);
                half toThermal = saturate((depthRaw - _DepthSplit) / (1.0 - _DepthSplit + 1e-4));

                half3 col = tex2D(_MainTex, uv).rgb;
                half3 ditheredCol = tex2D(_MainTex, uv + colorJitter).rgb;

                half3 blended = lerp(col, ditheredCol * _DitherBoost, toDither);
                half3 thermalOnDither = ThermalEffectRGB(col);
                half3 outRgb = lerp(blended, thermalOnDither, toThermal);

                return half4(outRgb * _Ratio, 1);
            }
            ENDCG
        }
    }
}
