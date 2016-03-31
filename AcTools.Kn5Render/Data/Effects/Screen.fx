cbuffer cbPerFrame {
	float2 gTexel;
};

cbuffer cbSettings {
	float gWeights[11] = {
		0.05f, 0.05f, 0.1f, 0.1f, 0.1f, 0.2f, 0.1f, 0.1f, 0.1f, 0.05f, 0.05f
	};
};

cbuffer cbFixed {
	static const int gBlurRadius = 5;
};

Texture2D gInputImage;

SamplerState samInputImage {
	Filter = MIN_MAG_LINEAR_MIP_POINT;
	AddressU = CLAMP;
    AddressV = CLAMP;
};

struct VS_IN {
    float3 PosL    : POSITION;
	float2 Tex     : TEXCOORD;
};

struct PS_IN {
    float4 PosH    : SV_POSITION;
	float2 Tex     : TEXCOORD;
};

PS_IN vs_main(VS_IN vin) {
	PS_IN vout;
	vout.PosH = float4(vin.PosL, 1.0f);
	vout.Tex = vin.Tex;
	return vout;
}

float4 ps_copy(PS_IN pin) : SV_Target {
	float3 luminanceWeights = float3(0.299f, 0.587f, 0.114f);
	float4 srcPixel = gInputImage.SampleLevel(samInputImage, pin.Tex, 0.0f);
	float luminance = dot(srcPixel.xyz, luminanceWeights);
	float4 dstPixel = lerp(luminance, srcPixel, 1.1f);
	dstPixel.a = srcPixel.a;
	return dstPixel;
}

technique11 Copy { // PT
    pass P0 {
        SetVertexShader( CompileShader( vs_4_0, vs_main() ) );
        SetGeometryShader( NULL );
        SetPixelShader( CompileShader( ps_5_0, ps_copy() ) );
    }
}

float4 ps_blur(PS_IN pin, uniform bool gHorizontalBlur) : SV_Target {
	float2 texOffset;
	if (gHorizontalBlur){
		texOffset = float2(gTexel.x, 0.0f);
	} else {
		texOffset = float2(0.0f, gTexel.y);
	}

	float4 color = 0;
	float totalWeight = 0;
	
    [flatten]
	for (float i = -gBlurRadius; i <= gBlurRadius; ++i){
		float weight = gWeights[i + gBlurRadius];
		color += weight * gInputImage.SampleLevel(samInputImage, pin.Tex + i * texOffset, 0.0);
		totalWeight += weight;
	}

	return color / totalWeight;
}

technique11 HorzBlur {
    pass P0 {
        SetVertexShader( CompileShader( vs_4_0, vs_main() ) );
		SetGeometryShader( NULL );
        SetPixelShader( CompileShader( ps_5_0, ps_blur(true) ) );
    }
}

technique11 VertBlur {
    pass P0 {
        SetVertexShader( CompileShader( vs_4_0, vs_main() ) );
		SetGeometryShader( NULL );
        SetPixelShader( CompileShader( ps_5_0, ps_blur(false) ) );
    }
}

