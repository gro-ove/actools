// Skip translation

#define BLOCKER_SEARCH_NUM_SAMPLES 81
#define PCF_NUM_SAMPLES 81
#define SCENE_SCALE 0.01
#define LIGHT_SIZE 0.4

cbuffer POISSON_DISKS {
	float2 poissonDisk[81] = {
		float2(-0.304967, -0.058754),
		float2(-0.043598, -0.452767),
		float2(-0.642527, 0.799442),
		float2(0.640218, 0.596450),
		float2(0.659291, -0.773008),
		float2(-0.178993, -0.872160),
		float2(0.276105, -0.820578),
		float2(0.580867, -0.117841),
		float2(-0.522690, -0.634022),
		float2(0.089258, 0.226401),
		float2(0.928508, 0.051883),
		float2(-0.868488, 0.350548),
		float2(0.941204, 0.957318),
		float2(-0.495066, 0.394927),
		float2(-0.125758, 0.984469),
		float2(-0.955218, -0.603685),
		float2(0.795363, -0.541541),
		float2(0.934481, 0.482255),
		float2(-0.755124, -0.059173),
		float2(0.311857, 0.783972),
		float2(-0.959199, -0.966749),
		float2(-0.065384, 0.641868),
		float2(0.923424, -0.935969),
		float2(0.040538, -0.118669),
		float2(-0.996426, 0.082208),
		float2(-0.613901, -0.982514),
		float2(0.236256, -0.397797),
		float2(0.609993, 0.962312),
		float2(-0.950827, 0.948278),
		float2(0.490153, 0.189333),
		float2(0.304875, 0.546167),
		float2(-0.263125, 0.281158),
		float2(-0.572705, 0.141030),
		float2(0.954722, -0.313642),
		float2(-0.346067, -0.330249),
		float2(-0.723442, -0.474052),
		float2(-0.383928, 0.651586),
		float2(0.286045, 0.018058),
		float2(-0.942373, 0.674203),
		float2(0.084795, -0.954489),
		float2(-0.948848, -0.187677),
		float2(0.535156, -0.407324),
		float2(0.659832, 0.351442),
		float2(-0.254076, -0.534739),
		float2(0.896457, 0.264414),
		float2(-0.396225, -0.983286),
		float2(-0.417622, 0.924533),
		float2(0.911242, 0.718191),
		float2(0.976459, -0.699264),
		float2(-0.116580, 0.108363),
		float2(0.319926, -0.220776),
		float2(-0.712503, 0.505410),
		float2(-0.774058, -0.812883),
		float2(0.090694, 0.829392),
		float2(0.330998, -0.608571),
		float2(-0.548603, -0.384875),
		float2(0.118297, 0.550209),
		float2(-0.919727, -0.400404),
		float2(0.024899, -0.646427),
		float2(-0.732525, 0.986321),
		float2(-0.060302, 0.361266),
		float2(0.178028, 0.986945),
		float2(0.527828, -0.931745),
		float2(-0.233099, 0.799864),
		float2(0.297159, 0.248822),
		float2(0.756461, -0.305501),
		float2(0.456517, 0.387692),
		float2(-0.533354, -0.140567),
		float2(0.739327, 0.111377),
		float2(-0.339785, -0.741848),
		float2(-0.755231, -0.263203),
		float2(0.489712, 0.714908),
		float2(-0.236110, 0.472629),
		float2(0.547581, -0.613419),
		float2(-0.643593, 0.332322),
		float2(0.736407, -0.057415),
		float2(0.064937, -0.287273),
		float2(-0.320082, 0.112393),
		float2(-0.992819, 0.524451),
		float2(-0.762316, 0.123410),
		float2(0.731807, -0.986733)
	};
};

#define MAX_LINEAR_DEPTH 1e30f

SamplerState PointSampler {
	Filter = MIN_MAG_MIP_POINT;
	AddressU = Border;
	AddressV = Border;
	BorderColor = float4(MAX_LINEAR_DEPTH, 0, 0, 0);
};

SamplerComparisonState PCF_Sampler {
	ComparisonFunc = LESS;
	Filter = COMPARISON_MIN_MAG_LINEAR_MIP_POINT;
	AddressU = Border;
	AddressV = Border;
	BorderColor = float4(MAX_LINEAR_DEPTH, 0, 0, 0);
};

float PenumbraSize(float zReceiver, float zBlocker){
	return (zReceiver - zBlocker) * LIGHT_SIZE / zBlocker;
}

void FindBlocker(Texture2D shadowMapTex, out float avgBlockerDepth, out float numBlockers, float2 uv, float zReceiver){
	// This uses similar triangles to compute what
	// area of the shadow map we should search

	float searchWidth = SCENE_SCALE * (zReceiver - 1.0f) / zReceiver;
	float blockerSum = 0;
	numBlockers = 0;
	for (int i = 0; i < BLOCKER_SEARCH_NUM_SAMPLES; ++i){
		float shadowMapDepth = shadowMapTex.SampleLevel(PointSampler, uv + poissonDisk[i] * searchWidth, 0).x;
		[flatten]
		if (shadowMapDepth < zReceiver) {
			blockerSum += shadowMapDepth;
			numBlockers++;
		}
	}

	[flatten]
	if (numBlockers != 0) {
		avgBlockerDepth = blockerSum / numBlockers;
	}
}

float PCF_Filter(Texture2D shadowMapTex, float2 uv, float zReceiver, float filterRadiusUV){
	float sum = 0.0f;
	for (int i = 0; i < PCF_NUM_SAMPLES; ++i){
		float2 offset = poissonDisk[i] * filterRadiusUV;
		sum += shadowMapTex.SampleCmpLevelZero(PCF_Sampler, uv + offset, zReceiver);
	}
	return sum / PCF_NUM_SAMPLES;
}

float PCSS(Texture2D shadowMapTex, float3 coords){
	float2 uv = coords.xy;
	float zReceiver = coords.z; // Assumed to be eye-space z in this code

								// STEP 1: blocker search
	float avgBlockerDepth = 0;
	float numBlockers = 0;
	FindBlocker(shadowMapTex, avgBlockerDepth, numBlockers, uv, zReceiver);

	if (numBlockers < 1)
		//There are no occluders so early out (this saves filtering)
		return 1.0f;

	// STEP 2: penumbra size
	float filterRadiusUV = PenumbraSize(zReceiver, avgBlockerDepth);

	// STEP 3: filtering
	return PCF_Filter(shadowMapTex, uv, zReceiver, filterRadiusUV);
}