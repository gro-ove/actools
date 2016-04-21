// textures
	Texture2D gBaseMap;
	Texture2D gNormalMap;
	Texture2D gMapsMap;
	Texture2D gDepthMap;

	SamplerState samInputImage {
		Filter = MIN_MAG_LINEAR_MIP_POINT;
		AddressU = CLAMP;
		AddressV = CLAMP;
	};

// input resources
	cbuffer cbPerObject : register(b0) {
		matrix gWorldViewProjInv;
		float3 gLightColor;
		float3 gPointLightPosition;
		float gPointLightRadius;
	}

	cbuffer cbPerFrame {
		float3 gEyePosW;
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

// point light
	float3 GetPosition(float2 uv, float depth){
		float4 position = mul(float4(uv.x * 2 - 1, -(uv.y * 2 - 1), depth, 1), gWorldViewProjInv);
		return position.xyz / position.w;
	}

	float4 ps_PointLight(PS_IN pin) : SV_Target {
		float4 baseValue = gBaseMap.SampleLevel(samInputImage, pin.Tex, 0.0);
		float4 mapsValue = gMapsMap.SampleLevel(samInputImage, pin.Tex, 0.0);

		float4 normalValue = gNormalMap.SampleLevel(samInputImage, pin.Tex, 0.0);
		float3 normal = normalValue.xyz;
		
		float depth = gDepthMap.SampleLevel(samInputImage, pin.Tex, 0.0).x;
		float3 position = GetPosition(pin.Tex, depth);

		float3 lightVector = gPointLightPosition - position;
		float3 toLight = normalize(lightVector);
		float distance = dot(lightVector, lightVector);

		float lightness = saturate(dot(normal, toLight)) * saturate(1 - distance / gPointLightRadius);

		float3 toEye = normalize(gEyePosW - position);
		float3 halfway = normalize(toEye + toLight);

		float3 color = baseValue.rgb;
		float diffuseValue = baseValue.a;
		float3 lightnessResult = color * diffuseValue * gLightColor;
			
		float specIntensity = mapsValue.r * 1.2;
		float specExp = mapsValue.g * 250 + 1;

		float nDotH = saturate(dot(halfway, normal));
		float specularLightness = pow(nDotH, specExp) * specIntensity;

		[flatten]
		if (specExp > 30){
			specularLightness += pow(nDotH, specExp * 10 + 5000) * (specIntensity * 12 + 5) * saturate((specExp - 30) / 40);
		}

		lightnessResult += specularLightness * gLightColor;
		return float4(lightnessResult, lightness + specularLightness);
	}

	technique11 PointLight {
		pass P0 {
			SetVertexShader( CompileShader( vs_4_0, vs_main() ) );
			SetGeometryShader( NULL );
			SetPixelShader( CompileShader( ps_4_0, ps_PointLight() ) );
		}
	}