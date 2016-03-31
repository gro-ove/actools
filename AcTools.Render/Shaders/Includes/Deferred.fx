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
};

struct PS_OUT {
	float4 Base   : SV_Target0;
	float3 Normal : SV_Target1;
	float4 Maps   : SV_Target2;
};

SamplerState samAnisotropic {
	Filter = ANISOTROPIC;
	MaxAnisotropy = 4;

	AddressU = WRAP;
	AddressV = WRAP;
};

