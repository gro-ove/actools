#include "Deferred.fx"

// structs
	cbuffer cbPerObject : register(b0) {
		matrix gWorld;
		matrix gWorldInvTranspose;
		matrix gWorldViewProj;
	}

// kunos shader "gl"
	struct SpecialGl_PS_IN {
		float4 PosH       : SV_POSITION;
		float3 NormalW    : NORMAL;
		float3 PosL       : POSITION;
	};

	SpecialGl_PS_IN vs_SpecialGl(VS_IN vin) {
		SpecialGl_PS_IN vout;
		vout.PosH = mul(float4(vin.PosL, 1.0f), gWorldViewProj);
		vout.PosL = vin.PosL;
		vout.NormalW = mul(vin.NormalL, (float3x3)gWorldInvTranspose);
		return vout;
	}

	PS_OUT ps_SpecialGlDeferred(SpecialGl_PS_IN pin) : SV_Target {
		PS_OUT pout;
		pout.Base = float4(normalize(pin.PosL), 1.0);
		pout.Normal = normalize(pin.NormalW);
		pout.Maps = 0;
		return pout;
	}

	technique11 SpecialGlDeferred {
		pass P0 {
			SetVertexShader( CompileShader( vs_4_0, vs_SpecialGl() ) );
			SetGeometryShader( NULL );
			SetPixelShader( CompileShader( ps_4_0, ps_SpecialGlDeferred() ) );
		}
	}

	float4 ps_SpecialGlForward(SpecialGl_PS_IN pin) : SV_Target {
		return float4(normalize(pin.PosL), 1.0);
	}

	technique11 SpecialGlForward {
		pass P0 {
			SetVertexShader( CompileShader( vs_4_0, vs_SpecialGl() ) );
			SetGeometryShader( NULL );
			SetPixelShader( CompileShader( ps_4_0, ps_SpecialGlForward() ) );
		}
	}