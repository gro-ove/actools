struct Material {
    float4 Ambient;
    float4 Diffuse;
    float4 Specular; // .w = SpecPower
    float4 Fresnel; // .w = FresnelPower
    float FresnelMax;
	float MinAlpha;
	float UseDetail;
	float UseNormal;
	float UseMap;
	float DetailUVMultiplier;
};

struct DirectionalLight {
    float4 Ambient;
    float4 Diffuse;
    float4 Specular;
    float3 Direction;
    float pad;
};

Texture2D gDiffuseMap;
Texture2D gDetailsMap;
Texture2D gNormalMap;
Texture2D gMapMap;
TextureCube gCubeMap;

SamplerState samAnisotropic {
	Filter = ANISOTROPIC;
	MaxAnisotropy = 4;

	AddressU = WRAP;
	AddressV = WRAP;
};

void ComputeDirectionalLight(Material mat, DirectionalLight L, float specExpMult, float3 normal, float3 toEye, out float4 ambient, out float4 diffuse, out float4 spec){
    float3 lightVec = -L.Direction;
    ambient = L.Ambient * mat.Ambient;
    
    float diffuseFactor = dot(lightVec, normal) * 0.5 + 0.5;

    [flatten]
    if (diffuseFactor > 0.0f){
        float3 v = reflect(-lightVec, normal);
        float specFactor = pow(max(dot(v, toEye), 0.0f), mat.Specular.a * specExpMult);
                    
        diffuse = diffuseFactor * mat.Diffuse * L.Diffuse;
        spec = specFactor * mat.Specular * mat.Specular * L.Specular;
    } else {
		diffuse = float4(0.0f, 0.0f, 0.0f, 0.0f);
		spec = float4(0.0f, 0.0f, 0.0f, 0.0f);
	}
}

cbuffer cbPerObject : register(b0) {
	matrix gWorld;
	matrix gWorldInvTranspose;
	matrix gWorldViewProj;
	Material gMaterial;
}

cbuffer cbPerFrame {
    DirectionalLight gDirLight;
    float3 gEyePosW;
};

// CAR

struct VS_IN {
    float3 PosL    : POSITION;
    float3 NormalL : NORMAL;
	float2 Tex     : TEXCOORD;
};

struct PS_IN {
    float4 PosH    : SV_POSITION;
    float3 PosW    : POSITION;
    float3 NormalW : NORMAL;
	float2 Tex     : TEXCOORD;
};

PS_IN vs_main(VS_IN vin) {
	PS_IN vout;

    vout.PosW    = mul(float4(vin.PosL, 1.0f), gWorld).xyz;
    vout.NormalW = mul(vin.NormalL, (float3x3)gWorldInvTranspose);

	vout.PosH = mul(float4(vin.PosL, 1.0f), gWorldViewProj);
	vout.Tex = vin.Tex;

	return vout;
}

float3 NormalSampleToWorldSpace(float3 normalMapSample, float3 unitNormalW, float3 tangentW){
    // Uncompress each component from [0,1] to [-1,1].
    float3 normalT = 2.0f*normalMapSample - 1.0f;

    // Build orthonormal basis.
    float3 N = unitNormalW;
    float3 T = normalize(tangentW - dot(tangentW, N)*N);
    float3 B = cross(N, T);

    float3x3 TBN = float3x3(T, B, N);

    // Transform from tangent space to world space.
    float3 bumpedNormalW = mul(normalT, TBN);

    return bumpedNormalW;
}

float4 ps_main(PS_IN pin) : SV_Target{
	float alphaMult = 1.0;
	if (gMaterial.UseNormal == 1.0f){
		float4 nmv = gNormalMap.Sample(samAnisotropic, pin.Tex);
		pin.NormalW = NormalSampleToWorldSpace(nmv.xyz, pin.NormalW, 1.0f);
		alphaMult = nmv.w;
	} else {
		pin.NormalW = normalize(pin.NormalW);
	}

    float3 toEyeW = normalize(gEyePosW - pin.PosW);

    float4 texColor = gDiffuseMap.Sample(samAnisotropic, pin.Tex);
	[flatten]
	if (gMaterial.UseDetail == 1.0f){
		float4 details = gDetailsMap.Sample(samAnisotropic, pin.Tex * gMaterial.DetailUVMultiplier);
		texColor *= texColor.a + details * (1.0f - texColor.a);
	}
	
	float4 mapColor;
	[flatten]
	if (gMaterial.UseMap == 1.0f){
		mapColor = gMapMap.Sample(samAnisotropic, pin.Tex);
	} else {
		mapColor = 1.0f;
	}

    float4 ambient, diffuse, spec;
    ComputeDirectionalLight(gMaterial, gDirLight, mapColor.g, pin.NormalW, toEyeW, ambient, diffuse, spec);

    float4 litColor = texColor * (ambient + diffuse) + spec;
	float rid = saturate(dot(toEyeW, pin.NormalW));
    float rim = pow(1 - rid, gMaterial.Fresnel.w);

    float4 reflectionColor = gCubeMap.SampleBias(samAnisotropic, reflect(-toEyeW, pin.NormalW),
		1.0f / (0.1f + gMaterial.Specular.a)) * gDirLight.Diffuse;

	litColor.xyz += mapColor.b * reflectionColor.xyz * min(gMaterial.FresnelMax, rim + gMaterial.Fresnel.x);
    litColor.a = alphaMult * max(gMaterial.Diffuse.a * texColor.a + pow(rim, 1.6f) * 0.3f, gMaterial.MinAlpha);

	litColor.xyz *= (0.2f * mapColor.r) + 0.8f;

	return litColor;
}

technique11 Car { // PNT
    pass P0 {
        SetVertexShader( CompileShader( vs_4_0, vs_main() ) );
        SetGeometryShader( NULL );
        SetPixelShader( CompileShader( ps_4_0, ps_main() ) );
    }
}

// SIMPLE

struct VS_SIMPLE_IN {
    float3 PosL    : POSITION;
	float2 Tex     : TEXCOORD;
};

struct PS_SIMPLE_IN {
    float4 PosH    : SV_POSITION;
	float2 Tex     : TEXCOORD;
};

PS_SIMPLE_IN vs_simple(VS_SIMPLE_IN vin) {
	PS_SIMPLE_IN vout;

	vout.PosH = mul(float4(vin.PosL, 1.0f), gWorldViewProj);
	vout.Tex = vin.Tex;

	return vout;
}

float4 ps_simple(PS_SIMPLE_IN pin) : SV_Target {
    return gDiffuseMap.Sample(samAnisotropic, pin.Tex);
}

technique11 Simple { // PT
    pass P0 {
        SetVertexShader( CompileShader( vs_4_0, vs_simple() ) );
        SetGeometryShader( NULL );
        SetPixelShader( CompileShader( ps_4_0, ps_simple() ) );
    }
}

float4 ps_shadow(PS_SIMPLE_IN pin) : SV_Target {
    float4 texColor = gDiffuseMap.Sample(samAnisotropic, pin.Tex);
	texColor.a = 1.0f - texColor.r * 0.2f;
	return 1.0f - texColor;
}

technique11 Shadow {
    pass P0 {
        SetVertexShader( CompileShader( vs_4_0, vs_simple() ) );
        SetGeometryShader( NULL );
        SetPixelShader( CompileShader( ps_4_0, ps_shadow() ) );
    }
}