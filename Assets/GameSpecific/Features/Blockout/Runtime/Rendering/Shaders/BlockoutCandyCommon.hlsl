#ifndef BLOCKOUT_CANDY_COMMON_INCLUDED
#define BLOCKOUT_CANDY_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

struct BlockoutCandyAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct BlockoutCandyVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    half3 normalWS : TEXCOORD1;
    half3 viewDirWS : TEXCOORD2;
    float4 shadowCoord : TEXCOORD3;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

struct BlockoutCandySurface
{
    half3 baseColor;
    half alpha;
    half metallic;
    half smoothness;
    half rimIntensity;
    half bubbleStrength;
    half crystalStrength;
    half crystalScale;
    half dissolveAmount;
    half dissolveEdgeWidth;
};

BlockoutCandyVaryings BlockoutCandyVertex(BlockoutCandyAttributes input)
{
    BlockoutCandyVaryings output = (BlockoutCandyVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
    output.positionCS = positionInputs.positionCS;
    output.positionWS = positionInputs.positionWS;
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    output.viewDirWS = GetWorldSpaceNormalizeViewDir(positionInputs.positionWS);
    output.shadowCoord = GetShadowCoord(positionInputs);
    return output;
}

half BlockoutCandyHash3(float3 value)
{
    return frac(sin(dot(value, float3(12.9898, 78.233, 37.719))) * 43758.5453);
}

half BlockoutCandyBubbleMask(float3 positionWS, half scale)
{
    float3 cell = floor(positionWS * scale);
    float3 local = frac(positionWS * scale) - 0.5;
    half randomSize = lerp(0.11, 0.34, BlockoutCandyHash3(cell));
    half distanceToCellCenter = length(local);
    return smoothstep(randomSize, randomSize * 0.45, distanceToCellCenter);
}

half BlockoutCandyCrystalMask(float3 positionWS, half scale)
{
    float3 tiled = frac(positionWS * max(scale, 1.0));
    half edgeX = 1.0 - smoothstep(0.36, 0.49, abs(tiled.x - 0.5));
    half edgeY = 1.0 - smoothstep(0.36, 0.49, abs(tiled.y - 0.5));
    half edgeZ = 1.0 - smoothstep(0.36, 0.49, abs(tiled.z - 0.5));
    half cellEdge = saturate(max(edgeX * edgeY, max(edgeY * edgeZ, edgeX * edgeZ)));
    return cellEdge * step(0.62, BlockoutCandyHash3(floor(positionWS * max(scale, 1.0))));
}

half4 BlockoutCandyEvaluate(BlockoutCandyVaryings input, BlockoutCandySurface surface, half3 rimColor, half3 bubbleColor, half3 crystalColor)
{
    half3 normalWS = normalize(input.normalWS);
    half3 viewDirWS = normalize(input.viewDirWS);
    Light mainLight = GetMainLight(input.shadowCoord);

    half ndotl = saturate(dot(normalWS, mainLight.direction));
    half shadow = mainLight.shadowAttenuation;
    half3 ambient = SampleSH(normalWS);
    half3 diffuse = surface.baseColor * (ambient + mainLight.color * (ndotl * shadow));

    half3 halfVector = SafeNormalize(mainLight.direction + viewDirWS);
    half specularPower = lerp(8.0, 96.0, surface.smoothness);
    half specular = pow(saturate(dot(normalWS, halfVector)), specularPower);
    half3 specularColor = lerp(half3(0.04, 0.04, 0.04), surface.baseColor, surface.metallic);

    half rim = pow(1.0 - saturate(dot(normalWS, viewDirWS)), 3.0) * surface.rimIntensity;
    half bubbles = BlockoutCandyBubbleMask(input.positionWS, 3.0) * surface.bubbleStrength;
    half crystals = BlockoutCandyCrystalMask(input.positionWS, surface.crystalScale) * surface.crystalStrength;
    half dissolveNoise = BlockoutCandyHash3(floor(input.positionWS * 3.5));
    half dissolveAmount = saturate(surface.dissolveAmount);
    half dissolveEnabled = step(0.0001, dissolveAmount);
    half dissolveEdge = dissolveEnabled * (1.0 - smoothstep(0.0, max(surface.dissolveEdgeWidth, 0.001), abs(dissolveNoise - dissolveAmount)));
    clip(1.0 - dissolveEnabled + dissolveNoise - dissolveAmount);

    half3 color = diffuse;
    color += specularColor * specular * mainLight.color;
    color += rimColor * rim;
    color = lerp(color, color + bubbleColor, bubbles);
    color += crystalColor * crystals;
    color += rimColor * dissolveEdge * 0.9;
    return half4(color, surface.alpha);
}

#endif
