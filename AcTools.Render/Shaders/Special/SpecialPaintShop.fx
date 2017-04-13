// textures
	Texture2D gInputMap;
	Texture2D gOverlayMap;
	Texture2D gNoiseMap;

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

	SamplerState samRandom {
		Filter = MIN_MAG_MIP_POINT;
		AddressU = Wrap;
		AddressV = Wrap;
	};

// common functions
	float Luminance(float3 color) {
		return saturate(dot(color, float3(0.299f, 0.587f, 0.114f)));
	}
	
// input resources
	cbuffer cbPerObject : register(b0) {
		float4 gColor;
		float4 gSize;
		float gNoiseMultipler;
		float gFlakes;
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

	float4 ps_Fill(PS_IN pin) : SV_Target {
		return gColor;
	}

	technique10 Fill {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_Fill()));
		}
	}

	float4 ps_Flakes(PS_IN pin) : SV_Target {
		float random = gNoiseMap.SampleLevel(samRandom, pin.Tex * gNoiseMultipler, 0).x;
		return float4(gColor.rgb, saturate(1.0 - random * gFlakes));
	}

	technique10 Flakes {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_Flakes()));
		}
	}

	float4 ps_Maps(PS_IN pin) : SV_Target {
		float4 base = gInputMap.SampleLevel(samLinear, pin.Tex, 0);
		return saturate(float4(base.r * gColor.r, base.g * gColor.g, base.b * gColor.b, 1.0));
	}

	technique10 Maps {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_Maps()));
		}
	}

	float4 ps_Maximum(PS_IN pin) : SV_Target {
		float4 result = 0;
		[unroll]
		for (float x = -0.375; x <= 0.376; x += 0.25) {
			[unroll]
			for (float y = -0.375; y <= 0.376; y += 0.25) {
				result = max(result, gInputMap.SampleLevel(samPoint, pin.Tex + gSize.zw * float2(x, y), 0));
			}
		}

		return result;
	}

	technique10 Maximum {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_Maximum()));
		}
	}

	float4 ps_MaximumApply(PS_IN pin) : SV_Target {
		return gInputMap.SampleLevel(samLinear, pin.Tex, 0) / gOverlayMap.SampleLevel(samPoint, (float2)0.5, 0);
	}

	technique10 MaximumApply {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_MaximumApply()));
		}
	}

	float4 ps_Tint(PS_IN pin) : SV_Target {
		float4 base = gInputMap.SampleLevel(samLinear, pin.Tex, 0);
		return saturate(float4(base.r * gColor.r, base.g * gColor.g, base.b * gColor.b, base.a + gColor.a));
	}

	technique10 Tint {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_Tint()));
		}
	}