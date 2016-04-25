// textures
	Texture2D gInputMap;
	Texture2D gOverlayMap;
	Texture2D gDepthMap;

	SamplerState samInputImage {
		Filter = MIN_MAG_LINEAR_MIP_POINT;
		AddressU = CLAMP;
		AddressV = CLAMP;
	};
	
// input resources
	cbuffer cbPerObject : register(b0) {
		float4 gScreenSize;
		float gSizeMultipler;
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
		vout.PosH = float4(vin.PosL, 1.0f);
		vout.Tex = vin.Tex;
		return vout;
	}

	float4 ps_Copy(PS_IN pin) : SV_Target{
		return gInputMap.SampleLevel(samInputImage, pin.Tex, 0.0f);
	}

	technique11 Copy {
		pass P0 {
			SetVertexShader(CompileShader(vs_5_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_5_0, ps_Copy()));
		}
	}

// overlay (gui) mode
	float4 ps_Overlay(PS_IN pin) : SV_Target{
		float4 b = gInputMap.Sample(samInputImage, pin.Tex);
		float4 o = gOverlayMap.Sample(samInputImage, pin.Tex);
		return float4(b.rgb * (1 - o.a) + o.rgb * o.a, 1.0);
	}

	technique11 Overlay {
		pass P0 {
			SetVertexShader(CompileShader(vs_5_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_5_0, ps_Overlay()));
		}
	}

// shadow (gui) mode
	float4 ps_Shadow(PS_IN pin) : SV_Target{
		float4 b = gInputMap.Sample(samInputImage, pin.Tex);

		float4 tex = gOverlayMap.Sample(samInputImage, pin.Tex);
		tex.rgb *= tex.a;

		float x, y;
		for (x = -1; x <= 1; x++)
			for (y = -1; y <= 1; y++) {
				float4 v = gOverlayMap.Sample(samInputImage, pin.Tex + float2(x * gScreenSize.z, y * gScreenSize.w));
				tex.a = max(tex.a, v.a);
			}

		return float4(tex.rgb, max(b.a, tex.a));;
	}

	technique11 Shadow {
		pass P0 {
			SetVertexShader(CompileShader(vs_5_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_5_0, ps_Shadow()));
		}
	}

// depth debug mode
	float4 ps_Depth(PS_IN pin) : SV_Target{
		float4 background = gInputMap.SampleLevel(samInputImage, pin.Tex, 0.0f);

		float mx = gScreenSize.z * 20;
		float my = gScreenSize.w * 20;
		float x = max(1 - gSizeMultipler - mx, 0);
		float y = max(1 - gSizeMultipler - my, 0);

		float rx = max(1 - mx, gSizeMultipler);
		float ry = max(1 - my, gSizeMultipler);

		if (pin.Tex.x > x && pin.Tex.x < rx) {
			if (pin.Tex.y > y && pin.Tex.y < ry) {
				float2 depthUv = float2((pin.Tex.x - x) / gSizeMultipler, (pin.Tex.y - y) / gSizeMultipler);
				return gDepthMap.SampleLevel(samInputImage, depthUv, 0.0f).r;
			}
		}

		return background;
	}

	technique11 Depth { // PT
		pass P0 {
			SetVertexShader(CompileShader(vs_5_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_5_0, ps_Depth()));
		}
	}

// fxaa
	#define FXAA_PRESET 5
	#include "FXAA.fx"

	float4 ps_Fxaa(PS_IN pin) : SV_Target {
		FxaaTex tex = { samInputImage, gInputMap };
		float3 aaImage = FxaaPixelShader(pin.Tex, tex, gScreenSize.zw);
		return float4(aaImage, 1.0f);
	}

	technique11 Fxaa { // PT
		pass P0 {
			SetVertexShader( CompileShader( vs_5_0, vs_main() ) );
			SetGeometryShader( NULL );
			SetPixelShader( CompileShader( ps_5_0, ps_Fxaa() ) );
		}
	}