#include "Common.fx"

#define uGhosts 8
#define uGhostDispersal 0.07

float4 ps_Ghosts(PS_IN pin) : SV_Target {
	float2 texcoord = -pin.Tex + (float2)1.0;

	// ghost vector to image centre:
	float2 ghostVec = ((float2)0.5 - texcoord) * uGhostDispersal;

	// sample ghosts:  
	float3 result = (float3)0.0;
	for (int i = 0; i < uGhosts; ++i) {
		float2 offset = frac(texcoord + ghostVec * (float)i);

		float weight = length((float2)(0.5) - offset) / length((float2)(0.5));
		weight = pow(1.0 - weight, 10.0);

		result += gInputMap.Sample(samInputImage, offset).rgb * weight;
	}

	return float4(result, 1.0);

}

technique10 Ghosts {
	pass P0 {
		SetVertexShader( CompileShader( vs_4_0, vs_main() ) );
		SetGeometryShader( NULL );
		SetPixelShader( CompileShader( ps_4_0, ps_Ghosts() ) );
	}
}