/* per-object rendering */

struct VS_IN {
	float3 PosL       : POSITION;
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

/* pp area */

Texture2D gInputMap;

SamplerState samInputImage {
	Filter = MIN_MAG_LINEAR_MIP_POINT;
	AddressU = CLAMP;
	AddressV = CLAMP;
};

cbuffer cbPerFrame {
	float4 gScreenSize;
};

struct Pp_VS_IN {
	float3 PosL    : POSITION;
	float2 Tex     : TEXCOORD;
};

struct Pp_PS_IN {
	float4 PosH    : SV_POSITION;
	float2 Tex     : TEXCOORD;
};

Pp_PS_IN vs_pp(Pp_VS_IN vin) {
	Pp_PS_IN vout;
	vout.PosH = float4(vin.PosL, 1.0);
	vout.Tex = vin.Tex;
	return vout;
}

float4 ps_pp(Pp_PS_IN pin) : SV_Target{
	float4 base = gInputMap.SampleLevel(samInputImage, pin.Tex, 0.0);
	float orig = base.g * (base.r * 0.7 + 0.3);

	float edge = 0;
	for (int x = -1; x <= 1; x++)
		for (int y = -1; y <= 1; y++) {
			if (gInputMap.SampleLevel(samInputImage, pin.Tex + float2(gScreenSize.z * x, gScreenSize.w * y), 0.0).g > 0.1) {
				edge += 1.0;
			}
		}

	return float4(orig, saturate(edge), orig, 1.0);
}

technique10 Pp {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_pp()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_pp()));
	}
}

float4 ps_final(Pp_PS_IN pin) : SV_Target{
	float4 base = gInputMap.SampleLevel(samInputImage, pin.Tex, 0.0);
	return float4(base.r, base.r, base.r, base.g);
}

technique10 Final {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_pp()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_final()));
	}
}

float4 ps_final_checkers(Pp_PS_IN pin) : SV_Target{
	float4 base = gInputMap.SampleLevel(samInputImage, pin.Tex, 0.0);

	float x = saturate((pin.Tex.x * (gScreenSize.x / 16) % 2 - 1.0) * 1e6);
	float y = saturate((pin.Tex.y * (gScreenSize.y / 16) % 2 - 1.0) * 1e6);
	float background = ((x + y) % 2) * 0.15 + 0.15;

	return background * (1 - base.g) + float4(base.r, base.r, base.r, 1.0) * base.g;
}

technique10 FinalCheckers {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_pp()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_final_checkers()));
	}
}

/* alternative blur */
cbuffer cbSettings {
	float gWeights[7] = {
		0.1f, 0.1f, 0.1f, 0.2f, 0.1f, 0.1f, 0.1f
	};
};

cbuffer cbFixed {
	static const int gBlurRadius = 3;
};

float4 ps_blur(Pp_PS_IN pin, uniform bool gHorizontalBlur) : SV_Target{
	float2 texOffset;
	if (gHorizontalBlur) {
		texOffset = float2(gScreenSize.z, 0.0f);
	} else {
		texOffset = float2(0.0f, gScreenSize.w);
	}

	float4 base = gInputMap.SampleLevel(samInputImage, pin.Tex, 0.0);

	float color = 0;
	float totalWeight = 0;

	[flatten]
	for (float i = -gBlurRadius; i <= gBlurRadius; ++i) {
		float weight = gWeights[i + gBlurRadius];
		color += weight * gInputMap.SampleLevel(samInputImage, pin.Tex + i * texOffset, 0.0).r;
		totalWeight += weight;
	}

	base.r = color / totalWeight;
	return base;
}

technique11 PpHorizontalBlur {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_pp()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_blur(true)));
	}
}

technique11 PpVerticalBlur {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_pp()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_blur(false)));
	}
}