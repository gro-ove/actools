#include "Common.fx"

Texture2D gFlatMirrorDepthMap;
Texture2D gFlatMirrorNormalsMap;
Texture2D gMapsMap;

static const int SAMPLE_COUNT = 15;

cbuffer cbPerFrame : register(b0) {
	float2 gSampleOffsets[SAMPLE_COUNT];
	float gSampleWeights[SAMPLE_COUNT];
	float gPower;
	matrix gWorldViewProjInv;
	float4 gScreenSize;
}

// default
float4 ps_GaussianBlurDebug(PS_IN pin) : SV_Target {
	return tex(pin.Tex);
}

float4 ps_GaussianBlur(PS_IN pin) : SV_Target {
	float4 c = 0;
	for (int i = 0; i < SAMPLE_COUNT; i++){
		c += tex(pin.Tex + gSampleOffsets[i] * gPower) * gSampleWeights[i];
	}
	return c;
}

technique10 GaussianBlur {
	pass P0 {
		SetVertexShader( CompileShader( vs_4_0, vs_main() ) );
		SetGeometryShader( NULL );
		SetPixelShader( CompileShader( ps_4_0, ps_GaussianBlur() ) );
	}
}

// special flat mirror blur
SamplerState samPoint {
	Filter = MIN_MAG_MIP_POINT;
	AddressU = CLAMP;
	AddressV = CLAMP;
};

float GetDepth(float2 uv) {
	return gFlatMirrorDepthMap.Sample(samPoint, uv).x;
}

float GetPosition(float2 uv) {
	float4 position = mul(float4(uv.x * 2 - 1, -(uv.y * 2 - 1), GetDepth(uv), 1), gWorldViewProjInv);
	return saturate(-position.y / position.w);
}

float4 ps_FlatMirrorBlur(PS_IN pin) : SV_Target {
	float p = GetPosition(pin.Tex);
	float4 c = gInputMap.Sample(samPoint, pin.Tex) / 17.0;

	float x, y;

	[flatten]
	for (x = -0.6; x < 0.61; x += 0.4) {
		[flatten]
		for (y = -0.6; y < 0.61; y += 0.4) {
			float2 delta = float2(x, y) * gScreenSize.zw * gPower;
			float mu = p;

			[flatten]
			for (int j = 0; j < 3; j++) {
				float np = GetPosition(pin.Tex + delta * mu);
				mu = min((np * 2 + mu) / 3, mu);
			}

			c += min(gInputMap.Sample(samPoint, pin.Tex + delta * mu), 1.2) / 17.0;
		}
	}

	return c;
}

technique10 FlatMirrorBlur {
	pass P0 {
		SetVertexShader( CompileShader( vs_4_0, vs_main() ) );
		SetGeometryShader( NULL );
		SetPixelShader( CompileShader( ps_4_0, ps_FlatMirrorBlur() ) );
	}
}

// special reflection mode
float4 ps_ReflectionGaussianBlur(PS_IN pin) : SV_Target {
	float power = saturate(1 - tex(gMapsMap, pin.Tex).y * 15);

	float4 c = 0;
	for (int i = 0; i < SAMPLE_COUNT; i++){
		c += tex(pin.Tex + gSampleOffsets[i] * power) * gSampleWeights[i];
	}

	return c;
}

technique10 ReflectionGaussianBlur {
	pass P0 {
		SetVertexShader( CompileShader( vs_4_0, vs_main() ) );
		SetGeometryShader( NULL );
		SetPixelShader( CompileShader( ps_4_0, ps_ReflectionGaussianBlur() ) );
	}
}