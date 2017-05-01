struct VS_IN {
	float3 PosL :   POSITION;
	float4 Color :  COLOR;
};

struct PS_IN {
	float4 PosH :   SV_POSITION;
	float4 Color :  COLOR;
};

cbuffer cbPerObject : register(b0) {
	matrix gWorldViewProj;
	bool gOverrideColor;
	float4 gCustomColor;
}

PS_IN vs_main(VS_IN vin) {
	PS_IN vout;
	vout.PosH = mul(float4(vin.PosL, 1.0f), gWorldViewProj);
	vout.Color = gOverrideColor ? gCustomColor : vin.Color;
	return vout;
}

float4 ps_main(PS_IN pin) : SV_Target {	
	return float4(pin.Color.rgb, 1.0);
}

technique10 Main {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_main()));
	}
}