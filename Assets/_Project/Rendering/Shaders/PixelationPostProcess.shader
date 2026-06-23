Shader "Hidden/RPG Clone/Pixelation Post Process"
{
    HLSLINCLUDE
        #pragma target 3.5

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        float _PixelAmount;

        half4 Frag(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            float blockSize = max(1.0, round(_PixelAmount));
            float2 sourceSize = max(_BlitTexture_TexelSize.zw, 1.0.xx);
            float2 pixelCoord = input.texcoord * sourceSize;
            float2 pixelatedCoord = (floor(pixelCoord / blockSize) + 0.5) * blockSize;
            float2 pixelatedUv = saturate(pixelatedCoord / sourceSize);

            return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, pixelatedUv);
        }
    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Pixelation"

            ZTest Always
            ZWrite Off
            Cull Off
            Blend Off

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment Frag
            ENDHLSL
        }
    }

    Fallback Off
}
