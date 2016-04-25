#include "Deferred.fx"

// structs
	cbuffer cbPerObject : register(b0) {
		matrix gWorld;
		matrix gWorldInvTranspose;
		matrix gWorldViewProj;
	}

	cbuffer cbPerFrame {
		float3 gSkyDown;
		float3 gSkyRange;
	}

// sky
	struct Sky_VS_IN {
		float3 PosL       : POSITION;
	};

	struct Sky_PS_IN {
		float4 PosH       : SV_POSITION;
		float3 PosL       : POSITION;
	};

	float3 CalcSky(float3 normal) {
		float up = normal.y * 0.5 + 0.5;
		return gSkyDown + up * gSkyRange;
		return up;
		return saturate(((up * 100) % 2 - 1.0) * 1e6);
	}

	Sky_PS_IN vs_Sky(Sky_VS_IN vin) {
		Sky_PS_IN vout;
		vout.PosH = mul(float4(vin.PosL, 1.0f), gWorldViewProj);
		vout.PosL = vin.PosL;
		return vout;
	}

	PS_OUT ps_SkyDeferred(Sky_PS_IN pin) : SV_Target {
		float3 normal = normalize(pin.PosL);

		PS_OUT pout;
		pout.Base = float4(CalcSky(normal), 1.0);
		pout.Normal = float4(normal, 1.0);
		pout.Maps = 0;
		return pout;
	}

	technique11 SkyDeferred {
		pass P0 {
			SetVertexShader( CompileShader( vs_4_0, vs_Sky() ) );
			SetGeometryShader( NULL );
			SetPixelShader( CompileShader( ps_4_0, ps_SkyDeferred() ) );
		}
	}

	float4 ps_SkyForward(Sky_PS_IN pin) : SV_Target {
		float3 normal = normalize(pin.PosL);
		return float4(CalcSky(normal), 1.0);
	}

	technique11 SkyForward {
		pass P0 {
			SetVertexShader( CompileShader( vs_4_0, vs_Sky() ) );
			SetGeometryShader( NULL );
			SetPixelShader( CompileShader( ps_4_0, ps_SkyForward() ) );
		}
	}