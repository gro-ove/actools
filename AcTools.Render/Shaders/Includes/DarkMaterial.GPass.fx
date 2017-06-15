// #include "DarkMaterial.Base.fx"

//////////////// For SSLR?

struct PS_OUT {
	float4 BaseReflection : SV_Target0;
	float4 Normal : SV_Target1;
	float Depth : SV_Target2;
};

cbuffer cbGPass {
	bool gGPassTransparent;
	float gGPassAlphaThreshold;
};

void GPassAlphaTest(float alpha) {
	clip(alpha - gGPassAlphaThreshold);
}

float4 GetReflection(float3 posW, float3 normal, float alpha) {
	float3 toEyeW = normalize(gEyePosW - posW);
	float3 reflected = reflect(-toEyeW, normal);
	float3 refl = GetReflection(reflected, gMaterial.SpecularExp);

	float val = GetReflectionStrength(normal, toEyeW);
	if (!HAS_FLAG(IS_ADDITIVE)) {
		alpha = alpha + val * (1 - alpha);
	}

	GPassAlphaTest(alpha);
	return float4(refl, (gGPassTransparent ? saturate(alpha + val) : 1.0) * val);
}

float4 GetReflection_Maps(float3 posW, float3 normal, float alpha, float specularExpMultiplier, float reflectionMultiplier) {
	float3 toEyeW = normalize(gEyePosW - posW);
	float3 reflected = reflect(-toEyeW, normal);
	float3 refl = GetReflection(reflected, (gMaterial.SpecularExp + 400 * GET_FLAG(IS_CARPAINT)) * specularExpMultiplier);

	float val = GetReflectionStrength(normal, toEyeW);
	if (!HAS_FLAG(IS_ADDITIVE)) {
		alpha = alpha + val * (1 - alpha);
	}

	GPassAlphaTest(alpha);
	return float4(refl, (gGPassTransparent ? saturate(alpha + val) : 1.0) * val * reflectionMultiplier);
}

float4 GetReflection_Maps_NoAlpha(float3 posW, float3 normal, float alpha, float specularExpMultiplier, float reflectionMultiplier) {
	float3 toEyeW = normalize(gEyePosW - posW);
	float3 reflected = reflect(-toEyeW, normal);
	float3 refl = GetReflection(reflected, gMaterial.SpecularExp * specularExpMultiplier);
	float val = GetReflectionStrength(normal, toEyeW);

	GPassAlphaTest(alpha);
	return float4(refl, (gGPassTransparent ? saturate(alpha + val) : 1.0) * val * reflectionMultiplier);
}

float2 EncodeNormal(float3 n) {
	float p = sqrt(n.z * 8 + 8);
	return float2(n.xy / p + 0.5);
}

PS_OUT PackResult(float4 reflection, float3 normal, float depth, float specularExp) {
	PS_OUT pout;
	pout.BaseReflection = reflection;
	pout.Normal = float4(normal.xyz, specularExp);
	pout.Depth = depth;
	return pout;
}

PS_OUT GetResult(float depth, float3 posW, float3 normal, float alpha) {
	return PackResult(
		GetReflection(posW, normal, alpha),
		normal, depth, gMaterial.SpecularExp);
}

PS_OUT GetResult_Maps(float depth, float3 posW, float3 normal, float alpha, float txMapsSpecularMultiplier, float txMapsSpecularExpMultiplier,
	float txMapsReflectionMultiplier) {
	return PackResult(
		GetReflection_Maps(posW, normal, alpha, txMapsSpecularExpMultiplier, txMapsReflectionMultiplier),
		normal, depth, (gMaterial.SpecularExp + 400 * GET_FLAG(IS_CARPAINT)) * txMapsSpecularExpMultiplier);
}

PS_OUT GetResult_Maps_NoAlpha(float depth, float3 posW, float3 normal, float alpha, float txMapsSpecularMultiplier, float txMapsSpecularExpMultiplier,
	float txMapsReflectionMultiplier) {
	return PackResult(
		GetReflection_Maps_NoAlpha(posW, normal, alpha, txMapsSpecularExpMultiplier, txMapsReflectionMultiplier),
		normal, depth, gMaterial.SpecularExp * txMapsSpecularExpMultiplier);
}

float GetDepth(float4 posH) {
	return posH.z;
}

//// Standart

PS_OUT ps_GPass_Standard(PS_IN pin) {
    FlatMirrorTest(pin);

	GPassAlphaTest(gDiffuseMap.Sample(samAnisotropic, pin.Tex).a);
	return PackResult((float4)0.0, normalize(pin.NormalW), GetDepth(pin.PosH), 0.0);
}

technique10 GPass_Standard {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_GPass_Standard()));
	}
}

//// Alpha

technique10 GPass_Alpha {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_GPass_Standard()));
	}
}

//// Reflective

PS_OUT ps_GPass_Reflective(PS_IN pin) {
    FlatMirrorTest(pin);

	return GetResult(GetDepth(pin.PosH), pin.PosW, normalize(pin.NormalW), gDiffuseMap.Sample(samAnisotropic, pin.Tex).a);
}

technique10 GPass_Reflective {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_GPass_Reflective()));
	}
}

//// NM

PS_OUT ps_GPass_Nm(PS_IN pin) {
    FlatMirrorTest(pin);

	float4 diffuseMapValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex);
	float4 normalValue = gNormalMap.Sample(samAnisotropic, pin.Tex);
	float alpha = normalValue.a * diffuseMapValue.a;
	float3 normal = normalize(NormalSampleToWorldSpace(normalValue.xyz, pin.NormalW, pin.TangentW));
	return GetResult(GetDepth(pin.PosH), pin.PosW, normal, alpha);
}

technique10 GPass_Nm {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_GPass_Nm()));
	}
}

//// NM UV Mult

PS_OUT ps_GPass_NmUvMult(PS_IN pin) {
    FlatMirrorTest(pin);

	float4 diffuseMapValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex * (1 + gNmUvMultMaterial.NormalMultiplier));
	float4 normalValue = gNormalMap.Sample(samAnisotropic, pin.Tex * (1 + gNmUvMultMaterial.NormalMultiplier));
	float3 normal = normalize(NormalSampleToWorldSpace(normalValue.xyz, pin.NormalW, pin.TangentW));
	return GetResult(GetDepth(pin.PosH), pin.PosW, normal, diffuseMapValue.a);
}

technique10 GPass_NmUvMult {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_GPass_NmUvMult()));
	}
}

//// AT_NM

technique10 GPass_AtNm {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_GPass_Standard()));
	}
}

//// Maps

PS_OUT ps_GPass_Maps(PS_IN pin) {
    FlatMirrorTest(pin);

	float3 mapsValue = gMapsMap.Sample(samAnisotropic, pin.Tex).rgb;
	float4 diffuseMapValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex);
	float mask = diffuseMapValue.a;

	float alpha;
	float3 normal;

	if (HAS_FLAG(HAS_DETAILS_MAP)) {
		float4 details = gDetailsMap.Sample(samAnisotropic, pin.Tex * gMapsMaterial.DetailsUvMultiplier);
		mapsValue.y *= (details.a * 0.5 + 0.5);
	}

	if (HAS_FLAG(HAS_NORMAL_MAP)) {
		float4 normalValue = gNormalMap.Sample(samAnisotropic, pin.Tex);
		alpha = HAS_FLAG(USE_NORMAL_ALPHA_AS_ALPHA) ? normalValue.a : 1.0;

        if (HAS_FLAG(HAS_DETAILS_MAP)) {
            float blend = gMapsMaterial.DetailsNormalBlend;
            if (blend > 0.0) {
                float4 detailsNormalValue = gDetailsNormalMap.Sample(samAnisotropic, pin.Tex * gMapsMaterial.DetailsUvMultiplier);
                normalValue += (detailsNormalValue - 0.5) * blend * (1.0 - mask);
            }
        }

		normal = normalize(NormalSampleToWorldSpace(normalValue.xyz, pin.NormalW, pin.TangentW));
	} else {
		normal = normalize(pin.NormalW);
		alpha = 1.0;
	}

	return GetResult_Maps(GetDepth(pin.PosH), pin.PosW, normal, alpha, mapsValue.r, mapsValue.g, mapsValue.b);
}

technique10 GPass_Maps {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_GPass_Maps()));
	}
}

//// Skinned maps

PS_OUT ps_GPass_SkinnedMaps(PS_IN pin) {
    FlatMirrorTest(pin);

	float3 mapsValue = gMapsMap.Sample(samAnisotropic, pin.Tex).rgb;
	float4 diffuseMapValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex);
	float mask = diffuseMapValue.a;

	float alpha;
	float3 normal;

	if (HAS_FLAG(HAS_DETAILS_MAP)) {
		float4 details = gDetailsMap.Sample(samAnisotropic, pin.Tex * gMapsMaterial.DetailsUvMultiplier);
		mapsValue.y *= (details.a * 0.5 + 0.5);
	}

    float4 normalValue = gNormalMap.Sample(samAnisotropic, pin.Tex);
    alpha = normalValue.a * mask;

    if (HAS_FLAG(HAS_DETAILS_MAP)) {
        float blend = gMapsMaterial.DetailsNormalBlend;
        if (blend > 0.0) {
            float4 detailsNormalValue = gDetailsNormalMap.Sample(samAnisotropic, pin.Tex * gMapsMaterial.DetailsUvMultiplier);
            normalValue += (detailsNormalValue - 0.5) * blend * (1.0 - mask);
        }
    }

    normal = normalize(NormalSampleToWorldSpace(normalValue.xyz, pin.NormalW, pin.TangentW));

	return GetResult_Maps(GetDepth(pin.PosH), pin.PosW, normal, alpha, mapsValue.r, mapsValue.g, mapsValue.b);
}

technique10 GPass_SkinnedMaps {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_skinned()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_GPass_SkinnedMaps()));
	}
}

// Diff maps (as Maps, but multipliers are taken from diffuse alpha-channel)

/*PS_OUT ps_GPass_DiffMaps(PS_IN pin) {
    FlatMirrorTest(pin);

float4 diffuseMapValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex);
float4 normalValue = gNormalMap.Sample(samAnisotropic, pin.Tex);
float3 normal = normalize(NormalSampleToWorldSpace(normalValue.xyz, pin.NormalW, pin.TangentW));
return GetResult_Maps_NoAlpha(GetDepth(pin.PosH), pin.PosW, normal, diffuseMapValue.a, diffuseMapValue.a, diffuseMapValue.a, diffuseMapValue.a);
}

technique10 GPass_DiffMaps {
pass P0 {
SetVertexShader(CompileShader(vs_4_0, vs_main()));
SetGeometryShader(NULL);
SetPixelShader(CompileShader(ps_4_0, ps_GPass_DiffMaps()));
}
}*/

// Tyres

PS_OUT ps_GPass_Tyres(PS_IN pin) {
    FlatMirrorTest(pin);

	float4 diffuseMapValue = lerp(
		gDiffuseMap.Sample(samAnisotropic, pin.Tex),
		gDiffuseBlurMap.Sample(samAnisotropic, pin.Tex),
		gTyresMaterial.BlurLevel);
	float4 normalValue = lerp(
		gNormalMap.Sample(samAnisotropic, pin.Tex),
		gNormalBlurMap.Sample(samAnisotropic, pin.Tex),
		gTyresMaterial.BlurLevel);

	float3 normal = normalize(NormalSampleToWorldSpace(normalValue.xyz, pin.NormalW, pin.TangentW));
	return GetResult_Maps_NoAlpha(GetDepth(pin.PosH), pin.PosW, normal, diffuseMapValue.a, diffuseMapValue.a, diffuseMapValue.a, diffuseMapValue.a);
}

technique10 GPass_Tyres {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_GPass_Tyres()));
	}
}

//// GL

technique10 GPass_Gl {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_GPass_Standard()));
	}
}

technique10 GPass_SkinnedGl {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_skinned()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_GPass_Standard()));
	}
}

//// Ground

PS_OUT ps_GPass_FlatMirror(pt_PS_IN pin) {
	return PackResult(float4(0, 1, 0, 0), float3(0, 1, 0), GetDepth(pin.PosH), 0.0);
}

technique10 GPass_FlatMirror {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_pt_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_GPass_FlatMirror()));
	}
}

float GetFlatBackgroundGroundOpacity(float3 posW, float baseOpacity, float frenselOpacity) {
	// distance to viewer
	float3 eyeW = gEyePosW - posW;
	float distance = length(eyeW);

	// if viewing angle is small, “fresnel” is smaller → result is more backgroundy
	float fresnel = baseOpacity + frenselOpacity * pow(normalize(eyeW).y, 4);

	// summary opacity (aka non-backgroundy) is combined from viewing angle and distance
	// for smooth transition\w\w not anymore
	float opacity = fresnel;

	return opacity;
}

float4 ps_GPass_FlatMirror_SslrFix(pt_PS_IN pin) : SV_Target{
	float2 tex = pin.PosH.xy / gScreenSize.xy;
	float4 value = gDiffuseMap.Sample(samAnisotropic, tex);

	float opacity = GetFlatBackgroundGroundOpacity(pin.PosW, 0.7, 0.3);
	value.a *= 1.0 - saturate(1.5 * opacity * (1.0 - gFlatMirrorPower));
	return value;
}

technique10 GPass_FlatMirror_SslrFix {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_pt_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_5_0, ps_GPass_FlatMirror_SslrFix()));
	}
}

//// Debug

float4 GetReflection_Debug(float3 posW, float3 normal, float alpha) {
	float3 toEyeW = normalize(gEyePosW - posW);
	float3 reflected = reflect(-toEyeW, normal);
	float3 refl = GetReflection(reflected, 12.0);

	float rid = 1 - saturate(dot(toEyeW, normal) - 0.02);
	float rim = pow(rid, 4.0);
	float val = min(rim, 0.05);

	return float4(refl, (gGPassTransparent ? alpha : 1.0) * val);
}

PS_OUT ps_GPass_Debug(PS_IN pin) {
    FlatMirrorTest(pin);

	float4 diffuseMapValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex);

	float3 normal;
	float alpha;
	if (HAS_FLAG(HAS_NORMAL_MAP)) {
		float4 normalValue = gNormalMap.Sample(samAnisotropic, pin.Tex);
		alpha = HAS_FLAG(USE_NORMAL_ALPHA_AS_ALPHA) ? normalValue.a : gDiffuseMap.Sample(samAnisotropic, pin.Tex).a;
		normal = normalize(NormalSampleToWorldSpace(normalValue.xyz, pin.NormalW, pin.TangentW));
	} else {
		normal = normalize(pin.NormalW);
		alpha = diffuseMapValue.a;
	}

	GPassAlphaTest(alpha);
	return PackResult(
		GetReflection_Debug(pin.PosW, normal, alpha),
		normal, GetDepth(pin.PosH), 12.0);
}

technique10 GPass_Debug {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_GPass_Debug()));
	}
}

technique10 GPass_SkinnedDebug {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_skinned()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_GPass_Debug()));
	}
}