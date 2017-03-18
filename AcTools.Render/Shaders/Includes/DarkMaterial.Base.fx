// structs
static const dword HAS_NORMAL_MAP = 1;
static const dword USE_NORMAL_ALPHA_AS_ALPHA = 64;
static const dword ALPHA_TEST = 128;

// basic stuff
struct StandartMaterial {
	float Ambient;
	float Diffuse;
	float Specular;
	float SpecularExp;

	float3 Emissive;

	dword Flags;
	float3 _padding;
};

Texture2D gDiffuseMap;
Texture2D gNormalMap;

// reflective
static const dword IS_ADDITIVE = 16;

struct ReflectiveMaterial {
	float FresnelC;
	float FresnelExp;
	float FresnelMaxLevel;
};

// maps
static const dword HAS_DETAILS_MAP = 4;
static const dword IS_CARPAINT = 32;

struct MapsMaterial {
	float DetailsUvMultipler;
	float DetailsNormalBlend;

	float SunSpecular;
	float SunSpecularExp;
};

Texture2D gMapsMap;
Texture2D gDetailsMap;
Texture2D gDetailsNormalMap;

// alpha
struct AlphaMaterial {
	float Alpha;
};

// nm uvmult
struct NmUvMultMaterial {
	float DiffuseMultipler;
	float NormalMultipler;
};

// input resources
cbuffer cbPerObject : register(b0) {
	matrix gWorld;
	matrix gWorldInvTranspose;
	matrix gWorldViewProj;

	StandartMaterial gMaterial;
	ReflectiveMaterial gReflectiveMaterial;
	MapsMaterial gMapsMaterial;
	AlphaMaterial gAlphaMaterial;
	NmUvMultMaterial gNmUvMultMaterial;

	bool gFlatMirrored;
}

cbuffer cbPerFrame {
	float3 gEyePosW;
	float3 gLightDir;

	float3 gLightColor;
	float3 gAmbientDown;
	float3 gAmbientRange;
	float3 gBackgroundColor;

	float gReflectionPower;
	float gAoPower;
	bool gCubemapReflections;
	float gCubemapAmbient;
}

cbuffer cbTextureFlatMirror {
	// matrix gWorldViewProjInv;
	float4 gScreenSize;
	float gFlatMirrorPower;
};

// z-buffer tricks if needed
#define FixPosH(x) (x)

float4 CalculatePosH(float3 posL) {
	return FixPosH(mul(float4(posL, 1.0f), gWorldViewProj));
}

// shadows
#define ENABLE_SHADOWS 1
#define ENABLE_PCSS 1
// define NUM_SPLITS 1
#include "Shadows.fx"

SamplerState samLinear {
	Filter = MIN_MAG_MIP_LINEAR;
	AddressU = CLAMP;
	AddressV = CLAMP;
};

// AO map for proper SSAO
Texture2D gAoMap;

float GetAo(float2 screenCoords) {
	[branch]
	if (gAoPower != 0.0) {
		return 1.0 - (1.0 - gAoMap.SampleLevel(samLinear, screenCoords / gScreenSize.xy, 0).r) * gAoPower;
	} else {
		return 1.0;
	}
}

SamplerState samAnisotropic {
	Filter = ANISOTROPIC;
	MaxAnisotropy = 8;

	AddressU = WRAP;
	AddressV = WRAP;
};

float3 NormalSampleToWorldSpace(float3 normalMapSample, float3 unitNormalW, float3 tangentW) {
	float3 normalT = float3(
		2.0 * normalMapSample.x - 1.0,
		1.0 - 2.0 * normalMapSample.y,
		2.0 * normalMapSample.z - 1.0);

	float3 N = unitNormalW;
	float3 T = normalize(tangentW - dot(tangentW, N)*N);
	float3 B = normalize(cross(N, T));

	return mul(normalT, float3x3(T, B, N));
}

struct pt_VS_IN {
	float3 PosL       : POSITION;
	float2 Tex        : TEXCOORD;
};

struct pt_PS_IN {
	float4 PosH       : SV_POSITION;
	float3 PosW       : POSITION;
	float2 Tex        : TEXCOORD;
};

pt_PS_IN vs_pt_main(pt_VS_IN vin) {
	pt_PS_IN vout;

	vout.PosW = mul(float4(vin.PosL, 1.0f), gWorld).xyz;
	vout.PosH = CalculatePosH(vin.PosL);
	vout.Tex = vin.Tex;

	return vout;
}

struct VS_IN {
	float3 PosL       : POSITION;
	float3 NormalL    : NORMAL;
	float2 Tex        : TEXCOORD;
	float3 TangentL   : TANGENT;
};

struct skinned_VS_IN {
	float3 PosL         : POSITION;
	float3 NormalL      : NORMAL;
	float2 Tex          : TEXCOORD;
	float3 TangentL     : TANGENT;
	float3 BoneWeights  : BLENDWEIGHTS;
	float4 BoneIndices  : BLENDINDICES;
};

struct PS_IN {
	float4 PosH       : SV_POSITION;
	float3 PosW       : POSITION;
	float3 NormalW    : NORMAL;
	float2 Tex        : TEXCOORD;
	float3 TangentW   : TANGENT;
};

PS_IN vs_main(VS_IN vin) {
	PS_IN vout;

	vout.PosW = mul(float4(vin.PosL, 1.0f), gWorld).xyz;
	vout.NormalW = mul(vin.NormalL, (float3x3)gWorldInvTranspose);
	vout.TangentW = mul(vin.TangentL, (float3x3)gWorldInvTranspose);

	vout.PosH = CalculatePosH(vin.PosL);
	vout.Tex = vin.Tex;

	return vout;
}

struct depthOnly_PS_IN {
	float4 PosH       : SV_POSITION;
};

depthOnly_PS_IN vs_depthOnly(VS_IN vin) {
	depthOnly_PS_IN vout;
	vout.PosH = CalculatePosH(vin.PosL);
	return vout;
}

#define MAX_BONES 64

cbuffer cbSkinned {
	float4x4 gBoneTransforms[MAX_BONES];
};

PS_IN vs_skinned(skinned_VS_IN vin) {
	float weight0 = vin.BoneWeights.x;
	float weight1 = vin.BoneWeights.y;
	float weight2 = vin.BoneWeights.z;
	float weight3 = 1.0f - (weight0 + weight1 + weight2);

	float4x4 bone0 = gBoneTransforms[(int)vin.BoneIndices.x];
	float4x4 bone1 = gBoneTransforms[(int)vin.BoneIndices.y];
	float4x4 bone2 = gBoneTransforms[(int)vin.BoneIndices.z];
	float4x4 bone3 = gBoneTransforms[(int)vin.BoneIndices.w];

	// offset position by bone matrices, using weights to scale
	float4 p = weight0 * mul(float4(vin.PosL, 1.0f), bone0);
	p += weight1 * mul(float4(vin.PosL, 1.0f), bone1);
	p += weight2 * mul(float4(vin.PosL, 1.0f), bone2);
	p += weight3 * mul(float4(vin.PosL, 1.0f), bone3);
	p.w = 1.0f;

	// offset normal by bone matrices, using weights to scale
	float4 n = weight0 * mul(float4(vin.NormalL, 0.0f), bone0);
	n += weight1 * mul(float4(vin.NormalL, 0.0f), bone1);
	n += weight2 * mul(float4(vin.NormalL, 0.0f), bone2);
	n += weight3 * mul(float4(vin.NormalL, 0.0f), bone3);
	n.w = 0.0f;

	// offset tangent by bone matrices, using weights to scale
	float4 t = weight0 * mul(float4(vin.TangentL, 0.0f), bone0);
	t += weight1 * mul(float4(vin.TangentL, 0.0f), bone1);
	t += weight2 * mul(float4(vin.TangentL, 0.0f), bone2);
	t += weight3 * mul(float4(vin.TangentL, 0.0f), bone3);
	t.w = 0.0f;

	PS_IN vout;

	vout.PosW = mul(p, gWorld).xyz;
	vout.NormalW = mul(n.xyz, (float3x3)gWorldInvTranspose);
	vout.TangentW = mul(t.xyz, (float3x3)gWorldInvTranspose);

	vout.PosH = FixPosH(mul(p, gWorldViewProj));
	vout.Tex = vin.Tex;

	return vout;
}

depthOnly_PS_IN vs_depthOnly_skinned(skinned_VS_IN vin) {
	depthOnly_PS_IN vout;

	float weight0 = vin.BoneWeights.x;
	float weight1 = vin.BoneWeights.y;
	float weight2 = vin.BoneWeights.z;
	float weight3 = 1.0f - (weight0 + weight1 + weight2);

	float4x4 bone0 = gBoneTransforms[(int)vin.BoneIndices.x];
	float4x4 bone1 = gBoneTransforms[(int)vin.BoneIndices.y];
	float4x4 bone2 = gBoneTransforms[(int)vin.BoneIndices.z];
	float4x4 bone3 = gBoneTransforms[(int)vin.BoneIndices.w];

	float4 p = weight0 * mul(float4(vin.PosL, 1.0f), bone0);
	p += weight1 * mul(float4(vin.PosL, 1.0f), bone1);
	p += weight2 * mul(float4(vin.PosL, 1.0f), bone2);
	p += weight3 * mul(float4(vin.PosL, 1.0f), bone3);
	p.w = 1.0f;

	vout.PosH = FixPosH(mul(p, gWorldViewProj));

	return vout;
}

float GetFakeHorizon(float3 d, float e) {
	return saturate((d.y + 0.02) * 5.0 * e) * saturate(1 - pow(d.y * 1.5, 2)) * 0.3;
}

float saturate(float v, float e) {
	return saturate(v * e + 0.5);
}

float GetFakeStudioLights(float3 d, float e) {
	return (
		saturate(0.3 - abs(0.6 - d.y), e) +
		saturate(0.1 - abs(0.1 - d.y), e)
		) * saturate(0.3 - abs(0.1 + sin(d.x * 11.0)), e);
}

// real reflection (not used by default)
TextureCube gReflectionCubemap;

float3 GetReflection(float3 reflected, float specularExp) {
	[branch]
	if (gCubemapReflections) {
		return gReflectionCubemap.SampleLevel(samAnisotropic, reflected, saturate(1 - specularExp / 255) * 8).rgb * gReflectionPower;
	} else {
		[flatten]
		if (gFlatMirrored) {
			reflected.y = -reflected.y;
		}

		float edge = specularExp / 30.0 + 1.0;
		float fake = saturate(GetFakeHorizon(reflected, edge) + GetFakeStudioLights(reflected, edge));
	    return (gBackgroundColor * (1 - fake) * 1.1 + fake * 1.8) * gReflectionPower;
	}
}

#define HAS_FLAG(x) ((gMaterial.Flags & x) == x)
#define GET_FLAG(x) ((gMaterial.Flags & x) / x)

void AlphaTest(float alpha) {
	if (HAS_FLAG(ALPHA_TEST)) clip(alpha - 0.5);
}

float Luminance(float3 color) {
	return saturate(dot(color, float3(0.299f, 0.587f, 0.114f)));
}

//////////////// Simple lighting

float GetNDotH(float3 normal, float3 position) {
	float3 toEye = normalize(gEyePosW - position);
	float3 halfway = normalize(toEye + gLightDir);
	return saturate(dot(halfway, normal));
}

float CalculateSpecularLight(float nDotH, float exp, float level) {
	return pow(nDotH, max(exp, 0.1)) * level;
}

float CalculateSpecularLight(float3 normal, float3 position) {
	float nDotH = GetNDotH(normal, position);
	return CalculateSpecularLight(nDotH, gMaterial.SpecularExp, gMaterial.Specular);
}

float CalculateSpecularLight_Maps(float3 normal, float3 position, float specularExpMultipler) {
	float nDotH = GetNDotH(normal, position);
	return CalculateSpecularLight(nDotH, gMaterial.SpecularExp * specularExpMultipler, gMaterial.Specular);
}

float CalculateSpecularLight_Maps_Sun(float3 normal, float3 position, float specularExpMultipler) {
	float nDotH = GetNDotH(normal, position);
	return CalculateSpecularLight(nDotH, gMaterial.SpecularExp * specularExpMultipler, gMaterial.Specular) +
		CalculateSpecularLight(nDotH, gMapsMaterial.SunSpecularExp * specularExpMultipler, gMapsMaterial.SunSpecular);
}

float3 GetAmbient(float3 normal) {
	float value = abs(gCubemapAmbient);
	float3 gradient = gCubemapAmbient < 0.0 ? (float3)1.0 : gAmbientDown + saturate(normal.y * 0.5 + 0.5) * gAmbientRange;

	[branch]
	if (gCubemapAmbient != 0) {
		float3 refl = gReflectionCubemap.SampleLevel(samAnisotropic, normal, 99).rgb;
		return max(refl, 0.0) / (max(dot(refl, float3(0.299f, 0.587f, 0.114f)), 0.0) + 0.04) * value + gradient * (1.0 - value);
	} else {
		return gradient;
	}
}

float GetDiffuseMultipler(float3 normal) {
	return saturate(dot(normal, gLightDir));
}

float GetShadow_ConsiderMirror(float3 position) {
	[flatten]
	if (gFlatMirrored) {
		position.y = -position.y;
	}
	return GetShadow(position);
}

float3 CalculateLight(float3 txColor, float3 normal, float3 position, float2 screenCoords) {
	float3 ambient = GetAmbient(normal);
	float diffuseMultipler = GetDiffuseMultipler(normal);

#if ENABLE_SHADOWS == 1
	diffuseMultipler *= GetShadow_ConsiderMirror(position);
#endif

	ambient *= GetAo(screenCoords);

	float3 lightResult = diffuseMultipler * gLightColor;
	float3 specular = CalculateSpecularLight(normal, position);
	return txColor * (gMaterial.Ambient * ambient + gMaterial.Diffuse * lightResult + gMaterial.Emissive) + specular * lightResult;
}

float3 CalculateLight_Maps(float3 txColor, float3 normal, float3 position, float specularMultipler, float specularExpMultipler, float2 screenCoords) {
	float3 ambient = GetAmbient(normal);
	float diffuseMultipler = GetDiffuseMultipler(normal);

#if ENABLE_SHADOWS == 1
	diffuseMultipler *= GetShadow_ConsiderMirror(position);
#endif

	ambient *= GetAo(screenCoords);

	float3 lightResult = diffuseMultipler * gLightColor;
	float3 specular = CalculateSpecularLight_Maps(normal, position, specularExpMultipler) * specularMultipler;
	return txColor * (gMaterial.Ambient * ambient + gMaterial.Diffuse * gLightColor * diffuseMultipler + gMaterial.Emissive) + specular * lightResult;
}

float3 CalculateLight_Maps_Sun(float3 txColor, float3 normal, float3 position, float specularMultipler, float specularExpMultipler, float2 screenCoords) {
	float3 ambient = GetAmbient(normal);
	float diffuseMultipler = GetDiffuseMultipler(normal);

#if ENABLE_SHADOWS == 1
	diffuseMultipler *= GetShadow_ConsiderMirror(position);
#endif

	ambient *= GetAo(screenCoords);

	float3 lightResult = diffuseMultipler * gLightColor;
	float3 specular = CalculateSpecularLight_Maps_Sun(normal, position, specularExpMultipler) * specularMultipler;
	return txColor * (gMaterial.Ambient * ambient + gMaterial.Diffuse * lightResult + gMaterial.Emissive) + specular * lightResult;
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
	float4 diffuseValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex * (1 + gNmUvMultMaterial.DiffuseMultipler));
	float4 normalValue = gNormalMap.Sample(samAnisotropic, pin.Tex * (1 + gNmUvMultMaterial.NormalMultipler));

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

void CalculateLighted_Maps(PS_IN pin, float txMapsSpecularMultipler, float txMapsSpecularExpMultipler, out float3 lighted, out float alpha,
		out float mask, out float3 normal) {
	float4 diffuseValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex);
	mask = diffuseValue.a;

	if (HAS_FLAG(HAS_DETAILS_MAP)) {
		float4 details = gDetailsMap.Sample(samAnisotropic, pin.Tex * gMapsMaterial.DetailsUvMultipler);
		diffuseValue = diffuseValue * (details * (1 - mask) + mask);
		txMapsSpecularExpMultipler *= (details.a * 0.5 + 0.5);
	}

	if (HAS_FLAG(HAS_NORMAL_MAP)) {
		float4 normalValue = gNormalMap.Sample(samAnisotropic, pin.Tex);
		alpha = HAS_FLAG(USE_NORMAL_ALPHA_AS_ALPHA) ? normalValue.a : 1.0;

		float blend = gMapsMaterial.DetailsNormalBlend;
		if (blend > 0.0) {
			float4 detailsNormalValue = gDetailsNormalMap.Sample(samAnisotropic, pin.Tex * gMapsMaterial.DetailsUvMultipler);
			normalValue += (detailsNormalValue - 0.5) * blend * (1.0 - mask);
		}

		normal = normalize(NormalSampleToWorldSpace(normalValue.xyz, pin.NormalW, pin.TangentW));
	} else {
		normal = normalize(pin.NormalW);
		alpha = 1.0;
	}

	lighted = CalculateLight_Maps_Sun(diffuseValue.rgb, normal, pin.PosW, txMapsSpecularMultipler, txMapsSpecularExpMultipler, pin.PosH.xy);
	AlphaTest(alpha);
}

float GetReflectionStrength(float3 normalW, float3 toEyeW) {
	// float rid = 1 - saturate(dot(toEyeW, normalW) - gReflectiveMaterial.FresnelC);

	// float rid = 1 - saturate(dot(toEyeW, normalW));
	// float rim = pow(rid, gReflectiveMaterial.FresnelExp);
	// return min(max(rim, gReflectiveMaterial.FresnelC), gReflectiveMaterial.FresnelMaxLevel);

	float rid = 1 - saturate(dot(toEyeW, normalW));
	float rim = pow(rid, gReflectiveMaterial.FresnelExp);
	return min(rim + gReflectiveMaterial.FresnelC, gReflectiveMaterial.FresnelMaxLevel);

	//float d = dot(toEyeW, normalW);
	//float y = 0.0 < d;
	//return min(exp(log(abs(1.0 - d)) * gReflectiveMaterial.FresnelExp), gReflectiveMaterial.FresnelC) + y;
}

float3 CalculateReflection(float3 lighted, float3 posW, float3 normalW) {
	float3 toEyeW = normalize(gEyePosW - posW);
	float3 reflected = reflect(-toEyeW, normalW);
	float3 refl = GetReflection(reflected, gMaterial.SpecularExp);

	float val = GetReflectionStrength(normalW, toEyeW);
	if (!HAS_FLAG(IS_ADDITIVE)) {
		lighted *= 1 - val;
	}

	return lighted + refl * val;
}

float3 CalculateReflection_Maps(float3 lighted, float3 posW, float3 normalW, float specularExpMultipler, float reflectionMultipler) {
	float3 toEyeW = normalize(gEyePosW - posW);
	float3 reflected = reflect(-toEyeW, normalW);
	float3 refl = GetReflection(reflected, (gMaterial.SpecularExp + 400 * GET_FLAG(IS_CARPAINT)) * specularExpMultipler);

	float val = GetReflectionStrength(normalW, toEyeW);
	if (!HAS_FLAG(IS_ADDITIVE)) {
		lighted *= 1 - val;
	}

	return lighted + refl * val * reflectionMultipler;
}