// textures
	Texture2D gInputMap;
	Texture2D gDepthMap;

	Texture2D gEdgesMap;
	Texture2D gBlendMap;

	Texture2D gAreaTexMap;
	Texture2D gSearchTexMap;

	SamplerState samInputImage {
		Filter = MIN_MAG_LINEAR_MIP_POINT;
		AddressU = CLAMP;
		AddressV = CLAMP;
	};
	
// input resources
	cbuffer cbPerObject : register(b0) {
		float4 gScreenSizeSpec;
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

// smaa
	#define SMAA_HLSL_4_1
	#define SMAA_PRESET_ULTRA
	#define SMAA_RT_METRICS gScreenSizeSpec

	#include "SMAA.fx"

// edge detection
	struct Smaa_PS_IN {
		float4 PosH       : SV_POSITION;
		float4 Offset[3]  : OFFSET;
		float2 Tex        : TEXCOORD;
	};

	Smaa_PS_IN vs_Smaa(VS_IN vin) {
		Smaa_PS_IN vout;

		vout.PosH = float4(vin.PosL, 1.0f);
		vout.Tex = vin.Tex;

		vout.Offset[0] = mad(SMAA_RT_METRICS.xyxy, float4(-1.0, 0.0, 0.0, -1.0), vout.Tex.xyxy);
		vout.Offset[1] = mad(SMAA_RT_METRICS.xyxy, float4(1.0, 0.0, 0.0, 1.0), vout.Tex.xyxy);
		vout.Offset[2] = mad(SMAA_RT_METRICS.xyxy, float4(-2.0, 0.0, 0.0, -2.0), vout.Tex.xyxy);

		return vout;
	}

	float4 ps_Smaa(Smaa_PS_IN pin) : SV_Target {
		return float4(SMAAColorEdgeDetectionPS(pin.Tex, pin.Offset, gInputMap), 0, 0);
		// return float4(SMAALumaEdgeDetectionPS(pin.Tex, pin.Offset, gInputMap), 0, 0);
		// return float4(SMAADepthEdgeDetectionPS(pin.Tex, pin.Offset, gDepthMap), 0, 0);
	}

	technique11 Smaa {
		pass P0 {
			SetVertexShader( CompileShader( vs_5_0, vs_Smaa() ) );
			SetGeometryShader( NULL );
			SetPixelShader( CompileShader( ps_5_0, ps_Smaa() ) );
		}
	}

// blending weight calculation
	struct Smaa_B_PS_IN {
		float4 PosH       : SV_POSITION;
		float2 Pix        : PIXCOORD;
		float4 Offset[3]  : OFFSET;
		float2 Tex        : TEXCOORD;
	};

	Smaa_B_PS_IN vs_Smaa_B(VS_IN vin) {
		Smaa_B_PS_IN vout;

		vout.PosH = float4(vin.PosL, 1.0f);
		vout.Tex = vin.Tex;

		vout.Pix = vout.Tex * SMAA_RT_METRICS.zw;

		vout.Offset[0] = mad(SMAA_RT_METRICS.xyxy, float4(-0.25, -0.125, 1.25, -0.125), vout.Tex.xyxy);
		vout.Offset[1] = mad(SMAA_RT_METRICS.xyxy, float4(-0.125, -0.25, -0.125, 1.25), vout.Tex.xyxy);

		// And these for the searches, they indicate the ends of the loops:
		vout.Offset[2] = mad(SMAA_RT_METRICS.xxyy,
			float4(-2.0, 2.0, -2.0, 2.0) * float(SMAA_MAX_SEARCH_STEPS),
			float4(vout.Offset[0].xz, vout.Offset[1].yw));

		return vout;
	}

	float4 ps_Smaa_B(Smaa_B_PS_IN pin) : SV_Target {
		return SMAABlendingWeightCalculationPS(pin.Tex, pin.Pix, pin.Offset, gEdgesMap, gAreaTexMap, gSearchTexMap, float4(0, 0, 0, 0));
	}

	technique11 SmaaB {
		pass P0 {
			SetVertexShader(CompileShader(vs_5_0, vs_Smaa_B()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_5_0, ps_Smaa_B()));
		}
	}

// neighborhood blending
	struct Smaa_N_PS_IN {
		float4 PosH       : SV_POSITION;
		float4 Offset     : OFFSET;
		float2 Tex        : TEXCOORD;
	};

	Smaa_N_PS_IN vs_Smaa_N(VS_IN vin) {
		Smaa_N_PS_IN vout;

		vout.PosH = float4(vin.PosL, 1.0f);
		vout.Tex = vin.Tex;
		vout.Offset = mad(SMAA_RT_METRICS.xyxy, float4(1.0, 0.0, 0.0, 1.0), vout.Tex.xyxy);

		return vout;
	}

	float4 ps_Smaa_N(Smaa_N_PS_IN pin) : SV_Target {
		return SMAANeighborhoodBlendingPS(pin.Tex, pin.Offset, gInputMap, gBlendMap);
	}

	technique11 SmaaN {
		pass P0 {
			SetVertexShader(CompileShader(vs_5_0, vs_Smaa_N()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_5_0, ps_Smaa_N()));
		}
	}