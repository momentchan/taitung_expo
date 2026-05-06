Shader "TaitungExpo/TrackerMask"
{
    Properties
    {
        _MainTex ("Previous Frame Mask", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        CGINCLUDE
        #include "UnityCG.cginc"
        #include "TrackerData.cginc"

        sampler2D _MainTex;
        float4 _MainTex_ST;

        StructuredBuffer<TrackerData> _TrackerBuffer;
        int _TrackerNum;

        float _Radius;
        float _Aspect;
        float _HistoryDecay;

        float4 ComputeInstantMask(float2 uv, TrackerData d)
        {
            float4 col = 0;
            float2 dis = (d.pos - uv) * float2(_Aspect, 1);
            float len = length(dis);
            float radius = _Radius * d.depth;
            col.rgb += smoothstep(radius, radius * 0.1, len) * d.activeRatio * d.depth;
            return col;
        }

        float4 frag(v2f_img i) : SV_Target
        {
            float4 prev = tex2D(_MainTex, i.uv);
            float4 instant = 0;
            for (int t = 0; t < _TrackerNum; t++)
            {
                TrackerData d = _TrackerBuffer[t];
                instant += ComputeInstantMask(i.uv, d);
            }
            // Fade last frame and add current tracker splats (clamped for stable feedback)
            float4 combined = saturate(instant + prev * _HistoryDecay);
            return combined;
        }
        ENDCG

        Pass
        {
            CGPROGRAM
            #pragma target 5.0
            #pragma vertex vert_img
            #pragma fragment frag
            ENDCG
        }
    }
}
