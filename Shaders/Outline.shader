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
            Cull Back
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

                float4 maskCenter = SAMPLE_TEXTURE2D_X(_OutlineMaskTexture, sampler_PointClamp, uv);

                float4 maskRight = SAMPLE_TEXTURE2D_X(_OutlineMaskTexture, sampler_PointClamp, uv + texelSize * float2(_BorderSize, 0.0));
                float4 maskLeft = SAMPLE_TEXTURE2D_X(_OutlineMaskTexture, sampler_PointClamp, uv + texelSize * float2(-_BorderSize, 0.0));
                float4 maskUp = SAMPLE_TEXTURE2D_X(_OutlineMaskTexture, sampler_PointClamp, uv + texelSize * float2(0.0, _BorderSize));
                float4 maskDown = SAMPLE_TEXTURE2D_X(_OutlineMaskTexture, sampler_PointClamp, uv + texelSize * float2(0.0, - _BorderSize));

                float4 maskCross = maskRight + maskLeft + maskUp + maskDown;

                float4 maskRightUp = SAMPLE_TEXTURE2D_X(_OutlineMaskTexture, sampler_PointClamp, uv + texelSize * float2(_BorderSize, _BorderSize));
                float4 maskRightDown = SAMPLE_TEXTURE2D_X(_OutlineMaskTexture, sampler_PointClamp, uv + texelSize * float2(_BorderSize, -_BorderSize));
                float4 maskLeftDown = SAMPLE_TEXTURE2D_X(_OutlineMaskTexture, sampler_PointClamp, uv + texelSize * float2(-_BorderSize, -_BorderSize));
                float4 maskLeftUp = SAMPLE_TEXTURE2D_X(_OutlineMaskTexture, sampler_PointClamp, uv + texelSize * float2(-_BorderSize, _BorderSize));

                float4 maskDiagonal = maskRightUp + maskRightDown + maskLeftDown + maskLeftUp;

                float4 rgbaMask = step(0.0001, maskCross + maskDiagonal) - maskCenter;

                float4 color1 = rgbaMask.r * _Colors[0];
                float4 color2 = rgbaMask.g * _Colors[1];
                float4 color3 = rgbaMask.b * _Colors[2];
                float4 color4 = rgbaMask.a * _Colors[3];

                float4 colorResult = color1 + color2 + color3 + color4;
                
                return colorResult;

                //float4 cameraColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, uv);
            }

            ENDHLSL
        }
    }

    Fallback Off
}