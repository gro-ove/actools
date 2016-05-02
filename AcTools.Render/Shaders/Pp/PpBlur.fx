#include "Common.fx"

Texture2D gMapsMap;

static const int SAMPLE_COUNT = 15;

cbuffer cbPerFrame : register(b0) {
	float4 gSampleOffsets[SAMPLE_COUNT];
	float gSampleWeights[SAMPLE_COUNT];
	float gPower;
}

// default
	float4 ps_GaussianBlurDebug(PS_IN pin) : SV_Target {
		return tex(pin.Tex);
	}

	float4 ps_GaussianBlur(PS_IN pin) : SV_Target {
		float4 c = 0;
		for (int i = 0; i < SAMPLE_COUNT; i++){
			c += tex(pin.Tex + gSampleOffsets[i] * gPower) * gSampleWeights[i];
		}
		return c;
	}

	technique10 GaussianBlur {
		pass P0 {
			SetVertexShader( CompileShader( vs_4_0, vs_main() ) );
			SetGeometryShader( NULL );
			SetPixelShader( CompileShader( ps_4_0, ps_GaussianBlur() ) );
		}
	}

// special reflection mode
	float4 ps_ReflectionGaussianBlur(PS_IN pin) : SV_Target {
		float power = saturate(1 - tex(gMapsMap, pin.Tex).y * 15);

		float4 c = 0;
		for (int i = 0; i < SAMPLE_COUNT; i++){
			c += tex(pin.Tex + gSampleOffsets[i] * power) * gSampleWeights[i];
		}

		return c;
	}

	technique10 ReflectionGaussianBlur {
		pass P0 {
			SetVertexShader( CompileShader( vs_4_0, vs_main() ) );
			SetGeometryShader( NULL );
			SetPixelShader( CompileShader( ps_4_0, ps_ReflectionGaussianBlur() ) );
		}
	}