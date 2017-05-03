/*
  To disable dynamic branching, you can specify fixed values before including
  this file:

  define NUM_SPLITS 1
  define SHADOW_MAP_SIZE 2048
*/

#ifndef NUM_SPLITS
#define MAX_NUM_SPLITS 3
#else
#define MAX_NUM_SPLITS NUM_SPLITS
#endif

cbuffer cbShadowsBuffer {
	matrix gShadowViewProj[MAX_NUM_SPLITS];
	float4 gPcssScale[MAX_NUM_SPLITS];

	int gNumSplits;
	bool gPcssEnabled;
	float2 gShadowMapSize;
}

Texture2D gShadowMaps[MAX_NUM_SPLITS];

#ifndef SHADOW_MAP_SIZE
#define SHADOW_MAP_SIZE gShadowMapSize.x
#define SHADOW_MAP_DX gShadowMapSize.y
#else
#define SHADOW_MAP_DX (1.0 / SHADOW_MAP_SIZE)
#endif

#if ENABLE_SHADOWS != 1
	float GetShadow(float3 position) {
		return 1.0;
	}
#else
	SamplerComparisonState samShadow {
		Filter = COMPARISON_MIN_MAG_MIP_LINEAR;
		AddressU = BORDER;
		AddressV = BORDER;
		AddressW = BORDER;
		BorderColor = float4(1.0f, 1.0f, 1.0f, 1.0f);
		ComparisonFunc = LESS;
	};

	float GetShadowSmooth(Texture2D tex, float shadowMapSize, float3 uv) {
		// uv: only float3 is required
		float shadow = 0.0, x, y;
		for (y = -1.5; y <= 1.501; y += 1.0)
			for (x = -1.5; x <= 1.501; x += 1.0)
				shadow += tex.SampleCmpLevelZero(samShadow, uv.xy + float2(x, y) * shadowMapSize, uv.z).r;
		// return shadow / 16.0;
		return saturate((shadow / 16 - 0.5) * 4 + 0.5);
	}

	float GetShadowFast(Texture2D tex, float3 uv) {
		return tex.SampleCmpLevelZero(samShadow, uv.xy, uv.z).r;
	}

	float GetShadowSmooth(Texture2D tex, float3 uv) {
		return GetShadowSmooth(tex, SHADOW_MAP_DX, uv);
	}

	#if ENABLE_PCSS == 1
		#include "Shadows.PCSS.fx"
		float GetShadowInner(const int cascadeIndex, float3 uv) {
			Texture2D tex = gShadowMaps[cascadeIndex];

			if (gPcssEnabled) {
				float4 scale = gPcssScale[cascadeIndex];
				return PCSS(tex, uv, scale.x, scale.y);
			} else {
				return GetShadowSmooth(tex, uv);
			}
		}
	#else
		float GetShadowInner(const int cascadeIndex, float3 uv) {
			Texture2D tex = gShadowMaps[cascadeIndex];
			return GetShadowSmooth(tex, uv);
		}
	#endif

	// Skip translation

	#define SHADOW_A 0.03
	#define SHADOW_Z 0.97

	#ifndef NUM_SPLITS
		float GetShadow(float3 position) {
			if (gNumSplits <= 0) return 1.0 + gNumSplits;

			float4 pos = float4(position, 1.0), uv, nv;

			uv = mul(pos, gShadowViewProj[gNumSplits - 1]);
			uv.xyz /= uv.w;
			if (uv.x < SHADOW_A || uv.x > SHADOW_Z || uv.y < SHADOW_A || uv.y > SHADOW_Z)
				return 1;

		#if MAX_NUM_SPLITS == 4
			if (gNumSplits > 3) {
				nv = mul(pos, gShadowViewProj[2]);
				nv.xyz /= nv.w;
				if (nv.x < SHADOW_A || nv.x > SHADOW_Z || nv.y < SHADOW_A || nv.y > SHADOW_Z)
					return GetShadowInner(3, uv.xyz);
				uv = nv;
			}
		#endif

			if (gNumSplits > 2) {
				nv = mul(pos, gShadowViewProj[1]);
				nv.xyz /= nv.w;
				if (nv.x < SHADOW_A || nv.x > SHADOW_Z || nv.y < SHADOW_A || nv.y > SHADOW_Z)
					return GetShadowInner(2, uv.xyz);
				uv = nv;
			}

			if (gNumSplits > 1) {
				nv = mul(pos, gShadowViewProj[0]);
				nv.xyz /= nv.w;
				if (nv.x < SHADOW_A || nv.x > SHADOW_Z || nv.y < SHADOW_A || nv.y > SHADOW_Z)
					return GetShadowInner(1, uv.xyz);
			} else {
				nv = uv;
			}

			return GetShadowInner(0, nv.xyz);
		}
	#elif NUM_SPLITS == 1
		float GetShadow(float3 position) {
			float4 uv = mul(float4(position, 1.0), gShadowViewProj[0]);
			uv.xyz /= uv.w;
			if (uv.x < SHADOW_A || uv.x > SHADOW_Z || uv.y < SHADOW_A || uv.y > SHADOW_Z)
				return 1;
			return GetShadowInner(0, uv.xyz);
		}
	#else
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
					return GetShadowInner(i, uv.xyz);
				uv = nv;
			}

			return GetShadowInner(0, nv.xyz);
		}
	#endif
#endif