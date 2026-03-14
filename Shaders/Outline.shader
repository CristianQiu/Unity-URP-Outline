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
            Name "OutlineMask"
            
            ZTest Always
            ZWrite Off
            Cull Back
            Blend One One
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

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                
                return float4(1.0, 1.0, 1.0, 1.0);
            }

            ENDHLSL
        }

        Pass
        {
            Name "OutlineResolve"
            
            ZTest Always
            ZWrite Off
            Cull Off
            Blend Off

            HLSLPROGRAM

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag

            SAMPLER(sampler_BlitTexture);
            TEXTURE2D_X(_OutlineMaskTexture);
         
            float _BorderSize;
            float4 _FillAlphas;
            float4 _Colors[] = 
            {
                float4(1.0, 1.0, 1.0, 1.0),
                float4(1.0, 1.0, 1.0, 1.0),
                float4(1.0, 1.0, 1.0, 1.0),
                float4(1.0, 1.0, 1.0, 1.0),
            };

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                float2 texelSize = _BlitTexture_TexelSize.xy;

                float4 up = SAMPLE_TEXTURE2D_X(_OutlineMaskTexture, sampler_PointClamp, uv + texelSize * float2(0.0, _BorderSize));
                float4 right = SAMPLE_TEXTURE2D_X(_OutlineMaskTexture, sampler_PointClamp, uv + texelSize * float2(_BorderSize, 0.0));
                float4 down = SAMPLE_TEXTURE2D_X(_OutlineMaskTexture, sampler_PointClamp, uv + texelSize * float2(0.0, - _BorderSize));
                float4 left = SAMPLE_TEXTURE2D_X(_OutlineMaskTexture, sampler_PointClamp, uv + texelSize * float2(-_BorderSize, 0.0));
                float4 rightUp = SAMPLE_TEXTURE2D_X(_OutlineMaskTexture, sampler_PointClamp, uv + texelSize * float2(_BorderSize, _BorderSize));
                float4 rightDown = SAMPLE_TEXTURE2D_X(_OutlineMaskTexture, sampler_PointClamp, uv + texelSize * float2(_BorderSize, -_BorderSize));
                float4 leftDown = SAMPLE_TEXTURE2D_X(_OutlineMaskTexture, sampler_PointClamp, uv + texelSize * float2(-_BorderSize, -_BorderSize));
                float4 leftUp = SAMPLE_TEXTURE2D_X(_OutlineMaskTexture, sampler_PointClamp, uv + texelSize * float2(-_BorderSize, _BorderSize));

                float4 center = SAMPLE_TEXTURE2D_X(_OutlineMaskTexture, sampler_PointClamp, uv);
                float4 cross = (right + left + up + down);
                float4 diagonal = (rightUp + rightDown + leftDown + leftUp);
                
                float4 insideFillMask = saturate(center * 1000);
                float4 expandedMask = saturate((center + cross + diagonal) * 1000);

                float4 color1 = expandedMask.r * _Colors[0];
                float4 color2 = expandedMask.g * _Colors[1];
                float4 color3 = expandedMask.b * _Colors[2];
                float4 color4 = expandedMask.a * _Colors[3];

                color1.a = lerp(color1.a, _FillAlphas.r, insideFillMask.r);
                color2.a = lerp(color2.a, _FillAlphas.g, insideFillMask.g);
                color3.a = lerp(color3.a, _FillAlphas.b, insideFillMask.b);
                color4.a = lerp(color4.a, _FillAlphas.a, insideFillMask.a);

                color1.rgb *= color1.a;
                color2.rgb *= color2.a;
                color3.rgb *= color3.a;
                color4.rgb *= color4.a;

                float4 finalColor = (color1 + color2 + color3 + color4) / max(1.0 , expandedMask.r + expandedMask.g + expandedMask.b + expandedMask.a);
                finalColor.a = Max3(color1.a, color2.a, max(color3.a, color4.a));;

                float4 cameraColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, uv);
                float3 composedColor = lerp(cameraColor.rgb, finalColor.rgb, finalColor.a);

                return float4(composedColor, cameraColor.a);
            }

            ENDHLSL
        }
    }

    Fallback Off
}