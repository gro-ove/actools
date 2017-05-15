#ifndef _NOISE_MAP_DEFINED
#define _NOISE_MAP_DEFINED
Texture2D gNoiseMap;
#endif

// Skip translation

#define BLOCKER_SEARCH_NUM_SAMPLES 25
#define PCF_NUM_SAMPLES 48

cbuffer POISSON_DISKS {
	float2 blockerSearchPoissonDisk[25] = {
		float2(-0.9192313f, 0.2457843f),
		float2(-0.5790563f, 0.3955263f),
		float2(-0.6346537f, -0.04642371f),
		float2(-0.9375812f, -0.2063955f),
		float2(-0.3735094f, -0.5372211f),
		float2(-0.1980452f, -0.01467185f),
		float2(-0.6887965f, -0.4922413f),
		float2(-0.1709323f, 0.5356379f),
		float2(-0.3866155f, 0.8176227f),
		float2(-0.7152032f, 0.6910197f),
		float2(0.1532712f, 0.7972272f),
		float2(0.2496267f, 0.3971553f),
		float2(0.4972632f, 0.709724f),
		float2(0.2775968f, -0.1397319f),
		float2(0.04903403f, -0.464276f),
		float2(-0.3010814f, -0.8965761f),
		float2(0.5884293f, -0.3083335f),
		float2(0.3185155f, -0.9042132f),
		float2(0.6583639f, 0.09571647f),
		float2(0.8057352f, 0.5108864f),
		float2(0.9027404f, -0.08043767f),
		float2(0.8957611f, -0.4370965f),
		float2(-0.01861715f, -0.7930358f),
		float2(0.5294089f, -0.6455017f),
		float2(-0.01366961f, 0.2491239f)
	};

	float2 poissonDisk[48] = {
		float2(0.855832f, 0.1898994f),
		float2(0.6336077f, 0.4661179f),
		float2(0.9748058f, 0.0005619526f),
		float2(0.6545785f, 0.0303187f),
		float2(0.8826399f, 0.4462606f),
		float2(0.5820866f, 0.2491302f),
		float2(0.7813198f, -0.1931429f),
		float2(0.4344696f, -0.463068f),
		float2(0.3466403f, -0.2568325f),
		float2(0.7616104f, -0.465308f),
		float2(0.3484367f, -0.6709957f),
		float2(0.1399384f, -0.1253062f),
		float2(0.06888496f, -0.5598317f),
		float2(0.3422709f, -0.007572813f),
		float2(0.1778579f, -0.8323114f),
		float2(0.6398911f, -0.7186768f),
		float2(-0.002360195f, -0.3007f),
		float2(0.3574681f, 0.2342279f),
		float2(0.3560048f, 0.5762256f),
		float2(0.4195986f, 0.8352308f),
		float2(0.05449425f, 0.8639945f),
		float2(0.6891651f, 0.6874872f),
		float2(0.1294608f, 0.6387498f),
		float2(-0.2249427f, -0.9602716f),
		float2(-0.1009111f, -0.7166787f),
		float2(-0.1966588f, -0.4243899f),
		float2(-0.3251926f, -0.7385348f),
		float2(0.1280651f, 0.1115537f),
		float2(-0.4393473f, -0.4411973f),
		float2(-0.3216322f, -0.1805354f),
		float2(-0.6028456f, -0.2775587f),
		float2(-0.2305238f, 0.8397814f),
		float2(-0.06095055f, 0.5215759f),
		float2(-0.7246163f, -0.6027799f),
		float2(0.130333f, 0.4074706f),
		float2(-0.09729999f, -0.02041483f),
		float2(-0.8133149f, -0.04492685f),
		float2(-0.4851737f, 0.03152225f),
		float2(-0.9180767f, -0.2857039f),
		float2(-0.4836228f, 0.6344832f),
		float2(-0.1267498f, 0.2698318f),
		float2(0.006003978f, -0.9894701f),
		float2(-0.3231294f, 0.4236689f),
		float2(-0.7657595f, 0.1987602f),
		float2(-0.5273226f, 0.2657178f),
		float2(-0.5848147f, -0.8110186f),
		float2(-0.8137172f, 0.5681945f),
		float2(0.4109656f, -0.9079512f)
	};
};

#define MAX_LINEAR_DEPTH 1e30f

SamplerState samPoint {
	Filter = MIN_MAG_MIP_POINT;
	AddressU = Border;
	AddressV = Border;
	BorderColor = float4(MAX_LINEAR_DEPTH, 0, 0, 0);
};

SamplerState samRandom {
	Filter = MIN_MAG_MIP_POINT;
	AddressU = Wrap;
	AddressV = Wrap;
};

SamplerComparisonState PCF_Sampler {
	ComparisonFunc = LESS;
	Filter = COMPARISON_MIN_MAG_LINEAR_MIP_POINT;
	AddressU = Border;
	AddressV = Border;
	BorderColor = float4(MAX_LINEAR_DEPTH, 0, 0, 0);
};

float PenumbraSize(float zReceiver, float zBlocker){
	return (zReceiver - zBlocker) / zBlocker;
}

void FindBlocker(Texture2D shadowMapTex, out float avgBlockerDepth, out float numBlockers, float2 uv, float zReceiver, float sceneScale){
	// This uses similar triangles to compute what
	// area of the shadow map we should search

	float searchWidth = sceneScale * (zReceiver - 1.0f) / zReceiver;
	float blockerSum = 0;
	//float blockerMin = MAX_LINEAR_DEPTH;
	//float blockerMax = 0;
	numBlockers = 0;
	for (int i = 0; i < BLOCKER_SEARCH_NUM_SAMPLES; ++i){
		float shadowMapDepth = shadowMapTex.SampleLevel(samPoint, uv + blockerSearchPoissonDisk[i] * searchWidth, 0).x;
		[flatten]
		if (shadowMapDepth < zReceiver) {
			blockerSum += shadowMapDepth;
			//blockerMin = min(blockerMin, shadowMapDepth);
			//blockerMax = max(blockerMax, shadowMapDepth);
			numBlockers++;
		}
	}

	[flatten]
	if (numBlockers != 0) {
		float avg = blockerSum / numBlockers;
		avgBlockerDepth = avg;

		// float range = blockerMax - blockerMin;
		// avgBlockerDepth = pow(max((avg - blockerMin) / range, 0.0), 0.5) * range + blockerMin;
	}
}

float PCF_Filter(Texture2D shadowMapTex, float2 uv, float zReceiver, float filterRadiusUV){
	float2 random = normalize(gNoiseMap.SampleLevel(samRandom, uv * 1000.0, 0).xy);

	float sum = 0.0f;
	for (int i = 0; i < PCF_NUM_SAMPLES; ++i){
		float2 offset = reflect(poissonDisk[i], random) * filterRadiusUV;
		sum += shadowMapTex.SampleCmpLevelZero(PCF_Sampler, uv + offset, zReceiver);
	}
	return sum / PCF_NUM_SAMPLES;
}

float PCSS_LastStep(Texture2D shadowMapTex, float2 uv, float zReceiver, float avgBlockerDepth, float lightScale) {
	// STEP 2: penumbra size
	float filterRadiusUV = min(PenumbraSize(zReceiver, avgBlockerDepth), 0.05) * lightScale + SHADOW_MAP_DX;

	// STEP 3: filtering
	return PCF_Filter(shadowMapTex, uv, zReceiver, filterRadiusUV);
}

float PCSS(Texture2D shadowMapTex, float3 coords, float sceneScale, float lightScale){
	float2 uv = coords.xy;
	float zReceiver = coords.z; // Assumed to be eye-space z in this code

								// STEP 1: blocker search
	float avgBlockerDepth = 0;
	float numBlockers = 0;
	FindBlocker(shadowMapTex, avgBlockerDepth, numBlockers, uv, zReceiver, sceneScale);

	// [branch] // produces weird warnings
	if (numBlockers < 1)
		//There are no occluders so early out (this saves filtering)
		return 1.0;

	return PCSS_LastStep(shadowMapTex, uv, zReceiver, avgBlockerDepth, lightScale);
}