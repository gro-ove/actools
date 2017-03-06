// textures
    Texture2D gDiffuseMap;
	Texture2D gDepthMap;
    Texture2D gBaseReflectionMap;
    Texture2D gNormalMap;
	Texture2D gFirstStepMap;

	SamplerState samLinear {
		Filter = MIN_MAG_MIP_LINEAR;
		AddressU = CLAMP;
		AddressV = CLAMP;
	};
    
// input resources
    cbuffer cbPerFrame : register(b0) {
		matrix gCameraProjInv;
		matrix gCameraProj;

        matrix gWorldViewProjInv;
        matrix gWorldViewProj;
        float3 gEyePosW;

		// bool gTemporary;
    }

    /*cbuffer cbSettings {
		float gStartFrom;
		float gFixMultiplier;
		float gOffset;
		float gGlowFix;
		float gDistanceThreshold;
    }*/

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

    float GetDepth(float2 uv, SamplerState s){
        return gDepthMap.SampleLevel(s, uv, 0).x;
    }

    float3 GetUv(float3 position){
        float4 pVP = mul(float4(position, 1.0f), gWorldViewProj);
        pVP.xy = float2(0.5f, 0.5f) + float2(0.5f, -0.5f) * pVP.xy / pVP.w;
        return float3(pVP.xy, pVP.z / pVP.w);
    }

// one vertex shader for everything
    PS_IN vs_main(VS_IN vin) {
        PS_IN vout;
        vout.PosH = float4(vin.PosL, 1.0f);
        vout.Tex = vin.Tex;
        return vout;
    }

	float3 GetNormal(float2 coords, SamplerState s) {
		return gNormalMap.Sample(s, coords).xyz;
	}

// hard-coded consts
	#define gStartFrom 0.02
	#define gFixMultiplier 0.7
	#define gOffset 0.048
	#define gGlowFix 0.15
	#define gDistanceThreshold 0.092

// new
    #define ITERATIONS 30

	float4 GetReflection(float2 coords, float3 normal, SamplerState s) {
		float depth = GetDepth(coords, s);
		float3 position = GetPosition(coords, depth);
		float3 viewDir = normalize(position - gEyePosW);
		float3 reflectDir = normalize(reflect(viewDir, normal));

		//depth = pow(depth, 20);
		//return float4(depth, depth, depth, 1.0);

		float3 newUv = 0;
		float L = gStartFrom;

		float3 calculatedPosition, newPosition;
		float newL;

		for (int i = 0; i < ITERATIONS; i++) {
			calculatedPosition = position + reflectDir * L;

			newUv = GetUv(calculatedPosition);
			newPosition = GetPosition(newUv.xy, GetDepth(newUv.xy, s));

			float newL = length(position - newPosition);
			newL = L + min(newL - L, gOffset + L * gGlowFix);

			L = L * (1 - gFixMultiplier) + newL * gFixMultiplier;
		}

		calculatedPosition = position + reflectDir * L;
		newUv = GetUv(calculatedPosition);
		newPosition = GetPosition(newUv.xy, GetDepth(newUv.xy, s));

		float fresnel = saturate(3.2 * pow(1 + dot(viewDir, normal), 2));
		float quality = 1 - saturate(abs(length(calculatedPosition - newPosition)) / gDistanceThreshold);

		float alpha = fresnel * quality * saturate(min(newUv.x, 1 - newUv.x) / 0.1) * saturate(min(newUv.y, 1 - newUv.y) / 0.1);
		return float4(newUv.xy - coords, saturate(L / quality), alpha);
	}

	float4 SslrFn(float2 coords, SamplerState s) {
		return GetReflection(coords, GetNormal(coords, s), s);
	}

    float4 ps_Sslr(PS_IN pin) : SV_Target {
		return SslrFn(pin.Tex, samLinear);
    }

    technique11 Sslr {
        pass P0 {
            SetVertexShader( CompileShader( vs_5_0, vs_main() ) );
            SetGeometryShader( NULL );
            SetPixelShader( CompileShader( ps_5_0, ps_Sslr() ) );
        }
    }
	
	cbuffer POISSON_DISKS {
		float2 poissonDisk[32] = {
			float2(0.7107409f, 0.5917311f),
			float2(0.3874443f, 0.7644074f),
			float2(0.4094146f, 0.151852f),
			float2(0.3779792f, 0.4699225f),
			float2(0.9367768f, 0.2930911f),
			float2(0.1184676f, 0.5660473f),
			float2(0.5350589f, -0.1797861f),
			float2(0.05063209f, 0.2463228f),
			float2(0.6854041f, 0.1558142f),
			float2(0.8824921f, -0.3403803f),
			float2(0.2487828f, -0.1240097f),
			float2(0.1110238f, 0.9691482f),
			float2(0.6206494f, -0.6748185f),
			float2(0.3984736f, -0.4907326f),
			float2(0.9640564f, -0.02796953f),
			float2(-0.394538f, 0.2868877f),
			float2(-0.1605287f, -0.001273256f),
			float2(-0.1351251f, 0.4460111f),
			float2(-0.1649308f, 0.9423735f),
			float2(-0.34411f, 0.7086557f),
			float2(0.07306198f, -0.372647f),
			float2(-0.6624553f, 0.4340924f),
			float2(0.01711876f, -0.6439707f),
			float2(0.3432294f, -0.7902341f),
			float2(-0.431942f, -0.01264048f),
			float2(0.0554834f, -0.9162955f),
			float2(-0.7119419f, 0.1407981f),
			float2(-0.2572051f, -0.709406f),
			float2(-0.3534312f, -0.442526f),
			float2(-0.7836586f, -0.1292122f),
			float2(-0.5489578f, -0.7478135f),
			float2(-0.6645309f, -0.4536549f)
		};
	};

	float4 GetReflection(float2 baseUv, float2 uv, float blur) {
		float4 reflection = (float4)0;

		for (float i = 0; i < 32; i++) {
			float2 uvOffset = poissonDisk[i] * blur;
			reflection += float4(gDiffuseMap.SampleLevel(samLinear, uv + uvOffset, 0).rgb, gFirstStepMap.SampleLevel(samLinear, baseUv + uvOffset, 0).a);
		}

		float4 result = reflection / 32;
		result.a = pow(max(result.a, 0), 1.5);

		return result;
	}

	float4 FinalStepFn(float2 coords, SamplerState s) {
		float4 firstStep = gFirstStepMap.SampleLevel(s, coords, 0);
		float2 reflectedUv = coords + firstStep.xy;

		float3 diffuseColor = gDiffuseMap.SampleLevel(s, coords, 0).rgb;
		float specularExp = gNormalMap.Sample(s, coords).a;

		float4 baseReflection = gBaseReflectionMap.SampleLevel(s, coords, 0);
		float blur = saturate(firstStep.b / specularExp / 5.0 - 0.01);

		float4 reflection;
		[branch]
		if (blur < 0.001) {
			reflection = float4(gDiffuseMap.SampleLevel(s, reflectedUv, 0).rgb, gFirstStepMap.SampleLevel(s, coords, 0).a);
		} else {
			reflection = GetReflection(coords, reflectedUv, blur * 0.5);
		}

		float a = reflection.a * baseReflection.a;
		return float4(diffuseColor + (reflection.rgb - baseReflection.rgb) * a, 1.0);
	}

	float4 ps_FinalStep(PS_IN pin) : SV_Target {
		return FinalStepFn(pin.Tex, samLinear);
	}

    technique11 FinalStep {
        pass P0 {
            SetVertexShader( CompileShader( vs_5_0, vs_main() ) );
            SetGeometryShader( NULL );
            SetPixelShader( CompileShader( ps_5_0, ps_FinalStep() ) );
        }
    }