#include "Include/HSV.fx"
float brightness : register(c0);
float4 main(float2 uv : TEXCOORD) : COLOR {
	return float4(HSVtoRGB(float3(uv.x, 1.0 - uv.y, brightness)), 1.0);
}