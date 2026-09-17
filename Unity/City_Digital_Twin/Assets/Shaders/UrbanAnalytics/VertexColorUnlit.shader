Shader "UrbanAnalytics/VertexColorUnlit"
{
    Properties
    {
        _Tint("Tint", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardUnlit"

            Tags
            {
                "LightMode" = "UniversalForward"
            }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"


            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
            };


            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half4 color : COLOR;
            };


            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
            CBUFFER_END


            Varyings Vert(
                Attributes input
            )
            {
                Varyings output;

                output.positionHCS =
                    TransformObjectToHClip(
                        input.positionOS.xyz
                    );

                output.color =
                    input.color * _Tint;

                return output;
            }


            half4 Frag(
                Varyings input
            ) : SV_Target
            {
                return input.color;
            }

            ENDHLSL
        }
    }
}