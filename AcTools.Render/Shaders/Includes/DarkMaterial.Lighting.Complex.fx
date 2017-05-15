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

static const dword LIGHT_SHADOW_OFF = 0;
static const dword LIGHT_SHADOW_MAIN = 1;
static const dword LIGHT_SHADOW_EXTRA_SMOOTH = 100;
static const dword LIGHT_SHADOW_EXTRA_FAST = 200;

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
	bool ShadowCube;
	uint ShadowId;

	// +16 = 52 bytes
};

cbuffer cbLighting : register(b1) {
	Light gLights[MAX_LIGHS_AMOUNT];
}

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
		x = L.y > 0 ? 0.5 - L.x * m : 0.5 + L.x * m;
		y = 0.5 - L.z * m;
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
	if (light.ShadowCube){
		float4 nearFar = gExtraShadowNearFar[extra];
		float3 toLightW = position - light.PosW.xyz;
		float3 toLightN = normalize(toLightW);
		float sD = VectorToDepth(toLightW, nearFar.z, nearFar.w, light.ShadowMode == LIGHT_SHADOW_EXTRA_SMOOTH ? 0.06 * dot(normal, toLightN) - 0.07 : 0.0);
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
	switch (light.ShadowMode) {
		case LIGHT_SHADOW_EXTRA_SMOOTH:
			return GetExtraShadowByUvSmooth(uv, light.ShadowId);
		// case LIGHT_SHADOW_EXTRA_FAST:
		default:
			return GetExtraShadowByUvFast(uv, light.ShadowId);
	}
#else
	return 1.0;
#endif
}

float GetExtraShadow_ConsiderMirror(Light light, float3 position, float3 normal) {
#if ENABLE_SHADOWS == 1 && MAX_EXTRA_SHADOWS > 0
	[branch]
	if (light.ShadowMode == LIGHT_SHADOW_OFF) {
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

float GetLight_SummaryShadow(Light light, float3 normal, float3 position, out float3 direction) {
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
		default: {
			direction = 0.0;
			attenuation = 0.0;
			break;
		}
	}

	return GetExtraShadow_ConsiderMirror(light, position, normal) * GetDiffuseMultiplier(normal, direction) * attenuation;
}

void GetLight_NoSpecular(float3 normal, float3 position, out float3 diffuse) {
	float3 direction = gLights[0].DirectionW.xyz;
	float shadow = GetMainShadow_ConsiderMirror(position) * GetDiffuseMultiplier(normal, direction);
	diffuse = gLights[0].Color.xyz * shadow;

	[loop]
	for (int i = 1; i < MAX_LIGHS_AMOUNT; i++) {
		[branch]
		if (gLights[i].Type == LIGHT_OFF) continue;

		shadow = GetLight_SummaryShadow(gLights[i], normal, position, direction);
		diffuse += gLights[i].Color.xyz * shadow;
	}
}

void GetLight_Custom(float3 normal, float3 position, float specularExp, float specularValue, out float3 diffuse, out float3 specular) {
	float3 direction = gLights[0].DirectionW.xyz;
	float shadow = GetMainShadow_ConsiderMirror(position) * GetDiffuseMultiplier(normal, direction);
	diffuse = gLights[0].Color.xyz * shadow;
	specular = CalculateSpecularLight_ByValues(normal, position, direction, specularExp, specularValue) * gLights[0].Color.xyz * shadow;

	[loop]
	for (int i = 1; i < MAX_LIGHS_AMOUNT; i++) {
		[branch]
		if (gLights[i].Type == LIGHT_OFF) continue;

		shadow = GetLight_SummaryShadow(gLights[i], normal, position, direction);
		diffuse += gLights[i].Color.xyz * shadow;
		specular += CalculateSpecularLight_ByValues(normal, position, direction, specularExp, specularValue) * gLights[i].Color.xyz * shadow;
	}
}

void GetLight_Material(float3 normal, float3 position, float specularExpMultiplier, out float3 diffuse, out float3 specular) {
	GetLight_Custom(normal, position, gMaterial.SpecularExp * specularExpMultiplier, gMaterial.Specular, diffuse, specular);
}

void GetLight_Material_Sun(float3 normal, float3 position, float specularExpMultiplier, out float3 diffuse, out float3 specular) {
	float3 direction = gLights[0].DirectionW.xyz;
	float shadow = GetMainShadow_ConsiderMirror(position) * GetDiffuseMultiplier(normal, direction);
	diffuse = gLights[0].Color.xyz * shadow;
	specular = CalculateSpecularLight_Maps_Sun(normal, position, specularExpMultiplier, direction) * gLights[0].Color.xyz * shadow;

	[loop]
	for (int i = 1; i < MAX_LIGHS_AMOUNT; i++) {
		if (gLights[i].Type == LIGHT_OFF) continue;

		shadow = GetLight_SummaryShadow(gLights[i], normal, position, direction);
		diffuse += gLights[i].Color.xyz * shadow;
		specular += CalculateSpecularLight_Maps_Sun(normal, position, specularExpMultiplier, direction) * gLights[i].Color.xyz * shadow;
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

//////////////// Different material types

void CalculateLighted(PS_IN pin, out float3 lighted, out float alpha, out float3 normal) {
	float4 diffuseValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex);

	alpha = diffuseValue.a;
	normal = normalize(pin.NormalW);
	lighted = CalculateLight(diffuseValue.rgb, normal, pin.PosW, pin.PosH.xy);

	AlphaTest(alpha);
}

void CalculateLighted_Nm(PS_IN pin, out float3 lighted, out float alpha, out float3 normal) {
	float4 diffuseValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex);
	float4 normalValue = gNormalMap.Sample(samAnisotropic, pin.Tex);

	alpha = normalValue.a;
	normal = normalize(NormalSampleToWorldSpace(normalValue.xyz, pin.NormalW, pin.TangentW));
	lighted = CalculateLight(diffuseValue.rgb, normal, pin.PosW, pin.PosH.xy);

	AlphaTest(alpha);
}

void CalculateLighted_NmUvMult(PS_IN pin, out float3 lighted, out float alpha, out float3 normal) {
	float4 diffuseValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex * (1 + gNmUvMultMaterial.DiffuseMultiplier));
	float4 normalValue = gNormalMap.Sample(samAnisotropic, pin.Tex * (1 + gNmUvMultMaterial.NormalMultiplier));

	alpha = diffuseValue.a;
	normal = normalize(NormalSampleToWorldSpace(normalValue.xyz, pin.NormalW, pin.TangentW));
	lighted = CalculateLight(diffuseValue.rgb, normal, pin.PosW, pin.PosH.xy);

	AlphaTest(alpha);
}

void CalculateLighted_AtNm(PS_IN pin, out float3 lighted, out float alpha, out float3 normal) {
	float4 diffuseValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex);
	float4 normalValue = gNormalMap.Sample(samAnisotropic, pin.Tex);

	alpha = diffuseValue.a;
	normal = normalize(NormalSampleToWorldSpace(normalValue.xyz, pin.NormalW, pin.TangentW));
	lighted = CalculateLight(diffuseValue.rgb, normal, pin.PosW, pin.PosH.xy);

	AlphaTest(alpha);
}

void CalculateLighted_DiffMaps(PS_IN pin, out float3 lighted, out float alpha, out float3 normal) {
	float4 diffuseValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex);
	float4 normalValue = gNormalMap.Sample(samAnisotropic, pin.Tex);

	alpha = diffuseValue.a;
	normal = normalize(NormalSampleToWorldSpace(normalValue.xyz, pin.NormalW, pin.TangentW));
	lighted = CalculateLight_Maps(diffuseValue.rgb, normal, pin.PosW, alpha, alpha, pin.PosH.xy);

	AlphaTest(alpha);
}

void CalculateLighted_Maps(PS_IN pin, float txMapsSpecularMultiplier, float txMapsSpecularExpMultiplier, out float3 lighted, out float alpha,
		out float mask, out float3 normal) {
	float4 diffuseValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex);
	mask = diffuseValue.a;

	if (HAS_FLAG(HAS_DETAILS_MAP)) {
		float4 details = gDetailsMap.Sample(samAnisotropic, pin.Tex * gMapsMaterial.DetailsUvMultiplier);
		diffuseValue = diffuseValue * (details * (1 - mask) + mask);
		txMapsSpecularExpMultiplier *= (details.a * 0.5 + 0.5);
	}

	if (HAS_FLAG(HAS_NORMAL_MAP)) {
		float4 normalValue = gNormalMap.Sample(samAnisotropic, pin.Tex);
		alpha = HAS_FLAG(USE_NORMAL_ALPHA_AS_ALPHA) ? normalValue.a : 1.0;

		float blend = gMapsMaterial.DetailsNormalBlend;
		if (blend > 0.0) {
			float4 detailsNormalValue = gDetailsNormalMap.Sample(samAnisotropic, pin.Tex * gMapsMaterial.DetailsUvMultiplier);
			normalValue += (detailsNormalValue - 0.5) * blend * (1.0 - mask);
		}

		normal = normalize(NormalSampleToWorldSpace(normalValue.xyz, pin.NormalW, pin.TangentW));
	} else {
		normal = normalize(pin.NormalW);
		alpha = 1.0;
	}

	lighted = CalculateLight_Maps_Sun(diffuseValue.rgb, normal, pin.PosW, txMapsSpecularMultiplier, txMapsSpecularExpMultiplier, pin.PosH.xy);
	AlphaTest(alpha);
}