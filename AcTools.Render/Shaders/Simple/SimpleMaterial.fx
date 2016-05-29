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
}

cbuffer cbPerFrame {
	float3 gEyePosW;
}

#define gAmbientDown float3(1, 1, 1)
#define gAmbientRange float3(0.5, 0.5, 0.5)

SamplerState samAnisotropic {
	Filter = ANISOTROPIC;
	MaxAnisotropy = 16;

	AddressU = WRAP;
	AddressV = WRAP;
};

float3 CalcAmbient(float3 normal) {
	float up = normal.y * 0.5 + 0.5;
	return gMaterial.Ambient * 2.0 + gMaterial.Diffuse * up;
	//return gAmbientDown + up * gAmbientRange;
}

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

float PseudoReflection(float3 reflected, float specularExp) {
	float val = reflected.y + reflected.x * 0.1;
	float edge = specularExp / 2.0 + 1.0;
	return (
		saturate((0.3 - abs(0.6 - val)) * edge) +
		saturate((0.1 - abs(0.1 - val)) * edge)
	);
}

#define HAS_FLAG(x) ((gMaterial.Flags & x) == x)
#define GET_FLAG(x) ((gMaterial.Flags & x) / x)

void AlphaTest(float alpha) {
	if (HAS_FLAG(ALPHA_TEST)) clip(alpha - 0.5);
}

//////////////// Different material types

void CalculateLighted(PS_IN pin, out float3 lighted, out float alpha, out float3 normal) {
	float4 diffuseValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex);

	alpha = diffuseValue.a;
	normal = normalize(pin.NormalW);
	lighted = (CalcAmbient(normal) + gMaterial.Emissive) * diffuseValue.rgb;

	AlphaTest(alpha);
}

void CalculateLighted_Nm(PS_IN pin, out float3 lighted, out float alpha, out float3 normal) {
	float4 diffuseValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex);
	float4 normalValue = gNormalMap.Sample(samAnisotropic, pin.Tex);

	alpha = diffuseValue.a * normalValue.a;
	normal = normalize(NormalSampleToWorldSpace(normalValue.xyz, pin.NormalW, pin.TangentW, pin.BitangentW));
	lighted = (CalcAmbient(normal) + gMaterial.Emissive) * diffuseValue.rgb;

	AlphaTest(alpha);
}

void CalculateLighted_NmUvMult(PS_IN pin, out float3 lighted, out float alpha, out float3 normal) {
	float4 diffuseValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex * (1 + gNmUvMultMaterial.DiffuseMultipler));
	float4 normalValue = gNormalMap.Sample(samAnisotropic, pin.Tex * (1 + gNmUvMultMaterial.NormalMultipler));

	alpha = diffuseValue.a;
	normal = normalize(NormalSampleToWorldSpace(normalValue.xyz, pin.NormalW, pin.TangentW, pin.BitangentW));
	lighted = (CalcAmbient(normal) + gMaterial.Emissive) * diffuseValue.rgb;

	AlphaTest(alpha);
}

void CalculateLighted_AtNm(PS_IN pin, out float3 lighted, out float alpha, out float3 normal) {
	float4 diffuseValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex);
	float4 normalValue = gNormalMap.Sample(samAnisotropic, pin.Tex);

	alpha = diffuseValue.a;
	normal = normalize(NormalSampleToWorldSpace(normalValue.xyz, pin.NormalW, pin.TangentW, pin.BitangentW));
	lighted = (CalcAmbient(normal) + gMaterial.Emissive) * diffuseValue.rgb;

	AlphaTest(alpha);
}

void CalculateLighted_Maps(PS_IN pin, out float3 lighted, out float alpha, out float mask, out float3 normal) {
	float4 diffuseValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex);

	mask = diffuseValue.a;

	if (HAS_FLAG(HAS_DETAILS_MAP)) {
		float4 details = gDetailsMap.Sample(samAnisotropic, pin.Tex * gMapsMaterial.DetailsUvMultipler);
		diffuseValue = diffuseValue * (details * (1 - mask) + mask);
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

	lighted = (CalcAmbient(normal) + gMaterial.Emissive) * diffuseValue.rgb;

	AlphaTest(alpha);
}

float3 CalculateReflection(float3 lighted, float3 posW, float3 normalW) {
	float3 toEyeW = normalize(gEyePosW - posW);
	float3 reflected = reflect(-toEyeW, normalW);
	float refl = PseudoReflection(reflected, gMaterial.SpecularExp);

	float rid = 1 - saturate(dot(toEyeW, normalW) - gReflectiveMaterial.FresnelC);
	float rim = pow(rid, gReflectiveMaterial.FresnelExp);
	float val = min(rim, gReflectiveMaterial.FresnelMaxLevel);

	return lighted - val * 0.32 * (1 - GET_FLAG(IS_ADDITIVE)) + refl * val;
}

float3 CalculateReflection_Maps(float3 lighted, float3 posW, float3 normalW, float specularExpMultipler, 
		float reflectionMultipler) {
	float3 toEyeW = normalize(gEyePosW - posW);
	float3 reflected = reflect(-toEyeW, normalW);
	float refl = PseudoReflection(reflected, (gMaterial.SpecularExp + 400 * GET_FLAG(IS_CARPAINT)) * specularExpMultipler);

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
	float alpha, mask; float3 lighted, normal;
	CalculateLighted_Maps(pin, lighted, alpha, mask, normal);

	float3 mapsValue = gMapsMap.Sample(samAnisotropic, pin.Tex).rgb;
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
	CalculateLighted_AtNm(pin, lighted, alpha, normal);
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

float4 ps_Mirror(PS_IN pin) : SV_Target{
	float3 toEyeW = normalize(gEyePosW - pin.PosW);
	float3 reflected = reflect(-toEyeW, pin.NormalW);
	float refl = PseudoReflection(reflected, gMaterial.SpecularExp);
	return float4(refl, refl, refl, 1.0);
}

technique10 Mirror {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Mirror()));
	}
}