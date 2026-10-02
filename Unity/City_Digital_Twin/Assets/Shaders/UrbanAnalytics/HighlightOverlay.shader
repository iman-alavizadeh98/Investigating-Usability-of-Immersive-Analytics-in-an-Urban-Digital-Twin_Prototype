// Transparent overlay drawn on top of a copy of an entity's own
// triangles (hover, selection and comparison markers).
//
// Pass 1 draws the visible part with a depth offset so it wins
// against the identical source triangles. Pass 2 draws the part
// hidden behind other geometry faintly ("x-ray"), so a selected
// cell stays findable under tall buildings or columns.
Shader "UrbanAnalytics/HighlightOverlay"
{
    Properties
    {
        _Color("Color", Color) = (1, 0.83, 0, 0.5)
        _OccludedAlpha("Occluded Alpha (fraction of Color alpha)", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+10"
        }

        HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        struct Attributes
        {
            float4 positionOS : POSITION;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionHCS : SV_POSITION;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            half _OccludedAlpha;
        CBUFFER_END

        Varyings Vert(Attributes input)
        {
            Varyings output;

            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);

            return output;
        }

        ENDHLSL

        Pass
        {
            Name "Visible"

            Tags
            {
                "LightMode" = "UniversalForward"
            }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back
            Offset -1, -1

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            half4 Frag(Varyings input) : SV_Target
            {
                return _Color;
            }

            ENDHLSL
        }

        Pass
        {
            Name "Occluded"

            Tags
            {
                "LightMode" = "SRPDefaultUnlit"
            }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Greater
            Cull Back

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            half4 Frag(Varyings input) : SV_Target
            {
                return half4(_Color.rgb, _Color.a * _OccludedAlpha);
            }

            ENDHLSL
        }
    }
}
