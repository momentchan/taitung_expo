// High-contrast 10-stop thermal — same math as DitherBitonal.shader ThermalEffectRGB.

#ifndef TAITUNG_THERMAL_EFFECT_INCLUDED
#define TAITUNG_THERMAL_EFFECT_INCLUDED

void ThermalEffect_float(float3 InColor, out float3 OutColor)
{
    float lum = dot(InColor, float3(0.299, 0.587, 0.114));

    float k = 0.15;
    float t = (lum / (k + max(lum, 1e-5))) * (k + 1.0);
    t = saturate(pow(t, 0.80));

    float3 c0  = float3(0.00, 0.00, 0.00);
    float3 c1  = float3(0.01, 0.00, 0.03);
    float3 c2  = float3(0.04, 0.01, 0.40);
    float3 c3  = float3(0.00, 0.75, 1.00);
    float3 c4  = float3(0.00, 1.00, 0.20);
    float3 c5  = float3(0.65, 1.00, 0.00);
    float3 c6  = float3(1.00, 1.00, 0.00);
    float3 c7  = float3(1.00, 0.60, 0.00);
    float3 c8  = float3(1.00, 0.00, 0.00);
    float3 c9  = float3(1.00, 0.80, 0.60);

    const float seg = 1.0 / 9.0;

    if (t <= seg * 1.0) { OutColor = lerp(c0, c1, smoothstep(0.0,       seg * 1.0, t)); return; }
    if (t <= seg * 2.0) { OutColor = lerp(c1, c2, smoothstep(seg * 1.0, seg * 2.0, t)); return; }
    if (t <= seg * 3.0) { OutColor = lerp(c2, c3, smoothstep(seg * 2.0, seg * 3.0, t)); return; }
    if (t <= seg * 4.0) { OutColor = lerp(c3, c4, smoothstep(seg * 3.0, seg * 4.0, t)); return; }
    if (t <= seg * 5.0) { OutColor = lerp(c4, c5, smoothstep(seg * 4.0, seg * 5.0, t)); return; }
    if (t <= seg * 6.0) { OutColor = lerp(c5, c6, smoothstep(seg * 5.0, seg * 6.0, t)); return; }
    if (t <= seg * 7.0) { OutColor = lerp(c6, c7, smoothstep(seg * 6.0, seg * 7.0, t)); return; }
    if (t <= seg * 8.0) { OutColor = lerp(c7, c8, smoothstep(seg * 7.0, seg * 8.0, t)); return; }
    OutColor = lerp(c8, c9, smoothstep(seg * 8.0, 1.0, t));
}

#endif
