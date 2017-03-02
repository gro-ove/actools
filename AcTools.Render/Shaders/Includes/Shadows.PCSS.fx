// Skip translation

#define BLOCKER_SEARCH_NUM_SAMPLES 75
#define PCF_NUM_SAMPLES 75
#define SCENE_SCALE 0.05
#define LIGHT_SIZE 0.4

cbuffer POISSON_DISKS {
	float2 poissonDisk[75] = {
		float2(0.3964394f, -0.8171995f),
		float2(0.2955409f, -0.5140343f),
		float2(0.1189529f, -0.7227168f),
		float2(0.462379f, -0.6427346f),
		float2(0.197204f, -0.9199736f),
		float2(0.5707584f, -0.8207682f),
		float2(0.619253f, -0.3867192f),
		float2(0.4557119f, -0.3263471f),
		float2(0.7689291f, -0.5033466f),
		float2(0.864945f, -0.3214218f),
		float2(0.6886449f, -0.08684903f),
		float2(0.4092027f, -0.07497749f),
		float2(0.8199115f, 0.1084751f),
		float2(0.4884825f, 0.1071999f),
		float2(0.8684561f, -0.151078f),
		float2(0.9910957f, -0.02976874f),
		float2(0.6940469f, -0.6597012f),
		float2(0.5251073f, 0.2759472f),
		float2(0.9467363f, 0.2806428f),
		float2(0.8675836f, 0.4559746f),
		float2(0.7786181f, 0.3087572f),
		float2(0.6425083f, 0.4184967f),
		float2(0.3783748f, 0.3955769f),
		float2(0.2163459f, 0.3347287f),
		float2(0.3403968f, 0.2060125f),
		float2(0.0717031f, 0.4417988f),
		float2(-0.00249392f, 0.2790409f),
		float2(0.1499164f, 0.07976127f),
		float2(0.1928585f, 0.6507974f),
		float2(0.3667813f, 0.5881755f),
		float2(0.1850111f, -0.1630209f),
		float2(0.1615383f, -0.4040257f),
		float2(0.6949322f, 0.6880403f),
		float2(0.5114231f, 0.7117831f),
		float2(0.6612711f, 0.1728285f),
		float2(-0.04155184f, -0.9427902f),
		float2(0.4414584f, 0.884654f),
		float2(-0.04835455f, 0.1055595f),
		float2(-0.1705874f, 0.508397f),
		float2(-0.2102747f, 0.3112007f),
		float2(-0.254422f, 0.1194162f),
		float2(-0.1534811f, 0.6870138f),
		float2(-0.2958196f, 0.8075609f),
		float2(-0.452293f, 0.6641834f),
		float2(-0.3896366f, 0.4588901f),
		float2(-0.4840627f, 0.162332f),
		float2(-0.6538553f, 0.7491403f),
		float2(-0.6729639f, 0.5009302f),
		float2(-0.4964786f, 0.8393756f),
		float2(-0.02808823f, -0.1125082f),
		float2(-0.1029722f, 0.8536173f),
		float2(0.05267743f, 0.9565226f),
		float2(-0.3073363f, -0.8367099f),
		float2(-0.1272667f, -0.7146859f),
		float2(-0.0837204f, -0.5036702f),
		float2(-0.3919785f, -0.1725451f),
		float2(-0.1877125f, -0.1838812f),
		float2(-0.6319329f, 0.2922871f),
		float2(-0.892957f, 0.201415f),
		float2(-0.844521f, 0.4509015f),
		float2(-0.7022541f, 0.07104941f),
		float2(0.2325914f, 0.8375258f),
		float2(-0.6067279f, -0.7296634f),
		float2(-0.4362467f, -0.549321f),
		float2(-0.6894493f, -0.4268726f),
		float2(-0.2514981f, -0.3616334f),
		float2(-0.5442315f, -0.2598025f),
		float2(0.02535145f, 0.7043346f),
		float2(-0.4557482f, -0.009153401f),
		float2(-0.7663495f, -0.1393502f),
		float2(-0.9448522f, -0.1056035f),
		float2(-0.2709835f, -0.6020311f),
		float2(-0.895565f, -0.4193061f),
		float2(-0.080726f, -0.3165345f),
		float2(-0.804958f, -0.5882706f)
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