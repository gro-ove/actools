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

float4 ps_track_map(PS_IN pin) : SV_Target {
	float orig = gInputImage.SampleLevel(samInputImage, pin.Tex, 0.0).a;

	float white = min(max(orig - 0.6f, 0.0f) * 10.0f, 1.0f);
	float black = min(max(orig - 0.5f, 0.0f) * 30.0f, 1.0f);

    return float4(white, white, white, black);
}

technique11 TrackMap { // PT
    pass P0 {
        SetVertexShader( CompileShader( vs_4_0, vs_main() ) );
		SetGeometryShader( NULL );
        SetPixelShader( CompileShader( ps_5_0, ps_track_map() ) );
    }
}
