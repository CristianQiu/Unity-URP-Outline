Shader "Hidden/Outline"
{
    SubShader
    {
        Tags
        { 
            "RenderPipeline" = "UniversalPipeline"
        }

        UsePass "Hidden/Universal Render Pipeline/ObjectMotionVectorFallback/MOTIONVECTORS"

        Pass
        {
            Name "OutlineRenderObjectsR"
            
            ZTest Always
            ZWrite Off
            Cull Off
            Blend One One

            HLSLPROGRAM

            // Needed to support the GPU resident drawer. 
            // Note that I have removed stuff that it seems I do not need.
            // See https://gamedev.center/how-to-write-a-custom-urp-shader-with-dots-instancing-support/
            #pragma target 4.5

            #include "OutlineInputs.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityDOTSInstancing.hlsl"

            #pragma multi_compile _ DOTS_INSTANCING_ON

            #pragma vertex Vert
            #pragma fragment FragR

            ENDHLSL
        }

        Pass
        {
            Name "OutlineRenderObjectsG"
            
            ZTest Always
            ZWrite Off
            Cull Off
            Blend One One

            HLSLPROGRAM

            #pragma target 4.5

            #include "OutlineInputs.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityDOTSInstancing.hlsl"

            #pragma multi_compile _ DOTS_INSTANCING_ON

            #pragma vertex Vert
            #pragma fragment FragG

            ENDHLSL
        }

        Pass
        {
            Name "OutlineRenderObjectsB"
            
            ZTest Always
            ZWrite Off
            Cull Off
            Blend One One

            HLSLPROGRAM

            #pragma target 4.5

            #include "OutlineInputs.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityDOTSInstancing.hlsl"

            #pragma multi_compile _ DOTS_INSTANCING_ON

            #pragma vertex Vert
            #pragma fragment FragB

            ENDHLSL
        }

        Pass
        {
            Name "OutlineRenderObjectsA"
            
            ZTest Always
            ZWrite Off
            Cull Off
            Blend One One

            HLSLPROGRAM

            #pragma target 4.5

            #include "OutlineInputs.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityDOTSInstancing.hlsl"

            #pragma multi_compile _ DOTS_INSTANCING_ON

            #pragma vertex Vert
            #pragma fragment FragA

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
            
            SAMPLER(sampler_BlitTexture);
            float _BorderSize;

            float4 _OutlineColors[] = 
            {
                float4(1.0, 1.0, 1.0, 1.0),
                float4(1.0, 1.0, 1.0, 1.0),
                float4(1.0, 1.0, 1.0, 1.0),
                float4(1.0, 1.0, 1.0, 1.0),
            };
            float4 _FillAlphas;

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                float2 texelSize = _BlitTexture_TexelSize.xy;

                float4 maskCenter = SAMPLE_TEXTURE2D_X(_OutlineRenderedObjectsMaskTexture, sampler_PointClamp, uv);

                float4 maskRight = SAMPLE_TEXTURE2D_X(_OutlineRenderedObjectsMaskTexture, sampler_PointClamp, uv + texelSize * float2(_BorderSize, 0.0));
                float4 maskLeft = SAMPLE_TEXTURE2D_X(_OutlineRenderedObjectsMaskTexture, sampler_PointClamp, uv + texelSize * float2(-_BorderSize, 0.0));
                float4 maskUp = SAMPLE_TEXTURE2D_X(_OutlineRenderedObjectsMaskTexture, sampler_PointClamp, uv + texelSize * float2(0.0, _BorderSize));
                float4 maskDown = SAMPLE_TEXTURE2D_X(_OutlineRenderedObjectsMaskTexture, sampler_PointClamp, uv + texelSize * float2(0.0, - _BorderSize));

                float4 maskCross = maskRight + maskLeft + maskUp + maskDown;

                float4 mask1 = SAMPLE_TEXTURE2D_X(_OutlineRenderedObjectsMaskTexture, sampler_PointClamp, uv + texelSize * float2(_BorderSize, _BorderSize));
                float4 mask2 = SAMPLE_TEXTURE2D_X(_OutlineRenderedObjectsMaskTexture, sampler_PointClamp, uv + texelSize * float2(-_BorderSize, -_BorderSize));
                float4 mask3 = SAMPLE_TEXTURE2D_X(_OutlineRenderedObjectsMaskTexture, sampler_PointClamp, uv + texelSize * float2(_BorderSize, -_BorderSize));
                float4 mask4 = SAMPLE_TEXTURE2D_X(_OutlineRenderedObjectsMaskTexture, sampler_PointClamp, uv + texelSize * float2(-_BorderSize, +_BorderSize));

                float4 maskDiagonal = mask1 + mask2 + mask3 + mask4;

                float4 rgbaMask = step(0.0001, maskCross) - maskCenter;

                float4 rColor = rgbaMask.r * _OutlineColors[0];
                float4 gColor = rgbaMask.g * _OutlineColors[1];
                float4 bColor = rgbaMask.b * _OutlineColors[2];
                float4 aColor = rgbaMask.a * _OutlineColors[3];

                float4 colorResult = rColor + gColor + bColor + aColor;
                
                return colorResult;

                float4 cameraColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, input.texcoord);
            }

            ENDHLSL
        }
    }

    Fallback Off
}