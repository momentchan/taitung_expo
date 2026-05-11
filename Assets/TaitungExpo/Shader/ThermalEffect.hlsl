void ThermalEffect_float(float3 InColor, out float3 OutColor)
{
    float lum = dot(InColor, float3(0.299, 0.587, 0.114));

    float3 c_black = float3(0.0, 0.0, 0.0); 
    float3 c_blue = float3(0.0, 0.5, 1.0); 
    float3 c_green = float3(0.0, 1.0, 0.0); 
    float3 c_yellow = float3(1.0, 1.0, 0.0); 
    float3 c_orange = float3(1.0, 0.5, 0.0); 
    float3 c_red = float3(1.0, 0.0, 0.0); 

    float3 color = c_black;
    color = lerp(color, c_blue, saturate(lum * 6.0));
    color = lerp(color, c_green, saturate((lum - 0.15) * 6.0));
    color = lerp(color, c_yellow, saturate((lum - 0.45) * 6.0));
    color = lerp(color, c_orange, saturate((lum - 0.7) * 6.0));
    OutColor = lerp(color, c_red, saturate((lum - 0.9) * 10.0));
}