// structures
	struct Material {
		float Ambient;
		float Diffuse;
		float Specular;
		float SpecularExp;
		float3 Emissive;
	};

// textures
	Texture2D gDiffuseMap;

	SamplerState samAnisotropic {
		Filter = ANISOTROPIC;
		MaxAnisotropy = 4;

		AddressU = WRAP;
		AddressV = WRAP;
	};

// input resources
	cbuffer cbPerObject : register(b0) {
		matrix gWorld;
		matrix gWorldInvTranspose;
		matrix gWorldViewProj;
		Material gMaterial;
	}

	cbuffer cbPerFrame {
		// DirectionalLight gDirLight;
		float3 gEyePosW;
	}

// fn structs
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

// per pixel
	PS_IN vs_main(VS_IN vin) {
		PS_IN vout;

		vout.PosW    = mul(float4(vin.PosL, 1.0f), gWorld).xyz;
		vout.NormalW = mul(vin.NormalL, (float3x3)gWorldInvTranspose);

		vout.PosH = mul(float4(vin.PosL, 1.0f), gWorldViewProj);
		vout.Tex = vin.Tex;

		return vout;
	}

	float4 ps_main(PS_IN pin) : SV_Target{
		// return float4(gMaterial.Ambient, gMaterial.Diffuse, gMaterial.Specular, gMaterial.SpecularExp);
		float4 texColor = gDiffuseMap.Sample(samAnisotropic, pin.Tex);
		return texColor * gMaterial.Ambient + gMaterial.Diffuse * (dot(pin.NormalW, float3(0, -1, 0)) + 0.3) / 1.3;
	}

	technique11 PerPixel { // PNT
		pass P0 {
			SetVertexShader( CompileShader( vs_4_0, vs_main() ) );
			SetGeometryShader( NULL );
			SetPixelShader( CompileShader( ps_4_0, ps_main() ) );
		}
	}