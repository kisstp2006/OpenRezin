// Matches Vulkan set 0/binding 0 and OpenGL uniform-buffer binding point 0.
// Row-major storage matches the uploaded System.Numerics matrices.
cbuffer CameraBuffer : register(b0, space0)
{
    row_major float4x4 View;
    row_major float4x4 Projection;
};
// Per-object data. Set 0 is mandatory for OpenGL SPIR-V; only the binding differs.
cbuffer ObjectBuffer : register(b1, space0)
{
    row_major float4x4 Model;
};

struct VSInput
{
    float2 Position : POSITION;
    float3 Color : COLOR0;
};

struct VSOutput
{
    float4 Position : SV_POSITION;
    float3 Color : COLOR0;
};

VSOutput VSMain(VSInput input)
{
    VSOutput output;

    // System.Numerics uses row vectors, so positions multiply View first and
    // Projection second before being written to clip space.
    float4 worldPosition = mul(
    float4(input.Position, 0.0, 1.0),
    Model);

    float4 viewPosition = mul(
    worldPosition,
    View);

    output.Position = mul(
    viewPosition,
    Projection);

    output.Color = input.Color;
    return output;
}

float4 PSMain(VSOutput input) : SV_TARGET
{
    return float4(input.Color, 1.0);
}
