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

	public class EffectDarkMaterial : IEffectWrapper, IEffectMatricesWrapper, IEffectScreenSizeWrapper {
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
            public float SunSpecular;
            public float SunSpecularExp;

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

		public static readonly uint HasNormalMap = 1;
		public static readonly uint UseNormalAlphaAsAlpha = 64;
		public static readonly uint AlphaTest = 128;
		public static readonly uint IsAdditive = 16;
		public static readonly uint HasDetailsMap = 4;
		public static readonly uint IsCarpaint = 32;
		public static readonly int NumSplits = 1;
		public static readonly int ShadowMapSize = 2048;
		public static readonly bool EnableShadows = true;
		public static readonly bool EnablePcss = true;
		public static readonly int MaxBones = 64;
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT, InputSignaturePNTG, InputSignaturePNTGW4B;
        public InputLayout LayoutPT, LayoutPNTG, LayoutPNTGW4B;

		public EffectTechnique TechGPass_Standard, TechGPass_Alpha, TechGPass_Reflective, TechGPass_Nm, TechGPass_NmUvMult, TechGPass_AtNm, TechGPass_Maps, TechGPass_SkinnedMaps, TechGPass_DiffMaps, TechGPass_Gl, TechGPass_SkinnedGl, TechGPass_FlatMirror, TechStandard, TechAlpha, TechReflective, TechNm, TechNmUvMult, TechAtNm, TechMaps, TechSkinnedMaps, TechDiffMaps, TechGl, TechSkinnedGl, TechCollider, TechDebug, TechSkinnedDebug, TechDepthOnly, TechSkinnedDepthOnly, TechAmbientShadow, TechMirror, TechFlatMirror, TechFlatTextureMirror, TechFlatTextureMirrorDark, TechFlatBackgroundGround, TechFlatAmbientGround;

		public EffectMatrixVariable FxShadowViewProj { get; private set; }
		public EffectMatrixVariable FxWorld { get; private set; }
		public EffectMatrixVariable FxWorldInvTranspose { get; private set; }
		public EffectMatrixVariable FxWorldViewProj { get; private set; }
		public EffectMatrixVariable FxBoneTransforms { get; private set; }
		public EffectResourceVariable FxShadowMaps, FxDiffuseMap, FxNormalMap, FxMapsMap, FxDetailsMap, FxDetailsNormalMap, FxReflectionCubemap;
		public EffectScalarVariable FxGPassTransparent, FxFlatMirrored, FxReflectionPower, FxShadowsEnabled, FxPcssEnabled, FxFlatMirrorPower;
		public EffectVectorVariable FxEyePosW { get; private set; }
		public EffectVectorVariable FxLightDir { get; private set; }
		public EffectVectorVariable FxLightColor { get; private set; }
		public EffectVectorVariable FxAmbientDown { get; private set; }
		public EffectVectorVariable FxAmbientRange { get; private set; }
		public EffectVectorVariable FxBackgroundColor { get; private set; }
		public EffectVectorVariable FxScreenSize { get; private set; }
		public EffectVariable FxMaterial, FxReflectiveMaterial, FxMapsMaterial, FxAlphaMaterial, FxNmUvMultMaterial;

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "DarkMaterial");
			E = new Effect(device, _b);

			TechGPass_Standard = E.GetTechniqueByName("GPass_Standard");
			TechGPass_Alpha = E.GetTechniqueByName("GPass_Alpha");
			TechGPass_Reflective = E.GetTechniqueByName("GPass_Reflective");
			TechGPass_Nm = E.GetTechniqueByName("GPass_Nm");
			TechGPass_NmUvMult = E.GetTechniqueByName("GPass_NmUvMult");
			TechGPass_AtNm = E.GetTechniqueByName("GPass_AtNm");
			TechGPass_Maps = E.GetTechniqueByName("GPass_Maps");
			TechGPass_SkinnedMaps = E.GetTechniqueByName("GPass_SkinnedMaps");
			TechGPass_DiffMaps = E.GetTechniqueByName("GPass_DiffMaps");
			TechGPass_Gl = E.GetTechniqueByName("GPass_Gl");
			TechGPass_SkinnedGl = E.GetTechniqueByName("GPass_SkinnedGl");
			TechGPass_FlatMirror = E.GetTechniqueByName("GPass_FlatMirror");
			TechStandard = E.GetTechniqueByName("Standard");
			TechAlpha = E.GetTechniqueByName("Alpha");
			TechReflective = E.GetTechniqueByName("Reflective");
			TechNm = E.GetTechniqueByName("Nm");
			TechNmUvMult = E.GetTechniqueByName("NmUvMult");
			TechAtNm = E.GetTechniqueByName("AtNm");
			TechMaps = E.GetTechniqueByName("Maps");
			TechSkinnedMaps = E.GetTechniqueByName("SkinnedMaps");
			TechDiffMaps = E.GetTechniqueByName("DiffMaps");
			TechGl = E.GetTechniqueByName("Gl");
			TechSkinnedGl = E.GetTechniqueByName("SkinnedGl");
			TechCollider = E.GetTechniqueByName("Collider");
			TechDebug = E.GetTechniqueByName("Debug");
			TechSkinnedDebug = E.GetTechniqueByName("SkinnedDebug");
			TechDepthOnly = E.GetTechniqueByName("DepthOnly");
			TechSkinnedDepthOnly = E.GetTechniqueByName("SkinnedDepthOnly");
			TechAmbientShadow = E.GetTechniqueByName("AmbientShadow");
			TechMirror = E.GetTechniqueByName("Mirror");
			TechFlatMirror = E.GetTechniqueByName("FlatMirror");
			TechFlatTextureMirror = E.GetTechniqueByName("FlatTextureMirror");
			TechFlatTextureMirrorDark = E.GetTechniqueByName("FlatTextureMirrorDark");
			TechFlatBackgroundGround = E.GetTechniqueByName("FlatBackgroundGround");
			TechFlatAmbientGround = E.GetTechniqueByName("FlatAmbientGround");

			for (var i = 0; i < TechGPass_FlatMirror.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechGPass_FlatMirror.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (DarkMaterial, PT, GPass_FlatMirror) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);
			for (var i = 0; i < TechGPass_Standard.Description.PassCount && InputSignaturePNTG == null; i++) {
				InputSignaturePNTG = TechGPass_Standard.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePNTG == null) throw new System.Exception("input signature (DarkMaterial, PNTG, GPass_Standard) == null");
			LayoutPNTG = new InputLayout(device, InputSignaturePNTG, InputLayouts.VerticePNTG.InputElementsValue);
			for (var i = 0; i < TechGPass_SkinnedMaps.Description.PassCount && InputSignaturePNTGW4B == null; i++) {
				InputSignaturePNTGW4B = TechGPass_SkinnedMaps.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePNTGW4B == null) throw new System.Exception("input signature (DarkMaterial, PNTGW4B, GPass_SkinnedMaps) == null");
			LayoutPNTGW4B = new InputLayout(device, InputSignaturePNTGW4B, InputLayouts.VerticePNTGW4B.InputElementsValue);

			FxShadowViewProj = E.GetVariableByName("gShadowViewProj").AsMatrix();
			FxWorld = E.GetVariableByName("gWorld").AsMatrix();
			FxWorldInvTranspose = E.GetVariableByName("gWorldInvTranspose").AsMatrix();
			FxWorldViewProj = E.GetVariableByName("gWorldViewProj").AsMatrix();
			FxBoneTransforms = E.GetVariableByName("gBoneTransforms").AsMatrix();
			FxShadowMaps = E.GetVariableByName("gShadowMaps").AsResource();
			FxDiffuseMap = E.GetVariableByName("gDiffuseMap").AsResource();
			FxNormalMap = E.GetVariableByName("gNormalMap").AsResource();
			FxMapsMap = E.GetVariableByName("gMapsMap").AsResource();
			FxDetailsMap = E.GetVariableByName("gDetailsMap").AsResource();
			FxDetailsNormalMap = E.GetVariableByName("gDetailsNormalMap").AsResource();
			FxReflectionCubemap = E.GetVariableByName("gReflectionCubemap").AsResource();
			FxGPassTransparent = E.GetVariableByName("gGPassTransparent").AsScalar();
			FxFlatMirrored = E.GetVariableByName("gFlatMirrored").AsScalar();
			FxReflectionPower = E.GetVariableByName("gReflectionPower").AsScalar();
			FxShadowsEnabled = E.GetVariableByName("gShadowsEnabled").AsScalar();
			FxPcssEnabled = E.GetVariableByName("gPcssEnabled").AsScalar();
			FxFlatMirrorPower = E.GetVariableByName("gFlatMirrorPower").AsScalar();
			FxEyePosW = E.GetVariableByName("gEyePosW").AsVector();
			FxLightDir = E.GetVariableByName("gLightDir").AsVector();
			FxLightColor = E.GetVariableByName("gLightColor").AsVector();
			FxAmbientDown = E.GetVariableByName("gAmbientDown").AsVector();
			FxAmbientRange = E.GetVariableByName("gAmbientRange").AsVector();
			FxBackgroundColor = E.GetVariableByName("gBackgroundColor").AsVector();
			FxScreenSize = E.GetVariableByName("gScreenSize").AsVector();
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
			InputSignaturePNTGW4B.Dispose();
            LayoutPNTGW4B.Dispose();
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
		public static readonly int FxaaPreset = 5;
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectTechnique TechCopy, TechCopyHq, TechCopyNoAlpha, TechOverlay, TechShadow, TechDepth, TechFxaa;

		public EffectResourceVariable FxInputMap, FxOverlayMap, FxDepthMap;
		public EffectScalarVariable FxSizeMultipler;
		public EffectVectorVariable FxScreenSize { get; private set; }

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "PpBasic");
			E = new Effect(device, _b);

			TechCopy = E.GetTechniqueByName("Copy");
			TechCopyHq = E.GetTechniqueByName("CopyHq");
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

	public class EffectPpBlur : IEffectWrapper, IEffectScreenSizeWrapper {
		public static readonly int SampleCount = 15;
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectTechnique TechGaussianBlur, TechFlatMirrorBlur, TechDarkSslrBlur0, TechReflectionGaussianBlur;

		public EffectMatrixVariable FxWorldViewProjInv { get; private set; }
		public EffectResourceVariable FxInputMap, FxFlatMirrorDepthMap, FxFlatMirrorNormalsMap, FxMapsMap;
		public EffectScalarVariable FxSampleWeights, FxPower;
		public EffectVectorVariable FxSampleOffsets { get; private set; }
		public EffectVectorVariable FxScreenSize { get; private set; }

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "PpBlur");
			E = new Effect(device, _b);

			TechGaussianBlur = E.GetTechniqueByName("GaussianBlur");
			TechFlatMirrorBlur = E.GetTechniqueByName("FlatMirrorBlur");
			TechDarkSslrBlur0 = E.GetTechniqueByName("DarkSslrBlur0");
			TechReflectionGaussianBlur = E.GetTechniqueByName("ReflectionGaussianBlur");

			for (var i = 0; i < TechGaussianBlur.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechGaussianBlur.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (PpBlur, PT, GaussianBlur) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);

			FxWorldViewProjInv = E.GetVariableByName("gWorldViewProjInv").AsMatrix();
			FxInputMap = E.GetVariableByName("gInputMap").AsResource();
			FxFlatMirrorDepthMap = E.GetVariableByName("gFlatMirrorDepthMap").AsResource();
			FxFlatMirrorNormalsMap = E.GetVariableByName("gFlatMirrorNormalsMap").AsResource();
			FxMapsMap = E.GetVariableByName("gMapsMap").AsResource();
			FxSampleWeights = E.GetVariableByName("gSampleWeights").AsScalar();
			FxPower = E.GetVariableByName("gPower").AsScalar();
			FxSampleOffsets = E.GetVariableByName("gSampleOffsets").AsVector();
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

	public class EffectPpDarkSslr : IEffectWrapper {
		public static readonly int Iterations = 30;
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectTechnique TechSslr, TechSslr_LinearFiltering, TechFinalStep, TechFinalStep_LinearFiltering;

		public EffectMatrixVariable FxCameraProjInv { get; private set; }
		public EffectMatrixVariable FxCameraProj { get; private set; }
		public EffectMatrixVariable FxWorldViewProjInv { get; private set; }
		public EffectMatrixVariable FxWorldViewProj { get; private set; }
		public EffectResourceVariable FxDiffuseMap, FxDepthMap, FxBaseReflectionMap, FxNormalMap, FxFirstStepMap;
		public EffectScalarVariable FxStartFrom, FxFixMultipler, FxOffset, FxGlowFix, FxDistanceThreshold;
		public EffectVectorVariable FxEyePosW { get; private set; }

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "PpDarkSslr");
			E = new Effect(device, _b);

			TechSslr = E.GetTechniqueByName("Sslr");
			TechSslr_LinearFiltering = E.GetTechniqueByName("Sslr_LinearFiltering");
			TechFinalStep = E.GetTechniqueByName("FinalStep");
			TechFinalStep_LinearFiltering = E.GetTechniqueByName("FinalStep_LinearFiltering");

			for (var i = 0; i < TechSslr.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechSslr.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (PpDarkSslr, PT, Sslr) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);

			FxCameraProjInv = E.GetVariableByName("gCameraProjInv").AsMatrix();
			FxCameraProj = E.GetVariableByName("gCameraProj").AsMatrix();
			FxWorldViewProjInv = E.GetVariableByName("gWorldViewProjInv").AsMatrix();
			FxWorldViewProj = E.GetVariableByName("gWorldViewProj").AsMatrix();
			FxDiffuseMap = E.GetVariableByName("gDiffuseMap").AsResource();
			FxDepthMap = E.GetVariableByName("gDepthMap").AsResource();
			FxBaseReflectionMap = E.GetVariableByName("gBaseReflectionMap").AsResource();
			FxNormalMap = E.GetVariableByName("gNormalMap").AsResource();
			FxFirstStepMap = E.GetVariableByName("gFirstStepMap").AsResource();
			FxStartFrom = E.GetVariableByName("gStartFrom").AsScalar();
			FxFixMultipler = E.GetVariableByName("gFixMultipler").AsScalar();
			FxOffset = E.GetVariableByName("gOffset").AsScalar();
			FxGlowFix = E.GetVariableByName("gGlowFix").AsScalar();
			FxDistanceThreshold = E.GetVariableByName("gDistanceThreshold").AsScalar();
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

	public class EffectPpDownsample : IEffectWrapper, IEffectScreenSizeWrapper {
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectTechnique TechCopy, TechFound, TechAverage, TechAnisotropic, TechBicubic;

		public EffectResourceVariable FxInputMap;
		public EffectVectorVariable FxScreenSize { get; private set; }
		public EffectVectorVariable FxMultipler { get; private set; }

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "PpDownsample");
			E = new Effect(device, _b);

			TechCopy = E.GetTechniqueByName("Copy");
			TechFound = E.GetTechniqueByName("Found");
			TechAverage = E.GetTechniqueByName("Average");
			TechAnisotropic = E.GetTechniqueByName("Anisotropic");
			TechBicubic = E.GetTechniqueByName("Bicubic");

			for (var i = 0; i < TechCopy.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechCopy.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (PpDownsample, PT, Copy) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);

			FxInputMap = E.GetVariableByName("gInputMap").AsResource();
			FxScreenSize = E.GetVariableByName("gScreenSize").AsVector();
			FxMultipler = E.GetVariableByName("gMultipler").AsVector();
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

		public EffectTechnique TechDownsampling, TechAdaptation, TechTonemap, TechCopy, TechCombine, TechBloom, TechBloomHighThreshold;

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
			TechBloomHighThreshold = E.GetTechniqueByName("BloomHighThreshold");

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

	public class EffectPpLensFlares : IEffectWrapper {
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectTechnique TechGhosts;

		public EffectResourceVariable FxInputMap;

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "PpLensFlares");
			E = new Effect(device, _b);

			TechGhosts = E.GetTechniqueByName("Ghosts");

			for (var i = 0; i < TechGhosts.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechGhosts.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (PpLensFlares, PT, Ghosts) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);

			FxInputMap = E.GetVariableByName("gInputMap").AsResource();
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
		public static readonly float Threshold = 0.99999f;
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

		public static readonly uint HasNormalMap = 1;
		public static readonly uint UseDiffuseAlphaAsMap = 2;
		public static readonly uint UseNormalAlphaAsAlpha = 64;
		public static readonly uint AlphaTest = 128;
		public static readonly uint IsAdditive = 16;
		public static readonly uint HasDetailsMap = 4;
		public static readonly uint IsCarpaint = 32;
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

	public class EffectSpecialDebugLines : IEffectWrapper {
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePC;
        public InputLayout LayoutPC;

		public EffectTechnique TechMain;

		public EffectMatrixVariable FxWorldViewProj { get; private set; }

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "SpecialDebugLines");
			E = new Effect(device, _b);

			TechMain = E.GetTechniqueByName("Main");

			for (var i = 0; i < TechMain.Description.PassCount && InputSignaturePC == null; i++) {
				InputSignaturePC = TechMain.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePC == null) throw new System.Exception("input signature (SpecialDebugLines, PC, Main) == null");
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

	public class EffectSpecialShadow : IEffectWrapper {
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT, InputSignatureP;
        public InputLayout LayoutPT, LayoutP;

		public EffectTechnique TechHorizontalShadowBlur, TechVerticalShadowBlur, TechAmbientShadow, TechResult, TechSimplest;

		public EffectMatrixVariable FxShadowViewProj { get; private set; }
		public EffectMatrixVariable FxWorldViewProj { get; private set; }
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
			TechSimplest = E.GetTechniqueByName("Simplest");

			for (var i = 0; i < TechHorizontalShadowBlur.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechHorizontalShadowBlur.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (SpecialShadow, PT, HorizontalShadowBlur) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);
			for (var i = 0; i < TechSimplest.Description.PassCount && InputSignatureP == null; i++) {
				InputSignatureP = TechSimplest.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignatureP == null) throw new System.Exception("input signature (SpecialShadow, P, Simplest) == null");
			LayoutP = new InputLayout(device, InputSignatureP, InputLayouts.VerticeP.InputElementsValue);

			FxShadowViewProj = E.GetVariableByName("gShadowViewProj").AsMatrix();
			FxWorldViewProj = E.GetVariableByName("gWorldViewProj").AsMatrix();
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
			InputSignatureP.Dispose();
            LayoutP.Dispose();
            E.Dispose();
            _b.Dispose();
        }
	}

	public class EffectSpecialTrackMap : IEffectWrapper, IEffectScreenSizeWrapper {
		public static readonly int Gblurradius = 3;
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignatureP, InputSignaturePT;
        public InputLayout LayoutP, LayoutPT;

		public EffectTechnique TechMain, TechPp, TechFinal, TechFinalCheckers, TechPpHorizontalBlur, TechPpVerticalBlur;

		public EffectMatrixVariable FxWorldViewProj { get; private set; }
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

			for (var i = 0; i < TechMain.Description.PassCount && InputSignatureP == null; i++) {
				InputSignatureP = TechMain.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignatureP == null) throw new System.Exception("input signature (SpecialTrackMap, P, Main) == null");
			LayoutP = new InputLayout(device, InputSignatureP, InputLayouts.VerticeP.InputElementsValue);
			for (var i = 0; i < TechPp.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechPp.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (SpecialTrackMap, PT, Pp) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);

			FxWorldViewProj = E.GetVariableByName("gWorldViewProj").AsMatrix();
			FxInputMap = E.GetVariableByName("gInputMap").AsResource();
			FxScreenSize = E.GetVariableByName("gScreenSize").AsVector();
		}

        public void Dispose() {
			if (E == null) return;
			InputSignatureP.Dispose();
            LayoutP.Dispose();
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

	public class EffectSpriteShader : IEffectWrapper {
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignatureSpriteSpecific;
        public InputLayout LayoutSpriteSpecific;

		public EffectTechnique TechRender;

		public EffectResourceVariable FxTex;

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "SpriteShader");
			E = new Effect(device, _b);

			TechRender = E.GetTechniqueByName("Render");

			for (var i = 0; i < TechRender.Description.PassCount && InputSignatureSpriteSpecific == null; i++) {
				InputSignatureSpriteSpecific = TechRender.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignatureSpriteSpecific == null) throw new System.Exception("input signature (SpriteShader, SpriteSpecific, Render) == null");
			LayoutSpriteSpecific = new InputLayout(device, InputSignatureSpriteSpecific, Base.Sprites.VerticeSpriteSpecific.InputElementsValue);

			FxTex = E.GetVariableByName("Tex").AsResource();
		}

        public void Dispose() {
			if (E == null) return;
			InputSignatureSpriteSpecific.Dispose();
            LayoutSpriteSpecific.Dispose();
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
        public static void Set(this EffectVariable variable, EffectDarkMaterial.StandartMaterial o) {
            SlimDxExtension.Set(variable, o, EffectDarkMaterial.StandartMaterial.Stride);
        }
        public static void Set(this EffectVariable variable, EffectDarkMaterial.ReflectiveMaterial o) {
            SlimDxExtension.Set(variable, o, EffectDarkMaterial.ReflectiveMaterial.Stride);
        }
        public static void Set(this EffectVariable variable, EffectDarkMaterial.MapsMaterial o) {
            SlimDxExtension.Set(variable, o, EffectDarkMaterial.MapsMaterial.Stride);
        }
        public static void Set(this EffectVariable variable, EffectDarkMaterial.AlphaMaterial o) {
            SlimDxExtension.Set(variable, o, EffectDarkMaterial.AlphaMaterial.Stride);
        }
        public static void Set(this EffectVariable variable, EffectDarkMaterial.NmUvMultMaterial o) {
            SlimDxExtension.Set(variable, o, EffectDarkMaterial.NmUvMultMaterial.Stride);
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
