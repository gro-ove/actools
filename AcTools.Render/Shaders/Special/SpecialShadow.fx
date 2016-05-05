#include "Common.fx"

Texture2D gDepthMap;

cbuffer cbPerFrame {
	float4 gSize;
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

float texd(float2 uv) {
	return gDepthMap.Sample(samDepth, uv).r;
}

// base
float4 ps_Base(PS_IN pin) : SV_Target {
	// return 1.0 - texd(pin.Tex);

	float brightness = 1;
	float value = texd(pin.Tex);

	return value == 1.0;

	float occluded = 1;

	float blurLevel = texd(pin.Tex) * 8.0 + 0.2;
	for (float x = -4.5; x <= 4.5; x++) {
		for (float y = -4.5; y <= 4.5; y++){
			float value = texd(pin.Tex + float2(x * gSize.z, y * gSize.w) * blurLevel);
			// value *= (abs(x) + abs(y)) / 5.0 + 0.5;
			occluded -= max(min(value, 1.0) - 0.95, 0.0);
		}
	}

	//occluded /= 64.0;
	//occluded = saturate(occluded - 0.33) * 1.5;
	occluded = saturate(occluded);
	return occluded;
}

float4 ps_BaseOld(PS_IN pin) : SV_Target {
	float tex = texd(pin.Tex);
	
	float d = sign(pin.Tex.x - 0.5f) * gSize.z * 1.5f;
	float k = 0.0f;
	float l = tex;

	for (float i = 1; i < 11; ++i){
		float n = texd(pin.Tex + float2(d * i, 0.0f));
		if (n > l + 0.007f){
			k = 1.0f;
			l = min(n, 0.992f);
		} else {
			k = 0.0f;
			break;
		}
	}
	
	if (l > 0.99f){
		tex += k;
	}
	
	d = sign(pin.Tex.y - 0.5f) * gSize.w * 0.8f;
	k = 0.0f;
	l = tex;

	for (float i = 1; i < 11; ++i){
		float n = texd(pin.Tex + float2(0.0f, d * i));

		if (n > l + 0.006f){
			k = 1.0f;
			l = min(n, 0.993f);
		} else {
			k = 0.0f;
			break;
		}
	}
	
	if (l > 0.99f){
		tex += k;
	}
	
	if (tex < 0.99f){
		tex = 1.0f - clamp(tex, 0.36f, 0.64f);
	} else {
		tex = 0.0f;
	}

    return float4(tex, tex, tex, 1.0f);
}

technique10 Base {
	pass P0 {
		SetVertexShader( CompileShader( vs_4_0, vs_main() ) );
		SetGeometryShader( NULL );
		SetPixelShader( CompileShader( ps_4_0, ps_Base() ) );
	}
}

float4 ps_ShadowBlur(PS_IN pin, uniform bool gHorizontalBlur) : SV_Target {
	float2 texOffset;

	if (gHorizontalBlur){
		texOffset = float2(gSize.z, 0.0);
	} else {
		texOffset = float2(0.0, gSize.w);
	}

	float color = 0;
	for (float i = -8; i <= 8; ++i){
		float val = gInputMap.SampleLevel(samInputImage, pin.Tex + i * texOffset, 0.0).r;
		color += val;
	}
	
    return color / 17;
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

float4 ps_Final(PS_IN pin) : SV_Target {
	return saturate(tex(pin.Tex) * 2.0 - 0.5);
}

technique10 Final {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Final()));
	}
}

