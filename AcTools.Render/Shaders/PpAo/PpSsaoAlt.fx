// textures
	Texture2D gDepthMap;
	Texture2D gNormalMap;
	Texture2D gNoiseMap;

	SamplerState samLinear {
		Filter = MIN_MAG_MIP_LINEAR;
		AddressU = CLAMP;
		AddressV = CLAMP;
	};

	SamplerState samDepth {
		Filter = MIN_MAG_LINEAR_MIP_POINT;
		AddressU = Border;
		AddressV = Border;
		BorderColor = float4(0, 0, 0, 1e5f);
	};

	SamplerState samNoise {
		Filter = MIN_MAG_MIP_POINT;
		AddressU = WRAP;
		AddressV = WRAP;
	};
    
// input resources
	#define SAMPLE_COUNT 24
	#define SAMPLE_THRESHOLD 14

    cbuffer cbPerFrame : register(b0) {
		float3 gSamplesKernel[SAMPLE_COUNT];
        matrix gViewProjInv;
        matrix gViewProj;
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
        float4 position = mul(float4(uv.x * 2 - 1, -(uv.y * 2 - 1), depth, 1), gViewProjInv);
        return position.xyz / position.w;
    }

    float GetDepth(float2 uv){
        return gDepthMap.SampleLevel(samDepth, uv, 0).x;
    }

    float3 GetUv(float3 position){
        float4 pVP = mul(float4(position, 1.0f), gViewProj);
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
	#define uSampleRadius 0.8
	#define uVerticalMultipler 2.7

// new
	float4 SsaoFn(float2 UV) {
		float depth = GetDepth(UV);
		float3 origin = GetPosition(UV, depth);

		float3 normal = GetNormal(UV);

		float3 random = normalize(gNoiseMap.Sample(samNoise, UV * gNoiseSize).xyz);
		float occlusion = 0.0;
		for (int i = 0; i < SAMPLE_COUNT; ++i) {
			float3 hemisphereRandomNormal = reflect(gSamplesKernel[i], random);
			float3 hemisphereNormalOrientated = hemisphereRandomNormal * sign(
				dot(hemisphereRandomNormal, normal));

			float2 samUv = GetUv(origin + hemisphereNormalOrientated * uSampleRadius).xy;
			float samDepth = GetDepth(samUv);
			float3 samW = GetPosition(samUv, samDepth);

			float3 samDirW = samW - origin;
			samDirW.xz *= uVerticalMultipler;

			float rangeDelta = length(samDirW);	
			occlusion += saturate((uSampleRadius / rangeDelta) * step(samDepth + 0.00005, depth));
		}

		return 1.0 - saturate(pow(occlusion / SAMPLE_THRESHOLD, 1.8));
	}

    float4 ps_Ssao(PS_IN pin) : SV_Target {
		return SsaoFn(pin.Tex);
    }

    technique11 SsaoVs {
        pass P0 {
            SetVertexShader( CompileShader( vs_5_0, vs_main() ) );
            SetGeometryShader( NULL );
            SetPixelShader( CompileShader( ps_5_0, ps_Ssao() ) );
        }
    }