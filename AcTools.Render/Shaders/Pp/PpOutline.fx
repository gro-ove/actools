#include "Common.fx"

Texture2D gDepthMap;

cbuffer cbPerObject : register(b0) {
	float4 gScreenSize;
}

#define THRESHOLD 0.99999

float4 ps_Outline(PS_IN pin) : SV_Target {
	float val = tex(gDepthMap, pin.Tex);
	if (val < THRESHOLD) return 0.0;

	float result = 0;
	for (float x = -3.5; x <= 3.5; x++) {
		for (float y = -3.5; y <= 3.5; y++) {
			result += tex(gDepthMap, pin.Tex + float2(x, y) * gScreenSize.zw) < THRESHOLD;
		}
	}

	return float4(1.0, 1.0, 1.0, saturate(result * 0.03));
}

technique10 Outline {
	pass P0 {
		SetVertexShader( CompileShader( vs_4_0, vs_main() ) );
		SetGeometryShader( NULL );
		SetPixelShader( CompileShader( ps_4_0, ps_Outline() ) );
	}
}
