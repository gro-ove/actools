// load color, normal, alpha and other params from textures or material properties

//// simplest version, just a diffuse texture
void Unpack(PS_IN pin, out float3 color, out float alpha, out float3 normal) {
	float4 diffuseValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex);

	color = diffuseValue.rgb;
	alpha = diffuseValue.a;
	normal = normalize(pin.NormalW);

	AlphaTest(alpha);
}

//// with alpha from shader
void Unpack_Alpha(PS_IN pin, out float3 color, out float alpha, out float3 normal) {
	float4 diffuseValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex);

	color = diffuseValue.rgb;
	alpha = diffuseValue.a * gAlphaMaterial.Alpha;
	normal = normalize(pin.NormalW);

	AlphaTest(alpha);
}

//// +normal map, alpha is from normal map
void Unpack_Nm(PS_IN pin, out float3 color, out float alpha, out float3 normal) {
	float4 diffuseValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex);
	float4 normalValue = gNormalMap.Sample(samAnisotropic, pin.Tex);

	color = diffuseValue.rgb;
	alpha = normalValue.a;
	normal = normalize(NormalSampleToWorldSpace(normalValue.xyz, pin.NormalW, pin.TangentW));

	AlphaTest(alpha);
}

//// same, but UV is multiplied
void Unpack_NmUvMult(PS_IN pin, out float3 color, out float alpha, out float3 normal) {
	float4 diffuseValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex * (1 + gNmUvMultMaterial.DiffuseMultiplier));
	float4 normalValue = gNormalMap.Sample(samAnisotropic, pin.Tex * (1 + gNmUvMultMaterial.NormalMultiplier));

	color = diffuseValue.rgb;
	alpha = diffuseValue.a;
	normal = normalize(NormalSampleToWorldSpace(normalValue.xyz, pin.NormalW, pin.TangentW));

	AlphaTest(alpha);
}

//// +normal map, alpha is from diffuse map
void Unpack_AtNm(PS_IN pin, out float3 color, out float alpha, out float3 normal) {
	float4 diffuseValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex);
	float4 normalValue = gNormalMap.Sample(samAnisotropic, pin.Tex);

	color = diffuseValue.rgb;
	alpha = diffuseValue.a;
	normal = normalize(NormalSampleToWorldSpace(normalValue.xyz, pin.NormalW, pin.TangentW));

	AlphaTest(alpha);
}

//// behaves like maps, but multipliers are taken from diffuse alpha
/*void Unpack_DiffMaps(PS_IN pin, out float3 color, out float alpha, out float3 normal) {
	float4 diffuseValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex);
	float4 normalValue = gNormalMap.Sample(samAnisotropic, pin.Tex);

	color = diffuseValue.rgb;
	alpha = diffuseValue.a;
	normal = normalize(NormalSampleToWorldSpace(normalValue.xyz, pin.NormalW, pin.TangentW));

	AlphaTest(alpha);
}*/

//// special version for tyres
void Unpack_Tyres(PS_IN pin, out float3 color, out float alpha, out float3 normal) {
	float4 diffuseValue = lerp(
		gDiffuseMap.Sample(samAnisotropic, pin.Tex),
		gDiffuseBlurMap.Sample(samAnisotropic, pin.Tex),
		gTyresMaterial.BlurLevel);
	float4 normalValue = lerp(
		gNormalMap.Sample(samAnisotropic, pin.Tex),
		gNormalBlurMap.Sample(samAnisotropic, pin.Tex),
		gTyresMaterial.BlurLevel);

	color = diffuseValue.rgb;
	alpha = 1.0; // diffuseValue.a;
	normal = normalize(NormalSampleToWorldSpace(normalValue.xyz, pin.NormalW, pin.TangentW));

	AlphaTest(diffuseValue.a); // alpha
}

//// maps, multipliers are for [ x: (specular power) , y: ( specular exp ), z: ( fresnel power ) ]
void Unpack_Maps(PS_IN pin, out float3 color, out float alpha, out float3 normal, out float3 mapsValue) {
	mapsValue = gMapsMap.Sample(samAnisotropic, pin.Tex).rgb;

	float4 diffuseValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex);
	float mask = diffuseValue.a;

	if (HAS_FLAG(HAS_DETAILS_MAP)) {
		float4 details = gDetailsMap.Sample(samAnisotropic, pin.Tex * gMapsMaterial.DetailsUvMultiplier);
		color = diffuseValue.rgb * (details.rgb * (1 - mask) + mask);
		mapsValue.y *= (details.a * 0.5 + 0.5);
	} else {
		color = diffuseValue.rgb;
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

	AlphaTest(alpha);
}

//// skinned maps, they are a bit weird; multipliers are for [ x: (specular power) , y: ( specular exp ), z: ( fresnel power ) ]
void Unpack_SkinnedMaps(PS_IN pin, out float3 color, out float alpha, out float3 normal, out float3 mapsValue) {
	mapsValue = gMapsMap.Sample(samAnisotropic, pin.Tex).rgb;

	float4 diffuseValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex);
	float mask = diffuseValue.a;

	if (HAS_FLAG(HAS_DETAILS_MAP)) {
		float4 details = gDetailsMap.Sample(samAnisotropic, pin.Tex * gMapsMaterial.DetailsUvMultiplier);
		color = diffuseValue.rgb * (details.rgb * (1 - mask) + mask);
		mapsValue.y *= (details.a * 0.5 + 0.5);
	} else {
		color = diffuseValue.rgb;
	}

    float4 normalValue = gNormalMap.Sample(samAnisotropic, pin.Tex);
    alpha = normalValue.a * mask;

    float blend = gMapsMaterial.DetailsNormalBlend;
    if (blend > 0.0) {
        float4 detailsNormalValue = gDetailsNormalMap.Sample(samAnisotropic, pin.Tex * gMapsMaterial.DetailsUvMultiplier);
        normalValue += (detailsNormalValue - 0.5) * blend * (1.0 - mask);
    }

    normal = normalize(NormalSampleToWorldSpace(normalValue.xyz, pin.NormalW, pin.TangentW));

	AlphaTest(alpha);
}