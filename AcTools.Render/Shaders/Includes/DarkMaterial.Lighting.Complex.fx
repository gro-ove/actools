//////////////// Complex lighting

#ifndef _NOISE_MAP_DEFINED
#define _NOISE_MAP_DEFINED
Texture2D gNoiseMap;
#endif

#define COMPLEX_LIGHTING 1

static const dword LIGHT_OFF = 0;
static const dword LIGHT_POINT = 1;
static const dword LIGHT_SPOT = 2;
static const dword LIGHT_DIRECTIONAL = 3;
static const dword LIGHT_PLANE = 4;

#if ENABLE_AREA_LIGHTS == 1
static const dword LIGHT_SPHERE = 104;
static const dword LIGHT_TUBE = 105;
static const dword LIGHT_LTC_PLANE = 106;
static const dword LIGHT_LTC_TUBE = 107;
static const dword LIGHT_LTC_SPHERE = 104;
#endif

#define _AREA_LIGHTS_FROM 4

static const dword LIGHT_NO_SHADOWS = 1;
static const dword LIGHT_SMOOTH_SHADOWS = 2;
static const dword LIGHT_SHADOWS_CUBE = 4;
static const dword LIGHT_SPECULAR = 8;

#if ENABLE_AREA_LIGHTS == 1
static const dword LIGHT_LTC_PLANE_DOUBLE_SIDE = 16;
static const dword LIGHT_LTC_TUBE_WITH_CAPS = 16;
#endif

struct Light {
	float3 PosW;
	float Range;

	float3 DirectionW;
	float SpotlightCosMin;

	float3 Color;
	float SpotlightCosMax;

	// 36 bytes here

	uint Type;
	uint Flags;
	uint ShadowId;
	float Padding;

	// +16 = 52 bytes

	float4 Extra;
};

#if ENABLE_AREA_LIGHTS == 1
Texture2D gLtcMap;
Texture2D gLtcAmp;

#define LIGHT_GET_SPHERE_RADIUS(l) (l.SpotlightCosMin)
#define LIGHT_GET_TUBE_RADIUS(l) (l.SpotlightCosMin)
#define LIGHT_GET_PLANE_WIDTH(l) (l.SpotlightCosMin)
#define LIGHT_GET_PLANE_HEIGHT(l) (l.SpotlightCosMax)
#define LIGHT_GET_PLANE_CORNER_0(l) (l.PosW)
#define LIGHT_GET_PLANE_CORNER_1(l) (l.DirectionW)
#define LIGHT_GET_PLANE_CORNER_2(l) (float3(l.SpotlightCosMin, l.SpotlightCosMax, l.Padding))
#define LIGHT_GET_PLANE_CORNER_3(l) (l.Extra.xyz)
#endif

cbuffer cbLighting : register(b1) {
	Light gLights[MAX_LIGHS_AMOUNT];
}

#define LIGHT_HAS_FLAG_I(i,x) ((gLights[i].Flags & x) == x)
#define LIGHT_HAS_FLAG(l,x) ((l.Flags & x) == x)

cbuffer cbNotUsed {
	float3 gLightDir;
	float3 gLightColor;
}

#if ENABLE_SHADOWS == 1 && MAX_EXTRA_SHADOWS > 0
Texture2D gExtraShadowMaps[MAX_EXTRA_SHADOWS];

SamplerState samNoise {
	Filter = MIN_MAG_MIP_POINT;
	AddressU = WRAP;
	AddressV = WRAP;
};

cbuffer cbExtraShadowsBuffer {
	matrix gExtraShadowViewProj[MAX_EXTRA_SHADOWS];
	float4 gExtraShadowMapSize[MAX_EXTRA_SHADOWS];
	float4 gExtraShadowNearFar[MAX_EXTRA_SHADOWS];
}

static const float2 SmoothShadowsSamples[] = {
	float2(0, 1.75),
	float2(1.6643, .5408),
	float2(1.0286, -1.4158),
	float2(-1.0286, -1.4158),
	float2(-1.6643, .5408),
};

float VectorToDepth(float3 vec, float z, float w, float bias) {
	float3 absVec = abs(vec);
	float localZcomp = max(absVec.x, max(absVec.y, absVec.z)) + bias;
	float zComp = z - w / localZcomp;
	return (zComp + 1.0) * 0.5;
}

#define CUBEMAP_PADDING 0.95

float CubemapFix(float v) {
	return v * CUBEMAP_PADDING + (1 - CUBEMAP_PADDING) / 2;
}

float2 GetCubemapUv(float3 L) {
	float x, y, e;
	float3 A = abs(L);
	if (A.x > A.y && A.x > A.z) {
		float m = 0.5 / A.x;
		x = L.x > 0 ? 0.5 - L.z * m : 0.5 + L.z * m;
		y = 0.5 - L.y * m;
		e = L.x > 0 ? 0 : 1;
	} else if (A.y > A.z) {
		float m = 0.5 / A.y;
		x = 0.5 + L.x * m;
		y = L.y > 0 ? 0.5 + L.z * m : 0.5 - L.z * m;
		e = L.y > 0 ? 2 : 3;
	} else {
		float m = 0.5 / A.z;
		x = L.z > 0 ? 0.5 + L.x * m : 0.5 - L.x * m;
		y = 0.5 - L.y * m;
		e = L.z > 0 ? 4 : 5;
	}

	return float2(
		CubemapFix(x),
		(CubemapFix(y) + e) / 6);
}

float3 GetExtraShadowUv(float3 position, float3 normal, Light light, uint extra) {
	if (LIGHT_HAS_FLAG(light, LIGHT_SHADOWS_CUBE)){
		float4 nearFar = gExtraShadowNearFar[extra];
		float3 toLightW = position - (gFlatMirrored ? float3(light.PosW.x, -light.PosW.y, light.PosW.z) : light.PosW.xyz);
		float3 toLightN = normalize(toLightW);
		float sD = VectorToDepth(toLightW, nearFar.z, nearFar.w, LIGHT_HAS_FLAG(light, LIGHT_SMOOTH_SHADOWS) ? 0.06 * dot(normal, toLightN) - 0.07 : 0.0);
		return float3(GetCubemapUv(toLightN), sD);
	} else {
		float4 uv = mul(float4(position, 1.0), gExtraShadowViewProj[extra]);
		return uv.xyz / uv.w;
	}
}

float GetExtraShadowByUvFast(float3 uv, const uint extra) {
	switch (extra) {
#if MAX_EXTRA_SHADOWS > 0
	case 0: return gExtraShadowMaps[0].SampleCmpLevelZero(samShadow, uv.xy, uv.z).r;
#endif
#if MAX_EXTRA_SHADOWS > 1
	case 1: return gExtraShadowMaps[1].SampleCmpLevelZero(samShadow, uv.xy, uv.z).r;
#endif
#if MAX_EXTRA_SHADOWS > 2
	case 2: return gExtraShadowMaps[2].SampleCmpLevelZero(samShadow, uv.xy, uv.z).r;
#endif
#if MAX_EXTRA_SHADOWS > 3
	case 3: return gExtraShadowMaps[3].SampleCmpLevelZero(samShadow, uv.xy, uv.z).r;
#endif
#if MAX_EXTRA_SHADOWS > 4
	case 4: return gExtraShadowMaps[4].SampleCmpLevelZero(samShadow, uv.xy, uv.z).r;
#endif
#if MAX_EXTRA_SHADOWS > 5
	case 5: return gExtraShadowMaps[5].SampleCmpLevelZero(samShadow, uv.xy, uv.z).r;
#endif
#if MAX_EXTRA_SHADOWS > 6
	case 6: return gExtraShadowMaps[6].SampleCmpLevelZero(samShadow, uv.xy, uv.z).r;
#endif
#if MAX_EXTRA_SHADOWS > 7
	case 7: return gExtraShadowMaps[7].SampleCmpLevelZero(samShadow, uv.xy, uv.z).r;
#endif
#if MAX_EXTRA_SHADOWS > 8
	case 8: return gExtraShadowMaps[8].SampleCmpLevelZero(samShadow, uv.xy, uv.z).r;
#endif
#if MAX_EXTRA_SHADOWS > 9
	case 9: return gExtraShadowMaps[9].SampleCmpLevelZero(samShadow, uv.xy, uv.z).r;
#endif
#if MAX_EXTRA_SHADOWS > 10
	case 10: return gExtraShadowMaps[10].SampleCmpLevelZero(samShadow, uv.xy, uv.z).r;
#endif
#if MAX_EXTRA_SHADOWS > 11
	case 11: return gExtraShadowMaps[11].SampleCmpLevelZero(samShadow, uv.xy, uv.z).r;
#endif
#if MAX_EXTRA_SHADOWS > 12
	case 12: return gExtraShadowMaps[12].SampleCmpLevelZero(samShadow, uv.xy, uv.z).r;
#endif
#if MAX_EXTRA_SHADOWS > 13
	case 13: return gExtraShadowMaps[13].SampleCmpLevelZero(samShadow, uv.xy, uv.z).r;
#endif
#if MAX_EXTRA_SHADOWS > 14
	case 14: return gExtraShadowMaps[14].SampleCmpLevelZero(samShadow, uv.xy, uv.z).r;
#endif
#if MAX_EXTRA_SHADOWS > 15
	case 15: return gExtraShadowMaps[15].SampleCmpLevelZero(samShadow, uv.xy, uv.z).r;
#endif
#if MAX_EXTRA_SHADOWS > 16
	case 16: return gExtraShadowMaps[16].SampleCmpLevelZero(samShadow, uv.xy, uv.z).r;
#endif
#if MAX_EXTRA_SHADOWS > 17
	case 17: return gExtraShadowMaps[17].SampleCmpLevelZero(samShadow, uv.xy, uv.z).r;
#endif
#if MAX_EXTRA_SHADOWS > 18
	case 18: return gExtraShadowMaps[18].SampleCmpLevelZero(samShadow, uv.xy, uv.z).r;
#endif
#if MAX_EXTRA_SHADOWS > 19
	case 19: return gExtraShadowMaps[19].SampleCmpLevelZero(samShadow, uv.xy, uv.z).r;
#endif
#if MAX_EXTRA_SHADOWS > 20
	case 20: return gExtraShadowMaps[20].SampleCmpLevelZero(samShadow, uv.xy, uv.z).r;
#endif
#if MAX_EXTRA_SHADOWS > 21
	case 21: return gExtraShadowMaps[21].SampleCmpLevelZero(samShadow, uv.xy, uv.z).r;
#endif
#if MAX_EXTRA_SHADOWS > 22
	case 22: return gExtraShadowMaps[22].SampleCmpLevelZero(samShadow, uv.xy, uv.z).r;
#endif
#if MAX_EXTRA_SHADOWS > 23
	case 23: return gExtraShadowMaps[23].SampleCmpLevelZero(samShadow, uv.xy, uv.z).r;
#endif
#if MAX_EXTRA_SHADOWS > 24
	case 24: return gExtraShadowMaps[24].SampleCmpLevelZero(samShadow, uv.xy, uv.z).r;
#endif
	default: return 1.0;
	}
}

float GetExtraShadowByUvSmooth(float3 uv, const uint extra) {
	float shadow = 0.0;
	float2 shadowMapSizeTexel = gExtraShadowMapSize[extra].zw;
	float2 shadowMapSize = gExtraShadowMapSize[extra].xy;

	[unroll]
	float2 random = normalize(gNoiseMap.Sample(samNoise, uv.xy * shadowMapSize).xy);
	for (int i = 0; i < 5; ++i) {
		float2 offset = reflect(SmoothShadowsSamples[i], random) * shadowMapSizeTexel;
		shadow += GetExtraShadowByUvFast(float3(uv.x + offset.x, uv.y + offset.y, uv.z), extra).r;
	}

	return shadow / 5;
}
#endif

float GetMainShadow_ConsiderMirror(float3 position) {
	[flatten]
	if (gFlatMirrored) {
		position.y = -position.y;
	}

	return GetShadow(position);
}

float GetExtraShadow_ByType(Light light, float3 position, float3 normal) {
#if ENABLE_SHADOWS == 1 && MAX_EXTRA_SHADOWS > 0
	float3 uv = GetExtraShadowUv(position, normal, light, light.ShadowId);

	[branch]
	if (LIGHT_HAS_FLAG(light, LIGHT_SMOOTH_SHADOWS)){
        return GetExtraShadowByUvSmooth(uv, light.ShadowId);
	} else {
        return GetExtraShadowByUvFast(uv, light.ShadowId);
	}
#else
	return 1.0;
#endif
}

float GetExtraShadow_ConsiderMirror(Light light, float3 position, float3 normal) {
#if ENABLE_SHADOWS == 1 && MAX_EXTRA_SHADOWS > 0
	[branch]
	if (LIGHT_HAS_FLAG(light, LIGHT_NO_SHADOWS)) {
		return 1.0;
	} else {
		[flatten]
		if (gFlatMirrored) {
			position.y = -position.y;
		}

		return GetExtraShadow_ByType(light, position, normal);
	}
#else
	return 1.0;
#endif
}

float GetNDotH(float3 normal, float3 position, float3 lightDir) {
	float3 toEye = normalize(gEyePosW - position);
	float3 halfway = normalize(toEye + lightDir);
	return saturate(dot(halfway, normal));
}

float CalculateSpecularLight(float nDotH, float exp, float level) {
	return pow(nDotH, max(exp, 0.1)) * level;
}

float CalculateSpecularLight_ByValues(float3 normal, float3 position, float3 lightDir, float exp, float level) {
	float nDotH = GetNDotH(normal, position, lightDir);
	return CalculateSpecularLight(nDotH, exp, level);
}

float CalculateSpecularLight_Maps_Sun(float3 normal, float3 position, float specularExpMultiplier, float3 lightDir) {
	float nDotH = GetNDotH(normal, position, lightDir);
	return CalculateSpecularLight(nDotH, gMaterial.SpecularExp * specularExpMultiplier, gMaterial.Specular) +
		CalculateSpecularLight(nDotH, gMapsMaterial.SunSpecularExp * specularExpMultiplier, gMapsMaterial.SunSpecular);
}

float GetDiffuseMultiplier(float3 normal, float3 lightDir) {
	return saturate(dot(normal, lightDir));
}

float Attenuation(float range, float d) {
	return 1.0f - smoothstep(range * 0.5f, range, d);
}

#if ENABLE_AREA_LIGHTS == 1
#include "DarkMaterial.Lighting.Complex.Area.fx"

#define LUT_SIZE 64.0
#define LUT_SCALE ((LUT_SIZE - 1.0) / LUT_SIZE)
#define LUT_BIAS (0.5 / LUT_SIZE)

SamplerState samLtc {
	Filter = MIN_MAG_MIP_LINEAR;
	AddressU = CLAMP;
	AddressV = CLAMP;
};
#endif

void GetLight_ByType(Light light, float3 normal, float3 position, const float specularExp, const float specularValue, const bool sunSpeculars,
		inout float3 diffuse, inout float3 specular) {
	float3 direction;
	float attenuation;

	[branch]
	switch (light.Type) {
		case LIGHT_DIRECTIONAL: {
			direction = light.DirectionW.xyz;
			attenuation = 1.0;
			break;
		}
		case LIGHT_POINT: {
			direction = light.PosW.xyz - position;
			float distance = length(direction);
			direction /= distance;

			attenuation = Attenuation(light.Range, distance);
			break;
		}
		case LIGHT_SPOT: {
			direction = light.PosW.xyz - position;
			float distance = length(direction);
			direction /= distance;

			float cosAngle = dot(light.DirectionW.xyz, direction);
			float spotCone = smoothstep(light.SpotlightCosMin, light.SpotlightCosMax, cosAngle);
			attenuation = Attenuation(light.Range, distance) * spotCone;
			break;
		}
		case LIGHT_PLANE: {
			direction = light.DirectionW.xyz;
            float3 onPlane = position - dot(direction, position - light.PosW.xyz) * direction;
			float distance = length(position - onPlane);
			attenuation = pow(1 - saturate(distance / light.Range), 2);
            diffuse += light.Color.xyz * (GetDiffuseMultiplier(normal, direction) * 0.5 + 0.5) * attenuation;
			return;
		}
#if ENABLE_AREA_LIGHTS == 1
#if ENABLE_ADDITIONAL_AREA_LIGHTS == 1
		case LIGHT_SPHERE: {
			direction = normalize(gEyePosW - position);
			float nov = saturate(dot(normal, direction));
			float3 r = reflect(-direction, normal);

			float specularExpValue = specularExp;
			if (sunSpeculars) {
				specularExpValue *= gMapsMaterial.SunSpecularExp;
			}

			float distance;
			float value = AreaLight_Sphere(position, normal, direction, r,
				light.PosW.xyz, LIGHT_GET_SPHERE_RADIUS(light),
				0.3, clamp(1 - specularExpValue / 250, 0.03, 0.42), nov, distance, attenuation);

			attenuation *= Attenuation(light.Range, distance);

			diffuse += light.Color.xyz * attenuation;
			if (LIGHT_HAS_FLAG(light, LIGHT_SPECULAR)) {
				specular += light.Color.xyz * value * attenuation * 0.002;
			}
			return;
		}
		case LIGHT_TUBE: {
			direction = normalize(gEyePosW - position);
			float nov = saturate(dot(normal, direction));
			float3 r = reflect(-direction, normal);

			float specularExpValue = specularExp;
			if (sunSpeculars) {
				specularExpValue *= gMapsMaterial.SunSpecularExp;
			}

			float distance;
			float value = AreaLight_Tube(position, normal, direction, r,
				light.PosW.xyz, light.DirectionW.xyz, LIGHT_GET_TUBE_RADIUS(light),
				0.3, clamp(1 - specularExpValue / 250, 0.03, 0.42), nov, distance, attenuation);

			attenuation *= Attenuation(light.Range, distance);

			diffuse += light.Color.xyz * attenuation;
			if (LIGHT_HAS_FLAG(light, LIGHT_SPECULAR)) {
				specular += light.Color.xyz * value * attenuation * 0.002;
			}
			return;
		}
		case LIGHT_LTC_TUBE: {
			direction = normalize(gEyePosW - position);

			float3 points[2];
			points[0] = light.PosW.xyz;
			points[1] = light.DirectionW.xyz;

			bool withCaps = LIGHT_HAS_FLAG(light, LIGHT_LTC_TUBE_WITH_CAPS);
			attenuation = AreaLight_Tube(normal, direction, position, float3x3(1, 0, 0, 0, 1, 0, 0, 0, 1), points, LIGHT_GET_TUBE_RADIUS(light), withCaps);
			diffuse += light.Color.xyz * attenuation;

			[branch]
			if (LIGHT_HAS_FLAG(light, LIGHT_SPECULAR)) {
                float specularExpValue = specularExp;
                float specularActualValue;
                if (sunSpeculars) {
                    specularExpValue *= gMapsMaterial.SunSpecularExp;
                    specularActualValue = saturate(gMapsMaterial.SunSpecular);
                } else {
                    specularActualValue = specularValue;
                }

				float theta = acos(dot(normal, direction));
				float2 uv = float2(clamp(1 - specularExpValue / 250, 0.03, 0.42), theta / (0.5 * 3.141592653));
				uv = uv * LUT_SCALE + LUT_BIAS;

				float4 t = gLtcMap.SampleLevel(samLtc, uv, 0);
				float3x3 minv = float3x3(float3(1, 0, t.w),	float3(0, t.z, 0), float3(t.y, 0, t.x));
				float spec = AreaLight_Tube(normal, direction, position, minv, points, LIGHT_GET_TUBE_RADIUS(light), withCaps);
				spec *= gLtcAmp.SampleLevel(samLtc, uv, 0).w * specularActualValue * 0.1;
				specular += light.Color.xyz * spec;
			}
			return;
		}
#endif
		case LIGHT_LTC_PLANE: {
			direction = normalize(gEyePosW - position);

			float3 points[4];
			points[0] = LIGHT_GET_PLANE_CORNER_0(light);
			points[1] = LIGHT_GET_PLANE_CORNER_1(light);
			points[2] = LIGHT_GET_PLANE_CORNER_2(light);
			points[3] = LIGHT_GET_PLANE_CORNER_3(light);

			bool doubleSide = LIGHT_HAS_FLAG(light, LIGHT_LTC_PLANE_DOUBLE_SIDE);
			attenuation = AreaLight_Plane(normal, direction, position, float3x3(1, 0, 0, 0, 1, 0, 0, 0, 1), points, doubleSide);
			diffuse += light.Color.xyz * attenuation;

			[branch]
			if (LIGHT_HAS_FLAG(light, LIGHT_SPECULAR)) {
                float specularExpValue = specularExp;
                float specularActualValue;
                if (sunSpeculars) {
                    specularExpValue *= gMapsMaterial.SunSpecularExp;
                    specularActualValue = saturate(gMapsMaterial.SunSpecular);
                } else {
                    specularActualValue = specularValue;
                }

				float theta = acos(dot(normal, direction));
				float2 uv = float2(clamp(1 - specularExpValue / 250, 0.03, 0.42), theta / (0.5 * PI));
				uv = uv * LUT_SCALE + LUT_BIAS;

				float4 t = gLtcMap.SampleLevel(samLtc, uv, 0);
				float3x3 minv = float3x3(float3(1, 0, t.w),	float3(0, t.z, 0), float3(t.y, 0, t.x));
				float spec = AreaLight_Plane(normal, direction, position, minv, points, doubleSide);
				spec *= gLtcAmp.SampleLevel(samLtc, uv, 0).w * specularActualValue * 0.1;
				specular += light.Color.xyz * spec;
			}
			return;
		}
#endif
		default: {
			direction = 0.0;
			attenuation = 0.0;
			break;
		}
	}

	float shadow = GetExtraShadow_ConsiderMirror(light, position, normal) * GetDiffuseMultiplier(normal, direction) * attenuation;

	diffuse += light.Color.xyz * shadow;
	if (specularValue != 0) {
		if (sunSpeculars) {
			if (LIGHT_HAS_FLAG(light, LIGHT_SPECULAR)) {
				specular += CalculateSpecularLight_Maps_Sun(normal, position, specularExp, direction) * light.Color.xyz * shadow;
			}
		} else {
			if (LIGHT_HAS_FLAG(light, LIGHT_SPECULAR)) {
				specular += CalculateSpecularLight_ByValues(normal, position, direction, specularExp, specularValue) * light.Color.xyz * shadow;
			}
		}
	}
}

void GetLight_NoSpecular_NoArea(float3 normal, float3 position, out float3 diffuse) {
	float3 direction = gLights[0].DirectionW.xyz;
	float shadow = GetMainShadow_ConsiderMirror(position) * GetDiffuseMultiplier(normal, direction);
	diffuse = gLights[0].Color.xyz * shadow;

	[loop]
	for (int i = 1; i < MAX_LIGHS_AMOUNT; i++) {
		[branch]
		if (gLights[i].Type == LIGHT_OFF || gLights[i].Type >= _AREA_LIGHTS_FROM) continue;

		float3 specular = 0.0;
		GetLight_ByType(gLights[i], normal, position, 0, 0, false, diffuse, specular);
	}
}

void GetLight_NoSpecular_ExtraOnly(float3 normal, float3 position, out float3 diffuse) {
	diffuse = 0;

	[loop]
	for (int i = 1; i < MAX_LIGHS_AMOUNT; i++) {
		[branch]
		if (gLights[i].Type == LIGHT_OFF) continue;

		float3 specular = 0.0;
		GetLight_ByType(gLights[i], normal, position, 0, 0, false, diffuse, specular);
	}
}

void GetLight_Custom(float3 normal, float3 position, float specularExp, float specularValue, out float3 diffuse, out float3 specular) {
	float3 direction = gLights[0].DirectionW.xyz;
	float shadow = GetMainShadow_ConsiderMirror(position) * GetDiffuseMultiplier(normal, direction);
	diffuse = gLights[0].Color.xyz * shadow;

	if (LIGHT_HAS_FLAG_I(0, LIGHT_SPECULAR)){
	    specular = CalculateSpecularLight_ByValues(normal, position, direction, specularExp, specularValue) * gLights[0].Color.xyz * shadow;
	}

	[loop]
	for (int i = 1; i < MAX_LIGHS_AMOUNT; i++) {
		[branch]
		if (gLights[i].Type == LIGHT_OFF) continue;
		GetLight_ByType(gLights[i], normal, position, specularExp, specularValue, false, diffuse, specular);
	}
}

void GetLight_Material(float3 normal, float3 position, float specularExpMultiplier, out float3 diffuse, out float3 specular) {
	GetLight_Custom(normal, position, gMaterial.SpecularExp * specularExpMultiplier, gMaterial.Specular, diffuse, specular);
}

void GetLight_Material_Sun(float3 normal, float3 position, float specularExpMultiplier, out float3 diffuse, out float3 specular) {
	float3 direction = gLights[0].DirectionW.xyz;
	float shadow = GetMainShadow_ConsiderMirror(position) * GetDiffuseMultiplier(normal, direction);
	diffuse = gLights[0].Color.xyz * shadow;

	if (LIGHT_HAS_FLAG_I(0, LIGHT_SPECULAR)) {
		specular = CalculateSpecularLight_Maps_Sun(normal, position, specularExpMultiplier, direction) * gLights[0].Color.xyz * shadow;
	}

	[loop]
	for (int i = 1; i < MAX_LIGHS_AMOUNT; i++) {
		if (gLights[i].Type == LIGHT_OFF) continue;
		GetLight_ByType(gLights[i], normal, position, specularExpMultiplier, 1, true, diffuse, specular);
	}
}

//////////////// Calculate light using material values

float3 CalculateLight(float3 txColor, float3 normal, float3 position, float2 screenCoords) {
	float3 ambient = GetAmbient(normal) * GetAo(screenCoords);
	float3 diffuse, specular;
	GetLight_Material(normal, position, 1.0, diffuse, specular);
	return txColor * (gMaterial.Ambient * ambient + gMaterial.Diffuse * diffuse + gMaterial.Emissive) + specular;
}

float3 CalculateLight_Maps(float3 txColor, float3 normal, float3 position, float specularMultiplier, float specularExpMultiplier, float2 screenCoords) {
	float3 ambient = GetAmbient(normal) * GetAo(screenCoords);
	float3 diffuse, specular;
	GetLight_Material(normal, position, specularExpMultiplier, diffuse, specular);
	return txColor * (gMaterial.Ambient * ambient + gMaterial.Diffuse * diffuse + gMaterial.Emissive) + specular * specularMultiplier;
}

float3 CalculateLight_Maps_Sun(float3 txColor, float3 normal, float3 position, float specularMultiplier, float specularExpMultiplier, float2 screenCoords) {
	float3 ambient = GetAmbient(normal) * GetAo(screenCoords);
	float3 diffuse, specular;
	GetLight_Material_Sun(normal, position, specularExpMultiplier, diffuse, specular);
	return txColor * (gMaterial.Ambient * ambient + gMaterial.Diffuse * diffuse + gMaterial.Emissive) + specular * specularMultiplier;
}