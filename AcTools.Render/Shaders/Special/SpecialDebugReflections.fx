cbuffer cbPerObject : register(b0) {
	matrix gWorld;
	matrix gWorldInvTranspose;
	matrix gWorldViewProj;
}

cbuffer cbPerFrame {
	float3 gEyePosW;
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
};

TextureCube gReflectionCubemap;

// z-buffer tricks if needed
#define FixPosH(x) (x)

float4 CalculatePosH(float3 posL) {
	return FixPosH(mul(float4(posL, 1.0f), gWorldViewProj));
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

SamplerState samAnisotropic {
	Filter = ANISOTROPIC;
	MaxAnisotropy = 16;

	AddressU = WRAP;
	AddressV = WRAP;
};

float4 ps_main(PS_IN pin) : SV_Target {
	float3 toEyeW = normalize(gEyePosW - pin.PosW);
	float3 reflected = reflect(-toEyeW, pin.NormalW);
	return gReflectionCubemap.SampleLevel(samAnisotropic, reflected, 100);
}

technique10 Main {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_main()));
	}
}