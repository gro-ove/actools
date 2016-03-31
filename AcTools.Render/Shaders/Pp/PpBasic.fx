// textures
	Texture2D gInputMap;

	SamplerState samInputImage {
		Filter = MIN_MAG_LINEAR_MIP_POINT;
		AddressU = CLAMP;
		AddressV = CLAMP;
	};
	
// input resources
	cbuffer cbPerObject : register(b0) {
		float4 gScreenSize;
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