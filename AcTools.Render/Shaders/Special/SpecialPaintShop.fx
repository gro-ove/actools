// textures
	Texture2D gInputMap;
	Texture2D gAoMap;
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

	SamplerState samLinearWrap {
		Filter = MIN_MAG_MIP_LINEAR;
		AddressU = WRAP;
		AddressV = WRAP;
	};

	SamplerState samPointWrap {
		Filter = MIN_MAG_MIP_POINT;
		AddressU = WRAP;
		AddressV = WRAP;
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
		float4 gColors[3];
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

	float4 ps_Pattern(PS_IN pin) : SV_Target {
		float4 pattern = gInputMap.SampleLevel(samLinear, pin.Tex, 0);
		float4 ao = gAoMap.SampleLevel(samLinear, pin.Tex, 0);
		float4 overlay = gOverlayMap.SampleLevel(samLinear, pin.Tex, 0);

		float3 resultColor = pattern.rgb;
		resultColor = resultColor * pattern.a + (float3)(1.0 - pattern.a);
		resultColor *= ao.rgb;

		float4 result = float4(resultColor, pattern.a);
		result.rgb = result.rgb * (1.0 - overlay.a) + overlay.rgb * overlay.a;
		result.a = saturate(result.a + overlay.a);
		return result;
	}

	technique10 Pattern {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_Pattern()));
		}
	}

	float4 ps_ColorfulPattern(PS_IN pin) : SV_Target {
		float4 pattern = gInputMap.SampleLevel(samLinear, pin.Tex, 0);
		float4 ao = gAoMap.SampleLevel(samLinear, pin.Tex, 0);
		float4 overlay = gOverlayMap.SampleLevel(samLinear, pin.Tex, 0);

		float3 resultColor = gColors[0].rgb * pattern.r;
		resultColor += gColors[1].rgb * pattern.g;
		resultColor += gColors[2].rgb * pattern.b;

		float patternA = pow(abs(pattern.a), 0.5);
		resultColor = resultColor * patternA + (float3)(1.0 - patternA);
		resultColor *= ao.rgb;

		float4 result = float4(resultColor, pattern.a);
		result.rgb = result.rgb * (1.0 - overlay.a) + overlay.rgb * overlay.a;
		result.a = saturate(result.a + overlay.a);

		return result;
	}

	technique10 ColorfulPattern {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_ColorfulPattern()));
		}
	}

	float4 ps_Flakes(PS_IN pin) : SV_Target {
		float random = gNoiseMap.SampleLevel(samPointWrap, pin.Tex * gNoiseMultipler, 0).x;
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

	float4 ps_MapsFillGreen(PS_IN pin) : SV_Target {
		float4 base = gInputMap.SampleLevel(samLinear, pin.Tex, 0);
		return saturate(float4(base.r * gColor.r, gColor.g, base.b * gColor.b, 1.0));
	}

	technique10 MapsFillGreen {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_MapsFillGreen()));
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