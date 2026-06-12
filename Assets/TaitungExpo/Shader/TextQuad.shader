Shader "Unlit/TextQuad"
{
    Properties
    {
        _MainTex ("UI", 2D) = "white" {}
        _BloomSmallTex ("UI Bloom Small", 2D) = "black" {}
        _BloomLargeTex ("UI Bloom Large", 2D) = "black" {}
        _TextUnderTexture ("Text Under Texture", 2D) = "black" {}
        _TextUnderStrength ("Text Under Strength", Range(0, 2)) = 1
        _TextUnderDistanceMaskRange ("Text Under Distance Mask Range", Vector) = (0.4, 0.4, 0, 0)

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
        [HDR] _HdrTintWhite ("HDR Tint White", Color) = (5,5,5,1)
        [HDR] _HdrTint1 ("HDR Tint Red Accent", Color) = (4,0.35,0,1)
        [HDR] _HdrTint2 ("HDR Tint Orange", Color) = (7,2.25,0,1)
        [HDR] _HdrTint3 ("HDR Tint Yellow", Color) = (7,5.5,0.35,1)
        _HdrTintRedWidth ("HDR Tint Red Width", Range(0.02, 0.5)) = 0.12
        _HdrTintYellowStart ("HDR Tint Yellow Start", Range(0.1, 1)) = 0.68
        _HdrTintRadiusScale ("HDR Tint Radius Scale", Range(0, 4)) = 1.85
        _HdrTintNoiseScale ("HDR Tint Noise Scale", Range(0.1, 20)) = 4.8
        _HdrTintNoiseStrength ("HDR Tint Noise Strength", Range(0, 1)) = 0.62
        _HdrTintDiagonalStrength ("HDR Tint Diagonal Strength", Range(0, 1)) = 0.52
        _HdrTintRadiusStrength ("HDR Tint Radius Strength", Range(0, 1)) = 0.22
        _HdrTintBaseOffset ("HDR Tint Base Offset", Range(0, 1)) = 0.18
        _HdrTintBlend ("HDR Tint Gradient Blend", Range(0, 1)) = 0

        [Header(Depth UV distort)]
        _DepthMap ("Depth Map", 2D) = "white" {}
        _DepthRangeMin ("Depth Range Min", Range(0, 1)) = 0
        _DepthRangeMax ("Depth Range Max", Range(0, 1)) = 1
        _UvDistortStrength ("UV Distort Strength", Float) = 0.02
        _DepthInteractionRatio ("Depth Interaction Ratio", Range(0, 1)) = 1
        _UvDistortNoDepthStrength ("No Depth Distort Strength", Range(0, 1)) = 0
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
            sampler2D _TextUnderTexture;
            half _TextUnderStrength;
            float4 _TextUnderDistanceMaskRange;

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

            half4 _HdrTintWhite;
            half4 _HdrTint1;
            half4 _HdrTint2;
            half4 _HdrTint3;
            half _HdrTintRedWidth;
            half _HdrTintYellowStart;
            half _HdrTintRadiusScale;
            half _HdrTintNoiseScale;
            half _HdrTintNoiseStrength;
            half _HdrTintDiagonalStrength;
            half _HdrTintRadiusStrength;
            half _HdrTintBaseOffset;
            half _HdrTintBlend;

            sampler2D _DepthMap;
            float4 _DepthMap_ST;
            half _DepthRangeMin;
            half _DepthRangeMax;
            float _UvDistortStrength;
            half _DepthInteractionRatio;
            half _UvDistortNoDepthStrength;
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

            // FBM UV offset, scaled by normalized depth with an optional no-depth baseline.
            float2 DistortedContentUv(float2 baseUv, float2 uvDepth)
            {
                half depthRaw = tex2D(_DepthMap, uvDepth).r;
                half depthSpan = max(_DepthRangeMax - _DepthRangeMin, 1e-4h);
                half depth = saturate((depthRaw - _DepthRangeMin) / depthSpan) * saturate(_DepthInteractionRatio);
                half distortWeight = lerp(saturate(_UvDistortNoDepthStrength), 1.0h, depth);

                float2 uvF = baseUv * _UvDistortFbmScale;
                float t = _Time.y * _UvDistortFbmTime + _UvDistortFbmPhase;
                float fX = fbm2(uvF, t);
                float fY = fbm2(uvF, t + 16.8);
                half2 fbm01 = saturate(half2(fX, fY) * 0.5h + 0.5h);
                half2 distort = (fbm01 - 0.5) * 2.0 * distortWeight;

                return saturate(baseUv + distort * _UvDistortStrength);
            }

            half4 SampleHdrTint(float2 uv)
            {
                float2 centeredUv = uv - 0.5;
                float radius01 = saturate(length(centeredUv) * _HdrTintRadiusScale);
                float diagonal01 = saturate(dot(uv, float2(0.72, 0.28)));
                float2 broadNoiseUv = uv * max(_HdrTintNoiseScale, 1e-3h) + float2(radius01 * 1.7, radius01 * 1.19);
                float noise01 = saturate(fbm3(broadNoiseUv, 0.37) * 0.5 + 0.5);
                float baseGradient = saturate(
                    diagonal01 * _HdrTintDiagonalStrength +
                    radius01 * _HdrTintRadiusStrength +
                    _HdrTintBaseOffset);
                float noiseScatter = (noise01 - 0.5) * _HdrTintNoiseStrength;
                half t = (half)saturate(baseGradient + noiseScatter);
                half redWidth = max(_HdrTintRedWidth, 1e-3h);
                half yellowStart = min(max(_HdrTintYellowStart, redWidth + 1e-3h), 0.999h);
                half redToOrange = smoothstep(0.0h, redWidth, t);
                half orangeToYellow = smoothstep(yellowStart, 1.0h, t);
                half4 redOrange = lerp(_HdrTint1, _HdrTint2, redToOrange);

                return lerp(redOrange, _HdrTint3, orangeToYellow);
            }

            half SampleTextUnderDistanceMask(float2 uv)
            {
                float inner = max(0.0, min(_TextUnderDistanceMaskRange.x, _TextUnderDistanceMaskRange.y));
                float outer = max(0.0, max(_TextUnderDistanceMaskRange.x, _TextUnderDistanceMaskRange.y));
                float enabled = step(1e-4, outer - inner);
                float distanceToCenter = length(uv - 0.5) * 2.0;
                float feather = max(fwidth(distanceToCenter) * 2.0, 0.002);
                float innerFade = smoothstep(inner - feather, inner + feather, distanceToCenter);
                float outerFade = 1.0 - smoothstep(outer - feather, outer + feather, distanceToCenter);
                float removeBand = saturate(innerFade * outerFade);

                return (half)lerp(1.0, 1.0 - removeBand, enabled);
            }

            half4 frag (v2f i) : SV_Target
            {
                float2 uvC = DistortedContentUv(i.uv, i.uvDepth);
                half2 uv = (i.uv - 0.5) * float2(0.5625, 1) * 0.98 + 0.5;

                half4 ui = tex2D(_MainTex, uvC);
                half4 bloomSmall = tex2D(_BloomSmallTex, uvC);
                half4 bloomLarge = tex2D(_BloomLargeTex, uvC);

                float fSmall = fbm2(uvC * _BloomSmallFbmScale, _Time.y * _BloomSmallFbmTime + _BloomSmallFbmPhase);
                float modSmall = lerp(1.0, saturate(fSmall * 0.5 + 0.5), saturate(_BloomSmallFbmInfluence));

                float fLarge = fbm2(uvC * _BloomLargeFbmScale, _Time.y * _BloomLargeFbmTime + _BloomLargeFbmPhase);
                float modLarge = lerp(1.0, saturate(fLarge * 0.5 + 0.5), saturate(_BloomLargeFbmInfluence));

                half4 gradientTint = SampleHdrTint(i.uv);
                half4 tint = lerp(_HdrTintWhite, gradientTint, saturate(_HdrTintBlend));
                half3 rgb = ui.rgb;
                rgb += bloomSmall.rgb * (_BloomSmallStrength * (half)modSmall);
                rgb += bloomLarge.rgb * (_BloomLargeStrength * (half)modLarge);
                rgb *= tint.rgb;

                half4 textUnder = tex2D(_TextUnderTexture, uv);
                half textUnderStrength = _TextUnderStrength * SampleTextUnderDistanceMask(i.uv);
                half textMask = saturate(max(max(rgb.r, rgb.g), rgb.b));
                rgb = textUnder.rgb * textUnderStrength * (1.0h - textMask) + rgb;

                float fAlpha = fbm2(uvC * _TextQuadAlphaFbmScale, _Time.y * _TextQuadAlphaFbmTimeScale + _TextQuadAlphaFbmPhase);
                float alphaFbm = saturate(fAlpha * 0.5 + 0.5);
                alphaFbm = smoothstep(_TextQuadAlphaFbmThreshold, 1.0, alphaFbm);

                half4 curr = half4(rgb, max(textUnder.a * textUnderStrength, tint.a * (half)alphaFbm));

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
