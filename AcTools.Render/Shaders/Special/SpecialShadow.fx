#include "Common.fx"

Texture2D gDepthMap;

cbuffer cbPerFrame {
	float4 gSize;
	float gMultipler;
	float gGamma;
	float gCount;
	float gAmbient;

	float gPadding;
	float2 gShadowSize;
	float2 gScreenSize;
	float gFade;

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
	return texd(pin.ShadowPosH.xy / pin.ShadowPosH.w) > 0.99 ? gMultipler : 0.0;
}

technique10 AmbientShadow {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_pts_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_AmbientShadow()));
	}
}

float Fade(float toEdge, float size, float padding) {
	return saturate(toEdge * size * padding);
}

float4 ps_Result(PS_IN pin) : SV_Target {
	float2 uv = pin.Tex;
	uv.x = 1.0 - uv.x;

	float2 de = (pin.Tex - gPadding) / (1.0 - gPadding * 2);
	float2 dd = float2(
		Fade(de.x < 0.5 ? de.x : 1 - de.x, gShadowSize.x, gFade),
		Fade(de.y < 0.5 ? de.y : 1 - de.y, gShadowSize.y, gFade));
	float d = saturate(1.0 - length((float2)1.0 - dd));

	float c = 1 - saturate(tex(uv).x / gCount);
	float v = gMultipler * c * d + lerp(0.00196, -0.00196, frac(0.25 + dot(uv, gScreenSize.xy * 0.5)));
	return float4(v, v, v, 1.0);
}

technique10 Result {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Result()));
	}
}

// Simplest shader for rendering meshes without any lighting
SamplerState samAnisotropic {
	Filter = ANISOTROPIC;
	MaxAnisotropy = 8;

	AddressU = WRAP;
	AddressV = WRAP;
};

Texture2D gAlphaMap;

cbuffer cbPerObject : register(b0) {
	matrix gWorldViewProj;
	float gAlphaRef;
}

struct simplest_VS_IN {
	float3 PosL : POSITION;
	float2 Tex : TEXCOORD;
};

struct simplest_PS_IN {
	float4 PosH : SV_POSITION;
	float2 Tex : TEXCOORD;
};

simplest_PS_IN vs_simplest(simplest_VS_IN vin) {
	simplest_PS_IN vout;
	vout.PosH = mul(float4(vin.PosL, 1.0f), gWorldViewProj);
	vout.Tex = vin.Tex;
	return vout;
}

float4 ps_simplest(simplest_PS_IN pin) : SV_Target{
	clip(gAlphaMap.Sample(samAnisotropic, pin.Tex).a - gAlphaRef - 0.0001);
	return float4(1.0, 1.0, 1.0, 1.0);
}

technique10 Simplest {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_simplest()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_simplest()));
	}
}

// Something extra
cbuffer cbPerObjectAo : register(b1) {
	matrix gWorld;
	matrix gWorldInvTranspose;
	float gNormalUvMult;
	float3 gLightDir;
}

cbuffer cbPerFrameAo {
	float2 gOffset;
};

Texture2D gNormalMap;

struct ao_VS_IN {
	float3 PosL       : POSITION;
	float3 NormalL    : NORMAL;
	float2 Tex        : TEXCOORD;
	float3 TangentL   : TANGENT;
};

struct ao_PS_IN {
	float4 PosH       : SV_POSITION;
	float3 PosW       : POSITION;
	float2 Tex        : TEXCOORD;
	float3 PosShadow  : TEXCOORD1;
	float3 NormalW    : NORMAL;
	float3 TangentW   : TANGENT;
	float3 BitangentW : BITANGENT;
};

#define fix(v) (v)

/*float fix(float v){
    return v;//((v % 1) + 1) % 1;
}*/

ao_PS_IN vs_Ao(ao_VS_IN vin) {
	ao_PS_IN vout;

	vout.PosW = mul(float4(vin.PosL, 1.0f), gWorld).xyz;
	vout.NormalW = mul(vin.NormalL, (float3x3)gWorldInvTranspose);
	vout.TangentW = mul(vin.TangentL, (float3x3)gWorldInvTranspose);
	vout.BitangentW = mul(cross(vin.NormalL, vin.TangentL), (float3x3)gWorldInvTranspose);

	vout.Tex = vin.Tex;

	float2 tex = vin.Tex + gOffset;
	vout.PosH = float4(fix(tex.x) * 2 - 1, fix(-tex.y) * 2 - 1, 1, 1);

	float4 posShadow = mul(float4(vout.PosW, 1.0), gShadowViewProj);
	vout.PosShadow = posShadow.xyz / posShadow.w;

	return vout;
}

SamplerComparisonState samShadow {
	Filter = COMPARISON_MIN_MAG_MIP_LINEAR;
	AddressU = BORDER;
	AddressV = BORDER;
	AddressW = BORDER;
	BorderColor = float4(1.0f, 1.0f, 1.0f, 1.0f);
	ComparisonFunc = LESS;
};

float3 NormalSampleToWorldSpace(float3 normalMapSample, float3 N, float3 T, float3 B) {
	return mul(2.0 * normalMapSample - 1.0, float3x3(T, B, N));
}

float4 ps_Ao(ao_PS_IN pin) : SV_Target {
	float4 normalValue = gNormalMap.Sample(samAnisotropic, pin.Tex);
	float3 normal = normalize(NormalSampleToWorldSpace(normalValue.xyz, pin.NormalW, pin.TangentW, pin.BitangentW));
	float ambient = saturate(pin.PosW.y * 2.0) * gAmbient;
	float light = saturate(dot(normal, gLightDir)) * (1.0 - ambient) + ambient;
	float shadow = gDepthMap.SampleCmpLevelZero(samShadow, pin.PosShadow.xy, pin.PosShadow.z).r;
	return float4((float3)(light * shadow), 1.0);
}

technique10 Ao {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_Ao()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Ao()));
	}
}

float4 ps_AoResult(PS_IN pin) : SV_Target {
	float2 uv = pin.Tex;
	float4 input = tex(uv);
	float v = saturate(pow(max(input.x / gCount, 0.0), gGamma))
	    + lerp(0.00196, -0.00196, frac(0.25 + dot(uv, gScreenSize.xy * 0.5)));
	return float4(v, v, v, input.a);
}

technique10 AoResult {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_AoResult()));
	}
}

#define _STEPS 5
#define _CUTOUT 1
#define _STEP_SIZE 0.5
#define _THRESHOLD 0.95

float4 ps_AoGrow(PS_IN pin) : SV_Target {
	float2 uv = pin.Tex;
	float4 base = tex(uv);

	if (base.a > _THRESHOLD &&
        tex(uv + float2(gSize.z, 0)).a > _THRESHOLD &&
        tex(uv + float2(-gSize.z, 0)).a > _THRESHOLD &&
        tex(uv + float2(0, gSize.w)).a > _THRESHOLD &&
        tex(uv + float2(0, -gSize.w)).a > _THRESHOLD &&
        tex(uv + float2(gSize.z, gSize.w)).a > _THRESHOLD &&
        tex(uv + float2(-gSize.z, -gSize.w)).a > _THRESHOLD &&
        tex(uv + float2(-gSize.z, gSize.w)).a > _THRESHOLD &&
        tex(uv + float2(gSize.z, -gSize.w)).a > _THRESHOLD) return base;

	base = (float4)0;
	int x, y;

	[unroll]
	for (x = -_STEPS; x <= _STEPS; x++)
		[unroll]
		for (y = -_STEPS; y <= _STEPS; y++) {
			// if (y > -_UP_TO && y < _UP_TO) continue;
			float4 n = tex(uv + gSize.zw * float2(x, y) * _STEP_SIZE);
			if (n.a > _THRESHOLD){
			    base.r = max(base.r, n.r);
			    base.g = max(base.g, n.g);
			    base.b = max(base.b, n.b);
			    base.a = max(base.a, n.a);
			}
			// base += float4(n.rgb * n.a, n.a);
		}

	/*[unroll]
	for (x = _UP_TO; x <= _STEPS; x++)
		[unroll]
		for (y = -_STEPS; y <= _STEPS; y++) {
			if (y > -_UP_TO && y < _UP_TO) continue;
			float4 n = tex(uv + gSize.zw * float2(x, y) * _STEP_SIZE);
			if (n.a > 0.9){
			    base.r = max(base.r, n.r);
			    base.g = max(base.g, n.g);
			    base.b = max(base.b, n.b);
			    base.a = max(base.a, n.a);
			}
			// base += float4(n.rgb * n.a, n.a);
		}*/

	if (base.a > 0.9) return base;// float4(base.rgb / base.a, base.a);
	return (float4)0;
}

technique10 AoGrow {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_AoGrow()));
	}
}