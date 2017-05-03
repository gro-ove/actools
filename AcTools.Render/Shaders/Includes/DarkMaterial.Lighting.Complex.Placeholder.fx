//////////////// Complex lighting (placeholder)

#define MAX_LIGHS_AMOUNT 1
#define MAX_EXTRA_SHADOWS 0
#define MAX_EXTRA_SHADOWS_SMOOTH 0
#define COMPLEX_LIGHTING 0

static const dword LIGHT_OFF = 0;
static const dword LIGHT_POINT = 1;
static const dword LIGHT_SPOT = 2;
static const dword LIGHT_DIRECTIONAL = 3;

static const dword LIGHT_SHADOW_OFF = 0;
static const dword LIGHT_SHADOW_MAIN = 1;
static const dword LIGHT_SHADOW_EXTRA = 100;
static const dword LIGHT_SHADOW_EXTRA_FAST = 200;
static const dword LIGHT_SHADOW_EXTRA_CUBE = 300;

struct Light {
	float3 PosW;
	float Range;

	float3 DirectionW;
	float SpotlightCosMin;

	float3 Color;
	float SpotlightCosMax;

	// 36 bytes here

	uint Type;
	uint ShadowMode;
	float2 Padding;

	// +16 = 52 bytes
};

cbuffer cbLighting : register(b1) {
	Light gLights[MAX_LIGHS_AMOUNT];
}

#if COMPLEX_LIGHTING
Texture2D gExtraShadowMaps[MAX_EXTRA_SHADOWS];
TextureCube gExtraShadowCubeMaps[MAX_EXTRA_SHADOWS];

cbuffer cbExtraShadowsBuffer {
	matrix gExtraShadowViewProj[MAX_EXTRA_SHADOWS];
	float gExtraShadowMapSize[MAX_EXTRA_SHADOWS];
	float4 gExtraShadowNearFar[MAX_EXTRA_SHADOWS];
}
#endif