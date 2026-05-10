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
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

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

                half4 col = half4(rgb, ui.a * _HdrTint.a);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
