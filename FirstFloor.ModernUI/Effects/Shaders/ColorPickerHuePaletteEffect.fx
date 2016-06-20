#include "Include/HSV.fx"
float hue : register(c0);
float4 main(float2 uv : TEXCOORD) : COLOR {
	return float4(HSVtoRGB(float3(hue, uv.x, 1.0 - uv.y)), 1.0);
}