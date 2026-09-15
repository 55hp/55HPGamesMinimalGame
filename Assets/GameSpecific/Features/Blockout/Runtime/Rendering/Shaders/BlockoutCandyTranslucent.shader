Shader "hp55games/Blockout/Candy Translucent"
{
    Properties
    {
        [MainColor] _BaseColor("Element Color", Color) = (0.8, 0.9, 1, 0.68)
        _RimColor("Rim Color", Color) = (0.65, 0.9, 1, 1)
        _BubbleColor("Bubble Highlight", Color) = (1, 1, 1, 1)
        _CrystalColor("Crystal Highlight", Color) = (0.75, 0.95, 1, 1)
        _SurfaceAlpha("Surface Alpha", Range(0.18, 0.9)) = 0.58
        _Metallic("Metallic", Range(0, 1)) = 0.12
        _Smoothness("Smoothness", Range(0, 1)) = 0.62
        _RimIntensity("Rim Intensity", Range(0, 2)) = 0.78
        _BubbleStrength("Bubble Strength", Range(0, 1)) = 0.18
        _CrystalScale("Crystal Scale", Range(1, 24)) = 8
        _CrystalStrength("Crystal Strength", Range(0, 1)) = 0.24
        _DissolveAmount("Dissolve Amount", Range(0, 1)) = 0
        _DissolveEdgeWidth("Dissolve Edge Width", Range(0.001, 0.25)) = 0.035
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex BlockoutCandyVertex
            #pragma fragment Fragment
            #pragma multi_compile_fog

            #include "BlockoutCandyCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _RimColor;
                half4 _BubbleColor;
                half4 _CrystalColor;
                half _SurfaceAlpha;
                half _Metallic;
                half _Smoothness;
                half _RimIntensity;
                half _BubbleStrength;
                half _CrystalScale;
                half _CrystalStrength;
                half _DissolveAmount;
                half _DissolveEdgeWidth;
            CBUFFER_END

            half4 Fragment(BlockoutCandyVaryings input) : SV_Target
            {
                BlockoutCandySurface surface;
                half fresnel = pow(1.0 - saturate(dot(normalize(input.normalWS), normalize(input.viewDirWS))), 2.4);
                surface.baseColor = _BaseColor.rgb;
                surface.alpha = saturate(_SurfaceAlpha + fresnel * 0.12);
                surface.metallic = _Metallic;
                surface.smoothness = _Smoothness;
                surface.rimIntensity = _RimIntensity;
                surface.bubbleStrength = _BubbleStrength;
                surface.crystalScale = _CrystalScale;
                surface.crystalStrength = _CrystalStrength;
                surface.dissolveAmount = _DissolveAmount;
                surface.dissolveEdgeWidth = _DissolveEdgeWidth;
                return BlockoutCandyEvaluate(input, surface, _RimColor.rgb, _BubbleColor.rgb, _CrystalColor.rgb);
            }
            ENDHLSL
        }
    }
}
