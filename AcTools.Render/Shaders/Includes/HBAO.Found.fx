float2 PositionToTexCoord(float3 position){
	float4 proj = mul(float4(position, 1), gProj);
	proj /= proj.w;

	float2 texCoord = (proj.xy + float2(1, 1)) / 2;
	texCoord.y = 1 - texCoord.y;

	return texCoord;
}

float4 Test(float3 P, float3 n, float randOffset, float2 frustumDiff) {
	const int nSampleDir = 6;
	const int nSampleRadius = 6;
	const float AORadius = 0.5;

	// precompute some values
	float occlusion = 0.0f;

	//return float4(n, 1.0);
	//return float4(P, 1.0);

	//return float4(PositionToTexCoord(P), 0.0, 1.0);
	//return float4(GetViewPosition(PositionToTexCoord(P), frustumDiff), 1.0);

	// Sample in every direction
	for (int i = 0; i < nSampleDir; i++) {
		float theta = i * 6.2831 / nSampleDir + randOffset * 6.28;
		float3 dir = normalize(float3(sin(theta), 0, cos(theta)));

		// tangent vector int the plane define by theta angle
		float3 T = normalize(dir - dot(n, dir) * n);
		float  alphaT = atan(-T.y / length(T.xz));

		float alphaH = alphaT;
		float finalRadius = 0;
		float value = 0;

		// Find horizon angle
		for (int k = 1; k <= nSampleRadius; k++) {
			float radius = k * AORadius / nSampleRadius;
			float3 offset = radius * normalize(float3(T.x, 0, T.z));

			float3 S = GetViewPosition(PositionToTexCoord(P + k * offset), frustumDiff);
			float3 H = S - P;
			float  h = length(H);

			float ah = atan(-H.y / length(H.xz));

			if (ah > alphaH && h < AORadius) {
				alphaH = ah;
				finalRadius = h;
				value = max(dot(normalize(H), n), 0);
			}
		}

		// Compute occlusion only if we are higher
		// than a certain bias
		if (alphaH - alphaT > 3.14 / 6) {
		//if (alphaH - alphaT > 0.000001) {
			// the value used is different from the paper of NVidia
			occlusion += (1 - finalRadius * finalRadius / (AORadius * AORadius)) * value;
		}
	}

	//return occlusion;
	float access = pow(clamp(1 - occlusion / nSampleDir, 0, 1), 4);
	return access;
}

float CalcHbao(float3 P, float3 n, float randOffset, float2 frustumDiff) {
	const int nSampleDir = 6;
	const int nSampleRadius = 6;
	const float AORadius = 0.5;

	// precompute some values
	float occlusion = 0.0f;

	// Sample in every direction
	for (int i = 0; i < nSampleDir; i++){
		float theta = i * 6.2831 / nSampleDir + randOffset * 6.28;
		float3 dir = normalize(float3(sin(theta), 0, cos(theta)));

		// tangent vector int the plane define by theta angle
		float3 T = normalize(dir - dot(n, dir) * n);
		float  alphaT = atan(-T.y / length(T.xz));

		float alphaH = alphaT;
		float finalRadius = 0;
		float value = 0;

		// Find horizon angle
		for (int k = 1; k <= nSampleRadius; k++){
			float radius = k * AORadius / nSampleRadius;
			float3 offset = radius * normalize(float3(T.x, 0, T.z));
			float3 S = GetViewPosition(PositionToTexCoord(P + k * offset), frustumDiff);
			float3 H = S - P;
			float  h = length(H);

			float ah = atan(-H.y / length(H.xz));

			if (ah > alphaH && h < AORadius){
				alphaH = ah;
				finalRadius = h;
				value = max(dot(normalize(H), n), 0);
			}
		}

		// Compute occlusion only if we are higher
		// than a certain bias
		if (alphaH - alphaT > 0.0001)	{
			// the value used is different from the paper of NVidia
			//occlusion += (1 - finalRadius * finalRadius / (AORadius * AORadius)) * value;
			occlusion += value;
		}
	}

	return occlusion;

	float access = pow(clamp(1 - occlusion / nSampleDir, 0, 1), 4);
	return access;
}