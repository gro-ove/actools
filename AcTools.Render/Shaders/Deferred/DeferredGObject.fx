#include "Deferred.fx"

// structs
	static const dword HAS_NORMAL_MAP = 1;
	static const dword HAS_DETAILS_MAP = 2;
	static const dword HAS_DETAILS_NORMAL_MAP = 4;
	static const dword HAS_MAPS = 8;
	static const dword USE_DIFFUSE_ALPHA_AS_MAP = 16;
	static const dword ALPHA_BLEND = 32;
	static const dword IS_ADDITIVE = 64;

	struct Material {
		float Ambient;
		float Diffuse;
		float Specular;
		float SpecularExp;

		float FresnelC;
		float FresnelExp;
		float FresnelMaxLevel;
		float DetailsUvMultipler;

		float3 Emissive;
		float DetailsNormalBlend;

		dword Flags;
		float3 _padding;
	};

// textures
	Texture2D gDiffuseMap;
	Texture2D gNormalMap;
	Texture2D gMapsMap;
	Texture2D gDetailsMap;
	Texture2D gDetailsNormalMap;
	TextureCube gReflectionCubemap;

// input resources
	cbuffer cbPerObject : register(b0) {
		matrix gWorld;
		matrix gWorldInvTranspose;
		matrix gWorldViewProj;
		Material gMaterial;
	}

	cbuffer cbPerFrame {
		float3 gEyePosW;
		float3 gAmbientDown;
		float3 gAmbientRange;

		float3 gLightColor;
		float3 gDirectionalLightDirection;
	}

// functions
	float3 CalcAmbient(float3 normal, float3 color) {
		float up = normal.y * 0.5 + 0.5;
		float3 ambient = gAmbientDown + up * gAmbientRange;
		return ambient * color;
	}

// standart
	PS_IN vs_Standard(VS_IN vin) {
		PS_IN vout;

		vout.PosW = mul(float4(vin.PosL, 1.0f), gWorld).xyz;
		vout.NormalW = mul(vin.NormalL, (float3x3)gWorldInvTranspose);
		vout.TangentW = mul(vin.TangentL, (float3x3)gWorldInvTranspose);
		vout.BitangentW = mul(cross(vin.NormalL, vin.TangentL), (float3x3)gWorldInvTranspose);

		vout.PosH = mul(float4(vin.PosL, 1.0f), gWorldViewProj);
		vout.Tex = vin.Tex;

		return vout;
	}

	PosOnly_PS_IN vs_PosOnly(VS_IN vin) {
		PosOnly_PS_IN vout;
		vout.PosH = mul(float4(vin.PosL, 1.0), gWorldViewProj);
		return vout;
	}

	float3 NormalSampleToWorldSpace(float3 normalMapSample, float3 N, float3 T, float3 B){
		return mul(2.0 * normalMapSample - 1.0, float3x3(T, B, N));
	}

	float CalculateAlpha(float value) {
		return min(value + 1.0 - (gMaterial.Flags & ALPHA_BLEND) / ALPHA_BLEND, 1.0);
	}

	PS_OUT ps_StandardDeferred(PS_IN pin) : SV_Target {
		float4 diffuseValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex);

		PS_OUT pout;
		if ((gMaterial.Flags & HAS_NORMAL_MAP) == HAS_NORMAL_MAP){
			float4 normalValue = gNormalMap.Sample(samAnisotropic, pin.Tex);
		
			if ((gMaterial.Flags & HAS_DETAILS_NORMAL_MAP) == HAS_DETAILS_NORMAL_MAP){
				float4 detailsNormalValue = gDetailsNormalMap.Sample(samAnisotropic, pin.Tex * gMaterial.DetailsUvMultipler);
				normalValue += (detailsNormalValue - 0.5) * gMaterial.DetailsNormalBlend * (1.0 - diffuseValue.a);
			}

			pout.Normal = float4(
				normalize(NormalSampleToWorldSpace(normalValue.xyz, pin.NormalW, pin.TangentW, pin.BitangentW)),
				CalculateAlpha(normalValue.a * diffuseValue.a));
		} else {
			pout.Normal = float4(normalize(pin.NormalW), CalculateAlpha(diffuseValue.a));
		}

		float specular = saturate(gMaterial.Specular / 2.5);
		float glossiness = saturate((gMaterial.SpecularExp - 1) / 250);
		float reflectiveness = saturate(gMaterial.FresnelMaxLevel);
		float metalness = saturate(max(
				gMaterial.FresnelC / gMaterial.FresnelMaxLevel, 
				1.1 / (gMaterial.FresnelExp + 1) - 0.1) - (gMaterial.Flags & IS_ADDITIVE));
		
		if ((gMaterial.Flags & HAS_MAPS) == HAS_MAPS){
			float4 mapsValue = gMapsMap.Sample(samAnisotropic, pin.Tex);
			specular *= mapsValue.r;
			glossiness *= mapsValue.g;
			reflectiveness *= mapsValue.b;
		}
		
		if ((gMaterial.Flags & USE_DIFFUSE_ALPHA_AS_MAP) == USE_DIFFUSE_ALPHA_AS_MAP){
			specular *= diffuseValue.a;
			reflectiveness *= diffuseValue.a / 2 + 0.5;
		}
		
		// diffuse part
		if ((gMaterial.Flags & HAS_DETAILS_MAP) == HAS_DETAILS_MAP){
			float4 detailsValue = gDetailsMap.Sample(samAnisotropic, pin.Tex * gMaterial.DetailsUvMultipler);

			diffuseValue *= diffuseValue.a + detailsValue - detailsValue * diffuseValue.a;
			glossiness *= (1.0 - detailsValue.a * (1.0 - diffuseValue.a)) / 2 + 0.5;
		}
		
		float ambient = max(gMaterial.Ambient, 0.01);
		pout.Base = float4(CalcAmbient(pout.Normal.xyz, diffuseValue.rgb * ambient) + diffuseValue.rgb * gMaterial.Emissive, gMaterial.Diffuse / ambient);
		// diffuse part - end
		
		pout.Maps = float4(specular, glossiness, reflectiveness, metalness);
		return pout;
	}

	technique11 StandardDeferred {
		pass P0 {
			SetVertexShader( CompileShader( vs_4_0, vs_Standard() ) );
			SetGeometryShader( NULL );
			SetPixelShader( CompileShader( ps_5_0, ps_StandardDeferred() ) );
		}
	}

	float4 ps_StandardForward(PS_IN pin) : SV_Target {
		float4 diffuseValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex);
		float ambient = max(gMaterial.Ambient, 0.05);

		float3 normal;
		if ((gMaterial.Flags & HAS_NORMAL_MAP) == HAS_NORMAL_MAP) {
			float4 normalValue = gNormalMap.Sample(samAnisotropic, pin.Tex);
			normal = normalize(NormalSampleToWorldSpace(normalValue.xyz, pin.NormalW, pin.TangentW, pin.BitangentW));
		} else {
			normal = normalize(pin.NormalW);
		}

		float3 toLight = -gDirectionalLightDirection;
		float lightness = saturate(dot(normal, toLight));

		float3 lighted = CalcAmbient(normal, diffuseValue.rgb * ambient) + diffuseValue.rgb * gMaterial.Emissive +
				diffuseValue.rgb * gMaterial.Diffuse * gLightColor * lightness;

		return float4(lighted, 1.0);
	}

	technique10 StandardForward {
		pass P0 {
			SetVertexShader( CompileShader( vs_4_0, vs_Standard() ) );
			SetGeometryShader( NULL );
			SetPixelShader( CompileShader( ps_4_0, ps_StandardForward() ) );
		}
	}

// ambient shadow part
	struct AmbientShadow_VS_IN {
		float3 PosL       : POSITION;
		float2 Tex        : TEXCOORD;
	};

	struct AmbientShadow_PS_IN {
		float4 PosH       : SV_POSITION;
		float3 PosW       : POSITION;
		float2 Tex        : TEXCOORD;
	};

	AmbientShadow_PS_IN vs_AmbientShadowDeferred(AmbientShadow_VS_IN vin) {
		AmbientShadow_PS_IN vout;
		vout.PosW = mul(float4(vin.PosL, 1.0f), gWorld).xyz;
		vout.PosH = mul(float4(vin.PosL, 1.0f), gWorldViewProj);
		vout.Tex = vin.Tex;
		return vout;
	}

	PS_OUT ps_AmbientShadowDeferred(AmbientShadow_PS_IN pin) : SV_Target {
		PS_OUT pout;

		float4 diffuseValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex);
		float shadowLevel = diffuseValue.x;

		pout.Base = float4(0, 0, 0, shadowLevel);
		pout.Normal = 0;
		pout.Maps = float4(0, 0, 0, shadowLevel);

		return pout;
	}

	technique11 AmbientShadowDeferred {
		pass P0 {
			SetVertexShader( CompileShader( vs_4_0, vs_AmbientShadowDeferred() ) );
			SetGeometryShader( NULL );
			SetPixelShader( CompileShader( ps_4_0, ps_AmbientShadowDeferred() ) );
		}
	}

// transparent deferred part
	technique11 TransparentDeferred {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_Standard()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_StandardDeferred()));
		}
	}

// transparent part
	float4 ps_TransparentForward(PS_IN pin) : SV_Target{
		float4 diffuseValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex);

		float alpha = diffuseValue.a;
		float3 normal;

		if ((gMaterial.Flags & HAS_NORMAL_MAP) == HAS_NORMAL_MAP) {
			float4 normalValue = gNormalMap.Sample(samAnisotropic, pin.Tex);
			alpha *= normalValue.a;
			normal = normalize(NormalSampleToWorldSpace(normalValue.xyz, normalize(pin.NormalW), pin.TangentW, pin.BitangentW));
		} else {
			normal = normalize(pin.NormalW);
		}

		float3 color = CalcAmbient(normal, diffuseValue.rgb);

		float specular = saturate(gMaterial.Specular / 2.5);
		float glossiness = saturate((gMaterial.SpecularExp - 1) / 250);
		float reflectiveness = saturate(gMaterial.FresnelMaxLevel);
		float metalness = saturate(max(
				gMaterial.FresnelC / gMaterial.FresnelMaxLevel,
				1.1 / (gMaterial.FresnelExp + 1) - 0.1));

		float3 toEyeW = normalize(gEyePosW - pin.PosW);

		float rid = saturate(dot(toEyeW, pin.NormalW));
		float rim = pow(1 - rid, gMaterial.FresnelExp);

		float3 reflectionColor = gReflectionCubemap.SampleBias(samAnisotropic, reflect(-toEyeW, normal),
			1.0f - glossiness).rgb;

		return float4(color * (gMaterial.Ambient - reflectiveness / 2) + diffuseValue.rgb * gMaterial.Emissive * diffuseValue.a + 
				reflectionColor * reflectiveness * rim, alpha);
	}

	technique10 TransparentForward {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_Standard()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_TransparentForward()));
		}
	}

	technique11 TransparentMask {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_PosOnly()));
			SetGeometryShader(NULL);
			SetPixelShader(NULL);
		}
	}