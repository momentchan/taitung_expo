Shader "TaitungExpo/MaskDepthBevel"
{
    Properties
    {
        _MainTex ("Mask", 2D) = "white" {}
        _BlurRadius ("Blur Radius (Pixels)", Float) = 5.0
        _DepthMultiplier ("Depth Multiplier", Float) = 5.0
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
            float4 _MainTex_TexelSize;

            half _BlurRadius;
            half _DepthMultiplier;

            // R-float / grayscale mask: sample red
            half sampleMask(float2 uv)
            {
                return tex2D(_MainTex, uv).r;
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 texel = _MainTex_TexelSize.xy;
                float2 offsetX = float2(texel.x * _BlurRadius, 0.0);
                float2 offsetY = float2(0.0, texel.y * _BlurRadius);
                float2 uv = i.uv;

                // 5-Tap Cross Blur to create a smooth depth slope
                float m = sampleMask(uv);
                m += sampleMask(uv + offsetX);
                m += sampleMask(uv - offsetX);
                m += sampleMask(uv + offsetY);
                m += sampleMask(uv - offsetY);

                // Average the samples
                m *= 0.2;

                // Generate smooth depth from the blurred mask
                float depth = saturate(m * _DepthMultiplier);

                // Output as grayscale depth map
                return half4(depth, depth, depth, 1.0);
            }
            ENDCG
        }
    }
}
