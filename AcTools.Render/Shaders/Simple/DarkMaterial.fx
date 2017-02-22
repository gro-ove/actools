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
}

cbuffer cbTextureFlatMirror {
	// matrix gWorldViewProjInv;
	float4 gScreenSize;
	float gFlatMirrorPower;
};

// z-buffer tricks if needed
#define FixPosH(x) (x)

#define farplane 500
#define Fcoef (2.0 / log2(farplane + 1.0))

float4 FixPosH_(float4 posH) {
	posH.z = log2(max(1e-6, 1.0 + posH.w)) * Fcoef - 1.0;
	return posH;
}

float4 CalculatePosH(float3 posL) {
	return FixPosH(mul(float4(posL, 1.0f), gWorldViewProj));
}

// real reflection (not used by default)
TextureCube gReflectionCubemap;

// shadows
#define ENABLE_SHADOWS 1
#define NUM_SPLITS 1
#define SHADOW_MAP_SIZE 2048
#include "Shadows.fx"

SamplerState samAnisotropic {
	Filter = ANISOTROPIC;
	MaxAnisotropy = 8;

	AddressU = WRAP;
	AddressV = WRAP;
};

float3 NormalSampleToWorldSpace(float3 normalMapSample, float3 unitNormalW, float3 tangentW){
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
	return saturate((d.y + 0.02) * 5.0 * e) * saturate(1 - pow(d.y * 1.5, 2)) * 0.4;
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

float3 GetReflection(float3 reflected, float specularExp) {
	float edge = specularExp / 10.0 + 1.0;
	float fake = saturate(GetFakeHorizon(reflected, edge) + GetFakeStudioLights(reflected, edge));
	return gBackgroundColor * (1 - fake) * 1.2 + fake * 1.6;
}

#define HAS_FLAG(x) ((gMaterial.Flags & x) == x)
#define GET_FLAG(x) ((gMaterial.Flags & x) / x)

void AlphaTest(float alpha) {
	if (HAS_FLAG(ALPHA_TEST)) clip(alpha - 0.5);
}

float Luminance(float3 color) {
	return dot(color, float3(0.299f, 0.587f, 0.114f));
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
	return CalculateSpecularLight(nDotH, gMaterial.SpecularExp * specularExpMultipler, gMaterial.Specular) +
		CalculateSpecularLight(nDotH, gMapsMaterial.SunSpecularExp * specularExpMultipler, gMapsMaterial.SunSpecular);
}

float3 GetAmbient(float3 normal) {
	float up = saturate(normal.y * 0.5 + 0.5);
	return gAmbientDown + up * gAmbientRange;
}

float GetDiffuseMultipler(float3 normal) {
	return saturate(dot(normal, gLightDir));
}

float GetShadow_ConsiderMirror(float3 position) {
	if (gFlatMirrored) {
		position.y = -position.y;
	}
	return GetShadow(position);
}

float3 CalculateLight(float3 normal, float3 position) {
	float3 ambient = GetAmbient(normal);
	float diffuseMultipler = GetDiffuseMultipler(normal);

#if ENABLE_SHADOWS == 1
	diffuseMultipler *= GetShadow_ConsiderMirror(position);
#endif

	float3 specular = CalculateSpecularLight(normal, position);
	return gMaterial.Ambient * ambient + (gMaterial.Diffuse + specular) * gLightColor * diffuseMultipler + gMaterial.Emissive;
}

float3 CalculateLight_Maps(float3 normal, float3 position, float specularMultipler, float specularExpMultipler) {
	float3 ambient = GetAmbient(normal);
	float diffuseMultipler = GetDiffuseMultipler(normal);

#if ENABLE_SHADOWS == 1
	diffuseMultipler *= GetShadow_ConsiderMirror(position);
#endif

	float3 specular = CalculateSpecularLight_Maps(normal, position, specularExpMultipler) * specularMultipler;
	return gMaterial.Ambient * ambient + (gMaterial.Diffuse + specular) * gLightColor * diffuseMultipler + gMaterial.Emissive;
}

//////////////// Different material types

void CalculateLighted(PS_IN pin, out float3 lighted, out float alpha, out float3 normal) {
	float4 diffuseValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex);

	alpha = diffuseValue.a;
	normal = normalize(pin.NormalW);
	lighted = CalculateLight(normal, pin.PosW) * diffuseValue.rgb;

	AlphaTest(alpha);
}

void CalculateLighted_Nm(PS_IN pin, out float3 lighted, out float alpha, out float3 normal) {
	float4 diffuseValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex);
	float4 normalValue = gNormalMap.Sample(samAnisotropic, pin.Tex);

	alpha = diffuseValue.a * normalValue.a;
	normal = normalize(NormalSampleToWorldSpace(normalValue.xyz, pin.NormalW, pin.TangentW));
	lighted = CalculateLight(normal, pin.PosW) * diffuseValue.rgb;

	AlphaTest(alpha);
}

void CalculateLighted_NmUvMult(PS_IN pin, out float3 lighted, out float alpha, out float3 normal) {
	float4 diffuseValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex * (1 + gNmUvMultMaterial.DiffuseMultipler));
	float4 normalValue = gNormalMap.Sample(samAnisotropic, pin.Tex * (1 + gNmUvMultMaterial.NormalMultipler));

	alpha = diffuseValue.a;
	normal = normalize(NormalSampleToWorldSpace(normalValue.xyz, pin.NormalW, pin.TangentW));
	lighted = CalculateLight(normal, pin.PosW) * diffuseValue.rgb;

	AlphaTest(alpha);
}

void CalculateLighted_AtNm(PS_IN pin, out float3 lighted, out float alpha, out float3 normal) {
	float4 diffuseValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex);
	float4 normalValue = gNormalMap.Sample(samAnisotropic, pin.Tex);

	alpha = diffuseValue.a;
	normal = normalize(NormalSampleToWorldSpace(normalValue.xyz, pin.NormalW, pin.TangentW));
	lighted = CalculateLight(normal, pin.PosW) * diffuseValue.rgb;

	AlphaTest(alpha);
}

void CalculateLighted_DiffMaps(PS_IN pin, out float3 lighted, out float alpha, out float3 normal) {
	float4 diffuseValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex);
	float4 normalValue = gNormalMap.Sample(samAnisotropic, pin.Tex);

	alpha = diffuseValue.a;
	normal = normalize(NormalSampleToWorldSpace(normalValue.xyz, pin.NormalW, pin.TangentW));
	lighted = CalculateLight_Maps(normal, pin.PosW, alpha, alpha) * diffuseValue.rgb;

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

	lighted = CalculateLight_Maps(normal, pin.PosW, txMapsSpecularMultipler, txMapsSpecularExpMultipler) * diffuseValue.rgb;

	AlphaTest(alpha);
}

float3 CalculateReflection(float3 lighted, float3 posW, float3 normalW) {
	float3 toEyeW = normalize(gEyePosW - posW);
	float3 reflected = reflect(-toEyeW, normalW);
	float3 refl = GetReflection(reflected, gMaterial.SpecularExp);

	float rid = 1 - saturate(dot(toEyeW, normalW) - gReflectiveMaterial.FresnelC);
	float rim = pow(rid, gReflectiveMaterial.FresnelExp);
	float val = min(rim, gReflectiveMaterial.FresnelMaxLevel);

	if (!HAS_FLAG(IS_ADDITIVE)) {
		lighted *= 1 - val;
	}

	return lighted + refl * val;
}

float3 CalculateReflection_Maps(float3 lighted, float3 posW, float3 normalW, float specularExpMultipler, 
		float reflectionMultipler) {
	float3 toEyeW = normalize(gEyePosW - posW);
	float3 reflected = reflect(-toEyeW, normalW);
	float3 refl = GetReflection(reflected, (gMaterial.SpecularExp + 400 * GET_FLAG(IS_CARPAINT)) * specularExpMultipler);

	float rid = 1 - saturate(dot(toEyeW, normalW) - gReflectiveMaterial.FresnelC);
	float rim = pow(rid, gReflectiveMaterial.FresnelExp);
	float val = min(rim, gReflectiveMaterial.FresnelMaxLevel);

	if (!HAS_FLAG(IS_ADDITIVE)) {
		lighted *= 1 - val;
	}

	return lighted + refl * val * reflectionMultipler;
}

//// Standart

float4 ps_Standard(PS_IN pin) : SV_Target{
	float alpha; float3 lighted, normal;
	CalculateLighted(pin, lighted, alpha, normal);
	return float4(lighted, alpha);
}

technique10 Standard {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Standard()));
	}
}

//// Alpha

float4 ps_Alpha(PS_IN pin) : SV_Target{
	float alpha; float3 lighted, normal;
	CalculateLighted(pin, lighted, alpha, normal);
	return float4(lighted, alpha * gAlphaMaterial.Alpha);
}

technique10 Alpha {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Alpha()));
	}
}

//// Reflective

float4 ps_Reflective(PS_IN pin) : SV_Target{
	float alpha; float3 lighted, normal;
	CalculateLighted(pin, lighted, alpha, normal);
	return float4(CalculateReflection(lighted, pin.PosW, normal), alpha);
}

technique10 Reflective {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Reflective()));
	}
}

//// NM

float4 ps_Nm(PS_IN pin) : SV_Target{
	float alpha; float3 lighted, normal;
	CalculateLighted_Nm(pin, lighted, alpha, normal);
	return float4(CalculateReflection(lighted, pin.PosW, normal), alpha);
}

technique10 Nm {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Nm()));
	}
}

//// NM UV Mult

float4 ps_NmUvMult(PS_IN pin) : SV_Target{
	float alpha; float3 lighted, normal;
	CalculateLighted_NmUvMult(pin, lighted, alpha, normal);
	return float4(CalculateReflection(lighted, pin.PosW, normal), alpha);
}

technique10 NmUvMult {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_NmUvMult()));
	}
}

//// AT_NM

float4 ps_AtNm(PS_IN pin) : SV_Target{
	float alpha; float3 lighted, normal;
	CalculateLighted_AtNm(pin, lighted, alpha, normal);
	return float4(lighted, alpha);
}

technique10 AtNm {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_AtNm()));
	}
}

//// Maps

float4 ps_Maps(PS_IN pin) : SV_Target{
	float3 mapsValue = gMapsMap.Sample(samAnisotropic, pin.Tex).rgb;

	float alpha, mask; float3 lighted, normal;
	CalculateLighted_Maps(pin, mapsValue.r, mapsValue.g, lighted, alpha, mask, normal);
	return float4(CalculateReflection_Maps(lighted, pin.PosW, normal, mapsValue.g, mapsValue.b), alpha);
}

technique10 Maps {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Maps()));
	}
}

technique10 SkinnedMaps {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_skinned()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Maps()));
	}
}

float4 ps_DiffMaps(PS_IN pin) : SV_Target{
	float alpha, mask; float3 lighted, normal;
	CalculateLighted_DiffMaps(pin, lighted, alpha, normal);
	return float4(CalculateReflection_Maps(lighted, pin.PosW, normal, alpha, alpha), 1.0);
}

technique10 DiffMaps {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_DiffMaps()));
	}
}

//// GL

float4 ps_Gl(PS_IN pin) : SV_Target {
	return float4(normalize(pin.NormalW), 1.0);
}

technique10 Gl {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Gl()));
	}
}

technique10 SkinnedGl {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_skinned()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Gl()));
	}
}

//// Collider

float4 ps_Collider(PS_IN pin) : SV_Target {
	float3 toEyeW = normalize(gEyePosW - pin.PosW);
	float3 normal = normalize(pin.NormalW);
	float opacify = pow(1.0 - dot(normal, toEyeW), 5.0);
	return float4((float3)1.0 - abs(normal), opacify);
}

technique10 Collider {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Collider()));
	}
}

//// Debug

float4 ps_Debug(PS_IN pin) : SV_Target {
	float3 position = pin.PosW;
	float4 diffuseMapValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex);

	float3 normal;
	float alpha;
	if (HAS_FLAG(HAS_NORMAL_MAP)) {
		float4 normalValue = gNormalMap.Sample(samAnisotropic, pin.Tex);
		alpha = HAS_FLAG(USE_NORMAL_ALPHA_AS_ALPHA) ? normalValue.a : gDiffuseMap.Sample(samAnisotropic, pin.Tex).a;
		normal = normalize(NormalSampleToWorldSpace(normalValue.xyz, pin.NormalW, pin.TangentW));
	} else {
		normal = normalize(pin.NormalW);
		alpha = diffuseMapValue.a;
	}

	float3 ambient = GetAmbient(normal);
	float diffuseMultipler = GetDiffuseMultipler(normal);

#if ENABLE_SHADOWS == 1
	diffuseMultipler *= GetShadow_ConsiderMirror(position);
#endif

	float nDotH = GetNDotH(normal, position);
	float specular = CalculateSpecularLight(nDotH, 50, 0.5);

	float distance = length(gEyePosW - position);

	//
	float3 toEyeW = normalize(gEyePosW - position);
	float3 reflected = reflect(-toEyeW, normal);
	float3 refl = GetReflection(reflected, 2);

	float rid = 1 - saturate(dot(toEyeW, normal) - 0.016);
	float rim = pow(rid, 4.0);
	float val = min(rim, 0.05);
	//

	float3 light = 0.4 * ambient + (0.5 + specular) * gLightColor * diffuseMultipler + refl * val + gMaterial.Emissive * diffuseMapValue.rgb;
	AlphaTest(alpha);

	return float4(light, alpha);
}

technique10 Debug {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Debug()));
	}
}

technique10 SkinnedDebug {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_skinned()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Debug()));
	}
}

//////////////// Misc stuff

technique10 DepthOnly {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_depthOnly()));
		SetGeometryShader(NULL);
		SetPixelShader(NULL);
	}
}

technique10 SkinnedDepthOnly {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_depthOnly_skinned()));
		SetGeometryShader(NULL);
		SetPixelShader(NULL);
	}
}

pt_PS_IN vs_AmbientShadow(pt_VS_IN vin) {
	pt_PS_IN vout;

	float3 posW = mul(float4(vin.PosL, 1.0f), gWorld).xyz;
	float3 eyeL = mul(float4(gEyePosW, 1.0f), transpose(gWorld)).xyz;
	float3 toEyeL = normalize(eyeL - vin.PosL);

	float4 p = CalculatePosH(vin.PosL);
	float4 r = CalculatePosH(vin.PosL + toEyeL * 0.02);
	p.z = r.z;

	vout.PosH = p;
	vout.PosW = posW;
	vout.Tex = vin.Tex;

	return vout;
}

float4 ps_AmbientShadow(pt_PS_IN pin) : SV_Target {
	float value = gDiffuseMap.Sample(samAnisotropic, pin.Tex).r;
	float lightBrightness = saturate(Luminance(gLightColor) * 1.5);

#if ENABLE_SHADOWS == 1
	float shadow = GetShadow(pin.PosW);
	return float4(0.0, 0.0, 0.0, value * (1.0 - shadow * lightBrightness * 0.95));
#else
	return float4(0.0, 0.0, 0.0, value);
#endif
}

technique10 AmbientShadow {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_AmbientShadow()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_AmbientShadow()));
	}
}

float4 ps_Mirror(PS_IN pin) : SV_Target {
	float3 toEyeW = normalize(gEyePosW - pin.PosW);
	float3 reflected = reflect(-toEyeW, pin.NormalW);
	float3 refl = GetReflection(reflected, 500) * 0.8;
	return float4(refl, 1.0);
}

technique10 Mirror {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Mirror()));
	}
}

float4 ps_FlatMirror(pt_PS_IN pin) : SV_Target{
	float3 eyeW = gEyePosW - pin.PosW;
	float3 toEyeW = normalize(eyeW);
	float3 normal = float3(0, 1, 0);
	float fresnel = (0.7 + 0.3 * pow(dot(toEyeW, normal), 4)) * (1 - gFlatMirrorPower);
	float distance = length(eyeW);

	float shadow = GetShadow(pin.PosW);
	float3 light = saturate(dot(normal, gLightDir)) * shadow * gLightColor;
	float opacity = fresnel * saturate(1.2 - distance / 60);
	return float4(light * opacity * gBackgroundColor, opacity);
}

technique10 FlatMirror {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_pt_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_FlatMirror()));
	}
}

float4 ps_FlatTextureMirror(pt_PS_IN pin) : SV_Target{
	float2 tex = pin.PosH.xy / gScreenSize.xy;
	float4 value = gDiffuseMap.Sample(samAnisotropic, tex);

	// value = float4(value.rgb * gFlatMirrorPower + gBackgroundColor * (1.0 - gFlatMirrorPower), value.a);

	float3 eyeW = gEyePosW - pin.PosW;
	float3 toEyeW = normalize(eyeW);
	float3 normal = float3(0, 1, 0);
	float fresnel = (0.7 + 0.3 * pow(dot(toEyeW, normal), 4)) * (1 - gFlatMirrorPower);
	float distance = length(eyeW);

	float shadow = GetShadow(pin.PosW);
	float3 light = saturate(dot(normal, gLightDir)) * shadow * gLightColor;
	float opacity = fresnel * saturate(1.2 - distance / 60);
	return float4(value * (1 - opacity) + light * opacity * gBackgroundColor, 1.0);
}

technique10 FlatTextureMirror {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_pt_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_FlatTextureMirror()));
	}
}

/*float3 GetReflPosition(float2 uv) {
	float4 position = mul(float4(uv.x * 2 - 1, -(uv.y * 2 - 1), gMapsMap.Sample(samAnisotropic, uv).x, 1), gWorldViewProjInv);
	return position.xyz / position.w;
}

float4 ps_FlatTextureMirrorDark(pt_PS_IN pin) : SV_Target{
	float2 tex = pin.PosH.xy / gScreenSize.xy;
	float3 position = GetReflPosition(tex);

	float3 reflDelta = gEyePosW - position;
	float reflDistance = (abs(position.y) / abs(reflDelta.y)) * length(reflDelta);

	// return reflDistance;

	float3 normals = (gNormalMap.Sample(samAnisotropic, pin.Tex * 1000) - 0.5) * 2;
	tex.x += normals.x * reflDistance / 100.0;

	float4 value = gDiffuseMap.Sample(samAnisotropic, tex);

	value = float4(value.rgb * 0.999 + gBackgroundColor * 0.0, value.a);

	float3 eyeW = gEyePosW - pin.PosW;
	float3 toEyeW = normalize(eyeW);
	float3 normal = float3(0, 1, 0);
	float fresnel = 0.11 + 0.52 * pow(dot(toEyeW, normal), 4);
	float distance = length(eyeW);

	float3 light = saturate(dot(normal, gLightDir)) * GetShadow(pin.PosW) * gLightColor;
	float opacity = fresnel * saturate(1 - distance / 60);
	return float4(value * (1 - opacity) + light * opacity, 1.0);
}

technique10 FlatTextureMirrorDark {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_pt_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_FlatTextureMirrorDark()));
	}
}*/

float4 ps_FlatBackgroundGround(pt_PS_IN pin) : SV_Target {
	// distance to viewer
	float3 eyeW = gEyePosW - pin.PosW;
	float distance = length(eyeW);

	// if viewing angle is small, “fresnel” is smaller → result is more backgroundy
	float fresnel = 0.21 + 0.12 * pow(normalize(eyeW).y, 4);

	// summary opacity (aka non-backgroundy) is combined from viewing angle and distance
	// for smooth transition
	float opacity = fresnel * saturate(1.2 - distance / 40);

	// how much surface is lighed according to light direction
	float3 light = gLightDir.y * gLightColor;

	// shadow at the point
	float shadow = GetShadow(pin.PosW);
	
	// ambient color
	float3 ambient = gAmbientDown * 0.73 + gAmbientRange * 0.27;

	// bright light source means more backgroundy surface
	float lightBrightness = Luminance(gLightColor);

	// separately color in lighted and shadowed areas
	// 100%-target is to match those colors if there is no light (aka no shadow)
	float3 lightPart = gBackgroundColor * (1 - opacity * lightBrightness) + light * opacity;
	float3 shadowPart = (1 - lightBrightness) * lightPart + lightBrightness * (ambient * Luminance(gBackgroundColor) * 0.32 + gBackgroundColor * 0.22);

	// combining
	return float4(lightPart * shadow + shadowPart * (1 - shadow), 1.0);
}

technique10 FlatBackgroundGround {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_pt_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_FlatBackgroundGround()));
	}
}

float4 ps_FlatAmbientGround(pt_PS_IN pin) : SV_Target {
	float3 normal = float3(0, 1, 0);
	float3 position = pin.PosW;

	float3 ambient = GetAmbient(normal);
	float diffuseMultipler = GetDiffuseMultipler(normal);

#if ENABLE_SHADOWS == 1
	diffuseMultipler *= GetShadow_ConsiderMirror(position);
#endif

	float nDotH = GetNDotH(normal, position);
	float specular = CalculateSpecularLight(nDotH, 50, 0.5);

	float distance = length(gEyePosW - position);

	float3 light = 0.4 * (gAmbientDown + 0.2 * gAmbientRange) + (0.5 + specular) * gLightColor * diffuseMultipler;
	return float4(light, saturate(1.5 - distance / 60));
}

technique10 FlatAmbientGround {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_pt_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_FlatAmbientGround()));
	}
}