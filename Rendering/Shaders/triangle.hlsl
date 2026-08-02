struct VSOutput
{
    float4 Position : SV_POSITION;
    float3 Color : COLOR0;
};

// Same hardcoded triangle NDC coordinates the Vulkan GLSL shader used -
// Vulkan's rasterizer interprets these identically regardless of source language.
static const float2 Positions[3] =
{
    float2(0.0, -0.5),
    float2(0.5, 0.5),
    float2(-0.5, 0.5)
};

static const float3 Colors[3] =
{
    float3(1.0, 0.0, 0.0),
    float3(0.0, 1.0, 0.0),
    float3(0.0, 0.0, 1.0)
};

VSOutput VSMain(uint vertexID : SV_VertexID)
{
    VSOutput output;
    output.Position = float4(Positions[vertexID], 0.0, 1.0);
    output.Color = Colors[vertexID];
    return output;
}

float4 PSMain(VSOutput input) : SV_TARGET
{
    return float4(input.Color, 1.0);
}
