// textures
Texture2D gInputMap;

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
	
// input resources
cbuffer cbPerObject : register(b0) {
	float4 gScreenSize;
	float2 gMultipler; // less than zero
}

// fn structs
struct VS_IN {
	float3 PosL    : POSITION;
	float2 Tex     : TEXCOORD;
};

struct PS_IN {
	float4 PosH    : SV_POSITION;
	float2 Tex     : TEXCOORD;
};

// one vertex shader for everything
PS_IN vs_main(VS_IN vin) {
	PS_IN vout;
	vout.PosH = float4(vin.PosL, 1.0);
	vout.Tex = vin.Tex;
	return vout;
}

// just copy to the output buffer
float4 ps_Copy(PS_IN pin) : SV_Target{
	return gInputMap.Sample(samPoint, pin.Tex);
}

technique10 Copy {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Copy()));
	}
}

// another thing I found
struct found_PS_IN {
	float4 PosH    : SV_POSITION;
	float2 Tex[5]  : TEXCOORD;
};

// one vertex shader for everything
found_PS_IN vs_Found(VS_IN vin) {
	found_PS_IN vout;
	vout.PosH = float4(vin.PosL, 1.0);

	float2 uv = vin.Tex;
	float w = 1.75;

	float2 up = float2(0.0, gScreenSize.z) * w;
	float2 right = float2(gScreenSize.w, 0.0) * w;

	vout.Tex[0].xy = uv - up;
	vout.Tex[1].xy = uv - right;
	vout.Tex[2].xy = uv + right;
	vout.Tex[3].xy = uv + up;
	vout.Tex[4].xy = uv;

	return vout;
}

float Luminance(float3 color){
	return dot(color, float3(0.299f, 0.587f, 0.114f));
}

float4 ps_Found(found_PS_IN pin) : SV_Target{
	float t = Luminance(gInputMap.SampleLevel(samPoint, pin.Tex[0], 0).xyz);
	float l = Luminance(gInputMap.SampleLevel(samPoint, pin.Tex[1], 0).xyz);
	float r = Luminance(gInputMap.SampleLevel(samPoint, pin.Tex[2], 0).xyz);
	float b = Luminance(gInputMap.SampleLevel(samPoint, pin.Tex[3], 0).xyz);

	float2 n = float2(-(t - b), r - l);
	float nl = length(n);

	if (nl < (1.0 / 16.0)) {
		return gInputMap.SampleLevel(samPoint, pin.Tex[4], 0);
	} else {
		n *= gScreenSize.zw / nl;

		float4 o = gInputMap.SampleLevel(samPoint, pin.Tex[4], 0);
		float4 t0 = gInputMap.SampleLevel(samPoint, pin.Tex[4] + n * 0.5, 0) * 0.9;
		float4 t1 = gInputMap.SampleLevel(samPoint, pin.Tex[4] - n * 0.5, 0) * 0.9;
		float4 t2 = gInputMap.SampleLevel(samPoint, pin.Tex[4] + n, 0) * 0.75;
		float4 t3 = gInputMap.SampleLevel(samPoint, pin.Tex[4] - n, 0) * 0.75;

		return (o + t0 + t1 + t2 + t3) / 4.3;
	}

	// return gInputMap.Sample(samPoint, pin.Tex);
}

technique10 Found {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_Found()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Found()));
	}
}

// just copy to the output buffer
float4 ps_Average(PS_IN pin) : SV_Target {
	float4 result = 0;
	float v = 0;

	float x, y;
	for (x = -1; x <= 1; x += 0.25) {
		for (y = -1; y <= 1; y += 0.25) {
			float2 uv = pin.Tex + float2(x, y) * gScreenSize.zw * 0.5;
			float w = sqrt(pow(1.5 - abs(x), 2) + pow(1.5 - abs(y), 2));
			result += gInputMap.SampleLevel(samPoint, uv, 0) * w;
			v += w;
		}
	}

	return result / v;
}

technique10 Average {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Average()));
	}
}

// anisotropic thing
SamplerState samAnisotropic {
	Filter = ANISOTROPIC;
	MaxAnisotropy = 16;

	AddressU = WRAP;
	AddressV = WRAP;
};

float4 ps_Anisotropic(PS_IN pin) : SV_Target {
	return gInputMap.Sample(samAnisotropic, pin.Tex);
}

technique10 Anisotropic {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Anisotropic()));
	}
}

// found online
float4 SampleBicubic(Texture2D tex, sampler texSampler, float2 uv){
	//--------------------------------------------------------------------------------------
	// Calculate the center of the texel to avoid any filtering

	float2 textureDimensions = gScreenSize.xy;
	float2 invTextureDimensions = gScreenSize.zw;

	uv *= textureDimensions;

	float2 texelCenter = floor(uv - 0.5f) + 0.5f;
	float2 fracOffset = uv - texelCenter;
	float2 fracOffset_x2 = fracOffset * fracOffset;
	float2 fracOffset_x3 = fracOffset * fracOffset_x2;

	//--------------------------------------------------------------------------------------
	// Calculate the filter weights (B-Spline Weighting Function)

	float2 weight0 = fracOffset_x2 - 0.5f * (fracOffset_x3 + fracOffset);
	float2 weight1 = 1.5f * fracOffset_x3 - 2.5f * fracOffset_x2 + 1.f;
	float2 weight3 = 0.5f * (fracOffset_x3 - fracOffset_x2);
	float2 weight2 = 1.f - weight0 - weight1 - weight3;

	//--------------------------------------------------------------------------------------
	// Calculate the texture coordinates

	float2 scalingFactor0 = weight0 + weight1;
	float2 scalingFactor1 = weight2 + weight3;

	float2 f0 = weight1 / (weight0 + weight1);
	float2 f1 = weight3 / (weight2 + weight3);

	float2 texCoord0 = texelCenter - 1.f + f0;
	float2 texCoord1 = texelCenter + 1.f + f1;

	texCoord0 *= invTextureDimensions;
	texCoord1 *= invTextureDimensions;

	//--------------------------------------------------------------------------------------
	// Sample the texture

	return tex.Sample(texSampler, float2(texCoord0.x, texCoord0.y)) * scalingFactor0.x * scalingFactor0.y +
		tex.Sample(texSampler, float2(texCoord1.x, texCoord0.y)) * scalingFactor1.x * scalingFactor0.y +
		tex.Sample(texSampler, float2(texCoord0.x, texCoord1.y)) * scalingFactor0.x * scalingFactor1.y +
		tex.Sample(texSampler, float2(texCoord1.x, texCoord1.y)) * scalingFactor1.x * scalingFactor1.y;
}

float4 ps_Bicubic(PS_IN pin) : SV_Target {
	return SampleBicubic(gInputMap, samPoint, pin.Tex);
}

technique10 Bicubic {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Bicubic()));
	}
}