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

namespace AcTools.Render.Shaders {
	internal static class ShadersResourceManager {
		internal static readonly ResourceManager Manager = new ResourceManager("AcTools.Render.Shaders", Assembly.GetExecutingAssembly());
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

		public const uint HasNormalMap = 1;
		public const uint HasDetailsMap = 2;
		public const uint HasDetailsNormalMap = 4;
		public const uint HasMaps = 8;
		public const uint UseDiffuseAlphaAsMap = 16;
		public const uint AlphaBlend = 32;
		public const uint IsAdditive = 64;
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePNTG, InputSignaturePT;
        public InputLayout LayoutPNTG, LayoutPT;

		public EffectTechnique TechStandardDeferred, TechStandardForward, TechAmbientShadowDeferred, TechTransparentDeferred, TechTransparentForward, TechTransparentMask;

		public EffectMatrixVariable FxWorld { get; private set; }
		public EffectMatrixVariable FxWorldInvTranspose { get; private set; }
		public EffectMatrixVariable FxWorldViewProj { get; private set; }
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

		public EffectMatrixVariable FxWorld { get; private set; }
		public EffectMatrixVariable FxWorldInvTranspose { get; private set; }
		public EffectMatrixVariable FxWorldViewProj { get; private set; }

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

			FxWorld = E.GetVariableByName("gWorld").AsMatrix();
			FxWorldInvTranspose = E.GetVariableByName("gWorldInvTranspose").AsMatrix();
			FxWorldViewProj = E.GetVariableByName("gWorldViewProj").AsMatrix();
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

		public EffectMatrixVariable FxWorld { get; private set; }
		public EffectMatrixVariable FxWorldInvTranspose { get; private set; }
		public EffectMatrixVariable FxWorldViewProj { get; private set; }
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

			FxWorld = E.GetVariableByName("gWorld").AsMatrix();
			FxWorldInvTranspose = E.GetVariableByName("gWorldInvTranspose").AsMatrix();
			FxWorldViewProj = E.GetVariableByName("gWorldViewProj").AsMatrix();
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
		public const int NumSplits = 4;
		public const float SmapSize = 2048.0f;
		public const float SmapDx = 1.0f / 2048.0f;
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectTechnique TechPointLight, TechPointLight_NoSpec, TechPointLight_Debug, TechDirectionalLight, TechDirectionalLight_Shadows, TechDirectionalLight_Shadows_NoFilter, TechDirectionalLight_Split;

		public EffectMatrixVariable FxWorld { get; private set; }
		public EffectMatrixVariable FxWorldInvTranspose { get; private set; }
		public EffectMatrixVariable FxWorldViewProj { get; private set; }
		public EffectMatrixVariable FxWorldViewProjInv { get; private set; }
		public EffectMatrixVariable FxShadowViewProj { get; private set; }
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

			FxWorld = E.GetVariableByName("gWorld").AsMatrix();
			FxWorldInvTranspose = E.GetVariableByName("gWorldInvTranspose").AsMatrix();
			FxWorldViewProj = E.GetVariableByName("gWorldViewProj").AsMatrix();
			FxWorldViewProjInv = E.GetVariableByName("gWorldViewProjInv").AsMatrix();
			FxShadowViewProj = E.GetVariableByName("gShadowViewProj").AsMatrix();
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
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectTechnique TechHabrahabrVersion;

		public EffectMatrixVariable FxWorldViewProjInv { get; private set; }
		public EffectMatrixVariable FxWorldViewProj { get; private set; }
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

			FxWorldViewProjInv = E.GetVariableByName("gWorldViewProjInv").AsMatrix();
			FxWorldViewProj = E.GetVariableByName("gWorldViewProj").AsMatrix();
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

		public EffectMatrixVariable FxWorldViewProjInv { get; private set; }
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

			FxWorldViewProjInv = E.GetVariableByName("gWorldViewProjInv").AsMatrix();
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

		public EffectMatrixVariable FxWorldViewProjInv { get; private set; }
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
			if (E == null) return;
			InputSignaturePT.Dispose();
            LayoutPT.Dispose();
            E.Dispose();
            _b.Dispose();
        }
	}

	public class EffectKunosShader : IEffectWrapper, IEffectMatricesWrapper {
		[StructLayout(LayoutKind.Sequential)]
        public struct Material {
            public float Ambient;
            public float Diffuse;
            public float Specular;
            public float SpecularExp;
            public Vector3 Emissive;

			public static readonly int Stride = Marshal.SizeOf(typeof(Material));
        }

		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePNT;
        public InputLayout LayoutPNT;

		public EffectTechnique TechPerPixel;

		public EffectMatrixVariable FxWorld { get; private set; }
		public EffectMatrixVariable FxWorldInvTranspose { get; private set; }
		public EffectMatrixVariable FxWorldViewProj { get; private set; }
		public EffectResourceVariable FxDiffuseMap;
		public EffectVectorVariable FxEyePosW { get; private set; }
		public EffectVariable FxMaterial;

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "KunosShader");
			E = new Effect(device, _b);

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
			if (E == null) return;
			InputSignaturePNT.Dispose();
            LayoutPNT.Dispose();
            E.Dispose();
            _b.Dispose();
        }
	}

	public class EffectPpBasic : IEffectWrapper, IEffectScreenSizeWrapper {
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectTechnique TechCopy, TechCopyNoAlpha, TechOverlay, TechShadow, TechDepth, TechFxaa;

		public EffectResourceVariable FxInputMap, FxOverlayMap, FxDepthMap;
		public EffectScalarVariable FxSizeMultipler;
		public EffectVectorVariable FxScreenSize { get; private set; }

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "PpBasic");
			E = new Effect(device, _b);

			TechCopy = E.GetTechniqueByName("Copy");
			TechCopyNoAlpha = E.GetTechniqueByName("CopyNoAlpha");
			TechOverlay = E.GetTechniqueByName("Overlay");
			TechShadow = E.GetTechniqueByName("Shadow");
			TechDepth = E.GetTechniqueByName("Depth");
			TechFxaa = E.GetTechniqueByName("Fxaa");

			for (var i = 0; i < TechCopy.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechCopy.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (PpBasic, PT, Copy) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);

			FxInputMap = E.GetVariableByName("gInputMap").AsResource();
			FxOverlayMap = E.GetVariableByName("gOverlayMap").AsResource();
			FxDepthMap = E.GetVariableByName("gDepthMap").AsResource();
			FxSizeMultipler = E.GetVariableByName("gSizeMultipler").AsScalar();
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

	public class EffectPpBlur : IEffectWrapper {
		public const int SampleCount = 15;
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectTechnique TechGaussianBlur, TechReflectionGaussianBlur;

		public EffectResourceVariable FxInputMap, FxMapsMap;
		public EffectScalarVariable FxSampleWeights, FxPower;
		public EffectVectorVariable FxSampleOffsets { get; private set; }

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "PpBlur");
			E = new Effect(device, _b);

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
			if (E == null) return;
			InputSignaturePT.Dispose();
            LayoutPT.Dispose();
            E.Dispose();
            _b.Dispose();
        }
	}

	public class EffectPpFxaa311 : IEffectWrapper, IEffectScreenSizeWrapper {
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectTechnique TechLuma, TechFxaa;

		public EffectResourceVariable FxInputMap, FxDepthMap;
		public EffectScalarVariable FxSizeMultipler;
		public EffectVectorVariable FxScreenSize { get; private set; }

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "PpFxaa311");
			E = new Effect(device, _b);

			TechLuma = E.GetTechniqueByName("Luma");
			TechFxaa = E.GetTechniqueByName("Fxaa");

			for (var i = 0; i < TechLuma.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechLuma.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (PpFxaa311, PT, Luma) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);

			FxInputMap = E.GetVariableByName("gInputMap").AsResource();
			FxDepthMap = E.GetVariableByName("gDepthMap").AsResource();
			FxSizeMultipler = E.GetVariableByName("gSizeMultipler").AsScalar();
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

	public class EffectPpHdr : IEffectWrapper {
		public static readonly Vector3 LumConvert = new Vector3(0.299f, 0.587f, 0.114f);
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectTechnique TechDownsampling, TechAdaptation, TechTonemap, TechCopy, TechCombine, TechBloom;

		public EffectResourceVariable FxInputMap, FxBrightnessMap, FxBloomMap;
		public EffectVectorVariable FxPixel { get; private set; }
		public EffectVectorVariable FxCropImage { get; private set; }

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "PpHdr");
			E = new Effect(device, _b);

			TechDownsampling = E.GetTechniqueByName("Downsampling");
			TechAdaptation = E.GetTechniqueByName("Adaptation");
			TechTonemap = E.GetTechniqueByName("Tonemap");
			TechCopy = E.GetTechniqueByName("Copy");
			TechCombine = E.GetTechniqueByName("Combine");
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
			if (E == null) return;
			InputSignaturePT.Dispose();
            LayoutPT.Dispose();
            E.Dispose();
            _b.Dispose();
        }
	}

	public class EffectPpOutline : IEffectWrapper, IEffectScreenSizeWrapper {
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectTechnique TechOutline;

		public EffectResourceVariable FxInputMap, FxDepthMap;
		public EffectVectorVariable FxScreenSize { get; private set; }

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "PpOutline");
			E = new Effect(device, _b);

			TechOutline = E.GetTechniqueByName("Outline");

			for (var i = 0; i < TechOutline.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechOutline.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (PpOutline, PT, Outline) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);

			FxInputMap = E.GetVariableByName("gInputMap").AsResource();
			FxDepthMap = E.GetVariableByName("gDepthMap").AsResource();
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

	public class EffectPpSmaa : IEffectWrapper {
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectTechnique TechSmaa, TechSmaaB, TechSmaaN;

		public EffectResourceVariable FxInputMap, FxEdgesMap, FxBlendMap, FxAreaTexMap, FxSearchTexMap;
		public EffectScalarVariable FxSizeMultipler;
		public EffectVectorVariable FxScreenSizeSpec { get; private set; }

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "PpSmaa");
			E = new Effect(device, _b);

			TechSmaa = E.GetTechniqueByName("Smaa");
			TechSmaaB = E.GetTechniqueByName("SmaaB");
			TechSmaaN = E.GetTechniqueByName("SmaaN");

			for (var i = 0; i < TechSmaa.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechSmaa.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (PpSmaa, PT, Smaa) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);

			FxInputMap = E.GetVariableByName("gInputMap").AsResource();
			FxEdgesMap = E.GetVariableByName("gEdgesMap").AsResource();
			FxBlendMap = E.GetVariableByName("gBlendMap").AsResource();
			FxAreaTexMap = E.GetVariableByName("gAreaTexMap").AsResource();
			FxSearchTexMap = E.GetVariableByName("gSearchTexMap").AsResource();
			FxSizeMultipler = E.GetVariableByName("gSizeMultipler").AsScalar();
			FxScreenSizeSpec = E.GetVariableByName("gScreenSizeSpec").AsVector();
		}

        public void Dispose() {
			if (E == null) return;
			InputSignaturePT.Dispose();
            LayoutPT.Dispose();
            E.Dispose();
            _b.Dispose();
        }
	}

	public class EffectSimpleMaterial : IEffectWrapper, IEffectMatricesWrapper {
		[StructLayout(LayoutKind.Sequential)]
        public struct StandartMaterial {
            public float Ambient;
            public float Diffuse;
            public float Specular;
            public float SpecularExp;
            public Vector3 Emissive;
            public uint Flags;
            public Vector3 _padding;

			public static readonly int Stride = Marshal.SizeOf(typeof(StandartMaterial));
        }

		[StructLayout(LayoutKind.Sequential)]
        public struct ReflectiveMaterial {
            public float FresnelC;
            public float FresnelExp;
            public float FresnelMaxLevel;

			public static readonly int Stride = Marshal.SizeOf(typeof(ReflectiveMaterial));
        }

		[StructLayout(LayoutKind.Sequential)]
        public struct MapsMaterial {
            public float DetailsUvMultipler;
            public float DetailsNormalBlend;

			public static readonly int Stride = Marshal.SizeOf(typeof(MapsMaterial));
        }

		[StructLayout(LayoutKind.Sequential)]
        public struct AlphaMaterial {
            public float Alpha;

			public static readonly int Stride = Marshal.SizeOf(typeof(AlphaMaterial));
        }

		[StructLayout(LayoutKind.Sequential)]
        public struct NmUvMultMaterial {
            public float DiffuseMultipler;
            public float NormalMultipler;

			public static readonly int Stride = Marshal.SizeOf(typeof(NmUvMultMaterial));
        }

		public const uint HasNormalMap = 1;
		public const uint UseDiffuseAlphaAsMap = 2;
		public const uint UseNormalAlphaAsAlpha = 64;
		public const uint AlphaTest = 128;
		public const uint IsAdditive = 16;
		public const uint HasDetailsMap = 4;
		public const uint IsCarpaint = 32;
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT, InputSignaturePNTG;
        public InputLayout LayoutPT, LayoutPNTG;

		public EffectTechnique TechStandard, TechAlpha, TechReflective, TechNm, TechNmUvMult, TechAtNm, TechMaps, TechDiffMaps, TechGl, TechAmbientShadow, TechMirror;

		public EffectMatrixVariable FxWorld { get; private set; }
		public EffectMatrixVariable FxWorldInvTranspose { get; private set; }
		public EffectMatrixVariable FxWorldViewProj { get; private set; }
		public EffectResourceVariable FxDiffuseMap, FxNormalMap, FxMapsMap, FxDetailsMap, FxDetailsNormalMap;
		public EffectVectorVariable FxEyePosW { get; private set; }
		public EffectVariable FxMaterial, FxReflectiveMaterial, FxMapsMaterial, FxAlphaMaterial, FxNmUvMultMaterial;

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "SimpleMaterial");
			E = new Effect(device, _b);

			TechStandard = E.GetTechniqueByName("Standard");
			TechAlpha = E.GetTechniqueByName("Alpha");
			TechReflective = E.GetTechniqueByName("Reflective");
			TechNm = E.GetTechniqueByName("Nm");
			TechNmUvMult = E.GetTechniqueByName("NmUvMult");
			TechAtNm = E.GetTechniqueByName("AtNm");
			TechMaps = E.GetTechniqueByName("Maps");
			TechDiffMaps = E.GetTechniqueByName("DiffMaps");
			TechGl = E.GetTechniqueByName("Gl");
			TechAmbientShadow = E.GetTechniqueByName("AmbientShadow");
			TechMirror = E.GetTechniqueByName("Mirror");

			for (var i = 0; i < TechAmbientShadow.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechAmbientShadow.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (SimpleMaterial, PT, AmbientShadow) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);
			for (var i = 0; i < TechStandard.Description.PassCount && InputSignaturePNTG == null; i++) {
				InputSignaturePNTG = TechStandard.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePNTG == null) throw new System.Exception("input signature (SimpleMaterial, PNTG, Standard) == null");
			LayoutPNTG = new InputLayout(device, InputSignaturePNTG, InputLayouts.VerticePNTG.InputElementsValue);

			FxWorld = E.GetVariableByName("gWorld").AsMatrix();
			FxWorldInvTranspose = E.GetVariableByName("gWorldInvTranspose").AsMatrix();
			FxWorldViewProj = E.GetVariableByName("gWorldViewProj").AsMatrix();
			FxDiffuseMap = E.GetVariableByName("gDiffuseMap").AsResource();
			FxNormalMap = E.GetVariableByName("gNormalMap").AsResource();
			FxMapsMap = E.GetVariableByName("gMapsMap").AsResource();
			FxDetailsMap = E.GetVariableByName("gDetailsMap").AsResource();
			FxDetailsNormalMap = E.GetVariableByName("gDetailsNormalMap").AsResource();
			FxEyePosW = E.GetVariableByName("gEyePosW").AsVector();
			FxMaterial = E.GetVariableByName("gMaterial");
			FxReflectiveMaterial = E.GetVariableByName("gReflectiveMaterial");
			FxMapsMaterial = E.GetVariableByName("gMapsMaterial");
			FxAlphaMaterial = E.GetVariableByName("gAlphaMaterial");
			FxNmUvMultMaterial = E.GetVariableByName("gNmUvMultMaterial");
		}

        public void Dispose() {
			if (E == null) return;
			InputSignaturePT.Dispose();
            LayoutPT.Dispose();
			InputSignaturePNTG.Dispose();
            LayoutPNTG.Dispose();
            E.Dispose();
            _b.Dispose();
        }
	}

	public class EffectSpecialShadow : IEffectWrapper {
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectTechnique TechHorizontalShadowBlur, TechVerticalShadowBlur, TechAmbientShadow, TechResult;

		public EffectMatrixVariable FxShadowViewProj { get; private set; }
		public EffectResourceVariable FxInputMap, FxDepthMap;
		public EffectScalarVariable FxMultipler, FxCount, FxPadding;
		public EffectVectorVariable FxSize { get; private set; }
		public EffectVectorVariable FxShadowSize { get; private set; }

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "SpecialShadow");
			E = new Effect(device, _b);

			TechHorizontalShadowBlur = E.GetTechniqueByName("HorizontalShadowBlur");
			TechVerticalShadowBlur = E.GetTechniqueByName("VerticalShadowBlur");
			TechAmbientShadow = E.GetTechniqueByName("AmbientShadow");
			TechResult = E.GetTechniqueByName("Result");

			for (var i = 0; i < TechHorizontalShadowBlur.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechHorizontalShadowBlur.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (SpecialShadow, PT, HorizontalShadowBlur) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);

			FxShadowViewProj = E.GetVariableByName("gShadowViewProj").AsMatrix();
			FxInputMap = E.GetVariableByName("gInputMap").AsResource();
			FxDepthMap = E.GetVariableByName("gDepthMap").AsResource();
			FxMultipler = E.GetVariableByName("gMultipler").AsScalar();
			FxCount = E.GetVariableByName("gCount").AsScalar();
			FxPadding = E.GetVariableByName("gPadding").AsScalar();
			FxSize = E.GetVariableByName("gSize").AsVector();
			FxShadowSize = E.GetVariableByName("gShadowSize").AsVector();
		}

        public void Dispose() {
			if (E == null) return;
			InputSignaturePT.Dispose();
            LayoutPT.Dispose();
            E.Dispose();
            _b.Dispose();
        }
	}

	public class EffectSpecialTrackMap : IEffectWrapper, IEffectScreenSizeWrapper {
		public const int Gblurradius = 3;
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePNTG, InputSignaturePT;
        public InputLayout LayoutPNTG, LayoutPT;

		public EffectTechnique TechMain, TechPp, TechFinal, TechFinalCheckers, TechPpHorizontalBlur, TechPpVerticalBlur;

		public EffectMatrixVariable FxWorldViewProj { get; private set; }
		public EffectMatrixVariable FxWorldInvTranspose { get; private set; }
		public EffectResourceVariable FxInputMap;
		public EffectVectorVariable FxScreenSize { get; private set; }

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "SpecialTrackMap");
			E = new Effect(device, _b);

			TechMain = E.GetTechniqueByName("Main");
			TechPp = E.GetTechniqueByName("Pp");
			TechFinal = E.GetTechniqueByName("Final");
			TechFinalCheckers = E.GetTechniqueByName("FinalCheckers");
			TechPpHorizontalBlur = E.GetTechniqueByName("PpHorizontalBlur");
			TechPpVerticalBlur = E.GetTechniqueByName("PpVerticalBlur");

			for (var i = 0; i < TechMain.Description.PassCount && InputSignaturePNTG == null; i++) {
				InputSignaturePNTG = TechMain.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePNTG == null) throw new System.Exception("input signature (SpecialTrackMap, PNTG, Main) == null");
			LayoutPNTG = new InputLayout(device, InputSignaturePNTG, InputLayouts.VerticePNTG.InputElementsValue);
			for (var i = 0; i < TechPp.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechPp.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (SpecialTrackMap, PT, Pp) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);

			FxWorldViewProj = E.GetVariableByName("gWorldViewProj").AsMatrix();
			FxWorldInvTranspose = E.GetVariableByName("gWorldInvTranspose").AsMatrix();
			FxInputMap = E.GetVariableByName("gInputMap").AsResource();
			FxScreenSize = E.GetVariableByName("gScreenSize").AsVector();
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

	public class EffectSpecialUv : IEffectWrapper {
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePNTG;
        public InputLayout LayoutPNTG;

		public EffectTechnique TechMain;

		public EffectVectorVariable FxOffset { get; private set; }

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "SpecialUv");
			E = new Effect(device, _b);

			TechMain = E.GetTechniqueByName("Main");

			for (var i = 0; i < TechMain.Description.PassCount && InputSignaturePNTG == null; i++) {
				InputSignaturePNTG = TechMain.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePNTG == null) throw new System.Exception("input signature (SpecialUv, PNTG, Main) == null");
			LayoutPNTG = new InputLayout(device, InputSignaturePNTG, InputLayouts.VerticePNTG.InputElementsValue);

			FxOffset = E.GetVariableByName("gOffset").AsVector();
		}

        public void Dispose() {
			if (E == null) return;
			InputSignaturePNTG.Dispose();
            LayoutPNTG.Dispose();
            E.Dispose();
            _b.Dispose();
        }
	}

	public class EffectTestingCube : IEffectWrapper {
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePC;
        public InputLayout LayoutPC;

		public EffectTechnique TechCube;

		public EffectMatrixVariable FxWorldViewProj { get; private set; }

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "TestingCube");
			E = new Effect(device, _b);

			TechCube = E.GetTechniqueByName("Cube");

			for (var i = 0; i < TechCube.Description.PassCount && InputSignaturePC == null; i++) {
				InputSignaturePC = TechCube.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePC == null) throw new System.Exception("input signature (TestingCube, PC, Cube) == null");
			LayoutPC = new InputLayout(device, InputSignaturePC, InputLayouts.VerticePC.InputElementsValue);

			FxWorldViewProj = E.GetVariableByName("gWorldViewProj").AsMatrix();
		}

        public void Dispose() {
			if (E == null) return;
			InputSignaturePC.Dispose();
            LayoutPC.Dispose();
            E.Dispose();
            _b.Dispose();
        }
	}

	public class EffectTestingPnt : IEffectWrapper {
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePNT;
        public InputLayout LayoutPNT;

		public EffectTechnique TechCube;

		public EffectMatrixVariable FxWorldViewProj { get; private set; }

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "TestingPnt");
			E = new Effect(device, _b);

			TechCube = E.GetTechniqueByName("Cube");

			for (var i = 0; i < TechCube.Description.PassCount && InputSignaturePNT == null; i++) {
				InputSignaturePNT = TechCube.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePNT == null) throw new System.Exception("input signature (TestingPnt, PNT, Cube) == null");
			LayoutPNT = new InputLayout(device, InputSignaturePNT, InputLayouts.VerticePNT.InputElementsValue);

			FxWorldViewProj = E.GetVariableByName("gWorldViewProj").AsMatrix();
		}

        public void Dispose() {
			if (E == null) return;
			InputSignaturePNT.Dispose();
            LayoutPNT.Dispose();
            E.Dispose();
            _b.Dispose();
        }
	}


	public static class EffectExtension {		
        public static void Set(this EffectVariable variable, EffectDeferredGObject.Material o) {
            SlimDxExtension.Set(variable, o, EffectDeferredGObject.Material.Stride);
        }
        public static void Set(this EffectVariable variable, EffectKunosShader.Material o) {
            SlimDxExtension.Set(variable, o, EffectKunosShader.Material.Stride);
        }
        public static void Set(this EffectVariable variable, EffectSimpleMaterial.StandartMaterial o) {
            SlimDxExtension.Set(variable, o, EffectSimpleMaterial.StandartMaterial.Stride);
        }
        public static void Set(this EffectVariable variable, EffectSimpleMaterial.ReflectiveMaterial o) {
            SlimDxExtension.Set(variable, o, EffectSimpleMaterial.ReflectiveMaterial.Stride);
        }
        public static void Set(this EffectVariable variable, EffectSimpleMaterial.MapsMaterial o) {
            SlimDxExtension.Set(variable, o, EffectSimpleMaterial.MapsMaterial.Stride);
        }
        public static void Set(this EffectVariable variable, EffectSimpleMaterial.AlphaMaterial o) {
            SlimDxExtension.Set(variable, o, EffectSimpleMaterial.AlphaMaterial.Stride);
        }
        public static void Set(this EffectVariable variable, EffectSimpleMaterial.NmUvMultMaterial o) {
            SlimDxExtension.Set(variable, o, EffectSimpleMaterial.NmUvMultMaterial.Stride);
        }
	}
}
