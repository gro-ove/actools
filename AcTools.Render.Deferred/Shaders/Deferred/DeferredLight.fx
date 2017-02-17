// textures
    Texture2D gBaseMap;
    Texture2D gNormalMap;
    Texture2D gMapsMap;
    Texture2D gDepthMap;

	static const int NUM_SPLITS = 4;
	Texture2D gShadowMaps[NUM_SPLITS];

    SamplerState samInputImage {
        Filter = MIN_MAG_LINEAR_MIP_POINT;
        AddressU = CLAMP;
        AddressV = CLAMP;
    };

// input resources
    cbuffer cbPerObject : register(b0) {
        matrix gWorld;
        matrix gWorldInvTranspose;
        matrix gWorldViewProj;

        float4 gScreenSize;

        matrix gWorldViewProjInv;
        float3 gLightColor;

        float3 gDirectionalLightDirection;

        float3 gPointLightPosition;
        float gPointLightRadius;

		float4 gShadowDepths;
		matrix gShadowViewProj[NUM_SPLITS];
    }

    cbuffer cbPerFrame {
        float3 gEyePosW;
    }

// fn structs
    struct VS_IN {
        float3 PosL    : POSITION;
        float2 Tex     : TEXCOORD;
    };

    struct VS_REPL_IN {
        float3 PosL       : POSITION;
        float3 NormalL    : NORMAL;
        float2 Tex        : TEXCOORD;
        float3 TangentL   : TANGENT;
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

	struct PosOnly_PS_IN {
		float4 PosH : SV_POSITION;
	};

	PosOnly_PS_IN vs_PosOnly(VS_IN vin) {
		PosOnly_PS_IN vout;
		vout.PosH = mul(float4(vin.PosL, 1.0), gWorldViewProj);
		return vout;
	}

// functions
    float3 GetPosition(float2 uv, float depth) {
        float4 position = mul(float4(uv.x * 2 - 1, -(uv.y * 2 - 1), depth, 1), gWorldViewProjInv);
        return position.xyz / position.w;
    }

	void GetParams(float2 uv,
		out float3 normal, out float3 position, out float depth,
		out float3 lightResult,
		out float specIntensity, out float specExp) {

		normal = gNormalMap.Sample(samInputImage, uv).xyz;
		depth = gDepthMap.Sample(samInputImage, uv).x;
		position = GetPosition(uv, depth);

		float4 baseValue = gBaseMap.Sample(samInputImage, uv);
		lightResult = baseValue.rgb * baseValue.a * gLightColor;

		float4 mapsValue = gMapsMap.Sample(samInputImage, uv);
		specIntensity = mapsValue.r * 1.2;
		specExp = mapsValue.g * 250 + 1;
	}

// point light
    float4 ps_PointLight_inner(float2 tex) {
		float3 normal, position, lightResult;
		float depth, specIntensity, specExp;
		GetParams(tex, normal, position, depth, lightResult, specIntensity, specExp);

		float3 lightVector = gPointLightPosition - position;
		float3 toLight = normalize(lightVector);

		float distance = saturate(1 - dot(lightVector, lightVector) / gPointLightRadius);
		float lightness = saturate(dot(normal, toLight));

		float3 toEye = normalize(gEyePosW - position);
		float3 halfway = normalize(toEye + toLight);

		float nDotH = saturate(dot(halfway, normal));
		float specularLightness = pow(nDotH, specExp) * specIntensity;

		if (specExp > 30) {
			specularLightness += pow(nDotH, specExp * 10 + 15000) * (specIntensity * 12 + 5) * saturate((specExp - 30) / 40);
		}

		lightResult += specularLightness * gLightColor;
		return float4(lightResult, (lightness + specularLightness) * distance);
	}

	float4 ps_PointLight_inner_NoSpec(float2 tex) {
		float3 normal, position, lightResult;
		float depth, specIntensity, specExp;
		GetParams(tex, normal, position, depth, lightResult, specIntensity, specExp);

		float3 lightVector = gPointLightPosition - position;
		float3 toLight = normalize(lightVector);

		float distance = saturate(1 - dot(lightVector, lightVector) / gPointLightRadius);
		float lightness = saturate(dot(normal, toLight));

		return float4(lightResult, lightness * distance);
	}

	float4 ps_PointLight(PS_IN pin) : SV_Target {
		return ps_PointLight_inner(pin.Tex);
	}

	float4 ps_PointLight_PosOnly(PosOnly_PS_IN pin) : SV_Target {
		return ps_PointLight_inner(pin.PosH.xy / gScreenSize.xy);
	}

	float4 ps_PointLight_PosOnly_NoSpec(PosOnly_PS_IN pin) : SV_Target {
		return ps_PointLight_inner_NoSpec(pin.PosH.xy / gScreenSize.xy);
	}

    technique11 PointLight {
        pass P0 {
            SetVertexShader(CompileShader(vs_4_0, vs_PosOnly()));
            SetGeometryShader(NULL);
            SetPixelShader(CompileShader(ps_4_0, ps_PointLight_PosOnly()));
        }
    }

    technique11 PointLight_NoSpec {
        pass P0 {
            SetVertexShader(CompileShader(vs_4_0, vs_PosOnly()));
            SetGeometryShader(NULL);
            SetPixelShader(CompileShader(ps_4_0, ps_PointLight_PosOnly_NoSpec()));
        }
    }

	float4 ps_PointLight_PosOnly_Debug(PosOnly_PS_IN pin) : SV_Target{
		return float4(gLightColor, 1.0);
	}

    technique11 PointLight_Debug {
        pass P0 {
            SetVertexShader(CompileShader(vs_4_0, vs_PosOnly()));
            SetGeometryShader(NULL);
            SetPixelShader(CompileShader(ps_4_0, ps_PointLight_PosOnly_Debug()));
        }
    }

// directional light
	SamplerComparisonState samShadow {
		Filter = COMPARISON_MIN_MAG_MIP_LINEAR;
		AddressU = BORDER;
		AddressV = BORDER;
		AddressW = BORDER;
		BorderColor = float4(1.0f, 1.0f, 1.0f, 0.0f);

		ComparisonFunc = LESS;
	};

	SamplerState samShadowSpecial {
		Filter = MIN_MAG_MIP_LINEAR;
		AddressU = BORDER;
		AddressV = BORDER;
		AddressW = BORDER;
		BorderColor = float4(1.0f, 1.0f, 1.0f, 0.0f);
	};

	static const float SMAP_SIZE = 2048.0f;
	static const float SMAP_DX = 1.0f / 2048.0f;

	/*float GetShadowInner2(float3 position, Texture2D tex, matrix viewProj) {
		float4 uv = mul(float4(position, 1.0f), viewProj);
		uv.xyz /= uv.w;

		float shadow = 0.0, x, y;
		for (y = -1.5; y <= 1.5; y += 1.0)
			for (x = -1.5; x <= 1.5; x += 1.0)
				shadow += tex.SampleCmpLevelZero(samShadow, uv.xy + float2(x, y) * SMAP_DX, uv.z).r;
		return shadow / 16.0;
	}

	float GetShadow2(float3 position, float depth) {
		float shadow0, shadow1, edge;
		if (depth > gShadowDepths.w) {
			return 1;
		} else if (depth > gShadowDepths.w) {
			shadow0 = GetShadowInner(position, gShadowMaps[3], gShadowViewProj[3]);
			shadow1 = 1;
			edge = gShadowDepths.w;
		} else if (depth > gShadowDepths.y) {
			shadow0 = GetShadowInner(position, gShadowMaps[2], gShadowViewProj[2]);
			shadow1 = GetShadowInner(position, gShadowMaps[3], gShadowViewProj[3]);
			edge = gShadowDepths.z;
		} else if (depth > gShadowDepths.x) {
			shadow0 = GetShadowInner(position, gShadowMaps[1], gShadowViewProj[1]);
			shadow1 = GetShadowInner(position, gShadowMaps[2], gShadowViewProj[2]);
			edge = gShadowDepths.y;
		} else {
			shadow0 = GetShadowInner(position, gShadowMaps[0], gShadowViewProj[0]);
			shadow1 = GetShadowInner(position, gShadowMaps[1], gShadowViewProj[1]);
			edge = gShadowDepths.x;
		}

		float k = saturate((depth / edge - 0.995) * 200.0);
		float shadow = shadow0 * (1 - k) + shadow1 * k;

		shadow = saturate((shadow - 0.5) * 8 + 0.5);
		return shadow;
	}

	float GetShadowInner_NoFilter(float3 position, Texture2D tex, matrix viewProj) {
		float4 uv = mul(float4(position, 1.0f), viewProj);
		uv.xyz /= uv.w;
		return tex.SampleCmpLevelZero(samShadow, uv.xy, uv.z).r;
	}

	float GetShadow_NoFilter2(float3 position, float depth) {
		if (depth > gShadowDepths.w) {
			return 1;
		} else if (depth > gShadowDepths.w) {
			return GetShadowInner_NoFilter(position, gShadowMaps[3], gShadowViewProj[3]);
		} else if (depth > gShadowDepths.y) {
			return GetShadowInner_NoFilter(position, gShadowMaps[2], gShadowViewProj[2]);
		} else if (depth > gShadowDepths.x) {
			return GetShadowInner_NoFilter(position, gShadowMaps[1], gShadowViewProj[1]);
		} else {
			return GetShadowInner_NoFilter(position, gShadowMaps[0], gShadowViewProj[0]);
		}
	}*/

	float GetShadowInner(Texture2D tex, float3 uv) {
		float shadow = 0.0, x, y;
		for (y = -1.5; y <= 1.5; y += 1.0)
			for (x = -1.5; x <= 1.5; x += 1.0)
				shadow += tex.SampleCmpLevelZero(samShadow, uv.xy + float2(x, y) * SMAP_DX, uv.z).r;
		// return shadow / 16.0;
		return saturate((shadow / 16 - 0.5) * 4 + 0.5);
	}

	#define SHADOW_A 0.0001
	#define SHADOW_Z 0.9999

	float GetShadow(float3 position, float depth) {
		float4 pos = float4(position, 1.0), uv, nv;

		uv = mul(pos, gShadowViewProj[3]);
		uv.xyz /= uv.w;
		if (uv.x < SHADOW_A || uv.x > SHADOW_Z || uv.y < SHADOW_A || uv.y > SHADOW_Z)
			return 1;

		nv = mul(pos, gShadowViewProj[2]);
		nv.xyz /= nv.w;
		if (nv.x < SHADOW_A || nv.x > SHADOW_Z || nv.y < SHADOW_A || nv.y > SHADOW_Z)
			return GetShadowInner(gShadowMaps[3], uv);
		uv = nv;

		nv = mul(pos, gShadowViewProj[1]);
		nv.xyz /= nv.w;
		if (nv.x < SHADOW_A || nv.x > SHADOW_Z || nv.y < SHADOW_A || nv.y > SHADOW_Z)
			return GetShadowInner(gShadowMaps[2], uv);
		uv = nv;

		nv = mul(pos, gShadowViewProj[0]);
		nv.xyz /= nv.w;
		if (nv.x < SHADOW_A || nv.x > SHADOW_Z || nv.y < SHADOW_A || nv.y > SHADOW_Z)
			return GetShadowInner(gShadowMaps[1], uv);

		return GetShadowInner(gShadowMaps[0], nv);
	}

	float GetShadow_NoFilter(float3 position, float depth) {
		float4 pos = float4(position, 1.0), uv, nv;

		uv = mul(pos, gShadowViewProj[3]);
		uv.xyz /= uv.w;
		if (uv.x < SHADOW_A || uv.x > SHADOW_Z || uv.y < SHADOW_A || uv.y > SHADOW_Z)
			return 1;

		nv = mul(pos, gShadowViewProj[2]);
		nv.xyz /= nv.w;
		if (nv.x < SHADOW_A || nv.x > SHADOW_Z || nv.y < SHADOW_A || nv.y > SHADOW_Z)
			return gShadowMaps[3].SampleCmpLevelZero(samShadow, uv.xy, uv.z).r;
		uv = nv;

		nv = mul(pos, gShadowViewProj[1]);
		nv.xyz /= nv.w;
		if (nv.x < SHADOW_A || nv.x > SHADOW_Z || nv.y < SHADOW_A || nv.y > SHADOW_Z)
			return gShadowMaps[2].SampleCmpLevelZero(samShadow, uv.xy, uv.z).r;
		uv = nv;

		nv = mul(pos, gShadowViewProj[0]);
		nv.xyz /= nv.w;
		if (nv.x < SHADOW_A || nv.x > SHADOW_Z || nv.y < SHADOW_A || nv.y > SHADOW_Z)
			return gShadowMaps[1].SampleCmpLevelZero(samShadow, uv.xy, uv.z).r;
		
		return gShadowMaps[0].SampleCmpLevelZero(samShadow, nv.xy, nv.z).r;
	}

	float4 ps_DirectionalLight(PS_IN pin) : SV_Target {
        float3 normal, position, lightResult;
        float depth, specIntensity, specExp;
        GetParams(pin.Tex, normal, position, depth, lightResult, specIntensity, specExp);

		float3 toLight = -gDirectionalLightDirection;
		float lightness = saturate(dot(normal, toLight));

		float3 toEye = normalize(gEyePosW - position);
		float3 halfway = normalize(toEye + toLight);

		float nDotH = saturate(dot(halfway, normal));
		float specularLightness = pow(nDotH, specExp) * specIntensity;

		[flatten]
		if (specExp > 30) {
			specularLightness += pow(nDotH, specExp * 10 + 5000) * (specIntensity * 12 + 5) * saturate((specExp - 30) / 40);
		}

		lightResult += specularLightness * gLightColor;
		return float4(lightResult, lightness + specularLightness);
	}

	float4 ps_DirectionalLight_Shadows(PS_IN pin) : SV_Target {
        float3 normal, position, lightResult;
        float depth, specIntensity, specExp;
        GetParams(pin.Tex, normal, position, depth, lightResult, specIntensity, specExp);

		float3 toLight = -gDirectionalLightDirection;
		float lightness = saturate(dot(normal, toLight));
		float shadow = GetShadow(position, depth);

		float3 toEye = normalize(gEyePosW - position);
		float3 halfway = normalize(toEye + toLight);

		float nDotH = saturate(dot(halfway, normal));
		float specularLightness = pow(nDotH, specExp) * specIntensity;

		[flatten]
		if (specExp > 30) {
			specularLightness += pow(nDotH, specExp * 10 + 5000) * (specIntensity * 12 + 5) * saturate((specExp - 30) / 40);
		}

		lightResult += specularLightness * gLightColor;
		return float4(lightResult, (lightness + specularLightness) * shadow);
	}

	float4 ps_DirectionalLight_Shadows_NoFilter(PS_IN pin) : SV_Target {
        float3 normal, position, lightResult;
        float depth, specIntensity, specExp;
        GetParams(pin.Tex, normal, position, depth, lightResult, specIntensity, specExp);

		float3 toLight = -gDirectionalLightDirection;
		float lightness = saturate(dot(normal, toLight));
		float shadow = GetShadow_NoFilter(position, depth);

		float3 toEye = normalize(gEyePosW - position);
		float3 halfway = normalize(toEye + toLight);

		float nDotH = saturate(dot(halfway, normal));
		float specularLightness = pow(nDotH, specExp) * specIntensity;

		[flatten]
		if (specExp > 30) {
			specularLightness += pow(nDotH, specExp * 10 + 5000) * (specIntensity * 12 + 5) * saturate((specExp - 30) / 40);
		}

		lightResult += specularLightness * gLightColor;
		return float4(lightResult, (lightness + specularLightness) * shadow);
	}

	float4 ps_DirectionalLight_Split(PS_IN pin) : SV_Target{
		float3 normal, position, lightResult;
		float depth, specIntensity, specExp;
		GetParams(pin.Tex, normal, position, depth, lightResult, specIntensity, specExp);

		float3 toLight = -gDirectionalLightDirection;
		float lightness = saturate(dot(normal, toLight));

		float spec = abs(depth - gShadowDepths.x) < 0.0005;
		spec += abs(depth - gShadowDepths.y) < 0.0001;
		spec += abs(depth - gShadowDepths.z) < 0.00002;
		spec += abs(depth - gShadowDepths.w) < 0.000003;

		if (depth > gShadowDepths.w) {
			lightResult += float3(0.4, 0, 0.4);
		} else if (depth > gShadowDepths.z) {
			lightResult += float3(0.4, 0.4, 0);
		} else if (depth > gShadowDepths.y) {
			lightResult += float3(0, 0, 0.2);
		} else if (depth > gShadowDepths.x) {
			lightResult += float3(0.2, 0, 0);
		} else {
			lightResult += float3(0, 0.2, 0);
		}

		lightResult.r += spec;
		lightResult.g -= spec;
		lightResult.b -= spec;

		return float4(lightResult, lightness);
	}

    technique11 DirectionalLight {
        pass P0 {
            SetVertexShader(CompileShader(vs_4_0, vs_main()));
            SetGeometryShader(NULL);
            SetPixelShader(CompileShader(ps_4_0, ps_DirectionalLight()));
        }
    }

    technique11 DirectionalLight_Shadows {
        pass P0 {
            SetVertexShader(CompileShader(vs_4_0, vs_main()));
            SetGeometryShader(NULL);
            SetPixelShader(CompileShader(ps_4_0, ps_DirectionalLight_Shadows()));
        }
    }

    technique11 DirectionalLight_Shadows_NoFilter {
        pass P0 {
            SetVertexShader(CompileShader(vs_4_0, vs_main()));
            SetGeometryShader(NULL);
            SetPixelShader(CompileShader(ps_4_0, ps_DirectionalLight_Shadows_NoFilter()));
        }
    }

    technique11 DirectionalLight_Split {
        pass P0 {
            SetVertexShader(CompileShader(vs_4_0, vs_main()));
            SetGeometryShader(NULL);
            SetPixelShader(CompileShader(ps_4_0, ps_DirectionalLight_Split()));
        }
    }