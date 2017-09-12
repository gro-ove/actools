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
	return pow(nDotH, max(exp, 0.1)) * level;
}

float CalculateSpecularLight(float3 normal, float3 position) {
	float nDotH = GetNDotH(normal, position);
	return CalculateSpecularLight(nDotH, gMaterial.SpecularExp, gMaterial.Specular);
}

float CalculateSpecularLight_Maps(float3 normal, float3 position, float specularExpMultiplier) {
	float nDotH = GetNDotH(normal, position);
	return CalculateSpecularLight(nDotH, gMaterial.SpecularExp * specularExpMultiplier, gMaterial.Specular);
}

float CalculateSpecularLight_Maps_Sun(float3 normal, float3 position, float specularExpMultiplier) {
	float nDotH = GetNDotH(normal, position);
	return CalculateSpecularLight(nDotH, gMaterial.SpecularExp * specularExpMultiplier, gMaterial.Specular) +
		CalculateSpecularLight(nDotH, gMapsMaterial.SunSpecularExp * specularExpMultiplier, gMapsMaterial.SunSpecular);
}

float GetDiffuseMultiplier(float3 normal) {
	return saturate(dot(normal, gLightDir));
}

float GetMainShadow_ConsiderMirror(float3 position) {
#if ENABLE_SHADOWS == 1
	[flatten]
	if (gFlatMirrored) {
		position.y = -position.y;
	}
	return GetShadow(position);
#else
	return 1.0;
#endif
}

float3 CalculateLight(float3 txColor, float3 normal, float3 position, float2 screenCoords) {
	float3 ambient = GetAmbient(normal);
	float diffuseMultiplier = GetDiffuseMultiplier(normal);

#if ENABLE_SHADOWS == 1
	diffuseMultiplier *= GetMainShadow_ConsiderMirror(position);
#endif

	ambient *= GetAo(screenCoords);

	float3 lightResult = diffuseMultiplier * gLightColor;
	float3 specular = CalculateSpecularLight(normal, position);
	return txColor * (gMaterial.Ambient * ambient + gMaterial.Diffuse * lightResult + gMaterial.Emissive) + specular * lightResult;
}

float3 CalculateLight_Maps(float3 txColor, float3 normal, float3 position, float specularMultiplier, float specularExpMultiplier, float2 screenCoords) {
	float3 ambient = GetAmbient(normal);
	float diffuseMultiplier = GetDiffuseMultiplier(normal);

#if ENABLE_SHADOWS == 1
	diffuseMultiplier *= GetMainShadow_ConsiderMirror(position);
#endif

	ambient *= GetAo(screenCoords);

	float3 lightResult = diffuseMultiplier * gLightColor;
	float3 specular = CalculateSpecularLight_Maps(normal, position, specularExpMultiplier) * specularMultiplier;
	return txColor * (gMaterial.Ambient * ambient + gMaterial.Diffuse * gLightColor * diffuseMultiplier + gMaterial.Emissive) + specular * lightResult;
}

float3 CalculateLight_Maps_Sun(float3 txColor, float3 normal, float3 position, float specularMultiplier, float specularExpMultiplier, float2 screenCoords) {
	float3 ambient = GetAmbient(normal);
	float diffuseMultiplier = GetDiffuseMultiplier(normal);

#if ENABLE_SHADOWS == 1
	diffuseMultiplier *= GetMainShadow_ConsiderMirror(position);
#endif

	ambient *= GetAo(screenCoords);

	float3 lightResult = diffuseMultiplier * gLightColor;
	float3 specular = CalculateSpecularLight_Maps_Sun(normal, position, specularExpMultiplier) * specularMultiplier;
	return txColor * (gMaterial.Ambient * ambient + gMaterial.Diffuse * lightResult + gMaterial.Emissive) + specular * lightResult;
}