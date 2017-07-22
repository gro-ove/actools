#include "Include/HSV.fx"
sampler2D implicitInput : register(s0);

float4 main(float2 uv : TEXCOORD) : COLOR {
    float4 color = tex2D(implicitInput, uv);
    if (color.a == 0.0) return (float4)0.0;
    float3 hsv = RGBtoHSV(color.rgb / color.a);
    hsv.z = (1.0 - hsv.z) * (1.0 - hsv.y) + hsv.z * hsv.y;
    return float4(HSVtoRGB(hsv) * color.a, color.a);
}

