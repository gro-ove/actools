sampler2D implicitInput : register(s0);
float4 main(float2 uv : TEXCOORD) : COLOR {
    float4 color = tex2D(implicitInput, uv);
    return float4((float3)1.0 - color.rgb, color.a);
}

