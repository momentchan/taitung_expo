void ApplyRadialDepthOffset_float(UnityTexture2D DepthMap, UnitySamplerState Sampler, float2 UV, float Amplitude, out float2 OutUV, out float DepthValue)
{
    // Sample the smoothed depth map (0 = flat, >0 = deep)
    float depth = SAMPLE_TEXTURE2D(DepthMap, Sampler, UV).r;

    // Define the center of the table/projection (usually 0.5, 0.5)
    float2 centerUV = float2(0.5, 0.5);

    // Calculate a fake view direction pointing from the center towards the current UV
    // The further from the center, the stronger the angle
    float2 fakeViewDir = UV - centerUV; 

    // Shift UV inward along the fake direction
    float2 offset = fakeViewDir * depth * Amplitude;

    OutUV = UV - offset;
    DepthValue = depth; 
}