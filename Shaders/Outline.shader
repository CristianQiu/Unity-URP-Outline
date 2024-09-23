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
            Blend Off
            ColorMask R

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;

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
            Name "OutlineHorizontalBlur"
            
            ZTest Always
            ZWrite Off
            Cull Off
            Blend Off
            ColorMask R

            HLSLPROGRAM

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "./GaussianBlur.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag

            int _BlurKernelRadius;
            float _BlurStandardDeviation;

            float Frag(Varyings input) : SV_Target
            {
                return GaussianBlur(input.texcoord, float2(1.0, 0.0), _BlurKernelRadius, _BlurStandardDeviation, _BlitTexture, sampler_LinearClamp, _BlitTexture_TexelSize.xy).r;
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
            ColorMask R

            HLSLPROGRAM

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "./GaussianBlur.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag

            int _BlurKernelRadius;
            float _BlurStandardDeviation;

            float Frag(Varyings input) : SV_Target
            {
                return GaussianBlur(input.texcoord, float2(0.0, 1.0), _BlurKernelRadius, _BlurStandardDeviation, _BlitTexture, sampler_LinearClamp, _BlitTexture_TexelSize.xy).r;
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

            float4 _OutlineColor;
            float _OutlineFallOff;
            float _FillAlpha;

            float4 Frag(Varyings input) : SV_Target
            {
                // get the camera color, the original objects render mask, and the expanded (blurred) mask
                float4 cameraColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, input.texcoord);
                float mask = SAMPLE_TEXTURE2D_X(_OutlineRenderedObjectsMaskTexture, sampler_LinearClamp, input.texcoord).r;
                float blurredMask = SAMPLE_TEXTURE2D_X(_OutlineBlurredRenderedObjectsMaskTexture, sampler_LinearClamp, input.texcoord).r;

                // subtract the original mask to the blurred mask to resolve the outline
                float outlineAlpha = saturate(blurredMask - mask);

                // apply some sort of softness to the outline
                float outlineAlphaRemap = Remap(0.0, _OutlineFallOff, 0.0, 1.0, outlineAlpha);
                outlineAlpha = outlineAlpha > 0.0 ? (outlineAlpha > _OutlineFallOff ? 1.0 : outlineAlphaRemap) : outlineAlpha;

                // alpha can be used to fade out the final outline
                outlineAlpha *= _OutlineColor.a;

                // if mask is greater than 1.0, then use the fill alpha, otherwise keep the outline alpha
                outlineAlpha = lerp(outlineAlpha, _FillAlpha, step(1.0, mask));

                // finally blend the outline color with the existing camera color
                float3 composedColor = cameraColor.rgb * (1.0 - outlineAlpha) + (_OutlineColor.rgb * outlineAlpha);

                return float4(composedColor, cameraColor.a);
            }

            ENDHLSL
        }
    }

    Fallback Off
}