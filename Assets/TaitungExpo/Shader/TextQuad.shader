Shader "Unlit/TextQuad"
{
    Properties
    {
        _MainTex ("UI", 2D) = "white" {}
        _BloomSmallTex ("UI Bloom Small", 2D) = "black" {}
        _BloomLargeTex ("UI Bloom Large", 2D) = "black" {}

        [Header(Bloom Small)]
        _BloomSmallStrength ("Bloom Small Strength", Range(0, 4)) = 1
        _BloomSmallFbmScale ("Bloom Small FBM Scale", Float) = 4
        _BloomSmallFbmTime ("Bloom Small FBM Time Scale", Float) = 0.4
        _BloomSmallFbmPhase ("Bloom Small FBM Phase", Float) = 0
        _BloomSmallFbmInfluence ("Bloom Small FBM Influence", Range(0, 1)) = 1

        [Header(Bloom Large)]
        _BloomLargeStrength ("Bloom Large Strength", Range(0, 4)) = 1
        _BloomLargeFbmScale ("Bloom Large FBM Scale", Float) = 1.5
        _BloomLargeFbmTime ("Bloom Large FBM Time Scale", Float) = 0.15
        _BloomLargeFbmPhase ("Bloom Large FBM Phase", Float) = 0
        _BloomLargeFbmInfluence ("Bloom Large FBM Influence", Range(0, 1)) = 1

        [Header(Final)]
        [HDR] _HdrTint ("HDR Tint", Color) = (1,1,1,1)

        [Header(Depth UV distort)]
        _DepthMap ("Depth Map", 2D) = "white" {}
        _DepthRangeMin ("Depth Range Min", Range(0, 1)) = 0
        _DepthRangeMax ("Depth Range Max", Range(0, 1)) = 1
        _UvDistortStrength ("UV Distort Strength", Float) = 0.02
        _UvDistortFbmScale ("Distort FBM Scale", Float) = 4
        _UvDistortFbmTime ("Distort FBM Time Scale", Float) = 0.2
        _UvDistortFbmPhase ("Distort FBM Phase", Float) = 0

        [Header(Alpha FBM)]
        _TextQuadAlphaFbmScale ("Alpha FBM Scale", Float) = 3
        _TextQuadAlphaFbmTimeScale ("Alpha FBM Time Scale", Float) = 0.2
        _TextQuadAlphaFbmPhase ("Alpha FBM Phase", Float) = 0
        _TextQuadAlphaFbmThreshold ("Alpha FBM Threshold", Range(-0.1, 1)) = 1

        [Header(Frame blend)]
        [HideInInspector] _HistoryTex ("History", 2D) = "black" {}
        _FrameBlendFactor ("Frame Blend (0 off, 1 max trail)", Range(0, 1)) = 0
        [HideInInspector] _FrameBlendHistoryValid ("History Valid", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        LOD 100

        Pass
        {
            Blend One Zero
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

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
                float2 uvDepth : TEXCOORD1;
                UNITY_FOG_COORDS(2)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _BloomSmallTex;
            sampler2D _BloomLargeTex;

            half _BloomSmallStrength;
            half _BloomLargeStrength;

            float _BloomSmallFbmScale;
            float _BloomSmallFbmTime;
            float _BloomSmallFbmPhase;
            half _BloomSmallFbmInfluence;

            float _BloomLargeFbmScale;
            float _BloomLargeFbmTime;
            float _BloomLargeFbmPhase;
            half _BloomLargeFbmInfluence;

            half4 _HdrTint;

            sampler2D _DepthMap;
            float4 _DepthMap_ST;
            half _DepthRangeMin;
            half _DepthRangeMax;
            float _UvDistortStrength;
            float _UvDistortFbmScale;
            float _UvDistortFbmTime;
            float _UvDistortFbmPhase;

            float _TextQuadAlphaFbmScale;
            float _TextQuadAlphaFbmTimeScale;
            float _TextQuadAlphaFbmPhase;

            half _TextQuadAlphaFbmThreshold;

            sampler2D _HistoryTex;
            half _FrameBlendFactor;
            half _FrameBlendHistoryValid;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.uvDepth = TRANSFORM_TEX(v.uv, _DepthMap);
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }

            // FBM UV offset, scaled by normalized depth (no distort where depth is empty).
            float2 DistortedContentUv(float2 baseUv, float2 uvDepth)
            {
                half depthRaw = tex2D(_DepthMap, uvDepth).r;
                half depthSpan = max(_DepthRangeMax - _DepthRangeMin, 1e-4h);
                half depth = saturate((depthRaw - _DepthRangeMin) / depthSpan);

                float2 uvF = baseUv * _UvDistortFbmScale;
                float t = _Time.y * _UvDistortFbmTime + _UvDistortFbmPhase;
                float fX = fbm2(uvF, t);
                float fY = fbm2(uvF, t + 16.8);
                half2 fbm01 = saturate(half2(fX, fY) * 0.5h + 0.5h);
                half2 distort = (fbm01 - 0.5) * 2.0 * depth;

                return saturate(baseUv + distort * _UvDistortStrength);
            }

            half4 frag (v2f i) : SV_Target
            {
                float2 uvC = DistortedContentUv(i.uv, i.uvDepth);

                half4 ui = tex2D(_MainTex, uvC);
                half4 bloomSmall = tex2D(_BloomSmallTex, uvC);
                half4 bloomLarge = tex2D(_BloomLargeTex, uvC);

                float fSmall = fbm2(uvC * _BloomSmallFbmScale, _Time.y * _BloomSmallFbmTime + _BloomSmallFbmPhase);
                float modSmall = lerp(1.0, saturate(fSmall * 0.5 + 0.5), saturate(_BloomSmallFbmInfluence));

                float fLarge = fbm2(uvC * _BloomLargeFbmScale, _Time.y * _BloomLargeFbmTime + _BloomLargeFbmPhase);
                float modLarge = lerp(1.0, saturate(fLarge * 0.5 + 0.5), saturate(_BloomLargeFbmInfluence));

                half3 rgb = ui.rgb;
                rgb += bloomSmall.rgb * (_BloomSmallStrength * (half)modSmall);
                rgb += bloomLarge.rgb * (_BloomLargeStrength * (half)modLarge);
                rgb *= _HdrTint.rgb;


                float fAlpha = fbm2(uvC * _TextQuadAlphaFbmScale, _Time.y * _TextQuadAlphaFbmTimeScale + _TextQuadAlphaFbmPhase);
                float alphaFbm = saturate(fAlpha * 0.5 + 0.5);
                alphaFbm = smoothstep(_TextQuadAlphaFbmThreshold, 1.0, alphaFbm);

                half4 curr = half4(rgb, _HdrTint.a * (half)alphaFbm);

                // Same idea as FrameBlendFeature: lerp current composite toward last frame (history).
                half useHistory = saturate(_FrameBlendFactor) * saturate(_FrameBlendHistoryValid);
                half4 hist = tex2D(_HistoryTex, i.uv);
                half4 col = lerp(curr, hist, useHistory);

                // UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
