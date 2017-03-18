// textures
	Texture2D gDepthMap;
	Texture2D gNormalMap;
	Texture2D gNoiseMap;
	Texture2D gDitherMap;
	Texture2D gFirstStepMap;

	SamplerState samLinear {
		Filter = MIN_MAG_MIP_LINEAR;
		AddressU = CLAMP;
		AddressV = CLAMP;
	};

	SamplerState samNoise {
		Filter = MIN_MAG_MIP_POINT;
		AddressU = WRAP;
		AddressV = WRAP;
	};
    
// input resources
    cbuffer cbPerFrame : register(b0) {
        matrix gWorldViewProjInv; // what the actual fuсk
		matrix gView;

        matrix gProj;
        matrix gProjT;
        matrix gProjInv;

		float4 gViewFrustumVectors[4];
		matrix gNormalsToViewSpace;
    }	

// fn structs
    struct VS_IN {
        float3 PosL : POSITION;
        float2 Tex  : TEXCOORD;
    };

    struct PS_IN {
        float4 PosH           : SV_POSITION;
		float2 Tex            : TEXCOORD0;
		float4 FrustumVector  : FRUSTUM_VECTOR;
    };

// one vertex shader for everything
    PS_IN vs_main(VS_IN vin, uint vertexID : SV_VertexID) {
        PS_IN vout;
        vout.PosH = float4(vin.PosL, 1.0f);
        vout.Tex = vin.Tex;

		/*uint vertexID;
		if (vin.PosL.x < 0) {
			vertexID = vin.PosL.y < 0 ? 0 : 3;
		} else {
			vertexID = vin.PosL.y < 0 ? 1 : 2;
		}*/

		vout.FrustumVector = gViewFrustumVectors[vertexID];
        return vout;
    }

	float3 GetNormal(float2 coords) {
		return gNormalMap.Sample(samLinear, coords).xyz;
	}

// new
	SamplerState sourceSampler {
		Filter = MIN_MAG_MIP_POINT;
	};

	SamplerState ditherSampler {
		Filter = MIN_MAG_MIP_POINT;
		AddressU = WRAP;
		AddressV = WRAP;
	};

	cbuffer cbCameraData {
		float4x4 gProjectionMatrix;	  	// the local projection matrix
		float2 gRenderTargetResolution;	// the size of the render target
	};

	cbuffer cbShaderData {
		float2 gSampleDirections[32];
		float gStrengthPerRay = 0.1875;	// strength / gNumRays
		uint gNumRays = 8;
		uint gMaxStepsPerRay = 2;
		float gHalfSampleRadius = .25;	// sampleRadius / 2
		float gFallOff = 2.0;			// the maximum distance to count samples
		float gDitherScale;				// the ratio between the render target size and the dither texture size. Normally: gRenderTargetResolution / 4
		float gBias = .03;				// minimum factor to start counting occluders
	};

	// Unproject a value from the depth buffer to the Z value in view space.
	// Multiply the result with an interpolated frustum vector to get the actual view-space coordinates
	float DepthToViewZ(float depthValue){
		return gProjectionMatrix[3][2] / (depthValue - gProjectionMatrix[2][2]);
	}

	// snaps a uv coord to the nearest texel centre
	float2 SnapToTexel(float2 uv, float2 maxScreenCoords){
		return round(uv * maxScreenCoords) * rcp(maxScreenCoords);
	}

	// rotates a sample direction according to the row-vectors of the rotation matrix
	float2 Rotate(float2 vec, float2 rotationX, float2 rotationY){
		float2 rotated;
		// just so we can use dot product
		float3 expanded = float3(vec, 0.0);
		rotated.x = dot(expanded.xyz, rotationX.xyy);
		rotated.y = dot(expanded.xyz, rotationY.xyy);
		return rotated;
	}

	// Gets the view position for a uv coord
	/*float3 GetViewPosition(float2 uv, float2 frustumDiff){
		float depth = gDepthMap.SampleLevel(sourceSampler, uv, 0).x;
		float3 frustumVector = float3(gViewFrustumVectors[3].xy + uv * frustumDiff, 1.0);
		return frustumVector * DepthToViewZ(depth);
	}*/

	float3 N4T3(float4 v) {
		return v.xyz / v.w;
	}

	float3 GetPosition(float2 uv, float depth) {
		return N4T3(mul(float4(uv.x * 2 - 1, -(uv.y * 2 - 1), depth, 1), gWorldViewProjInv));
	}

	// Gets the view position for a uv coord
	float3 GetViewPosition(float2 uv, float2 frustumDiff){
		float3 posW = GetPosition(uv, gDepthMap.SampleLevel(sourceSampler, uv, 0).x);
		return N4T3(mul(float4(posW, 1.0), gView));
	}

	// Retrieves the occlusion factor for a particular sample
	// uv: the centre coordinate of the kernel
	// frustumVector: The frustum vector of the sample point
	// centerViewPos: The view space position of the centre point
	// centerNormal: The normal of the centre point
	// tangent: The tangent vector in the sampling direction at the centre point
	// topOcclusion: The maximum cos(angle) found so far, will be updated when a new occlusion segment has been found
	float GetSampleOcclusion(float2 uv, float3 frustumVector, float3 centerViewPos, float3 centerNormal, float3 tangent, inout float topOcclusion){
		// reconstruct sample's view space position based on depth buffer and interpolated frustum vector
		float sampleDepth = gDepthMap.SampleLevel(sourceSampler, uv, 0).x;
		float3 sampleViewPos = frustumVector * DepthToViewZ(sampleDepth);

		// get occlusion factor based on candidate horizon elevation
		float3 horizonVector = sampleViewPos - centerViewPos;
		float horizonVectorLength = length(horizonVector);

		float occlusion;

		// If the horizon vector points away from the tangent, make an estimate
		if (dot(tangent, horizonVector) < 0.0)
			return 0.5;
		else
			occlusion = dot(centerNormal, horizonVector) / horizonVectorLength;

		// this adds occlusion only if angle of the horizon vector is higher than the previous highest one without branching
		float diff = max(occlusion - topOcclusion, 0.0);
		topOcclusion = max(occlusion, topOcclusion);

		// attenuate occlusion contribution using distance function 1 - (d/f)^2
		float distanceFactor = saturate(horizonVectorLength / gFallOff);
		distanceFactor = 1.0 - distanceFactor * distanceFactor;
		return diff * distanceFactor;
	}

	// Retrieves the occlusion for a given ray
	// origin: The uv coordinates of the ray origin (the AO kernel centre)
	// direction: The direction of the ray
	// jitter: The random jitter factor by which to offset the start position
	// maxScreenCoords: The maximum screen position (the texel that corresponds with uv = 1)
	// projectedRadii: The sample radius in uv space
	// numStepsPerRay: The amount of samples to take along the ray
	// centerViewPos: The view space position of the centre point
	// centerNormal: The normal of the centre point
	// frustumDiff: The difference between frustum vectors horizontally and vertically, used for frustum vector interpolation
	float GetRayOcclusion(float2 origin, float2 direction, float jitter, float2 maxScreenCoords, float2 projectedRadii, uint numStepsPerRay, float3 centerViewPos, float3 centerNormal, float2 frustumDiff){
		// calculate the nearest neighbour sample along the direction vector
		float2 texelSizedStep = direction * rcp(gRenderTargetResolution);
		direction *= projectedRadii;

		// gets the tangent for the current ray, this will be used to handle opposing horizon vectors
		// Tangent is corrected with respect to per-pixel normal by projecting it onto the tangent plane defined by the normal
		float3 tangent = GetViewPosition(origin + texelSizedStep, frustumDiff) - centerViewPos;
		tangent -= dot(centerNormal, tangent) * centerNormal;
		tangent = normalize(tangent);

		// calculate uv increments per marching step, snapped to texel centres to avoid depth discontinuity artefacts
		float2 stepUV = SnapToTexel(direction.xy / (numStepsPerRay - 1), maxScreenCoords);

		// jitter the starting position for ray marching between the nearest neighbour and the sample step size
		float2 jitteredOffset = lerp(texelSizedStep, stepUV, jitter);
		float2 uv = SnapToTexel(origin + jitteredOffset, maxScreenCoords);

		// initial frustum vector matching the starting position and its per-step increments
		float3 frustumVector = float3(gViewFrustumVectors[3].xy + uv * frustumDiff, 1.0);
		float2 frustumVectorStep = stepUV * frustumDiff;

		// top occlusion keeps track of the occlusion contribution of the last found occluder.
		// set to gBias value to avoid near-occluders
		float topOcclusion = gBias;
		float occlusion = 0.0;

		// march!
		for (uint step = 0; step < numStepsPerRay; ++step) {
			occlusion += GetSampleOcclusion(uv, frustumVector, centerViewPos, centerNormal, tangent, topOcclusion);

			uv += stepUV;
			frustumVector.xy += frustumVectorStep.xy;
		}

		return occlusion;
	}

	//float3 reconstructNormalVS(float3 positionVS) {
	//	return normalize(cross(ddx(positionVS), ddy(positionVS)));
	//}

	//float3 VSPositionFromDepth(float2 vTexCoord){
	//	// Get the depth value for this pixel
	//	float z = gDepthMap.SampleLevel(sourceSampler, vTexCoord, 0).x;
	//	// Get x/w and y/w from the viewport position
	//	float x = vTexCoord.x * 2 - 1;
	//	float y = (1 - vTexCoord.y) * 2 - 1;
	//	float4 vProjectedPos = float4(x, y, z, 1.0f);
	//	// Transform by the inverse projection matrix
	//	float4 vPositionVS = mul(vProjectedPos, gProjInv);
	//	// Divide by w to get the view-space position
	//	return vPositionVS.xyz / vPositionVS.w;
	//}

	/*float3 GetPositionV(in float2 uv){
		// Get the depth value for this pixel
		float z = SampleDepthBuffer(uv);

		float x = uv.x * 2 - 1;
		float y = (1 - uv.y) * 2 - 1;
		float4 vProjectedPos = float4(x, y, z, 1.0f);

		// Transform by the inverse projection matrix
		float4 vPositionVS = mul(vProjectedPos, InverseProjection);

		// Divide by w to get the view-space position
		vPositionVS.z = -vPositionVS.z;

		// solution - discard texel values at depth = 1
		if (z == 1.0)
			return float3(0, 0, 0);

		return vPositionVS.xyz / vPositionVS.w;
	}*/

	#include "HBAO.Found.fx"

    float4 ps_Hbao(PS_IN pin) : SV_Target {
		// normally, you'd pass this in as a constant, but placing it here makes things easier to understand.
		// basically, this is just so we can use UV coords to interpolate frustum vectors
		float2 frustumDiff = float2(gViewFrustumVectors[2].x - gViewFrustumVectors[3].x, gViewFrustumVectors[0].y - gViewFrustumVectors[3].y);

		// The maximum screen position (the texel that corresponds with uv = 1), used to snap to texels
		// (normally, this would be passed in as a constant)
		float2 maxScreenCoords = gRenderTargetResolution - 1.0;

		// reconstruct view-space position from depth buffer
		float centerDepth = gDepthMap.SampleLevel(sourceSampler, pin.Tex, 0).x;
		float3 centerViewPos = pin.FrustumVector.xyz * DepthToViewZ(centerDepth);

		// return float4(centerViewPos * 0.5 + 0.5, 1.0);;

		// unpack normal
		float3 posW = GetPosition(pin.Tex, centerDepth);
		float3 normalW = gNormalMap.SampleLevel(sourceSampler, pin.Tex, 0).xyz;
		float3 posPlusNormalW = posW + normalW;

		float3 posV = N4T3(mul(float4(posW, 1.0), gView));
		float3 posPlusNormalV = N4T3(mul(float4(posPlusNormalW, 1.0), gView));
		float3 normalV = normalize(posPlusNormalV - posV);

		//float3 centerNormal = normalize(mul(float4(normalW, 0.0), gNormalsToViewSpace).xyz);
		//float3 centerNormal = normalize(mul(normalW, (float3x3)gNormalsToViewSpace));
		//float3 centerNormal = normalize(normalW);
		//float3 centerNormal = reconstructNormalVS(centerViewPos);
		//float3 centerNormal = normalize(mul(normalW, (float3x3)gNormalsToViewSpace).xyz - mul((float3)0, (float3x3)gNormalsToViewSpace).xyz);
		float3 centerNormal = normalV;

		//return float4(posW, 1.0);

		if (pin.Tex.x > 0.25 && pin.Tex.x < 0.3 || pin.Tex.x > 0.75 && pin.Tex.x < 0.8) {
			return float4(posV, 1.0);
		}

		//return float4(posV, 1.0);
		//return Test(posV, centerNormal, 0.0, frustumDiff);
		

		//centerViewPos = posV;
		// return float4(posV, 1.0);

		// Get the random factors and construct the row vectors for the 2D matrix from cos(a) and -sin(a) to rotate the sample directions
		float3 randomFactors = gDitherMap.SampleLevel(ditherSampler, pin.Tex * gDitherScale, 0).rgb;
		float2 rotationX = randomFactors.xy;
		float2 rotationY = rotationX.yx * float2(-1.0f, 1.0f);

		// return CalcHbao(centerViewPos, centerNormal, 0, frustumDiff);

		// scale the sample radius perspectively according to the given view depth (becomes ellipse)
		float w = centerViewPos.z * gProjectionMatrix[2][3] + gProjectionMatrix[3][3];
		float2 projectedRadii = gHalfSampleRadius * float2(gProjectionMatrix[1][1], gProjectionMatrix[2][2]) / w;	// half radius because projection ([-1, 1]) -> uv ([0, 1])
		float screenRadius = projectedRadii.x * gRenderTargetResolution.x;

		// bail out if there's nothing to march
		if (screenRadius < 1.0)
			return 1.0;

		// do not take more steps than there are pixels		
		uint numStepsPerRay = min(gMaxStepsPerRay, screenRadius);

		float totalOcclusion = 0.0;

		if ((pin.Tex.x > 0.25 && pin.Tex.x < 0.3 || pin.Tex.x > 0.75 && pin.Tex.x < 0.8)) {
			return 1.0 - GetRayOcclusion(pin.Tex, float2(0, -1), randomFactors.z, maxScreenCoords, projectedRadii, numStepsPerRay, centerViewPos, centerNormal, frustumDiff);
		}

		float v = GetRayOcclusion(pin.Tex, float2(0, 1), randomFactors.z, maxScreenCoords, projectedRadii, numStepsPerRay, centerViewPos, centerNormal, frustumDiff);
		v += GetRayOcclusion(pin.Tex, float2(1, 0), randomFactors.z, maxScreenCoords, projectedRadii, numStepsPerRay, centerViewPos, centerNormal, frustumDiff);
		v += GetRayOcclusion(pin.Tex, float2(-1, 0), randomFactors.z, maxScreenCoords, projectedRadii, numStepsPerRay, centerViewPos, centerNormal, frustumDiff);
		v += GetRayOcclusion(pin.Tex, float2(0, -1), randomFactors.z, maxScreenCoords, projectedRadii, numStepsPerRay, centerViewPos, centerNormal, frustumDiff);
		return 1.0 - v / 4;

		/*for (uint i = 0; i < gNumRays; ++i) {
			float2 sampleDir = Rotate(gSampleDirections[i].xy, rotationX, rotationY);
			totalOcclusion += GetRayOcclusion(pin.Tex, sampleDir, randomFactors.z, maxScreenCoords, projectedRadii, numStepsPerRay, centerViewPos, centerNormal, frustumDiff);
		}*/

		return 1.0 - saturate(gStrengthPerRay * totalOcclusion);
    }

    technique11 Hbao {
        pass P0 {
            SetVertexShader( CompileShader( vs_5_0, vs_main() ) );
            SetGeometryShader( NULL );
            SetPixelShader( CompileShader( ps_5_0, ps_Hbao() ) );
        }
    }