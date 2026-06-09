/* per-object rendering */

struct VS_IN {
	float3 PosL       : POSITION;
};

cbuffer cbPerObject : register(b0) {
	matrix gWorldViewProj;
	float4 gColor;
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
	return gColor;
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
Texture2D gPreprocessedBaseMap;
Texture2D gPreprocessedMarksMap;
Texture2D gBlurredMap;

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
	float4 baseBlurred = gBlurredMap.SampleLevel(samInputImage, pin.Tex, 0.0);

	float edge = 0;
	for (int x = -1; x <= 1; x++) {
		for (int y = -1; y <= 1; y++) {
			if (gInputMap.SampleLevel(samInputImage, pin.Tex + float2(gScreenSize.z * x, gScreenSize.w * y), 0.0).w > 0.1) {
				edge += 1;
			}
		}
    }

	return float4(base.rgb * base.w, max(saturate(edge) * 0.6, max(base.w, baseBlurred.w)));
}

technique10 Pp {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_pp()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_pp()));
	}
}

float4 ps_final(Pp_PS_IN pin) : SV_Target{
	float4 base = gPreprocessedBaseMap.SampleLevel(samInputImage, pin.Tex, 0.0);
	float4 marks = gPreprocessedMarksMap.SampleLevel(samInputImage, pin.Tex, 0.0);
	base = lerp(base, marks, marks.w);
	return float4(base.rgb, base.w);
}

technique10 Final {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_pp()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_final()));
	}
}

float4 ps_final_checkers(Pp_PS_IN pin) : SV_Target{
	float4 base = gPreprocessedBaseMap.SampleLevel(samInputImage, pin.Tex, 0.0);
	float4 marks = gPreprocessedMarksMap.SampleLevel(samInputImage, pin.Tex, 0.0);
	base = lerp(base, marks, marks.w);

	float x = saturate((pin.Tex.x * (gScreenSize.x / 16) % 2 - 1.0) * 1e6);
	float y = saturate((pin.Tex.y * (gScreenSize.y / 16) % 2 - 1.0) * 1e6);
	float background = ((x + y) % 2) * 0.15 + 0.15;

	return background * (1 - base.w) + float4(base.rgb, 1.0) * base.w;
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

	float4 color = 0;
	float totalWeight = 0;

	[unroll]
	for (float i = -gBlurRadius; i <= gBlurRadius; ++i) {
		float weight = gWeights[i + gBlurRadius];
		color += weight * gInputMap.SampleLevel(samInputImage, pin.Tex + i * texOffset, 0.0);
		totalWeight += weight;
	}

	return color / totalWeight;
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