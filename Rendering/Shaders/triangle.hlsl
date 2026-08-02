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
    output.Position = float4(input.Position, 0.0, 1.0);
    output.Color = input.Color;
    return output;
}

float4 PSMain(VSOutput input) : SV_TARGET
{
    return float4(input.Color, 1.0);
}