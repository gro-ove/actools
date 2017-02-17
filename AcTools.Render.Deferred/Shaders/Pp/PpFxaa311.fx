// textures
	Texture2D gInputMap;
	Texture2D gDepthMap;

	SamplerState samInputImage {
		Filter = MIN_MAG_MIP_LINEAR;
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

	float4 ps_Luma(PS_IN pin) : SV_Target{
		float4 color = gInputMap.SampleLevel(samInputImage, pin.Tex, 0.0f);
		color.a = dot(color.rgb, float3(0.299, 0.587, 0.114));
		return color;
	}

	technique11 Luma {
		pass P0 {
			SetVertexShader(CompileShader(vs_5_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_5_0, ps_Luma()));
		}
	}

// fxaa
	#define FXAA_PC 1
	#define FXAA_HLSL_5 1
	#define FXAA_QUALITY__PRESET 29

	#include "FXAA_311.fx"

	float4 ps_Fxaa_311(PS_IN pin) : SV_Target {
		FxaaTex tex = { samInputImage, gInputMap };
		return FxaaPixelShader(
			pin.Tex,								// FxaaFloat2 pos,
			FxaaFloat4(0.0f, 0.0f, 0.0f, 0.0f),		// FxaaFloat4 fxaaConsolePosPos,
			tex,							// FxaaTex tex,
			tex,							// FxaaTex fxaaConsole360TexExpBiasNegOne,
			tex,							// FxaaTex fxaaConsole360TexExpBiasNegTwo,
			gScreenSize.zw,							// FxaaFloat2 fxaaQualityRcpFrame,
			FxaaFloat4(0.0f, 0.0f, 0.0f, 0.0f),		// FxaaFloat4 fxaaConsoleRcpFrameOpt,
			FxaaFloat4(0.0f, 0.0f, 0.0f, 0.0f),		// FxaaFloat4 fxaaConsoleRcpFrameOpt2,
			FxaaFloat4(0.0f, 0.0f, 0.0f, 0.0f),		// FxaaFloat4 fxaaConsole360RcpFrameOpt2,
			1.0f,									// FxaaFloat fxaaQualitySubpix,
			0.125f,									// FxaaFloat fxaaQualityEdgeThreshold,
			0.0833f,								// FxaaFloat fxaaQualityEdgeThresholdMin,
			0.0f,									// FxaaFloat fxaaConsoleEdgeSharpness,
			0.0f,									// FxaaFloat fxaaConsoleEdgeThreshold,
			0.0f,									// FxaaFloat fxaaConsoleEdgeThresholdMin,
			FxaaFloat4(0.0f, 0.0f, 0.0f, 0.0f)		// FxaaFloat fxaaConsole360ConstDir,
		);
	}

	technique11 Fxaa {
		pass P0 {
			SetVertexShader( CompileShader( vs_5_0, vs_main() ) );
			SetGeometryShader( NULL );
			SetPixelShader( CompileShader( ps_5_0, ps_Fxaa_311() ) );
		}
	}