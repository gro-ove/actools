Texture2D gDepthMap;
Texture2D gShadowMap;
Texture2D gNoiseMap;

SamplerState samLinear {
	Filter = MIN_MAG_MIP_LINEAR;
	AddressU = CLAMP;
	AddressV = CLAMP;
};

SamplerState samDepth {
	Filter = MIN_MAG_LINEAR_MIP_POINT;
	AddressU = Border;
	AddressV = Border;
	BorderColor = (float4)1e5f;
};

SamplerState samShadow {
	Filter = MIN_MAG_MIP_LINEAR;
	AddressU = BORDER;
	AddressV = BORDER;
	BorderColor = (float4)0.0;
};

SamplerState samNoise {
	Filter = MIN_MAG_MIP_POINT;
	AddressU = WRAP;
	AddressV = WRAP;
};

cbuffer cbParams : register(b0) {
	matrix gViewProjInv;
	matrix gViewProj;
	matrix gShadowViewProj;
	float3 gShadowPosition;
	float2 gNoiseSize;
	float2 gShadowSize;
}

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
float3 GetPosition(float2 uv, float depth) {
	float4 position = mul(float4(uv.x * 2 - 1, -(uv.y * 2 - 1), depth, 1), gViewProjInv);
	return position.xyz / position.w;
}

float GetDepth(float2 uv) {
	return gDepthMap.SampleLevel(samDepth, uv, 0).x;
}

float3 GetUv(float3 position) {
	float4 pVP = mul(float4(position, 1.0f), gViewProj);
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

#define _PADDING 0.1

float GetOpacity(float delta) {
	return 1 - saturate(delta < 0 ? -delta * 100.0 : delta - _PADDING);
}

float4 ps_AddShadow(PS_IN pin) : SV_Target {
	float depth = GetDepth(pin.Tex);
	float3 pos = GetPosition(pin.Tex, depth);

	float4 uv = mul(float4(pos, 1.0), gShadowViewProj);
	uv.xyz /= uv.w;

	float delta = gShadowPosition.y - pos.y;
	float opacity = GetOpacity(delta);

	return (float4)(1.0 - opacity * gShadowMap.SampleLevel(samLinear, uv.xz, 0.0).x);
}

technique10 AddShadow {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_AddShadow()));
	}
}

#define _POISSON_DISKS_SIZE 10

cbuffer POISSON_DISKS {
	float2 poissonDisk[_POISSON_DISKS_SIZE] = {
		float2(-0.2027472f, -0.7174203f),
		float2(-0.4839617f, -0.1232477f),
		float2(0.4924171f, -0.06338801f),
		float2(-0.6403998f, 0.6834511f),
		float2(-0.8817205f, -0.4650014f),
		float2(0.04554421f, 0.1661989f),
		float2(0.1042245f, 0.9336259f),
		float2(0.6152743f, 0.6344957f),
		float2(0.5085323f, -0.7106467f),
		float2(-0.9731231f, 0.1328296f)
	};
}

float4 ps_AddShadowBlur(PS_IN pin) : SV_Target {
	float depth = GetDepth(pin.Tex);
	float3 pos = GetPosition(pin.Tex, depth);

	float delta = gShadowPosition.y - pos.y;
	float opacity = GetOpacity(delta);

	float2 random = normalize(gNoiseMap.SampleLevel(samNoise, pin.Tex * gNoiseSize, 0.0).xy);
	float shadowAvg = 0.0;

	float4 uv = mul(float4(pos, 1.0), gShadowViewProj);
	uv.xyz /= uv.w;

	float2 maxOffset = saturate(delta - _PADDING) * 0.8 * gShadowSize;

	[branch]
	if ((uv.x < 0.5 ? uv.x < -maxOffset.x : uv.x > 1.0 + maxOffset.x) ||
		(uv.z < 0.5 ? uv.z < -maxOffset.y : uv.z > 1.0 + maxOffset.y)) {
		clip(-1);
		return (float4)1.0;
	}

	for (int i = 0; i < _POISSON_DISKS_SIZE; i++) {
		float2 randomDirection = reflect(poissonDisk[i], random);
		float2 randomOffset = randomDirection * saturate(delta - _PADDING) * 0.8 * gShadowSize;
		shadowAvg += gShadowMap.SampleLevel(samLinear, uv.xz + randomOffset, 0.0).x;
	}

	shadowAvg /= _POISSON_DISKS_SIZE;
	return (float4)saturate(1.0 - opacity * shadowAvg);
}

technique10 AddShadowBlur {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_AddShadowBlur()));
	}
}