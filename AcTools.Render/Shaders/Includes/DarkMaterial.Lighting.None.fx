//////////////// Simple lighting

cbuffer cbLighting {
	float3 gLightDir;
	float3 gLightColor;
}

float GetNDotH(float3 normal, float3 position) {
	float3 toEye = normalize(gEyePosW - position);
	float3 halfway = normalize(toEye + gLightDir);
	return saturate(dot(halfway, normal));
}

float CalculateSpecularLight(float nDotH, float exp, float level) {
	return 0;
}

float CalculateSpecularLight(float3 normal, float3 position) {
	float nDotH = GetNDotH(normal, position);
	return 0;
}

float CalculateSpecularLight_Maps(float3 normal, float3 position, float specularExpMultiplier) {
	return 0;
}

float CalculateSpecularLight_Maps_Sun(float3 normal, float3 position, float specularExpMultiplier) {
	return 0;
}

float GetDiffuseMultiplier(float3 normal) {
	return 0;
}

float GetShadow_ConsiderMirror(float3 position) {
	[flatten]
	if (gFlatMirrored) {
		position.y = -position.y;
	}
	return GetShadow(position);
}

float3 CalculateLight(float3 txColor, float3 normal, float3 position, float2 screenCoords) {
	float3 ambient = GetAmbient(normal) * GetAo(screenCoords);
	return txColor * (gMaterial.Ambient * ambient + gMaterial.Emissive);
}

float3 CalculateLight_Maps(float3 txColor, float3 normal, float3 position, float specularMultiplier, float specularExpMultiplier, float2 screenCoords) {
	float3 ambient = GetAmbient(normal) * GetAo(screenCoords);
	return txColor * (gMaterial.Ambient * ambient + gMaterial.Emissive);
}

float3 CalculateLight_Maps_Sun(float3 txColor, float3 normal, float3 position, float specularMultiplier, float specularExpMultiplier, float2 screenCoords) {
	float3 ambient = GetAmbient(normal) * GetAo(screenCoords);
	return txColor * (gMaterial.Ambient * ambient + gMaterial.Emissive);
}