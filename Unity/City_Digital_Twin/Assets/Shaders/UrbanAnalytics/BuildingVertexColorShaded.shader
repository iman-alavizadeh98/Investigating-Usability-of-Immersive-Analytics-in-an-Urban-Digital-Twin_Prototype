// Vertex-colour shader for data-coloured buildings.
//
// VertexColorUnlit paints every face of a building the same flat
// colour, so roofs and walls merge and the 3D form is lost. This
// shader keeps the vertex colour unchanged on upward faces (roofs,
// so they match the legend exactly) and darkens walls by a fixed,
// scene-light-independent factor based on which way they face.
// The result is reproducible across scenes and lighting setups.
Shader "UrbanAnalytics/BuildingVertexColorShaded"
{
    Properties
    {
        _Tint("Tint", Color) = (1, 1, 1, 1)

        _WallShadeMin("Wall Shade (away from light)", Range(0, 1)) = 0.55
        _WallShadeMax("Wall Shade (toward light)", Range(0, 1)) = 0.85

        // Only X and Z are used (horizontal direction walls are lit from).
        _ShadeDirection("Shade Direction (XZ)", Vector) = (-0.5, 0, -0.8, 0)
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
            Name "ForwardShaded"

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
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"


            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };


            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };


            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _WallShadeMin;
                half _WallShadeMax;
                float4 _ShadeDirection;
            CBUFFER_END


            Varyings Vert(
                Attributes input
            )
            {
                Varyings output;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionHCS =
                    TransformObjectToHClip(
                        input.positionOS.xyz
                    );

                output.normalWS =
                    TransformObjectToWorldNormal(
                        input.normalOS
                    );

                output.color =
                    input.color * _Tint;

                return output;
            }


            half4 Frag(
                Varyings input
            ) : SV_Target
            {
                half3 normalWS =
                    normalize(input.normalWS);

                float2 direction =
                    _ShadeDirection.xz;

                direction =
                    dot(direction, direction) > 1e-6
                        ? normalize(direction)
                        : float2(0.0, -1.0);

                // 0 = wall faces away from the shade direction,
                // 1 = wall faces toward it.
                half facing =
                    saturate(
                        dot(normalWS.xz, direction) * 0.5 + 0.5
                    );

                half wallShade =
                    lerp(
                        _WallShadeMin,
                        _WallShadeMax,
                        facing
                    );

                // Roofs (normal.y = 1) keep the exact data colour.
                half shade =
                    lerp(
                        wallShade,
                        1.0,
                        saturate(normalWS.y)
                    );

                return half4(
                    input.color.rgb * shade,
                    input.color.a
                );
            }

            ENDHLSL
        }
    }
}
