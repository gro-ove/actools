// structs
static const dword HAS_NORMAL_MAP = 1;
static const dword USE_DIFFUSE_ALPHA_AS_MAP = 2;
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
}

// real reflection (not used by default)
TextureCube gReflectionCubemap;

// shadows
#define ENABLE_SHADOWS true
#define NUM_SPLITS 1
#define SHADOW_MAP_SIZE 2048
#include "Shadows.fx"

SamplerState samAnisotropic {
	Filter = ANISOTROPIC;
	MaxAnisotropy = 8;

	AddressU = WRAP;
	AddressV = WRAP;
};

float3 NormalSampleToWorldSpace(float3 normalMapSample, float3 N, float3 T, float3 B) {
	return mul(2.0 * normalMapSample - 1.0, float3x3(T, B, N));
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
	vout.PosH = mul(float4(vin.PosL, 1.0f), gWorldViewProj);
	vout.Tex = vin.Tex;

	return vout;
}

struct VS_IN {
	float3 PosL       : POSITION;
	float3 NormalL    : NORMAL;
	float2 Tex        : TEXCOORD;
	float3 TangentL   : TANGENT;
};

struct PS_IN {
	float4 PosH       : SV_POSITION;
	float3 PosW       : POSITION;
	float3 NormalW    : NORMAL;
	float2 Tex        : TEXCOORD;
	float3 TangentW   : TANGENT;
	float3 BitangentW : BITANGENT;
};

PS_IN vs_main(VS_IN vin) {
	PS_IN vout;

	vout.PosW = mul(float4(vin.PosL, 1.0f), gWorld).xyz;
	vout.NormalW = mul(vin.NormalL, (float3x3)gWorldInvTranspose);
	vout.TangentW = mul(vin.TangentL, (float3x3)gWorldInvTranspose);
	vout.BitangentW = mul(cross(vin.NormalL, vin.TangentL), (float3x3)gWorldInvTranspose);

	vout.PosH = mul(float4(vin.PosL, 1.0f), gWorldViewProj);
	vout.Tex = vin.Tex;

	return vout;
}

float GetReflection(float3 reflected, float specularExp) {
	float edge = specularExp / 10.0 + 1.0;
	return (
		saturate((0.3 - abs(0.6 - reflected.y)) * edge) +
		saturate((0.1 - abs(0.1 - reflected.y)) * edge)
	) * saturate((0.3 - abs(0.6 - sin(reflected.x * 16.0) - 0.5)) * edge) * 1.2;
}

#define HAS_FLAG(x) ((gMaterial.Flags & x) == x)
#define GET_FLAG(x) ((gMaterial.Flags & x) / x)

void AlphaTest(float alpha) {
	if (HAS_FLAG(ALPHA_TEST)) clip(alpha - 0.5);
}

//////////////// Simple lighting

#define gLightMultipler 0.4
#define gAmbientDown float3(0.3, 0.5, 0.7)*1.2
#define gAmbientRange float3(0.3, 0.1, -0.2)*1.2

float3 CalculateBaseLight(float3 normal) {
	if (gFlatMirrored) {
		normal.y = -normal.y;
	}

	float up = saturate(normal.y * 0.5 + 0.5);
	float3 ambient = gAmbientDown + up * gAmbientRange;
	return (gMaterial.Ambient + gMaterial.Diffuse * (saturate(dot(normal, gLightDir)) * gLightMultipler + ambient));
}

float3 CalculateBaseLight_ConsiderShadows(float3 normal, float3 position) {
	if (gFlatMirrored) {
		normal.y = -normal.y;
		position.y = -position.y;
	}

	float up = saturate(normal.y * 0.5 + 0.5);
	float3 ambient = gAmbientDown + up * gAmbientRange;
	return (gMaterial.Ambient + gMaterial.Diffuse * (saturate(dot(normal, gLightDir)) * gLightMultipler * GetShadow(position) + ambient));
}

float3 CalculateSpecularLight(float3 normal, float3 position) {
	float3 toEye = normalize(gEyePosW - position);
	float3 halfway = normalize(toEye + gLightDir);
	float nDotH = saturate(dot(halfway, normal));
	return pow(nDotH, max(gMaterial.SpecularExp, 0.1)) * gMaterial.Specular;
}

float3 CalculateSpecularLight_Maps(float3 normal, float3 position, float specularMultipler, float specularExpMultipler) {
	float3 toEye = normalize(gEyePosW - position);
	float3 halfway = normalize(toEye + gLightDir);
	float nDotH = saturate(dot(halfway, normal));
	return pow(nDotH, max(gMaterial.SpecularExp * specularExpMultipler, 0.1)) * gMaterial.Specular * specularMultipler;
}

float3 CalculateLight(float3 normal, float3 position) {
#if ENABLE_SHADOWS == true
	return CalculateBaseLight_ConsiderShadows(normal, position) + CalculateSpecularLight(normal, position)
		+ gMaterial.Emissive;
#else
	return CalculateBaseLight(normal) + CalculateSpecularLight(normal, position)
		+ gMaterial.Emissive;
#endif
}

float3 CalculateLight_Maps(float3 normal, float3 position, float specularMultipler, float specularExpMultipler) {
#if ENABLE_SHADOWS == true
	return CalculateBaseLight_ConsiderShadows(normal, position) + CalculateSpecularLight_Maps(normal, position, specularMultipler, specularExpMultipler)
		+ gMaterial.Emissive;
#else
	return CalculateBaseLight(normal) + CalculateSpecularLight_Maps(normal, position, specularMultipler, specularExpMultipler)
		+ gMaterial.Emissive;
#endif
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
	normal = normalize(NormalSampleToWorldSpace(normalValue.xyz, pin.NormalW, pin.TangentW, pin.BitangentW));
	lighted = CalculateLight(normal, pin.PosW) * diffuseValue.rgb;

	AlphaTest(alpha);
}

void CalculateLighted_NmUvMult(PS_IN pin, out float3 lighted, out float alpha, out float3 normal) {
	float4 diffuseValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex * (1 + gNmUvMultMaterial.DiffuseMultipler));
	float4 normalValue = gNormalMap.Sample(samAnisotropic, pin.Tex * (1 + gNmUvMultMaterial.NormalMultipler));

	alpha = diffuseValue.a;
	normal = normalize(NormalSampleToWorldSpace(normalValue.xyz, pin.NormalW, pin.TangentW, pin.BitangentW));
	lighted = CalculateLight(normal, pin.PosW) * diffuseValue.rgb;

	AlphaTest(alpha);
}

void CalculateLighted_AtNm(PS_IN pin, out float3 lighted, out float alpha, out float3 normal) {
	float4 diffuseValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex);
	float4 normalValue = gNormalMap.Sample(samAnisotropic, pin.Tex);

	alpha = diffuseValue.a;
	normal = normalize(NormalSampleToWorldSpace(normalValue.xyz, pin.NormalW, pin.TangentW, pin.BitangentW));
	lighted = CalculateLight(normal, pin.PosW) * diffuseValue.rgb;

	AlphaTest(alpha);
}

void CalculateLighted_DiffMaps(PS_IN pin, out float3 lighted, out float alpha, out float3 normal) {
	float4 diffuseValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex);
	float4 normalValue = gNormalMap.Sample(samAnisotropic, pin.Tex);

	alpha = diffuseValue.a;
	normal = normalize(NormalSampleToWorldSpace(normalValue.xyz, pin.NormalW, pin.TangentW, pin.BitangentW));
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

		normal = normalize(NormalSampleToWorldSpace(normalValue.xyz, pin.NormalW, pin.TangentW, pin.BitangentW));
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
	float refl = GetReflection(reflected, gMaterial.SpecularExp);

	float rid = 1 - saturate(dot(toEyeW, normalW) - gReflectiveMaterial.FresnelC);
	float rim = pow(rid, gReflectiveMaterial.FresnelExp);
	float val = min(rim, gReflectiveMaterial.FresnelMaxLevel);

	return lighted - val * 0.32 * (1 - GET_FLAG(IS_ADDITIVE)) + refl * val;
}

float3 CalculateReflection_Maps(float3 lighted, float3 posW, float3 normalW, float specularExpMultipler, 
		float reflectionMultipler) {
	float3 toEyeW = normalize(gEyePosW - posW);
	float3 reflected = reflect(-toEyeW, normalW);
	float refl = GetReflection(reflected, (gMaterial.SpecularExp + 400 * GET_FLAG(IS_CARPAINT)) * specularExpMultipler);

	float rid = 1 - saturate(dot(toEyeW, normalW) - gReflectiveMaterial.FresnelC);
	float rim = pow(rid, gReflectiveMaterial.FresnelExp);
	float val = min(rim, gReflectiveMaterial.FresnelMaxLevel);

	return lighted - val * 0.32 * (1 - GET_FLAG(IS_ADDITIVE)) + refl * val * reflectionMultipler;
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

float4 ps_Gl(PS_IN pin) : SV_Target{
	return float4(normalize(pin.NormalW), 1.0);
}

technique10 Gl {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Gl()));
	}
}


//////////////// Misc stuff

float4 ps_AmbientShadow(pt_PS_IN pin) : SV_Target {
	return float4(0.0, 0.0, 0.0, gDiffuseMap.Sample(samAnisotropic, pin.Tex).r);
}

technique10 AmbientShadow {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_pt_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_AmbientShadow()));
	}
}

float4 ps_Mirror(PS_IN pin) : SV_Target {
	float3 toEyeW = normalize(gEyePosW - pin.PosW);
	float3 reflected = reflect(-toEyeW, pin.NormalW);
	float refl = GetReflection(reflected, gMaterial.SpecularExp);
	return float4(refl, refl, refl, 1.0);
}

technique10 Mirror {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Mirror()));
	}
}

float4 ps_FlatMirror(pt_PS_IN pin) : SV_Target {
	float3 eyeW = gEyePosW - pin.PosW;
	float3 toEyeW = normalize(eyeW);
	float3 normal = float3(0, 1, 0);
	float fresnel = 0.11 + 0.52 * pow(dot(toEyeW, normal), 4);
	float distance = length(eyeW);

	float light = saturate(dot(normal, gLightDir)) * gLightMultipler * GetShadow(pin.PosW);
	return float4(
		light, light, light,
		fresnel * saturate(1 - distance / 60));
}

technique10 FlatMirror {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_pt_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_FlatMirror()));
	}
}