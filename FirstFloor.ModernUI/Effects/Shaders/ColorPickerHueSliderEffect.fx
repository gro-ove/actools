#include "Include/HSV.fx"

float4 main(float2 uv : TEXCOORD) : COLOR {
	return float4(HSVtoRGB(float3(1.0 - uv.y, 1.0, 1.0)), 1.0);
}
