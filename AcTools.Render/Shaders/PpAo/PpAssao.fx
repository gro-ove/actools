// textures
	Texture2D gDepthMap;
	Texture2D gNormalMap;
	Texture2D gNoiseMap;
	Texture2D gDitherMap;
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
        matrix gWorldViewProjInv;
        matrix gWorldViewProj;
        matrix gProj;
        matrix gProjInv;

		float4 gViewFrustumVectors[4];
		matrix gNormalsToViewSpace;
    }	

// fn structs
    struct VS_IN {
        float3 PosL : POSITION;
        float2 Tex  : TEXCOORD;
    };

    struct PS_IN {
        float4 PosH           : SV_POSITION;
		float2 Tex            : TEXCOORD0;
		float4 FrustumVector  : FRUSTUM_VECTOR;
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

// one vertex shader for everything
    PS_IN vs_main(VS_IN vin, uint vertexID : SV_VertexID) {
        PS_IN vout;
        vout.PosH = float4(vin.PosL, 1.0f);
        vout.Tex = vin.Tex;
		vout.FrustumVector = gViewFrustumVectors[vertexID];
        return vout;
    }

	float3 GetNormal(float2 coords) {
		return gNormalMap.Sample(samLinear, coords).xyz;
	}

// hard-coded consts
	#define SSAO_ENABLE_NORMAL_WORLD_TO_VIEW_CONVERSION 1
	#include "ASSAO.fx"

// new
    technique11 PrepareDepth {
        pass P0 {
            SetVertexShader( CompileShader( vs_5_0, vs_main() ) );
            SetGeometryShader( NULL );
            SetPixelShader( CompileShader( ps_5_0, PSPrepareDepthsAndNormals() ) );
        }
    }

    float4 ps_Assao(PS_IN pin) : SV_Target {
		float   outShadowTerm;
		float   outWeight;
		float4  outEdges;
		GenerateSSAOShadowsInternal(outShadowTerm, outEdges, outWeight, pin.PosH.xy/*, inUV*/, 2, false);
		return outShadowTerm;
		//return float4(outShadowTerm, PackEdges(outEdges), outWeight, 1.0);
		// out0.y = PackEdges(outEdges);

		// return 1.0;
    }

    technique11 Assao {
        pass P0 {
            SetVertexShader( CompileShader( vs_5_0, vs_main() ) );
            SetGeometryShader( NULL );
            SetPixelShader( CompileShader( ps_5_0, ps_Assao() ) );
        }
    }