// structs
static const dword HAS_NORMAL_MAP = 1;
static const dword HAS_DETAILS_MAP = 2;
static const dword HAS_DETAILS_NORMAL_MAP = 4;
static const dword HAS_MAPS = 8;
static const dword USE_DIFFUSE_ALPHA_AS_MAP = 16;
static const dword ALPHA_BLEND = 32;
static const dword IS_ADDITIVE = 64;

struct NmMaterial {
	float Ambient;
	float Diffuse;
	float Specular;
	float SpecularExp;

	float FresnelC;
	float FresnelExp;
	float FresnelMaxLevel;
	float DetailsUvMultipler;

	float3 Emissive;
	float DetailsNormalBlend;

	dword Flags;
	float3 _padding;
};

struct StandartMaterial {
	float Ambient;
	float Diffuse;
	float Specular;
	float SpecularExp;

	float FresnelC;
	float FresnelExp;
	float FresnelMaxLevel;
	float DetailsUvMultipler;

	float3 Emissive;
	float DetailsNormalBlend;

	dword Flags;
	float3 _padding;
};

// textures
Texture2D gDiffuseMap;
Texture2D gNormalMap;
Texture2D gMapsMap;
Texture2D gDetailsMap;
Texture2D gDetailsNormalMap;
TextureCube gReflectionCubemap;

// input resources
cbuffer cbPerObject : register(b0) {
	matrix gWorld;
	matrix gWorldInvTranspose;
	matrix gWorldViewProj;
	StandartMaterial gStandartMaterial;
}

cbuffer cbPerFrame {
	float3 gEyePosW;
}

#define gAmbientDown float3(0.7, 0.7, 0.7)
#define gAmbientRange float3(0.5, 0.5, 0.5)

SamplerState samAnisotropic {
	Filter = ANISOTROPIC;
	MaxAnisotropy = 4;

	AddressU = WRAP;
	AddressV = WRAP;
};

float3 CalcAmbient(float3 normal) {
	float up = normal.y * 0.5 + 0.5;
	return gAmbientDown + up * gAmbientRange;
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
	return saturate((0.3 - abs(0.6 - reflected.y + reflected.x * 0.1)) * specularExp / 4.0) * 2.7;
}

#define gMaterial gStandartMaterial

float4 ps_Standard(PS_IN pin) : SV_Target {
	float4 diffuseValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex);
	float ambient = gMaterial.Ambient + gMaterial.Diffuse / 2;

	float alpha = diffuseValue.a;

	float3 normal;
	if ((gMaterial.Flags & HAS_NORMAL_MAP) == HAS_NORMAL_MAP) {
		float4 normalValue = gNormalMap.Sample(samAnisotropic, pin.Tex);
		normal = normalize(NormalSampleToWorldSpace(normalValue.xyz, pin.NormalW, pin.TangentW, pin.BitangentW));
		alpha *= normalValue.a;
	} else {
		normal = normalize(pin.NormalW);
	}

	float3 lighted = CalcAmbient(normal) * diffuseValue.rgb * ambient + diffuseValue.rgb * gMaterial.Emissive;

	float3 toEyeW = normalize(gEyePosW - pin.PosW);
	float3 reflected = reflect(-toEyeW, normal);
	float refl = PseudoReflection(reflected, gMaterial.SpecularExp);

	float rid = saturate(dot(toEyeW, normal));
	float rim = pow(1 - rid, gMaterial.FresnelExp);
	float val = gMaterial.FresnelC + rim * (gMaterial.FresnelMaxLevel - gMaterial.FresnelC);

	lighted += -val * 0.32 + refl * val;

	return float4(lighted, alpha);
}

technique10 Standard {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Standard()));
	}
}

float4 ps_AmbientShadow(pt_PS_IN pin) : SV_Target {
	float value = gDiffuseMap.Sample(samAnisotropic, pin.Tex).r;
	return float4(0.0, 0.0, 0.0, value);
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
	return float4(CalcAmbient(reflected), 1.0);
}

technique10 Mirror {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Mirror()));
	}
}