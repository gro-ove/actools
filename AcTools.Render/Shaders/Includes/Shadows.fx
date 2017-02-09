/*
  Before including this shader, don’t forget to specify the number of splits
  and the size of shadow map.

  #define NUM_SPLITS 1
  #define SHADOW_MAP_SIZE 2048
*/

cbuffer cbShadowsBuffer {
	matrix gShadowViewProj[NUM_SPLITS];
}

Texture2D gShadowMaps[NUM_SPLITS];

SamplerComparisonState samShadow {
	Filter = COMPARISON_MIN_MAG_MIP_LINEAR;
	AddressU = BORDER;
	AddressV = BORDER;
	AddressW = BORDER;
	BorderColor = float4(1.0f, 1.0f, 1.0f, 0.0f);
	ComparisonFunc = LESS;
};

#define _SHADOW_MAP_DX (1.0 / SHADOW_MAP_SIZE)

float GetShadowInner(Texture2D tex, float3 uv) {
	// uv: only float3 is required
	float shadow = 0.0, x, y;
	for (y = -1.5; y <= 1.5; y += 1.0)
		for (x = -1.5; x <= 1.5; x += 1.0)
			shadow += tex.SampleCmpLevelZero(samShadow, uv.xy + float2(x, y) * _SHADOW_MAP_DX, uv.z).r;
	// return shadow / 16.0;
	return saturate((shadow / 16 - 0.5) * 4 + 0.5);
}

#if NUM_SPLITS == 1
float GetShadow(float3 position) {
	float4 uv = mul(float4(position, 1.0), gShadowViewProj[0]);
	return GetShadowInner(gShadowMaps[0], uv.xyz / uv.w);
}
#else
#define SHADOW_A 0.0001
#define SHADOW_Z 0.9999

float GetShadow(float3 position) {
	float4 pos = float4(position, 1.0), uv, nv;

	uv = mul(pos, gShadowViewProj[NUM_SPLITS - 1]);
	uv.xyz /= uv.w;
	if (uv.x < SHADOW_A || uv.x > SHADOW_Z || uv.y < SHADOW_A || uv.y > SHADOW_Z)
		return 1;

	[flatten]
	for (int i = NUM_SPLITS - 1; i > 0; i--) {
		nv = mul(pos, gShadowViewProj[i - 1]);
		nv.xyz /= nv.w;
		if (nv.x < SHADOW_A || nv.x > SHADOW_Z || nv.y < SHADOW_A || nv.y > SHADOW_Z)
			return GetShadowInner(gShadowMaps[i], uv.xyz);
		uv = nv;
	}

	return GetShadowInner(gShadowMaps[0], nv.xyz);
}
#endif