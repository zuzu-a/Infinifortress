#if OPENGL
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_4_0_level_9_1
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif

// Input texture from the game's render
Texture xSceneTexture;
sampler SceneSampler = sampler_state
{
    texture = <xSceneTexture>;
    magfilter = POINT;
    minfilter = POINT;
    mipfilter = NONE;
    AddressU = CLAMP;
    AddressV = CLAMP;
};

// Parameters for customizing the effect
float xDitherStrength = 0.8;
float xMetallicOverlayStrength = 0.3;
float xTime; // For animated effects
float2 xResolution; // Screen resolution for proper scaling

// 4x4 Bayer matrix for ordered dithering
static const float bayerMatrix[16] = {
    0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
    12.0/16.0, 4.0/16.0, 14.0/16.0,  6.0/16.0,
    3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
    15.0/16.0, 7.0/16.0, 13.0/16.0,  5.0/16.0
};

// Helper function to get bayer matrix value for current pixel
float GetBayerFromCoordinate(float2 pixelCoord)
{
    int x = int(pixelCoord.x) % 4;
    int y = int(pixelCoord.y) % 4;
    return bayerMatrix[y * 4 + x];
}

// Metallic gradient calculation
float3 CalculateMetallicGradient(float2 uv)
{
    float2 gradientUV = uv + float2(sin(xTime * 0.5) * 0.1, cos(xTime * 0.3) * 0.1);
    float gradient = (gradientUV.x + gradientUV.y) * 0.5;
    
    // Create a metallic color palette
    float3 metalColor1 = float3(0.7, 0.7, 0.8); // Light metallic
    float3 metalColor2 = float3(0.4, 0.4, 0.5); // Dark metallic
    
    return lerp(metalColor1, metalColor2, gradient);
}

// Vertex shader just passes through the data
void DitherVS(
    inout float4 position : POSITION,
    inout float2 texCoord : TEXCOORD0)
{
    position = position;
    texCoord = texCoord;
}

float4 DitherPS(float2 texCoord : TEXCOORD0) : COLOR0
{
    // Sample the original scene
    float4 color = tex2D(SceneSampler, texCoord);
    
    // Calculate pixel coordinates for dithering
    float2 pixelCoord = texCoord * xResolution;
    
    // Get dither threshold from Bayer matrix
    float ditherThreshold = GetBayerFromCoordinate(pixelCoord);
    
    // Calculate luminance
    float luminance = dot(color.rgb, float3(0.299, 0.587, 0.114));
    
    // Apply dithering
    float ditheredLuminance = luminance + (ditherThreshold - 0.5) * xDitherStrength;
    ditheredLuminance = saturate(ditheredLuminance);
    
    // Calculate metallic gradient overlay
    float3 metallicOverlay = CalculateMetallicGradient(texCoord);
    
    // Combine dithered color with metallic overlay
    float3 finalColor = lerp(color.rgb, metallicOverlay * ditheredLuminance, xMetallicOverlayStrength);
    
    // Maintain original alpha
    return float4(finalColor, color.a);
}

technique Dither
{
    pass Pass1
    {
        VertexShader = compile VS_SHADERMODEL DitherVS();
        PixelShader = compile PS_SHADERMODEL DitherPS();
    }
} 