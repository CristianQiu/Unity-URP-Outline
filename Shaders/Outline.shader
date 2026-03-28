Shader "Hidden/Outline"
{
    SubShader
    {
        Tags
        { 
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Mask"
            
            ZTest Always
            ZWrite Off
            Cull Off
            Blend Off
            ColorMask R

            HLSLPROGRAM

            // Needed to support the GPU resident drawer. 
            // Note that I have removed stuff that it seems I do not need.
            // See https://gamedev.center/how-to-write-a-custom-urp-shader-with-dots-instancing-support/
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityDOTSInstancing.hlsl"

            #pragma multi_compile _ DOTS_INSTANCING_ON

            #pragma vertex Vert
            #pragma fragment Frag

            struct Attributes
            {
                float4 positionOS : POSITION;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;

                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);

                return OUT;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                
                return 1.0h;
            }

            ENDHLSL
        }

        Pass
        {
            Name "Resolve"
            
            ZTest Always
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag

            TEXTURE2D_X(_OutlineMaskTexture);
            float4 _OutlineMaskTexture_TexelSize;

            float _BorderSize;
            half4 _Colors[4] = 
            {
                half4(1.0, 1.0, 1.0, 1.0),
                half4(1.0, 1.0, 1.0, 1.0),
                half4(1.0, 1.0, 1.0, 1.0),
                half4(1.0, 1.0, 1.0, 1.0),
            };
            half4 _FillAlphas;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                float2 texelSize = _OutlineMaskTexture_TexelSize.xy;

                half4 top = SAMPLE_TEXTURE2D_X(_OutlineMaskTexture, sampler_PointClamp, uv + texelSize * float2(0.0, _BorderSize));
                half4 right = SAMPLE_TEXTURE2D_X(_OutlineMaskTexture, sampler_PointClamp, uv + texelSize * float2(_BorderSize, 0.0));
                half4 bottom = SAMPLE_TEXTURE2D_X(_OutlineMaskTexture, sampler_PointClamp, uv + texelSize * float2(0.0, - _BorderSize));
                half4 left = SAMPLE_TEXTURE2D_X(_OutlineMaskTexture, sampler_PointClamp, uv + texelSize * float2(-_BorderSize, 0.0));
                half4 rightTop = SAMPLE_TEXTURE2D_X(_OutlineMaskTexture, sampler_PointClamp, uv + texelSize * float2(_BorderSize, _BorderSize));
                half4 rightBottom = SAMPLE_TEXTURE2D_X(_OutlineMaskTexture, sampler_PointClamp, uv + texelSize * float2(_BorderSize, -_BorderSize));
                half4 leftBottom = SAMPLE_TEXTURE2D_X(_OutlineMaskTexture, sampler_PointClamp, uv + texelSize * float2(-_BorderSize, -_BorderSize));
                half4 leftTop = SAMPLE_TEXTURE2D_X(_OutlineMaskTexture, sampler_PointClamp, uv + texelSize * float2(-_BorderSize, _BorderSize));

                half4 center = SAMPLE_TEXTURE2D_X(_OutlineMaskTexture, sampler_PointClamp, uv);
                half4 cross = (right + left + top + bottom);
                half4 diagonal = (rightTop + rightBottom + leftBottom + leftTop);
                half4 total = cross + diagonal;
                
                half4 insideFillMask = saturate(center * 1000.0h);
                half4 expandedMask = saturate((center + total) * 1000.0h);

                half4 color1 = expandedMask.r * _Colors[0];
                half4 color2 = expandedMask.g * _Colors[1];
                half4 color3 = expandedMask.b * _Colors[2];
                half4 color4 = expandedMask.a * _Colors[3];

                half4 colorAlphas = half4(color1.a, color2.a, color3.a, color4.a);
                colorAlphas = lerp(colorAlphas, _FillAlphas, insideFillMask);

                color1.a = colorAlphas.x;
                color2.a = colorAlphas.y;
                color3.a = colorAlphas.z;
                color4.a = colorAlphas.w;

                color1.rgb *= color1.a;
                color2.rgb *= color2.a;
                color3.rgb *= color3.a;
                color4.rgb *= color4.a;

                half4 finalColor = (color1 + color2 + color3 + color4) / max(1.0h, expandedMask.r + expandedMask.g + expandedMask.b + expandedMask.a);
                finalColor.a = Max3(color1.a, color2.a, max(color3.a, color4.a));;

                return finalColor;
            }

            ENDHLSL
        }
    }

    Fallback Off
}