/* GENERATED AUTOMATICALLY */
/* DON’T MODIFY */

using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Base.Structs;
using AcTools.Render.Base.Utils;
using SlimDX;
using SlimDX.D3DCompiler;
using SlimDX.Direct3D11;
// ReSharper disable InconsistentNaming
// ReSharper disable LocalizableElement

namespace AcTools.Render.Deferred.Shaders {
	internal static class ShadersResourceManager {
		internal static readonly ResourceManager Manager = new ResourceManager("AcTools.Render.Deferred.Shaders", Assembly.GetExecutingAssembly());
	}

	public class EffectDeferredGObject : IEffectWrapper, IEffectMatricesWrapper {
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

		public static readonly uint HasNormalMap = 1;
		public static readonly uint HasDetailsMap = 2;
		public static readonly uint HasDetailsNormalMap = 4;
		public static readonly uint HasMaps = 8;
		public static readonly uint UseDiffuseAlphaAsMap = 16;
		public static readonly uint AlphaBlend = 32;
		public static readonly uint IsAdditive = 64;
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePNTG, InputSignaturePT;
        public InputLayout LayoutPNTG, LayoutPT;

		public EffectTechnique TechStandardDeferred, TechStandardForward, TechAmbientShadowDeferred, TechTransparentDeferred, TechTransparentForward, TechTransparentMask;

		public EffectOnlyMatrixVariable FxWorld { get; private set; }
		public EffectOnlyMatrixVariable FxWorldInvTranspose { get; private set; }
		public EffectOnlyMatrixVariable FxWorldViewProj { get; private set; }
		public EffectResourceVariable FxDiffuseMap, FxNormalMap, FxMapsMap, FxDetailsMap, FxDetailsNormalMap, FxReflectionCubemap;
		public EffectVectorVariable FxEyePosW { get; private set; }
		public EffectVectorVariable FxAmbientDown { get; private set; }
		public EffectVectorVariable FxAmbientRange { get; private set; }
		public EffectVectorVariable FxLightColor { get; private set; }
		public EffectVectorVariable FxDirectionalLightDirection { get; private set; }
		public EffectVariable FxMaterial;

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "DeferredGObject");
			E = new Effect(device, _b);

			TechStandardDeferred = E.GetTechniqueByName("StandardDeferred");
			TechStandardForward = E.GetTechniqueByName("StandardForward");
			TechAmbientShadowDeferred = E.GetTechniqueByName("AmbientShadowDeferred");
			TechTransparentDeferred = E.GetTechniqueByName("TransparentDeferred");
			TechTransparentForward = E.GetTechniqueByName("TransparentForward");
			TechTransparentMask = E.GetTechniqueByName("TransparentMask");

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

			FxWorld = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorld").AsMatrix());
			FxWorldInvTranspose = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldInvTranspose").AsMatrix());
			FxWorldViewProj = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldViewProj").AsMatrix());
			FxDiffuseMap = E.GetVariableByName("gDiffuseMap").AsResource();
			FxNormalMap = E.GetVariableByName("gNormalMap").AsResource();
			FxMapsMap = E.GetVariableByName("gMapsMap").AsResource();
			FxDetailsMap = E.GetVariableByName("gDetailsMap").AsResource();
			FxDetailsNormalMap = E.GetVariableByName("gDetailsNormalMap").AsResource();
			FxReflectionCubemap = E.GetVariableByName("gReflectionCubemap").AsResource();
			FxEyePosW = E.GetVariableByName("gEyePosW").AsVector();
			FxAmbientDown = E.GetVariableByName("gAmbientDown").AsVector();
			FxAmbientRange = E.GetVariableByName("gAmbientRange").AsVector();
			FxLightColor = E.GetVariableByName("gLightColor").AsVector();
			FxDirectionalLightDirection = E.GetVariableByName("gDirectionalLightDirection").AsVector();
			FxMaterial = E.GetVariableByName("gMaterial");
		}

        public void Dispose() {
			if (E == null) return;
			InputSignaturePNTG.Dispose();
            LayoutPNTG.Dispose();
			InputSignaturePT.Dispose();
            LayoutPT.Dispose();
            E.Dispose();
            _b.Dispose();
        }
	}

	public class EffectDeferredGObjectSpecial : IEffectWrapper, IEffectMatricesWrapper {
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePNTG;
        public InputLayout LayoutPNTG;

		public EffectTechnique TechSpecialGlDeferred, TechSpecialGlForward, TechSpecialGlMask;

		public EffectOnlyMatrixVariable FxWorld { get; private set; }
		public EffectOnlyMatrixVariable FxWorldInvTranspose { get; private set; }
		public EffectOnlyMatrixVariable FxWorldViewProj { get; private set; }

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "DeferredGObjectSpecial");
			E = new Effect(device, _b);

			TechSpecialGlDeferred = E.GetTechniqueByName("SpecialGlDeferred");
			TechSpecialGlForward = E.GetTechniqueByName("SpecialGlForward");
			TechSpecialGlMask = E.GetTechniqueByName("SpecialGlMask");

			for (var i = 0; i < TechSpecialGlDeferred.Description.PassCount && InputSignaturePNTG == null; i++) {
				InputSignaturePNTG = TechSpecialGlDeferred.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePNTG == null) throw new System.Exception("input signature (DeferredGObjectSpecial, PNTG, SpecialGlDeferred) == null");
			LayoutPNTG = new InputLayout(device, InputSignaturePNTG, InputLayouts.VerticePNTG.InputElementsValue);

			FxWorld = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorld").AsMatrix());
			FxWorldInvTranspose = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldInvTranspose").AsMatrix());
			FxWorldViewProj = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldViewProj").AsMatrix());
		}

        public void Dispose() {
			if (E == null) return;
			InputSignaturePNTG.Dispose();
            LayoutPNTG.Dispose();
            E.Dispose();
            _b.Dispose();
        }
	}

	public class EffectDeferredGSky : IEffectWrapper, IEffectMatricesWrapper {
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignatureP;
        public InputLayout LayoutP;

		public EffectTechnique TechSkyDeferred, TechSkyForward;

		public EffectOnlyMatrixVariable FxWorld { get; private set; }
		public EffectOnlyMatrixVariable FxWorldInvTranspose { get; private set; }
		public EffectOnlyMatrixVariable FxWorldViewProj { get; private set; }
		public EffectVectorVariable FxSkyDown { get; private set; }
		public EffectVectorVariable FxSkyRange { get; private set; }

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "DeferredGSky");
			E = new Effect(device, _b);

			TechSkyDeferred = E.GetTechniqueByName("SkyDeferred");
			TechSkyForward = E.GetTechniqueByName("SkyForward");

			for (var i = 0; i < TechSkyDeferred.Description.PassCount && InputSignatureP == null; i++) {
				InputSignatureP = TechSkyDeferred.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignatureP == null) throw new System.Exception("input signature (DeferredGSky, P, SkyDeferred) == null");
			LayoutP = new InputLayout(device, InputSignatureP, InputLayouts.VerticeP.InputElementsValue);

			FxWorld = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorld").AsMatrix());
			FxWorldInvTranspose = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldInvTranspose").AsMatrix());
			FxWorldViewProj = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldViewProj").AsMatrix());
			FxSkyDown = E.GetVariableByName("gSkyDown").AsVector();
			FxSkyRange = E.GetVariableByName("gSkyRange").AsVector();
		}

        public void Dispose() {
			if (E == null) return;
			InputSignatureP.Dispose();
            LayoutP.Dispose();
            E.Dispose();
            _b.Dispose();
        }
	}

	public class EffectDeferredLight : IEffectWrapper, IEffectMatricesWrapper, IEffectScreenSizeWrapper {
		public static readonly int NumSplits = 4;
		public static readonly float SmapSize = 2048.0f;
		public static readonly float SmapDx = 1.0f / 2048.0f;
		public static readonly float ShadowA = 0.0001f;
		public static readonly float ShadowZ = 0.9999f;
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectTechnique TechPointLight, TechPointLight_NoSpec, TechPointLight_Debug, TechDirectionalLight, TechDirectionalLight_Shadows, TechDirectionalLight_Shadows_NoFilter, TechDirectionalLight_Split;

		public EffectOnlyMatrixVariable FxWorld { get; private set; }
		public EffectOnlyMatrixVariable FxWorldInvTranspose { get; private set; }
		public EffectOnlyMatrixVariable FxWorldViewProj { get; private set; }
		public EffectOnlyMatrixVariable FxWorldViewProjInv { get; private set; }
		public EffectOnlyMatrixArrayVariable FxShadowViewProj { get; private set; }
		public EffectResourceVariable FxBaseMap, FxNormalMap, FxMapsMap, FxDepthMap, FxShadowMaps;
		public EffectScalarVariable FxPointLightRadius;
		public EffectVectorVariable FxScreenSize { get; private set; }
		public EffectVectorVariable FxLightColor { get; private set; }
		public EffectVectorVariable FxDirectionalLightDirection { get; private set; }
		public EffectVectorVariable FxPointLightPosition { get; private set; }
		public EffectVectorVariable FxShadowDepths { get; private set; }
		public EffectVectorVariable FxEyePosW { get; private set; }

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "DeferredLight");
			E = new Effect(device, _b);

			TechPointLight = E.GetTechniqueByName("PointLight");
			TechPointLight_NoSpec = E.GetTechniqueByName("PointLight_NoSpec");
			TechPointLight_Debug = E.GetTechniqueByName("PointLight_Debug");
			TechDirectionalLight = E.GetTechniqueByName("DirectionalLight");
			TechDirectionalLight_Shadows = E.GetTechniqueByName("DirectionalLight_Shadows");
			TechDirectionalLight_Shadows_NoFilter = E.GetTechniqueByName("DirectionalLight_Shadows_NoFilter");
			TechDirectionalLight_Split = E.GetTechniqueByName("DirectionalLight_Split");

			for (var i = 0; i < TechDirectionalLight.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechDirectionalLight.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (DeferredLight, PT, DirectionalLight) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);

			FxWorld = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorld").AsMatrix());
			FxWorldInvTranspose = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldInvTranspose").AsMatrix());
			FxWorldViewProj = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldViewProj").AsMatrix());
			FxWorldViewProjInv = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldViewProjInv").AsMatrix());
			FxShadowViewProj = new EffectOnlyMatrixArrayVariable(E.GetVariableByName("gShadowViewProj").AsMatrix());
			FxBaseMap = E.GetVariableByName("gBaseMap").AsResource();
			FxNormalMap = E.GetVariableByName("gNormalMap").AsResource();
			FxMapsMap = E.GetVariableByName("gMapsMap").AsResource();
			FxDepthMap = E.GetVariableByName("gDepthMap").AsResource();
			FxShadowMaps = E.GetVariableByName("gShadowMaps").AsResource();
			FxPointLightRadius = E.GetVariableByName("gPointLightRadius").AsScalar();
			FxScreenSize = E.GetVariableByName("gScreenSize").AsVector();
			FxLightColor = E.GetVariableByName("gLightColor").AsVector();
			FxDirectionalLightDirection = E.GetVariableByName("gDirectionalLightDirection").AsVector();
			FxPointLightPosition = E.GetVariableByName("gPointLightPosition").AsVector();
			FxShadowDepths = E.GetVariableByName("gShadowDepths").AsVector();
			FxEyePosW = E.GetVariableByName("gEyePosW").AsVector();
		}

        public void Dispose() {
			if (E == null) return;
			InputSignaturePT.Dispose();
            LayoutPT.Dispose();
            E.Dispose();
            _b.Dispose();
        }
	}

	public class EffectDeferredPpSslr : IEffectWrapper {
		public static readonly float MaxL = 0.72f;
		public static readonly float FadingFrom = 0.5f;
		public static readonly float MinL = 0.0f;
		public static readonly int Iterations = 20;
		public static readonly float StartL = 0.01f;
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectTechnique TechHabrahabrVersion;

		public EffectOnlyMatrixVariable FxWorldViewProjInv { get; private set; }
		public EffectOnlyMatrixVariable FxWorldViewProj { get; private set; }
		public EffectResourceVariable FxBaseMap, FxLightMap, FxNormalMap, FxMapsMap, FxDepthMap;
		public EffectVectorVariable FxEyePosW { get; private set; }

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "DeferredPpSslr");
			E = new Effect(device, _b);

			TechHabrahabrVersion = E.GetTechniqueByName("HabrahabrVersion");

			for (var i = 0; i < TechHabrahabrVersion.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechHabrahabrVersion.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (DeferredPpSslr, PT, HabrahabrVersion) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);

			FxWorldViewProjInv = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldViewProjInv").AsMatrix());
			FxWorldViewProj = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldViewProj").AsMatrix());
			FxBaseMap = E.GetVariableByName("gBaseMap").AsResource();
			FxLightMap = E.GetVariableByName("gLightMap").AsResource();
			FxNormalMap = E.GetVariableByName("gNormalMap").AsResource();
			FxMapsMap = E.GetVariableByName("gMapsMap").AsResource();
			FxDepthMap = E.GetVariableByName("gDepthMap").AsResource();
			FxEyePosW = E.GetVariableByName("gEyePosW").AsVector();
		}

        public void Dispose() {
			if (E == null) return;
			InputSignaturePT.Dispose();
            LayoutPT.Dispose();
            E.Dispose();
            _b.Dispose();
        }
	}

	public class EffectDeferredResult : IEffectWrapper, IEffectScreenSizeWrapper {
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectTechnique TechDebug, TechDebugPost, TechDebugLighting, TechDebugLocalReflections, TechCombine0;

		public EffectOnlyMatrixVariable FxWorldViewProjInv { get; private set; }
		public EffectResourceVariable FxBaseMap, FxNormalMap, FxMapsMap, FxDepthMap, FxLightMap, FxLocalReflectionMap, FxBottomLayerMap, FxReflectionCubemap;
		public EffectVectorVariable FxEyePosW { get; private set; }
		public EffectVectorVariable FxScreenSize { get; private set; }

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "DeferredResult");
			E = new Effect(device, _b);

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

			FxWorldViewProjInv = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldViewProjInv").AsMatrix());
			FxBaseMap = E.GetVariableByName("gBaseMap").AsResource();
			FxNormalMap = E.GetVariableByName("gNormalMap").AsResource();
			FxMapsMap = E.GetVariableByName("gMapsMap").AsResource();
			FxDepthMap = E.GetVariableByName("gDepthMap").AsResource();
			FxLightMap = E.GetVariableByName("gLightMap").AsResource();
			FxLocalReflectionMap = E.GetVariableByName("gLocalReflectionMap").AsResource();
			FxBottomLayerMap = E.GetVariableByName("gBottomLayerMap").AsResource();
			FxReflectionCubemap = E.GetVariableByName("gReflectionCubemap").AsResource();
			FxEyePosW = E.GetVariableByName("gEyePosW").AsVector();
			FxScreenSize = E.GetVariableByName("gScreenSize").AsVector();
		}

        public void Dispose() {
			if (E == null) return;
			InputSignaturePT.Dispose();
            LayoutPT.Dispose();
            E.Dispose();
            _b.Dispose();
        }
	}

	public class EffectDeferredTransparent : IEffectWrapper {
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectTechnique TechDebug, TechDebugPost, TechDebugLighting, TechDebugLocalReflections, TechCombine0;

		public EffectOnlyMatrixVariable FxWorldViewProjInv { get; private set; }
		public EffectResourceVariable FxBaseMap, FxNormalMap, FxMapsMap, FxDepthMap, FxLightMap, FxLocalReflectionMap, FxReflectionCubemap;
		public EffectVectorVariable FxAmbientDown { get; private set; }
		public EffectVectorVariable FxAmbientRange { get; private set; }
		public EffectVectorVariable FxEyePosW { get; private set; }

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "DeferredTransparent");
			E = new Effect(device, _b);

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

			FxWorldViewProjInv = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldViewProjInv").AsMatrix());
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
			if (E == null) return;
			InputSignaturePT.Dispose();
            LayoutPT.Dispose();
            E.Dispose();
            _b.Dispose();
        }
	}


	public static class EffectExtension {		
        public static void Set(this EffectVariable variable, EffectDeferredGObject.Material o) {
            SlimDxExtension.Set(variable, o, EffectDeferredGObject.Material.Stride);
        }
	}
}
