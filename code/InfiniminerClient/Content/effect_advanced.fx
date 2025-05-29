#if OPENGL
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_4_0_level_9_1
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif

struct VertexToPixel
{
    float4 Position   	: POSITION;    
    float4 Color		: COLOR0;
    float2 SpecialInfo  : TEXCOORD0;  // x = lighting factor, y = depth
    float2 TextureCoords: TEXCOORD1;
    float3 WorldPos     : TEXCOORD2;  // For ambient occlusion
    float3 Normal       : TEXCOORD3;  // For dynamic lighting
};

struct PixelToFrame
{
    float4 Color : COLOR0;
};

// Existing parameters
float4x4 xView;
float4x4 xProjection;
float4x4 xWorld;
float3 xLODColor;
float xTime;
float xLight;

// New advanced parameters
float3 xSunDirection = float3(0.5, -1.0, 0.3);
float xAmbientOcclusionStrength = 0.5;
float xShadowStrength = 0.7;
float xMetallicness = 0.3;
float xRoughness = 0.7;
float3 xPlayerPosition;

Texture xTexture;
sampler TextureSampler = sampler_state
{
    texture = <xTexture>;
    magfilter = POINT;
    minfilter = ANISOTROPIC;
    mipfilter = NONE;
    AddressU = WRAP;
    AddressV = WRAP;
};

// Ambient Occlusion helper
float CalculateAO(float3 worldPos, float3 normal)
{
    float ao = 1.0;
    float3 toPlayer = normalize(xPlayerPosition - worldPos);
    float dotNP = dot(normal, toPlayer);
    ao *= saturate(1.0 - xAmbientOcclusionStrength * (1.0 - abs(dotNP)));
    return ao;
}

// PBR lighting calculation
float3 CalculatePBRLighting(float3 albedo, float3 normal, float3 worldPos)
{
    float3 lightDir = normalize(-xSunDirection);
    float3 viewDir = normalize(xPlayerPosition - worldPos);
    float3 halfDir = normalize(lightDir + viewDir);
    
    // Fresnel-Schlick
    float3 F0 = lerp(float3(0.04, 0.04, 0.04), albedo, xMetallicness);
    float cosTheta = max(dot(normal, viewDir), 0.0);
    float3 F = F0 + (1.0 - F0) * pow(1.0 - cosTheta, 5.0);
    
    // Geometry-Smith
    float roughness2 = xRoughness * xRoughness;
    float NdotV = max(dot(normal, viewDir), 0.0);
    float NdotL = max(dot(normal, lightDir), 0.0);
    float ggx2 = roughness2 + 1.0;
    float gv = NdotV + sqrt(NdotV * (NdotV - NdotV * roughness2) + roughness2);
    float gl = NdotL + sqrt(NdotL * (NdotL - NdotL * roughness2) + roughness2);
    float G = ggx2 / (gv * gl);
    
    // Cook-Torrance BRDF
    float3 numerator = F * G;
    float denominator = 4.0 * max(dot(normal, viewDir), 0.0) * max(dot(normal, lightDir), 0.0);
    float3 specular = numerator / max(denominator, 0.001);
    
    float3 kD = (float3(1.0, 1.0, 1.0) - F) * (1.0 - xMetallicness);
    float3 diffuse = kD * albedo / 3.14159265359;
    
    return (diffuse + specular) * NdotL;
}

VertexToPixel AdvancedVS(float4 inPos : POSITION, float2 inTexCoords: TEXCOORD0, float inShade: TEXCOORD1)
{    
    VertexToPixel Output = (VertexToPixel)0;
    float4x4 preViewProjection = mul(xView, xProjection);
    float4x4 preWorldViewProjection = mul(xWorld, preViewProjection);
    
    Output.Position = mul(inPos, preWorldViewProjection);
    Output.TextureCoords = inTexCoords;
    Output.SpecialInfo.x = inShade;
    Output.SpecialInfo.y = clamp(Output.Position.z, 0, 64) / 64;
    
    // Calculate world position and normal for lighting
    Output.WorldPos = mul(inPos, xWorld).xyz;
    Output.Normal = normalize(mul(float4(0, 1, 0, 0), xWorld).xyz); // Assuming up normal
    
    return Output;    
}

PixelToFrame AdvancedPS(VertexToPixel PSIn) 
{
    PixelToFrame Output = (PixelToFrame)0;        
    
    // Sample base texture
    float4 texColor = tex2D(TextureSampler, PSIn.TextureCoords);
    
    // Calculate ambient occlusion
    float ao = CalculateAO(PSIn.WorldPos, PSIn.Normal);
    
    // Calculate PBR lighting
    float3 pbr = CalculatePBRLighting(texColor.rgb, PSIn.Normal, PSIn.WorldPos);
    
    // Combine everything
    float3 finalColor = texColor.rgb;
    finalColor *= PSIn.SpecialInfo.x; // Apply original lighting factor
    finalColor *= ao; // Apply ambient occlusion
    finalColor = lerp(finalColor, pbr, 0.6); // Blend with PBR lighting
    finalColor = lerp(finalColor, xLODColor, PSIn.SpecialInfo.y); // Apply LOD color
    finalColor *= xLight; // Apply global light
    
    Output.Color = float4(finalColor, texColor.a);
    return Output;
}

technique Advanced
{
    pass Pass1
    {
        VertexShader = compile VS_SHADERMODEL AdvancedVS();
        PixelShader = compile PS_SHADERMODEL AdvancedPS();
    }
} 