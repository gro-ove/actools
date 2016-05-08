#include "Common.fx"

Texture2D gDepthMap;

cbuffer cbPerFrame {
	float4 gSize;
	float gMultipler;
	float gCount;
	
	matrix gShadowViewProj;
};

cbuffer cbSettings {
	float gWeights[11] = {
		0.05f, 0.05f, 0.1f, 0.1f, 0.1f, 0.2f, 0.1f, 0.1f, 0.1f, 0.05f, 0.05f
	};
};

SamplerState samDepth {
	Filter = MIN_MAG_MIP_LINEAR;
	AddressU = BORDER;
	AddressV = BORDER;
	AddressW = BORDER;
	BorderColor = float4(1.0f, 1.0f, 1.0f, 0.0f);
};

SamplerState samInputImageTest {
	Filter = MIN_MAG_LINEAR_MIP_POINT;
	AddressU = BORDER;
	AddressV = BORDER;
	AddressW = BORDER;
	BorderColor = float4(1.0f, 1.0f, 1.0f, 1.0f);
};

float texd(float2 uv) {
	// return gDepthMap.Sample(samDepth, uv).r;
	return gDepthMap.Sample(samInputImageTest, uv).r;
}

float4 ps_ShadowBlur(PS_IN pin, uniform bool gHorizontalBlur) : SV_Target {
	float2 texOffset;

	if (gHorizontalBlur){
		texOffset = float2(gSize.z, 0.0) * gMultipler;
	} else {
		texOffset = float2(0.0, gSize.w) * gMultipler;
	}

	float color = 0;
	for (float i = -5; i <= 5; ++i){
		float val = gInputMap.SampleLevel(samInputImageTest, pin.Tex + i * texOffset, 0.0).r;
		color += val * gWeights[i + 5];
	}
	
    return color;
}

technique10 HorizontalShadowBlur {
    pass P0 {
        SetVertexShader( CompileShader( vs_4_0, vs_main() ) );
		SetGeometryShader( NULL );
        SetPixelShader( CompileShader( ps_4_0, ps_ShadowBlur(true) ) );
    }
}

technique10 VerticalShadowBlur {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_ShadowBlur(false)));
	}
}

struct pts_PS_IN {
	float4 PosH       : SV_POSITION;
	float2 Tex        : TEXCOORD;
	float4 ShadowPosH : TEXCOORD1;
};

pts_PS_IN vs_pts_main(VS_IN vin) {
	pts_PS_IN vout;
	vout.PosH = float4(vin.PosL, 1.0f);
	vout.Tex = vin.Tex;
	vout.ShadowPosH = mul(float4(-vin.PosL.x, 0.0f, -vin.PosL.y, 1.0f), gShadowViewProj);
	return vout;
}

float4 ps_AmbientShadow(pts_PS_IN pin) : SV_Target{
	return texd(pin.ShadowPosH.xy / pin.ShadowPosH.w) > 0.99;
}

technique10 AmbientShadow {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_pts_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_AmbientShadow()));
	}
}

float4 ps_Temp(PS_IN pin) : SV_Target{
	float v = 0.8 * (1 - saturate(tex(pin.Tex).x / gCount));
	return float4(v, v, v, 1.0);
}

technique10 Temp {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Temp()));
	}
}