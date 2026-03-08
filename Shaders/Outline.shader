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
            Name "OutlineRenderObjects"
            
            ZTest Always
            ZWrite Off
            Cull Off
            BlendOp Add
            Blend One One Blend One One

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

            float4 _MaskColor;

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

                return _MaskColor;
            }

            ENDHLSL
        }

        Pass
        {
            Name "OutlineHorizontalBlur"
            
            ZTest Always
            ZWrite Off
            Cull Off
            Blend Off

            HLSLPROGRAM

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "./GaussianBlur.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag

            int _BlurKernelRadius;
            float _BlurStandardDeviation;

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                return GaussianBlur(input.texcoord, float2(1.0, 0.0), _BlurKernelRadius, _BlurStandardDeviation, _BlitTexture, sampler_PointClamp, _BlitTexture_TexelSize.xy);
            }

            ENDHLSL
        }

        Pass
        {
            Name "OutlineVerticalBlur"
            
            ZTest Always
            ZWrite Off
            Cull Off
            Blend Off

            HLSLPROGRAM

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "./GaussianBlur.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag

            int _BlurKernelRadius;
            float _BlurStandardDeviation;

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                return GaussianBlur(input.texcoord, float2(0.0, 1.0), _BlurKernelRadius, _BlurStandardDeviation, _BlitTexture, sampler_PointClamp, _BlitTexture_TexelSize.xy);
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

            float4 _Colors[] = 
            {
                float4(1.0, 1.0, 1.0, 1.0),
                float4(1.0, 1.0, 1.0, 1.0),
                float4(1.0, 1.0, 1.0, 1.0)
            };
            float3 _FallOffs;
            float3 _FillAlphas;

            TEXTURE2D_X(_BlurredRenderedObjectsMaskTexture);

            float3 Remap3(float3 origFrom, float3 origTo, float3 targetFrom, float3 targetTo, float3 value)
            {
                return lerp(targetFrom, targetTo, saturate((value - origFrom) / (origTo - origFrom)));
            }

            int DecodeFillMask(float alphaSample, int bitIndex)
            {
                int alpha255 = (int)round(alphaSample * 255.0);
                
                int mask = ((alpha255 >> bitIndex) & 1);

                return mask;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // get the camera color and the blurred mask, which has the fill masks for each color channel encoded in the alpha channel 
                float4 cameraColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, input.texcoord);
                float4 blurredMask = SAMPLE_TEXTURE2D_X(_BlurredRenderedObjectsMaskTexture, sampler_PointClamp, input.texcoord);

                // calculate the outline mask with softened edges
                float3 outlineSoftMask = blurredMask.rgb - blurredMask.aaa;

                // remap the mask to apply stronger falloffs to the outline
                float3 outlineSoftMaskRemap = Remap3(0.0, _FallOffs, 0.0, 1.0, outlineSoftMask);

                // get the unsoftened masks for the fill and the outline
                float isFill = blurredMask.a;
                float3 isOutline = step(0.000001, outlineSoftMask);
                float3 isFillAndOutline = step(0.000001, blurredMask.rgb);

                // apply alpha to the outline from the outline colors
                float3 outlineColorsAlphas = float3(_Colors[0].a, _Colors[1].a, _Colors[2].a);
                outlineSoftMaskRemap *= outlineColorsAlphas;

                // decode the alpha channel to get the mask per each color channel
                float rMaskNoBlur = DecodeFillMask(blurredMask.a, 0);
                float gMaskNoBlur = DecodeFillMask(blurredMask.a, 1);
                float bMaskNoBlur = DecodeFillMask(blurredMask.a, 2);

                return float4(blurredMask.aaa, 1.0);

                // calculate each outline color layer individually
                float3 colorOutline1 = _Colors[0].rgb * (/* isFillAndOutline.r * */ outlineSoftMaskRemap.r);
                float3 colorOutline2 = _Colors[1].rgb * (/* isFillAndOutline.g * */ outlineSoftMaskRemap.g);
                float3 colorOutline3 = _Colors[2].rgb * (/* isFillAndOutline.b * */ outlineSoftMaskRemap.b);

                return float4(colorOutline1 + colorOutline2 + colorOutline3, 1.0);

                // and now the fills
                float3 colorFill1 = _Colors[0].rgb * (blurredMask.r * _FillAlphas.r);
                float3 colorFill2 = _Colors[1].rgb * (blurredMask.g * _FillAlphas.g);
                float3 colorFill3 = _Colors[2].rgb * (blurredMask.b * _FillAlphas.b);

                // choose the color depending on if we are at an outline or in fill
                float3 color1 = lerp(colorOutline1, colorFill1, isFill);
                float3 color2 = lerp(colorOutline2, colorFill2, isFill);
                float3 color3 = lerp(colorOutline3, colorFill3, isFill);

                 // calculate the maximum alpha for when several layers intersect on screen
                float maxAlpha = Max3(outlineSoftMaskRemap.r, outlineSoftMaskRemap.g, outlineSoftMaskRemap.b);

                return float4(color1 + color2 + color3, 1.0);

                // // blend the color with the background 
                // float3 composedColor = cameraColor.rgb * (1.0 - maxAlpha) + (layersSumColor * maxAlpha);

                // return float4(composedColor, cameraColor.a);
            }

            ENDHLSL
        }
    }

    Fallback Off
}