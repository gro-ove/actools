sampler2D implicitInput : register(s0);
float4 overlayColor : register(c0);

float4 main(float2 uv : TEXCOORD) : COLOR {
    return overlayColor * tex2D(implicitInput, uv).r;
}
