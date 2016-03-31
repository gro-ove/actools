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

float4 ps_body_shadow(PS_IN pin) : SV_Target {
	float tex = gInputImage.SampleLevel(samInputImage, pin.Tex, 0.0).r;
	
	float d = sign(pin.Tex.x - 0.5f) * gTexel.x * 1.5f;
	float k = 0.0f;
	float l = tex;

    [flatten]
	for (float i = 1; i < 11; ++i){
		float n = gInputImage.SampleLevel(samInputImage, pin.Tex + float2(d * i, 0.0f), 0.0).r;
		if (n > l + 0.007f){
			k = 1.0f;
			l = min(n, 0.992f);
		} else {
			k = 0.0f;
			break;
		}
	}
	
    [flatten]
	if (l > 0.99f){
		tex += k;
	}
	
	d = sign(pin.Tex.y - 0.5f) * gTexel.y * 0.8f;
	k = 0.0f;
	l = tex;

    [flatten]
	for (float i = 1; i < 11; ++i){
		float n = gInputImage.SampleLevel(samInputImage, pin.Tex + float2(0.0f, d * i), 0.0).r;

		[flatten]
		if (n > l + 0.006f){
			k = 1.0f;
			l = min(n, 0.993f);
		} else {
			k = 0.0f;
			break;
		}
	}
	
    [flatten]
	if (l > 0.99f){
		tex += k;
	}
	
    [flatten]
	if (tex < 0.99f){
		tex = 1.0f - clamp(tex, 0.36f, 0.64f);
	} else {
		tex = 0.0f;
	}

    return float4(tex, tex, tex, 1.0f);
}

technique11 CreateBodyShadow { // PT
    pass P0 {
        SetVertexShader( CompileShader( vs_4_0, vs_main() ) );
        SetGeometryShader( NULL );
        SetPixelShader( CompileShader( ps_5_0, ps_body_shadow() ) );
    }
}

float4 ps_body_shadow_blur(PS_IN pin, uniform bool gHorizontalBlur) : SV_Target {
	float2 texOffset;

    [flatten]
	if (gHorizontalBlur){
		texOffset = float2(gTexel.x, 0.0f);
	} else {
		texOffset = float2(0.0f, gTexel.y);
	}

	float orig = gInputImage.SampleLevel(samInputImage, pin.Tex, 0.0).r;
	float color = 0;

	texOffset *= 1.0f + (0.64f - orig) * 2.5f; 
	
    [flatten]
	for (float i = -8; i <= 8; ++i){
		float val = gInputImage.SampleLevel(samInputImage, pin.Tex + i * texOffset, 0.0).r;
		color += val / 8;
	}
	
	color = max(color - orig, 0.0f);
	float4 res = min(color, orig);
	res.a = 1.0f;
    return res;
}

technique11 HorzBodyShadowBlur {
    pass P0 {
        SetVertexShader( CompileShader( vs_4_0, vs_main() ) );
		SetGeometryShader( NULL );
        SetPixelShader( CompileShader( ps_5_0, ps_body_shadow_blur(true) ) );
    }
}

technique11 VertBodyShadowBlur {
    pass P0 {
        SetVertexShader( CompileShader( vs_4_0, vs_main() ) );
		SetGeometryShader( NULL );
        SetPixelShader( CompileShader( ps_5_0, ps_body_shadow_blur(false) ) );
    }
}
