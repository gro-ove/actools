// structs
static const dword HAS_NORMAL_MAP = 1;
static const dword NM_OBJECT_SPACE = 2;
static const dword HAS_DETAILS_MAP = 4; // maps
static const dword DEBUG_USE_REFL_AS_COLOR = 8;
static const dword IS_ADDITIVE = 16; // reflective
static const dword IS_CARPAINT = 32; // maps
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

struct ReflectiveMaterial {
	float FresnelC;
	float FresnelExp;
	float FresnelMaxLevel;
};

// maps
struct MapsMaterial {
	float DetailsUvMultiplier;
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
	float DiffuseMultiplier;
	float NormalMultiplier;
};

// tyres
struct TyresMaterial {
	float BlurLevel;
	float DirtyLevel;
};

Texture2D gDiffuseBlurMap;
Texture2D gNormalBlurMap;
Texture2D gDirtyMap;

// input resources
cbuffer cbPerObject : register(b0) {
	matrix gWorld;
	matrix gWorldInvTranspose;
	matrix gWorldViewProj;
	matrix gViewProj;

	StandartMaterial gMaterial;
	ReflectiveMaterial gReflectiveMaterial;
	MapsMaterial gMapsMaterial;
	AlphaMaterial gAlphaMaterial;
	NmUvMultMaterial gNmUvMultMaterial;
	TyresMaterial gTyresMaterial;
}

#define HAS_FLAG(x) ((gMaterial.Flags & x) == x)
#define GET_FLAG(x) ((gMaterial.Flags & x) / x)

cbuffer cbPerFrame {
	float3 gEyePosW;

	float3 gAmbientDown;
	float3 gAmbientRange;
	float3 gBackgroundColor;

	float gReflectionPower;
	bool gUseAo;
	bool gCubemapReflections;
	float gCubemapReflectionsOffset;
	float gCubemapAmbient;
	float gAmbientShadowOpacity;

	int gFlatMirrorSide;
}

#define gFlatMirrored (gFlatMirrorSide == -1)

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
	if (gUseAo) {
		return gAoMap.SampleLevel(samLinear, screenCoords / gScreenSize.xy, 0).r;
	} else {
		return 1.0;
	}
}

SamplerState samAnisotropic {
	Filter = ANISOTROPIC;
	MaxAnisotropy = 16;

	AddressU = WRAP;
	AddressV = WRAP;
};

float3 NormalSampleToWorldSpace(float3 normalMapSample, float3 unitNormalW, float3 tangentW) {
	float3 n = float3(
		2.0 * normalMapSample.x - 1.0,
		1.0 - 2.0 * normalMapSample.y,
		2.0 * normalMapSample.z - 1.0);
	float3x3 m;
    if (HAS_FLAG(NM_OBJECT_SPACE)){
        n = float3(n.x, n.z, n.y);
        m = (float3x3)gWorldInvTranspose;
    } else {
	    float3 t = normalize(tangentW);
        float3 N = normalize(unitNormalW);
        float3 T = normalize(t - dot(t, N)*N);
        float3 B = normalize(cross(N, T));
        m = float3x3(T, B, N);
	}

    return mul(n, m);
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

void FlatMirrorTest(pt_PS_IN pin){
    if (gFlatMirrorSide != 0){
        clip(pin.PosW.y * gFlatMirrorSide);
    }
}

void FlatMirrorTest(PS_IN pin){
    if (gFlatMirrorSide != 0){
        clip(pin.PosW.y * gFlatMirrorSide);
    }
}

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

void AlphaTest(float alpha) {
	if (HAS_FLAG(ALPHA_TEST)) clip(alpha - 0.5);
}

float Luminance(float3 color) {
	return saturate(dot(color, float3(0.299f, 0.587f, 0.114f)));
}