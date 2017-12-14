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
	Texture2D gDetailsMap;

	struct ChannelsParams {
	    float4 Map;
	    float4 Add;
	    float4 Multiply;
	};

	cbuffer cbInputSources : register(b1) {
		ChannelsParams gInputParams;
		ChannelsParams gAoParams;
		ChannelsParams gMaskParams;
		ChannelsParams gOverlayParams;
		ChannelsParams gUnderlayParams;
		ChannelsParams gDetailsParams;
	}

	float4 GetSource(SamplerState sam, Texture2D tex, ChannelsParams p, float2 uv) {
		float4 value = tex.SampleLevel(sam, uv, 0);
		return float4(
			p.Add[0] + (p.Map[0] < 0 ? p.Map[0] + 2.0 : value[p.Map[0]]) * p.Multiply[0],
			p.Add[1] + (p.Map[1] < 0 ? p.Map[1] + 2.0 : value[p.Map[1]]) * p.Multiply[1],
			p.Add[2] + (p.Map[2] < 0 ? p.Map[2] + 2.0 : value[p.Map[2]]) * p.Multiply[2],
			p.Add[3] + (p.Map[3] < 0 ? p.Map[3] + 2.0 : value[p.Map[3]]) * p.Multiply[3]);
	}

	float4 GetSource(Texture2D tex, ChannelsParams p, float2 uv) {
		return GetSource(samLinear, tex, p, uv);
	}

	float4 GetInputMap(float2 uv) { return GetSource(gInputMap, gInputParams, uv); }
	float4 GetAoMap(float2 uv) { return GetSource(gAoMap, gAoParams, uv); }
	float4 GetMaskMap(float2 uv) { return GetSource(gMaskMap, gMaskParams, uv); }
	float4 GetOverlayMap(float2 uv) { return GetSource(gOverlayMap, gOverlayParams, uv); }
	float4 GetUnderlayMap(float2 uv) { return GetSource(gUnderlayMap, gUnderlayParams, uv); }
	float4 GetDetailsMap(float2 uv) { return GetSource(gDetailsMap, gDetailsParams, uv); }

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
		float2 gAlphaAdjustments;
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
        BorderColor = float4(1.0, 1.0, 1.0, 0.0);
	};

	PS_IN vs_Piece(VS_IN vin) {
		PS_IN vout;
		vout.PosH = float4(vin.PosL, 1.0);
	    vout.Tex = mul(float4(vin.Tex, 1.0, 1.0), gTransform).xy;
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

// flakes
	#define _FLAKES_SPLIT 0.57
	#define _FLAKES_SPLIT_LEFT (1 - _FLAKES_SPLIT)

	float4 ps_Flakes(PS_IN pin) : SV_Target {
		float4 random4 = gNoiseMap.SampleLevel(samPointWrap, pin.Tex * gNoiseMultipler, 0);
		float random = Luminance(random4.rgb);
		random = 1.0 - saturate(random * 3);
		random = random < _FLAKES_SPLIT ? pow(random / _FLAKES_SPLIT, 1.2) : 1.0;
		return float4(gColor.rgb, saturate(gColor.a - gFlakes + saturate(random + (random4.a - 0.5) * 0.02) * gFlakes));
	}

	technique10 Flakes {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_Flakes()));
		}
	}

// pattern
	float4 ps_Pattern(PS_IN pin) : SV_Target {
		float4 pattern = GetInputMap(pin.Tex);
		float4 ao = GetAoMap(pin.Tex);
		float4 overlay = GetOverlayMap(pin.Tex);
		float4 details = gDetailsMap.SampleLevel(samLinear, pin.Tex, 0);

		float3 resultColor = pattern.rgb;
		resultColor = resultColor * pattern.a + (float3)(1.0 - pattern.a);
		resultColor *= ao.rgb;

		float4 result = float4(resultColor, pattern.a);
		result.rgb = result.rgb * (1.0 - overlay.a) + overlay.rgb * overlay.a;
		result.rgb = result.rgb * (1.0 - details.a) + details.rgb * ao.rgb * details.a;
		result.a = saturate(result.a + details.a + overlay.a);

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
		float4 details = gDetailsMap.SampleLevel(samLinear, pin.Tex, 0);

		float3 resultColor = gColors[0].rgb * saturate(pattern.r * 100);
		resultColor += gColors[1].rgb * saturate(pattern.g * 100);
		resultColor += gColors[2].rgb * saturate(pattern.b * 100);

		float patternA = pow(abs(pattern.a), 0.5);
		resultColor = resultColor * patternA + gColor.rgb * (float3)(1.0 - patternA);
		resultColor *= ao.rgb;

		float4 result = float4(resultColor, pattern.a);
		result.rgb = result.rgb * (1.0 - overlay.a) + overlay.rgb * overlay.a;
		result.rgb = result.rgb * (1.0 - details.a) + details.rgb * ao.rgb * details.a;
		result.a = saturate(result.a + details.a + overlay.a);

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

// mask
	float4 ps_Mask(PS_IN pin) : SV_Target {
	    // return gUnderlayMap.SampleLevel(samLinear, pin.Tex, 0);

	    float4 overlay = GetInputMap(pin.Tex);
	    if (!gUseMask) return overlay;

	    float mask = GetMaskMap(pin.Tex).r;
	    if (mask >= 1.0) return overlay;

	    float4 underlay = gUnderlayMap.SampleLevel(samLinear, pin.Tex, 0);
	    if (mask <= 0.0) return underlay;

	    return lerp(
	        gUnderlayMap.SampleLevel(samLinear, pin.Tex, 0),
	        GetInputMap(pin.Tex),
	        GetMaskMap(pin.Tex).r);
	}

	technique10 Mask {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_Mask()));
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
		float4 modified = saturate(float4(
		    gColors[0].r + base.r * gColors[1].r,
		    gColors[0].g + base.g * gColors[1].g,
		    gColors[0].b + base.b * gColors[1].b,
		    1.0));
		return gUseMask ? lerp(base, modified, GetMaskMap(pin.Tex).r) : modified;
	}

	technique10 Maps {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_Maps()));
		}
	}

// tinting
	float4 ps_Tint(PS_IN pin) : SV_Target {
		float4 base = GetInputMap(pin.Tex);
        float alpha = saturate(gAlphaAdjustments.x + base.a * gAlphaAdjustments.y);
		float4 result = float4(base.rgb * gColor.rgb, alpha);
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

        float alpha = saturate(gAlphaAdjustments.x + base.a * gAlphaAdjustments.y);
		float4 result = base * ((float4)(1.0 - mask.r) + gColor * mask.r)
			* ((float4)(1.0 - mask.g) + gColors[0] * mask.g)
			* ((float4)(1.0 - mask.b) + gColors[1] * mask.b)
			* ((float4)(1.0 - mask.a) + gColors[2] * mask.a);
		return SimpleBlending(float4(result.rgb, alpha), GetOverlayMap(pin.Tex));
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
    #define _FLOAT_MAX 3.402823466e+38f

	float4 ps_FindLimitsFirstStep(PS_IN pin) : SV_Target {
		float4 result = float4(0, _FLOAT_MAX, 0, 0);

		[unroll]
		for (float x = -0.375; x <= 0.376; x += 0.25) {
			[unroll]
			for (float y = -0.375; y <= 0.376; y += 0.25) {
			    float4 value = gInputMap.SampleLevel(samPoint, pin.Tex + gSize.zw * float2(x, y), 0);
				result.x = max(result.x, max(value.r, max(value.g, value.b)));
				result.y = min(result.y, min(value.r, min(value.g, value.b)));
			}
		}

		return result;
	}

	technique10 FindLimitsFirstStep {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_FindLimitsFirstStep()));
		}
	}

	float4 ps_FindLimits(PS_IN pin) : SV_Target {
		float4 result = float4(0, _FLOAT_MAX, 0, 0);

		[unroll]
		for (float x = -0.375; x <= 0.376; x += 0.25) {
			[unroll]
			for (float y = -0.375; y <= 0.376; y += 0.25) {
			    float4 value = gInputMap.SampleLevel(samPoint, pin.Tex + gSize.zw * float2(x, y), 0);
				result.x = max(result.x, value.x);
				result.y = min(result.y, value.y);
			}
		}

		return result;
	}

	technique10 FindLimits {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_FindLimits()));
		}
	}

	float4 ps_NormalizeLimits(PS_IN pin) : SV_Target {
		float4 limits = gOverlayMap.SampleLevel(samPoint, (float2)0.5, 0);
		float4 input = gInputMap.SampleLevel(samLinear, pin.Tex, 0);

		float3 color = (input.rgb - (float3)limits.y) / max(limits.x - limits.y, 0.0001);
		return saturate(float4(color, input.a));
	}

	technique10 NormalizeLimits {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_NormalizeLimits()));
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