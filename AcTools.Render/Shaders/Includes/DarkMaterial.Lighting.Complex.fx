//////////////// Complex lighting

#define MAX_LIGHS_AMOUNT 30
#define MAX_EXTRA_SHADOWS 5
#define MAX_EXTRA_SHADOWS_SMOOTH 1
#define COMPLEX_LIGHTING 1

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

cbuffer cbNotUsed {
	float3 gLightDir;
	float3 gLightColor;
}

#if ENABLE_SHADOWS == 1
Texture2D gExtraShadowMaps[MAX_EXTRA_SHADOWS];
TextureCube gExtraShadowCubeMaps[MAX_EXTRA_SHADOWS];

cbuffer cbExtraShadowsBuffer {
	matrix gExtraShadowViewProj[MAX_EXTRA_SHADOWS];
	float gExtraShadowMapSize[MAX_EXTRA_SHADOWS];
	float4 gExtraShadowNearFar[MAX_EXTRA_SHADOWS];
}

float GetExtraShadow(float3 position, uint extra) {
	float4 uv = mul(float4(position, 1.0), gExtraShadowViewProj[extra]);
	uv.xyz /= uv.w;
	if (uv.x < SHADOW_A || uv.x > SHADOW_Z || uv.y < SHADOW_A || uv.y > SHADOW_Z)
		return 1;
	return GetShadowSmooth(gExtraShadowMaps[extra], gExtraShadowMapSize[extra], uv.xyz);
}

float GetExtraShadowFast(float3 position, uint extra) {
	float4 uv = mul(float4(position, 1.0), gExtraShadowViewProj[extra]);
	uv.xyz /= uv.w;
	if (uv.x < SHADOW_A || uv.x > SHADOW_Z || uv.y < SHADOW_A || uv.y > SHADOW_Z)
		return 1;
	return gExtraShadowMaps[extra].SampleCmpLevelZero(samShadow, uv.xy, uv.z).r;
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

float GetExtraShadowCubeSmooth(float3 toLightW, uint extra) {
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
}

float VectorToDepth(float3 vec, float z, float w) {
	float3 absVec = abs(vec);
	float localZcomp = max(absVec.x, max(absVec.y, absVec.z));
	float zComp = z - w / localZcomp;
	return (zComp + 1.0) * 0.5;
}

float GetExtraShadowCube(float3 toLightW, uint extra) {
	float4 nearFar = gExtraShadowNearFar[extra];
	float sD = VectorToDepth(toLightW, nearFar.z, nearFar.w);
	float3 L = normalize(toLightW);
	return gExtraShadowCubeMaps[extra].SampleCmpLevelZero(samShadow, L, sD).r;
}
#endif

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

float Attenuation(float range, float d){
	return 1.0f - smoothstep(range * 0.5f, range, d);
}

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

	[call]
	switch (light.ShadowMode) {
		case LIGHT_SHADOW_MAIN:
			return GetShadow(position);
#if MAX_EXTRA_SHADOWS > 0
		case LIGHT_SHADOW_EXTRA_FAST:
			return GetExtraShadowFast(position, 0);
		case LIGHT_SHADOW_EXTRA_CUBE:
			return GetExtraShadowCubeSmooth(position - light.PosW.xyz, 0);
#if MAX_EXTRA_SHADOWS_SMOOTH > 0
		case LIGHT_SHADOW_EXTRA:
			return GetExtraShadow(position, 0);
#endif
#if MAX_EXTRA_SHADOWS > 1
		case LIGHT_SHADOW_EXTRA_FAST + 1:
			return GetExtraShadowFast(position, 1);
		case LIGHT_SHADOW_EXTRA_CUBE + 1:
			return GetExtraShadowCubeSmooth(position - light.PosW.xyz, 1);
#if MAX_EXTRA_SHADOWS_SMOOTH > 1
		case LIGHT_SHADOW_EXTRA + 1:
			return GetExtraShadow(position, 1);
#endif
#if MAX_EXTRA_SHADOWS > 2
		case LIGHT_SHADOW_EXTRA_FAST + 2:
			return GetExtraShadowFast(position, 2);
		case LIGHT_SHADOW_EXTRA_CUBE + 2:
			return GetExtraShadowCubeSmooth(position - light.PosW.xyz, 2);
#if MAX_EXTRA_SHADOWS_SMOOTH > 2
		case LIGHT_SHADOW_EXTRA + 1:
			return GetExtraShadow(position, 1);
#endif
#if MAX_EXTRA_SHADOWS > 3
		case LIGHT_SHADOW_EXTRA_FAST + 3:
			return GetExtraShadowFast(position, 3);
		case LIGHT_SHADOW_EXTRA_CUBE + 3:
			return GetExtraShadowCube(position - light.PosW.xyz, 3);
#if MAX_EXTRA_SHADOWS > 4
		case LIGHT_SHADOW_EXTRA_FAST + 4:
			return GetExtraShadowFast(position, 4);
		case LIGHT_SHADOW_EXTRA_CUBE + 4:
			return GetExtraShadowCube(position - light.PosW.xyz, 4);
#if MAX_EXTRA_SHADOWS > 5
		case LIGHT_SHADOW_EXTRA_FAST + 5:
			return GetExtraShadowFast(position, 5);
		case LIGHT_SHADOW_EXTRA_CUBE + 5:
			return GetExtraShadowCube(position - light.PosW.xyz, 5);
#endif
#endif
#endif
#endif
#endif
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

float GetLight_SummaryShadow(Light light, float3 normal, float3 position, out float3 direction) {
	[branch]
	switch (light.Type) {
		case LIGHT_DIRECTIONAL: {
			direction = light.DirectionW.xyz;
			return GetShadow_ConsiderMirror(light, position) * GetDiffuseMultiplier(normal, direction);
		}
		case LIGHT_POINT: {
			direction = light.PosW.xyz - position;
			float distance = length(direction);
			direction /= distance;

			float attenuation = Attenuation(light.Range, distance);
			return GetShadow_ConsiderMirror(light, position) * GetDiffuseMultiplier(normal, direction) * attenuation;
		}
		case LIGHT_SPOT: {
			direction = light.PosW.xyz - position;
			float distance = length(direction);
			direction /= distance;

			float cosAngle = dot(light.DirectionW.xyz, direction);
			float spotCone = smoothstep(light.SpotlightCosMin, light.SpotlightCosMax, cosAngle);

			float attenuation = Attenuation(light.Range, distance);
			return GetShadow_ConsiderMirror(light, position) * GetDiffuseMultiplier(normal, direction) * attenuation * spotCone;
		}
		default: {
			direction = 0.0;
			return 0.0;
		}
	}
}

void GetLight_Maps(float3 normal, float3 position, float specularExpMultiplier, out float3 diffuse, out float3 specular) {
	diffuse = 0;
	specular = 0;

	[loop]
	for (int i = 0; i < MAX_LIGHS_AMOUNT; i++) {
		if (gLights[i].Type == LIGHT_OFF) continue;

		float3 direction;
		float shadow = GetLight_SummaryShadow(gLights[i], normal, position, direction);
		diffuse += gLights[i].Color.xyz * shadow;
		specular += CalculateSpecularLight_Maps(normal, position, specularExpMultiplier, direction) * gLights[i].Color.xyz * shadow;
	}
}

void GetLight_Maps_Sun(float3 normal, float3 position, float specularExpMultiplier, out float3 diffuse, out float3 specular) {
	diffuse = 0;
	specular = 0;

	[loop]
	for (int i = 0; i < MAX_LIGHS_AMOUNT; i++) {
		if (gLights[i].Type == LIGHT_OFF) continue;

		float3 direction;
		float shadow = GetLight_SummaryShadow(gLights[i], normal, position, direction);
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