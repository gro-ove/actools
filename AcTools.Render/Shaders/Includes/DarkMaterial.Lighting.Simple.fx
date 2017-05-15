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

//////////////// Different material types

void CalculateLighted(PS_IN pin, out float3 lighted, out float alpha, out float3 normal) {
	float4 diffuseValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex);

	alpha = diffuseValue.a;
	normal = normalize(pin.NormalW);
	lighted = CalculateLight(diffuseValue.rgb, normal, pin.PosW, pin.PosH.xy);

	AlphaTest(alpha);
}

void CalculateLighted_Nm(PS_IN pin, out float3 lighted, out float alpha, out float3 normal) {
	float4 diffuseValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex);
	float4 normalValue = gNormalMap.Sample(samAnisotropic, pin.Tex);

	alpha = normalValue.a;
	normal = normalize(NormalSampleToWorldSpace(normalValue.xyz, pin.NormalW, pin.TangentW));
	lighted = CalculateLight(diffuseValue.rgb, normal, pin.PosW, pin.PosH.xy);

	AlphaTest(alpha);
}

void CalculateLighted_NmUvMult(PS_IN pin, out float3 lighted, out float alpha, out float3 normal) {
	float4 diffuseValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex * (1 + gNmUvMultMaterial.DiffuseMultiplier));
	float4 normalValue = gNormalMap.Sample(samAnisotropic, pin.Tex * (1 + gNmUvMultMaterial.NormalMultiplier));

	alpha = diffuseValue.a;
	normal = normalize(NormalSampleToWorldSpace(normalValue.xyz, pin.NormalW, pin.TangentW));
	lighted = CalculateLight(diffuseValue.rgb, normal, pin.PosW, pin.PosH.xy);

	AlphaTest(alpha);
}

void CalculateLighted_AtNm(PS_IN pin, out float3 lighted, out float alpha, out float3 normal) {
	float4 diffuseValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex);
	float4 normalValue = gNormalMap.Sample(samAnisotropic, pin.Tex);

	alpha = diffuseValue.a;
	normal = normalize(NormalSampleToWorldSpace(normalValue.xyz, pin.NormalW, pin.TangentW));
	lighted = CalculateLight(diffuseValue.rgb, normal, pin.PosW, pin.PosH.xy);

	AlphaTest(alpha);
}

void CalculateLighted_DiffMaps(PS_IN pin, out float3 lighted, out float alpha, out float3 normal) {
	float4 diffuseValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex);
	float4 normalValue = gNormalMap.Sample(samAnisotropic, pin.Tex);

	alpha = diffuseValue.a;
	normal = normalize(NormalSampleToWorldSpace(normalValue.xyz, pin.NormalW, pin.TangentW));
	lighted = CalculateLight_Maps(diffuseValue.rgb, normal, pin.PosW, alpha, alpha, pin.PosH.xy);

	AlphaTest(alpha);
}

void CalculateLighted_Maps(PS_IN pin, float txMapsSpecularMultiplier, float txMapsSpecularExpMultiplier, out float3 lighted, out float alpha,
		out float mask, out float3 normal) {
	float4 diffuseValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex);
	mask = diffuseValue.a;

	if (HAS_FLAG(HAS_DETAILS_MAP)) {
		float4 details = gDetailsMap.Sample(samAnisotropic, pin.Tex * gMapsMaterial.DetailsUvMultiplier);
		diffuseValue = diffuseValue * (details * (1 - mask) + mask);
		txMapsSpecularExpMultiplier *= (details.a * 0.5 + 0.5);
	}

	if (HAS_FLAG(HAS_NORMAL_MAP)) {
		float4 normalValue = gNormalMap.Sample(samAnisotropic, pin.Tex);
		alpha = HAS_FLAG(USE_NORMAL_ALPHA_AS_ALPHA) ? normalValue.a : 1.0;

		float blend = gMapsMaterial.DetailsNormalBlend;
		if (blend > 0.0) {
			float4 detailsNormalValue = gDetailsNormalMap.Sample(samAnisotropic, pin.Tex * gMapsMaterial.DetailsUvMultiplier);
			normalValue += (detailsNormalValue - 0.5) * blend * (1.0 - mask);
		}

		normal = normalize(NormalSampleToWorldSpace(normalValue.xyz, pin.NormalW, pin.TangentW));
	} else {
		normal = normalize(pin.NormalW);
		alpha = 1.0;
	}

	lighted = CalculateLight_Maps_Sun(diffuseValue.rgb, normal, pin.PosW, txMapsSpecularMultiplier, txMapsSpecularExpMultiplier, pin.PosH.xy);
	AlphaTest(alpha);
}