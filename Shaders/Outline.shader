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
            Blend One One

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
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

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
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

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

            SAMPLER(sampler_BlitTexture);

            float4 _Colors[] = 
            {
                float4(1.0, 1.0, 1.0, 1.0),
                float4(1.0, 1.0, 1.0, 1.0),
                float4(1.0, 1.0, 1.0, 1.0),
                float4(1.0, 1.0, 1.0, 1.0),
            };
            float4 _FallOffs;
            float4 _FillAlphas;

            TEXTURE2D_X(_RenderedObjectsMaskTexture);
            TEXTURE2D_X(_BlurredRenderedObjectsMaskTexture);

            float4 Remap4(float4 origFrom, float4 origTo, float4 targetFrom, float4 targetTo, float4 value)
            {
                return lerp(targetFrom, targetTo, (value - origFrom) / (origTo - origFrom));
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float4 cameraColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, input.texcoord);
                float4 mask = SAMPLE_TEXTURE2D_X(_RenderedObjectsMaskTexture, sampler_PointClamp, input.texcoord);
                float4 blurredMask = SAMPLE_TEXTURE2D_X(_BlurredRenderedObjectsMaskTexture, sampler_PointClamp, input.texcoord);

                float4 outline = saturate(blurredMask - mask);

                float4 isFill = step(0.000001, mask);
                float4 isOutline = step(0.000001, outline);
                float4 isFillAndOutline = blurredMask;

                //float4 alphas = lerp()

                // calculate each layer color individually
                float3 color1 = _Colors[0].rgb * isFillAndOutline * outline.r;
                float3 color2 = _Colors[1].rgb * isFillAndOutline * outline.g;
                float3 color3 = _Colors[2].rgb * isFillAndOutline * outline.b;
                float3 color4 = _Colors[3].rgb * isFillAndOutline * outline.a;

                return isOutline;

                // apply some sort of softness to the outline
                float4 outlineAlphaRemap = Remap4(0.0, _FallOffs, 0.0, 1.0, outline);

                return outlineAlphaRemap;

                outline = outline > 0.0 ? (outline > _FallOffs ? 1.0 : outlineAlphaRemap) : outline;

                // alpha can be used to fade out the final outline
                outline *= float4(_Colors[0].a, _Colors[1].a, _Colors[2].a, _Colors[3].a);

                // if mask is greater than 1.0, then use the fill alpha, otherwise keep the outline alpha
                outline = lerp(outline, _FillAlphas, step(1.0, mask));

                // calculate the maximum alpha for when several layers intersect on screen
                float maxAlpha = Max3(outline.x, outline.y, max(outline.z, outline.w));

                // calculate each layer color individually
                float3 layer1Color = _Colors[0].rgb * step(0.0, blurredMask.r) * outline.r;
                float3 layer2Color = _Colors[1].rgb * step(0.0, blurredMask.g) * outline.g;
                float3 layer3Color = _Colors[2].rgb * step(0.0, blurredMask.b) * outline.b;
                float3 layer4Color = _Colors[3].rgb * step(0.0, blurredMask.a) * outline.a;

                // calculate the total color for when the layers overlap
                float3 layersSumColor = layer1Color + layer2Color + layer3Color + layer4Color;

                // blend the color with the background 
                float3 composedColor = cameraColor.rgb * (1.0 - maxAlpha) + (layersSumColor * maxAlpha);

                return float4(composedColor, cameraColor.a);
            }

            ENDHLSL
        }
    }

    Fallback Off
}