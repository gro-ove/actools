// textures
	Texture2D gDepthMap;
	Texture2D gNormalMap;
	Texture2D gNoiseMap;
	Texture2D gFirstStepMap;

	SamplerState samLinear {
		Filter = MIN_MAG_MIP_LINEAR;
		AddressU = CLAMP;
		AddressV = CLAMP;
	};

	SamplerState samNoise {
		Filter = MIN_MAG_MIP_POINT;
		AddressU = WRAP;
		AddressV = WRAP;
	};
    
// input resources
	#define SAMPLE_COUNT 16

    cbuffer cbPerFrame : register(b0) {
		float3 gSamplesKernel[SAMPLE_COUNT];

		matrix gCameraProjInv;
		matrix gCameraProj;

        matrix gWorldViewProjInv;
        matrix gWorldViewProj;
        float3 gEyePosW;

		float2 gNoiseSize;
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
    float3 GetPosition(float2 uv, float depth){
        float4 position = mul(float4(uv.x * 2 - 1, -(uv.y * 2 - 1), depth, 1), gWorldViewProjInv);
        return position.xyz / position.w;
    }

    float GetDepth(float2 uv){
        return gDepthMap.SampleLevel(samLinear, uv, 0).x;
    }

    float3 GetUv(float3 position){
        float4 pVP = mul(float4(position, 1.0f), gWorldViewProj);
        pVP.xy = float2(0.5f, 0.5f) + float2(0.5f, -0.5f) * pVP.xy / pVP.w;
        return float3(pVP.xy, pVP.z / pVP.w);
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

// hard-coded consts
	#define RADIUS 0.15
	#define NORMAL_BIAS 0.01

// new
	float depthAssessment_invsqrt(float nonLinearDepth)	{
		return 1 / sqrt(1.0 - nonLinearDepth);
	}

	float calculateOcclusion(float3 texelPosition, float3 texelNormal, float3 sampleDir, float radius, float assessmentDepth){
		float3 position = texelPosition + sampleDir * radius;

		float3 sampleProjected = GetUv(position);
		float sampleRealDepth = GetDepth(sampleProjected.xy);

		float assessProjected = depthAssessment_invsqrt(sampleProjected.z);
		float assessReaded = depthAssessment_invsqrt(sampleRealDepth);

		float differnce = (assessReaded - assessProjected);

		// return differnce;

		float occlussion = step(differnce, 0); // (x >= y) ? 1 : 0
		float distanceCheck = min(1.0, radius / abs(assessmentDepth - assessReaded));

		// fix?

		//sampleDir.xz *= 100.0;
		//float3 fixed = sampleDir * radius;
		//radius = length(fixed);
		//float distanceCheck = min(1.0, radius / abs(assessmentDepth - assessReaded));
		//float3 delta = texelPosition - position;
		//delta.xz *= 5.0;
		//float distanceCheck = min(1.0, 0.1 / length(delta));

		//distanceCheck *= abs(sampleDir.y);

		return occlussion * distanceCheck;
	}

	float4 SsaoFn(float2 UV) {
		float depth = GetDepth(UV);
		float3 texelNormal = GetNormal(UV);
		float3 texelPosition = GetPosition(UV, depth) + texelNormal * NORMAL_BIAS;

		float3 random = normalize(gNoiseMap.Sample(samNoise, UV * gNoiseSize).xyz);
		float assessOriginal = depthAssessment_invsqrt(depth);

		float ssao = 0;

		for (int i = 0; i < SAMPLE_COUNT; i++) {
			float3 hemisphereRandomNormal = reflect(gSamplesKernel[i], random);

			float3 hemisphereNormalOrientated = hemisphereRandomNormal * sign(
				dot(hemisphereRandomNormal, texelNormal));

			ssao += calculateOcclusion(texelPosition,
				texelNormal,
				hemisphereNormalOrientated,
				RADIUS,
				assessOriginal);
		}

		return pow(1 - (ssao / SAMPLE_COUNT), 2);
	}

    float4 ps_Ssao(PS_IN pin) : SV_Target {
		return SsaoFn(pin.Tex);
    }

    technique11 Ssao {
        pass P0 {
            SetVertexShader( CompileShader( vs_5_0, vs_main() ) );
            SetGeometryShader( NULL );
            SetPixelShader( CompileShader( ps_5_0, ps_Ssao() ) );
        }
    }

// blur
	#define MAX_BLUR_RADIUS 4
	#define WEIGHTS_SIZE MAX_BLUR_RADIUS * 2 + 1
	#define BLUR_RADIUS 4

	#define _DepthAnalysis true
	#define _NormalAnalysis true
	#define _DepthAnalysisFactor 1.0

	cbuffer cbBlur : register(b1) {
		float gWeights[WEIGHTS_SIZE];

		float2 gSourcePixel;
		float2 gNearFarValue;

		//float DepthAnalysisFactor;
		//bool DepthAnalysis;
		//bool NormalAnalysis;
	}

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