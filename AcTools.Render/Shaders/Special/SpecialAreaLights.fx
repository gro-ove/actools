struct VS_IN {
	float3 PosL :   POSITION;
	float4 Color :  COLOR;
};

struct PS_IN {
	float4 PosH :   SV_POSITION;
	float4 Color :  COLOR;
};

struct PS_OUT {
	float4 BaseReflection : SV_Target0;
	float4 Normal : SV_Target1;
	float Depth : SV_Target2;
};

PS_OUT PackResult(float4 reflection, float3 normal, float depth, float specularExp) {
	PS_OUT pout;
	pout.BaseReflection = reflection;
	pout.Normal = float4(normal.xyz, specularExp);
	pout.Depth = depth;
	return pout;
}

float GetDepth(float4 posH) {
	return posH.z;
}

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

PS_OUT ps_GPass(PS_IN pin) {
	return PackResult((float4)0.0, float3(0, 1, 0), GetDepth(pin.PosH), 0.0);
}

technique10 GPass {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_GPass()));
	}
}