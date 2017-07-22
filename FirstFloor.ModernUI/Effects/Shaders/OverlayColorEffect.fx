sampler2D implicitInput : register(s0);
float4 overlayColor : register(c0);

float4 main(float2 uv : TEXCOORD) : COLOR {
    float4 loaded = tex2D(implicitInput, uv);
    if (loaded.a == 0.0) return (float4)0.0;

    float3 color = loaded.rgb / loaded.a;

    float4 result;
    result.r = lerp(color.r, overlayColor.r, overlayColor.a);
    result.g = lerp(color.g, overlayColor.g, overlayColor.a);
    result.b = lerp(color.b, overlayColor.b, overlayColor.a);
    result.a = loaded.a;
    result.rgb *= loaded.a;
    return result;
}
