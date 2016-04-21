/* GENERATED AUTOMATICALLY */
/* DON'T MODIFY */

using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using SlimDX;
using SlimDX.D3DCompiler;
using SlimDX.Direct3D11;
using AcTools.Render.Base.Structs;
using AcTools.Render.Base.Utils;
// ReSharper disable InconsistentNaming
// ReSharper disable LocalizableElement

namespace AcTools.Render.Base.Shaders {
	public interface IEffectWrapper : System.IDisposable {
		void Initialize(Device device);
	}

	public interface IEffectMatricesWrapper : IEffectWrapper {
		EffectMatrixVariable FxWorld { get; }
		EffectMatrixVariable FxWorldInvTranspose { get; }
		EffectMatrixVariable FxWorldViewProj { get; }
	}

	public class EffectDeferredGObject : IEffectMatricesWrapper {
		[StructLayout(LayoutKind.Sequential)]
        public struct Material {
            public float Ambient;
            public float Diffuse;
            public float Specular;
            public float SpecularExp;
            public float FresnelC;
            public float FresnelExp;
            public float FresnelMaxLevel;
            public float DetailsUvMultipler;
            public Vector3 Emissive;
            public float DetailsNormalBlend;
            public uint Flags;
            public Vector3 _padding;

			public static readonly int Stride = Marshal.SizeOf(typeof(Material));
        }
		[StructLayout(LayoutKind.Sequential)]
        public struct AmbientShadow_VS_IN {


			public static readonly int Stride = Marshal.SizeOf(typeof(AmbientShadow_VS_IN));
        }
		[StructLayout(LayoutKind.Sequential)]
        public struct AmbientShadow_PS_IN {


			public static readonly int Stride = Marshal.SizeOf(typeof(AmbientShadow_PS_IN));
        }
		public const uint HasNormalMap = 1;
		public const uint HasDetailsMap = 2;
		public const uint HasDetailsNormalMap = 4;
		public const uint HasMaps = 8;
		public const uint UseDiffuseAlphaAsMap = 16;
        public Effect E;

        public ShaderSignature InputSignaturePNTG, InputSignaturePT;
        public InputLayout LayoutPNTG, LayoutPT;

		public EffectTechnique TechStandardDeferred, TechStandardForward, TechTransparentForward, TechAmbientShadowDeferred;

		public EffectMatrixVariable FxWorld { get; private set; }
		public EffectMatrixVariable FxWorldInvTranspose { get; private set; }
		public EffectMatrixVariable FxWorldViewProj { get; private set; }
		public EffectResourceVariable FxDiffuseMap, FxNormalMap, FxMapsMap, FxDetailsMap, FxDetailsNormalMap, FxReflectionCubemap;
		public EffectVectorVariable FxEyePosW;
		public EffectVariable FxMaterial;

		public EffectDeferredGObject() {
		}

		public void Initialize(Device device) {
            using (var bc = EffectUtils.Compile(Encoding.UTF8.GetBytes("#include \"Deferred.fx\"\n\n\n\tstatic const dword HAS_NORMAL_MAP = 1;\n\tstatic const dword HAS_DETAILS_MAP = 2;\n\tstatic const dword HAS_DETAILS_NORMAL_MAP = 4;\n\tstatic const dword HAS_MAPS = 8;\n\tstatic const dword USE_DIFFUSE_ALPHA_AS_MAP = 16;\n\n\tstruct Material {\n\t\tfloat Ambient;\n\t\tfloat Diffuse;\n\t\tfloat Specular;\n\t\tfloat SpecularExp;\n\n\t\tfloat FresnelC;\n\t\tfloat FresnelExp;\n\t\tfloat FresnelMaxLevel;\n\t\tfloat DetailsUvMultipler;\n\n\t\tfloat3 Emissive;\n\t\tfloat DetailsNormalBlend;\n\n\t\tdword Flags;\n\t\tfloat3 _padding;\n\t};\n\n\n\tTexture2D gDiffuseMap;\n\tTexture2D gNormalMap;\n\tTexture2D gMapsMap;\n\tTexture2D gDetailsMap;\n\tTexture2D gDetailsNormalMap;\n\tTextureCube gReflectionCubemap;\n\n\n\tcbuffer cbPerObject : register(b0) {\n\t\tmatrix gWorld;\n\t\tmatrix gWorldInvTranspose;\n\t\tmatrix gWorldViewProj;\n\t\tMaterial gMaterial;\n\t}\n\n\tcbuffer cbPerFrame {\n\t\tfloat3 gEyePosW;\n\t}\n\n\n\tPS_IN vs_Standard(VS_IN vin) {\n\t\tPS_IN vout;\n\n\t\tvout.PosW = mul(float4(vin.PosL, 1.0f), gWorld).xyz;\n\t\tvout.NormalW = mul(vin.NormalL, (float3x3)gWorldInvTranspose);\n\t\tvout.TangentW = mul(vin.TangentL, (float3x3)gWorldInvTranspose);\n\n\t\tvout.PosH = mul(float4(vin.PosL, 1.0f), gWorldViewProj);\n\t\tvout.Tex = vin.Tex;\n\n\t\treturn vout;\n\t}\n\n\tfloat3 NormalSampleToWorldSpace(float3 normalMapSample, float3 unitNormalW, float3 tangentW){\n\t\t\n\t\tfloat3 normalT = 2.0f*normalMapSample - 1.0f;\n\n\t\t\n\t\tfloat3 N = unitNormalW;\n\t\tfloat3 T = normalize(tangentW - dot(tangentW, N)*N);\n\t\tfloat3 B = cross(N, T);\n\n\t\tfloat3x3 TBN = float3x3(T, B, N);\n\n\t\t\n\t\tfloat3 bumpedNormalW = mul(normalT, TBN);\n\t\treturn bumpedNormalW;\n\t}\n\n\tPS_OUT ps_StandardDeferred(PS_IN pin) : SV_Target{\n\t\tfloat4 diffuseValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex);\n\n\t\tPS_OUT pout;\n\t\t\n\t\t[flatten]\n\t\tif ((gMaterial.Flags & HAS_NORMAL_MAP) == HAS_NORMAL_MAP){\n\t\t\tfloat4 normalValue = gNormalMap.Sample(samAnisotropic, pin.Tex);\n\t\t\n\t\t\t[flatten]\n\t\t\tif ((gMaterial.Flags & HAS_DETAILS_NORMAL_MAP) == HAS_DETAILS_NORMAL_MAP){\n\t\t\t\tfloat4 detailsNormalValue = gDetailsNormalMap.Sample(samAnisotropic, pin.Tex * gMaterial.DetailsUvMultipler);\n\t\t\t\tnormalValue += (detailsNormalValue - 0.5) * gMaterial.DetailsNormalBlend * (1.0 - diffuseValue.a);\n\t\t\t}\n\n\t\t\tpout.Normal = normalize(NormalSampleToWorldSpace(normalize(normalValue.xyz), pin.NormalW, pin.TangentW));\n\t\t} else {\n\t\t\tpout.Normal = normalize(pin.NormalW);\n\t\t}\n\n\t\tfloat specular = saturate(gMaterial.Specular / 2.5);\n\t\tfloat glossiness = saturate((gMaterial.SpecularExp - 1) / 250);\n\t\tfloat reflectiveness = saturate(gMaterial.FresnelMaxLevel);\n\t\tfloat metalness = saturate(max(\n\t\t\tgMaterial.FresnelC / gMaterial.FresnelMaxLevel, \n\t\t\t1.1 / (gMaterial.FresnelExp + 1) - 0.1));\n\t\t\n\t\tif ((gMaterial.Flags & HAS_MAPS) == HAS_MAPS){\n\t\t\tfloat4 mapsValue = gMapsMap.Sample(samAnisotropic, pin.Tex);\n\t\t\tspecular *= mapsValue.r;\n\t\t\tglossiness *= mapsValue.g;\n\t\t\treflectiveness *= mapsValue.b;\n\t\t}\n\t\t\n\t\tif ((gMaterial.Flags & USE_DIFFUSE_ALPHA_AS_MAP) == USE_DIFFUSE_ALPHA_AS_MAP){\n\t\t\tspecular *= diffuseValue.a;\n\t\t\treflectiveness *= diffuseValue.a / 2 + 0.5;\n\t\t}\n\t\t\n\t\t\n\t\t[flatten]\n\t\tif ((gMaterial.Flags & HAS_DETAILS_MAP) == HAS_DETAILS_MAP){\n\t\t\tfloat4 detailsValue = gDetailsMap.Sample(samAnisotropic, pin.Tex * gMaterial.DetailsUvMultipler);\n\n\t\t\tdiffuseValue *= diffuseValue.a + detailsValue - detailsValue * diffuseValue.a;\n\t\t\tglossiness *= (1.0 - detailsValue.a * (1.0 - diffuseValue.a)) / 2 + 0.5;\n\t\t}\n\t\t\n\t\tfloat ambient = max(gMaterial.Ambient, 0.05);\n\t\tpout.Base = diffuseValue * ambient;\n\t\tpout.Base.a = gMaterial.Diffuse / ambient;\n\t\t\n\t\t\n\t\tpout.Maps = float4(specular, glossiness, reflectiveness, metalness);\n\t\treturn pout;\n\t}\n\n\ttechnique11 StandardDeferred {\n\t\tpass P0 {\n\t\t\tSetVertexShader( CompileShader( vs_4_0, vs_Standard() ) );\n\t\t\tSetGeometryShader( NULL );\n\t\t\tSetPixelShader( CompileShader( ps_5_0, ps_StandardDeferred() ) );\n\t\t}\n\t}\n\n\tfloat4 ps_StandardForward(PS_IN pin) : SV_Target {\n\t\treturn gDiffuseMap.Sample(samAnisotropic, pin.Tex);\n\t}\n\n\ttechnique11 StandardForward {\n\t\tpass P0 {\n\t\t\tSetVertexShader( CompileShader( vs_4_0, vs_Standard() ) );\n\t\t\tSetGeometryShader( NULL );\n\t\t\tSetPixelShader( CompileShader( ps_4_0, ps_StandardForward() ) );\n\t\t}\n\t}\n\n\n\tfloat4 ps_TransparentForward(PS_IN pin) : SV_Target{\n\t\tfloat4 diffuseValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex);\n\n\t\tfloat3 color = diffuseValue.rgb;\n\t\tfloat alpha = diffuseValue.a;\n\t\tfloat3 normal;\n\n\t\t[flatten]\n\t\tif ((gMaterial.Flags & HAS_NORMAL_MAP) == HAS_NORMAL_MAP) {\n\t\t\tfloat4 normalValue = gNormalMap.Sample(samAnisotropic, pin.Tex);\n\t\t\talpha *= normalValue.a;\n\t\t\tnormal = normalize(NormalSampleToWorldSpace(normalize(normalValue.xyz), pin.NormalW, pin.TangentW));\n\t\t} else {\n\t\t\tnormal = normalize(pin.NormalW);\n\t\t}\n\n\t\tfloat specular = saturate(gMaterial.Specular / 2.5);\n\t\tfloat glossiness = saturate((gMaterial.SpecularExp - 1) / 250);\n\t\tfloat reflectiveness = saturate(gMaterial.FresnelMaxLevel);\n\t\tfloat metalness = saturate(max(\n\t\t\t\tgMaterial.FresnelC / gMaterial.FresnelMaxLevel,\n\t\t\t\t1.1 / (gMaterial.FresnelExp + 1) - 0.1));\n\n\t\tfloat3 toEyeW = normalize(gEyePosW - pin.PosW);\n\n\t\tfloat rid = saturate(dot(toEyeW, pin.NormalW));\n\t\tfloat rim = pow(1 - rid, gMaterial.FresnelExp);\n\n\t\tfloat4 reflectionColor = gReflectionCubemap.SampleBias(samAnisotropic, reflect(-toEyeW, normal),\n\t\t\t\t1.0f - glossiness);\n\n\t\t\n\t\treturn float4(color * (gMaterial.Ambient - reflectiveness / 2) + reflectionColor * reflectiveness * rim, alpha);\n\t}\n\n\ttechnique11 TransparentForward {\n\t\tpass P0 {\n\t\t\tSetVertexShader(CompileShader(vs_4_0, vs_Standard()));\n\t\t\tSetGeometryShader(NULL);\n\t\t\tSetPixelShader(CompileShader(ps_4_0, ps_TransparentForward()));\n\t\t}\n\t}\n\n\n\tstruct AmbientShadow_VS_IN {\n\t\tfloat3 PosL       : POSITION;\n\t\tfloat2 Tex        : TEXCOORD;\n\t};\n\n\tstruct AmbientShadow_PS_IN {\n\t\tfloat4 PosH       : SV_POSITION;\n\t\tfloat3 PosW       : POSITION;\n\t\tfloat2 Tex        : TEXCOORD;\n\t};\n\n\tAmbientShadow_PS_IN vs_AmbientShadowDeferred(AmbientShadow_VS_IN vin) {\n\t\tAmbientShadow_PS_IN vout;\n\t\tvout.PosW = mul(float4(vin.PosL, 1.0f), gWorld).xyz;\n\t\tvout.PosH = mul(float4(vin.PosL, 1.0f), gWorldViewProj);\n\t\tvout.Tex = vin.Tex;\n\t\treturn vout;\n\t}\n\n\tPS_OUT ps_AmbientShadowDeferred(AmbientShadow_PS_IN pin) : SV_Target {\n\t\tPS_OUT pout;\n\n\t\tfloat4 diffuseValue = gDiffuseMap.Sample(samAnisotropic, pin.Tex);\n\t\tfloat shadowLevel = diffuseValue.x * 1.15;\n\n\t\tpout.Base = float4(0, 0, 0, shadowLevel);\n\t\tpout.Normal = 0;\n\t\tpout.Maps = float4(0, 0, 0, shadowLevel);\n\n\t\treturn pout;\n\t}\n\n\ttechnique11 AmbientShadowDeferred {\n\t\tpass P0 {\n\t\t\tSetVertexShader( CompileShader( vs_5_0, vs_AmbientShadowDeferred() ) );\n\t\t\tSetGeometryShader( NULL );\n\t\t\tSetPixelShader( CompileShader( ps_5_0, ps_AmbientShadowDeferred() ) );\n\t\t}\n\t}"), "DeferredGObject")){
                E = new Effect(device, bc);
			}

			TechStandardDeferred = E.GetTechniqueByName("StandardDeferred");
			TechStandardForward = E.GetTechniqueByName("StandardForward");
			TechTransparentForward = E.GetTechniqueByName("TransparentForward");
			TechAmbientShadowDeferred = E.GetTechniqueByName("AmbientShadowDeferred");

			for (var i = 0; i < TechStandardDeferred.Description.PassCount && InputSignaturePNTG == null; i++) {
				InputSignaturePNTG = TechStandardDeferred.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePNTG == null) throw new System.Exception("input signature (DeferredGObject, PNTG, StandardDeferred) == null");
			LayoutPNTG = new InputLayout(device, InputSignaturePNTG, InputLayouts.VerticePNTG.InputElementsValue);
			for (var i = 0; i < TechAmbientShadowDeferred.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechAmbientShadowDeferred.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (DeferredGObject, PT, AmbientShadowDeferred) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);

			FxWorld = E.GetVariableByName("gWorld").AsMatrix();
			FxWorldInvTranspose = E.GetVariableByName("gWorldInvTranspose").AsMatrix();
			FxWorldViewProj = E.GetVariableByName("gWorldViewProj").AsMatrix();
			FxDiffuseMap = E.GetVariableByName("gDiffuseMap").AsResource();
			FxNormalMap = E.GetVariableByName("gNormalMap").AsResource();
			FxMapsMap = E.GetVariableByName("gMapsMap").AsResource();
			FxDetailsMap = E.GetVariableByName("gDetailsMap").AsResource();
			FxDetailsNormalMap = E.GetVariableByName("gDetailsNormalMap").AsResource();
			FxReflectionCubemap = E.GetVariableByName("gReflectionCubemap").AsResource();
			FxEyePosW = E.GetVariableByName("gEyePosW").AsVector();
			FxMaterial = E.GetVariableByName("gMaterial");
		}

        public void Dispose() {
            E.Dispose();
			InputSignaturePNTG.Dispose();
            LayoutPNTG.Dispose();
			InputSignaturePT.Dispose();
            LayoutPT.Dispose();
        }
	}

	public class EffectDeferredGObjectSpecial : IEffectMatricesWrapper {
		[StructLayout(LayoutKind.Sequential)]
        public struct SpecialGl_PS_IN {


			public static readonly int Stride = Marshal.SizeOf(typeof(SpecialGl_PS_IN));
        }
		
        public Effect E;

        public ShaderSignature InputSignaturePNTG;
        public InputLayout LayoutPNTG;

		public EffectTechnique TechSpecialGlDeferred, TechSpecialGlForward;

		public EffectMatrixVariable FxWorld { get; private set; }
		public EffectMatrixVariable FxWorldInvTranspose { get; private set; }
		public EffectMatrixVariable FxWorldViewProj { get; private set; }

		public EffectDeferredGObjectSpecial() {
		}

		public void Initialize(Device device) {
            using (var bc = EffectUtils.Compile(Encoding.UTF8.GetBytes("#include \"Deferred.fx\"\n\n\n\tcbuffer cbPerObject : register(b0) {\n\t\tmatrix gWorld;\n\t\tmatrix gWorldInvTranspose;\n\t\tmatrix gWorldViewProj;\n\t}\n\n\n\tstruct SpecialGl_PS_IN {\n\t\tfloat4 PosH       : SV_POSITION;\n\t\tfloat3 NormalW    : NORMAL;\n\t\tfloat3 PosL       : POSITION;\n\t};\n\n\tSpecialGl_PS_IN vs_SpecialGl(VS_IN vin) {\n\t\tSpecialGl_PS_IN vout;\n\t\tvout.PosH = mul(float4(vin.PosL, 1.0f), gWorldViewProj);\n\t\tvout.PosL = vin.PosL;\n\t\tvout.NormalW = mul(vin.NormalL, (float3x3)gWorldInvTranspose);\n\t\treturn vout;\n\t}\n\n\tPS_OUT ps_SpecialGlDeferred(SpecialGl_PS_IN pin) : SV_Target {\n\t\tPS_OUT pout;\n\t\tpout.Base = float4(normalize(pin.PosL), 1.0);\n\t\tpout.Normal = normalize(pin.NormalW);\n\t\tpout.Maps = 0;\n\t\treturn pout;\n\t}\n\n\ttechnique11 SpecialGlDeferred {\n\t\tpass P0 {\n\t\t\tSetVertexShader( CompileShader( vs_4_0, vs_SpecialGl() ) );\n\t\t\tSetGeometryShader( NULL );\n\t\t\tSetPixelShader( CompileShader( ps_4_0, ps_SpecialGlDeferred() ) );\n\t\t}\n\t}\n\n\tfloat4 ps_SpecialGlForward(SpecialGl_PS_IN pin) : SV_Target {\n\t\treturn float4(normalize(pin.PosL), 1.0);\n\t}\n\n\ttechnique11 SpecialGlForward {\n\t\tpass P0 {\n\t\t\tSetVertexShader( CompileShader( vs_4_0, vs_SpecialGl() ) );\n\t\t\tSetGeometryShader( NULL );\n\t\t\tSetPixelShader( CompileShader( ps_4_0, ps_SpecialGlForward() ) );\n\t\t}\n\t}"), "DeferredGObjectSpecial")){
                E = new Effect(device, bc);
			}

			TechSpecialGlDeferred = E.GetTechniqueByName("SpecialGlDeferred");
			TechSpecialGlForward = E.GetTechniqueByName("SpecialGlForward");

			for (var i = 0; i < TechSpecialGlDeferred.Description.PassCount && InputSignaturePNTG == null; i++) {
				InputSignaturePNTG = TechSpecialGlDeferred.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePNTG == null) throw new System.Exception("input signature (DeferredGObjectSpecial, PNTG, SpecialGlDeferred) == null");
			LayoutPNTG = new InputLayout(device, InputSignaturePNTG, InputLayouts.VerticePNTG.InputElementsValue);

			FxWorld = E.GetVariableByName("gWorld").AsMatrix();
			FxWorldInvTranspose = E.GetVariableByName("gWorldInvTranspose").AsMatrix();
			FxWorldViewProj = E.GetVariableByName("gWorldViewProj").AsMatrix();
		}

        public void Dispose() {
            E.Dispose();
			InputSignaturePNTG.Dispose();
            LayoutPNTG.Dispose();
        }
	}

	public class EffectDeferredLight : IEffectWrapper {
		
		
        public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectTechnique TechPointLight;

		public EffectMatrixVariable FxWorldViewProjInv { get; private set; }
		public EffectResourceVariable FxBaseMap, FxNormalMap, FxMapsMap, FxDepthMap;
		public EffectScalarVariable FxPointLightRadius;
		public EffectVectorVariable FxLightColor, FxPointLightPosition, FxEyePosW;

		public EffectDeferredLight() {
		}

		public void Initialize(Device device) {
            using (var bc = EffectUtils.Compile(Encoding.UTF8.GetBytes("\n\tTexture2D gBaseMap;\n\tTexture2D gNormalMap;\n\tTexture2D gMapsMap;\n\tTexture2D gDepthMap;\n\n\tSamplerState samInputImage {\n\t\tFilter = MIN_MAG_LINEAR_MIP_POINT;\n\t\tAddressU = CLAMP;\n\t\tAddressV = CLAMP;\n\t};\n\n\n\tcbuffer cbPerObject : register(b0) {\n\t\tmatrix gWorldViewProjInv;\n\t\tfloat3 gLightColor;\n\t\tfloat3 gPointLightPosition;\n\t\tfloat gPointLightRadius;\n\t}\n\n\tcbuffer cbPerFrame {\n\t\tfloat3 gEyePosW;\n\t}\n\n\n\tstruct VS_IN {\n\t\tfloat3 PosL    : POSITION;\n\t\tfloat2 Tex     : TEXCOORD;\n\t};\n\n\tstruct PS_IN {\n\t\tfloat4 PosH    : SV_POSITION;\n\t\tfloat2 Tex     : TEXCOORD;\n\t};\n\n\n\tPS_IN vs_main(VS_IN vin) {\n\t\tPS_IN vout;\n\t\tvout.PosH = float4(vin.PosL, 1.0f);\n\t\tvout.Tex = vin.Tex;\n\t\treturn vout;\n\t}\n\n\n\tfloat3 GetPosition(float2 uv, float depth){\n\t\tfloat4 position = mul(float4(uv.x * 2 - 1, -(uv.y * 2 - 1), depth, 1), gWorldViewProjInv);\n\t\treturn position.xyz / position.w;\n\t}\n\n\tfloat4 ps_PointLight(PS_IN pin) : SV_Target {\n\t\tfloat4 baseValue = gBaseMap.SampleLevel(samInputImage, pin.Tex, 0.0);\n\t\tfloat4 mapsValue = gMapsMap.SampleLevel(samInputImage, pin.Tex, 0.0);\n\n\t\tfloat4 normalValue = gNormalMap.SampleLevel(samInputImage, pin.Tex, 0.0);\n\t\tfloat3 normal = normalValue.xyz;\n\t\t\n\t\tfloat depth = gDepthMap.SampleLevel(samInputImage, pin.Tex, 0.0).x;\n\t\tfloat3 position = GetPosition(pin.Tex, depth);\n\n\t\tfloat3 lightVector = gPointLightPosition - position;\n\t\tfloat3 toLight = normalize(lightVector);\n\t\tfloat distance = dot(lightVector, lightVector);\n\n\t\tfloat lightness = saturate(dot(normal, toLight)) * saturate(1 - distance / gPointLightRadius);\n\n\t\tfloat3 toEye = normalize(gEyePosW - position);\n\t\tfloat3 halfway = normalize(toEye + toLight);\n\n\t\tfloat3 color = baseValue.rgb;\n\t\tfloat diffuseValue = baseValue.a;\n\t\tfloat3 lightnessResult = color * diffuseValue * gLightColor;\n\t\t\t\n\t\tfloat specIntensity = mapsValue.r * 1.2;\n\t\tfloat specExp = mapsValue.g * 250 + 1;\n\n\t\tfloat nDotH = saturate(dot(halfway, normal));\n\t\tfloat specularLightness = pow(nDotH, specExp) * specIntensity;\n\n\t\t[flatten]\n\t\tif (specExp > 30){\n\t\t\tspecularLightness += pow(nDotH, specExp * 10 + 5000) * (specIntensity * 12 + 5) * saturate((specExp - 30) / 40);\n\t\t}\n\n\t\tlightnessResult += specularLightness * gLightColor;\n\t\treturn float4(lightnessResult, lightness + specularLightness);\n\t}\n\n\ttechnique11 PointLight {\n\t\tpass P0 {\n\t\t\tSetVertexShader( CompileShader( vs_4_0, vs_main() ) );\n\t\t\tSetGeometryShader( NULL );\n\t\t\tSetPixelShader( CompileShader( ps_4_0, ps_PointLight() ) );\n\t\t}\n\t}"), "DeferredLight")){
                E = new Effect(device, bc);
			}

			TechPointLight = E.GetTechniqueByName("PointLight");

			for (var i = 0; i < TechPointLight.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechPointLight.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (DeferredLight, PT, PointLight) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);

			FxWorldViewProjInv = E.GetVariableByName("gWorldViewProjInv").AsMatrix();
			FxBaseMap = E.GetVariableByName("gBaseMap").AsResource();
			FxNormalMap = E.GetVariableByName("gNormalMap").AsResource();
			FxMapsMap = E.GetVariableByName("gMapsMap").AsResource();
			FxDepthMap = E.GetVariableByName("gDepthMap").AsResource();
			FxPointLightRadius = E.GetVariableByName("gPointLightRadius").AsScalar();
			FxLightColor = E.GetVariableByName("gLightColor").AsVector();
			FxPointLightPosition = E.GetVariableByName("gPointLightPosition").AsVector();
			FxEyePosW = E.GetVariableByName("gEyePosW").AsVector();
		}

        public void Dispose() {
            E.Dispose();
			InputSignaturePT.Dispose();
            LayoutPT.Dispose();
        }
	}

	public class EffectDeferredPpSslr : IEffectWrapper {
		
		
        public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectTechnique TechHabrahabrVersion;

		public EffectMatrixVariable FxWorldViewProjInv { get; private set; }
		public EffectMatrixVariable FxWorldViewProj { get; private set; }
		public EffectResourceVariable FxBaseMap, FxLightMap, FxNormalMap, FxDepthMap;
		public EffectVectorVariable FxEyePosW;

		public EffectDeferredPpSslr() {
		}

		public void Initialize(Device device) {
            using (var bc = EffectUtils.Compile(Encoding.UTF8.GetBytes("\n\tTexture2D gBaseMap;\n\tTexture2D gLightMap;\n\tTexture2D gNormalMap;\n\tTexture2D gDepthMap;\n\n\tSamplerState samInputImage {\n\t\tFilter = MIN_MAG_LINEAR_MIP_POINT;\n\t\tAddressU = CLAMP;\n\t\tAddressV = CLAMP;\n\t};\n\t\n\n\tcbuffer cbPerFrame : register(b0) {\n\t\tmatrix gWorldViewProjInv;\n\t\tmatrix gWorldViewProj;\n\t\tfloat3 gEyePosW;\n\t}\n\n\n\tstruct VS_IN {\n\t\tfloat3 PosL    : POSITION;\n\t\tfloat2 Tex     : TEXCOORD;\n\t};\n\n\tstruct PS_IN {\n\t\tfloat4 PosH    : SV_POSITION;\n\t\tfloat2 Tex     : TEXCOORD;\n\t};\n\n\n\tfloat3 GetColor(float2 uv){\n\t\treturn gBaseMap.SampleLevel(samInputImage, uv, 0).rgb + \n\t\t\t\tgLightMap.SampleLevel(samInputImage, uv, 0.0).rgb;\n\t}\n\n\tfloat3 GetPosition(float2 uv, float depth){\n\t\tfloat4 position = mul(float4(uv.x * 2 - 1, -(uv.y * 2 - 1), depth, 1), gWorldViewProjInv);\n\t\treturn position.xyz / position.w;\n\t}\n\n\tfloat GetDepth(float2 uv){\n\t\treturn gDepthMap.SampleLevel(samInputImage, uv, 0).x;\n\t}\n\n\tfloat3 GetUv(float3 position){\n\t\tfloat4 pVP = mul(float4(position, 1.0f), gWorldViewProj);\n\t\tpVP.xy = float2(0.5f, 0.5f) + float2(0.5f, -0.5f) * pVP.xy / pVP.w;\n\t\treturn float3(pVP.xy, pVP.z / pVP.w);\n\t}\n\n\n\tPS_IN vs_main(VS_IN vin) {\n\t\tPS_IN vout;\n\t\tvout.PosH = float4(vin.PosL, 1.0f);\n\t\tvout.Tex = vin.Tex;\n\t\treturn vout;\n\t}\n\n\n\t#define MAX_L 0.4\n\t#define FADING_FROM 0.2\n\t#define MIN_L 0.0\n\t#define ITERATIONS 20\n\t#define START_L 0.01\n\n\tfloat4 ps_HabrahabrVersion(PS_IN pin) : SV_Target {\n\t\tfloat depth = GetDepth(pin.Tex);\n\t\tfloat3 position = GetPosition(pin.Tex, depth);\n\t\tfloat3 normal = gNormalMap.SampleLevel(samInputImage, pin.Tex, 0).xyz;\n\t\tfloat3 viewDir = normalize(position - gEyePosW);\n\t\tfloat3 reflectDir = normalize(reflect(viewDir, normal));\n\n\t\tfloat3 newUv = 0;\n\t\tfloat L = START_L;\n\t\tfloat quality = 0;\n\n\t\t[flatten]\n\t\tfor(int i = 0; i < ITERATIONS; i++){\n\t\t\tfloat3 calculatedPosition = position + reflectDir * L;\n\n\t\t\tnewUv = GetUv(calculatedPosition);\n\t\t\tfloat3 newPosition = GetPosition(newUv.xy, GetDepth(newUv.xy));\n\t\t\tquality = length(calculatedPosition - newPosition);\n\t\t\tL = length(position - newPosition);\n\t\t}\n\n\t\tfloat fresnel = saturate(2.8 * pow(1 + dot(viewDir, normal), 2));\n\t\tquality = 1 - saturate(abs(quality) / 0.1);\n\n\t\tfloat alpha = fresnel * quality * saturate(\n\t\t\t(1 - saturate((length(newUv - pin.Tex) - FADING_FROM) / (MAX_L - FADING_FROM)))\n\t\t\t\t- min(L - MIN_L, 0) * -10000\n\t\t) * saturate(min(newUv.x, 1 - newUv.x) / 0.1) * saturate(min(newUv.y, 1 - newUv.y) / 0.1);\n\n\t\treturn float4(GetColor(newUv.xy).rgb * min(alpha * 4, 1), alpha);\n\t}\n\n\ttechnique11 HabrahabrVersion {\n\t\tpass P0 {\n\t\t\tSetVertexShader( CompileShader( vs_5_0, vs_main() ) );\n\t\t\tSetGeometryShader( NULL );\n\t\t\tSetPixelShader( CompileShader( ps_5_0, ps_HabrahabrVersion() ) );\n\t\t}\n\t}"), "DeferredPpSslr")){
                E = new Effect(device, bc);
			}

			TechHabrahabrVersion = E.GetTechniqueByName("HabrahabrVersion");

			for (var i = 0; i < TechHabrahabrVersion.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechHabrahabrVersion.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (DeferredPpSslr, PT, HabrahabrVersion) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);

			FxWorldViewProjInv = E.GetVariableByName("gWorldViewProjInv").AsMatrix();
			FxWorldViewProj = E.GetVariableByName("gWorldViewProj").AsMatrix();
			FxBaseMap = E.GetVariableByName("gBaseMap").AsResource();
			FxLightMap = E.GetVariableByName("gLightMap").AsResource();
			FxNormalMap = E.GetVariableByName("gNormalMap").AsResource();
			FxDepthMap = E.GetVariableByName("gDepthMap").AsResource();
			FxEyePosW = E.GetVariableByName("gEyePosW").AsVector();
		}

        public void Dispose() {
            E.Dispose();
			InputSignaturePT.Dispose();
            LayoutPT.Dispose();
        }
	}

	public class EffectDeferredResult : IEffectWrapper {
		
		
        public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectTechnique TechDebug, TechDebugPost, TechDebugLighting, TechDebugLocalReflections, TechCombine0;

		public EffectMatrixVariable FxWorldViewProjInv { get; private set; }
		public EffectResourceVariable FxBaseMap, FxNormalMap, FxMapsMap, FxDepthMap, FxLightMap, FxLocalReflectionMap, FxReflectionCubemap;
		public EffectVectorVariable FxAmbientDown, FxAmbientRange, FxEyePosW, FxScreenSize;

		public EffectDeferredResult() {
		}

		public void Initialize(Device device) {
            using (var bc = EffectUtils.Compile(Encoding.UTF8.GetBytes("\n\tTexture2D gBaseMap;\n\tTexture2D gNormalMap;\n\tTexture2D gMapsMap;\n\tTexture2D gDepthMap;\n\tTexture2D gLightMap;\n\tTexture2D gLocalReflectionMap;\n\tTextureCube gReflectionCubemap;\n\n\tSamplerState samInputImage {\n\t\tFilter = MIN_MAG_LINEAR_MIP_POINT;\n\t\tAddressU = CLAMP;\n\t\tAddressV = CLAMP;\n\t};\n\n\tSamplerState samAnisotropic {\n\t\tFilter = ANISOTROPIC;\n\t\tMaxAnisotropy = 4;\n\n\t\tAddressU = WRAP;\n\t\tAddressV = WRAP;\n\t};\n\t\n\n\tcbuffer cbPerFrame : register(b0) {\n\t\tmatrix gWorldViewProjInv;\n\t\tfloat3 gAmbientDown;\n\t\tfloat3 gAmbientRange;\n\t\tfloat3 gEyePosW;\n\t\tfloat4 gScreenSize;\n\t}\n\n\n\tstruct VS_IN {\n\t\tfloat3 PosL    : POSITION;\n\t\tfloat2 Tex     : TEXCOORD;\n\t};\n\n\tstruct PS_IN {\n\t\tfloat4 PosH    : SV_POSITION;\n\t\tfloat2 Tex     : TEXCOORD;\n\t};\n\n\n\tfloat3 GetPosition(float2 uv, float depth){\n\t\tfloat4 position = mul(float4(uv.x * 2 - 1, -(uv.y * 2 - 1), depth, 1), gWorldViewProjInv);\n\t\treturn position.xyz / position.w;\n\t}\n\n\tfloat CalculateReflectionPower(float3 toEyeNormalW, float3 normalW, float metalness){\n\t\tfloat rid = dot(toEyeNormalW, normalW);\n\t\tfloat rim = metalness + pow(1 - rid, (2 + 1 / metalness) / 3);\n\t\treturn rim * 2;\n\t}\n\n\n\tPS_IN vs_main(VS_IN vin) {\n\t\tPS_IN vout;\n\t\tvout.PosH = float4(vin.PosL, 1.0f);\n\t\tvout.Tex = vin.Tex;\n\t\treturn vout;\n\t}\n\n\n\tfloat4 ps_debug(PS_IN pin) : SV_Target {\n\t\tif (pin.Tex.y < 0.5){\n\t\t\tif (pin.Tex.x < 0.5){\n\t\t\t\treturn gBaseMap.SampleLevel(samInputImage, pin.Tex * 2, 0);\n\t\t\t} else {\n\t\t\t\tfloat4 normalValue = gNormalMap.SampleLevel(samInputImage, pin.Tex * 2 - float2(1, 0), 0);\n\t\t\t\tif (normalValue.x == 0 && normalValue.y == 0 && normalValue.z == 0){\n\t\t\t\t\treturn 0.0;\n\t\t\t\t}\n\t\t\t\treturn 0.5 + 0.5 * normalValue;\n\t\t\t}\n\t\t} else {\n\t\t\tif (pin.Tex.x < 0.5){\n\t\t\t\tfloat depthValue = gDepthMap.SampleLevel(samInputImage, pin.Tex * 2 - float2(0, 1), 0).x;\n\t\t\t\treturn (1 - depthValue) * 5;\n\t\t\t}\n\t\t}\n\t\t\n\t\tif (pin.Tex.y < 0.75){\n\t\t\tif (pin.Tex.x < 0.75){\n\t\t\t\treturn gMapsMap.SampleLevel(samInputImage, pin.Tex * 4 - float2(2, 2), 0).x;\n\t\t\t} else {\n\t\t\t\treturn gMapsMap.SampleLevel(samInputImage, pin.Tex * 4 - float2(3, 2), 0).y;\n\t\t\t}\n\t\t} else {\n\t\t\tif (pin.Tex.x < 0.75){\n\t\t\t\treturn gMapsMap.SampleLevel(samInputImage, pin.Tex * 4 - float2(2, 3), 0).z;\n\t\t\t} else {\n\t\t\t\treturn gMapsMap.SampleLevel(samInputImage, pin.Tex * 4 - float2(3, 3), 0).w;\n\t\t\t}\n\t\t}\n\n\t\treturn 0;\n\t}\n\n\ttechnique11 Debug {\n\t\tpass P0 {\n\t\t\tSetVertexShader( CompileShader( vs_5_0, vs_main() ) );\n\t\t\tSetGeometryShader( NULL );\n\t\t\tSetPixelShader( CompileShader( ps_5_0, ps_debug() ) );\n\t\t}\n\t}\n\n\n\tfloat4 ps_DebugPost(PS_IN pin) : SV_Target {\n\t\tif (pin.Tex.y < 0.5){\n\t\t\tif (pin.Tex.x < 0.5){\n\t\t\t\treturn gBaseMap.SampleLevel(samInputImage, pin.Tex * 2, 0.0).a;\n\t\t\t} else {\n\t\t\t\tfloat2 uv = pin.Tex * 2 - float2(1.0, 0.0);\n\n\t\t\t\tfloat4 normalValue = gNormalMap.SampleLevel(samInputImage, uv, 0.0);\n\t\t\t\tfloat3 normal = normalValue.xyz;\n\t\t\n\t\t\t\tfloat depth = gDepthMap.SampleLevel(samInputImage, uv, 0.0).x;\n\t\t\t\tfloat3 position = GetPosition(uv, depth);\n\t\t\n\t\t\t\tfloat3 toEyeW = normalize(gEyePosW - position);\n\t\t\t\tfloat4 reflectionColor = gReflectionCubemap.Sample(samAnisotropic, reflect(-toEyeW, normal));\n\t\t\t\treturn reflectionColor;\n\t\t\t\t\n\t\t\t\tfloat4 mapsValue = gMapsMap.SampleLevel(samInputImage, uv, 0.0);\n\t\t\t\tfloat glossiness = mapsValue.g;\n\t\t\t\tfloat reflectiveness = mapsValue.z;\n\t\t\t\tfloat metalness = mapsValue.w;\n\n\t\t\t\treturn reflectionColor * reflectiveness * CalculateReflectionPower(toEyeW, normal, metalness);\n\t\t\t}\n\t\t} else {\n\t\t\tif (pin.Tex.x < 0.5){\n\t\t\t\treturn gLocalReflectionMap.SampleLevel(samInputImage, pin.Tex * 2 - float2(0.0, 1.0), 0.0);\n\t\t\t} else {\n\t\t\t\treturn gLightMap.SampleLevel(samInputImage, pin.Tex * 2 - float2(1.0, 1.0), 0.0);\n\t\t\t}\n\t\t}\n\t}\n\n\ttechnique11 DebugPost {\n\t\tpass P0 {\n\t\t\tSetVertexShader( CompileShader( vs_5_0, vs_main() ) );\n\t\t\tSetGeometryShader( NULL );\n\t\t\tSetPixelShader( CompileShader( ps_5_0, ps_DebugPost() ) );\n\t\t}\n\t}\n\n\n\tfloat4 ps_DebugLighting(PS_IN pin) : SV_Target {\n\t\treturn gLightMap.SampleLevel(samInputImage, pin.Tex, 0.0);\n\t}\n\n\ttechnique11 DebugLighting {\n\t\tpass P0 {\n\t\t\tSetVertexShader( CompileShader( vs_5_0, vs_main() ) );\n\t\t\tSetGeometryShader( NULL );\n\t\t\tSetPixelShader( CompileShader( ps_5_0, ps_DebugLighting() ) );\n\t\t}\n\t}\n\n\n\tfloat4 ps_DebugLocalReflections(PS_IN pin) : SV_Target {\n\t\tfloat4 base = gBaseMap.SampleLevel(samInputImage, pin.Tex, 0.0);\n\t\tfloat4 light = gLightMap.SampleLevel(samInputImage, pin.Tex, 0.0);\n\t\tfloat4 reflection = gLocalReflectionMap.SampleLevel(samInputImage, pin.Tex, 0.0);\n\n\t\tfloat x = saturate((pin.Tex.x * (gScreenSize.x / 16) % 2 - 1.0) * 1e6);\n\t\tfloat y = saturate((pin.Tex.y * (gScreenSize.y / 16) % 2 - 1.0) * 1e6);\n\t\tfloat background = ((x + y) % 2) * 0.2 + 0.2;\n\n\t\treturn background * (1 - reflection.a) + reflection * reflection.a;\n\t}\n\n\ttechnique11 DebugLocalReflections {\n\t\tpass P0 {\n\t\t\tSetVertexShader( CompileShader( vs_5_0, vs_main() ) );\n\t\t\tSetGeometryShader( NULL );\n\t\t\tSetPixelShader( CompileShader( ps_5_0, ps_DebugLocalReflections() ) );\n\t\t}\n\t}\n\n\n\tfloat3 CalcAmbient(float3 normal, float3 color) {\n\t\tfloat up = normal.y * 0.5 + 0.5;\n\t\tfloat3 ambient = gAmbientDown + up * gAmbientRange;\n\t\treturn ambient * color;\n\t}\n\n\tfloat3 ReflectionColor(float3 toEyeW, float3 normal, float glossiness) {\n\t\treturn gReflectionCubemap.SampleBias(samAnisotropic, reflect(-toEyeW, normal), 0.3 / (glossiness + 0.3)).rgb;\n\t}\n\n\tfloat4 ps_0(PS_IN pin) : SV_Target {\n\t\t\n\t\tfloat4 normalValue = gNormalMap.SampleLevel(samInputImage, pin.Tex, 0.0);\n\t\tfloat depthValue = gDepthMap.SampleLevel(samInputImage, pin.Tex, 0.0).x;\n\n\t\tfloat3 normal = normalValue.xyz;\n\t\tfloat3 position = GetPosition(pin.Tex, depthValue);\n\n\t\t\n\t\tfloat4 baseValue = gBaseMap.SampleLevel(samInputImage, pin.Tex, 0.0);\n\t\tfloat4 lightValue = gLightMap.SampleLevel(samInputImage, pin.Tex, 0.0);\n\n\t\tfloat3 lighted = CalcAmbient(normal, baseValue.rgb) + lightValue.rgb;\n\n\t\t\n\t\tfloat4 mapsValue = gMapsMap.SampleLevel(samInputImage, pin.Tex, 0.0);\n\t\tfloat glossiness = mapsValue.g;\n\t\tfloat reflectiveness = mapsValue.z;\n\t\tfloat metalness = mapsValue.w;\n\t\t\n\t\t\n\t\tfloat3 toEyeW = normalize(gEyePosW - position);\n\t\tfloat3 reflectionColor = ReflectionColor(toEyeW, normal, glossiness);\n\n\t\tfloat4 localReflectionColor = gLocalReflectionMap.SampleLevel(samInputImage, pin.Tex, 0.0);\n\t\treflectionColor = reflectionColor * (1 - localReflectionColor.a) + localReflectionColor.rgb * localReflectionColor.a;\n\n\t\tfloat rid = dot(toEyeW, normal);\n\t\tfloat rim = metalness + pow(1 - rid, 1 / metalness);\n\n\t\tfloat3 reflection = (reflectionColor - 0.5 * (metalness + 0.2) / 1.2) * saturate(reflectiveness * \n\t\t\tCalculateReflectionPower(toEyeW, normal, metalness));\n\n\t\t\n\t\treturn float4(lighted + reflection, 1.0);\n\t}\n\n\ttechnique11 Combine0 {\n\t\tpass P0 {\n\t\t\tSetVertexShader( CompileShader( vs_5_0, vs_main() ) );\n\t\t\tSetGeometryShader( NULL );\n\t\t\tSetPixelShader( CompileShader( ps_5_0, ps_0() ) );\n\t\t}\n\t}"), "DeferredResult")){
                E = new Effect(device, bc);
			}

			TechDebug = E.GetTechniqueByName("Debug");
			TechDebugPost = E.GetTechniqueByName("DebugPost");
			TechDebugLighting = E.GetTechniqueByName("DebugLighting");
			TechDebugLocalReflections = E.GetTechniqueByName("DebugLocalReflections");
			TechCombine0 = E.GetTechniqueByName("Combine0");

			for (var i = 0; i < TechDebug.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechDebug.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (DeferredResult, PT, Debug) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);

			FxWorldViewProjInv = E.GetVariableByName("gWorldViewProjInv").AsMatrix();
			FxBaseMap = E.GetVariableByName("gBaseMap").AsResource();
			FxNormalMap = E.GetVariableByName("gNormalMap").AsResource();
			FxMapsMap = E.GetVariableByName("gMapsMap").AsResource();
			FxDepthMap = E.GetVariableByName("gDepthMap").AsResource();
			FxLightMap = E.GetVariableByName("gLightMap").AsResource();
			FxLocalReflectionMap = E.GetVariableByName("gLocalReflectionMap").AsResource();
			FxReflectionCubemap = E.GetVariableByName("gReflectionCubemap").AsResource();
			FxAmbientDown = E.GetVariableByName("gAmbientDown").AsVector();
			FxAmbientRange = E.GetVariableByName("gAmbientRange").AsVector();
			FxEyePosW = E.GetVariableByName("gEyePosW").AsVector();
			FxScreenSize = E.GetVariableByName("gScreenSize").AsVector();
		}

        public void Dispose() {
            E.Dispose();
			InputSignaturePT.Dispose();
            LayoutPT.Dispose();
        }
	}

	public class EffectDeferredTransparent : IEffectWrapper {
		
		
        public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectTechnique TechDebug, TechDebugPost, TechDebugLighting, TechDebugLocalReflections, TechCombine0;

		public EffectMatrixVariable FxWorldViewProjInv { get; private set; }
		public EffectResourceVariable FxBaseMap, FxNormalMap, FxMapsMap, FxDepthMap, FxLightMap, FxLocalReflectionMap, FxReflectionCubemap;
		public EffectVectorVariable FxAmbientDown, FxAmbientRange, FxEyePosW;

		public EffectDeferredTransparent() {
		}

		public void Initialize(Device device) {
            using (var bc = EffectUtils.Compile(Encoding.UTF8.GetBytes("\n\tTexture2D gBaseMap;\n\tTexture2D gNormalMap;\n\tTexture2D gMapsMap;\n\tTexture2D gDepthMap;\n\tTexture2D gLightMap;\n\tTexture2D gLocalReflectionMap;\n\tTextureCube gReflectionCubemap;\n\n\tSamplerState samInputImage {\n\t\tFilter = MIN_MAG_LINEAR_MIP_POINT;\n\t\tAddressU = CLAMP;\n\t\tAddressV = CLAMP;\n\t};\n\n\tSamplerState samAnisotropic {\n\t\tFilter = ANISOTROPIC;\n\t\tMaxAnisotropy = 4;\n\n\t\tAddressU = WRAP;\n\t\tAddressV = WRAP;\n\t};\n\t\n\n\tcbuffer cbPerFrame : register(b0) {\n\t\tmatrix gWorldViewProjInv;\n\t\tfloat3 gAmbientDown;\n\t\tfloat3 gAmbientRange;\n\t\tfloat3 gEyePosW;\n\t}\n\n\n\tstruct VS_IN {\n\t\tfloat3 PosL    : POSITION;\n\t\tfloat2 Tex     : TEXCOORD;\n\t};\n\n\tstruct PS_IN {\n\t\tfloat4 PosH    : SV_POSITION;\n\t\tfloat2 Tex     : TEXCOORD;\n\t};\n\n\n\tfloat3 GetPosition(float2 uv, float depth){\n\t\tfloat4 position = mul(float4(uv.x * 2 - 1, -(uv.y * 2 - 1), depth, 1), gWorldViewProjInv);\n\t\treturn position.xyz / position.w;\n\t}\n\n\tfloat CalculateReflectionPower(float3 toEyeNormalW, float3 normalW, float metalness){\n\t\tfloat rid = dot(toEyeNormalW, normalW);\n\t\tfloat rim = metalness + pow(1 - rid, (2 + 1 / metalness) / 3);\n\t\treturn rim * 2;\n\t}\n\n\n\tPS_IN vs_main(VS_IN vin) {\n\t\tPS_IN vout;\n\t\tvout.PosH = float4(vin.PosL, 1.0f);\n\t\tvout.Tex = vin.Tex;\n\t\treturn vout;\n\t}\n\n\n\tfloat4 ps_debug(PS_IN pin) : SV_Target {\n\t\tif (pin.Tex.y < 0.5){\n\t\t\tif (pin.Tex.x < 0.5){\n\t\t\t\treturn gBaseMap.SampleLevel(samInputImage, pin.Tex * 2, 0);\n\t\t\t} else {\n\t\t\t\tfloat4 normalValue = gNormalMap.SampleLevel(samInputImage, pin.Tex * 2 - float2(1, 0), 0);\n\t\t\t\tif (normalValue.x == 0 && normalValue.y == 0 && normalValue.z == 0){\n\t\t\t\t\treturn 0.0;\n\t\t\t\t}\n\t\t\t\treturn 0.5 + 0.5 * normalValue;\n\t\t\t}\n\t\t} else {\n\t\t\tif (pin.Tex.x < 0.5){\n\t\t\t\tfloat depthValue = gDepthMap.SampleLevel(samInputImage, pin.Tex * 2 - float2(0, 1), 0).x;\n\t\t\t\treturn (1 - depthValue) * 5;\n\t\t\t}\n\t\t}\n\t\t\n\t\tif (pin.Tex.y < 0.75){\n\t\t\tif (pin.Tex.x < 0.75){\n\t\t\t\treturn gMapsMap.SampleLevel(samInputImage, pin.Tex * 4 - float2(2, 2), 0).x;\n\t\t\t} else {\n\t\t\t\treturn gMapsMap.SampleLevel(samInputImage, pin.Tex * 4 - float2(3, 2), 0).y;\n\t\t\t}\n\t\t} else {\n\t\t\tif (pin.Tex.x < 0.75){\n\t\t\t\treturn gMapsMap.SampleLevel(samInputImage, pin.Tex * 4 - float2(2, 3), 0).z;\n\t\t\t} else {\n\t\t\t\treturn gMapsMap.SampleLevel(samInputImage, pin.Tex * 4 - float2(3, 3), 0).w;\n\t\t\t}\n\t\t}\n\n\t\treturn 0;\n\t}\n\n\ttechnique11 Debug {\n\t\tpass P0 {\n\t\t\tSetVertexShader( CompileShader( vs_5_0, vs_main() ) );\n\t\t\tSetGeometryShader( NULL );\n\t\t\tSetPixelShader( CompileShader( ps_5_0, ps_debug() ) );\n\t\t}\n\t}\n\n\n\tfloat4 ps_DebugPost(PS_IN pin) : SV_Target {\n\t\tif (pin.Tex.y < 0.5){\n\t\t\tif (pin.Tex.x < 0.5){\n\t\t\t\treturn gBaseMap.SampleLevel(samInputImage, pin.Tex * 2, 0.0).a;\n\t\t\t} else {\n\t\t\t\tfloat2 uv = pin.Tex * 2 - float2(1.0, 0.0);\n\n\t\t\t\tfloat4 normalValue = gNormalMap.SampleLevel(samInputImage, uv, 0.0);\n\t\t\t\tfloat3 normal = normalValue.xyz;\n\t\t\n\t\t\t\tfloat depth = gDepthMap.SampleLevel(samInputImage, uv, 0.0).x;\n\t\t\t\tfloat3 position = GetPosition(uv, depth);\n\t\t\n\t\t\t\tfloat3 toEyeW = normalize(gEyePosW - position);\n\t\t\t\tfloat4 reflectionColor = gReflectionCubemap.Sample(samAnisotropic, reflect(-toEyeW, normal));\n\t\t\t\treturn reflectionColor;\n\t\t\t\t\n\t\t\t\tfloat4 mapsValue = gMapsMap.SampleLevel(samInputImage, uv, 0.0);\n\t\t\t\tfloat glossiness = mapsValue.g;\n\t\t\t\tfloat reflectiveness = mapsValue.z;\n\t\t\t\tfloat metalness = mapsValue.w;\n\n\t\t\t\treturn reflectionColor * reflectiveness * CalculateReflectionPower(toEyeW, normal, metalness);\n\t\t\t}\n\t\t} else {\n\t\t\tif (pin.Tex.x < 0.5){\n\t\t\t\treturn gLocalReflectionMap.SampleLevel(samInputImage, pin.Tex * 2 - float2(0.0, 1.0), 0.0);\n\t\t\t} else {\n\t\t\t\treturn gLightMap.SampleLevel(samInputImage, pin.Tex * 2 - float2(1.0, 1.0), 0.0);\n\t\t\t}\n\t\t}\n\t}\n\n\ttechnique11 DebugPost {\n\t\tpass P0 {\n\t\t\tSetVertexShader( CompileShader( vs_5_0, vs_main() ) );\n\t\t\tSetGeometryShader( NULL );\n\t\t\tSetPixelShader( CompileShader( ps_5_0, ps_DebugPost() ) );\n\t\t}\n\t}\n\n\n\tfloat4 ps_DebugLighting(PS_IN pin) : SV_Target {\n\t\treturn gLightMap.SampleLevel(samInputImage, pin.Tex, 0.0);\n\t}\n\n\ttechnique11 DebugLighting {\n\t\tpass P0 {\n\t\t\tSetVertexShader( CompileShader( vs_5_0, vs_main() ) );\n\t\t\tSetGeometryShader( NULL );\n\t\t\tSetPixelShader( CompileShader( ps_5_0, ps_DebugLighting() ) );\n\t\t}\n\t}\n\n\n\tfloat4 ps_DebugLocalReflections(PS_IN pin) : SV_Target {\n\t\tfloat4 base = gBaseMap.SampleLevel(samInputImage, pin.Tex, 0.0);\n\t\tfloat4 light = gLightMap.SampleLevel(samInputImage, pin.Tex, 0.0);\n\t\tfloat4 reflection = gLocalReflectionMap.SampleLevel(samInputImage, pin.Tex, 0.0);\n\t\t\n\t\treturn (base + light) * (1 - reflection.a) + reflection * reflection.a;\n\t}\n\n\ttechnique11 DebugLocalReflections {\n\t\tpass P0 {\n\t\t\tSetVertexShader( CompileShader( vs_5_0, vs_main() ) );\n\t\t\tSetGeometryShader( NULL );\n\t\t\tSetPixelShader( CompileShader( ps_5_0, ps_DebugLocalReflections() ) );\n\t\t}\n\t}\n\n\n\tfloat3 CalcAmbient(float3 normal, float3 color) {\n\t\tfloat up = normal.y * 0.5 + 0.5;\n\t\tfloat3 ambient = gAmbientDown + up * gAmbientRange;\n\t\treturn ambient * color;\n\t}\n\n\tfloat3 ReflectionColor(float3 toEyeW, float3 normal, float glossiness) {\n\t\treturn gReflectionCubemap.SampleBias(samAnisotropic, reflect(-toEyeW, normal), 0.3 / (glossiness + 0.3)).rgb;\n\t}\n\n\tfloat4 ps_0(PS_IN pin) : SV_Target {\n\t\t\n\t\tfloat4 normalValue = gNormalMap.SampleLevel(samInputImage, pin.Tex, 0.0);\n\t\tfloat depthValue = gDepthMap.SampleLevel(samInputImage, pin.Tex, 0.0).x;\n\n\t\tfloat3 normal = normalValue.xyz;\n\t\tfloat3 position = GetPosition(pin.Tex, depthValue);\n\n\t\t\n\t\tfloat4 baseValue = gBaseMap.SampleLevel(samInputImage, pin.Tex, 0.0);\n\t\tfloat4 lightValue = gLightMap.SampleLevel(samInputImage, pin.Tex, 0.0);\n\n\t\tfloat3 lighted = CalcAmbient(normal, baseValue.rgb) + lightValue.rgb;\n\n\t\t\n\t\tfloat4 mapsValue = gMapsMap.SampleLevel(samInputImage, pin.Tex, 0.0);\n\t\tfloat glossiness = mapsValue.g;\n\t\tfloat reflectiveness = mapsValue.z;\n\t\tfloat metalness = mapsValue.w;\n\t\t\n\t\t\n\t\tfloat3 toEyeW = normalize(gEyePosW - position);\n\t\tfloat3 reflectionColor = ReflectionColor(toEyeW, normal, glossiness);\n\n\t\tfloat4 localReflectionColor = gLocalReflectionMap.SampleLevel(samInputImage, pin.Tex, 0.0);\n\t\treflectionColor = reflectionColor * (1 - localReflectionColor.a) + localReflectionColor.rgb * localReflectionColor.a;\n\n\t\tfloat rid = dot(toEyeW, normal);\n\t\tfloat rim = metalness + pow(1 - rid, 1 / metalness);\n\n\t\tfloat3 reflection = (reflectionColor - 0.5 * (metalness + 0.2) / 1.2) * saturate(reflectiveness * \n\t\t\tCalculateReflectionPower(toEyeW, normal, metalness));\n\n\t\t\n\t\treturn float4(lighted + reflection, 1.0);\n\t}\n\n\ttechnique11 Combine0 {\n\t\tpass P0 {\n\t\t\tSetVertexShader( CompileShader( vs_5_0, vs_main() ) );\n\t\t\tSetGeometryShader( NULL );\n\t\t\tSetPixelShader( CompileShader( ps_5_0, ps_0() ) );\n\t\t}\n\t}"), "DeferredTransparent")){
                E = new Effect(device, bc);
			}

			TechDebug = E.GetTechniqueByName("Debug");
			TechDebugPost = E.GetTechniqueByName("DebugPost");
			TechDebugLighting = E.GetTechniqueByName("DebugLighting");
			TechDebugLocalReflections = E.GetTechniqueByName("DebugLocalReflections");
			TechCombine0 = E.GetTechniqueByName("Combine0");

			for (var i = 0; i < TechDebug.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechDebug.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (DeferredTransparent, PT, Debug) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);

			FxWorldViewProjInv = E.GetVariableByName("gWorldViewProjInv").AsMatrix();
			FxBaseMap = E.GetVariableByName("gBaseMap").AsResource();
			FxNormalMap = E.GetVariableByName("gNormalMap").AsResource();
			FxMapsMap = E.GetVariableByName("gMapsMap").AsResource();
			FxDepthMap = E.GetVariableByName("gDepthMap").AsResource();
			FxLightMap = E.GetVariableByName("gLightMap").AsResource();
			FxLocalReflectionMap = E.GetVariableByName("gLocalReflectionMap").AsResource();
			FxReflectionCubemap = E.GetVariableByName("gReflectionCubemap").AsResource();
			FxAmbientDown = E.GetVariableByName("gAmbientDown").AsVector();
			FxAmbientRange = E.GetVariableByName("gAmbientRange").AsVector();
			FxEyePosW = E.GetVariableByName("gEyePosW").AsVector();
		}

        public void Dispose() {
            E.Dispose();
			InputSignaturePT.Dispose();
            LayoutPT.Dispose();
        }
	}

	public class EffectKunosShader : IEffectMatricesWrapper {
		[StructLayout(LayoutKind.Sequential)]
        public struct Material {
            public float Ambient;
            public float Diffuse;
            public float Specular;
            public float SpecularExp;
            public Vector3 Emissive;

			public static readonly int Stride = Marshal.SizeOf(typeof(Material));
        }
		
        public Effect E;

        public ShaderSignature InputSignaturePNT;
        public InputLayout LayoutPNT;

		public EffectTechnique TechPerPixel;

		public EffectMatrixVariable FxWorld { get; private set; }
		public EffectMatrixVariable FxWorldInvTranspose { get; private set; }
		public EffectMatrixVariable FxWorldViewProj { get; private set; }
		public EffectResourceVariable FxDiffuseMap;
		public EffectVectorVariable FxEyePosW;
		public EffectVariable FxMaterial;

		public EffectKunosShader() {
		}

		public void Initialize(Device device) {
            using (var bc = EffectUtils.Compile(Encoding.UTF8.GetBytes("\n\tstruct Material {\n\t\tfloat Ambient;\n\t\tfloat Diffuse;\n\t\tfloat Specular;\n\t\tfloat SpecularExp;\n\t\tfloat3 Emissive;\n\t};\n\n\n\tTexture2D gDiffuseMap;\n\n\tSamplerState samAnisotropic {\n\t\tFilter = ANISOTROPIC;\n\t\tMaxAnisotropy = 4;\n\n\t\tAddressU = WRAP;\n\t\tAddressV = WRAP;\n\t};\n\n\n\tcbuffer cbPerObject : register(b0) {\n\t\tmatrix gWorld;\n\t\tmatrix gWorldInvTranspose;\n\t\tmatrix gWorldViewProj;\n\t\tMaterial gMaterial;\n\t}\n\n\tcbuffer cbPerFrame {\n\t\t\n\t\tfloat3 gEyePosW;\n\t}\n\n\n\tstruct VS_IN {\n\t\tfloat3 PosL    : POSITION;\n\t\tfloat3 NormalL : NORMAL;\n\t\tfloat2 Tex     : TEXCOORD;\n\t};\n\n\tstruct PS_IN {\n\t\tfloat4 PosH    : SV_POSITION;\n\t\tfloat3 PosW    : POSITION;\n\t\tfloat3 NormalW : NORMAL;\n\t\tfloat2 Tex     : TEXCOORD;\n\t};\n\n\n\tPS_IN vs_main(VS_IN vin) {\n\t\tPS_IN vout;\n\n\t\tvout.PosW    = mul(float4(vin.PosL, 1.0f), gWorld).xyz;\n\t\tvout.NormalW = mul(vin.NormalL, (float3x3)gWorldInvTranspose);\n\n\t\tvout.PosH = mul(float4(vin.PosL, 1.0f), gWorldViewProj);\n\t\tvout.Tex = vin.Tex;\n\n\t\treturn vout;\n\t}\n\n\tfloat4 ps_main(PS_IN pin) : SV_Target{\n\t\t\n\t\tfloat4 texColor = gDiffuseMap.Sample(samAnisotropic, pin.Tex);\n\t\treturn texColor * gMaterial.Ambient + gMaterial.Diffuse * (dot(pin.NormalW, float3(0, -1, 0)) + 0.3) / 1.3;\n\t}\n\n\ttechnique11 PerPixel { \n\t\tpass P0 {\n\t\t\tSetVertexShader( CompileShader( vs_4_0, vs_main() ) );\n\t\t\tSetGeometryShader( NULL );\n\t\t\tSetPixelShader( CompileShader( ps_4_0, ps_main() ) );\n\t\t}\n\t}"), "KunosShader")){
                E = new Effect(device, bc);
			}

			TechPerPixel = E.GetTechniqueByName("PerPixel");

			for (var i = 0; i < TechPerPixel.Description.PassCount && InputSignaturePNT == null; i++) {
				InputSignaturePNT = TechPerPixel.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePNT == null) throw new System.Exception("input signature (KunosShader, PNT, PerPixel) == null");
			LayoutPNT = new InputLayout(device, InputSignaturePNT, InputLayouts.VerticePNT.InputElementsValue);

			FxWorld = E.GetVariableByName("gWorld").AsMatrix();
			FxWorldInvTranspose = E.GetVariableByName("gWorldInvTranspose").AsMatrix();
			FxWorldViewProj = E.GetVariableByName("gWorldViewProj").AsMatrix();
			FxDiffuseMap = E.GetVariableByName("gDiffuseMap").AsResource();
			FxEyePosW = E.GetVariableByName("gEyePosW").AsVector();
			FxMaterial = E.GetVariableByName("gMaterial");
		}

        public void Dispose() {
            E.Dispose();
			InputSignaturePNT.Dispose();
            LayoutPNT.Dispose();
        }
	}

	public class EffectPpBasic : IEffectWrapper {
		
		
        public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectTechnique TechFxaa;

		public EffectResourceVariable FxInputMap;
		public EffectVectorVariable FxScreenSize;

		public EffectPpBasic() {
		}

		public void Initialize(Device device) {
            using (var bc = EffectUtils.Compile(Encoding.UTF8.GetBytes("\n\tTexture2D gInputMap;\n\n\tSamplerState samInputImage {\n\t\tFilter = MIN_MAG_LINEAR_MIP_POINT;\n\t\tAddressU = CLAMP;\n\t\tAddressV = CLAMP;\n\t};\n\t\n\n\tcbuffer cbPerObject : register(b0) {\n\t\tfloat4 gScreenSize;\n\t}\n\n\n\tstruct VS_IN {\n\t\tfloat3 PosL    : POSITION;\n\t\tfloat2 Tex     : TEXCOORD;\n\t};\n\n\tstruct PS_IN {\n\t\tfloat4 PosH    : SV_POSITION;\n\t\tfloat2 Tex     : TEXCOORD;\n\t};\n\n\n\tPS_IN vs_main(VS_IN vin) {\n\t\tPS_IN vout;\n\t\tvout.PosH = float4(vin.PosL, 1.0f);\n\t\tvout.Tex = vin.Tex;\n\t\treturn vout;\n\t}\n\n\n\t#define FXAA_PRESET 5\n\t#include \"FXAA.fx\"\n\n\tfloat4 ps_Fxaa(PS_IN pin) : SV_Target {\n\t\tFxaaTex tex = { samInputImage, gInputMap };\n\t\tfloat3 aaImage = FxaaPixelShader(pin.Tex, tex, gScreenSize.zw);\n\t\treturn float4(aaImage, 1.0f);\n\t}\n\n\ttechnique11 Fxaa { \n\t\tpass P0 {\n\t\t\tSetVertexShader( CompileShader( vs_5_0, vs_main() ) );\n\t\t\tSetGeometryShader( NULL );\n\t\t\tSetPixelShader( CompileShader( ps_5_0, ps_Fxaa() ) );\n\t\t}\n\t}"), "PpBasic")){
                E = new Effect(device, bc);
			}

			TechFxaa = E.GetTechniqueByName("Fxaa");

			for (var i = 0; i < TechFxaa.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechFxaa.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (PpBasic, PT, Fxaa) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);

			FxInputMap = E.GetVariableByName("gInputMap").AsResource();
			FxScreenSize = E.GetVariableByName("gScreenSize").AsVector();
		}

        public void Dispose() {
            E.Dispose();
			InputSignaturePT.Dispose();
            LayoutPT.Dispose();
        }
	}

	public class EffectPpBlur : IEffectWrapper {
		
		public const int SampleCount = 15;
        public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectTechnique TechGaussianBlur, TechReflectionGaussianBlur;

		public EffectResourceVariable FxInputMap, FxMapsMap;
		public EffectScalarVariable FxSampleWeights, FxPower;
		public EffectVectorVariable FxSampleOffsets;

		public EffectPpBlur() {
		}

		public void Initialize(Device device) {
            using (var bc = EffectUtils.Compile(Encoding.UTF8.GetBytes("#include \"Common.fx\"\n\nTexture2D gMapsMap;\n\nstatic const int SAMPLE_COUNT = 15;\n\ncbuffer cbPerFrame : register(b0) {\n\tfloat4 gSampleOffsets[SAMPLE_COUNT];\n\tfloat gSampleWeights[SAMPLE_COUNT];\n\tfloat gPower;\n}\n\n\n\tfloat4 ps_GaussianBlurDebug(PS_IN pin) : SV_Target {\n\t\treturn tex(pin.Tex);\n\t}\n\n\tfloat4 ps_GaussianBlur(PS_IN pin) : SV_Target {\n\t\tfloat4 c = 0;\n\t\tfor (int i = 0; i < SAMPLE_COUNT; i++){\n\t\t\tc += tex(pin.Tex + gSampleOffsets[i] * gPower) * gSampleWeights[i];\n\t\t}\n\t\treturn c;\n\t}\n\n\ttechnique11 GaussianBlur {\n\t\tpass P0 {\n\t\t\tSetVertexShader( CompileShader( vs_4_0, vs_main() ) );\n\t\t\tSetGeometryShader( NULL );\n\t\t\tSetPixelShader( CompileShader( ps_4_0, ps_GaussianBlur() ) );\n\t\t}\n\t}\n\n\n\tfloat4 ps_ReflectionGaussianBlur(PS_IN pin) : SV_Target {\n\t\tfloat power = saturate(1 - tex(gMapsMap, pin.Tex).y * 8);\n\n\t\tfloat4 c = 0;\n\t\tfor (int i = 0; i < SAMPLE_COUNT; i++){\n\t\t\tc += tex(pin.Tex + gSampleOffsets[i] * power) * gSampleWeights[i];\n\t\t}\n\n\t\treturn c;\n\t}\n\n\ttechnique11 ReflectionGaussianBlur {\n\t\tpass P0 {\n\t\t\tSetVertexShader( CompileShader( vs_4_0, vs_main() ) );\n\t\t\tSetGeometryShader( NULL );\n\t\t\tSetPixelShader( CompileShader( ps_4_0, ps_ReflectionGaussianBlur() ) );\n\t\t}\n}"), "PpBlur")){
                E = new Effect(device, bc);
			}

			TechGaussianBlur = E.GetTechniqueByName("GaussianBlur");
			TechReflectionGaussianBlur = E.GetTechniqueByName("ReflectionGaussianBlur");

			for (var i = 0; i < TechGaussianBlur.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechGaussianBlur.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (PpBlur, PT, GaussianBlur) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);

			FxInputMap = E.GetVariableByName("gInputMap").AsResource();
			FxMapsMap = E.GetVariableByName("gMapsMap").AsResource();
			FxSampleWeights = E.GetVariableByName("gSampleWeights").AsScalar();
			FxPower = E.GetVariableByName("gPower").AsScalar();
			FxSampleOffsets = E.GetVariableByName("gSampleOffsets").AsVector();
		}

        public void Dispose() {
            E.Dispose();
			InputSignaturePT.Dispose();
            LayoutPT.Dispose();
        }
	}

	public class EffectPpHdr : IEffectWrapper {
		
		public static readonly Vector3 LumConvert = new Vector3(0.299f, 0.587f, 0.114f);
        public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectTechnique TechDownsampling, TechAdaptation, TechTonemap, TechCopy, TechBloom;

		public EffectResourceVariable FxInputMap, FxBrightnessMap, FxBloomMap;
		public EffectVectorVariable FxPixel, FxCropImage;

		public EffectPpHdr() {
		}

		public void Initialize(Device device) {
            using (var bc = EffectUtils.Compile(Encoding.UTF8.GetBytes("#include \"Common.fx\"\n\n\tcbuffer cbPerFrame : register(b0) {\n\t\tfloat2 gPixel;\n\t\tfloat2 gCropImage;\n\t}\n\n\n\tfloat4 ps_Downsampling (PS_IN pin) : SV_Target {\n\t\tfloat2 uv = pin.Tex * gCropImage + 0.5 - gCropImage / 2;\n\t\tfloat2 delta = gPixel * uv;\n\t\t\n\t\tfloat4 color = tex(uv);\n\t\tcolor += tex(uv + float2(-delta.x, 0));\n\t\tcolor += tex(uv + float2(delta.x, 0));\n\t\tcolor += tex(uv + float2(0, -delta.y));\n\t\tcolor += tex(uv + float2(0, delta.y));\n\t\treturn saturate(color / 5);\n\t}\n\n\ttechnique11 Downsampling {\n\t\tpass P0 {\n\t\t\tSetVertexShader( CompileShader( vs_4_0, vs_main() ) );\n\t\t\tSetGeometryShader( NULL );\n\t\t\tSetPixelShader( CompileShader( ps_4_0, ps_Downsampling() ) );\n\t\t}\n\t}\n\n\n\tTexture2D gBrightnessMap;\n\n\tfloat4 ps_Adaptation (PS_IN pin) : SV_Target {\n\t\treturn (tex(0.5) * 49 + tex(gBrightnessMap, 0.5)) / 50;\n\t}\n\n\ttechnique11 Adaptation {\n\t\tpass P0 {\n\t\t\tSetVertexShader( CompileShader( vs_4_0, vs_main() ) );\n\t\t\tSetGeometryShader( NULL );\n\t\t\tSetPixelShader( CompileShader( ps_4_0, ps_Adaptation() ) );\n\t\t}\n\t}\n\t\n\n\tstatic const float3 LUM_CONVERT = float3(0.299f, 0.587f, 0.114f);\n\n\tTexture2D gBloomMap;\n\n\tfloat3 ToneReinhard(float3 vColor, float average, float exposure, float whitePoint){\n\t\t\n\t\tconst float3x3 RGB2XYZ = {  0.5141364, 0.3238786,  0.16036376,\n\t\t\t\t\t\t\t\t\t0.265068,  0.67023428, 0.06409157,\n\t\t\t\t\t\t\t\t\t0.0241188, 0.1228178,  0.84442666  };\t\t\t\t                    \n\t\tfloat3 XYZ = mul(RGB2XYZ, vColor.rgb);\n  \n\t\t\n\t\tfloat3 Yxy;\n\t\tYxy.r = XYZ.g;                            \n\t\tYxy.g = XYZ.r / (XYZ.r + XYZ.g + XYZ.b ); \n\t\tYxy.b = XYZ.g / (XYZ.r + XYZ.g + XYZ.b ); \n    \n\t\t\n\t\tfloat Lp = Yxy.r * exposure / average;         \n                \n\t\t\n\t\tYxy.r = (Lp * (1.0f + Lp/(whitePoint * whitePoint)))/(1.0f + Lp);\n  \n\t\t\n\t\tXYZ.r = Yxy.r * Yxy.g / Yxy. b;               \n\t\tXYZ.g = Yxy.r;                                \n\t\tXYZ.b = Yxy.r * (1 - Yxy.g - Yxy.b) / Yxy.b;  \n    \n\t\t\n\t\tconst float3x3 XYZ2RGB  = {  2.5651, -1.1665, -0.3986,\n\t\t\t\t\t\t\t\t\t-1.0217,  1.9777,  0.0439, \n\t\t\t\t\t\t\t\t\t 0.0753, -0.2543,  1.1892  };\n\t\treturn saturate(mul(XYZ2RGB, XYZ));\n\t}\n\n\tfloat4 ps_Tonemap (PS_IN pin) : SV_Target {\n\t\tfloat currentBrightness = 0.167 + dot(tex(gBrightnessMap, 0.5).rgb, LUM_CONVERT) * 0.667;\n\t\treturn float4(ToneReinhard(tex(pin.Tex).rgb, currentBrightness, 0.56, 1.2), 1) + tex(gBloomMap, pin.Tex);\n\t}\n\n\ttechnique11 Tonemap {\n\t\tpass P0 {\n\t\t\tSetVertexShader( CompileShader( vs_4_0, vs_main() ) );\n\t\t\tSetGeometryShader( NULL );\n\t\t\tSetPixelShader( CompileShader( ps_4_0, ps_Tonemap() ) );\n\t\t}\n\t}\n\t\n\n\tfloat4 ps_Copy (PS_IN pin) : SV_Target {\n\t\treturn tex(pin.Tex);\n\t}\n\n\ttechnique11 Copy {\n\t\tpass P0 {\n\t\t\tSetVertexShader( CompileShader( vs_4_0, vs_main() ) );\n\t\t\tSetGeometryShader( NULL );\n\t\t\tSetPixelShader( CompileShader( ps_4_0, ps_Copy() ) );\n\t\t}\n\t}\n\n\n\tfloat4 ps_Bloom (PS_IN pin) : SV_Target {\n\t\treturn saturate(tex(pin.Tex) - 1.0);\n\t}\n\n\ttechnique11 Bloom {\n\t\tpass P0 {\n\t\t\tSetVertexShader( CompileShader( vs_4_0, vs_main() ) );\n\t\t\tSetGeometryShader( NULL );\n\t\t\tSetPixelShader( CompileShader( ps_4_0, ps_Bloom() ) );\n\t\t}\n\t}"), "PpHdr")){
                E = new Effect(device, bc);
			}

			TechDownsampling = E.GetTechniqueByName("Downsampling");
			TechAdaptation = E.GetTechniqueByName("Adaptation");
			TechTonemap = E.GetTechniqueByName("Tonemap");
			TechCopy = E.GetTechniqueByName("Copy");
			TechBloom = E.GetTechniqueByName("Bloom");

			for (var i = 0; i < TechDownsampling.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechDownsampling.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (PpHdr, PT, Downsampling) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);

			FxInputMap = E.GetVariableByName("gInputMap").AsResource();
			FxBrightnessMap = E.GetVariableByName("gBrightnessMap").AsResource();
			FxBloomMap = E.GetVariableByName("gBloomMap").AsResource();
			FxPixel = E.GetVariableByName("gPixel").AsVector();
			FxCropImage = E.GetVariableByName("gCropImage").AsVector();
		}

        public void Dispose() {
            E.Dispose();
			InputSignaturePT.Dispose();
            LayoutPT.Dispose();
        }
	}

	public class EffectTestingCube : IEffectWrapper {
		
		
        public Effect E;

        public ShaderSignature InputSignaturePC;
        public InputLayout LayoutPC;

		public EffectTechnique TechCube;

		public EffectMatrixVariable FxWorldViewProj { get; private set; }

		public EffectTestingCube() {
		}

		public void Initialize(Device device) {
            using (var bc = EffectUtils.Compile(Encoding.UTF8.GetBytes("struct VS_IN {\n    float3 pos : POSITION;\n    float4 col : COLOR;\n};\n\nstruct PS_IN {\n    float4 pos : SV_POSITION;\n    float4 col : COLOR;\n};\n\ncbuffer cbPerObject : register(b0) {\n\tfloat4x4 gWorldViewProj;\n}\n\nPS_IN vs_main( VS_IN input ){\n    PS_IN output = (PS_IN)0;\n    \n    output.pos = mul(float4(input.pos, 1.0f), gWorldViewProj);\n    output.col = input.col;\n    \n    return output;\n}\n\nfloat4 ps_main( PS_IN input ) : SV_Target {\n    return input.col;\n}\n\ntechnique11 Cube { \n    pass P0 {\n        SetVertexShader( CompileShader( vs_4_0, vs_main() ) );\n        SetGeometryShader( NULL );\n        SetPixelShader( CompileShader( ps_4_0, ps_main() ) );\n    }\n}"), "TestingCube")){
                E = new Effect(device, bc);
			}

			TechCube = E.GetTechniqueByName("Cube");

			for (var i = 0; i < TechCube.Description.PassCount && InputSignaturePC == null; i++) {
				InputSignaturePC = TechCube.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePC == null) throw new System.Exception("input signature (TestingCube, PC, Cube) == null");
			LayoutPC = new InputLayout(device, InputSignaturePC, InputLayouts.VerticePC.InputElementsValue);

			FxWorldViewProj = E.GetVariableByName("gWorldViewProj").AsMatrix();
		}

        public void Dispose() {
            E.Dispose();
			InputSignaturePC.Dispose();
            LayoutPC.Dispose();
        }
	}

	public class EffectTestingPnt : IEffectWrapper {
		
		
        public Effect E;

        public ShaderSignature InputSignaturePNT;
        public InputLayout LayoutPNT;

		public EffectTechnique TechCube;

		public EffectMatrixVariable FxWorldViewProj { get; private set; }

		public EffectTestingPnt() {
		}

		public void Initialize(Device device) {
            using (var bc = EffectUtils.Compile(Encoding.UTF8.GetBytes("struct VS_IN {\n    float3 pos    : POSITION;\n    float3 nor    : NORMAL;\n\tfloat2 tex    : TEXCOORD;\n};\n\nstruct PS_IN {\n    float4 pos : SV_POSITION;\n    float4 col : COLOR;\n};\n\ncbuffer cbPerObject : register(b0) {\n\tfloat4x4 gWorldViewProj;\n}\n\nPS_IN vs_main( VS_IN input ){\n    PS_IN output = (PS_IN)0;\n    \n    output.pos = mul(float4(input.pos, 1.0f), gWorldViewProj);\n    output.col = float4(input.tex, input.nor.xy);\n    \n    return output;\n}\n\nfloat4 ps_main( PS_IN input ) : SV_Target {\n    return input.col;\n}\n\ntechnique11 Cube { \n    pass P0 {\n        SetVertexShader( CompileShader( vs_4_0, vs_main() ) );\n        SetGeometryShader( NULL );\n        SetPixelShader( CompileShader( ps_4_0, ps_main() ) );\n    }\n}"), "TestingPnt")){
                E = new Effect(device, bc);
			}

			TechCube = E.GetTechniqueByName("Cube");

			for (var i = 0; i < TechCube.Description.PassCount && InputSignaturePNT == null; i++) {
				InputSignaturePNT = TechCube.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePNT == null) throw new System.Exception("input signature (TestingPnt, PNT, Cube) == null");
			LayoutPNT = new InputLayout(device, InputSignaturePNT, InputLayouts.VerticePNT.InputElementsValue);

			FxWorldViewProj = E.GetVariableByName("gWorldViewProj").AsMatrix();
		}

        public void Dispose() {
            E.Dispose();
			InputSignaturePNT.Dispose();
            LayoutPNT.Dispose();
        }
	}


	public static class EffectUtils {
		private static string GetIncludedData(string filename){
			switch(filename){
				case "Common.fx":
					return "SamplerState samInputImage {\n\tFilter = MIN_MAG_LINEAR_MIP_POINT;\n\tAddressU = CLAMP;\n\tAddressV = CLAMP;\n};\n\nTexture2D gInputMap;\n\nfloat4 tex(float2 uv){\n\treturn gInputMap.SampleLevel(samInputImage, uv, 0.0);\n}\n\nfloat4 tex(Texture2D t, float2 uv){\n\treturn t.SampleLevel(samInputImage, uv, 0.0);\n}\n\nstruct VS_IN {\n\tfloat3 PosL    : POSITION;\n\tfloat2 Tex     : TEXCOORD;\n};\n\nstruct PS_IN {\n\tfloat4 PosH    : SV_POSITION;\n\tfloat2 Tex     : TEXCOORD;\n};\n\nPS_IN vs_main(VS_IN vin) {\n\tPS_IN vout;\n\tvout.PosH = float4(vin.PosL, 1.0f);\n\tvout.Tex = vin.Tex;\n\treturn vout;\n}\n";
				case "Deferred.fx":
					return "struct VS_IN {\n\tfloat3 PosL       : POSITION;\n\tfloat3 NormalL    : NORMAL;\n\tfloat2 Tex        : TEXCOORD;\n\tfloat3 TangentL   : TANGENT;\n};\n\nstruct PS_IN {\n\tfloat4 PosH       : SV_POSITION;\n\tfloat3 PosW       : POSITION;\n\tfloat3 NormalW    : NORMAL;\n\tfloat2 Tex        : TEXCOORD;\n\tfloat3 TangentW   : TANGENT;\n};\n\nstruct PS_OUT {\n\tfloat4 Base   : SV_Target0;\n\tfloat3 Normal : SV_Target1;\n\tfloat4 Maps   : SV_Target2;\n};\n\nSamplerState samAnisotropic {\n\tFilter = ANISOTROPIC;\n\tMaxAnisotropy = 4;\n\n\tAddressU = WRAP;\n\tAddressV = WRAP;\n};\n\n";
				case "FXAA.fx":
					return "// Copyright (c) 2011 NVIDIA Corporation. All rights reserved.\n//\n// TO  THE MAXIMUM  EXTENT PERMITTED  BY APPLICABLE  LAW, THIS SOFTWARE  IS PROVIDED\n// *AS IS*  AND NVIDIA AND  ITS SUPPLIERS DISCLAIM  ALL WARRANTIES,  EITHER  EXPRESS\n// OR IMPLIED, INCLUDING, BUT NOT LIMITED  TO, NONINFRINGEMENT,IMPLIED WARRANTIES OF\n// MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE.  IN NO EVENT SHALL  NVIDIA \n// OR ITS SUPPLIERS BE  LIABLE  FOR  ANY  DIRECT, SPECIAL,  INCIDENTAL,  INDIRECT,  OR  \n// CONSEQUENTIAL DAMAGES WHATSOEVER (INCLUDING, WITHOUT LIMITATION,  DAMAGES FOR LOSS \n// OF BUSINESS PROFITS, BUSINESS INTERRUPTION, LOSS OF BUSINESS INFORMATION, OR ANY \n// OTHER PECUNIARY LOSS) ARISING OUT OF THE  USE OF OR INABILITY  TO USE THIS SOFTWARE, \n// EVEN IF NVIDIA HAS BEEN ADVISED OF THE POSSIBILITY OF SUCH DAMAGES.\n//\n// Please direct any bugs or questions to SDKFeedback@nvidia.com\n\n#define FXAA_HLSL_4 1\n\n\n/*============================================================================\n \n                                    FXAA                                 \n \n============================================================================*/\n \n/*============================================================================\n                                 API PORTING\n============================================================================*/\n#ifndef     FXAA_GLSL_120\n    #define FXAA_GLSL_120 0\n#endif\n#ifndef     FXAA_GLSL_130\n    #define FXAA_GLSL_130 0\n#endif\n#ifndef     FXAA_HLSL_3\n    #define FXAA_HLSL_3 0\n#endif\n#ifndef     FXAA_HLSL_4\n    #define FXAA_HLSL_4 0\n#endif    \n/*--------------------------------------------------------------------------*/\n#if FXAA_GLSL_120\n    // Requires,\n    //  #version 120\n    //  #extension GL_EXT_gpu_shader4 : enable\n    #define int2 ivec2\n    #define float2 vec2\n    #define float3 vec3\n    #define float4 vec4\n    #define FxaaBool3 bvec3\n    #define FxaaInt2 ivec2\n    #define FxaaFloat2 vec2\n    #define FxaaFloat3 vec3\n    #define FxaaFloat4 vec4\n    #define FxaaBool2Float(a) mix(0.0, 1.0, (a))\n    #define FxaaPow3(x, y) pow(x, y)\n    #define FxaaSel3(f, t, b) mix((f), (t), (b))\n    #define FxaaTex sampler2D\n#endif\n/*--------------------------------------------------------------------------*/\n#if FXAA_GLSL_130\n    // Requires \"#version 130\" or better\n    #define int2 ivec2\n    #define float2 vec2\n    #define float3 vec3\n    #define float4 vec4\n    #define FxaaBool3 bvec3\n    #define FxaaInt2 ivec2\n    #define FxaaFloat2 vec2\n    #define FxaaFloat3 vec3\n    #define FxaaFloat4 vec4\n    #define FxaaBool2Float(a) mix(0.0, 1.0, (a))\n    #define FxaaPow3(x, y) pow(x, y)\n    #define FxaaSel3(f, t, b) mix((f), (t), (b))\n    #define FxaaTex sampler2D\n#endif\n/*--------------------------------------------------------------------------*/\n#if FXAA_HLSL_3\n    #define int2 float2\n    #define FxaaInt2 float2\n    #define FxaaFloat2 float2\n    #define FxaaFloat3 float3\n    #define FxaaFloat4 float4\n    #define FxaaBool2Float(a) (a)\n    #define FxaaPow3(x, y) pow(x, y)\n    #define FxaaSel3(f, t, b) ((f)*(!b) + (t)*(b))\n    #define FxaaTex sampler2D\n#endif\n/*--------------------------------------------------------------------------*/\n#if FXAA_HLSL_4\n    #define FxaaInt2 int2\n    #define FxaaFloat2 float2\n    #define FxaaFloat3 float3\n    #define FxaaFloat4 float4\n    #define FxaaBool2Float(a) (a)\n    #define FxaaPow3(x, y) pow(x, y)\n    #define FxaaSel3(f, t, b) ((f)*(!b) + (t)*(b))\n    struct FxaaTex { SamplerState smpl; Texture2D tex; };\n#endif\n/*--------------------------------------------------------------------------*/\n#define FxaaToFloat3(a) FxaaFloat3((a), (a), (a))\n/*--------------------------------------------------------------------------*/\nfloat4 FxaaTexLod0(FxaaTex tex, float2 pos) {\n    #if FXAA_GLSL_120\n        return texture2DLod(tex, pos.xy, 0.0);\n    #endif\n    #if FXAA_GLSL_130\n        return textureLod(tex, pos.xy, 0.0);\n    #endif\n    #if FXAA_HLSL_3\n        return tex2Dlod(tex, float4(pos.xy, 0.0, 0.0)); \n    #endif\n    #if FXAA_HLSL_4\n        return tex.tex.SampleLevel(tex.smpl, pos.xy, 0.0);\n    #endif\n}\n/*--------------------------------------------------------------------------*/\nfloat4 FxaaTexGrad(FxaaTex tex, float2 pos, float2 grad) {\n    #if FXAA_GLSL_120\n        return texture2DGrad(tex, pos.xy, grad, grad);\n    #endif\n    #if FXAA_GLSL_130\n        return textureGrad(tex, pos.xy, grad, grad);\n    #endif\n    #if FXAA_HLSL_3\n        return tex2Dgrad(tex, pos.xy, grad, grad); \n    #endif\n    #if FXAA_HLSL_4\n        return tex.tex.SampleGrad(tex.smpl, pos.xy, grad, grad);\n    #endif\n}\n/*--------------------------------------------------------------------------*/\nfloat4 FxaaTexOff(FxaaTex tex, float2 pos, int2 off, float2 rcpFrame) {\n    #if FXAA_GLSL_120\n        return texture2DLodOffset(tex, pos.xy, 0.0, off.xy);\n    #endif\n    #if FXAA_GLSL_130\n        return textureLodOffset(tex, pos.xy, 0.0, off.xy);\n    #endif\n    #if FXAA_HLSL_3\n        return tex2Dlod(tex, float4(pos.xy + (off * rcpFrame), 0, 0)); \n    #endif\n    #if FXAA_HLSL_4\n        return tex.tex.SampleLevel(tex.smpl, pos.xy, 0.0, off.xy);\n    #endif\n}\n\n/*============================================================================\n                                 SRGB KNOBS\n------------------------------------------------------------------------------\nFXAA_SRGB_ROP - Set to 1 when applying FXAA to an sRGB back buffer (DX10/11).\n                This will do the sRGB to linear transform, \n                as ROP will expect linear color from this shader,\n                and this shader works in non-linear color.\n============================================================================*/\n#define FXAA_SRGB_ROP 0\n\n/*============================================================================\n                                DEBUG KNOBS\n------------------------------------------------------------------------------\nAll debug knobs draw FXAA-untouched pixels in FXAA computed luma (monochrome).\n \nFXAA_DEBUG_PASSTHROUGH - Red for pixels which are filtered by FXAA with a\n                         yellow tint on sub-pixel aliasing filtered by FXAA.\nFXAA_DEBUG_HORZVERT    - Blue for horizontal edges, gold for vertical edges. \nFXAA_DEBUG_PAIR        - Blue/green for the 2 pixel pair choice. \nFXAA_DEBUG_NEGPOS      - Red/blue for which side of center of span.\nFXAA_DEBUG_OFFSET      - Red/blue for -/+ x, gold/skyblue for -/+ y.\n============================================================================*/\n#ifndef     FXAA_DEBUG_PASSTHROUGH\n    #define FXAA_DEBUG_PASSTHROUGH 0\n#endif    \n#ifndef     FXAA_DEBUG_HORZVERT\n    #define FXAA_DEBUG_HORZVERT    0\n#endif    \n#ifndef     FXAA_DEBUG_PAIR   \n    #define FXAA_DEBUG_PAIR        0\n#endif    \n#ifndef     FXAA_DEBUG_NEGPOS\n    #define FXAA_DEBUG_NEGPOS      0\n#endif\n#ifndef     FXAA_DEBUG_OFFSET\n    #define FXAA_DEBUG_OFFSET      0\n#endif    \n/*--------------------------------------------------------------------------*/\n#if FXAA_DEBUG_PASSTHROUGH || FXAA_DEBUG_HORZVERT || FXAA_DEBUG_PAIR\n    #define FXAA_DEBUG 1\n#endif    \n#if FXAA_DEBUG_NEGPOS || FXAA_DEBUG_OFFSET\n    #define FXAA_DEBUG 1\n#endif\n#ifndef FXAA_DEBUG\n    #define FXAA_DEBUG 0\n#endif\n  \n/*============================================================================\n                              COMPILE-IN KNOBS\n------------------------------------------------------------------------------\nFXAA_PRESET - Choose compile-in knob preset 0-5.\n------------------------------------------------------------------------------\nFXAA_EDGE_THRESHOLD - The minimum amount of local contrast required \n                      to apply algorithm.\n                      1.0/3.0  - too little\n                      1.0/4.0  - good start\n                      1.0/8.0  - applies to more edges\n                      1.0/16.0 - overkill\n------------------------------------------------------------------------------\nFXAA_EDGE_THRESHOLD_MIN - Trims the algorithm from processing darks.\n                          Perf optimization.\n                          1.0/32.0 - visible limit (smaller isn't visible)\n                          1.0/16.0 - good compromise\n                          1.0/12.0 - upper limit (seeing artifacts)\n------------------------------------------------------------------------------\nFXAA_SEARCH_STEPS - Maximum number of search steps for end of span.\n------------------------------------------------------------------------------\nFXAA_SEARCH_ACCELERATION - How much to accelerate search,\n                           1 - no acceleration\n                           2 - skip by 2 pixels\n                           3 - skip by 3 pixels\n                           4 - skip by 4 pixels\n------------------------------------------------------------------------------\nFXAA_SEARCH_THRESHOLD - Controls when to stop searching.\n                        1.0/4.0 - seems to be the best quality wise\n------------------------------------------------------------------------------\nFXAA_SUBPIX_FASTER - Turn on lower quality but faster subpix path.\n                     Not recomended, but used in preset 0.\n------------------------------------------------------------------------------\nFXAA_SUBPIX - Toggle subpix filtering.\n              0 - turn off\n              1 - turn on\n              2 - turn on full (ignores FXAA_SUBPIX_TRIM and CAP)\n------------------------------------------------------------------------------\nFXAA_SUBPIX_TRIM - Controls sub-pixel aliasing removal.\n                   1.0/2.0 - low removal\n                   1.0/3.0 - medium removal\n                   1.0/4.0 - default removal\n                   1.0/8.0 - high removal\n                   0.0 - complete removal\n------------------------------------------------------------------------------\nFXAA_SUBPIX_CAP - Insures fine detail is not completely removed.\n                  This is important for the transition of sub-pixel detail,\n                  like fences and wires.\n                  3.0/4.0 - default (medium amount of filtering)\n                  7.0/8.0 - high amount of filtering\n                  1.0 - no capping of sub-pixel aliasing removal\n============================================================================*/\n#ifndef FXAA_PRESET\n    #define FXAA_PRESET 3\n#endif\n/*--------------------------------------------------------------------------*/\n#if (FXAA_PRESET == 0)\n    #define FXAA_EDGE_THRESHOLD      (1.0/4.0)\n    #define FXAA_EDGE_THRESHOLD_MIN  (1.0/12.0)\n    #define FXAA_SEARCH_STEPS        2\n    #define FXAA_SEARCH_ACCELERATION 4\n    #define FXAA_SEARCH_THRESHOLD    (1.0/4.0)\n    #define FXAA_SUBPIX              1\n    #define FXAA_SUBPIX_FASTER       1\n    #define FXAA_SUBPIX_CAP          (2.0/3.0)\n    #define FXAA_SUBPIX_TRIM         (1.0/4.0)\n#endif\n/*--------------------------------------------------------------------------*/\n#if (FXAA_PRESET == 1)\n    #define FXAA_EDGE_THRESHOLD      (1.0/8.0)\n    #define FXAA_EDGE_THRESHOLD_MIN  (1.0/16.0)\n    #define FXAA_SEARCH_STEPS        4\n    #define FXAA_SEARCH_ACCELERATION 3\n    #define FXAA_SEARCH_THRESHOLD    (1.0/4.0)\n    #define FXAA_SUBPIX              1\n    #define FXAA_SUBPIX_FASTER       0\n    #define FXAA_SUBPIX_CAP          (3.0/4.0)\n    #define FXAA_SUBPIX_TRIM         (1.0/4.0)\n#endif\n/*--------------------------------------------------------------------------*/\n#if (FXAA_PRESET == 2)\n    #define FXAA_EDGE_THRESHOLD      (1.0/8.0)\n    #define FXAA_EDGE_THRESHOLD_MIN  (1.0/24.0)\n    #define FXAA_SEARCH_STEPS        8\n    #define FXAA_SEARCH_ACCELERATION 2\n    #define FXAA_SEARCH_THRESHOLD    (1.0/4.0)\n    #define FXAA_SUBPIX              1\n    #define FXAA_SUBPIX_FASTER       0\n    #define FXAA_SUBPIX_CAP          (3.0/4.0)\n    #define FXAA_SUBPIX_TRIM         (1.0/4.0)\n#endif\n/*--------------------------------------------------------------------------*/\n#if (FXAA_PRESET == 3)\n    #define FXAA_EDGE_THRESHOLD      (1.0/8.0)\n    #define FXAA_EDGE_THRESHOLD_MIN  (1.0/24.0)\n    #define FXAA_SEARCH_STEPS        16\n    #define FXAA_SEARCH_ACCELERATION 1\n    #define FXAA_SEARCH_THRESHOLD    (1.0/4.0)\n    #define FXAA_SUBPIX              1\n    #define FXAA_SUBPIX_FASTER       0\n    #define FXAA_SUBPIX_CAP          (3.0/4.0)\n    #define FXAA_SUBPIX_TRIM         (1.0/4.0)\n#endif\n/*--------------------------------------------------------------------------*/\n#if (FXAA_PRESET == 4)\n    #define FXAA_EDGE_THRESHOLD      (1.0/8.0)\n    #define FXAA_EDGE_THRESHOLD_MIN  (1.0/24.0)\n    #define FXAA_SEARCH_STEPS        24\n    #define FXAA_SEARCH_ACCELERATION 1\n    #define FXAA_SEARCH_THRESHOLD    (1.0/4.0)\n    #define FXAA_SUBPIX              1\n    #define FXAA_SUBPIX_FASTER       0\n    #define FXAA_SUBPIX_CAP          (3.0/4.0)\n    #define FXAA_SUBPIX_TRIM         (1.0/4.0)\n#endif\n/*--------------------------------------------------------------------------*/\n#if (FXAA_PRESET == 5)\n    #define FXAA_EDGE_THRESHOLD      (1.0/8.0)\n    #define FXAA_EDGE_THRESHOLD_MIN  (1.0/24.0)\n    #define FXAA_SEARCH_STEPS        32\n    #define FXAA_SEARCH_ACCELERATION 1\n    #define FXAA_SEARCH_THRESHOLD    (1.0/4.0)\n    #define FXAA_SUBPIX              1\n    #define FXAA_SUBPIX_FASTER       0\n    #define FXAA_SUBPIX_CAP          (3.0/4.0)\n    #define FXAA_SUBPIX_TRIM         (1.0/4.0)\n#endif\n/*--------------------------------------------------------------------------*/\n#define FXAA_SUBPIX_TRIM_SCALE (1.0/(1.0 - FXAA_SUBPIX_TRIM))\n\n/*============================================================================\n                                   HELPERS\n============================================================================*/\n// Return the luma, the estimation of luminance from rgb inputs.\n// This approximates luma using one FMA instruction,\n// skipping normalization and tossing out blue.\n// FxaaLuma() will range 0.0 to 2.963210702.\nfloat FxaaLuma(float3 rgb) {\n    return rgb.y * (0.587/0.299) + rgb.x; } \n/*--------------------------------------------------------------------------*/\nfloat3 FxaaLerp3(float3 a, float3 b, float amountOfA) {\n    return (FxaaToFloat3(-amountOfA) * b) + \n        ((a * FxaaToFloat3(amountOfA)) + b); } \n/*--------------------------------------------------------------------------*/\n// Support any extra filtering before returning color.\nfloat3 FxaaFilterReturn(float3 rgb) {\n    #if FXAA_SRGB_ROP\n        // Do sRGB encoded value to linear conversion.\n        return FxaaSel3(\n            rgb * FxaaToFloat3(1.0/12.92), \n            FxaaPow3(\n                rgb * FxaaToFloat3(1.0/1.055) + FxaaToFloat3(0.055/1.055), \n                FxaaToFloat3(2.4)),\n            rgb > FxaaToFloat3(0.04045)); \n    #else\n        return rgb;\n    #endif\n}\n \n/*============================================================================\n                                VERTEX SHADER\n============================================================================*/\nfloat2 FxaaVertexShader(\n// Both x and y range {-1.0 to 1.0 across screen}.\nfloat2 inPos) {\n    float2 pos;\n    pos.xy = (inPos.xy * FxaaFloat2(0.5, 0.5)) + FxaaFloat2(0.5, 0.5);\n    return pos; }  \n \n/*============================================================================\n \n                                PIXEL SHADER\n                                \n============================================================================*/\nfloat3 FxaaPixelShader(\n// Output of FxaaVertexShader interpolated across screen.\n//  xy -> actual texture position {0.0 to 1.0}\nfloat2 pos,\n// Input texture.\nFxaaTex tex,\n// RCPFRAME SHOULD PIXEL SHADER CONSTANTS!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!\n// {1.0/frameWidth, 1.0/frameHeight}\nfloat2 rcpFrame) {\n    \n/*----------------------------------------------------------------------------\n            EARLY EXIT IF LOCAL CONTRAST BELOW EDGE DETECT LIMIT\n------------------------------------------------------------------------------\nMajority of pixels of a typical image do not require filtering, \noften pixels are grouped into blocks which could benefit from early exit \nright at the beginning of the algorithm. \nGiven the following neighborhood, \n \n      N   \n    W M E\n      S   \n    \nIf the difference in local maximum and minimum luma (contrast \"range\") \nis lower than a threshold proportional to the maximum local luma (\"rangeMax\"), \nthen the shader early exits (no visible aliasing). \nThis threshold is clamped at a minimum value (\"FXAA_EDGE_THRESHOLD_MIN\")\nto avoid processing in really dark areas.    \n----------------------------------------------------------------------------*/\n    float3 rgbN = FxaaTexOff(tex, pos.xy, FxaaInt2( 0,-1), rcpFrame).xyz;\n    float3 rgbW = FxaaTexOff(tex, pos.xy, FxaaInt2(-1, 0), rcpFrame).xyz;\n    float3 rgbM = FxaaTexOff(tex, pos.xy, FxaaInt2( 0, 0), rcpFrame).xyz;\n    float3 rgbE = FxaaTexOff(tex, pos.xy, FxaaInt2( 1, 0), rcpFrame).xyz;\n    float3 rgbS = FxaaTexOff(tex, pos.xy, FxaaInt2( 0, 1), rcpFrame).xyz;\n    float lumaN = FxaaLuma(rgbN);\n    float lumaW = FxaaLuma(rgbW);\n    float lumaM = FxaaLuma(rgbM);\n    float lumaE = FxaaLuma(rgbE);\n    float lumaS = FxaaLuma(rgbS);\n    float rangeMin = min(lumaM, min(min(lumaN, lumaW), min(lumaS, lumaE)));\n    float rangeMax = max(lumaM, max(max(lumaN, lumaW), max(lumaS, lumaE)));\n    float range = rangeMax - rangeMin;\n    #if FXAA_DEBUG\n        float lumaO = lumaM / (1.0 + (0.587/0.299));\n    #endif        \n    if(range < max(FXAA_EDGE_THRESHOLD_MIN, rangeMax * FXAA_EDGE_THRESHOLD)) {\n        #if FXAA_DEBUG\n            return FxaaFilterReturn(FxaaToFloat3(lumaO));\n        #endif\n        return FxaaFilterReturn(rgbM); }\n    #if FXAA_SUBPIX > 0\n        #if FXAA_SUBPIX_FASTER\n            float3 rgbL = (rgbN + rgbW + rgbE + rgbS + rgbM) * \n                FxaaToFloat3(1.0/5.0);\n        #else\n            float3 rgbL = rgbN + rgbW + rgbM + rgbE + rgbS;\n        #endif\n    #endif        \n    \n/*----------------------------------------------------------------------------\n                               COMPUTE LOWPASS\n------------------------------------------------------------------------------\nFXAA computes a local neighborhood lowpass value as follows,\n \n  (N + W + E + S)/4\n  \nThen uses the ratio of the contrast range of the lowpass \nand the range found in the early exit check, \nas a sub-pixel aliasing detection filter. \nWhen FXAA detects sub-pixel aliasing (such as single pixel dots), \nit later blends in \"blendL\" amount \nof a lowpass value (computed in the next section) to the final result.\n----------------------------------------------------------------------------*/\n    #if FXAA_SUBPIX != 0\n        float lumaL = (lumaN + lumaW + lumaE + lumaS) * 0.25;\n        float rangeL = abs(lumaL - lumaM);\n    #endif        \n    #if FXAA_SUBPIX == 1\n        float blendL = max(0.0, \n            (rangeL / range) - FXAA_SUBPIX_TRIM) * FXAA_SUBPIX_TRIM_SCALE; \n        blendL = min(FXAA_SUBPIX_CAP, blendL);\n    #endif\n    #if FXAA_SUBPIX == 2\n        float blendL = rangeL / range; \n    #endif\n    #if FXAA_DEBUG_PASSTHROUGH\n        #if FXAA_SUBPIX == 0\n            float blendL = 0.0;\n        #endif\n        return FxaaFilterReturn(\n            FxaaFloat3(1.0, blendL/FXAA_SUBPIX_CAP, 0.0));\n    #endif    \n    \n/*----------------------------------------------------------------------------\n                    CHOOSE VERTICAL OR HORIZONTAL SEARCH\n------------------------------------------------------------------------------\nFXAA uses the following local neighborhood,\n \n    NW N NE\n    W  M  E\n    SW S SE\n    \nTo compute an edge amount for both vertical and horizontal directions.\nNote edge detect filters like Sobel fail on single pixel lines through M.\nFXAA takes the weighted average magnitude of the high-pass values \nfor rows and columns as an indication of local edge amount.\n \nA lowpass value for anti-sub-pixel-aliasing is computed as \n    (N+W+E+S+M+NW+NE+SW+SE)/9.\nThis full box pattern has higher quality than other options.\n \nNote following this block, both vertical and horizontal cases\nflow in parallel (reusing the horizontal variables).\n----------------------------------------------------------------------------*/\n    float3 rgbNW = FxaaTexOff(tex, pos.xy, FxaaInt2(-1,-1), rcpFrame).xyz;\n    float3 rgbNE = FxaaTexOff(tex, pos.xy, FxaaInt2( 1,-1), rcpFrame).xyz;\n    float3 rgbSW = FxaaTexOff(tex, pos.xy, FxaaInt2(-1, 1), rcpFrame).xyz;\n    float3 rgbSE = FxaaTexOff(tex, pos.xy, FxaaInt2( 1, 1), rcpFrame).xyz;\n    #if (FXAA_SUBPIX_FASTER == 0) && (FXAA_SUBPIX > 0)\n        rgbL += (rgbNW + rgbNE + rgbSW + rgbSE);\n        rgbL *= FxaaToFloat3(1.0/9.0);\n    #endif\n    float lumaNW = FxaaLuma(rgbNW);\n    float lumaNE = FxaaLuma(rgbNE);\n    float lumaSW = FxaaLuma(rgbSW);\n    float lumaSE = FxaaLuma(rgbSE);\n    float edgeVert = \n        abs((0.25 * lumaNW) + (-0.5 * lumaN) + (0.25 * lumaNE)) +\n        abs((0.50 * lumaW ) + (-1.0 * lumaM) + (0.50 * lumaE )) +\n        abs((0.25 * lumaSW) + (-0.5 * lumaS) + (0.25 * lumaSE));\n    float edgeHorz = \n        abs((0.25 * lumaNW) + (-0.5 * lumaW) + (0.25 * lumaSW)) +\n        abs((0.50 * lumaN ) + (-1.0 * lumaM) + (0.50 * lumaS )) +\n        abs((0.25 * lumaNE) + (-0.5 * lumaE) + (0.25 * lumaSE));\n    bool horzSpan = edgeHorz >= edgeVert;\n    #if FXAA_DEBUG_HORZVERT\n        if(horzSpan) return FxaaFilterReturn(FxaaFloat3(1.0, 0.75, 0.0));\n        else         return FxaaFilterReturn(FxaaFloat3(0.0, 0.50, 1.0));\n    #endif\n    float lengthSign = horzSpan ? -rcpFrame.y : -rcpFrame.x;\n    if(!horzSpan) lumaN = lumaW;\n    if(!horzSpan) lumaS = lumaE;\n    float gradientN = abs(lumaN - lumaM);\n    float gradientS = abs(lumaS - lumaM);\n    lumaN = (lumaN + lumaM) * 0.5;\n    lumaS = (lumaS + lumaM) * 0.5;\n    \n/*----------------------------------------------------------------------------\n                CHOOSE SIDE OF PIXEL WHERE GRADIENT IS HIGHEST\n------------------------------------------------------------------------------\nThis chooses a pixel pair. \nFor \"horzSpan == true\" this will be a vertical pair,\n \n    [N]     N\n    [M] or [M]\n     S     [S]\n \nNote following this block, both {N,M} and {S,M} cases\nflow in parallel (reusing the {N,M} variables).\n \nThis pair of image rows or columns is searched below\nin the positive and negative direction \nuntil edge status changes \n(or the maximum number of search steps is reached).\n----------------------------------------------------------------------------*/    \n    bool pairN = gradientN >= gradientS;\n    #if FXAA_DEBUG_PAIR\n        if(pairN) return FxaaFilterReturn(FxaaFloat3(0.0, 0.0, 1.0));\n        else      return FxaaFilterReturn(FxaaFloat3(0.0, 1.0, 0.0));\n    #endif\n    if(!pairN) lumaN = lumaS;\n    if(!pairN) gradientN = gradientS;\n    if(!pairN) lengthSign *= -1.0;\n    float2 posN;\n    posN.x = pos.x + (horzSpan ? 0.0 : lengthSign * 0.5);\n    posN.y = pos.y + (horzSpan ? lengthSign * 0.5 : 0.0);\n    \n/*----------------------------------------------------------------------------\n                         CHOOSE SEARCH LIMITING VALUES\n------------------------------------------------------------------------------\nSearch limit (+/- gradientN) is a function of local gradient.\n----------------------------------------------------------------------------*/\n    gradientN *= FXAA_SEARCH_THRESHOLD;\n    \n/*----------------------------------------------------------------------------\n    SEARCH IN BOTH DIRECTIONS UNTIL FIND LUMA PAIR AVERAGE IS OUT OF RANGE\n------------------------------------------------------------------------------\nThis loop searches either in vertical or horizontal directions,\nand in both the negative and positive direction in parallel.\nThis loop fusion is faster than searching separately.\n \nThe search is accelerated using FXAA_SEARCH_ACCELERATION length box filter\nvia anisotropic filtering with specified texture gradients.\n----------------------------------------------------------------------------*/\n    float2 posP = posN;\n    float2 offNP = horzSpan ? \n        FxaaFloat2(rcpFrame.x, 0.0) :\n        FxaaFloat2(0.0f, rcpFrame.y); \n    float lumaEndN = lumaN;\n    float lumaEndP = lumaN;\n    bool doneN = false;\n    bool doneP = false;\n    #if FXAA_SEARCH_ACCELERATION == 1\n        posN += offNP * FxaaFloat2(-1.0, -1.0);\n        posP += offNP * FxaaFloat2( 1.0,  1.0);\n    #endif\n    #if FXAA_SEARCH_ACCELERATION == 2\n        posN += offNP * FxaaFloat2(-1.5, -1.5);\n        posP += offNP * FxaaFloat2( 1.5,  1.5);\n        offNP *= FxaaFloat2(2.0, 2.0);\n    #endif\n    #if FXAA_SEARCH_ACCELERATION == 3\n        posN += offNP * FxaaFloat2(-2.0, -2.0);\n        posP += offNP * FxaaFloat2( 2.0,  2.0);\n        offNP *= FxaaFloat2(3.0, 3.0);\n    #endif\n    #if FXAA_SEARCH_ACCELERATION == 4\n        posN += offNP * FxaaFloat2(-2.5, -2.5);\n        posP += offNP * FxaaFloat2( 2.5,  2.5);\n        offNP *= FxaaFloat2(4.0, 4.0);\n    #endif\n    for(int i = 0; i < FXAA_SEARCH_STEPS; i++) {\n        #if FXAA_SEARCH_ACCELERATION == 1\n            if(!doneN) lumaEndN = \n                FxaaLuma(FxaaTexLod0(tex, posN.xy).xyz);\n            if(!doneP) lumaEndP = \n                FxaaLuma(FxaaTexLod0(tex, posP.xy).xyz);\n        #else\n            if(!doneN) lumaEndN = \n                FxaaLuma(FxaaTexGrad(tex, posN.xy, offNP).xyz);\n            if(!doneP) lumaEndP = \n                FxaaLuma(FxaaTexGrad(tex, posP.xy, offNP).xyz);\n        #endif\n        doneN = doneN || (abs(lumaEndN - lumaN) >= gradientN);\n        doneP = doneP || (abs(lumaEndP - lumaN) >= gradientN);\n        if(doneN && doneP) break;\n        if(!doneN) posN -= offNP;\n        if(!doneP) posP += offNP; }\n    \n/*----------------------------------------------------------------------------\n               HANDLE IF CENTER IS ON POSITIVE OR NEGATIVE SIDE \n------------------------------------------------------------------------------\nFXAA uses the pixel's position in the span \nin combination with the values (lumaEnd*) at the ends of the span,\nto determine filtering.\n \nThis step computes which side of the span the pixel is on. \nOn negative side if dstN < dstP,\n \n     posN        pos                      posP\n      |-----------|------|------------------|\n      |           |      |                  | \n      |<--dstN--->|<---------dstP---------->|\n                         |\n                    span center\n                    \n----------------------------------------------------------------------------*/\n    float dstN = horzSpan ? pos.x - posN.x : pos.y - posN.y;\n    float dstP = horzSpan ? posP.x - pos.x : posP.y - pos.y;\n    bool directionN = dstN < dstP;\n    #if FXAA_DEBUG_NEGPOS\n        if(directionN) return FxaaFilterReturn(FxaaFloat3(1.0, 0.0, 0.0));\n        else           return FxaaFilterReturn(FxaaFloat3(0.0, 0.0, 1.0));\n    #endif\n    lumaEndN = directionN ? lumaEndN : lumaEndP;\n    \n/*----------------------------------------------------------------------------\n         CHECK IF PIXEL IS IN SECTION OF SPAN WHICH GETS NO FILTERING\n------------------------------------------------------------------------------\nIf both the pair luma at the end of the span (lumaEndN) \nand middle pixel luma (lumaM)\nare on the same side of the middle pair average luma (lumaN),\nthen don't filter.\n \nCases,\n \n(1.) \"L\",\n  \n               lumaM\n                 |\n                 V    XXXXXXXX <- other line averaged\n         XXXXXXX[X]XXXXXXXXXXX <- source pixel line\n        |      .      | \n    --------------------------                    \n       [ ]xxxxxx[x]xx[X]XXXXXX <- pair average\n    --------------------------           \n        ^      ^ ^    ^\n        |      | |    |\n        .      |<---->|<---------- no filter region\n        .      | |    |\n        . center |    |\n        .        |  lumaEndN \n        .        |    .\n        .      lumaN  .\n        .             .\n        |<--- span -->|\n        \n                        \n(2.) \"^\" and \"-\",\n  \n                               <- other line averaged\n          XXXXX[X]XXX          <- source pixel line\n         |     |     | \n    --------------------------                    \n        [ ]xxxx[x]xx[ ]        <- pair average\n    --------------------------           \n         |     |     |\n         |<--->|<--->|<---------- filter both sides\n \n \n(3.) \"v\" and inverse of \"-\",\n  \n    XXXXXX           XXXXXXXXX <- other line averaged\n    XXXXXXXXXXX[X]XXXXXXXXXXXX <- source pixel line\n         |     |     |\n    --------------------------                    \n    XXXX[X]xxxx[x]xx[X]XXXXXXX <- pair average\n    --------------------------           \n         |     |     |\n         |<--->|<--->|<---------- don't filter both!\n \n         \nNote the \"v\" case for FXAA requires no filtering.\nThis is because the inverse of the \"-\" case is the \"v\".\nFiltering \"v\" case turns open spans like this,\n \n    XXXXXXXXX\n    \nInto this (which is not desired),\n \n    x+.   .+x\n    XXXXXXXXX\n \n----------------------------------------------------------------------------*/\n    if(((lumaM - lumaN) < 0.0) == ((lumaEndN - lumaN) < 0.0)) \n        lengthSign = 0.0;\n \n/*----------------------------------------------------------------------------\n                COMPUTE SUB-PIXEL OFFSET AND FILTER SPAN\n------------------------------------------------------------------------------\nFXAA filters using a bilinear texture fetch offset \nfrom the middle pixel M towards the center of the pair (NM below).\nMaximum filtering will be half way between pair.\nReminder, at this point in the code, \nthe {N,M} pair is also reused for all cases: {S,M}, {W,M}, and {E,M}.\n \n    +-------+\n    |       |    0.5 offset\n    |   N   |     |\n    |       |     V\n    +-------+....---\n    |       |\n    |   M...|....---\n    |       |     ^\n    +-------+     |\n    .       .    0.0 offset\n    .   S   .\n    .       .\n    .........\n \nPosition on span is used to compute sub-pixel filter offset using simple ramp,\n \n             posN           posP\n              |\\             |<------- 0.5 pixel offset into pair pixel\n              | \\            |\n              |  \\           |\n    ---.......|...\\..........|<------- 0.25 pixel offset into pair pixel\n     ^        |   ^\\         |\n     |        |   | \\        |\n     V        |   |  \\       |\n    ---.......|===|==========|<------- 0.0 pixel offset (ie M pixel)\n     ^        .   |   ^      .\n     |        .  pos  |      .\n     |        .   .   |      .\n     |        .   . center   .\n     |        .   .          .\n     |        |<->|<---------.-------- dstN\n     |        .   .          .    \n     |        .   |<-------->|<------- dstP    \n     |        .             .\n     |        |<------------>|<------- spanLength    \n     |\n    subPixelOffset\n    \n----------------------------------------------------------------------------*/\n    float spanLength = (dstP + dstN);\n    dstN = directionN ? dstN : dstP;\n    float subPixelOffset = (0.5 + (dstN * (-1.0/spanLength))) * lengthSign;\n    #if FXAA_DEBUG_OFFSET\n        float ox = horzSpan ? 0.0 : subPixelOffset*2.0/rcpFrame.x;\n        float oy = horzSpan ? subPixelOffset*2.0/rcpFrame.y : 0.0;\n        if(ox < 0.0) return FxaaFilterReturn(\n            FxaaLerp3(FxaaToFloat3(lumaO), \n                      FxaaFloat3(1.0, 0.0, 0.0), -ox));\n        if(ox > 0.0) return FxaaFilterReturn(\n            FxaaLerp3(FxaaToFloat3(lumaO), \n                      FxaaFloat3(0.0, 0.0, 1.0),  ox));\n        if(oy < 0.0) return FxaaFilterReturn(\n            FxaaLerp3(FxaaToFloat3(lumaO), \n                      FxaaFloat3(1.0, 0.6, 0.2), -oy));\n        if(oy > 0.0) return FxaaFilterReturn(\n            FxaaLerp3(FxaaToFloat3(lumaO), \n                      FxaaFloat3(0.2, 0.6, 1.0),  oy));\n        return FxaaFilterReturn(FxaaFloat3(lumaO, lumaO, lumaO));\n    #endif\n    float3 rgbF = FxaaTexLod0(tex, FxaaFloat2(\n        pos.x + (horzSpan ? 0.0 : subPixelOffset),\n        pos.y + (horzSpan ? subPixelOffset : 0.0))).xyz;\n    #if FXAA_SUBPIX == 0\n        return FxaaFilterReturn(rgbF); \n    #else        \n        return FxaaFilterReturn(FxaaLerp3(rgbL, rgbF, blendL)); \n    #endif\n}\n\n";
				default:
					return null;
			}
		}

	    private static readonly Include IncludeFx = new IncludeImplementation();

	    private class IncludeImplementation : Include {
	        public void Open(IncludeType type, string fileName, Stream parentStream, out Stream stream) {
	            stream = new MemoryStream(Encoding.UTF8.GetBytes(GetIncludedData(fileName)));
	        }

	        public void Close(Stream stream) {
                stream.Close();
                stream.Dispose();
	        }
	    }

		internal static ShaderBytecode Compile(byte[] data, string name = "") {
            try {
                return ShaderBytecode.Compile(data, "Render", "fx_5_0", ShaderFlags.None, EffectFlags.None, null, IncludeFx);
            } catch (System.Exception e) {
				System.Diagnostics.Debug.WriteLine("Shader " + (name ?? "?") + " compilation failed:\n\n" + e.Message);
                System.Windows.Forms.MessageBox.Show("Shader " + (name ?? "?") + " compilation failed:\n\n" + e.Message);
                throw;
            }
        }
		
        public static void Set(this EffectVariable variable, EffectDeferredGObject.Material o) {
            SlimDxExtension.Set(variable, o, EffectDeferredGObject.Material.Stride);
        }
        public static void Set(this EffectVariable variable, EffectDeferredGObject.AmbientShadow_VS_IN o) {
            SlimDxExtension.Set(variable, o, EffectDeferredGObject.AmbientShadow_VS_IN.Stride);
        }
        public static void Set(this EffectVariable variable, EffectDeferredGObject.AmbientShadow_PS_IN o) {
            SlimDxExtension.Set(variable, o, EffectDeferredGObject.AmbientShadow_PS_IN.Stride);
        }
        public static void Set(this EffectVariable variable, EffectDeferredGObjectSpecial.SpecialGl_PS_IN o) {
            SlimDxExtension.Set(variable, o, EffectDeferredGObjectSpecial.SpecialGl_PS_IN.Stride);
        }
        public static void Set(this EffectVariable variable, EffectKunosShader.Material o) {
            SlimDxExtension.Set(variable, o, EffectKunosShader.Material.Stride);
        }
	}
}
