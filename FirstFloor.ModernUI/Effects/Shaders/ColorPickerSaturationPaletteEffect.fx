#include "Include/HSV.fx"
float saturation : register(c0);
float4 main(float2 uv : TEXCOORD) : COLOR {
	return float4(HSVtoRGB(float3(uv.x, saturation, 1.0 - uv.y)), 1.0);
}