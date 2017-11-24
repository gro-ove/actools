// Options

#define ENABLE_TESSELATION 1  // Optionally, car paint meshes can be smoothed

// Kunos-compatible shaders and more

//// Standart

float4 ps_Standard(PS_IN pin) : SV_Target {
    FlatMirrorTest(pin);

	float3 color, normal; float alpha;
	Unpack(pin, color, alpha, normal);
	float3 lighted = CalculateLight(color, normal, pin.PosW, pin.PosH.xy);
	return float4(lighted, alpha);
}

technique10 Standard {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Standard()));
	}
}

//// Sky

float4 ps_Sky(PS_IN pin) : SV_Target {
    FlatMirrorTest(pin);
	return float4(gBackgroundColor, 1.0);
}

technique10 Sky {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Sky()));
	}
}

//// Alpha

float4 ps_Alpha(PS_IN pin) : SV_Target {
    FlatMirrorTest(pin);

	float3 color, normal; float alpha;
	Unpack_Alpha(pin, color, alpha, normal);
	float3 lighted = CalculateLight(color, normal, pin.PosW, pin.PosH.xy);
	return float4(lighted, alpha);
}

technique10 Alpha {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Alpha()));
	}
}

//// Reflective

float4 ps_Reflective(PS_IN pin) : SV_Target {
    FlatMirrorTest(pin);

	float3 color, normal; float alpha;
	Unpack(pin, color, alpha, normal);

	float4 refl = CalculateReflection(pin.PosW, normal);
	float3 lighted = CalculateLight(color, normal, pin.PosW, pin.PosH.xy);

	if (!HAS_FLAG(IS_ADDITIVE)) {
		lighted *= 1.0 - refl.a;
		alpha = alpha + refl.a * (1.0 - alpha);
	}

	return float4(lighted + refl.rgb * refl.a, alpha);
}

technique10 Reflective {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Reflective()));
	}
}

//// NM

float4 ps_Nm(PS_IN pin) : SV_Target {
    FlatMirrorTest(pin);

	float3 color, normal; float alpha;
	Unpack_Nm(pin, color, alpha, normal);

	float4 refl = CalculateReflection(pin.PosW, normal);
	float3 lighted = CalculateLight(color, normal, pin.PosW, pin.PosH.xy);

	if (!HAS_FLAG(IS_ADDITIVE)) {
		lighted *= 1.0 - refl.a;
		alpha = alpha + refl.a * (1.0 - alpha);
	}

	return float4(lighted + refl.rgb * refl.a, alpha);
}

technique10 Nm {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Nm()));
	}
}

//// NM UV Mult

float4 ps_NmUvMult(PS_IN pin) : SV_Target {
    FlatMirrorTest(pin);

	float3 color, normal; float alpha;
	Unpack_NmUvMult(pin, color, alpha, normal);

	float4 refl = CalculateReflection(pin.PosW, normal);
	float3 lighted = CalculateLight(color, normal, pin.PosW, pin.PosH.xy);

	if (!HAS_FLAG(IS_ADDITIVE)) {
		lighted *= 1.0 - refl.a;
		alpha = alpha + refl.a * (1.0 - alpha);
	}

	return float4(lighted + refl.rgb * refl.a, alpha);
}

technique10 NmUvMult {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_NmUvMult()));
	}
}

//// AT NM

float4 ps_AtNm(PS_IN pin) : SV_Target {
    FlatMirrorTest(pin);

	float3 color, normal; float alpha;
	Unpack_AtNm(pin, color, alpha, normal);

	float3 lighted = CalculateLight(color, normal, pin.PosW, pin.PosH.xy);
	return float4(lighted, alpha);
}

technique10 AtNm {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_AtNm()));
	}
}

//// Maps

float4 ps_Maps(PS_IN pin) : SV_Target {
    FlatMirrorTest(pin);

	float3 color, normal, maps; float alpha;
	Unpack_Maps(pin, color, alpha, normal, maps);

	float4 refl = CalculateReflection_Maps(pin.PosW, normal, maps.y);
	float3 lighted = CalculateLight_Maps_Sun(color, normal, pin.PosW, maps.x, maps.y, pin.PosH.xy);

	if (!HAS_FLAG(IS_ADDITIVE)) {
		lighted *= 1.0 - refl.a;
		alpha = alpha + refl.a * (1.0 - alpha);
	}

	return float4(lighted + refl.rgb * refl.a * maps.z, alpha);
}

technique10 Maps {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Maps()));
	}
}

#if ENABLE_TESSELATION == 1
#include "DarkMaterial.Tesselation.fx"

technique10 Maps_TesselatePhong {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, VS()));
        SetHullShader(CompileShader(hs_5_0, HS()));
        SetDomainShader(CompileShader(ds_5_0, DS_phong()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Maps()));
	}
}

technique10 Maps_TesselatePn {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, VS()));
        SetHullShader(CompileShader(hs_5_0, HS_pn()));
        SetDomainShader(CompileShader(ds_5_0, DS_pn()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Maps()));
	}
}
#endif

//// Skinned maps

float4 ps_SkinnedMaps(PS_IN pin) : SV_Target {
    FlatMirrorTest(pin);

	float3 color, normal, maps; float alpha;
	Unpack_SkinnedMaps(pin, color, alpha, normal, maps);

	float4 refl = CalculateReflection_Maps(pin.PosW, normal, maps.y);
	float3 lighted = CalculateLight_Maps_Sun(color, normal, pin.PosW, maps.x, maps.y, pin.PosH.xy);

	if (!HAS_FLAG(IS_ADDITIVE)) {
		lighted *= 1.0 - refl.a;
		alpha = alpha + refl.a * (1.0 - alpha);
	}

	return float4(lighted + refl.rgb * refl.a * maps.z, alpha);
}

technique10 SkinnedMaps {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_skinned()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_SkinnedMaps()));
	}
}

// Diff maps (as Maps, but multipliers are taken from diffuse alpha-channel)

/*float4 ps_DiffMaps(PS_IN pin) : SV_Target {
    FlatMirrorTest(pin);

	float3 color, normal; float alpha;
	Unpack_DiffMaps(pin, color, alpha, normal);

	float4 refl = CalculateReflection_Maps(pin.PosW, normal, alpha);
	float3 lighted = CalculateLight_Maps(color, normal, pin.PosW, alpha, 1.0, pin.PosH.xy);

	if (!HAS_FLAG(IS_ADDITIVE)) {
		lighted *= 1.0 - refl.a;
	}

	return float4(lighted + refl.rgb * refl.a * alpha, alpha);
}

technique10 DiffMaps {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_DiffMaps()));
	}
}*/

// Tyres

float4 ps_Tyres(PS_IN pin) : SV_Target {
    FlatMirrorTest(pin);

	float3 color, normal; float alpha;
	Unpack_Tyres(pin, color, alpha, normal);

	float4 refl = CalculateReflection_Maps(pin.PosW, normal, alpha);
	float3 lighted = CalculateLight_Maps(color, normal, pin.PosW, alpha, 1.0, pin.PosH.xy);

	if (!HAS_FLAG(IS_ADDITIVE)) {
		lighted *= 1.0 - refl.a;
	}

	return float4(lighted + refl.rgb * refl.a * alpha, 1.0);
}

technique10 Tyres {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Tyres()));
	}
}

//// GL

float4 ps_Gl(PS_IN pin) : SV_Target {
    FlatMirrorTest(pin);
    return float4(normalize(pin.NormalW), 1.0);
}

technique10 Gl {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Gl()));
	}
}

technique10 SkinnedGl {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_skinned()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Gl()));
	}
}

//// Windscreen

float4 ps_Windscreen(PS_IN pin) : SV_Target {
    FlatMirrorTest(pin);

	float4 txColor = gDiffuseMap.Sample(samAnisotropic, pin.Tex);

	float alpha = txColor.a;
	AlphaTest(alpha);

	float3 normal = normalize(pin.NormalW);
	// float3 lighted = CalculateLight(txColor.rgb, normal, pin.PosW, pin.PosH.xy);

	float3 fromEyeW = normalize(pin.PosW - gEyePosW);

#if COMPLEX_LIGHTING == 1
	float3 diffuse;
	GetLight_NoSpecular_NoArea(fromEyeW, pin.PosW, diffuse);
#else
	float3 diffuse = saturate(Luminance(gLightColor) * 0.76);
#if ENABLE_SHADOWS == 1
	diffuse *= GetMainShadow_ConsiderMirror(pin.PosW);
#endif
#endif

	float3 ambient = GetAmbient(normal);
	float3 lighted = txColor.rgb * (gMaterial.Ambient * ambient + gMaterial.Diffuse * diffuse + gMaterial.Emissive);
	return float4(lighted, alpha > 0.5 ? alpha : alpha * saturate(Luminance(diffuse) * 0.76));
}

technique10 Windscreen {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Windscreen()));
	}
}

//// Collider

float4 ps_Collider(PS_IN pin) : SV_Target {
    FlatMirrorTest(pin);

	float3 toEyeW = normalize(gEyePosW - pin.PosW);
	float3 normal = normalize(pin.NormalW);
	float opacify = pow(1.0 - dot(normal, toEyeW), 5.0);
#ifdef SIMPLE_TEST
	return float4((float3)1.0 - abs(normal), 1.0);
#else
	return float4((float3)1.0 - abs(normal), opacify);
#endif
}

technique10 Collider {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Collider()));
	}
}

//// Debug

float4 ps_Debug(PS_IN pin) : SV_Target {
    FlatMirrorTest(pin);

	float3 position = pin.PosW;
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

	float3 ambient = GetAmbient(normal) * GetAo(pin.PosH.xy);

#if COMPLEX_LIGHTING == 0
	float diffuseMultiplier = GetDiffuseMultiplier(normal);

#if ENABLE_SHADOWS == 1
	diffuseMultiplier *= GetMainShadow_ConsiderMirror(position);
#endif

	float nDotH = GetNDotH(normal, position);
	float specular = CalculateSpecularLight(nDotH, 50, 0.5);
	float3 light = 0.4 * ambient + (0.5 + specular) * gLightColor * diffuseMultiplier;
#else
	float3 diffuse, specular;
	GetLight_Custom(normal, position, 50.0, 0.5, diffuse, specular);
	float3 light = 0.4 * ambient + 0.5 * diffuse + specular;
#endif

	float distance = length(gEyePosW - position);

	//
	float3 toEyeW = normalize(gEyePosW - position);
	float3 reflected = reflect(-toEyeW, normal);
	float3 refl = GetReflection(reflected, 12);

	float rid = 1 - saturate(dot(toEyeW, normal) - 0.02);
	float rim = pow(rid, 4.0);
	float val = min(rim, 0.05);
	//

	light += refl * val + gMaterial.Emissive * diffuseMapValue.rgb;
	AlphaTest(alpha);

	return float4(light, alpha);
}

technique10 Debug {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Debug()));
	}
}

technique10 SkinnedDebug {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_skinned()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Debug()));
	}
}

//////////////// For shadows

technique10 DepthOnly {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_depthOnly()));
		SetGeometryShader(NULL);
		SetPixelShader(NULL);
	}
}

technique10 SkinnedDepthOnly {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_depthOnly_skinned()));
		SetGeometryShader(NULL);
		SetPixelShader(NULL);
	}
}

//////////////// Misc stuff

pt_PS_IN vs_AmbientShadow(pt_VS_IN vin) {
	pt_PS_IN vout;

	float3 posW = mul(float4(vin.PosL, 1.0f), gWorld).xyz;
	float3 eyeL = mul(float4(gEyePosW, 1.0f), transpose(gWorld)).xyz;
	float3 toEyeL = normalize(eyeL - vin.PosL);

	float4 p = CalculatePosH(vin.PosL);
	//float4 r = CalculatePosH(vin.PosL + toEyeL * 0.02);
	//p.z = r.z;

	vout.PosH = p;
	vout.PosW = posW;
	vout.Tex = vin.Tex;

	return vout;
}

float4 ps_AmbientShadow(pt_PS_IN pin) : SV_Target {
    FlatMirrorTest(pin);

#if COMPLEX_LIGHTING == 1
	float3 gLightColor = gLights[0].Type == LIGHT_DIRECTIONAL ? gLights[0].Color.xyz : (float3)0.0;
#endif

	float value = gDiffuseMap.Sample(samAnisotropic, pin.Tex).r * gAmbientShadowOpacity;
	float lightBrightness = saturate(Luminance(gLightColor) * 1.5);

#if ENABLE_SHADOWS == 1
	float shadow = GetShadow(pin.PosW);
	return float4(0.0, 0.0, 0.0, value * (1.0 - shadow * lightBrightness * (gNumSplits > 0 ? 1 : 0) * 0.95));
#else
	return float4(0.0, 0.0, 0.0, value);
#endif
}

technique10 AmbientShadow {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_AmbientShadow()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_AmbientShadow()));
	}
}

float4 ps_Mirror(PS_IN pin) : SV_Target {
	float3 toEyeW = normalize(gEyePosW - pin.PosW);
	float3 reflected = reflect(-toEyeW, pin.NormalW);
	float3 refl = GetReflection(reflected, 500) * 0.8;
	return float4(refl, 1.0);
}

technique10 Mirror {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_Mirror()));
	}
}

SamplerState samTest {
	Filter = MIN_MAG_MIP_LINEAR;
	AddressU = Border;
	AddressV = Border;
	BorderColor = (float4)0.5;
};

float4 GetFlatBackgroundGroundColor(float3 posW, float baseOpacity, float frenselOpacity) {
#if COMPLEX_LIGHTING == 1
	float3 gLightColor = gLights[0].Type == LIGHT_DIRECTIONAL ? gLights[0].Color.xyz : (float3)0.0;
#endif

	// distance to viewer
	float3 eyeW = gEyePosW - posW;
	float distance = length(eyeW);

	// if viewing angle is small, “fresnel” is smaller → result is more backgroundy
	float fresnel = baseOpacity + frenselOpacity * pow(normalize(eyeW).y, 4);

	// summary opacity (aka non-backgroundy) is combined from viewing angle and distance
	// for smooth transition\w\w not anymore
	float opacity = fresnel;

	// only light is affected by distance
	float distanceMultiplier = saturate(1.2 - distance / 40);

	// shadow at the point
#if ENABLE_SHADOWS == 1
	float shadow = GetShadow(posW);
#else
	float shadow = 0.0;
#endif

#if COMPLEX_LIGHTING == 1
	float3 diffuse;
	GetLight_NoSpecular_ExtraOnly(float3(0, 1, 0), posW, diffuse);
	float3 light = (gLightDir.y * gLightColor + diffuse) * distanceMultiplier;
#else
	// how much surface is lighed according to light direction
	float3 light = gLightDir.y * gLightColor * distanceMultiplier;
#endif

	// ambient color
	float3 ambient = gAmbientDown * 0.73 + gAmbientRange * 0.27;

	// bright light source means more backgroundy surface
	float lightBrightness = Luminance(gLightColor) * distanceMultiplier;

	// separately color in lighted and shadowed areas
	// 100%-target is to match those colors if there is no light (aka no shadow)
	float3 lightPart = gBackgroundColor * (1 - opacity * lightBrightness) + light * opacity;
	float3 shadowPart = (1 - lightBrightness) * lightPart + lightBrightness * (ambient * Luminance(gBackgroundColor) * 0.32 + saturate(gBackgroundColor) * 0.22);

	// combining
	return float4(lightPart * shadow + shadowPart * (1 - shadow), opacity);
}

float4 ps_FlatMirror(pt_PS_IN pin) : SV_Target {
	float4 ground = GetFlatBackgroundGroundColor(pin.PosW, 0.7, 0.3);
	ground.a = saturate(1.5 * ground.a * (1.0 - gFlatMirrorPower));
	return ground;
}

technique10 FlatMirror {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_pt_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_FlatMirror()));
	}
}

float4 ps_TransparentGround(pt_PS_IN pin) : SV_Target {
#if ENABLE_SHADOWS == 1
	float shadow = 1.0 - GetShadow(pin.PosW);
#else
	float shadow = 0.0;
#endif

	float3 ambient = gAmbientDown * 0.73 + gAmbientRange * 0.27;
    shadow *= saturate(Luminance(gLightColor) / (1 + saturate(Luminance(ambient))));

	float2 tex = pin.PosH.xy / gScreenSize.xy;
	float4 value = gDiffuseMap.Sample(samAnisotropic, tex);
	shadow *= 1 - gFlatMirrorPower;
	return float4(
	    gFlatMirrorPower == 0.0 ? (float3)0 : value.rgb * gFlatMirrorPower / (shadow + gFlatMirrorPower),
	    shadow * (1 - gFlatMirrorPower) + gFlatMirrorPower);
}

technique10 TransparentGround {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_pt_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_TransparentGround()));
	}
}

float4 ps_FlatTextureMirror(pt_PS_IN pin) : SV_Target {
	float2 tex = pin.PosH.xy / gScreenSize.xy;
	float4 value = gDiffuseMap.Sample(samAnisotropic, tex);

	float4 ground = GetFlatBackgroundGroundColor(pin.PosW, 0.7, 0.3);
	float alpha = saturate(1.5 * ground.a * (1 - gFlatMirrorPower));
	return float4(value.rgb * (1 - alpha) + ground.rgb * alpha, 1.0);
}

technique10 FlatTextureMirror {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_pt_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_FlatTextureMirror()));
	}
}

float4 ps_FlatTextureMirrorNoGround(pt_PS_IN pin) : SV_Target {
	float2 tex = pin.PosH.xy / gScreenSize.xy;
	float4 value = gDiffuseMap.Sample(samAnisotropic, tex);
	return float4(value.rgb, gFlatMirrorPower);
}

technique10 FlatTextureMirrorNoGround {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_pt_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_FlatTextureMirrorNoGround()));
	}
}

float4 ps_FlatBackgroundGround(pt_PS_IN pin) : SV_Target {
	return float4(GetFlatBackgroundGroundColor(pin.PosW, 0.21, 0.12).rgb, 1.0);
}

technique10 FlatBackgroundGround {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_pt_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_FlatBackgroundGround()));
	}
}

#if COMPLEX_LIGHTING == 1
float GetNDotH(float3 normal, float3 position) {
	float3 toEye = normalize(gEyePosW - position);
	float3 halfway = normalize(toEye + gLights[0].DirectionW.xyz);
	return saturate(dot(halfway, normal));
}
#endif

float4 ps_FlatAmbientGround(pt_PS_IN pin) : SV_Target {
#if COMPLEX_LIGHTING == 1
	float3 gLightDir = gLights[0].DirectionW.xyz;
	float3 gLightColor = gLights[0].Type == LIGHT_DIRECTIONAL ? gLights[0].Color.xyz : (float3)0.0;
#endif

	float3 normal = float3(0, 1, 0);
	float3 position = pin.PosW;

	float3 ambient = GetAmbient(normal);
	float diffuseMultiplier = saturate(dot(normal, gLightDir));

#if ENABLE_SHADOWS == 1
	diffuseMultiplier *= GetMainShadow_ConsiderMirror(position);
#endif

	float nDotH = GetNDotH(normal, position);
	float specular = CalculateSpecularLight(nDotH, 50, 0.5);

	float distance = length(gEyePosW - position);

	float3 light = 0.4 * (gAmbientDown + 0.2 * gAmbientRange) + (0.5 + specular) * gLightColor * diffuseMultiplier;
	return float4(light, saturate(1.5 - distance / 60));
}

technique10 FlatAmbientGround {
	pass P0 {
		SetVertexShader(CompileShader(vs_4_0, vs_pt_main()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_4_0, ps_FlatAmbientGround()));
	}
}