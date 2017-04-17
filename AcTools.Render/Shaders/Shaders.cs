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

        public class EffectStructStandartMaterialVariable {
			private readonly EffectVariable _v;

			public EffectStructStandartMaterialVariable(EffectVariable v) {
				_v = v;
			}

			public void Set(StandartMaterial value){
				 SlimDxExtension.SetObject(_v, value, StandartMaterial.Stride);
			}
        }

		[StructLayout(LayoutKind.Sequential)]
        public struct ReflectiveMaterial {
            public float FresnelC;
            public float FresnelExp;
            public float FresnelMaxLevel;

			public static readonly int Stride = Marshal.SizeOf(typeof(ReflectiveMaterial));
        }

        public class EffectStructReflectiveMaterialVariable {
			private readonly EffectVariable _v;

			public EffectStructReflectiveMaterialVariable(EffectVariable v) {
				_v = v;
			}

			public void Set(ReflectiveMaterial value){
				 SlimDxExtension.SetObject(_v, value, ReflectiveMaterial.Stride);
			}
        }

		[StructLayout(LayoutKind.Sequential)]
        public struct MapsMaterial {
            public float DetailsUvMultipler;
            public float DetailsNormalBlend;
            public float SunSpecular;
            public float SunSpecularExp;

			public static readonly int Stride = Marshal.SizeOf(typeof(MapsMaterial));
        }

        public class EffectStructMapsMaterialVariable {
			private readonly EffectVariable _v;

			public EffectStructMapsMaterialVariable(EffectVariable v) {
				_v = v;
			}

			public void Set(MapsMaterial value){
				 SlimDxExtension.SetObject(_v, value, MapsMaterial.Stride);
			}
        }

		[StructLayout(LayoutKind.Sequential)]
        public struct AlphaMaterial {
            public float Alpha;

			public static readonly int Stride = Marshal.SizeOf(typeof(AlphaMaterial));
        }

        public class EffectStructAlphaMaterialVariable {
			private readonly EffectVariable _v;

			public EffectStructAlphaMaterialVariable(EffectVariable v) {
				_v = v;
			}

			public void Set(AlphaMaterial value){
				 SlimDxExtension.SetObject(_v, value, AlphaMaterial.Stride);
			}
        }

		[StructLayout(LayoutKind.Sequential)]
        public struct NmUvMultMaterial {
            public float DiffuseMultipler;
            public float NormalMultipler;

			public static readonly int Stride = Marshal.SizeOf(typeof(NmUvMultMaterial));
        }

        public class EffectStructNmUvMultMaterialVariable {
			private readonly EffectVariable _v;

			public EffectStructNmUvMultMaterialVariable(EffectVariable v) {
				_v = v;
			}

			public void Set(NmUvMultMaterial value){
				 SlimDxExtension.SetObject(_v, value, NmUvMultMaterial.Stride);
			}
        }

		public static readonly uint HasNormalMap = 1;
		public static readonly uint UseNormalAlphaAsAlpha = 64;
		public static readonly uint AlphaTest = 128;
		public static readonly uint IsAdditive = 16;
		public static readonly uint HasDetailsMap = 4;
		public static readonly uint IsCarpaint = 32;
		public static readonly int MaxNumSplits = 3;
		public static readonly bool EnableShadows = true;
		public static readonly bool EnablePcss = true;
		public static readonly int MaxBones = 64;
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT, InputSignaturePNTG, InputSignaturePNTGW4B;
        public InputLayout LayoutPT, LayoutPNTG, LayoutPNTGW4B;

		public EffectReadyTechnique TechGPass_Standard, TechGPass_Alpha, TechGPass_Reflective, TechGPass_Nm, TechGPass_NmUvMult, TechGPass_AtNm, TechGPass_Maps, TechGPass_SkinnedMaps, TechGPass_DiffMaps, TechGPass_Gl, TechGPass_SkinnedGl, TechGPass_FlatMirror, TechGPass_Debug, TechGPass_SkinnedDebug, TechStandard, TechSky, TechAlpha, TechReflective, TechNm, TechNmUvMult, TechAtNm, TechMaps, TechSkinnedMaps, TechDiffMaps, TechGl, TechSkinnedGl, TechWindscreen, TechCollider, TechDebug, TechSkinnedDebug, TechDepthOnly, TechSkinnedDepthOnly, TechAmbientShadow, TechMirror, TechFlatMirror, TechFlatTextureMirror, TechFlatBackgroundGround, TechFlatAmbientGround;

		public EffectOnlyMatrixVariable FxWorld { get; private set; }
		public EffectOnlyMatrixVariable FxWorldInvTranspose { get; private set; }
		public EffectOnlyMatrixVariable FxWorldViewProj { get; private set; }
		public EffectOnlyMatrixArrayVariable FxShadowViewProj { get; private set; }
		public EffectOnlyMatrixArrayVariable FxBoneTransforms { get; private set; }
		public EffectOnlyResourceVariable FxNoiseMap, FxDiffuseMap, FxNormalMap, FxMapsMap, FxDetailsMap, FxDetailsNormalMap, FxAoMap, FxReflectionCubemap;
		public EffectOnlyResourceArrayVariable FxShadowMaps;
		public EffectScalarVariable FxGPassTransparent, FxGPassAlphaThreshold, FxNumSplits, FxPcssEnabled, FxFlatMirrored, FxReflectionPower, FxAoPower, FxCubemapReflections, FxCubemapAmbient, FxFlatMirrorPower;
		public EffectVectorVariable FxPcssScale { get; private set; }
		public EffectVectorVariable FxShadowMapSize { get; private set; }
		public EffectVectorVariable FxEyePosW { get; private set; }
		public EffectVectorVariable FxLightDir { get; private set; }
		public EffectVectorVariable FxLightColor { get; private set; }
		public EffectVectorVariable FxAmbientDown { get; private set; }
		public EffectVectorVariable FxAmbientRange { get; private set; }
		public EffectVectorVariable FxBackgroundColor { get; private set; }
		public EffectVectorVariable FxScreenSize { get; private set; }
		public EffectStructStandartMaterialVariable FxMaterial { get; private set; }
		public EffectStructReflectiveMaterialVariable FxReflectiveMaterial { get; private set; }
		public EffectStructMapsMaterialVariable FxMapsMaterial { get; private set; }
		public EffectStructAlphaMaterialVariable FxAlphaMaterial { get; private set; }
		public EffectStructNmUvMultMaterialVariable FxNmUvMultMaterial { get; private set; }

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "DarkMaterial");
			E = new Effect(device, _b);

			TechGPass_Standard = new EffectReadyTechnique(E.GetTechniqueByName("GPass_Standard"));
			TechGPass_Alpha = new EffectReadyTechnique(E.GetTechniqueByName("GPass_Alpha"));
			TechGPass_Reflective = new EffectReadyTechnique(E.GetTechniqueByName("GPass_Reflective"));
			TechGPass_Nm = new EffectReadyTechnique(E.GetTechniqueByName("GPass_Nm"));
			TechGPass_NmUvMult = new EffectReadyTechnique(E.GetTechniqueByName("GPass_NmUvMult"));
			TechGPass_AtNm = new EffectReadyTechnique(E.GetTechniqueByName("GPass_AtNm"));
			TechGPass_Maps = new EffectReadyTechnique(E.GetTechniqueByName("GPass_Maps"));
			TechGPass_SkinnedMaps = new EffectReadyTechnique(E.GetTechniqueByName("GPass_SkinnedMaps"));
			TechGPass_DiffMaps = new EffectReadyTechnique(E.GetTechniqueByName("GPass_DiffMaps"));
			TechGPass_Gl = new EffectReadyTechnique(E.GetTechniqueByName("GPass_Gl"));
			TechGPass_SkinnedGl = new EffectReadyTechnique(E.GetTechniqueByName("GPass_SkinnedGl"));
			TechGPass_FlatMirror = new EffectReadyTechnique(E.GetTechniqueByName("GPass_FlatMirror"));
			TechGPass_Debug = new EffectReadyTechnique(E.GetTechniqueByName("GPass_Debug"));
			TechGPass_SkinnedDebug = new EffectReadyTechnique(E.GetTechniqueByName("GPass_SkinnedDebug"));
			TechStandard = new EffectReadyTechnique(E.GetTechniqueByName("Standard"));
			TechSky = new EffectReadyTechnique(E.GetTechniqueByName("Sky"));
			TechAlpha = new EffectReadyTechnique(E.GetTechniqueByName("Alpha"));
			TechReflective = new EffectReadyTechnique(E.GetTechniqueByName("Reflective"));
			TechNm = new EffectReadyTechnique(E.GetTechniqueByName("Nm"));
			TechNmUvMult = new EffectReadyTechnique(E.GetTechniqueByName("NmUvMult"));
			TechAtNm = new EffectReadyTechnique(E.GetTechniqueByName("AtNm"));
			TechMaps = new EffectReadyTechnique(E.GetTechniqueByName("Maps"));
			TechSkinnedMaps = new EffectReadyTechnique(E.GetTechniqueByName("SkinnedMaps"));
			TechDiffMaps = new EffectReadyTechnique(E.GetTechniqueByName("DiffMaps"));
			TechGl = new EffectReadyTechnique(E.GetTechniqueByName("Gl"));
			TechSkinnedGl = new EffectReadyTechnique(E.GetTechniqueByName("SkinnedGl"));
			TechWindscreen = new EffectReadyTechnique(E.GetTechniqueByName("Windscreen"));
			TechCollider = new EffectReadyTechnique(E.GetTechniqueByName("Collider"));
			TechDebug = new EffectReadyTechnique(E.GetTechniqueByName("Debug"));
			TechSkinnedDebug = new EffectReadyTechnique(E.GetTechniqueByName("SkinnedDebug"));
			TechDepthOnly = new EffectReadyTechnique(E.GetTechniqueByName("DepthOnly"));
			TechSkinnedDepthOnly = new EffectReadyTechnique(E.GetTechniqueByName("SkinnedDepthOnly"));
			TechAmbientShadow = new EffectReadyTechnique(E.GetTechniqueByName("AmbientShadow"));
			TechMirror = new EffectReadyTechnique(E.GetTechniqueByName("Mirror"));
			TechFlatMirror = new EffectReadyTechnique(E.GetTechniqueByName("FlatMirror"));
			TechFlatTextureMirror = new EffectReadyTechnique(E.GetTechniqueByName("FlatTextureMirror"));
			TechFlatBackgroundGround = new EffectReadyTechnique(E.GetTechniqueByName("FlatBackgroundGround"));
			TechFlatAmbientGround = new EffectReadyTechnique(E.GetTechniqueByName("FlatAmbientGround"));

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

			FxWorld = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorld").AsMatrix());
			FxWorldInvTranspose = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldInvTranspose").AsMatrix());
			FxWorldViewProj = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldViewProj").AsMatrix());
			FxShadowViewProj = new EffectOnlyMatrixArrayVariable(E.GetVariableByName("gShadowViewProj").AsMatrix());
			FxBoneTransforms = new EffectOnlyMatrixArrayVariable(E.GetVariableByName("gBoneTransforms").AsMatrix());
			FxNoiseMap = new EffectOnlyResourceVariable(E.GetVariableByName("gNoiseMap").AsResource());
			FxDiffuseMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDiffuseMap").AsResource());
			FxNormalMap = new EffectOnlyResourceVariable(E.GetVariableByName("gNormalMap").AsResource());
			FxMapsMap = new EffectOnlyResourceVariable(E.GetVariableByName("gMapsMap").AsResource());
			FxDetailsMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDetailsMap").AsResource());
			FxDetailsNormalMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDetailsNormalMap").AsResource());
			FxAoMap = new EffectOnlyResourceVariable(E.GetVariableByName("gAoMap").AsResource());
			FxReflectionCubemap = new EffectOnlyResourceVariable(E.GetVariableByName("gReflectionCubemap").AsResource());
			FxShadowMaps = new EffectOnlyResourceArrayVariable(E.GetVariableByName("gShadowMaps").AsResource());
			FxGPassTransparent = E.GetVariableByName("gGPassTransparent").AsScalar();
			FxGPassAlphaThreshold = E.GetVariableByName("gGPassAlphaThreshold").AsScalar();
			FxNumSplits = E.GetVariableByName("gNumSplits").AsScalar();
			FxPcssEnabled = E.GetVariableByName("gPcssEnabled").AsScalar();
			FxFlatMirrored = E.GetVariableByName("gFlatMirrored").AsScalar();
			FxReflectionPower = E.GetVariableByName("gReflectionPower").AsScalar();
			FxAoPower = E.GetVariableByName("gAoPower").AsScalar();
			FxCubemapReflections = E.GetVariableByName("gCubemapReflections").AsScalar();
			FxCubemapAmbient = E.GetVariableByName("gCubemapAmbient").AsScalar();
			FxFlatMirrorPower = E.GetVariableByName("gFlatMirrorPower").AsScalar();
			FxPcssScale = E.GetVariableByName("gPcssScale").AsVector();
			FxShadowMapSize = E.GetVariableByName("gShadowMapSize").AsVector();
			FxEyePosW = E.GetVariableByName("gEyePosW").AsVector();
			FxLightDir = E.GetVariableByName("gLightDir").AsVector();
			FxLightColor = E.GetVariableByName("gLightColor").AsVector();
			FxAmbientDown = E.GetVariableByName("gAmbientDown").AsVector();
			FxAmbientRange = E.GetVariableByName("gAmbientRange").AsVector();
			FxBackgroundColor = E.GetVariableByName("gBackgroundColor").AsVector();
			FxScreenSize = E.GetVariableByName("gScreenSize").AsVector();
			FxMaterial = new EffectStructStandartMaterialVariable(E.GetVariableByName("gMaterial"));
			FxReflectiveMaterial = new EffectStructReflectiveMaterialVariable(E.GetVariableByName("gReflectiveMaterial"));
			FxMapsMaterial = new EffectStructMapsMaterialVariable(E.GetVariableByName("gMapsMaterial"));
			FxAlphaMaterial = new EffectStructAlphaMaterialVariable(E.GetVariableByName("gAlphaMaterial"));
			FxNmUvMultMaterial = new EffectStructNmUvMultMaterialVariable(E.GetVariableByName("gNmUvMultMaterial"));
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

        public class EffectStructMaterialVariable {
			private readonly EffectVariable _v;

			public EffectStructMaterialVariable(EffectVariable v) {
				_v = v;
			}

			public void Set(Material value){
				 SlimDxExtension.SetObject(_v, value, Material.Stride);
			}
        }

		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePNT;
        public InputLayout LayoutPNT;

		public EffectReadyTechnique TechPerPixel;

		public EffectOnlyMatrixVariable FxWorld { get; private set; }
		public EffectOnlyMatrixVariable FxWorldInvTranspose { get; private set; }
		public EffectOnlyMatrixVariable FxWorldViewProj { get; private set; }
		public EffectOnlyResourceVariable FxDiffuseMap;
		public EffectVectorVariable FxEyePosW { get; private set; }
		public EffectStructMaterialVariable FxMaterial { get; private set; }

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "KunosShader");
			E = new Effect(device, _b);

			TechPerPixel = new EffectReadyTechnique(E.GetTechniqueByName("PerPixel"));

			for (var i = 0; i < TechPerPixel.Description.PassCount && InputSignaturePNT == null; i++) {
				InputSignaturePNT = TechPerPixel.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePNT == null) throw new System.Exception("input signature (KunosShader, PNT, PerPixel) == null");
			LayoutPNT = new InputLayout(device, InputSignaturePNT, InputLayouts.VerticePNT.InputElementsValue);

			FxWorld = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorld").AsMatrix());
			FxWorldInvTranspose = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldInvTranspose").AsMatrix());
			FxWorldViewProj = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldViewProj").AsMatrix());
			FxDiffuseMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDiffuseMap").AsResource());
			FxEyePosW = E.GetVariableByName("gEyePosW").AsVector();
			FxMaterial = new EffectStructMaterialVariable(E.GetVariableByName("gMaterial"));
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

		public EffectReadyTechnique TechCopy, TechCut, TechCopyHq, TechCopyNoAlpha, TechOverlay, TechShadow, TechDepth, TechFxaa;

		public EffectOnlyResourceVariable FxInputMap, FxOverlayMap, FxDepthMap;
		public EffectScalarVariable FxSizeMultipler;
		public EffectVectorVariable FxScreenSize { get; private set; }

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "PpBasic");
			E = new Effect(device, _b);

			TechCopy = new EffectReadyTechnique(E.GetTechniqueByName("Copy"));
			TechCut = new EffectReadyTechnique(E.GetTechniqueByName("Cut"));
			TechCopyHq = new EffectReadyTechnique(E.GetTechniqueByName("CopyHq"));
			TechCopyNoAlpha = new EffectReadyTechnique(E.GetTechniqueByName("CopyNoAlpha"));
			TechOverlay = new EffectReadyTechnique(E.GetTechniqueByName("Overlay"));
			TechShadow = new EffectReadyTechnique(E.GetTechniqueByName("Shadow"));
			TechDepth = new EffectReadyTechnique(E.GetTechniqueByName("Depth"));
			TechFxaa = new EffectReadyTechnique(E.GetTechniqueByName("Fxaa"));

			for (var i = 0; i < TechCopy.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechCopy.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (PpBasic, PT, Copy) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);

			FxInputMap = new EffectOnlyResourceVariable(E.GetVariableByName("gInputMap").AsResource());
			FxOverlayMap = new EffectOnlyResourceVariable(E.GetVariableByName("gOverlayMap").AsResource());
			FxDepthMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDepthMap").AsResource());
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

		public EffectReadyTechnique TechGaussianBlur, TechFlatMirrorBlur, TechDarkSslrBlur0, TechReflectionGaussianBlur;

		public EffectOnlyMatrixVariable FxWorldViewProjInv { get; private set; }
		public EffectOnlyResourceVariable FxInputMap, FxFlatMirrorDepthMap, FxFlatMirrorNormalsMap, FxMapsMap;
		public EffectScalarVariable FxSampleWeights, FxPower;
		public EffectVectorVariable FxSampleOffsets { get; private set; }
		public EffectVectorVariable FxScreenSize { get; private set; }

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "PpBlur");
			E = new Effect(device, _b);

			TechGaussianBlur = new EffectReadyTechnique(E.GetTechniqueByName("GaussianBlur"));
			TechFlatMirrorBlur = new EffectReadyTechnique(E.GetTechniqueByName("FlatMirrorBlur"));
			TechDarkSslrBlur0 = new EffectReadyTechnique(E.GetTechniqueByName("DarkSslrBlur0"));
			TechReflectionGaussianBlur = new EffectReadyTechnique(E.GetTechniqueByName("ReflectionGaussianBlur"));

			for (var i = 0; i < TechGaussianBlur.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechGaussianBlur.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (PpBlur, PT, GaussianBlur) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);

			FxWorldViewProjInv = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldViewProjInv").AsMatrix());
			FxInputMap = new EffectOnlyResourceVariable(E.GetVariableByName("gInputMap").AsResource());
			FxFlatMirrorDepthMap = new EffectOnlyResourceVariable(E.GetVariableByName("gFlatMirrorDepthMap").AsResource());
			FxFlatMirrorNormalsMap = new EffectOnlyResourceVariable(E.GetVariableByName("gFlatMirrorNormalsMap").AsResource());
			FxMapsMap = new EffectOnlyResourceVariable(E.GetVariableByName("gMapsMap").AsResource());
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

		public EffectReadyTechnique TechDownscale4, TechSslr, TechFinalStep;

		public EffectOnlyMatrixVariable FxCameraProjInv { get; private set; }
		public EffectOnlyMatrixVariable FxCameraProj { get; private set; }
		public EffectOnlyMatrixVariable FxWorldViewProjInv { get; private set; }
		public EffectOnlyMatrixVariable FxWorldViewProj { get; private set; }
		public EffectOnlyResourceVariable FxDiffuseMap, FxDepthMap, FxBaseReflectionMap, FxNormalMap, FxNoiseMap, FxFirstStepMap, FxDepthMapDown, FxDepthMapDownMore;
		public EffectScalarVariable FxStartFrom, FxFixMultiplier, FxOffset, FxGlowFix, FxDistanceThreshold;
		public EffectVectorVariable FxEyePosW { get; private set; }
		public EffectVectorVariable FxSize { get; private set; }

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "PpDarkSslr");
			E = new Effect(device, _b);

			TechDownscale4 = new EffectReadyTechnique(E.GetTechniqueByName("Downscale4"));
			TechSslr = new EffectReadyTechnique(E.GetTechniqueByName("Sslr"));
			TechFinalStep = new EffectReadyTechnique(E.GetTechniqueByName("FinalStep"));

			for (var i = 0; i < TechDownscale4.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechDownscale4.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (PpDarkSslr, PT, Downscale4) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);

			FxCameraProjInv = new EffectOnlyMatrixVariable(E.GetVariableByName("gCameraProjInv").AsMatrix());
			FxCameraProj = new EffectOnlyMatrixVariable(E.GetVariableByName("gCameraProj").AsMatrix());
			FxWorldViewProjInv = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldViewProjInv").AsMatrix());
			FxWorldViewProj = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldViewProj").AsMatrix());
			FxDiffuseMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDiffuseMap").AsResource());
			FxDepthMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDepthMap").AsResource());
			FxBaseReflectionMap = new EffectOnlyResourceVariable(E.GetVariableByName("gBaseReflectionMap").AsResource());
			FxNormalMap = new EffectOnlyResourceVariable(E.GetVariableByName("gNormalMap").AsResource());
			FxNoiseMap = new EffectOnlyResourceVariable(E.GetVariableByName("gNoiseMap").AsResource());
			FxFirstStepMap = new EffectOnlyResourceVariable(E.GetVariableByName("gFirstStepMap").AsResource());
			FxDepthMapDown = new EffectOnlyResourceVariable(E.GetVariableByName("gDepthMapDown").AsResource());
			FxDepthMapDownMore = new EffectOnlyResourceVariable(E.GetVariableByName("gDepthMapDownMore").AsResource());
			FxStartFrom = E.GetVariableByName("gStartFrom").AsScalar();
			FxFixMultiplier = E.GetVariableByName("gFixMultiplier").AsScalar();
			FxOffset = E.GetVariableByName("gOffset").AsScalar();
			FxGlowFix = E.GetVariableByName("gGlowFix").AsScalar();
			FxDistanceThreshold = E.GetVariableByName("gDistanceThreshold").AsScalar();
			FxEyePosW = E.GetVariableByName("gEyePosW").AsVector();
			FxSize = E.GetVariableByName("gSize").AsVector();
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

		public EffectReadyTechnique TechCopy, TechAverage, TechAnisotropic;

		public EffectOnlyResourceVariable FxInputMap;
		public EffectVectorVariable FxScreenSize { get; private set; }
		public EffectVectorVariable FxMultipler { get; private set; }

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "PpDownsample");
			E = new Effect(device, _b);

			TechCopy = new EffectReadyTechnique(E.GetTechniqueByName("Copy"));
			TechAverage = new EffectReadyTechnique(E.GetTechniqueByName("Average"));
			TechAnisotropic = new EffectReadyTechnique(E.GetTechniqueByName("Anisotropic"));

			for (var i = 0; i < TechCopy.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechCopy.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (PpDownsample, PT, Copy) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);

			FxInputMap = new EffectOnlyResourceVariable(E.GetVariableByName("gInputMap").AsResource());
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

		public EffectReadyTechnique TechDownsampling, TechAdaptation, TechTonemap, TechCopy, TechColorGrading, TechCombine_ToneReinhard, TechCombine_ToneFilmic, TechCombine_ToneFilmicReinhard, TechCombine, TechBloom, TechBloomHighThreshold;

		public EffectOnlyResourceVariable FxInputMap, FxBrightnessMap, FxBloomMap, FxColorGradingMap;
		public EffectVectorVariable FxPixel { get; private set; }
		public EffectVectorVariable FxCropImage { get; private set; }
		public EffectVectorVariable FxParams { get; private set; }

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "PpHdr");
			E = new Effect(device, _b);

			TechDownsampling = new EffectReadyTechnique(E.GetTechniqueByName("Downsampling"));
			TechAdaptation = new EffectReadyTechnique(E.GetTechniqueByName("Adaptation"));
			TechTonemap = new EffectReadyTechnique(E.GetTechniqueByName("Tonemap"));
			TechCopy = new EffectReadyTechnique(E.GetTechniqueByName("Copy"));
			TechColorGrading = new EffectReadyTechnique(E.GetTechniqueByName("ColorGrading"));
			TechCombine_ToneReinhard = new EffectReadyTechnique(E.GetTechniqueByName("Combine_ToneReinhard"));
			TechCombine_ToneFilmic = new EffectReadyTechnique(E.GetTechniqueByName("Combine_ToneFilmic"));
			TechCombine_ToneFilmicReinhard = new EffectReadyTechnique(E.GetTechniqueByName("Combine_ToneFilmicReinhard"));
			TechCombine = new EffectReadyTechnique(E.GetTechniqueByName("Combine"));
			TechBloom = new EffectReadyTechnique(E.GetTechniqueByName("Bloom"));
			TechBloomHighThreshold = new EffectReadyTechnique(E.GetTechniqueByName("BloomHighThreshold"));

			for (var i = 0; i < TechDownsampling.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechDownsampling.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (PpHdr, PT, Downsampling) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);

			FxInputMap = new EffectOnlyResourceVariable(E.GetVariableByName("gInputMap").AsResource());
			FxBrightnessMap = new EffectOnlyResourceVariable(E.GetVariableByName("gBrightnessMap").AsResource());
			FxBloomMap = new EffectOnlyResourceVariable(E.GetVariableByName("gBloomMap").AsResource());
			FxColorGradingMap = new EffectOnlyResourceVariable(E.GetVariableByName("gColorGradingMap").AsResource());
			FxPixel = E.GetVariableByName("gPixel").AsVector();
			FxCropImage = E.GetVariableByName("gCropImage").AsVector();
			FxParams = E.GetVariableByName("gParams").AsVector();
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

		public EffectReadyTechnique TechGhosts;

		public EffectOnlyResourceVariable FxInputMap;

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "PpLensFlares");
			E = new Effect(device, _b);

			TechGhosts = new EffectReadyTechnique(E.GetTechniqueByName("Ghosts"));

			for (var i = 0; i < TechGhosts.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechGhosts.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (PpLensFlares, PT, Ghosts) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);

			FxInputMap = new EffectOnlyResourceVariable(E.GetVariableByName("gInputMap").AsResource());
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

		public EffectReadyTechnique TechOutline;

		public EffectOnlyResourceVariable FxInputMap, FxDepthMap;
		public EffectVectorVariable FxScreenSize { get; private set; }

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "PpOutline");
			E = new Effect(device, _b);

			TechOutline = new EffectReadyTechnique(E.GetTechniqueByName("Outline"));

			for (var i = 0; i < TechOutline.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechOutline.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (PpOutline, PT, Outline) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);

			FxInputMap = new EffectOnlyResourceVariable(E.GetVariableByName("gInputMap").AsResource());
			FxDepthMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDepthMap").AsResource());
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

	public class EffectPpAoBlur : IEffectWrapper {
		public static readonly int MaxBlurRadius = 4;
		public static readonly int BlurRadius = 4;
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectReadyTechnique TechBlurH, TechBlurV;

		public EffectOnlyResourceVariable FxDepthMap, FxNormalMap, FxFirstStepMap;
		public EffectScalarVariable FxWeights;
		public EffectVectorVariable FxSourcePixel { get; private set; }
		public EffectVectorVariable FxNearFarValue { get; private set; }

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "PpAoBlur");
			E = new Effect(device, _b);

			TechBlurH = new EffectReadyTechnique(E.GetTechniqueByName("BlurH"));
			TechBlurV = new EffectReadyTechnique(E.GetTechniqueByName("BlurV"));

			for (var i = 0; i < TechBlurH.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechBlurH.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (PpAoBlur, PT, BlurH) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);

			FxDepthMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDepthMap").AsResource());
			FxNormalMap = new EffectOnlyResourceVariable(E.GetVariableByName("gNormalMap").AsResource());
			FxFirstStepMap = new EffectOnlyResourceVariable(E.GetVariableByName("gFirstStepMap").AsResource());
			FxWeights = E.GetVariableByName("gWeights").AsScalar();
			FxSourcePixel = E.GetVariableByName("gSourcePixel").AsVector();
			FxNearFarValue = E.GetVariableByName("gNearFarValue").AsVector();
		}

        public void Dispose() {
			if (E == null) return;
			InputSignaturePT.Dispose();
            LayoutPT.Dispose();
            E.Dispose();
            _b.Dispose();
        }
	}

	public class EffectPpAssao : IEffectWrapper {
		[StructLayout(LayoutKind.Sequential)]
        public struct ASSAOConstants {
            public Vector2 ViewportPixelSize;
            public Vector2 HalfViewportPixelSize;
            public Vector2 DepthUnpackConsts;
            public Vector2 CameraTanHalfFOV;
            public Vector2 NDCToViewMul;
            public Vector2 NDCToViewAdd;
            public Vector2 PerPassFullResCoordOffset;
            public Vector2 PerPassFullResUVOffset;
            public Vector2 Viewport2xPixelSize;
            public Vector2 Viewport2xPixelSize_x_025;
            public float EffectRadius;
            public float EffectShadowStrength;
            public float EffectShadowPow;
            public float EffectShadowClamp;
            public float EffectFadeOutMul;
            public float EffectFadeOutAdd;
            public float EffectHorizonAngleThreshold;
            public float EffectSamplingRadiusNearLimitRec;
            public float DepthPrecisionOffsetMod;
            public float NegRecEffectRadius;
            public float LoadCounterAvgDiv;
            public float AdaptiveSampleCountLimit;
            public float InvSharpness;
            public int PassIndex;
            public Vector2 QuarterResPixelSize;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
            public Vector4[] PatternRotScaleMatrices;
            public float NormalsUnpackMul;
            public float NormalsUnpackAdd;
            public float DetailAOStrength;
            public float Dummy0;
            public Matrix NormalsWorldToViewspaceMatrix;

			public static readonly int Stride = Marshal.SizeOf(typeof(ASSAOConstants));
        }

        public class EffectStructASSAOConstantsVariable {
			private readonly EffectVariable _v;

			public EffectStructASSAOConstantsVariable(EffectVariable v) {
				_v = v;
			}

			public void Set(ASSAOConstants value){
				 SlimDxExtension.SetObject(_v, value, ASSAOConstants.Stride);
			}
        }

		public static readonly int SsaoAdaptiveTapBaseCount = 5;
		public static readonly int SsaoAdaptiveTapFlexibleCount = 5;
		public static readonly int SsaoMaxTaps = 12;
		public static readonly int SampleCount = 16;
		public static readonly int SsaoEnableNormalWorldToViewConversion = 1;
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectReadyTechnique TechPrepareDepth, TechAssao;

		public EffectOnlyMatrixVariable FxWorldViewProjInv { get; private set; }
		public EffectOnlyMatrixVariable FxWorldViewProj { get; private set; }
		public EffectOnlyMatrixVariable FxProj { get; private set; }
		public EffectOnlyMatrixVariable FxProjInv { get; private set; }
		public EffectOnlyMatrixVariable FxNormalsToViewSpace { get; private set; }
		public EffectOnlyResourceVariable Fx_DepthSource, Fx_NormalmapSource, Fx_ViewspaceDepthSource, Fx_ViewspaceDepthSource1, Fx_ViewspaceDepthSource2, Fx_ViewspaceDepthSource3, Fx_ImportanceMap, Fx_LoadCounter, Fx_BlurInput, FxDepthMap, FxNormalMap, FxNoiseMap, FxDitherMap, FxFirstStepMap;
		public EffectVectorVariable FxViewFrustumVectors { get; private set; }
		public EffectStructASSAOConstantsVariable Fx_ASSAOConsts { get; private set; }

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "PpAssao");
			E = new Effect(device, _b);

			TechPrepareDepth = new EffectReadyTechnique(E.GetTechniqueByName("PrepareDepth"));
			TechAssao = new EffectReadyTechnique(E.GetTechniqueByName("Assao"));

			for (var i = 0; i < TechPrepareDepth.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechPrepareDepth.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (PpAssao, PT, PrepareDepth) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);

			FxWorldViewProjInv = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldViewProjInv").AsMatrix());
			FxWorldViewProj = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldViewProj").AsMatrix());
			FxProj = new EffectOnlyMatrixVariable(E.GetVariableByName("gProj").AsMatrix());
			FxProjInv = new EffectOnlyMatrixVariable(E.GetVariableByName("gProjInv").AsMatrix());
			FxNormalsToViewSpace = new EffectOnlyMatrixVariable(E.GetVariableByName("gNormalsToViewSpace").AsMatrix());
			Fx_DepthSource = new EffectOnlyResourceVariable(E.GetVariableByName("g_DepthSource").AsResource());
			Fx_NormalmapSource = new EffectOnlyResourceVariable(E.GetVariableByName("g_NormalmapSource").AsResource());
			Fx_ViewspaceDepthSource = new EffectOnlyResourceVariable(E.GetVariableByName("g_ViewspaceDepthSource").AsResource());
			Fx_ViewspaceDepthSource1 = new EffectOnlyResourceVariable(E.GetVariableByName("g_ViewspaceDepthSource1").AsResource());
			Fx_ViewspaceDepthSource2 = new EffectOnlyResourceVariable(E.GetVariableByName("g_ViewspaceDepthSource2").AsResource());
			Fx_ViewspaceDepthSource3 = new EffectOnlyResourceVariable(E.GetVariableByName("g_ViewspaceDepthSource3").AsResource());
			Fx_ImportanceMap = new EffectOnlyResourceVariable(E.GetVariableByName("g_ImportanceMap").AsResource());
			Fx_LoadCounter = new EffectOnlyResourceVariable(E.GetVariableByName("g_LoadCounter").AsResource());
			Fx_BlurInput = new EffectOnlyResourceVariable(E.GetVariableByName("g_BlurInput").AsResource());
			FxDepthMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDepthMap").AsResource());
			FxNormalMap = new EffectOnlyResourceVariable(E.GetVariableByName("gNormalMap").AsResource());
			FxNoiseMap = new EffectOnlyResourceVariable(E.GetVariableByName("gNoiseMap").AsResource());
			FxDitherMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDitherMap").AsResource());
			FxFirstStepMap = new EffectOnlyResourceVariable(E.GetVariableByName("gFirstStepMap").AsResource());
			FxViewFrustumVectors = E.GetVariableByName("gViewFrustumVectors").AsVector();
			Fx_ASSAOConsts = new EffectStructASSAOConstantsVariable(E.GetVariableByName("g_ASSAOConsts"));
		}

        public void Dispose() {
			if (E == null) return;
			InputSignaturePT.Dispose();
            LayoutPT.Dispose();
            E.Dispose();
            _b.Dispose();
        }
	}

	public class EffectPpHbao : IEffectWrapper {
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectReadyTechnique TechHbao;

		public EffectOnlyMatrixVariable FxWorldViewProjInv { get; private set; }
		public EffectOnlyMatrixVariable FxView { get; private set; }
		public EffectOnlyMatrixVariable FxProj { get; private set; }
		public EffectOnlyMatrixVariable FxProjT { get; private set; }
		public EffectOnlyMatrixVariable FxProjInv { get; private set; }
		public EffectOnlyMatrixVariable FxNormalsToViewSpace { get; private set; }
		public EffectOnlyMatrixVariable FxProjectionMatrix { get; private set; }
		public EffectOnlyResourceVariable FxDepthMap, FxNormalMap, FxNoiseMap, FxDitherMap, FxFirstStepMap;
		public EffectScalarVariable FxDitherScale;
		public EffectVectorVariable FxViewFrustumVectors { get; private set; }
		public EffectVectorVariable FxRenderTargetResolution { get; private set; }
		public EffectVectorVariable FxSampleDirections { get; private set; }

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "PpHbao");
			E = new Effect(device, _b);

			TechHbao = new EffectReadyTechnique(E.GetTechniqueByName("Hbao"));

			for (var i = 0; i < TechHbao.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechHbao.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (PpHbao, PT, Hbao) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);

			FxWorldViewProjInv = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldViewProjInv").AsMatrix());
			FxView = new EffectOnlyMatrixVariable(E.GetVariableByName("gView").AsMatrix());
			FxProj = new EffectOnlyMatrixVariable(E.GetVariableByName("gProj").AsMatrix());
			FxProjT = new EffectOnlyMatrixVariable(E.GetVariableByName("gProjT").AsMatrix());
			FxProjInv = new EffectOnlyMatrixVariable(E.GetVariableByName("gProjInv").AsMatrix());
			FxNormalsToViewSpace = new EffectOnlyMatrixVariable(E.GetVariableByName("gNormalsToViewSpace").AsMatrix());
			FxProjectionMatrix = new EffectOnlyMatrixVariable(E.GetVariableByName("gProjectionMatrix").AsMatrix());
			FxDepthMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDepthMap").AsResource());
			FxNormalMap = new EffectOnlyResourceVariable(E.GetVariableByName("gNormalMap").AsResource());
			FxNoiseMap = new EffectOnlyResourceVariable(E.GetVariableByName("gNoiseMap").AsResource());
			FxDitherMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDitherMap").AsResource());
			FxFirstStepMap = new EffectOnlyResourceVariable(E.GetVariableByName("gFirstStepMap").AsResource());
			FxDitherScale = E.GetVariableByName("gDitherScale").AsScalar();
			FxViewFrustumVectors = E.GetVariableByName("gViewFrustumVectors").AsVector();
			FxRenderTargetResolution = E.GetVariableByName("gRenderTargetResolution").AsVector();
			FxSampleDirections = E.GetVariableByName("gSampleDirections").AsVector();
		}

        public void Dispose() {
			if (E == null) return;
			InputSignaturePT.Dispose();
            LayoutPT.Dispose();
            E.Dispose();
            _b.Dispose();
        }
	}

	public class EffectPpSsao : IEffectWrapper {
		public static readonly int SampleCount = 16;
		public static readonly float Radius = 0.15f;
		public static readonly float NormalBias = 0.01f;
		public static readonly int MaxBlurRadius = 4;
		public static readonly int BlurRadius = 4;
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectReadyTechnique TechSsao, TechBlurH, TechBlurV;

		public EffectOnlyMatrixVariable FxCameraProjInv { get; private set; }
		public EffectOnlyMatrixVariable FxCameraProj { get; private set; }
		public EffectOnlyMatrixVariable FxWorldViewProjInv { get; private set; }
		public EffectOnlyMatrixVariable FxWorldViewProj { get; private set; }
		public EffectOnlyResourceVariable FxDepthMap, FxNormalMap, FxNoiseMap, FxFirstStepMap;
		public EffectScalarVariable FxWeights;
		public EffectVectorVariable FxSamplesKernel { get; private set; }
		public EffectVectorVariable FxEyePosW { get; private set; }
		public EffectVectorVariable FxNoiseSize { get; private set; }
		public EffectVectorVariable FxSourcePixel { get; private set; }
		public EffectVectorVariable FxNearFarValue { get; private set; }

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "PpSsao");
			E = new Effect(device, _b);

			TechSsao = new EffectReadyTechnique(E.GetTechniqueByName("Ssao"));
			TechBlurH = new EffectReadyTechnique(E.GetTechniqueByName("BlurH"));
			TechBlurV = new EffectReadyTechnique(E.GetTechniqueByName("BlurV"));

			for (var i = 0; i < TechSsao.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechSsao.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (PpSsao, PT, Ssao) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);

			FxCameraProjInv = new EffectOnlyMatrixVariable(E.GetVariableByName("gCameraProjInv").AsMatrix());
			FxCameraProj = new EffectOnlyMatrixVariable(E.GetVariableByName("gCameraProj").AsMatrix());
			FxWorldViewProjInv = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldViewProjInv").AsMatrix());
			FxWorldViewProj = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldViewProj").AsMatrix());
			FxDepthMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDepthMap").AsResource());
			FxNormalMap = new EffectOnlyResourceVariable(E.GetVariableByName("gNormalMap").AsResource());
			FxNoiseMap = new EffectOnlyResourceVariable(E.GetVariableByName("gNoiseMap").AsResource());
			FxFirstStepMap = new EffectOnlyResourceVariable(E.GetVariableByName("gFirstStepMap").AsResource());
			FxWeights = E.GetVariableByName("gWeights").AsScalar();
			FxSamplesKernel = E.GetVariableByName("gSamplesKernel").AsVector();
			FxEyePosW = E.GetVariableByName("gEyePosW").AsVector();
			FxNoiseSize = E.GetVariableByName("gNoiseSize").AsVector();
			FxSourcePixel = E.GetVariableByName("gSourcePixel").AsVector();
			FxNearFarValue = E.GetVariableByName("gNearFarValue").AsVector();
		}

        public void Dispose() {
			if (E == null) return;
			InputSignaturePT.Dispose();
            LayoutPT.Dispose();
            E.Dispose();
            _b.Dispose();
        }
	}

	public class EffectPpSsaoAlt : IEffectWrapper {
		public static readonly int SampleCount = 24;
		public static readonly int SampleThreshold = 14;
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectReadyTechnique TechSsaoVs;

		public EffectOnlyMatrixVariable FxViewProjInv { get; private set; }
		public EffectOnlyMatrixVariable FxViewProj { get; private set; }
		public EffectOnlyResourceVariable FxDepthMap, FxNormalMap, FxNoiseMap;
		public EffectVectorVariable FxSamplesKernel { get; private set; }
		public EffectVectorVariable FxNoiseSize { get; private set; }

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "PpSsaoAlt");
			E = new Effect(device, _b);

			TechSsaoVs = new EffectReadyTechnique(E.GetTechniqueByName("SsaoVs"));

			for (var i = 0; i < TechSsaoVs.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechSsaoVs.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (PpSsaoAlt, PT, SsaoVs) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);

			FxViewProjInv = new EffectOnlyMatrixVariable(E.GetVariableByName("gViewProjInv").AsMatrix());
			FxViewProj = new EffectOnlyMatrixVariable(E.GetVariableByName("gViewProj").AsMatrix());
			FxDepthMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDepthMap").AsResource());
			FxNormalMap = new EffectOnlyResourceVariable(E.GetVariableByName("gNormalMap").AsResource());
			FxNoiseMap = new EffectOnlyResourceVariable(E.GetVariableByName("gNoiseMap").AsResource());
			FxSamplesKernel = E.GetVariableByName("gSamplesKernel").AsVector();
			FxNoiseSize = E.GetVariableByName("gNoiseSize").AsVector();
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

        public class EffectStructStandartMaterialVariable {
			private readonly EffectVariable _v;

			public EffectStructStandartMaterialVariable(EffectVariable v) {
				_v = v;
			}

			public void Set(StandartMaterial value){
				 SlimDxExtension.SetObject(_v, value, StandartMaterial.Stride);
			}
        }

		[StructLayout(LayoutKind.Sequential)]
        public struct ReflectiveMaterial {
            public float FresnelC;
            public float FresnelExp;
            public float FresnelMaxLevel;

			public static readonly int Stride = Marshal.SizeOf(typeof(ReflectiveMaterial));
        }

        public class EffectStructReflectiveMaterialVariable {
			private readonly EffectVariable _v;

			public EffectStructReflectiveMaterialVariable(EffectVariable v) {
				_v = v;
			}

			public void Set(ReflectiveMaterial value){
				 SlimDxExtension.SetObject(_v, value, ReflectiveMaterial.Stride);
			}
        }

		[StructLayout(LayoutKind.Sequential)]
        public struct MapsMaterial {
            public float DetailsUvMultipler;
            public float DetailsNormalBlend;

			public static readonly int Stride = Marshal.SizeOf(typeof(MapsMaterial));
        }

        public class EffectStructMapsMaterialVariable {
			private readonly EffectVariable _v;

			public EffectStructMapsMaterialVariable(EffectVariable v) {
				_v = v;
			}

			public void Set(MapsMaterial value){
				 SlimDxExtension.SetObject(_v, value, MapsMaterial.Stride);
			}
        }

		[StructLayout(LayoutKind.Sequential)]
        public struct AlphaMaterial {
            public float Alpha;

			public static readonly int Stride = Marshal.SizeOf(typeof(AlphaMaterial));
        }

        public class EffectStructAlphaMaterialVariable {
			private readonly EffectVariable _v;

			public EffectStructAlphaMaterialVariable(EffectVariable v) {
				_v = v;
			}

			public void Set(AlphaMaterial value){
				 SlimDxExtension.SetObject(_v, value, AlphaMaterial.Stride);
			}
        }

		[StructLayout(LayoutKind.Sequential)]
        public struct NmUvMultMaterial {
            public float DiffuseMultipler;
            public float NormalMultipler;

			public static readonly int Stride = Marshal.SizeOf(typeof(NmUvMultMaterial));
        }

        public class EffectStructNmUvMultMaterialVariable {
			private readonly EffectVariable _v;

			public EffectStructNmUvMultMaterialVariable(EffectVariable v) {
				_v = v;
			}

			public void Set(NmUvMultMaterial value){
				 SlimDxExtension.SetObject(_v, value, NmUvMultMaterial.Stride);
			}
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

		public EffectReadyTechnique TechStandard, TechAlpha, TechReflective, TechNm, TechNmUvMult, TechAtNm, TechMaps, TechDiffMaps, TechGl, TechAmbientShadow, TechMirror;

		public EffectOnlyMatrixVariable FxWorld { get; private set; }
		public EffectOnlyMatrixVariable FxWorldInvTranspose { get; private set; }
		public EffectOnlyMatrixVariable FxWorldViewProj { get; private set; }
		public EffectOnlyResourceVariable FxDiffuseMap, FxNormalMap, FxMapsMap, FxDetailsMap, FxDetailsNormalMap;
		public EffectVectorVariable FxEyePosW { get; private set; }
		public EffectStructStandartMaterialVariable FxMaterial { get; private set; }
		public EffectStructReflectiveMaterialVariable FxReflectiveMaterial { get; private set; }
		public EffectStructMapsMaterialVariable FxMapsMaterial { get; private set; }
		public EffectStructAlphaMaterialVariable FxAlphaMaterial { get; private set; }
		public EffectStructNmUvMultMaterialVariable FxNmUvMultMaterial { get; private set; }

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "SimpleMaterial");
			E = new Effect(device, _b);

			TechStandard = new EffectReadyTechnique(E.GetTechniqueByName("Standard"));
			TechAlpha = new EffectReadyTechnique(E.GetTechniqueByName("Alpha"));
			TechReflective = new EffectReadyTechnique(E.GetTechniqueByName("Reflective"));
			TechNm = new EffectReadyTechnique(E.GetTechniqueByName("Nm"));
			TechNmUvMult = new EffectReadyTechnique(E.GetTechniqueByName("NmUvMult"));
			TechAtNm = new EffectReadyTechnique(E.GetTechniqueByName("AtNm"));
			TechMaps = new EffectReadyTechnique(E.GetTechniqueByName("Maps"));
			TechDiffMaps = new EffectReadyTechnique(E.GetTechniqueByName("DiffMaps"));
			TechGl = new EffectReadyTechnique(E.GetTechniqueByName("Gl"));
			TechAmbientShadow = new EffectReadyTechnique(E.GetTechniqueByName("AmbientShadow"));
			TechMirror = new EffectReadyTechnique(E.GetTechniqueByName("Mirror"));

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

			FxWorld = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorld").AsMatrix());
			FxWorldInvTranspose = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldInvTranspose").AsMatrix());
			FxWorldViewProj = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldViewProj").AsMatrix());
			FxDiffuseMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDiffuseMap").AsResource());
			FxNormalMap = new EffectOnlyResourceVariable(E.GetVariableByName("gNormalMap").AsResource());
			FxMapsMap = new EffectOnlyResourceVariable(E.GetVariableByName("gMapsMap").AsResource());
			FxDetailsMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDetailsMap").AsResource());
			FxDetailsNormalMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDetailsNormalMap").AsResource());
			FxEyePosW = E.GetVariableByName("gEyePosW").AsVector();
			FxMaterial = new EffectStructStandartMaterialVariable(E.GetVariableByName("gMaterial"));
			FxReflectiveMaterial = new EffectStructReflectiveMaterialVariable(E.GetVariableByName("gReflectiveMaterial"));
			FxMapsMaterial = new EffectStructMapsMaterialVariable(E.GetVariableByName("gMapsMaterial"));
			FxAlphaMaterial = new EffectStructAlphaMaterialVariable(E.GetVariableByName("gAlphaMaterial"));
			FxNmUvMultMaterial = new EffectStructNmUvMultMaterialVariable(E.GetVariableByName("gNmUvMultMaterial"));
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

		public EffectReadyTechnique TechMain;

		public EffectOnlyMatrixVariable FxWorldViewProj { get; private set; }

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "SpecialDebugLines");
			E = new Effect(device, _b);

			TechMain = new EffectReadyTechnique(E.GetTechniqueByName("Main"));

			for (var i = 0; i < TechMain.Description.PassCount && InputSignaturePC == null; i++) {
				InputSignaturePC = TechMain.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePC == null) throw new System.Exception("input signature (SpecialDebugLines, PC, Main) == null");
			LayoutPC = new InputLayout(device, InputSignaturePC, InputLayouts.VerticePC.InputElementsValue);

			FxWorldViewProj = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldViewProj").AsMatrix());
		}

        public void Dispose() {
			if (E == null) return;
			InputSignaturePC.Dispose();
            LayoutPC.Dispose();
            E.Dispose();
            _b.Dispose();
        }
	}

	public class EffectSpecialPaintShop : IEffectWrapper {
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectReadyTechnique TechFill, TechPattern, TechColorfulPattern, TechFlakes, TechMaps, TechMapsFillGreen, TechTint, TechTintMask, TechMaximum, TechMaximumApply, TechDesaturate;

		public EffectOnlyResourceVariable FxInputMap, FxAoMap, FxMaskMap, FxOverlayMap, FxNoiseMap;
		public EffectScalarVariable FxNoiseMultipler, FxFlakes;
		public EffectVectorVariable FxColor { get; private set; }
		public EffectVectorVariable FxSize { get; private set; }
		public EffectVectorVariable FxColors { get; private set; }

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "SpecialPaintShop");
			E = new Effect(device, _b);

			TechFill = new EffectReadyTechnique(E.GetTechniqueByName("Fill"));
			TechPattern = new EffectReadyTechnique(E.GetTechniqueByName("Pattern"));
			TechColorfulPattern = new EffectReadyTechnique(E.GetTechniqueByName("ColorfulPattern"));
			TechFlakes = new EffectReadyTechnique(E.GetTechniqueByName("Flakes"));
			TechMaps = new EffectReadyTechnique(E.GetTechniqueByName("Maps"));
			TechMapsFillGreen = new EffectReadyTechnique(E.GetTechniqueByName("MapsFillGreen"));
			TechTint = new EffectReadyTechnique(E.GetTechniqueByName("Tint"));
			TechTintMask = new EffectReadyTechnique(E.GetTechniqueByName("TintMask"));
			TechMaximum = new EffectReadyTechnique(E.GetTechniqueByName("Maximum"));
			TechMaximumApply = new EffectReadyTechnique(E.GetTechniqueByName("MaximumApply"));
			TechDesaturate = new EffectReadyTechnique(E.GetTechniqueByName("Desaturate"));

			for (var i = 0; i < TechFill.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechFill.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (SpecialPaintShop, PT, Fill) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);

			FxInputMap = new EffectOnlyResourceVariable(E.GetVariableByName("gInputMap").AsResource());
			FxAoMap = new EffectOnlyResourceVariable(E.GetVariableByName("gAoMap").AsResource());
			FxMaskMap = new EffectOnlyResourceVariable(E.GetVariableByName("gMaskMap").AsResource());
			FxOverlayMap = new EffectOnlyResourceVariable(E.GetVariableByName("gOverlayMap").AsResource());
			FxNoiseMap = new EffectOnlyResourceVariable(E.GetVariableByName("gNoiseMap").AsResource());
			FxNoiseMultipler = E.GetVariableByName("gNoiseMultipler").AsScalar();
			FxFlakes = E.GetVariableByName("gFlakes").AsScalar();
			FxColor = E.GetVariableByName("gColor").AsVector();
			FxSize = E.GetVariableByName("gSize").AsVector();
			FxColors = E.GetVariableByName("gColors").AsVector();
		}

        public void Dispose() {
			if (E == null) return;
			InputSignaturePT.Dispose();
            LayoutPT.Dispose();
            E.Dispose();
            _b.Dispose();
        }
	}

	public class EffectSpecialRandom : IEffectWrapper {
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectReadyTechnique TechMain, TechFlatNormalMap;


		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "SpecialRandom");
			E = new Effect(device, _b);

			TechMain = new EffectReadyTechnique(E.GetTechniqueByName("Main"));
			TechFlatNormalMap = new EffectReadyTechnique(E.GetTechniqueByName("FlatNormalMap"));

			for (var i = 0; i < TechMain.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechMain.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (SpecialRandom, PT, Main) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);

		}

        public void Dispose() {
			if (E == null) return;
			InputSignaturePT.Dispose();
            LayoutPT.Dispose();
            E.Dispose();
            _b.Dispose();
        }
	}

	public class EffectSpecialShadow : IEffectWrapper, IEffectMatricesWrapper {
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT, InputSignaturePNTG;
        public InputLayout LayoutPT, LayoutPNTG;

		public EffectReadyTechnique TechHorizontalShadowBlur, TechVerticalShadowBlur, TechAmbientShadow, TechResult, TechSimplest, TechAo, TechAoResult, TechAoGrow;

		public EffectOnlyMatrixVariable FxShadowViewProj { get; private set; }
		public EffectOnlyMatrixVariable FxWorldViewProj { get; private set; }
		public EffectOnlyMatrixVariable FxWorld { get; private set; }
		public EffectOnlyMatrixVariable FxWorldInvTranspose { get; private set; }
		public EffectOnlyResourceVariable FxInputMap, FxDepthMap, FxAlphaMap, FxNormalMap;
		public EffectScalarVariable FxMultipler, FxGamma, FxCount, FxAmbient, FxPadding, FxFade, FxAlphaRef, FxNormalUvMult;
		public EffectVectorVariable FxSize { get; private set; }
		public EffectVectorVariable FxShadowSize { get; private set; }
		public EffectVectorVariable FxLightDir { get; private set; }

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "SpecialShadow");
			E = new Effect(device, _b);

			TechHorizontalShadowBlur = new EffectReadyTechnique(E.GetTechniqueByName("HorizontalShadowBlur"));
			TechVerticalShadowBlur = new EffectReadyTechnique(E.GetTechniqueByName("VerticalShadowBlur"));
			TechAmbientShadow = new EffectReadyTechnique(E.GetTechniqueByName("AmbientShadow"));
			TechResult = new EffectReadyTechnique(E.GetTechniqueByName("Result"));
			TechSimplest = new EffectReadyTechnique(E.GetTechniqueByName("Simplest"));
			TechAo = new EffectReadyTechnique(E.GetTechniqueByName("Ao"));
			TechAoResult = new EffectReadyTechnique(E.GetTechniqueByName("AoResult"));
			TechAoGrow = new EffectReadyTechnique(E.GetTechniqueByName("AoGrow"));

			for (var i = 0; i < TechHorizontalShadowBlur.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechHorizontalShadowBlur.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (SpecialShadow, PT, HorizontalShadowBlur) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);
			for (var i = 0; i < TechAo.Description.PassCount && InputSignaturePNTG == null; i++) {
				InputSignaturePNTG = TechAo.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePNTG == null) throw new System.Exception("input signature (SpecialShadow, PNTG, Ao) == null");
			LayoutPNTG = new InputLayout(device, InputSignaturePNTG, InputLayouts.VerticePNTG.InputElementsValue);

			FxShadowViewProj = new EffectOnlyMatrixVariable(E.GetVariableByName("gShadowViewProj").AsMatrix());
			FxWorldViewProj = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldViewProj").AsMatrix());
			FxWorld = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorld").AsMatrix());
			FxWorldInvTranspose = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldInvTranspose").AsMatrix());
			FxInputMap = new EffectOnlyResourceVariable(E.GetVariableByName("gInputMap").AsResource());
			FxDepthMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDepthMap").AsResource());
			FxAlphaMap = new EffectOnlyResourceVariable(E.GetVariableByName("gAlphaMap").AsResource());
			FxNormalMap = new EffectOnlyResourceVariable(E.GetVariableByName("gNormalMap").AsResource());
			FxMultipler = E.GetVariableByName("gMultipler").AsScalar();
			FxGamma = E.GetVariableByName("gGamma").AsScalar();
			FxCount = E.GetVariableByName("gCount").AsScalar();
			FxAmbient = E.GetVariableByName("gAmbient").AsScalar();
			FxPadding = E.GetVariableByName("gPadding").AsScalar();
			FxFade = E.GetVariableByName("gFade").AsScalar();
			FxAlphaRef = E.GetVariableByName("gAlphaRef").AsScalar();
			FxNormalUvMult = E.GetVariableByName("gNormalUvMult").AsScalar();
			FxSize = E.GetVariableByName("gSize").AsVector();
			FxShadowSize = E.GetVariableByName("gShadowSize").AsVector();
			FxLightDir = E.GetVariableByName("gLightDir").AsVector();
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

	public class EffectSpecialTrackMap : IEffectWrapper, IEffectScreenSizeWrapper {
		public static readonly int Gblurradius = 3;
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignatureP, InputSignaturePT;
        public InputLayout LayoutP, LayoutPT;

		public EffectReadyTechnique TechMain, TechPp, TechFinal, TechFinalCheckers, TechPpHorizontalBlur, TechPpVerticalBlur;

		public EffectOnlyMatrixVariable FxWorldViewProj { get; private set; }
		public EffectOnlyResourceVariable FxInputMap;
		public EffectVectorVariable FxScreenSize { get; private set; }

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "SpecialTrackMap");
			E = new Effect(device, _b);

			TechMain = new EffectReadyTechnique(E.GetTechniqueByName("Main"));
			TechPp = new EffectReadyTechnique(E.GetTechniqueByName("Pp"));
			TechFinal = new EffectReadyTechnique(E.GetTechniqueByName("Final"));
			TechFinalCheckers = new EffectReadyTechnique(E.GetTechniqueByName("FinalCheckers"));
			TechPpHorizontalBlur = new EffectReadyTechnique(E.GetTechniqueByName("PpHorizontalBlur"));
			TechPpVerticalBlur = new EffectReadyTechnique(E.GetTechniqueByName("PpVerticalBlur"));

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

			FxWorldViewProj = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldViewProj").AsMatrix());
			FxInputMap = new EffectOnlyResourceVariable(E.GetVariableByName("gInputMap").AsResource());
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

	public class EffectSpecialTrackOutline : IEffectWrapper, IEffectScreenSizeWrapper {
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT, InputSignatureP;
        public InputLayout LayoutPT, LayoutP;

		public EffectReadyTechnique TechFirstStep, TechExtraWidth, TechShadow, TechCombine, TechBlend, TechFinal, TechFinalBg, TechFinalCheckers, TechFirstStepObj;

		public EffectOnlyMatrixVariable FxMatrix { get; private set; }
		public EffectOnlyMatrixVariable FxWorldViewProj { get; private set; }
		public EffectOnlyResourceVariable FxInputMap, FxBgMap;
		public EffectScalarVariable FxExtraWidth, FxDropShadowRadius;
		public EffectVectorVariable FxScreenSize { get; private set; }
		public EffectVectorVariable FxBlendColor { get; private set; }

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "SpecialTrackOutline");
			E = new Effect(device, _b);

			TechFirstStep = new EffectReadyTechnique(E.GetTechniqueByName("FirstStep"));
			TechExtraWidth = new EffectReadyTechnique(E.GetTechniqueByName("ExtraWidth"));
			TechShadow = new EffectReadyTechnique(E.GetTechniqueByName("Shadow"));
			TechCombine = new EffectReadyTechnique(E.GetTechniqueByName("Combine"));
			TechBlend = new EffectReadyTechnique(E.GetTechniqueByName("Blend"));
			TechFinal = new EffectReadyTechnique(E.GetTechniqueByName("Final"));
			TechFinalBg = new EffectReadyTechnique(E.GetTechniqueByName("FinalBg"));
			TechFinalCheckers = new EffectReadyTechnique(E.GetTechniqueByName("FinalCheckers"));
			TechFirstStepObj = new EffectReadyTechnique(E.GetTechniqueByName("FirstStepObj"));

			for (var i = 0; i < TechExtraWidth.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechExtraWidth.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (SpecialTrackOutline, PT, ExtraWidth) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);
			for (var i = 0; i < TechFirstStepObj.Description.PassCount && InputSignatureP == null; i++) {
				InputSignatureP = TechFirstStepObj.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignatureP == null) throw new System.Exception("input signature (SpecialTrackOutline, P, FirstStepObj) == null");
			LayoutP = new InputLayout(device, InputSignatureP, InputLayouts.VerticeP.InputElementsValue);

			FxMatrix = new EffectOnlyMatrixVariable(E.GetVariableByName("gMatrix").AsMatrix());
			FxWorldViewProj = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldViewProj").AsMatrix());
			FxInputMap = new EffectOnlyResourceVariable(E.GetVariableByName("gInputMap").AsResource());
			FxBgMap = new EffectOnlyResourceVariable(E.GetVariableByName("gBgMap").AsResource());
			FxExtraWidth = E.GetVariableByName("gExtraWidth").AsScalar();
			FxDropShadowRadius = E.GetVariableByName("gDropShadowRadius").AsScalar();
			FxScreenSize = E.GetVariableByName("gScreenSize").AsVector();
			FxBlendColor = E.GetVariableByName("gBlendColor").AsVector();
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

	public class EffectSpecialUv : IEffectWrapper {
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePNTG;
        public InputLayout LayoutPNTG;

		public EffectReadyTechnique TechMain;

		public EffectVectorVariable FxOffset { get; private set; }

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "SpecialUv");
			E = new Effect(device, _b);

			TechMain = new EffectReadyTechnique(E.GetTechniqueByName("Main"));

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

		public EffectReadyTechnique TechRender;

		public EffectOnlyResourceVariable FxTex;

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "SpriteShader");
			E = new Effect(device, _b);

			TechRender = new EffectReadyTechnique(E.GetTechniqueByName("Render"));

			for (var i = 0; i < TechRender.Description.PassCount && InputSignatureSpriteSpecific == null; i++) {
				InputSignatureSpriteSpecific = TechRender.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignatureSpriteSpecific == null) throw new System.Exception("input signature (SpriteShader, SpriteSpecific, Render) == null");
			LayoutSpriteSpecific = new InputLayout(device, InputSignatureSpriteSpecific, Base.Sprites.VerticeSpriteSpecific.InputElementsValue);

			FxTex = new EffectOnlyResourceVariable(E.GetVariableByName("Tex").AsResource());
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

		public EffectReadyTechnique TechCube;

		public EffectOnlyMatrixVariable FxWorldViewProj { get; private set; }

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "TestingCube");
			E = new Effect(device, _b);

			TechCube = new EffectReadyTechnique(E.GetTechniqueByName("Cube"));

			for (var i = 0; i < TechCube.Description.PassCount && InputSignaturePC == null; i++) {
				InputSignaturePC = TechCube.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePC == null) throw new System.Exception("input signature (TestingCube, PC, Cube) == null");
			LayoutPC = new InputLayout(device, InputSignaturePC, InputLayouts.VerticePC.InputElementsValue);

			FxWorldViewProj = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldViewProj").AsMatrix());
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

		public EffectReadyTechnique TechCube;

		public EffectOnlyMatrixVariable FxWorldViewProj { get; private set; }

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "TestingPnt");
			E = new Effect(device, _b);

			TechCube = new EffectReadyTechnique(E.GetTechniqueByName("Cube"));

			for (var i = 0; i < TechCube.Description.PassCount && InputSignaturePNT == null; i++) {
				InputSignaturePNT = TechCube.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePNT == null) throw new System.Exception("input signature (TestingPnt, PNT, Cube) == null");
			LayoutPNT = new InputLayout(device, InputSignaturePNT, InputLayouts.VerticePNT.InputElementsValue);

			FxWorldViewProj = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldViewProj").AsMatrix());
		}

        public void Dispose() {
			if (E == null) return;
			InputSignaturePNT.Dispose();
            LayoutPNT.Dispose();
            E.Dispose();
            _b.Dispose();
        }
	}

}
