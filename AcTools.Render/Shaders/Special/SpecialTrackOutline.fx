/* pp area */

Texture2D gInputMap;
Texture2D gBgMap;

SamplerState samLinear {
	Filter = MIN_MAG_MIP_LINEAR;
	AddressU = CLAMP;
	AddressV = CLAMP;
};

SamplerState samPoint {
	Filter = MIN_MAG_MIP_POINT;
	AddressU = CLAMP;
	AddressV = CLAMP;
};

SamplerState samLinearBorder {
	Filter = MIN_MAG_MIP_LINEAR;
	AddressU = BORDER;
	AddressV = BORDER;
	BorderColor = (float4)0.0;
};

cbuffer cbPerFrame {
	float4 gScreenSize;
	float gExtraWidth;
	float gDropShadowRadius;
	// float gDropShadowOpacity;
	// float gCombineOpacity;
	float4 gBlendColor;
	matrix gMatrix;
};

struct VS_IN {
	float3 PosL    : POSITION;
	float2 Tex     : TEXCOORD;
};

struct PS_IN {
	float4 PosH    : SV_POSITION;
	float2 Tex     : TEXCOORD;
};

struct matrix_PS_IN {
	float4 PosH    : SV_POSITION;
	float2 Tex     : TEXCOORD;
	float2 TexM    : TEXCOORD1;
};

PS_IN vs_main(VS_IN vin) {
	PS_IN vout;
	vout.PosH = float4(vin.PosL, 1.0);
	vout.Tex = vin.Tex;
	return vout;
}

PS_IN vs_FirstStep(VS_IN vin) {
	PS_IN vout;
	vout.PosH = float4(vin.PosL, 1.0);
	vout.Tex = mul(float4(vin.Tex, 0.0, 1.0), gMatrix).xy;
	return vout;
}

matrix_PS_IN vs_MatrixMain(VS_IN vin) {
	matrix_PS_IN vout;
	vout.PosH = float4(vin.PosL, 1.0);
	vout.Tex = vin.Tex;
	vout.TexM = mul(float4(vin.Tex, 0.0, 1.0), gMatrix).xy;
	return vout;
}

cbuffer POISSON_DISKS {
	float2 poissonDisk[48] = {
		float2(0.855832f, 0.1898994f),
		float2(0.6336077f, 0.4661179f),
		float2(0.9748058f, 0.0005619526f),
		float2(0.6545785f, 0.0303187f),
		float2(0.8826399f, 0.4462606f),
		float2(0.5820866f, 0.2491302f),
		float2(0.7813198f, -0.1931429f),
		float2(0.4344696f, -0.463068f),
		float2(0.3466403f, -0.2568325f),
		float2(0.7616104f, -0.465308f),
		float2(0.3484367f, -0.6709957f),
		float2(0.1399384f, -0.1253062f),
		float2(0.06888496f, -0.5598317f),
		float2(0.3422709f, -0.007572813f),
		float2(0.1778579f, -0.8323114f),
		float2(0.6398911f, -0.7186768f),
		float2(-0.002360195f, -0.3007f),
		float2(0.3574681f, 0.2342279f),
		float2(0.3560048f, 0.5762256f),
		float2(0.4195986f, 0.8352308f),
		float2(0.05449425f, 0.8639945f),
		float2(0.6891651f, 0.6874872f),
		float2(0.1294608f, 0.6387498f),
		float2(-0.2249427f, -0.9602716f),
		float2(-0.1009111f, -0.7166787f),
		float2(-0.1966588f, -0.4243899f),
		float2(-0.3251926f, -0.7385348f),
		float2(0.1280651f, 0.1115537f),
		float2(-0.4393473f, -0.4411973f),
		float2(-0.3216322f, -0.1805354f),
		float2(-0.6028456f, -0.2775587f),
		float2(-0.2305238f, 0.8397814f),
		float2(-0.06095055f, 0.5215759f),
		float2(-0.7246163f, -0.6027799f),
		float2(0.130333f, 0.4074706f),
		float2(-0.09729999f, -0.02041483f),
		float2(-0.8133149f, -0.04492685f),
		float2(-0.4851737f, 0.03152225f),
		float2(-0.9180767f, -0.2857039f),
		float2(-0.4836228f, 0.6344832f),
		float2(-0.1267498f, 0.2698318f),
		float2(0.006003978f, -0.9894701f),
		float2(-0.3231294f, 0.4236689f),
		float2(-0.7657595f, 0.1987602f),
		float2(-0.5273226f, 0.2657178f),
		float2(-0.5848147f, -0.8110186f),
		float2(-0.8137172f, 0.5681945f),
		float2(0.4109656f, -0.9079512f)
	};
}

float4 ps_FirstStep(PS_IN pin) : SV_Target {
	return gInputMap.SampleLevel(samLinearBorder, pin.Tex, 0.0).a > 0.1 ? (float4)1.0 : (float4)0.0;
}

technique10 FirstStep {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_FirstStep()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_FirstStep()));
	}
}

float4 ps_ExtraWidth(PS_IN pin) : SV_Target {
	float edge = 0;

	for (int i = 0; i < 48; i++) {
		if (gInputMap.SampleLevel(samLinear, pin.Tex + gScreenSize.zw * poissonDisk[i] * gExtraWidth, 0.0).a > 0.1) {
			edge += 1.0;
		}
	}

	return (float4)edge;
}

technique10 ExtraWidth {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_ExtraWidth()));
	}
}

float4 ps_Shadow(PS_IN pin) : SV_Target{
	return gInputMap.SampleLevel(samLinear, pin.Tex - gScreenSize.zw * gDropShadowRadius, 0.0);
}

technique10 Shadow {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Shadow()));
	}
}

float4 ps_Combine(PS_IN pin) : SV_Target{
	float value = gInputMap.SampleLevel(samLinear, pin.Tex, 0.0).r;
	return float4(gBlendColor.rgb, value * gBlendColor.a);
}

technique10 Combine {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Combine()));
	}
}

// proper version
float4 ps_Blend(PS_IN pin) : SV_Target {
	float srcA = gInputMap.SampleLevel(samLinear, pin.Tex, 0.0).r * gBlendColor.a;
	float3 srcRGB = gBlendColor.rgb;
	float4 dst = gBgMap.SampleLevel(samLinear, pin.Tex, 0.0);

	float outA = srcA + dst.a * (1 - srcA);
	float3 outRGB = (srcRGB * srcA + dst.rgb * dst.a * (1 - srcA)) / outA;
	if (outA < 0.00001) {
		outA = 0.0;
		outRGB = 0.0;
	}

	return saturate(float4(outRGB, outA));
}

technique10 Blend {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Blend()));
	}
}

float4 ps_Final(PS_IN pin) : SV_Target {
	// TODO: remove?
	return gInputMap.SampleLevel(samLinear, pin.Tex, 0.0);
}

technique10 Final {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Final()));
	}
}

float4 ps_FinalBg(matrix_PS_IN pin) : SV_Target {
	float4 base = gInputMap.SampleLevel(samLinear, pin.Tex, 0.0);
	float4 bg = gBgMap.SampleLevel(samLinear, pin.TexM, 0.0);
	return bg * (1 - base.a) + float4(base.rgb, 1.0) * base.a;
}

technique10 FinalBg {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_MatrixMain()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_FinalBg()));
	}
}

float4 ps_FinalCheckers(PS_IN pin) : SV_Target {
	float4 base = gInputMap.SampleLevel(samLinear, pin.Tex, 0.0);

	float x = saturate((pin.Tex.x * (gScreenSize.x / 16) % 2 - 1.0) * 1e6);
	float y = saturate((pin.Tex.y * (gScreenSize.y / 16) % 2 - 1.0) * 1e6);
	float background = ((x + y) % 2) * 0.15 + 0.15;

	return background * (1 - base.a) + float4(base.rgb, 1.0) * base.a;
}

technique10 FinalCheckers {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_FinalCheckers()));
	}
}

/* per-object rendering */

struct obj_VS_IN {
	float3 PosL : POSITION;
};

cbuffer cbPerObject : register(b0) {
	matrix gWorldViewProj;
}

struct obj_PS_IN {
	float4 PosH : SV_POSITION;
};

obj_PS_IN vs_FirstStepObj(obj_VS_IN vin) {
	obj_PS_IN vout;
	vout.PosH = mul(float4(vin.PosL, 1.0f), gWorldViewProj);
	return vout;
}

float4 ps_FirstStepObj(obj_PS_IN pin) : SV_Target {
	return (float4)1.0;
}

technique10 FirstStepObj {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_FirstStepObj()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_FirstStepObj()));
	}
}

