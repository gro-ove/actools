// textures
Texture2D gInputMap;

SamplerState samPoint {
	Filter = MIN_MAG_MIP_POINT;
	AddressU = CLAMP;
	AddressV = CLAMP;
};

SamplerState samLinear {
	Filter = MIN_MAG_MIP_LINEAR;
	AddressU = CLAMP;
	AddressV = CLAMP;
};
	
// input resources
cbuffer cbPerObject : register(b0) {
	float4 gScreenSize;
	float2 gMultipler; // less than zero
}

// fn structs
struct VS_IN {
	float3 PosL    : POSITION;
	float2 Tex     : TEXCOORD;
};

struct PS_IN {
	float4 PosH    : SV_POSITION;
	float2 Tex     : TEXCOORD;
};

// one vertex shader for everything
PS_IN vs_main(VS_IN vin) {
	PS_IN vout;
	vout.PosH = float4(vin.PosL, 1.0);
	vout.Tex = vin.Tex;
	return vout;
}

// just copy to the output buffer
float4 ps_Copy(PS_IN pin) : SV_Target{
	return gInputMap.Sample(samPoint, pin.Tex);
}

technique10 Copy {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Copy()));
	}
}

// find average around — stupid, but works
float4 ps_Average(PS_IN pin) : SV_Target{
	float4 result = 0;
	float v = 0;

	float x, y;
	for (x = -1; x <= 1; x += 0.25) {
		for (y = -1; y <= 1; y += 0.25) {
			float2 uv = pin.Tex + float2(x, y) * gScreenSize.zw * 0.5;
			float w = sqrt(pow(1.5 - abs(x), 2) + pow(1.5 - abs(y), 2));
			float4 n = gInputMap.SampleLevel(samPoint, uv, 0);
			result += n * w;
			v += w;
		}
	}

	return result / v;
}

technique10 Average {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Average()));
	}
}

// anisotropic thing
SamplerState samAnisotropic {
	Filter = ANISOTROPIC;
	MaxAnisotropy = 16;

	AddressU = WRAP;
	AddressV = WRAP;
};

float4 ps_Anisotropic(PS_IN pin) : SV_Target {
	return gInputMap.Sample(samAnisotropic, pin.Tex);
}

technique10 Anisotropic {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Anisotropic()));
	}
}