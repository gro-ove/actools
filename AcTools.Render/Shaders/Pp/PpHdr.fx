#include "Common.fx"

	cbuffer cbPerFrame : register(b0) {
		float2 gPixel;
		float2 gCropImage;

		float4 gParams;

		float4 gScreenSize;
		bool gUseDither;
	}

// downsampling
	float4 ps_Downsampling (PS_IN pin) : SV_Target {
		float2 uv = pin.Tex * gCropImage + 0.5 - gCropImage / 2;
		float2 delta = gPixel * uv;

		float4 color = tex(uv);
		color += tex(uv + float2(-delta.x, 0));
		color += tex(uv + float2(delta.x, 0));
		color += tex(uv + float2(0, -delta.y));
		color += tex(uv + float2(0, delta.y));
		return saturate(color / 5);
	}

	technique10 Downsampling {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_Downsampling()));
		}
	}

// downsampling
	Texture2D gBrightnessMap;

	float4 ps_Adaptation (PS_IN pin) : SV_Target {
		return (tex(0.5) * 49 + tex(gBrightnessMap, 0.5)) / 50;
	}

	technique10 Adaptation {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_Adaptation()));
		}
	}

// tonemap
	static const float3 LUM_CONVERT = float3(0.299f, 0.587f, 0.114f);

	Texture2D gBloomMap;

	float3 ToneReinhard(float3 vColor, float average, float exposure, float whitePoint){
		// RGB -> XYZ conversion
		const float3x3 RGB2XYZ = {  0.5141364, 0.3238786,  0.16036376,
									0.265068,  0.67023428, 0.06409157,
									0.0241188, 0.1228178,  0.84442666  };
		float3 XYZ = mul(RGB2XYZ, vColor.rgb);

		// XYZ -> Yxy conversion
		float3 Yxy;
		Yxy.r = XYZ.g;                            // copy luminance Y
		Yxy.g = XYZ.r / (XYZ.r + XYZ.g + XYZ.b); // x = X / (X + Y + Z)
		Yxy.b = XYZ.g / (XYZ.r + XYZ.g + XYZ.b); // y = Y / (X + Y + Z)

		// (Lp) Map average luminance to the middlegrey zone by scaling pixel luminance
		float Lp = Yxy.r * exposure / average;

		// (Ld) Scale all luminance within a displayable range of 0 to 1
		Yxy.r = (Lp * (1.0f + Lp / (whitePoint * whitePoint)))/(1.0f + Lp);

		// Yxy -> XYZ conversion
		XYZ.r = Yxy.r * Yxy.g / Yxy. b;               // X = Y * x / y
		XYZ.g = Yxy.r;                                // copy luminance Y
		XYZ.b = Yxy.r * (1 - Yxy.g - Yxy.b) / Yxy.b;  // Z = Y * (1-x-y) / y

		// XYZ -> RGB conversion
		const float3x3 XYZ2RGB  = {  2.5651, -1.1665, -0.3986,
									-1.0217,  1.9777,  0.0439,
									 0.0753, -0.2543,  1.1892  };
		return mul(XYZ2RGB, XYZ);
	}

	float4 ps_Tonemap (PS_IN pin) : SV_Target {
		float4 value = tex(pin.Tex);
		float currentBrightness = 0.167 + dot(tex(gBrightnessMap, 0.5).rgb, LUM_CONVERT) * 0.667;
		return float4(ToneReinhard(value.rgb, currentBrightness, 0.56, 1.2), value.a);
	}

	technique10 Tonemap {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_Tonemap()));
		}
	}

// copy
	float4 ps_Copy (PS_IN pin) : SV_Target {
		return tex(pin.Tex);
	}

	technique10 Copy {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_Copy()));
		}
	}

// Color grading
	Texture3D gColorGradingMap;

	SamplerState samColorGrading {
		Filter = MIN_MAG_MIP_LINEAR;
		AddressU = CLAMP;
		AddressV = CLAMP;
		AddressW = CLAMP;
	};

	float4 ps_ColorGrading(PS_IN pin) : SV_Target{
		float4 value = tex(pin.Tex);
		return float4(gColorGradingMap.SampleLevel(samColorGrading, saturate(value.rbg), 0).rbg, value.a);
	}

	technique10 ColorGrading {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_ColorGrading()));
		}
	}

// Tone mappings
	float4 CombineInput(float2 uv){
        return tex(uv) + tex(gBloomMap, uv)
            + (gUseDither ? lerp(0.00196, -0.00196, frac(0.25 + dot(uv, gScreenSize.xy * 0.5))) : 0);
	}

	#define gamma gParams[0]
	#define exposure gParams[1]
	#define whitePoint gParams[2]

	/*float3 SaturateColor(float3 color) {
		// return color / max(max(color.x, 1.0), max(color.y, color.z));
	}*/

	#define SaturateColor(x) x

// Luma-based Reinhard tone mapping
	float4 ps_Combine_ToneLumaBasedReinhard(PS_IN pin) : SV_Target {
		float4 inputValue = CombineInput(pin.Tex);
		float3 color = max(inputValue.rgb, 0.0) * exposure * 3.4;
        float luma = dot(color, float3(0.2126, 0.7152, 0.0722));
        float toneMappedLuma = luma / (whitePoint + luma);
        color *= toneMappedLuma / luma;
        color = pow(max(color, 0.0), (float3)(1.0 / gamma));
        return float4(color, inputValue.a);
	}

	technique10 Combine_ToneLumaBasedReinhard {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_Combine_ToneLumaBasedReinhard()));
		}
	}

// White preserving luma-based Reinhard tone mapping
	float4 ps_Combine_ToneWhitePreservingLumaBasedReinhard(PS_IN pin) : SV_Target {
		float4 inputValue = CombineInput(pin.Tex);
		float3 color = max(inputValue.rgb, 0.0) * exposure * 1.428;
        float luma = dot(color, float3(0.2126, 0.7152, 0.0722));
        float white = whitePoint * 1.20481927710843;
        float toneMappedLuma = luma * (1.0 + luma / (white * white)) / (1.0 + luma);
        color *= toneMappedLuma / luma;
        color = pow(max(color, 0.0), (float3)(1.0 / gamma));
        return float4(color, inputValue.a);
	}

	technique10 Combine_ToneWhitePreservingLumaBasedReinhard {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_Combine_ToneWhitePreservingLumaBasedReinhard()));
		}
	}

// Uncharted 2 tone mapping
	float4 ps_Combine_ToneUncharted2(PS_IN pin) : SV_Target {
		float4 inputValue = CombineInput(pin.Tex);
		float3 color = max(inputValue.rgb, 0.0);
        float A = 0.15;
        float B = 0.50;
        float C = 0.10;
        float D = 0.20;
        float E = 0.02;
        float F = 0.30;
        float W = whitePoint;
        color *= exposure * 1.4168;
        color = ((color * (A * color + C * B) + D * E) / (color * (A * color + B) + D * F)) - E / F;
        float white = ((W * (A * W + C * B) + D * E) / (W * (A * W + B) + D * F)) - E / F;
        color /= white;
        color = pow(max(color, 0.0), (float3)(1.0 / gamma));
        return float4(color, inputValue.a);
	}

	technique10 Combine_ToneUncharted2 {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_Combine_ToneUncharted2()));
		}
	}

// Reinhard (Habrahabr version)
	float4 ps_Combine_ToneReinhard(PS_IN pin) : SV_Target {
		float4 inputValue = CombineInput(pin.Tex);
		float3 value = max(inputValue.rgb, 0.0);
		return float4(SaturateColor(pow(max(ToneReinhard(value, 0.5, exposure * 0.715, whitePoint), 0.0), 1.0 / gamma)), inputValue.a);
	}

	technique10 Combine_ToneReinhard {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_Combine_ToneReinhard()));
		}
	}

// Filmic
	#define _SHOULDER_STRENGTH 0.22
	#define _LINEAR_STRENGTH 0.3
	#define _LINEAR_ANGLE 0.1
	#define _TOE_STRENGTH 0.20
	#define _TOE_NUMERATOR 0.01
	#define _TOE_DENUMERATOR 0.30

	float FilmicCurve(float x) {
		return (
			(x*(_SHOULDER_STRENGTH*x + _LINEAR_ANGLE*_LINEAR_STRENGTH) + _TOE_STRENGTH*_TOE_NUMERATOR) /
			(x*(_SHOULDER_STRENGTH*x + _LINEAR_STRENGTH) + _TOE_STRENGTH*_TOE_DENUMERATOR)
		) - _TOE_NUMERATOR / _TOE_DENUMERATOR;
	}

	float3 Filmic(float3 x) {
		float w = FilmicCurve(whitePoint);
		x = max(x, 0.0);
		return float3(
			FilmicCurve(x.r),
			FilmicCurve(x.g),
			FilmicCurve(x.b)) / w;
	}

	float4 ps_Combine_ToneFilmic(PS_IN pin) : SV_Target {
		float4 inputValue = CombineInput(pin.Tex);
		float3 value = inputValue.rgb;
		return float4(SaturateColor(pow(max(Filmic(1.17504 * exposure * value), 0.0), 1.0 / gamma)), inputValue.a);
	}

	technique10 Combine_ToneFilmic {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_Combine_ToneFilmic()));
		}
	}

// Reinhard (Filmic)
	#define _TOE 0.01

	float FilmicReinhardCurve(float x) {
		x = max(x, 0.0);
		float q = (_TOE + 1.0)*x*x;
		return q / (q + x + _TOE);
	}

	float3 FilmicReinhard(float3 x) {
		float w = FilmicReinhardCurve(whitePoint);
		return float3(
			FilmicReinhardCurve(x.r),
			FilmicReinhardCurve(x.g),
			FilmicReinhardCurve(x.b)) / w;
	}

	float4 ps_Combine_ToneFilmicReinhard(PS_IN pin) : SV_Target {
		float4 inputValue = CombineInput(pin.Tex);
		float3 value = inputValue.rgb;
		return float4(SaturateColor(pow(max(FilmicReinhard(1.008 * exposure * value), 0.0), 1.0 / gamma)), inputValue.a);
	}

	technique10 Combine_ToneFilmicReinhard {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_Combine_ToneFilmicReinhard()));
		}
	}

// Disabled
	float4 ps_Combine (PS_IN pin) : SV_Target {
		float4 inputValue = CombineInput(pin.Tex);
		float3 value = inputValue.rgb;
		return float4(SaturateColor(value), inputValue.a);
	}

	technique10 Combine {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_Combine()));
		}
	}

// Bloom
	float4 ps_Bloom (PS_IN pin) : SV_Target {
		return saturate(tex(pin.Tex) - 1.2);
	}

	technique10 Bloom {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_Bloom()));
		}
	}

// Bloom
	float4 ps_BloomHighThreshold(PS_IN pin) : SV_Target {
		return saturate(tex(pin.Tex) - 1.5);
	}

	technique10 BloomHighThreshold {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_BloomHighThreshold()));
		}
	}