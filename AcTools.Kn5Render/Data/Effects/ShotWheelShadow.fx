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

float4 ps_wheel_shadow(PS_IN pin) : SV_Target {
	float tex = 1.0f - clamp((gInputImage.SampleLevel(samInputImage, pin.Tex, 0.0).r * 0.64f + 0.36f), 0.36f, 1.0f);
    return float4(tex, tex, tex, 1.0f);
}

technique11 CreateWheelShadow { // PT
    pass P0 {
        SetVertexShader( CompileShader( vs_4_0, vs_main() ) );
        SetGeometryShader( NULL );
        SetPixelShader( CompileShader( ps_5_0, ps_wheel_shadow() ) );
    }
}

float4 ps_wheel_shadow_blur(PS_IN pin, uniform bool gHorizontalBlur) : SV_Target {
	float2 texOffset;

    [flatten]
	if (gHorizontalBlur){
		texOffset = float2(gTexel.x, 0.0f);
	} else {
		texOffset = float2(0.0f, gTexel.y);
	}

	float orig = gInputImage.SampleLevel(samInputImage, pin.Tex, 0.0).r;
	float color = 0;

	texOffset *= 1.0f + (0.64f - orig) * 8.0f; 
	
    [flatten]
	for (float i = -5; i <= 5; ++i){
		float val = gInputImage.SampleLevel(samInputImage, pin.Tex + i * texOffset, 0.0).r;
		color += val / 5;
	}
	
	color = max(color - orig, 0.0f);
	float4 res = min(color, orig);
	res.a = 1.0f;
    return res;
}

technique11 HorzWheelShadowBlur {
    pass P0 {
        SetVertexShader( CompileShader( vs_4_0, vs_main() ) );
		SetGeometryShader( NULL );
        SetPixelShader( CompileShader( ps_5_0, ps_wheel_shadow_blur(true) ) );
    }
}

float4 ps_copy(PS_IN pin) : SV_Target {
	return gInputImage.SampleLevel(samInputImage, pin.Tex, 0.0f);
}

technique11 VertWheelShadowBlur {
    pass P0 {
        SetVertexShader( CompileShader( vs_4_0, vs_main() ) );
		SetGeometryShader( NULL );
        SetPixelShader( CompileShader( ps_5_0, ps_copy() ) );
    }
}
