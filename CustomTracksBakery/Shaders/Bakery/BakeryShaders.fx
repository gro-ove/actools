SamplerState samLinear {
	Filter = MIN_MAG_MIP_LINEAR;
	AddressU = WRAP;
	AddressV = WRAP;
};

Texture2D gDiffuseMap;
Texture2D gMaskMap;
Texture2D gDetailRMap;
Texture2D gDetailGMap;
Texture2D gDetailBMap;
Texture2D gDetailAMap;
Texture2D gAlphaMap;

cbuffer cbPerObject : register(b0) {
	matrix gWorldViewProj;
	float4 gMultRGBA;
	float gKsDiffuse;
	float gAlphaRef;
	float gMagicMult;
	float gSecondPassMode;
}

struct VS_IN {
 	float3 PosL       : POSITION;
 	float3 NormalL    : NORMAL;
 	float2 Tex        : TEXCOORD;
 	float3 TangentL   : TANGENT;
 };

struct PS_IN {
	float4 PosH : SV_POSITION;
	float3 PosW : POSITION;
	float3 NormalW : NORMAL;
	float2 Tex : TEXCOORD;
};

PS_IN vs_PerPixel(VS_IN vin) {
	PS_IN vout;
	vout.PosH = mul(float4(vin.PosL, 1.0f), gWorldViewProj);
	vout.PosW = vin.PosL;
	vout.NormalW = vin.NormalL;
	vout.Tex = vin.Tex;
	return vout;
}

float4 ps_PerPixel(PS_IN pin) : SV_Target {
    float4 diffuse = gDiffuseMap.Sample(samLinear, pin.Tex);
	clip(diffuse.a - gAlphaRef - 0.0001);
	return float4(diffuse.xyz * gKsDiffuse * (pin.NormalW.y * 0.27 + 0.73), 1.0);
}

technique10 PerPixel {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_PerPixel()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_PerPixel()));
	}
}

float4 ps_MultiLayer(PS_IN pin) : SV_Target {
    float4 txDiffuseValue = gDiffuseMap.Sample(samLinear, pin.Tex);
    float4 txMaskValue = gMaskMap.Sample(samLinear, pin.Tex);
    float4 txDetailRValue = gDetailRMap.Sample(samLinear, pin.PosW.xz * gMultRGBA.r);
    float4 txDetailGValue = gDetailGMap.Sample(samLinear, pin.PosW.xz * gMultRGBA.g);
    float4 txDetailBValue = gDetailBMap.Sample(samLinear, pin.PosW.xz * gMultRGBA.b);
    float4 txDetailAValue = gDetailAMap.Sample(samLinear, pin.PosW.xz * gMultRGBA.a);

    float4 combined =
          txDetailRValue * txMaskValue.x
        + txDetailGValue * txMaskValue.y
        + txDetailBValue * txMaskValue.z
        + txDetailAValue * txMaskValue.w;
    txDiffuseValue *= combined;
    txDiffuseValue *= gMagicMult;

	return float4(txDiffuseValue.xyz * (pin.NormalW.y * 0.27 + 0.73), 1.0);
}

technique10 MultiLayer {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_PerPixel()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_MultiLayer()));
	}
}

struct secondPass_PS_IN {
	float4 PosH : SV_POSITION;
	float3 PosW : POSITION;
	float3 NormalW : NORMAL;
	float2 Tex : TEXCOORD;
	float3 AoColor : COLOR;
};

secondPass_PS_IN vs_PerPixel_SecondPass(VS_IN vin) {
	secondPass_PS_IN vout;
	vout.PosH = mul(float4(vin.PosL, 1.0f), gWorldViewProj);
	vout.PosW = vin.PosL;
	vout.NormalW = vin.NormalL;
	vout.Tex = vin.Tex;
	vout.AoColor = gSecondPassMode == 1
	    ? 1 - length(vin.TangentL) / 1e7
	    : vin.TangentL / 1e7 + 1;
	return vout;
}

float4 ps_PerPixel_SecondPass(secondPass_PS_IN pin) : SV_Target {
    float4 diffuse = gDiffuseMap.Sample(samLinear, pin.Tex);
	clip(diffuse.a - gAlphaRef - 0.0001);
	return float4(diffuse.xyz * pin.AoColor * gKsDiffuse, 1.0);
}

technique10 PerPixel_SecondPass {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_PerPixel_SecondPass()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_PerPixel_SecondPass()));
	}
}

float4 ps_MultiLayer_SecondPass(secondPass_PS_IN pin) : SV_Target {
    float4 txDiffuseValue = gDiffuseMap.Sample(samLinear, pin.Tex);
    float4 txMaskValue = gMaskMap.Sample(samLinear, pin.Tex);
    float4 txDetailRValue = gDetailRMap.Sample(samLinear, pin.PosW.xz * gMultRGBA.r);
    float4 txDetailGValue = gDetailGMap.Sample(samLinear, pin.PosW.xz * gMultRGBA.g);
    float4 txDetailBValue = gDetailBMap.Sample(samLinear, pin.PosW.xz * gMultRGBA.b);
    float4 txDetailAValue = gDetailAMap.Sample(samLinear, pin.PosW.xz * gMultRGBA.a);

    float4 combined =
          txDetailRValue * txMaskValue.x
        + txDetailGValue * txMaskValue.y
        + txDetailBValue * txMaskValue.z
        + txDetailAValue * txMaskValue.w;
    txDiffuseValue *= combined;
    txDiffuseValue *= gMagicMult;

	return float4(txDiffuseValue.xyz * pin.AoColor, 1.0);
}

technique10 MultiLayer_SecondPass {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_PerPixel_SecondPass()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_MultiLayer_SecondPass()));
	}
}