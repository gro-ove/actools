struct VS_IN {
    float3 pos    : POSITION;
    float3 nor    : NORMAL;
	float2 tex    : TEXCOORD;
};

struct PS_IN {
    float4 pos : SV_POSITION;
    float4 col : COLOR;
};

cbuffer cbPerObject : register(b0) {
	float4x4 gWorldViewProj;
}

PS_IN vs_main( VS_IN input ){
    PS_IN output = (PS_IN)0;
    
    output.pos = mul(float4(input.pos, 1.0f), gWorldViewProj);
    output.col = float4(input.tex, input.nor.xy);
    
    return output;
}

float4 ps_main( PS_IN input ) : SV_Target {
    return input.col;
}

technique11 Cube { // PNT
    pass P0 {
        SetVertexShader( CompileShader( vs_4_0, vs_main() ) );
        SetGeometryShader( NULL );
        SetPixelShader( CompileShader( ps_4_0, ps_main() ) );
    }
}