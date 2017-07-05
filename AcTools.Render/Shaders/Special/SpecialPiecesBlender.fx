// textures
    Texture2D gInputMaps[9];

    SamplerState samInputImage {
        Filter = MIN_MAG_MIP_LINEAR;
		AddressU = Border;
		AddressV = Border;
		BorderColor = (float4)0;
    };

// input resources
	cbuffer cbPerObject : register(b0) {
		float2 gPaddingSize;
		float2 gTexMultiplier;
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

	float4 ps_Blend(PS_IN pin) : SV_Target {
	    float l = 1.0 - saturate(pin.Tex.x * gPaddingSize.x);
	    float r = 1.0 - saturate((1.0 - pin.Tex.x) * gPaddingSize.x);
	    float t = 1.0 - saturate(pin.Tex.y * gPaddingSize.y);
	    float b = 1.0 - saturate((1.0 - pin.Tex.y) * gPaddingSize.y);

	    float2 tex = (pin.Tex - 0.5) * gTexMultiplier + 0.5;
        float4 centerPiece = gInputMaps[0].Sample(samInputImage, tex);
        float4 topPiece = gInputMaps[1].Sample(samInputImage, tex + float2(0, gTexMultiplier.y));
        float4 topRightPiece = gInputMaps[2].Sample(samInputImage, tex + float2(-gTexMultiplier.x, gTexMultiplier.y));
        float4 rightPiece = gInputMaps[3].Sample(samInputImage, tex + float2(-gTexMultiplier.x, 0));
        float4 bottomRightPiece = gInputMaps[4].Sample(samInputImage, tex + float2(-gTexMultiplier.x, -gTexMultiplier.y));
        float4 bottomPiece = gInputMaps[5].Sample(samInputImage, tex + float2(0, -gTexMultiplier.y));
        float4 bottomLeftPiece = gInputMaps[6].Sample(samInputImage, tex + float2(gTexMultiplier.x, -gTexMultiplier.y));
        float4 leftPiece = gInputMaps[7].Sample(samInputImage, tex + float2(gTexMultiplier.x, 0));
        float4 topLeftPiece = gInputMaps[8].Sample(samInputImage, tex + float2(gTexMultiplier.x, gTexMultiplier.y));

        if (leftPiece.a < 1.0) l = 0.0;
        if (topPiece.a < 1.0) t = 0.0;
        if (bottomPiece.a < 1.0) b = 0.0;
        if (rightPiece.a < 1.0) r = 0.0;

		float lp = l * (1 - saturate(t + b));
		float rp = r * (1 - saturate(t + b));
		float tp = t * (1 - saturate(l + r));
		float bp = b * (1 - saturate(l + r));

		float tl = saturate(t - tp) * (l > 0 ? 1 : 0);
		float tr = saturate(t - tp) * (l > 0 ? 0 : 1);
		float bl = saturate(b - bp) * (l > 0 ? 1 : 0);
		float br = saturate(b - bp) * (l > 0 ? 0 : 1);

		float4 result =
			centerPiece * (1.0 - saturate(tp + rp + bp + lp + tl + tr + bl + br) * 0.5) +
		   (topPiece * tp +
			rightPiece * rp +
			bottomPiece * bp +
			leftPiece * lp +
			topRightPiece * tr +
			bottomRightPiece * br +
			bottomLeftPiece * bl +
			topLeftPiece * tl) * 0.5;

        result.a = 1.0;
        return result;
	}

	technique10 Blend {
		pass P0 {
			SetVertexShader(CompileShader(vs_4_0, vs_main()));
			SetGeometryShader(NULL);
			SetPixelShader(CompileShader(ps_4_0, ps_Blend()));
		}
	}