// Draw transformed, colored geometry.

#if OPENGL
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_4_0_level_9_1
	#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

// Global Parameters
float4x4 View;
float4x4 Projection;
float4x4 World;
float3 LightDirection;
float Ambient;
bool EnableLighting;

// Technique: Colored
struct ColoredPixel
{
    float4 Position : POSITION;
    float4 Color : COLOR0;
    float LightingFactor : TEXCOORD0;
};

ColoredPixel ColoredVS(float4 position : POSITION, float3 normal: NORMAL, float4 color: COLOR0)
{    
    ColoredPixel output = (ColoredPixel)0;
    float4x4 transform = mul(World, mul(View, Projection));
    
    output.Position = mul(position, transform);    
    output.Color = color;
    
    normal = normalize(mul(normalize(normal), World));
    output.LightingFactor = 1;
    if (EnableLighting)
        output.LightingFactor = saturate(dot(normal, -LightDirection));
    
    return output;    
}

float4 ColoredPS(ColoredPixel pixel) : COLOR
{
    float4 color = 0;
    
    color = pixel.Color;
    color.rgb *= saturate(pixel.LightingFactor + Ambient);

    return color;
}

technique Colored
{
    pass Pass0
    {
        VertexShader = compile VS_SHADERMODEL ColoredVS();
        PixelShader = compile PS_SHADERMODEL ColoredPS();
    }
}
