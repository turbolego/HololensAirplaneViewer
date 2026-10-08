cbuffer ModelConstantBuffer : register(b0)
{
    float4x4 model;
    float4 color;
};

struct LinePixelInput
{
    min16float4 pos : SV_POSITION;
};

min16float4 main(LinePixelInput input) : SV_TARGET
{
    return color;
}
