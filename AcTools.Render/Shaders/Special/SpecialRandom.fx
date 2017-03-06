// fn structs
struct VS_IN {
	float3 PosL    : POSITION;
	float2 Tex     : TEXCOORD;
};

struct PS_IN {
	float4 PosH    : SV_POSITION;
	float2 Tex     : TEXCOORD;
};

PS_IN vs_main(VS_IN vin) {
	PS_IN vout;
	vout.PosH = float4(vin.PosL, 1.0);
	vout.Tex = vin.Tex;
	return vout;
}

float nrand1(float2 uv){
	return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453);
}

float nrand2(float2 uv){
	return frac(sin(dot(uv, float2(32.9898, 28.233))) * 23758.5453);
}

float nrand3(float2 uv){
	return frac(sin(dot(uv, float2(62.9898, 18.233))) * 33758.5453);
}

float nrand4(float2 uv){
	return frac(sin(dot(uv, float2(22.9898, 58.233))) * 13758.5453);
}

float4 ps_main(PS_IN pin) : SV_Target{
	return float4(nrand1(pin.Tex), nrand2(pin.Tex), nrand3(pin.Tex), nrand4(pin.Tex));
}

technique10 Main {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_main()));
	}
}

float4 ps_FlatNormalMap(PS_IN pin) : SV_Target {
	return float4(0.5, 0.5, 1.0, 1.0);
}

technique10 FlatNormalMap {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_FlatNormalMap()));
	}
}