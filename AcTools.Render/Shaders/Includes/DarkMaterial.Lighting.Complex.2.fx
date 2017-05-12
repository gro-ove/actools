//////////////// Complex lighting

#define COMPLEX_LIGHTING 1

static const dword LIGHT_OFF = 0;
static const dword LIGHT_POINT = 1;
static const dword LIGHT_SPOT = 2;
static const dword LIGHT_DIRECTIONAL = 3;

static const dword LIGHT_SHADOW_OFF = 0;
static const dword LIGHT_SHADOW_MAIN = 1;
static const dword LIGHT_SHADOW_EXTRA_SMOOTH = 100;
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
	uint ShadowId;
	float Padding;

	// +16 = 52 bytes
};

cbuffer cbLighting : register(b1) {
	Light gLights[MAX_LIGHS_AMOUNT];
}

cbuffer cbNotUsed {
	float3 gLightDir;
	float3 gLightColor;
}

#if ENABLE_SHADOWS == 1
Texture2D gExtraShadowMaps[MAX_EXTRA_SHADOWS];

cbuffer cbExtraShadowsBuffer {
	matrix gExtraShadowViewProj[MAX_EXTRA_SHADOWS];
	float gExtraShadowMapSize[MAX_EXTRA_SHADOWS];
	float4 gExtraShadowNearFar[MAX_EXTRA_SHADOWS];
}

float GetExtraShadowSmooth(float3 position, uint extra) {
	float4 uv = mul(float4(position, 1.0), gExtraShadowViewProj[extra]);
	return GetShadowSmooth(gExtraShadowMaps[extra], gExtraShadowMapSize[extra], uv.xyz / uv.w);
}

float VectorToDepth(float3 vec, float z, float w, float bias){
	float3 absVec = abs(vec);
	float localZcomp = max(absVec.x, max(absVec.y, absVec.z)) + bias;
	float zComp = z - w / localZcomp;
	return (zComp + 1.0) * 0.5;
}

static const float2 GetExtraShadowCubeSmooth_Samples[] = {
	float2(0, 1.75),
	float2(1.6643, .5408),
	float2(1.0286, -1.4158),
	float2(-1.0286, -1.4158),
	float2(-1.6643, .5408),
};

/*float GetExtraShadowCubeSmooth(float3 toLightW, uint extra) {
	float4 nearFar = gExtraShadowNearFar[extra];
	float sD = VectorToDepth(toLightW, nearFar.z, nearFar.w, -0.02);
	float3 L = normalize(toLightW);

	float totalShadow = 0;

	float3 sideVector = normalize(cross(L, float3(0, 0, 1)));
	float3 upVector = normalize(cross(sideVector, L));

	sideVector *= gExtraShadowMapSize[extra];
	upVector *= gExtraShadowMapSize[extra];

	[unroll]
	for (int i = 0; i < 5; ++i){
		float3 samplePos = normalize(L + sideVector * GetExtraShadowCubeSmooth_Samples[i].x + upVector * GetExtraShadowCubeSmooth_Samples[i].y);
		totalShadow += gExtraShadowCubeMaps[extra].SampleCmpLevelZero(samShadow, samplePos, sD);
	}

	return totalShadow / 5;
}*/

float VectorToDepth(float3 vec, float z, float w) {
	float3 absVec = abs(vec);
	float localZcomp = max(absVec.x, max(absVec.y, absVec.z));
	float zComp = z - w / localZcomp;
	return (zComp + 1.0) * 0.5;
}

#define _ONE_SIDE 0.1666666666666667

float3 GetExtraShadowUv(float3 position, Light light, uint extra) {
	if (light.ShadowCube){
		float4 nearFar = gExtraShadowNearFar[extra];
		float3 toLightW = position - light.PosW.xyz;
		float sD = VectorToDepth(toLightW, nearFar.z, nearFar.w);

		float3 L = normalize(toLightW);
		float3 A = abs(L);
		float2 uv;		
		if (A.x > A.y && A.x > A.z) {
			float3 Lx = L / A.x;
			uv = float2(
				Lx.x > 0 ? 0.5 - Lx.z / 2 : 0.5 + Lx.z / 2, 
				(0.5 - Lx.y / 2) * _ONE_SIDE + (Lx.x > 0 ? 0 : _ONE_SIDE));
		} else if (A.y > A.z) {
			float3 Ly = L / A.y;
			uv = float2(
				Ly.y > 0 ? 0.5 - Ly.x / 2 : 0.5 + Ly.x / 2,
				(0.5 - Ly.z / 2) * _ONE_SIDE + (Ly.y > 0 ? _ONE_SIDE * 2 : _ONE_SIDE * 3));
		} else {
			float3 Lz = L / A.z;
			uv = float2(
				Lz.z > 0 ? 0.5 + Lz.x / 2 : 0.5 - Lz.x / 2,
				(0.5 - Lz.y / 2) * _ONE_SIDE + (Lz.z > 0 ? _ONE_SIDE * 4 : _ONE_SIDE * 5));
		}

		return float3(uv, sD);
	} else {
		float4 uv = mul(float4(position, 1.0), gExtraShadowViewProj[extra]);
		uv.xyz /= uv.w;
		return uv.xyz;
	}
}

float GetExtraShadowByUvFast(float3 uv, uint extra) {
	return gExtraShadowMaps[extra].SampleCmpLevelZero(samShadow, uv.xy, uv.z).r;
}

float GetExtraShadowFast(float3 position, Light light, uint extra) {
	return GetExtraShadowByUvFast(GetExtraShadowUv(position, light, extra), extra);
}
#endif

float GetShadow_ConsiderMirror(float3 position) {
	[flatten]
	if (gFlatMirrored) {
		position.y = -position.y;
	}
	return GetShadow(position);
}

float GetShadow_ConsiderMirror(Light light, float3 position) {
	float shadowMultiplier;

#if ENABLE_SHADOWS == 1
	[flatten]
	if (gFlatMirrored) {
		position.y = -position.y;
	}

	[branch]
	switch (light.ShadowMode) {
#if MAX_EXTRA_SHADOWS > 0
		case LIGHT_SHADOW_EXTRA_FAST:
			return GetExtraShadowFast(position, light, 0);
#if MAX_EXTRA_SHADOWS_SMOOTH > 0
		case LIGHT_SHADOW_EXTRA_SMOOTH:
			return GetExtraShadowSmooth(position, 0);
#endif

#if COMPLEX_LIGHTING_DEBUG_MODE != 1

#if MAX_EXTRA_SHADOWS > 1
		case LIGHT_SHADOW_EXTRA_FAST + 1:
			return GetExtraShadowFast(position, light, 1), 1);
#if MAX_EXTRA_SHADOWS_SMOOTH > 1
		case LIGHT_SHADOW_EXTRA_SMOOTH + 1:
			return GetExtraShadowSmooth(position, 1);
#endif
#if MAX_EXTRA_SHADOWS > 2
		case LIGHT_SHADOW_EXTRA_FAST + 2:
			return GetExtraShadowFast(position, light, 2), 2);
#if MAX_EXTRA_SHADOWS_SMOOTH > 2
		case LIGHT_SHADOW_EXTRA_SMOOTH + 2:
			return GetExtraShadowSmooth(position, 2);
#endif
#if MAX_EXTRA_SHADOWS > 3
		case LIGHT_SHADOW_EXTRA_FAST + 3:
			return GetExtraShadowFast(position, light, 3), 3);
#if MAX_EXTRA_SHADOWS > 4
		case LIGHT_SHADOW_EXTRA_FAST + 4:
			return GetExtraShadowFast(position, light, 4), 4);
#if MAX_EXTRA_SHADOWS > 5
		case LIGHT_SHADOW_EXTRA_FAST + 5:
			return GetExtraShadowFast(position, light, 5), 5);
#if MAX_EXTRA_SHADOWS > 6
		case LIGHT_SHADOW_EXTRA_FAST + 6:
			return GetExtraShadowFast(position, light, 6), 6);
#if MAX_EXTRA_SHADOWS > 7
		case LIGHT_SHADOW_EXTRA_FAST + 7:
			return GetExtraShadowFast(position, light, 7), 7);
#if MAX_EXTRA_SHADOWS > 8
		case LIGHT_SHADOW_EXTRA_FAST + 8:
			return GetExtraShadowFast(position, light, 8), 8);
#if MAX_EXTRA_SHADOWS > 9
		case LIGHT_SHADOW_EXTRA_FAST + 9:
			return GetExtraShadowFast(position, light, 9), 9);
#endif
#endif
#endif
#endif
#endif
#endif
#endif
#endif
#endif

#endif /* COMPLEX_LIGHTING_DEBUG_MODE */

#endif
		case LIGHT_SHADOW_OFF:
		default:
			return 1.0;
	}
#else
	shadowMultiplier = 1.0;
#endif

	return shadowMultiplier;
}

float GetNDotH(float3 normal, float3 position, float3 lightDir) {
	float3 toEye = normalize(gEyePosW - position);
	float3 halfway = normalize(toEye + lightDir);
	return saturate(dot(halfway, normal));
}

float CalculateSpecularLight(float nDotH, float exp, float level) {
	return pow(nDotH, max(exp, 0.1)) * level;
}

float CalculateSpecularLight(float3 normal, float3 position, float3 lightDir) {
	float nDotH = GetNDotH(normal, position, lightDir);
	return CalculateSpecularLight(nDotH, gMaterial.SpecularExp, gMaterial.Specular);
}

float CalculateSpecularLight_Maps(float3 normal, float3 position, float specularExpMultiplier, float3 lightDir) {
	float nDotH = GetNDotH(normal, position, lightDir);
	return CalculateSpecularLight(nDotH, gMaterial.SpecularExp * specularExpMultiplier, gMaterial.Specular);
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

	return GetShadow_ConsiderMirror(light, position) * GetDiffuseMultiplier(normal, direction) * attenuation;
}

void GetLight_NoSpecular(float3 normal, float3 position, out float3 diffuse) {
	float3 direction = gLights[0].DirectionW.xyz;
	float shadow = GetShadow_ConsiderMirror(position) * GetDiffuseMultiplier(normal, direction);
	diffuse = gLights[0].Color.xyz * shadow;

	[loop]
	for (int i = 1; i < MAX_LIGHS_AMOUNT; i++) {
		[branch]
		if (gLights[i].Type == LIGHT_OFF) continue;

		shadow = GetLight_SummaryShadow(gLights[i], normal, position, direction);
		diffuse += gLights[i].Color.xyz * shadow;
	}
}

void GetLight_Maps(float3 normal, float3 position, float specularExpMultiplier, out float3 diffuse, out float3 specular) {
	float3 direction = gLights[0].DirectionW.xyz;
	float shadow = GetShadow_ConsiderMirror(position) * GetDiffuseMultiplier(normal, direction);
	diffuse = gLights[0].Color.xyz * shadow;
	specular = CalculateSpecularLight_Maps(normal, position, specularExpMultiplier, direction) * gLights[0].Color.xyz * shadow;

	[loop]
	for (int i = 1; i < MAX_LIGHS_AMOUNT; i++) {
		[branch]
		if (gLights[i].Type == LIGHT_OFF) continue;

		shadow = GetLight_SummaryShadow(gLights[i], normal, position, direction);
		diffuse += gLights[i].Color.xyz * shadow;
		specular += CalculateSpecularLight_Maps(normal, position, specularExpMultiplier, direction) * gLights[i].Color.xyz * shadow;
	}
}

void GetLight_Maps_Sun(float3 normal, float3 position, float specularExpMultiplier, out float3 diffuse, out float3 specular) {
	float3 direction = gLights[0].DirectionW.xyz;
	float shadow = GetShadow_ConsiderMirror(position) * GetDiffuseMultiplier(normal, direction);
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

float3 CalculateLight(float3 txColor, float3 normal, float3 position, float2 screenCoords) {
	float3 ambient = GetAmbient(normal) * GetAo(screenCoords);
	float3 diffuse, specular;
	GetLight_Maps(normal, position, 1.0, diffuse, specular);
	return txColor * (gMaterial.Ambient * ambient + gMaterial.Diffuse * diffuse + gMaterial.Emissive) + specular;
}

float3 CalculateLight_Maps(float3 txColor, float3 normal, float3 position, float specularMultiplier, float specularExpMultiplier, float2 screenCoords) {
	float3 ambient = GetAmbient(normal) * GetAo(screenCoords);
	float3 diffuse, specular;
	GetLight_Maps(normal, position, specularExpMultiplier, diffuse, specular);
	return txColor * (gMaterial.Ambient * ambient + gMaterial.Diffuse * diffuse + gMaterial.Emissive) + specular * specularMultiplier;
}

float3 CalculateLight_Maps_Sun(float3 txColor, float3 normal, float3 position, float specularMultiplier, float specularExpMultiplier, float2 screenCoords) {
	float3 ambient = GetAmbient(normal) * GetAo(screenCoords);
	float3 diffuse, specular;
	GetLight_Maps_Sun(normal, position, specularExpMultiplier, diffuse, specular);
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