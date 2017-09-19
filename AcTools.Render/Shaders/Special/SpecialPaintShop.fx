// textures
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

// input sources
	Texture2D gInputMap;
	Texture2D gAoMap;
	Texture2D gMaskMap;
	Texture2D gOverlayMap;
	Texture2D gUnderlayMap;

	cbuffer cbInputSources : register(b1) {
		float4 gInputMapChannels;
		float4 gAoMapChannels;
		float4 gMaskMapChannels;
		float4 gOverlayMapChannels;
		float4 gUnderlayMapChannels;
	}

	float4 GetSource(SamplerState sam, Texture2D tex, float4 channels, float2 uv) {
		float4 value = tex.SampleLevel(sam, uv, 0);
		return float4(
			channels[0] < 0 ? channels[0] + 2.0 : value[channels[0]],
			channels[1] < 0 ? channels[1] + 2.0 : value[channels[1]],
			channels[2] < 0 ? channels[2] + 2.0 : value[channels[2]],
			channels[3] < 0 ? channels[3] + 2.0 : value[channels[3]]
		);
	}

	float4 GetSource(Texture2D tex, float4 channels, float2 uv) {
		return GetSource(samLinear, tex, channels, uv);
	}

	float4 GetInputMap(float2 uv) { return GetSource(samLinear, gInputMap, gInputMapChannels, uv); }
	float4 GetAoMap(float2 uv) { return GetSource(samLinear, gAoMap, gAoMapChannels, uv); }
	float4 GetMaskMap(float2 uv) { return GetSource(samLinear, gMaskMap, gMaskMapChannels, uv); }
	float4 GetOverlayMap(float2 uv) { return GetSource(samLinear, gOverlayMap, gOverlayMapChannels, uv); }
	float4 GetUnderlayMap(float2 uv) { return GetSource(samLinear, gUnderlayMap, gUnderlayMapChannels, uv); }

// common functions
	float Luminance(float3 color) {
		return saturate(dot(color, float3(0.299f, 0.587f, 0.114f)));
	}

	float4 ProperBlending(float4 background, float4 foreground) {
		float a = foreground.a + background.a * (1 - foreground.a);
		if (a < 0.00001) return background;
		return saturate(float4(
			(foreground.rgb * foreground.a + background.rgb * background.a * (1 - foreground.a)) / a,
			a));
	}

	float4 SimpleBlending(float4 background, float4 foreground) {
		return float4(foreground.rgb * foreground.a + background.rgb * (1 - foreground.a), foreground.a + background.a * (1 - foreground.a));
	}

// input resources
	cbuffer cbPerObject : register(b0) {
		float4 gColor;
		float4 gSize;
		float gNoiseMultipler;
		float gFlakes;
		float4 gColors[3];
		bool gUseMask;
	    matrix gTransform;
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

// draw piece in position
	SamplerState samPiece {
		Filter = MIN_MAG_MIP_LINEAR;
	    AddressU = BORDER;
        AddressV = BORDER;
        AddressW = BORDER;
        BorderColor = float4(1.0f, 1.0f, 1.0f, 0.0f);
	};

	PS_IN vs_Piece(VS_IN vin) {
		PS_IN vout;
		vout.PosH = float4(vin.PosL, 1.0);
		vout.Tex = vin.Tex;
		return vout;
	}

	float4 ps_Piece(PS_IN pin) : SV_Target {
		return gInputMap.SampleLevel(samPiece, pin.Tex, 0);
	}

	technique10 Piece {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_Piece()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_Piece()));
		}
	}

// one vertex shader for everything
	PS_IN vs_main(VS_IN vin) {
		PS_IN vout;
		vout.PosH = float4(vin.PosL, 1.0);
		vout.Tex = vin.Tex;
		return vout;
	}

// solid color
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

// pattern
	float4 ps_Pattern(PS_IN pin) : SV_Target {
		float4 pattern = GetInputMap(pin.Tex);
		float4 ao = GetAoMap(pin.Tex);
		float4 overlay = GetOverlayMap(pin.Tex);

		float3 resultColor = pattern.rgb;
		resultColor = resultColor * pattern.a + (float3)(1.0 - pattern.a);
		resultColor *= ao.rgb;

		float4 result = float4(resultColor, pattern.a);
		result.rgb = result.rgb * (1.0 - overlay.a) + overlay.rgb * overlay.a;
		result.a = saturate(result.a + overlay.a);

        float4 underlay = GetUnderlayMap(pin.Tex);
        result.rgb = underlay.rgb * (1.0 - result.a) * underlay.a + result.rgb * saturate(result.a + (1.0 - underlay.a));
		result.a = saturate(underlay.a + result.a);
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
		float4 pattern = GetInputMap(pin.Tex);
		float4 ao = GetAoMap(pin.Tex);
		float4 overlay = GetOverlayMap(pin.Tex);

		float3 resultColor = gColors[0].rgb * pattern.r;
		resultColor += gColors[1].rgb * pattern.g;
		resultColor += gColors[2].rgb * pattern.b;

		float patternA = pow(abs(pattern.a), 0.5);
		resultColor = resultColor * patternA + (float3)(1.0 - patternA);
		resultColor *= ao.rgb;

		float4 result = float4(resultColor, pattern.a);
		result.rgb = result.rgb * (1.0 - overlay.a) + overlay.rgb * overlay.a;
		result.a = saturate(result.a + overlay.a);

        float4 underlay = GetUnderlayMap(pin.Tex);
        result.rgb = underlay.rgb * (1.0 - result.a) * underlay.a + result.rgb * saturate(result.a + (1.0 - underlay.a));
		result.a = saturate(underlay.a + result.a);
		return result;
	}

	technique10 ColorfulPattern {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_ColorfulPattern()));
		}
	}

// flakes
	#define _FLAKES_SPLIT 0.57
	#define _FLAKES_SPLIT_LEFT (1 - _FLAKES_SPLIT)

	float4 ps_Flakes(PS_IN pin) : SV_Target {
		float4 random4 = gNoiseMap.SampleLevel(samPointWrap, pin.Tex * gNoiseMultipler, 0);
		float random = Luminance(random4.rgb);
		random = 1.0 - saturate(random * 3);
		random = random < _FLAKES_SPLIT ? pow(random / _FLAKES_SPLIT, 1.2) : 1.0;
		return float4(gColor.rgb, saturate(1.0 - gFlakes + saturate(random + (random4.a - 0.5) * 0.02) * gFlakes));
	}

	technique10 Flakes {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_Flakes()));
		}
	}

// replacement
	float4 ps_Replacement(PS_IN pin) : SV_Target {
		return GetInputMap(pin.Tex);
	}

	technique10 Replacement {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_Replacement()));
		}
	}

// maps
	float4 ps_Maps(PS_IN pin) : SV_Target {
		float4 base = GetInputMap(pin.Tex);
		float4 modified = saturate(float4(base.r * gColor.r, base.g * gColor.g, base.b * gColor.b, 1.0));
		if (gUseMask) {
			float mask = GetMaskMap(pin.Tex).r;
			return base * (1.0 - mask) + modified * mask;
		}
		return modified;
	}

	technique10 Maps {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_Maps()));
		}
	}

	float4 ps_MapsFillGreen(PS_IN pin) : SV_Target {
		float4 base = GetInputMap(pin.Tex);
		return saturate(float4(base.r * gColor.r, gColor.g, base.b * gColor.b, 1.0));
	}

	technique10 MapsFillGreen {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_MapsFillGreen()));
		}
	}

// tinting
	float4 ps_Tint(PS_IN pin) : SV_Target {
		float4 base = GetInputMap(pin.Tex);
		float4 result = float4(base.rgb * gColor.rgb, saturate(base.a + gColor.a));
		return SimpleBlending(result, GetOverlayMap(pin.Tex));
	}

	technique10 Tint {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_Tint()));
		}
	}

	float4 ps_TintMask(PS_IN pin) : SV_Target {
		float4 base = GetInputMap(pin.Tex);
		float4 mask = GetMaskMap(pin.Tex);

		float4 result = base * ((float4)(1.0 - mask.r) + gColor * mask.r)
			* ((float4)(1.0 - mask.g) + gColors[0] * mask.g)
			* ((float4)(1.0 - mask.b) + gColors[1] * mask.b)
			* ((float4)(1.0 - mask.a) + gColors[2] * mask.a);
		return SimpleBlending(float4(result.rgb, base.a + gColor.a), GetOverlayMap(pin.Tex));
	}

	technique10 TintMask {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_TintMask()));
		}
	}

// combine channels
	float4 ps_CombineChannels(PS_IN pin) : SV_Target {
		// alphabet order
		float4 red = GetAoMap(pin.Tex);
		float4 green = GetInputMap(pin.Tex);
		float4 blue = GetMaskMap(pin.Tex);
		float4 alpha = GetOverlayMap(pin.Tex);
		return float4(red.r, green.g, blue.b, alpha.a);
	}

	technique10 CombineChannels {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_CombineChannels()));
		}
	}

// preparation
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
		float4 maxValue = gOverlayMap.SampleLevel(samPoint, (float2)0.5, 0);
		float4 input = gInputMap.SampleLevel(samLinear, pin.Tex, 0);
		return saturate(float4(input.rgb / max(max(maxValue.r, max(maxValue.b, maxValue.g)), 0.0001), input.a));
	}

	technique10 MaximumApply {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_MaximumApply()));
		}
	}

	float4 ps_Desaturate(PS_IN pin) : SV_Target {
		float4 input = gInputMap.SampleLevel(samLinear, pin.Tex, 0);
		return float4((float3)Luminance(input.rgb), input.a);
	}

	technique10 Desaturate {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_Desaturate()));
		}
	}