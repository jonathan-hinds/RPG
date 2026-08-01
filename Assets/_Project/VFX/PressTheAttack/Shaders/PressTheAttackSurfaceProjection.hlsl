#ifndef PRESS_THE_ATTACK_SURFACE_PROJECTION_INCLUDED
#define PRESS_THE_ATTACK_SURFACE_PROJECTION_INCLUDED

float2 PTARotateUv(float2 uv, float radians)
{
    float sineValue;
    float cosineValue;
    sincos(radians, sineValue, cosineValue);
    uv -= 0.5;
    uv = mul(float2x2(cosineValue, -sineValue, sineValue, cosineValue), uv);
    return uv + 0.5;
}

float3 PTATriplanarWeights(float3 normalOS)
{
    // A sharp blend keeps thin authored bolts from being averaged away on
    // rounded surfaces while retaining a narrow, soft transition at seams.
    float3 weights = pow(abs(normalize(normalOS)), 10.0);
    return weights / max(dot(weights, 1.0), 0.0001);
}

half4 PTASampleTriplanar(
    TEXTURE2D_PARAM(textureValue, samplerValue),
    float3 normalizedPosition,
    float3 normalOS,
    float2 tiling,
    float2 offset,
    float rotation)
{
    float3 weights = PTATriplanarWeights(normalOS);

    // Each face receives a complete, square projection of the authored texture.
    // Axis-specific rotations keep branching motifs from resolving into one
    // shared vertical direction as they wrap around a humanoid silhouette.
    float2 uvX = PTARotateUv(normalizedPosition.zy, rotation + 1.0472) * tiling + offset;
    float2 uvY = PTARotateUv(normalizedPosition.xz, rotation - 0.7854) * tiling + offset.yx;
    float2 uvZ = PTARotateUv(normalizedPosition.xy, rotation) * tiling + offset;

    uvX.x *= normalOS.x < 0.0 ? -1.0 : 1.0;
    uvY.x *= normalOS.y < 0.0 ? -1.0 : 1.0;
    uvZ.x *= normalOS.z >= 0.0 ? -1.0 : 1.0;

    half4 sampleX = SAMPLE_TEXTURE2D(textureValue, samplerValue, uvX);
    half4 sampleY = SAMPLE_TEXTURE2D(textureValue, samplerValue, uvY);
    half4 sampleZ = SAMPLE_TEXTURE2D(textureValue, samplerValue, uvZ);
    return sampleX * weights.x + sampleY * weights.y + sampleZ * weights.z;
}

#endif
