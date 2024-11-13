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
            Name "OutlineRenderObjectsR"
            
            ZTest Always
            ZWrite Off
            Cull Off
            Blend Off
            ColorMask R

            HLSLPROGRAM

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityDOTSInstancing.hlsl"

            // Needed to support the GPU resident drawer. 
            // Note that I have removed stuff that it seems I do not need.
            // See https://gamedev.center/how-to-write-a-custom-urp-shader-with-dots-instancing-support/
            #pragma target 4.5

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

            float Frag() : SV_Target
            {
                return 1.0;
            }

            ENDHLSL
        }

        Pass
        {
            Name "OutlineCombineMasks"
            
            ZTest Always
            ZWrite Off
            Cull Off
            Blend Off
            ColorMask RGBA

            HLSLPROGRAM

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag

            TEXTURE2D_X(_Outline_R);
            TEXTURE2D_X(_Outline_G);
            TEXTURE2D_X(_Outline_B);
            TEXTURE2D_X(_Outline_A);

            float4 Frag(Varyings input) : SV_Target
            {
                float r = SAMPLE_TEXTURE2D_X(_Outline_R, sampler_PointClamp, input.texcoord);
                float g = SAMPLE_TEXTURE2D_X(_Outline_G, sampler_PointClamp, input.texcoord);
                float b = SAMPLE_TEXTURE2D_X(_Outline_B, sampler_PointClamp, input.texcoord);
                float a = SAMPLE_TEXTURE2D_X(_Outline_A, sampler_PointClamp, input.texcoord);

                return float4(r, g, b, a);
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
            ColorMask RGBA

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
                return GaussianBlur(input.texcoord, float2(1.0, 0.0), _BlurKernelRadius, _BlurStandardDeviation, _BlitTexture, sampler_LinearClamp, _BlitTexture_TexelSize.xy);
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
            ColorMask RGBA

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
                return GaussianBlur(input.texcoord, float2(0.0, 1.0), _BlurKernelRadius, _BlurStandardDeviation, _BlitTexture, sampler_LinearClamp, _BlitTexture_TexelSize.xy);
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

            TEXTURE2D_X(_OutlineRenderedObjectsMaskTexture);
            TEXTURE2D_X(_OutlineBlurredRenderedObjectsMaskTexture);
            
            SAMPLER(sampler_BlitTexture);

            float4 _OutlineColors[] = 
            {
                float4(1.0, 1.0, 1.0, 1.0),
                float4(1.0, 1.0, 1.0, 1.0),
                float4(1.0, 1.0, 1.0, 1.0),
                float4(1.0, 1.0, 1.0, 1.0),
            };
            float4 _OutlineFallOffs;
            float4 _FillAlphas;

            float4 Remap4(float4 origFrom, float4 origTo, float4 targetFrom, float4 targetTo, float4 value)
            {
                return lerp(targetFrom, targetTo, (value - origFrom) / (origTo - origFrom));
            }

            float4 Frag(Varyings input) : SV_Target
            {
                // get the camera color, the original objects render mask, and the expanded (blurred) mask
                float4 cameraColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, input.texcoord);
                float4 mask = SAMPLE_TEXTURE2D_X(_OutlineRenderedObjectsMaskTexture, sampler_LinearClamp, input.texcoord);
                float4 blurredMask = SAMPLE_TEXTURE2D_X(_OutlineBlurredRenderedObjectsMaskTexture, sampler_LinearClamp, input.texcoord);

                // subtract the original mask to the blurred mask to resolve the outline
                float4 outlineAlphas = saturate(blurredMask - mask);

                // apply some sort of softness to the outline
                float4 outlineAlphaRemap = Remap4(0.0, _OutlineFallOffs, 0.0, 1.0, outlineAlphas);
                outlineAlphas = outlineAlphas > 0.0 ? (outlineAlphas > _OutlineFallOffs ? 1.0 : outlineAlphaRemap) : outlineAlphas;

                // alpha can be used to fade out the final outline
                outlineAlphas *= float4(_OutlineColors[0].a, _OutlineColors[1].a, _OutlineColors[2].a, _OutlineColors[3].a);

                // if mask is greater than 1.0, then use the fill alpha, otherwise keep the outline alpha
                outlineAlphas = lerp(outlineAlphas, _FillAlphas, step(1.0, mask));

                float maxAlpha = Max3(outlineAlphas.x, outlineAlphas.y, max(outlineAlphas.z, outlineAlphas.w));

                float3 layer1Color = _OutlineColors[0].rgb * step(0.0, blurredMask.r) * outlineAlphas.r;
                float3 layer2Color = _OutlineColors[1].rgb * step(0.0, blurredMask.g) * outlineAlphas.g;
                float3 layer3Color = _OutlineColors[2].rgb * step(0.0, blurredMask.b) * outlineAlphas.b;
                float3 layer4Color = _OutlineColors[3].rgb * step(0.0, blurredMask.a) * outlineAlphas.a;

                float3 layersSumColor = layer1Color + layer2Color + layer3Color + layer4Color;

                float3 composedColor = cameraColor.rgb * (1.0 - maxAlpha) + (layersSumColor * maxAlpha);

                // float3 outlineAndFillColor = (_OutlineColors[0].rgb) + (_OutlineColors[1].rgb) + (_OutlineColors[2].rgb) + (_OutlineColors[3].rgb);
                // float4 result = float4(outlineAndFillColor * maxAlpha, 1.0);

                // // finally blend the outline color with the existing camera color
                // float3 composedColor = cameraColor.rgb * (1.0.xxx - outlineAlphas.rgb) + (_OutlineColors[0].rrr * outlineAlphas.rgb);

                return float4(composedColor, cameraColor.a);
            }

            ENDHLSL
        }
    }

    Fallback Off
}