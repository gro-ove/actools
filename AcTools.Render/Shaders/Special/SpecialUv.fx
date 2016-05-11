struct VS_IN {
	float3 PosL       : POSITION;
	float3 NormalL    : NORMAL;
	float2 Tex        : TEXCOORD;
	float3 TangentL   : TANGENT;
};

struct PS_IN {
	float4 PosH : SV_POSITION;
};

cbuffer cbPerFrame {
	float2 gOffset;
};

PS_IN vs_main(VS_IN vin) {
	PS_IN vout;
	float2 tex = vin.Tex + gOffset;
	vout.PosH = float4(tex.x * 2 - 1, -tex.y * 2 - 1, 1, 1);
	return vout;
}

float4 ps_main(PS_IN pin) : SV_Target {
	return float4(0, 1, 0, 1);
}

technique10 Main {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_main()));
	}
}