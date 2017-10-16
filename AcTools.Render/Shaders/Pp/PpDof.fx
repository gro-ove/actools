// PixelShader: DownsampleColorCoC, entry: DownsampleColorCoC
// VertexShader: VertexFullScreenDofGrid, entry: VShader
// PixelShader: BokehSprite, entry: BokehSprite
// PixelShader: ResolveBokeh, entry: ResolveBokeh
// PixelShader: ResolveBokehDebug, entry: ResolveBokeh, defines: DEBUG_BOKEH

// samplers
SamplerState samLinear {
	Filter = MIN_MAG_MIP_LINEAR;
	AddressU = CLAMP;
	AddressV = CLAMP;
};

SamplerState samPoint {
	Filter = MIN_MAG_MIP_POINT;
	AddressU = CLAMP;
	AddressV = CLAMP;
};

// input resources
cbuffer cbPerObject : register(b0) {
	float4 gScreenSize;
	float4 gScreenSizeHalfRes;
	float4 gCocScaleBias;
	float gZNear;
	float gZFar;
	float gFocusPlane;
	float gDofCoCScale;
	float gDebugBokeh;
	float gCoCLimit;
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

// actual shader
Texture2D<float4> InputTexture;
Texture2D<float4> InputTextureBokenBase;
Texture2D<float> InputTextureDepth;
Texture2D<float4> InputTextureBokeh;
Texture2D<float4> InputTextureDownscaledColor;

float CalculateCoc(float postProjDepth) {
	float CoC = gCocScaleBias.x * postProjDepth + gCocScaleBias.y;
	return CoC;
}

float LinearizeDepth(float depth) {
	// unoptimal
	return -gZFar * gZNear / (depth * (gZFar - gZNear) - gZFar);
}

float3 GammaToLinear(float3 input) {
	return pow(max(input, 0.0f), 2.2f);
}

float3 LinearToGamma(float3 input) {
	return pow(max(input, 0.0f), 1.0f / 2.2f);
}

float4 ps_DownsampleColorCoC(PS_IN pin) : SV_Target {
	float3 textureSample = InputTexture.SampleLevel(samLinear, pin.Tex, 0).rgb;
	float4 depth = InputTextureDepth.GatherRed(samPoint, pin.Tex);
	float coc = CalculateCoc(max(max(depth.x, depth.y), max(depth.z, depth.w)));
	return float4(textureSample, coc);
}

technique10 DownsampleColorCoC {
	pass P0 {
		SetVertexShader(CompileShader(vs_5_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_5_0, ps_DownsampleColorCoC()));
	}
}

struct Bokeh_PS_IN {
    float4 position : SV_POSITION;
    float4 colorAlpha : TEXCOORD0;
    float2 uv : TEXCOORD1;
};

Bokeh_PS_IN vs_bokeh(uint id : SV_VERTEXID) {
    uint quadIndex = id / 4;
    uint vertexIndex = id % 4;

    float screenWidth = gScreenSizeHalfRes.x;
    float screenWidthRcp = gScreenSizeHalfRes.z;

    float quadIndexAsFloat = quadIndex;

    float pixelY = floor(quadIndex * screenWidthRcp);
    float pixelX = quadIndex - pixelY * screenWidth;

    float4 colorAndDepth = InputTextureDownscaledColor.Load(uint3(pixelX, pixelY, 0));

    Bokeh_PS_IN vo;
    float2 position2D;
    position2D.x = (vertexIndex % 2) ? 1.0f : 0.0f;
    position2D.y = (vertexIndex & 2) ? 1.0f : 0.0f;
    vo.uv = position2D;

    // make the scale not biased in any direction
    position2D -= 0.5f;

    float near = colorAndDepth.a < 0.0f ? -1.0f : 0.0f;
    float cocScale = abs(colorAndDepth.a);

    // multiply by bokeh size + clamp max to not kill the bw
    float size = min(cocScale, gCoCLimit);
    position2D *= size;

    // rebias
    position2D += 0.5f;

    position2D += float2(pixelX, pixelY);

    // "texture space" coords
    position2D *= gScreenSizeHalfRes.zw;

    // screen space coords, near goes right, far goes left
    position2D = saturate(position2D) * float2(1.0f, -2.0f) + float2(near, 1.0f);

    vo.position.xy = position2D;
    vo.position.z = 0.0f;

    // if in focus, cull it out
    vo.position.w = (cocScale < 1.0f) ? -1.0f : 1.0f;
    vo.colorAlpha = float4(colorAndDepth.rgb, 1.0f * rcp (size * size));
    return vo;
}

float4 ps_BokehSprite(Bokeh_PS_IN i) : SV_Target {
    float3 bokehSample = InputTextureBokenBase.Sample(samLinear, i.uv).rgb;
    float bokehLuminance = dot(bokehSample, float3(0.299f, 0.587f, 0.114f));
    return float4(bokehSample * i.colorAlpha.rgb, bokehLuminance) * i.colorAlpha.aaaa;
}

technique10 BokehSprite {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_bokeh()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_BokehSprite()));
	}
}

// #define DEBUG_BOKEH 1

float4 ps_ResolveBokeh(PS_IN pin) : SV_Target {
    float4 farPlaneColor = InputTextureBokeh.SampleLevel(samLinear, pin.Tex * float2(0.5f, 1.0f) + float2(0.5f, 0.0f), 0);
    float4 nearPlaneColor = InputTextureBokeh.SampleLevel(samLinear, pin.Tex * float2(0.5f, 1.0f), 0);

    float4 origColor = InputTexture.SampleLevel(samPoint, pin.Tex, 0);
    float4 downsampledColor = InputTextureDownscaledColor.SampleLevel(samLinear, pin.Tex, 0);

    float coc = downsampledColor.a;

    float3 farColor = farPlaneColor.rgb / max(farPlaneColor.a, 0.0001f);
    float3 nearColor = nearPlaneColor.rgb / max(nearPlaneColor.a, 0.0001f);

#if DEBUG_BOKEH
    farColor = float3(1.0f, 0.0f, 0.0f);
    nearColor = float3(0.0f, 0.0f, 1.0f);
    downsampledColor.rgb = float3(0.0f, 1.0f, 0.0f);
    origColor.rgb = float3(1.0f, 1.0f, 1.0f);
#endif

    // we must take into account the fact that we avoided drawing sprites of size 1 (optimization), only bigger - both for near and far
    float3 blendedFarFocus = lerp(downsampledColor.rgb, farColor, saturate(coc - 2.0f));

    // this one is hack to smoothen the transition - we blend between low res and high res in < 1 half res pixel transition zone
    blendedFarFocus = lerp(origColor.rgb, blendedFarFocus, saturate(0.5f * coc-1.0f));

    // we have 2 factors:
    // 1. one is scene CoC - if it is supposed to be totally blurry, but feature was thin,
    // we will have an artifact and cannot do anything about it :( as we do not know fragments behind contributing to it
    // 2. second one is accumulated, scattered bokeh intensity. Note "magic" number of 8.0f - to have it proper, I would have to
    // calculate true coverage per mip of bokeh texture - "normalization factor" - or the texture itself should be float/HDR normalized to impulse response.
    // For the demo purpose I hardcoded some value.
    float3 finalColor = lerp(blendedFarFocus, nearColor, saturate(saturate(-coc - 1.0f) + nearPlaneColor.aaa * 8.0f));

    return float4(finalColor, 1.0f);
}

technique10 ResolveBokeh {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_ResolveBokeh()));
	}
}