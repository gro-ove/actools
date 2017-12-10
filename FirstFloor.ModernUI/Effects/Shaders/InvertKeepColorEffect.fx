// Basically, does this line (in HSV space):
// value = (1.0 - value) * (1.0 - saturation) + value * saturation;

sampler2D implicitInput : register(s0);

float4 main(float2 uv : TEXCOORD) : COLOR {
    float4 i = tex2D(implicitInput, uv);
    float3 v = i.rgb / (i.a + 1e-10);
    float x = max(v.r, max(v.g, v.b));
    float m = min(v.r, min(v.g, v.b));
    float k = 1 + (1 / x - 2) * m / x;
    return float4(x == 0 ? i.aaa : i.rgb * k, i.a);
}

// approximately 16 instruction slots used (1 texture, 15 arithmetic)
