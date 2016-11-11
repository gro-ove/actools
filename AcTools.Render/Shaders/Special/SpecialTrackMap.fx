struct VS_IN {
	float3 PosL       : POSITION;
	float3 NormalL    : NORMAL;
	float2 Tex        : TEXCOORD;
	float3 TangentL   : TANGENT;
};

cbuffer cbPerObject : register(b0) {
	matrix gWorldViewProj;
}

struct PS_IN {
	float4 PosH : SV_POSITION;
};

PS_IN vs_main(VS_IN vin) {
	PS_IN vout;
	vout.PosH = mul(float4(vin.PosL, 1.0f), gWorldViewProj);
	return vout;
}

float4 ps_main(PS_IN pin) : SV_Target {
	return float4(1, 1, 1, 1);
}

technique10 Main {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_main()));
	}
}