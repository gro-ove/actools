#include "Common.fx"

Texture2D gFlatMirrorDepthMap;
Texture2D gFlatMirrorNormalsMap;
Texture2D gMapsMap;

static const int SAMPLE_COUNT = 15;

cbuffer cbPerFrame : register(b0) {
	float2 gSampleOffsets[SAMPLE_COUNT];
	float gSampleWeights[SAMPLE_COUNT];
	float gPower;
	matrix gWorldViewProjInv;
	float4 gScreenSize;
}

// default
float4 ps_GaussianBlurDebug(PS_IN pin) : SV_Target {
	return tex(pin.Tex);
}

float4 ps_GaussianBlur(PS_IN pin) : SV_Target {
	float4 c = 0;
	for (int i = 0; i < SAMPLE_COUNT; i++){
		c += tex(pin.Tex + gSampleOffsets[i] * gPower) * gSampleWeights[i];
	}
	return c;
}

technique10 GaussianBlur {
	pass P0 {
		SetVertexShader( CompileShader( vs_4_0, vs_main() ) );
		SetGeometryShader( NULL );
		SetPixelShader( CompileShader( ps_4_0, ps_GaussianBlur() ) );
	}
}

// special flat mirror blur
SamplerState samPoint {
	Filter = MIN_MAG_MIP_POINT;
	AddressU = CLAMP;
	AddressV = CLAMP;
};

SamplerState samLinear {
	Filter = MIN_MAG_MIP_LINEAR;
	AddressU = CLAMP;
	AddressV = CLAMP;
};

float GetDepth(float2 uv) {
	return gFlatMirrorDepthMap.SampleLevel(samPoint, uv, 0).x;
}

float GetPosition(float2 uv) {
	float4 position = mul(float4(uv.x * 2 - 1, -(uv.y * 2 - 1), GetDepth(uv), 1), gWorldViewProjInv);
	return saturate(-position.y / position.w);
}

cbuffer POISSON_DISKS {
	float3 poissonDisk[25] = {
		float3(-0.8599291f, 0.1053815f, 0.8663621f),
		float3(-0.955691f, -0.2597262f, 0.990355f),
		float3(-0.6555323f, 0.4653695f, 0.8039225f),
		float3(-0.546823f, 0.1474342f, 0.5663499f),
		float3(-0.5594394f, -0.3889233f, 0.6813471f),
		float3(-0.2577913f, -0.1047817f, 0.2782725f),
		float3(-0.1837782f, -0.4135874f, 0.4525804f),
		float3(-0.2575692f, -0.9050629f, 0.9409998f),
		float3(0.1318264f, -0.6595055f, 0.6725516f),
		float3(0.2684311f, -0.9539945f, 0.9910402f),
		float3(0.2540674f, -0.3429227f, 0.4267859f),
		float3(0.6022666f, -0.5264002f, 0.7998889f),
		float3(-0.2070377f, 0.5548291f, 0.5921992f),
		float3(-0.01066613f, 0.31967f, 0.3198479f),
		float3(0.5018253f, 0.1017721f, 0.5120412f),
		float3(0.1294076f, 0.03881982f, 0.1351048f),
		float3(0.5085628f, 0.6038149f, 0.7894483f),
		float3(0.1376956f, 0.7000137f, 0.7134278f),
		float3(0.7529016f, -0.2451619f, 0.7918113f),
		float3(-0.1535011f, 0.9169651f, 0.9297245f),
		float3(-0.4586751f, 0.746668f, 0.8762968f),
		float3(-0.7747433f, -0.6192066f, 0.9917883f),
		float3(0.7762839f, 0.2649287f, 0.8202463f),
		float3(0.9939056f, -0.04252804f, 0.9948151f),
		float3(0.3324822f, 0.3584656f, 0.4889192f)
	};
};

float4 ps_FlatMirrorBlur(PS_IN pin) : SV_Target {
	float p = GetPosition(pin.Tex);
	float4 c = gInputMap.Sample(samPoint, pin.Tex);
	float r = 1;

	/*float x, y;
	for (x = -0.6; x < 0.61; x += 0.4) {
		for (y = -0.6; y < 0.61; y += 0.4) {
			float2 delta = float2(x, y) * gScreenSize.zw * gPower;
			float mu = p;

			for (int j = 0; j < 3; j++) {
				float np = GetPosition(pin.Tex + delta * mu);
				mu = min((np * 2 + mu) / 3, mu);
			}

			c += min(gInputMap.SampleLevel(samPoint, pin.Tex + delta * mu, 0), 1.2);
			r++;
		}
	}*/

	for (int i = 0; i < 25; ++i) {
		float2 uv = pin.Tex + poissonDisk[i].xy * gScreenSize.zw * gPower * p;

		float np = GetPosition(uv);
		[flatten]
		if (np > p * poissonDisk[i].z * 0.5) {
			c += min(gInputMap.SampleLevel(samLinear, uv, 0), 1.5);
			r++;
		}
	}

	return c / r;
}

technique10 FlatMirrorBlur {
	pass P0 {
		SetVertexShader( CompileShader( vs_4_0, vs_main() ) );
		SetGeometryShader( NULL );
		SetPixelShader( CompileShader( ps_4_0, ps_FlatMirrorBlur() ) );
	}
}

// special dark sslr mode
float4 ps_DarkSslrBlur0(PS_IN pin) : SV_Target {
	float4 r = tex(pin.Tex);

	float c = 0;
	for (int i = 0; i < SAMPLE_COUNT; i++) {
		c += tex(pin.Tex + gSampleOffsets[i] * gPower).b * gSampleWeights[i];
	}
	r.b = c;

	return r;
}

technique10 DarkSslrBlur0 {
	pass P0 {
		SetVertexShader( CompileShader( vs_4_0, vs_main() ) );
		SetGeometryShader( NULL );
		SetPixelShader( CompileShader( ps_4_0, ps_DarkSslrBlur0() ) );
	}
}

// special reflection mode
float4 ps_ReflectionGaussianBlur(PS_IN pin) : SV_Target {
	float power = saturate(1 - tex(gMapsMap, pin.Tex).y * 15);

	float4 c = 0;
	for (int i = 0; i < SAMPLE_COUNT; i++) {
		c += tex(pin.Tex + gSampleOffsets[i] * power) * gSampleWeights[i];
	}

	return c;
}

technique10 ReflectionGaussianBlur {
	pass P0 {
		SetVertexShader( CompileShader( vs_4_0, vs_main() ) );
		SetGeometryShader( NULL );
		SetPixelShader( CompileShader( ps_4_0, ps_ReflectionGaussianBlur() ) );
	}
}