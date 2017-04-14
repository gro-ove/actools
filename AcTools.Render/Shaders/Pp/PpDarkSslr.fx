// textures
    Texture2D gDiffuseMap;
	Texture2D gDepthMap;
    Texture2D gBaseReflectionMap;
    Texture2D gNormalMap;
    Texture2D gNoiseMap;
	Texture2D gFirstStepMap;

	SamplerState samLinear {
		Filter = MIN_MAG_MIP_LINEAR;
		AddressU = CLAMP;
		AddressV = CLAMP;
	};

	SamplerState samRandom {
		Filter = MIN_MAG_MIP_POINT;
		AddressU = Wrap;
		AddressV = Wrap;
	};
    
// input resources
    cbuffer cbPerFrame : register(b0) {
		matrix gCameraProjInv;
		matrix gCameraProj;

        matrix gWorldViewProjInv;
        matrix gWorldViewProj;
        float3 gEyePosW;

		float4 gScreenSize;

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

	float3 DecodeNm(float2 enc){
		float2 fenc = enc * 4 - 2;
		float f = dot(fenc, fenc);
		float g = sqrt(1 - f / 4);
		float3 n;
		n.xy = fenc*g;
		n.z = 1 - f / 2;
		return n;
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

		/*[unroll]
		for (int i = 0; i < 35; i++) {
			calculatedPosition = position + reflectDir * L;

			newUv = GetUv(calculatedPosition);
			newPosition = GetPosition(newUv.xy, GetDepth(newUv.xy, s));

			float newL = length(position - newPosition);
			L = L + 0.01 * saturate(abs(newL - L) * 50 - 0.2);
		}*/

		[unroll]
		for (int j = 0; j < ITERATIONS; j++) {
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
		return float4(newUv.xy - coords, saturate(2.0 * L / quality), alpha);
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
		float2 poissonDisk[25] = {
			float2(-0.5496699f, -0.3607742f),
			float2(-0.93247f, -0.2627924f),
			float2(-0.428109f, 0.2621388f),
			float2(-0.3889751f, -0.7699139f),
			float2(-0.007669114f, -0.4256215f),
			float2(-0.2229971f, -0.03531943f),
			float2(0.49135f, -0.05647383f),
			float2(0.3557799f, -0.4577287f),
			float2(0.02084914f, 0.1899813f),
			float2(0.1282342f, -0.7269787f),
			float2(-0.1825718f, 0.4983515f),
			float2(-0.8501378f, 0.08436206f),
			float2(-0.2846428f, 0.8403723f),
			float2(-0.8721611f, 0.4181926f),
			float2(-0.6079646f, 0.6299406f),
			float2(0.2123813f, 0.6794029f),
			float2(0.4791782f, 0.4958593f),
			float2(0.667411f, 0.2290769f),
			float2(0.8077992f, 0.5232179f),
			float2(0.8575023f, -0.29065f),
			float2(0.9838881f, 0.08113442f),
			float2(0.4223681f, -0.8714673f),
			float2(-0.1068291f, -0.9763235f),
			float2(-0.7130113f, -0.6807091f),
			float2(0.4687168f, 0.8776741f)
		};
	};

	float4 GetReflection(float2 baseUv, float2 uv, float blur) {
		float4 reflection = (float4)0;
		float2 random = normalize(gNoiseMap.SampleLevel(samRandom, uv * 1000.0, 0).xy);

		for (float i = 0; i < 25; i++) {
			float2 uvOffset = reflect(poissonDisk[i], random) * blur;// *gScreenSize.zw;

			float3 reflectedColor = min(gDiffuseMap.SampleLevel(samLinear, uv + uvOffset, 0).rgb, 2.0);
			float reflectedQuality = gFirstStepMap.SampleLevel(samLinear, baseUv + uvOffset, 0).a;

			if (reflectedQuality > 0.02) {
				reflection += float4(reflectedColor, reflectedQuality);
			}
		}

		// return reflection / max(reflection.a, 1.0);
		float4 result = reflection / 25;
		// result.a = pow(max(result.a, 0), 1.2);
		return result;
	}

	float4 FinalStepFn(float2 coords, SamplerState s) {
		float4 firstStep = gFirstStepMap.SampleLevel(s, coords, 0);
		float2 reflectedUv = coords + firstStep.xy;

		float3 diffuseColor = gDiffuseMap.SampleLevel(s, coords, 0).rgb;
		float specularExp = gNormalMap.Sample(s, coords).a;

		float4 baseReflection = gBaseReflectionMap.SampleLevel(s, coords, 0);
		float blur = saturate(firstStep.b / specularExp - 0.1) * 0.1;

		// return gDiffuseMap.SampleLevel(s, reflectedUv, 0) * gFirstStepMap.SampleLevel(s, coords, 0).a;

		float4 reflection;
		[branch]
		if (blur < 0.001 || baseReflection.a < 0.01) {
			reflection = float4(gDiffuseMap.SampleLevel(s, reflectedUv, 0).rgb, gFirstStepMap.SampleLevel(s, coords, 0).a);
		} else {
			reflection = GetReflection(coords, reflectedUv, blur);
		}

		// baseReflection.a = 1.0; // BUG

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