// textures
	Texture2D gDepthMap;
	Texture2D gNormalMap;
	Texture2D gFirstStepMap;

	SamplerState samLinear {
		Filter = MIN_MAG_MIP_LINEAR;
		AddressU = CLAMP;
		AddressV = CLAMP;
	};

// params
	#define MAX_BLUR_RADIUS 4
	#define WEIGHTS_SIZE MAX_BLUR_RADIUS * 2 + 1
	#define BLUR_RADIUS 4

	#define _DepthAnalysis true
	#define _NormalAnalysis true
	#define _DepthAnalysisFactor 1.0
    
// input resources
    cbuffer cbPerFrame : register(b0) {
		float gWeights[WEIGHTS_SIZE];

		float2 gSourcePixel;
		float2 gNearFarValue;
    }	

// fn structs
    struct VS_IN {
        float3 PosL : POSITION;
        float2 Tex  : TEXCOORD;
    };

    struct PS_IN {
        float4 PosH : SV_POSITION;
		float2 Tex  : TEXCOORD0;
    };

// functions
    float GetDepth(float2 uv){
        return gDepthMap.SampleLevel(samLinear, uv, 0).x;
    }

	float3 GetNormal(float2 coords) {
		return gNormalMap.Sample(samLinear, coords).xyz;
	}

// one vertex shader for everything
    PS_IN vs_main(VS_IN vin) {
        PS_IN vout;
        vout.PosH = float4(vin.PosL, 1.0f);
        vout.Tex = vin.Tex;
        return vout;
    }

// blur
	float LinearizeDepth(float depth){
		float z_n = 2.0 * depth - 1.0;
		return 2.0 * gNearFarValue.x * gNearFarValue.y / (gNearFarValue.y + gNearFarValue.x - z_n * (gNearFarValue.y - gNearFarValue.x));
	}

	float4 Blur(const float2 UV, const float2 dir){
		float4 finalColor = gFirstStepMap.Sample(samLinear, UV) * gWeights[BLUR_RADIUS];

		float lDepthC = _DepthAnalysis ? LinearizeDepth(GetDepth(UV)) : 0;

		float3 normalC = GetNormal(UV);

		float totalAdditionalWeight = gWeights[BLUR_RADIUS];

		for (int i = 1; i <= BLUR_RADIUS; i++){
			float2 UVL = UV - gSourcePixel * dir * i;
			float2 UVR = UV + gSourcePixel * dir * i;

			float depthFactorR = 1.0f;
			float depthFactorL = 1.0f;
			float normalFactorL = 1.0f;
			float normalFactorR = 1.0f;

			//[flatten]
			if (_DepthAnalysis)	{
				float lDepthR = LinearizeDepth(GetDepth(UVR));
				float lDepthL = LinearizeDepth(GetDepth(UVL));

				depthFactorR = saturate(1.0f / (abs(lDepthR - lDepthC) / _DepthAnalysisFactor));
				depthFactorL = saturate(1.0f / (abs(lDepthL - lDepthC) / _DepthAnalysisFactor));
			}

			//[flatten]
			if (_NormalAnalysis)	{
				float3 normalR = GetNormal(UVR);
				float3 normalL = GetNormal(UVL);

				normalFactorL = saturate(max(0.0f, dot(normalC, normalL)));
				normalFactorR = saturate(max(0.0f, dot(normalC, normalR)));
			}

			float cwR = gWeights[BLUR_RADIUS + i] * depthFactorR * normalFactorR;
			float cwL = gWeights[BLUR_RADIUS - i] * depthFactorL * normalFactorL;

			finalColor += gFirstStepMap.Sample(samLinear, UVR) * cwR;
			finalColor += gFirstStepMap.Sample(samLinear, UVL) * cwL;

			totalAdditionalWeight += cwR;
			totalAdditionalWeight += cwL;
		}


		return finalColor / totalAdditionalWeight;
	}

    float4 ps_BlurH(PS_IN pin) : SV_Target {
		return Blur(pin.Tex, float2(1, 0));
    }

    float4 ps_BlurV(PS_IN pin) : SV_Target {
		return Blur(pin.Tex, float2(0, 1));
    }

	technique11 BlurH {
		pass P0 {
			SetVertexShader(CompileShader(vs_5_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_5_0, ps_BlurH()));
		}
	}

	technique11 BlurV {
		pass P0 {
			SetVertexShader(CompileShader(vs_5_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_5_0, ps_BlurV()));
		}
	}