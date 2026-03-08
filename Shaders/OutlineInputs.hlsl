#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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

float4 FragR(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    return float4(1.0, 0.0, 0.0, 0.0);
}

float4 FragG(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    return float4(0.0, 1.0, 0.0, 0.0);
}

float4 FragB(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    return float4(0.0, 0.0, 1.0, 0.0);
}

float4 FragA(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    return float4(0.0, 0.0, 0.0, 1.0);
}