// textures
	Texture2D gInputMap;
	Texture2D gOverlayMap;
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
		vout.PosH = float4(vin.PosL, 1.0);
		vout.Tex = vin.Tex;
		return vout;
	}

	float4 ps_Copy(PS_IN pin) : SV_Target{
		return gInputMap.Sample(samInputImage, pin.Tex);
	}

		technique10 Copy {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_Copy()));
		}
	}

	float4 ps_Cut(PS_IN pin) : SV_Target {
		return gInputMap.Sample(samInputImage, (pin.Tex - 0.5) * gSizeMultipler + 0.5);
	}

	technique10 Cut {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_Cut()));
		}
	}

	SamplerState samInputImageHq {
		Filter = ANISOTROPIC;
		MaxAnisotropy = 8;

		AddressU = WRAP;
		AddressV = WRAP;
	};

	float4 SampleBicubic(Texture2D tex, sampler texSampler, float2 uv){
		//--------------------------------------------------------------------------------------
		// Calculate the center of the texel to avoid any filtering

		float2 textureDimensions = gScreenSize.xy;
		float2 invTextureDimensions = gScreenSize.zw;

		uv *= textureDimensions;

		float2 texelCenter = floor(uv - 0.5f) + 0.5f;
		float2 fracOffset = uv - texelCenter;
		float2 fracOffset_x2 = fracOffset * fracOffset;
		float2 fracOffset_x3 = fracOffset * fracOffset_x2;

		//--------------------------------------------------------------------------------------
		// Calculate the filter weights (B-Spline Weighting Function)

		float2 weight0 = fracOffset_x2 - 0.5f * (fracOffset_x3 + fracOffset);
		float2 weight1 = 1.5f * fracOffset_x3 - 2.5f * fracOffset_x2 + 1.f;
		float2 weight3 = 0.5f * (fracOffset_x3 - fracOffset_x2);
		float2 weight2 = 1.f - weight0 - weight1 - weight3;

		//--------------------------------------------------------------------------------------
		// Calculate the texture coordinates

		float2 scalingFactor0 = weight0 + weight1;
		float2 scalingFactor1 = weight2 + weight3;

		float2 f0 = weight1 / (weight0 + weight1);
		float2 f1 = weight3 / (weight2 + weight3);

		float2 texCoord0 = texelCenter - 1.f + f0;
		float2 texCoord1 = texelCenter + 1.f + f1;

		texCoord0 *= invTextureDimensions;
		texCoord1 *= invTextureDimensions;

		//--------------------------------------------------------------------------------------
		// Sample the texture

		return tex.Sample(texSampler, float2(texCoord0.x, texCoord0.y)) * scalingFactor0.x * scalingFactor0.y +
			tex.Sample(texSampler, float2(texCoord1.x, texCoord0.y)) * scalingFactor1.x * scalingFactor0.y +
			tex.Sample(texSampler, float2(texCoord0.x, texCoord1.y)) * scalingFactor0.x * scalingFactor1.y +
			tex.Sample(texSampler, float2(texCoord1.x, texCoord1.y)) * scalingFactor1.x * scalingFactor1.y;
	}

	float4 ps_CopyHq(PS_IN pin) : SV_Target {
		//return SampleBicubic(gInputMap, samInputImage, pin.Tex);
		return gInputMap.Sample(samInputImageHq, pin.Tex);
	}

	technique10 CopyHq {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_CopyHq()));
		}
	}

	float4 ps_CopyNoAlpha(PS_IN pin) : SV_Target {
		return float4(gInputMap.SampleLevel(samInputImage, pin.Tex, 0.0).rgb, 1.0);
	}

	technique10 CopyNoAlpha {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_CopyNoAlpha()));
		}
	}

// overlay (gui) mode
	float4 ps_Overlay(PS_IN pin) : SV_Target {
		float4 b = gInputMap.Sample(samInputImage, pin.Tex);
		float4 o = gOverlayMap.Sample(samInputImage, pin.Tex);
		return float4(b.rgb * (1 - o.a) + o.rgb * o.a, 1.0);
	}

	technique10 Overlay {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_Overlay()));
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

	technique10 Shadow {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_Shadow()));
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

	technique10 Depth { // PT
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_Depth()));
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

	technique10 Fxaa { // PT
		pass P0 {
			SetVertexShader( CompileShader( vs_4_0, vs_main() ) );
			SetGeometryShader( NULL );
			SetPixelShader( CompileShader( ps_4_0, ps_Fxaa() ) );
		}
	}