// Procedural aurora, authored for this application. Compile as ps_3_0.
// No desktop texture: every pixel is generated from viewport coordinates and time.
float time : register(c0);
float aspectRatio : register(c1);

float hash(float x)
{
    x = frac(x * 0.1031);
    x *= x + 33.33;
    return frac(x * (x + x));
}

float noise(float x)
{
    float cell = floor(x);
    float f = frac(x);
    return lerp(hash(cell), hash(cell + 1.0), f * f * (3.0 - 2.0 * f));
}

float4 main(float2 uv : TEXCOORD) : COLOR
{
    float t = time * 0.24;
    // Limit aspect-dependent detail to keep narrow coverage and the mini preview legible.
    float detail = clamp(aspectRatio, 2.0, 12.0) / 8.0;
    float3 light = 0.0;
    [unroll]
    for (int layer = 0; layer < 3; layer++)
    {
        float phase = t + layer * 2.13;
        float x = uv.x + 0.025 * sin(phase + uv.x * 5.0);
        float fold = sin(x * 8.0 + phase) + 0.38 * sin(x * 19.0 - phase * 0.7);
        float bottom = 0.38 + layer * 0.09 + fold * 0.095;
        float rise = bottom - uv.y;
        // Vertical rays bend and spread upward from each folded, luminous lower edge.
        float rayX = x + max(rise, 0.0) * 0.045 * sin(x * 11.0 + phase);
        float rays = noise(rayX * 150.0 * detail + layer * 31.0 + t * 0.6);
        float filaments = noise(rayX * 330.0 * detail - t * 0.4 + layer * 17.0);
        float billow = 0.5 + 0.5 * noise(x * 9.0 + phase);
        float curtain = exp2(-max(rise, 0.0) * 5.5) * smoothstep(-0.045, 0.018, rise);
        float beam = curtain * (0.12 + rays * rays * 0.65 + filaments * filaments * 0.23);
        float ribbon = exp2(-abs(rise) * 75.0) * 0.48;
        float halo = exp2(-abs(rise) * 15.0) * 0.09;
        float3 green = float3(0.16, 1.0, 0.48);
        float3 cyan = float3(0.12, 0.68, 0.95);
        float3 violet = float3(0.65, 0.25, 0.92);
        float3 baseColor = lerp(green, cyan, 0.5 + 0.5 * sin(x * 4.0 + phase));
        float3 color = lerp(baseColor, violet, saturate(rise * 2.8) * 0.7);
        light += color * (beam + ribbon + halo) * billow * (0.7 - layer * 0.12);
    }
    float3 rgb = 1.0 - exp2(-light * 1.35);
    // WPF expects premultiplied BGRA: rgb must never exceed alpha.
    // The parent applies the shared viewport feather and animation opacity afterward.
    float alpha = max(rgb.r, max(rgb.g, rgb.b));
    return float4(rgb, alpha);
}
