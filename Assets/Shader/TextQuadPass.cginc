#ifndef TEXT_QUAD_PASS_INCLUDED
#define TEXT_QUAD_PASS_INCLUDED

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
    UNITY_FOG_COORDS(1)
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

float _TextQuadAlphaFbmScale;
float _TextQuadAlphaFbmTimeScale;
float _TextQuadAlphaFbmPhase;
half _TextQuadAlphaFbmInfluence;
half _TextQuadAlphaFbmThreshold;

v2f vert (appdata v)
{
    v2f o;
    o.vertex = UnityObjectToClipPos(v.vertex);
    o.uv = TRANSFORM_TEX(v.uv, _MainTex);
    UNITY_TRANSFER_FOG(o,o.vertex);
    return o;
}

half4 frag (v2f i) : SV_Target
{
    half4 ui = tex2D(_MainTex, i.uv);
    half4 bloomSmall = tex2D(_BloomSmallTex, i.uv);
    half4 bloomLarge = tex2D(_BloomLargeTex, i.uv);

    float fSmall = fbm2(i.uv * _BloomSmallFbmScale, _Time.y * _BloomSmallFbmTime + _BloomSmallFbmPhase);
    float modSmall = lerp(1.0, saturate(fSmall * 0.5 + 0.5), saturate(_BloomSmallFbmInfluence));

    float fLarge = fbm2(i.uv * _BloomLargeFbmScale, _Time.y * _BloomLargeFbmTime + _BloomLargeFbmPhase);
    float modLarge = lerp(1.0, saturate(fLarge * 0.5 + 0.5), saturate(_BloomLargeFbmInfluence));

    half3 rgb = ui.rgb;
    rgb += bloomSmall.rgb * (_BloomSmallStrength * (half)modSmall);
    rgb += bloomLarge.rgb * (_BloomLargeStrength * (half)modLarge);
    rgb *= _HdrTint.rgb;

    float fAlpha = fbm2(i.uv * _TextQuadAlphaFbmScale, _Time.y * _TextQuadAlphaFbmTimeScale + _TextQuadAlphaFbmPhase);
    float alphaFbm01 = saturate(fAlpha * 0.5 + 0.5);
    float rate = max(saturate(_TextQuadAlphaFbmThreshold), 1e-3);
    float alphaShaped = smoothstep(0.0, rate, alphaFbm01);
    float alphaMul = lerp(1.0, alphaShaped, saturate(_TextQuadAlphaFbmInfluence));

    half4 col = half4(rgb, ui.a * _HdrTint.a * (half)alphaMul);
    UNITY_APPLY_FOG(i.fogCoord, col);
    return col;
}

#endif
