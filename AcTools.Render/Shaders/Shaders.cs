/* GENERATED AUTOMATICALLY */
/* DON’T MODIFY */

using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Base.Structs;
using AcTools.Render.Base.Utils;
using AcTools.Utils.Helpers;
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
		public enum Mode { Main, NoPCSS, NoShadows, Simple, SimpleNoPCSS, SimpleNoShadows }
			
		[StructLayout(LayoutKind.Sequential)]
        public struct Light {
            public Vector3 PosW;
            public float Range;
            public Vector3 DirectionW;
            public float SpotlightCosMin;
            public Vector3 Color;
            public float SpotlightCosMax;
            public uint Type;
            public uint ShadowMode;
            public Vector2 Padding;

			public static readonly int Stride = Marshal.SizeOf(typeof(Light));
        }

		public class EffectStructLightArrayVariable {
			private readonly EffectVariable _v;

			public EffectStructLightArrayVariable(EffectVariable v) {
				_v = v;
			}

			public void SetArray(Light[] value){
				 SlimDxExtension.SetArray(_v, value, Light.Stride);
			}
        }
		
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
            public float DetailsUvMultiplier;
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
            public float DiffuseMultiplier;
            public float NormalMultiplier;

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
		
		public const uint LightOff = 0;
		public const uint LightPoint = 1;
		public const uint LightSpot = 2;
		public const uint LightDirectional = 3;
		public const uint LightShadowOff = 0;
		public const uint LightShadowMain = 1;
		public const uint LightShadowExtra = 100;
		public const uint LightShadowExtraFast = 200;
		public const uint LightShadowExtraCube = 300;
		public const uint HasNormalMap = 1;
		public const uint UseNormalAlphaAsAlpha = 64;
		public const uint AlphaTest = 128;
		public const uint IsAdditive = 16;
		public const uint HasDetailsMap = 4;
		public const uint IsCarpaint = 32;
		public const int MaxLighsAmount = 30;
		public const int MaxExtraShadows = 5;
		public const int MaxExtraShadowsSmooth = 1;
		public const int ComplexLighting = 1;
		public const int MaxNumSplits = 3;
		public const int MaxBones = 64;
		public const bool EnableShadows = true;
		public const bool EnablePcss = true;
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT, InputSignaturePNTG, InputSignaturePNTGW4B;
        public InputLayout LayoutPT, LayoutPNTG, LayoutPNTGW4B;

		public EffectReadyTechnique TechGPass_Standard, TechGPass_Alpha, TechGPass_Reflective, TechGPass_Nm, TechGPass_NmUvMult, TechGPass_AtNm, TechGPass_Maps, TechGPass_SkinnedMaps, TechGPass_DiffMaps, TechGPass_Gl, TechGPass_SkinnedGl, TechGPass_FlatMirror, TechGPass_Debug, TechGPass_SkinnedDebug, TechStandard, TechSky, TechAlpha, TechReflective, TechNm, TechNmUvMult, TechAtNm, TechMaps, TechSkinnedMaps, TechDiffMaps, TechGl, TechSkinnedGl, TechWindscreen, TechCollider, TechDebug, TechSkinnedDebug, TechDepthOnly, TechSkinnedDepthOnly, TechAmbientShadow, TechMirror, TechFlatMirror, TechFlatTextureMirror, TechFlatBackgroundGround, TechFlatAmbientGround;

		public EffectOnlyMatrixVariable FxWorld, FxWorldInvTranspose, FxWorldViewProj;
		public EffectOnlyMatrixArrayVariable FxExtraShadowViewProj, FxShadowViewProj, FxBoneTransforms;
		public EffectOnlyResourceVariable FxReflectionCubemap, FxNoiseMap, FxDiffuseMap, FxNormalMap, FxMapsMap, FxDetailsMap, FxDetailsNormalMap, FxAoMap;
		public EffectOnlyResourceArrayVariable FxExtraShadowMaps, FxExtraShadowCubeMaps, FxShadowMaps;
		public EffectScalarVariable FxGPassTransparent, FxGPassAlphaThreshold, FxExtraShadowMapSize, FxNumSplits, FxPcssEnabled, FxFlatMirrored, FxReflectionPower, FxUseAo, FxCubemapReflections, FxCubemapAmbient, FxFlatMirrorPower;
		public EffectVectorVariable FxLightDir, FxLightColor, FxExtraShadowNearFar, FxPcssScale, FxShadowMapSize, FxEyePosW, FxAmbientDown, FxAmbientRange, FxBackgroundColor, FxScreenSize;
		public EffectStructStandartMaterialVariable FxMaterial;
		public EffectStructReflectiveMaterialVariable FxReflectiveMaterial;
		public EffectStructMapsMaterialVariable FxMapsMaterial;
		public EffectStructAlphaMaterialVariable FxAlphaMaterial;
		public EffectStructNmUvMultMaterialVariable FxNmUvMultMaterial;
		public EffectStructLightArrayVariable FxLights;

		EffectVectorVariable IEffectScreenSizeWrapper.FxScreenSize => FxScreenSize;

		EffectOnlyMatrixVariable IEffectMatricesWrapper.FxWorld => FxWorld;
		EffectOnlyMatrixVariable IEffectMatricesWrapper.FxWorldInvTranspose => FxWorldInvTranspose;
		EffectOnlyMatrixVariable IEffectMatricesWrapper.FxWorldViewProj => FxWorldViewProj;
		
		private Mode _mode = Mode.Main;

		public Mode GetMode(){
			return _mode;
		}

		public void SetMode(Mode mode, Device device){
			if (mode == _mode) return;
			_mode = mode;
			if (_b != null) {
				Dispose();
				_b = null;
				Initialize(device);
			}
		}
		
		public void Initialize(Device device) {
			if (_b != null) return;

			_b = EffectUtils.Load(ShadersResourceManager.Manager, _mode == Mode.Main ? "DarkMaterial" : "DarkMaterial." + _mode);
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

			for (var i = 0; i < TechAmbientShadow.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechAmbientShadow.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (DarkMaterial, PT, AmbientShadow) == null");
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
			FxExtraShadowViewProj = new EffectOnlyMatrixArrayVariable(E.GetVariableByName("gExtraShadowViewProj").AsMatrix());
			FxShadowViewProj = new EffectOnlyMatrixArrayVariable(E.GetVariableByName("gShadowViewProj").AsMatrix());
			FxBoneTransforms = new EffectOnlyMatrixArrayVariable(E.GetVariableByName("gBoneTransforms").AsMatrix());
			FxReflectionCubemap = new EffectOnlyResourceVariable(E.GetVariableByName("gReflectionCubemap").AsResource());
			FxNoiseMap = new EffectOnlyResourceVariable(E.GetVariableByName("gNoiseMap").AsResource());
			FxDiffuseMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDiffuseMap").AsResource());
			FxNormalMap = new EffectOnlyResourceVariable(E.GetVariableByName("gNormalMap").AsResource());
			FxMapsMap = new EffectOnlyResourceVariable(E.GetVariableByName("gMapsMap").AsResource());
			FxDetailsMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDetailsMap").AsResource());
			FxDetailsNormalMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDetailsNormalMap").AsResource());
			FxAoMap = new EffectOnlyResourceVariable(E.GetVariableByName("gAoMap").AsResource());
			FxExtraShadowMaps = new EffectOnlyResourceArrayVariable(E.GetVariableByName("gExtraShadowMaps").AsResource());
			FxExtraShadowCubeMaps = new EffectOnlyResourceArrayVariable(E.GetVariableByName("gExtraShadowCubeMaps").AsResource());
			FxShadowMaps = new EffectOnlyResourceArrayVariable(E.GetVariableByName("gShadowMaps").AsResource());
			FxGPassTransparent = E.GetVariableByName("gGPassTransparent").AsScalar();
			FxGPassAlphaThreshold = E.GetVariableByName("gGPassAlphaThreshold").AsScalar();
			FxExtraShadowMapSize = E.GetVariableByName("gExtraShadowMapSize").AsScalar();
			FxNumSplits = E.GetVariableByName("gNumSplits").AsScalar();
			FxPcssEnabled = E.GetVariableByName("gPcssEnabled").AsScalar();
			FxFlatMirrored = E.GetVariableByName("gFlatMirrored").AsScalar();
			FxReflectionPower = E.GetVariableByName("gReflectionPower").AsScalar();
			FxUseAo = E.GetVariableByName("gUseAo").AsScalar();
			FxCubemapReflections = E.GetVariableByName("gCubemapReflections").AsScalar();
			FxCubemapAmbient = E.GetVariableByName("gCubemapAmbient").AsScalar();
			FxFlatMirrorPower = E.GetVariableByName("gFlatMirrorPower").AsScalar();
			FxLightDir = E.GetVariableByName("gLightDir").AsVector();
			FxLightColor = E.GetVariableByName("gLightColor").AsVector();
			FxExtraShadowNearFar = E.GetVariableByName("gExtraShadowNearFar").AsVector();
			FxPcssScale = E.GetVariableByName("gPcssScale").AsVector();
			FxShadowMapSize = E.GetVariableByName("gShadowMapSize").AsVector();
			FxEyePosW = E.GetVariableByName("gEyePosW").AsVector();
			FxAmbientDown = E.GetVariableByName("gAmbientDown").AsVector();
			FxAmbientRange = E.GetVariableByName("gAmbientRange").AsVector();
			FxBackgroundColor = E.GetVariableByName("gBackgroundColor").AsVector();
			FxScreenSize = E.GetVariableByName("gScreenSize").AsVector();
			FxMaterial = new EffectStructStandartMaterialVariable(E.GetVariableByName("gMaterial"));
			FxReflectiveMaterial = new EffectStructReflectiveMaterialVariable(E.GetVariableByName("gReflectiveMaterial"));
			FxMapsMaterial = new EffectStructMapsMaterialVariable(E.GetVariableByName("gMapsMaterial"));
			FxAlphaMaterial = new EffectStructAlphaMaterialVariable(E.GetVariableByName("gAlphaMaterial"));
			FxNmUvMultMaterial = new EffectStructNmUvMultMaterialVariable(E.GetVariableByName("gNmUvMultMaterial"));
			FxLights = new EffectStructLightArrayVariable(E.GetVariableByName("gLights"));
		}

        public void Dispose() {
			if (E == null) return;
			DisposeHelper.Dispose(ref InputSignaturePT);
			DisposeHelper.Dispose(ref LayoutPT);
			DisposeHelper.Dispose(ref InputSignaturePNTG);
			DisposeHelper.Dispose(ref LayoutPNTG);
			DisposeHelper.Dispose(ref InputSignaturePNTGW4B);
			DisposeHelper.Dispose(ref LayoutPNTGW4B);
			DisposeHelper.Dispose(ref E);
			DisposeHelper.Dispose(ref _b);
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

		public EffectOnlyMatrixVariable FxWorld, FxWorldInvTranspose, FxWorldViewProj;
		public EffectOnlyResourceVariable FxDiffuseMap;
		public EffectVectorVariable FxEyePosW;
		public EffectStructMaterialVariable FxMaterial;

		EffectOnlyMatrixVariable IEffectMatricesWrapper.FxWorld => FxWorld;
		EffectOnlyMatrixVariable IEffectMatricesWrapper.FxWorldInvTranspose => FxWorldInvTranspose;
		EffectOnlyMatrixVariable IEffectMatricesWrapper.FxWorldViewProj => FxWorldViewProj;

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
			DisposeHelper.Dispose(ref InputSignaturePNT);
			DisposeHelper.Dispose(ref LayoutPNT);
			DisposeHelper.Dispose(ref E);
			DisposeHelper.Dispose(ref _b);
        }
	}

	public class EffectPpBasic : IEffectWrapper, IEffectScreenSizeWrapper {
		public const int FxaaPreset = 5;
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectReadyTechnique TechCopy, TechCut, TechCopyNoAlpha, TechAccumulate, TechAccumulateDivide, TechAccumulateBokehDivide, TechOverlay, TechDepthToLinear, TechShadow, TechDepth, TechFxaa;

		public EffectOnlyResourceVariable FxInputMap, FxOverlayMap, FxDepthMap;
		public EffectScalarVariable FxSizeMultipler, FxMultipler;
		public EffectVectorVariable FxScreenSize, FxBokenMultipler;

		EffectVectorVariable IEffectScreenSizeWrapper.FxScreenSize => FxScreenSize;

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "PpBasic");
			E = new Effect(device, _b);

			TechCopy = new EffectReadyTechnique(E.GetTechniqueByName("Copy"));
			TechCut = new EffectReadyTechnique(E.GetTechniqueByName("Cut"));
			TechCopyNoAlpha = new EffectReadyTechnique(E.GetTechniqueByName("CopyNoAlpha"));
			TechAccumulate = new EffectReadyTechnique(E.GetTechniqueByName("Accumulate"));
			TechAccumulateDivide = new EffectReadyTechnique(E.GetTechniqueByName("AccumulateDivide"));
			TechAccumulateBokehDivide = new EffectReadyTechnique(E.GetTechniqueByName("AccumulateBokehDivide"));
			TechOverlay = new EffectReadyTechnique(E.GetTechniqueByName("Overlay"));
			TechDepthToLinear = new EffectReadyTechnique(E.GetTechniqueByName("DepthToLinear"));
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
			FxMultipler = E.GetVariableByName("gMultipler").AsScalar();
			FxScreenSize = E.GetVariableByName("gScreenSize").AsVector();
			FxBokenMultipler = E.GetVariableByName("gBokenMultipler").AsVector();
		}

        public void Dispose() {
			if (E == null) return;
			DisposeHelper.Dispose(ref InputSignaturePT);
			DisposeHelper.Dispose(ref LayoutPT);
			DisposeHelper.Dispose(ref E);
			DisposeHelper.Dispose(ref _b);
        }
	}

	public class EffectPpBlur : IEffectWrapper, IEffectScreenSizeWrapper {
		public const int SampleCount = 15;
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectReadyTechnique TechGaussianBlur, TechFlatMirrorBlur, TechDarkSslrBlur0, TechReflectionGaussianBlur;

		public EffectOnlyMatrixVariable FxWorldViewProjInv;
		public EffectOnlyResourceVariable FxInputMap, FxFlatMirrorDepthMap, FxFlatMirrorNormalsMap, FxMapsMap;
		public EffectScalarVariable FxSampleWeights, FxPower;
		public EffectVectorVariable FxSampleOffsets, FxScreenSize;

		EffectVectorVariable IEffectScreenSizeWrapper.FxScreenSize => FxScreenSize;

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
			DisposeHelper.Dispose(ref InputSignaturePT);
			DisposeHelper.Dispose(ref LayoutPT);
			DisposeHelper.Dispose(ref E);
			DisposeHelper.Dispose(ref _b);
        }
	}

	public class EffectPpDarkSslr : IEffectWrapper {
		public const int Iterations = 30;
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectReadyTechnique TechDownscale4, TechSslr, TechFinalStep;

		public EffectOnlyMatrixVariable FxCameraProjInv, FxCameraProj, FxWorldViewProjInv, FxWorldViewProj;
		public EffectOnlyResourceVariable FxDiffuseMap, FxDepthMap, FxBaseReflectionMap, FxNormalMap, FxNoiseMap, FxFirstStepMap, FxDepthMapDown, FxDepthMapDownMore;
		public EffectScalarVariable FxStartFrom, FxFixMultiplier, FxOffset, FxGlowFix, FxDistanceThreshold;
		public EffectVectorVariable FxEyePosW, FxSize;

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
			DisposeHelper.Dispose(ref InputSignaturePT);
			DisposeHelper.Dispose(ref LayoutPT);
			DisposeHelper.Dispose(ref E);
			DisposeHelper.Dispose(ref _b);
        }
	}

	public class EffectPpDof : IEffectWrapper, IEffectScreenSizeWrapper {
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectReadyTechnique TechDownsampleColorCoC, TechBokehSprite, TechResolveBokeh;

		public EffectOnlyResourceVariable FxInputTexture, FxInputTextureBokenBase, FxInputTextureDepth, FxInputTextureBokeh, FxInputTextureDownscaledColor;
		public EffectScalarVariable FxZNear, FxZFar, FxFocusPlane, FxDofCoCScale, FxDebugBokeh, FxCoCLimit;
		public EffectVectorVariable FxScreenSize, FxScreenSizeHalfRes, FxCocScaleBias;

		EffectVectorVariable IEffectScreenSizeWrapper.FxScreenSize => FxScreenSize;

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "PpDof");
			E = new Effect(device, _b);

			TechDownsampleColorCoC = new EffectReadyTechnique(E.GetTechniqueByName("DownsampleColorCoC"));
			TechBokehSprite = new EffectReadyTechnique(E.GetTechniqueByName("BokehSprite"));
			TechResolveBokeh = new EffectReadyTechnique(E.GetTechniqueByName("ResolveBokeh"));

			for (var i = 0; i < TechDownsampleColorCoC.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechDownsampleColorCoC.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (PpDof, PT, DownsampleColorCoC) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);

			FxInputTexture = new EffectOnlyResourceVariable(E.GetVariableByName("InputTexture").AsResource());
			FxInputTextureBokenBase = new EffectOnlyResourceVariable(E.GetVariableByName("InputTextureBokenBase").AsResource());
			FxInputTextureDepth = new EffectOnlyResourceVariable(E.GetVariableByName("InputTextureDepth").AsResource());
			FxInputTextureBokeh = new EffectOnlyResourceVariable(E.GetVariableByName("InputTextureBokeh").AsResource());
			FxInputTextureDownscaledColor = new EffectOnlyResourceVariable(E.GetVariableByName("InputTextureDownscaledColor").AsResource());
			FxZNear = E.GetVariableByName("gZNear").AsScalar();
			FxZFar = E.GetVariableByName("gZFar").AsScalar();
			FxFocusPlane = E.GetVariableByName("gFocusPlane").AsScalar();
			FxDofCoCScale = E.GetVariableByName("gDofCoCScale").AsScalar();
			FxDebugBokeh = E.GetVariableByName("gDebugBokeh").AsScalar();
			FxCoCLimit = E.GetVariableByName("gCoCLimit").AsScalar();
			FxScreenSize = E.GetVariableByName("gScreenSize").AsVector();
			FxScreenSizeHalfRes = E.GetVariableByName("gScreenSizeHalfRes").AsVector();
			FxCocScaleBias = E.GetVariableByName("gCocScaleBias").AsVector();
		}

        public void Dispose() {
			if (E == null) return;
			DisposeHelper.Dispose(ref InputSignaturePT);
			DisposeHelper.Dispose(ref LayoutPT);
			DisposeHelper.Dispose(ref E);
			DisposeHelper.Dispose(ref _b);
        }
	}

	public class EffectPpDownsample : IEffectWrapper, IEffectScreenSizeWrapper {
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectReadyTechnique TechCopy, TechAverage, TechAnisotropic;

		public EffectOnlyResourceVariable FxInputMap;
		public EffectVectorVariable FxScreenSize, FxMultipler;

		EffectVectorVariable IEffectScreenSizeWrapper.FxScreenSize => FxScreenSize;

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
			DisposeHelper.Dispose(ref InputSignaturePT);
			DisposeHelper.Dispose(ref LayoutPT);
			DisposeHelper.Dispose(ref E);
			DisposeHelper.Dispose(ref _b);
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
		public EffectVectorVariable FxPixel, FxCropImage, FxParams;

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
			DisposeHelper.Dispose(ref InputSignaturePT);
			DisposeHelper.Dispose(ref LayoutPT);
			DisposeHelper.Dispose(ref E);
			DisposeHelper.Dispose(ref _b);
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
			DisposeHelper.Dispose(ref InputSignaturePT);
			DisposeHelper.Dispose(ref LayoutPT);
			DisposeHelper.Dispose(ref E);
			DisposeHelper.Dispose(ref _b);
        }
	}

	public class EffectPpOutline : IEffectWrapper, IEffectScreenSizeWrapper {
		public const float Threshold = 0.99999f;
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectReadyTechnique TechOutline;

		public EffectOnlyResourceVariable FxInputMap, FxDepthMap;
		public EffectVectorVariable FxScreenSize;

		EffectVectorVariable IEffectScreenSizeWrapper.FxScreenSize => FxScreenSize;

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
			DisposeHelper.Dispose(ref InputSignaturePT);
			DisposeHelper.Dispose(ref LayoutPT);
			DisposeHelper.Dispose(ref E);
			DisposeHelper.Dispose(ref _b);
        }
	}

	public class EffectPpAmbientShadows : IEffectWrapper {
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectReadyTechnique TechAddShadow, TechAddShadowBlur;

		public EffectOnlyMatrixVariable FxViewProjInv, FxViewProj, FxShadowViewProj;
		public EffectOnlyResourceVariable FxDepthMap, FxShadowMap, FxNoiseMap;
		public EffectVectorVariable FxShadowPosition, FxNoiseSize, FxShadowSize;

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "PpAmbientShadows");
			E = new Effect(device, _b);

			TechAddShadow = new EffectReadyTechnique(E.GetTechniqueByName("AddShadow"));
			TechAddShadowBlur = new EffectReadyTechnique(E.GetTechniqueByName("AddShadowBlur"));

			for (var i = 0; i < TechAddShadow.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechAddShadow.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (PpAmbientShadows, PT, AddShadow) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);

			FxViewProjInv = new EffectOnlyMatrixVariable(E.GetVariableByName("gViewProjInv").AsMatrix());
			FxViewProj = new EffectOnlyMatrixVariable(E.GetVariableByName("gViewProj").AsMatrix());
			FxShadowViewProj = new EffectOnlyMatrixVariable(E.GetVariableByName("gShadowViewProj").AsMatrix());
			FxDepthMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDepthMap").AsResource());
			FxShadowMap = new EffectOnlyResourceVariable(E.GetVariableByName("gShadowMap").AsResource());
			FxNoiseMap = new EffectOnlyResourceVariable(E.GetVariableByName("gNoiseMap").AsResource());
			FxShadowPosition = E.GetVariableByName("gShadowPosition").AsVector();
			FxNoiseSize = E.GetVariableByName("gNoiseSize").AsVector();
			FxShadowSize = E.GetVariableByName("gShadowSize").AsVector();
		}

        public void Dispose() {
			if (E == null) return;
			DisposeHelper.Dispose(ref InputSignaturePT);
			DisposeHelper.Dispose(ref LayoutPT);
			DisposeHelper.Dispose(ref E);
			DisposeHelper.Dispose(ref _b);
        }
	}

	public class EffectPpAoBlur : IEffectWrapper {
		public const int MaxBlurRadius = 4;
		public const int BlurRadius = 4;
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectReadyTechnique TechBlurH, TechBlurV;

		public EffectOnlyResourceVariable FxDepthMap, FxNormalMap, FxFirstStepMap;
		public EffectScalarVariable FxWeights;
		public EffectVectorVariable FxSourcePixel, FxNearFarValue;

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
			DisposeHelper.Dispose(ref InputSignaturePT);
			DisposeHelper.Dispose(ref LayoutPT);
			DisposeHelper.Dispose(ref E);
			DisposeHelper.Dispose(ref _b);
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
		
		public const int SsaoAdaptiveTapBaseCount = 5;
		public const int SsaoAdaptiveTapFlexibleCount = 5;
		public const int SsaoMaxTaps = 12;
		public const int SampleCount = 16;
		public const int SsaoEnableNormalWorldToViewConversion = 1;
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectReadyTechnique TechPrepareDepth, TechAssao;

		public EffectOnlyMatrixVariable FxWorldViewProjInv, FxWorldViewProj, FxProj, FxProjInv, FxNormalsToViewSpace;
		public EffectOnlyResourceVariable Fx_DepthSource, Fx_NormalmapSource, Fx_ViewspaceDepthSource, Fx_ViewspaceDepthSource1, Fx_ViewspaceDepthSource2, Fx_ViewspaceDepthSource3, Fx_ImportanceMap, Fx_LoadCounter, Fx_BlurInput, FxDepthMap, FxNormalMap, FxNoiseMap, FxDitherMap, FxFirstStepMap;
		public EffectVectorVariable FxViewFrustumVectors;
		public EffectStructASSAOConstantsVariable Fx_ASSAOConsts;

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
			DisposeHelper.Dispose(ref InputSignaturePT);
			DisposeHelper.Dispose(ref LayoutPT);
			DisposeHelper.Dispose(ref E);
			DisposeHelper.Dispose(ref _b);
        }
	}

	public class EffectPpHbao : IEffectWrapper {
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectReadyTechnique TechHbao;

		public EffectOnlyMatrixVariable FxWorldViewProjInv, FxView, FxProj, FxProjT, FxProjInv, FxNormalsToViewSpace, FxProjectionMatrix;
		public EffectOnlyResourceVariable FxDepthMap, FxNormalMap, FxNoiseMap, FxDitherMap, FxFirstStepMap;
		public EffectScalarVariable FxDitherScale;
		public EffectVectorVariable FxViewFrustumVectors, FxRenderTargetResolution, FxSampleDirections;

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
			DisposeHelper.Dispose(ref InputSignaturePT);
			DisposeHelper.Dispose(ref LayoutPT);
			DisposeHelper.Dispose(ref E);
			DisposeHelper.Dispose(ref _b);
        }
	}

	public class EffectPpSsao : IEffectWrapper {
		public const int SampleCount = 16;
		public const float Radius = 0.15f;
		public const float NormalBias = 0.01f;
		public const int MaxBlurRadius = 4;
		public const int BlurRadius = 4;
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectReadyTechnique TechSsao, TechBlurH, TechBlurV;

		public EffectOnlyMatrixVariable FxCameraProjInv, FxCameraProj, FxWorldViewProjInv, FxWorldViewProj;
		public EffectOnlyResourceVariable FxDepthMap, FxNormalMap, FxNoiseMap, FxFirstStepMap;
		public EffectScalarVariable FxAoPower, FxWeights;
		public EffectVectorVariable FxSamplesKernel, FxEyePosW, FxNoiseSize, FxSourcePixel, FxNearFarValue;

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
			FxAoPower = E.GetVariableByName("gAoPower").AsScalar();
			FxWeights = E.GetVariableByName("gWeights").AsScalar();
			FxSamplesKernel = E.GetVariableByName("gSamplesKernel").AsVector();
			FxEyePosW = E.GetVariableByName("gEyePosW").AsVector();
			FxNoiseSize = E.GetVariableByName("gNoiseSize").AsVector();
			FxSourcePixel = E.GetVariableByName("gSourcePixel").AsVector();
			FxNearFarValue = E.GetVariableByName("gNearFarValue").AsVector();
		}

        public void Dispose() {
			if (E == null) return;
			DisposeHelper.Dispose(ref InputSignaturePT);
			DisposeHelper.Dispose(ref LayoutPT);
			DisposeHelper.Dispose(ref E);
			DisposeHelper.Dispose(ref _b);
        }
	}

	public class EffectPpSsaoAlt : IEffectWrapper {
		public const int SampleCount = 24;
		public const int SampleThreshold = 14;
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectReadyTechnique TechSsaoVs;

		public EffectOnlyMatrixVariable FxViewProjInv, FxViewProj;
		public EffectOnlyResourceVariable FxDepthMap, FxNormalMap, FxNoiseMap;
		public EffectScalarVariable FxAoPower;
		public EffectVectorVariable FxSamplesKernel, FxNoiseSize;

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
			FxAoPower = E.GetVariableByName("gAoPower").AsScalar();
			FxSamplesKernel = E.GetVariableByName("gSamplesKernel").AsVector();
			FxNoiseSize = E.GetVariableByName("gNoiseSize").AsVector();
		}

        public void Dispose() {
			if (E == null) return;
			DisposeHelper.Dispose(ref InputSignaturePT);
			DisposeHelper.Dispose(ref LayoutPT);
			DisposeHelper.Dispose(ref E);
			DisposeHelper.Dispose(ref _b);
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

		public EffectReadyTechnique TechStandard, TechAlpha, TechReflective, TechNm, TechNmUvMult, TechAtNm, TechMaps, TechDiffMaps, TechGl, TechAmbientShadow, TechMirror;

		public EffectOnlyMatrixVariable FxWorld, FxWorldInvTranspose, FxWorldViewProj;
		public EffectOnlyResourceVariable FxDiffuseMap, FxNormalMap, FxMapsMap, FxDetailsMap, FxDetailsNormalMap;
		public EffectVectorVariable FxEyePosW;
		public EffectStructStandartMaterialVariable FxMaterial;
		public EffectStructReflectiveMaterialVariable FxReflectiveMaterial;
		public EffectStructMapsMaterialVariable FxMapsMaterial;
		public EffectStructAlphaMaterialVariable FxAlphaMaterial;
		public EffectStructNmUvMultMaterialVariable FxNmUvMultMaterial;

		EffectOnlyMatrixVariable IEffectMatricesWrapper.FxWorld => FxWorld;
		EffectOnlyMatrixVariable IEffectMatricesWrapper.FxWorldInvTranspose => FxWorldInvTranspose;
		EffectOnlyMatrixVariable IEffectMatricesWrapper.FxWorldViewProj => FxWorldViewProj;

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
			DisposeHelper.Dispose(ref InputSignaturePT);
			DisposeHelper.Dispose(ref LayoutPT);
			DisposeHelper.Dispose(ref InputSignaturePNTG);
			DisposeHelper.Dispose(ref LayoutPNTG);
			DisposeHelper.Dispose(ref E);
			DisposeHelper.Dispose(ref _b);
        }
	}

	public class EffectSpecialDebugLines : IEffectWrapper {
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePC;
        public InputLayout LayoutPC;

		public EffectReadyTechnique TechMain;

		public EffectOnlyMatrixVariable FxWorldViewProj;
		public EffectScalarVariable FxOverrideColor;
		public EffectVectorVariable FxCustomColor;

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
			FxOverrideColor = E.GetVariableByName("gOverrideColor").AsScalar();
			FxCustomColor = E.GetVariableByName("gCustomColor").AsVector();
		}

        public void Dispose() {
			if (E == null) return;
			DisposeHelper.Dispose(ref InputSignaturePC);
			DisposeHelper.Dispose(ref LayoutPC);
			DisposeHelper.Dispose(ref E);
			DisposeHelper.Dispose(ref _b);
        }
	}

	public class EffectSpecialPaintShop : IEffectWrapper {
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectReadyTechnique TechFill, TechPattern, TechColorfulPattern, TechFlakes, TechReplacement, TechMaps, TechMapsFillGreen, TechTint, TechTintMask, TechCombineChannels, TechMaximum, TechMaximumApply, TechDesaturate;

		public EffectOnlyResourceVariable FxNoiseMap, FxInputMap, FxAoMap, FxMaskMap, FxOverlayMap;
		public EffectScalarVariable FxNoiseMultipler, FxFlakes, FxUseMask;
		public EffectVectorVariable FxInputMapChannels, FxAoMapChannels, FxMaskMapChannels, FxOverlayMapChannels, FxColor, FxSize, FxColors;

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "SpecialPaintShop");
			E = new Effect(device, _b);

			TechFill = new EffectReadyTechnique(E.GetTechniqueByName("Fill"));
			TechPattern = new EffectReadyTechnique(E.GetTechniqueByName("Pattern"));
			TechColorfulPattern = new EffectReadyTechnique(E.GetTechniqueByName("ColorfulPattern"));
			TechFlakes = new EffectReadyTechnique(E.GetTechniqueByName("Flakes"));
			TechReplacement = new EffectReadyTechnique(E.GetTechniqueByName("Replacement"));
			TechMaps = new EffectReadyTechnique(E.GetTechniqueByName("Maps"));
			TechMapsFillGreen = new EffectReadyTechnique(E.GetTechniqueByName("MapsFillGreen"));
			TechTint = new EffectReadyTechnique(E.GetTechniqueByName("Tint"));
			TechTintMask = new EffectReadyTechnique(E.GetTechniqueByName("TintMask"));
			TechCombineChannels = new EffectReadyTechnique(E.GetTechniqueByName("CombineChannels"));
			TechMaximum = new EffectReadyTechnique(E.GetTechniqueByName("Maximum"));
			TechMaximumApply = new EffectReadyTechnique(E.GetTechniqueByName("MaximumApply"));
			TechDesaturate = new EffectReadyTechnique(E.GetTechniqueByName("Desaturate"));

			for (var i = 0; i < TechFill.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechFill.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (SpecialPaintShop, PT, Fill) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);

			FxNoiseMap = new EffectOnlyResourceVariable(E.GetVariableByName("gNoiseMap").AsResource());
			FxInputMap = new EffectOnlyResourceVariable(E.GetVariableByName("gInputMap").AsResource());
			FxAoMap = new EffectOnlyResourceVariable(E.GetVariableByName("gAoMap").AsResource());
			FxMaskMap = new EffectOnlyResourceVariable(E.GetVariableByName("gMaskMap").AsResource());
			FxOverlayMap = new EffectOnlyResourceVariable(E.GetVariableByName("gOverlayMap").AsResource());
			FxNoiseMultipler = E.GetVariableByName("gNoiseMultipler").AsScalar();
			FxFlakes = E.GetVariableByName("gFlakes").AsScalar();
			FxUseMask = E.GetVariableByName("gUseMask").AsScalar();
			FxInputMapChannels = E.GetVariableByName("gInputMapChannels").AsVector();
			FxAoMapChannels = E.GetVariableByName("gAoMapChannels").AsVector();
			FxMaskMapChannels = E.GetVariableByName("gMaskMapChannels").AsVector();
			FxOverlayMapChannels = E.GetVariableByName("gOverlayMapChannels").AsVector();
			FxColor = E.GetVariableByName("gColor").AsVector();
			FxSize = E.GetVariableByName("gSize").AsVector();
			FxColors = E.GetVariableByName("gColors").AsVector();
		}

        public void Dispose() {
			if (E == null) return;
			DisposeHelper.Dispose(ref InputSignaturePT);
			DisposeHelper.Dispose(ref LayoutPT);
			DisposeHelper.Dispose(ref E);
			DisposeHelper.Dispose(ref _b);
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
			DisposeHelper.Dispose(ref InputSignaturePT);
			DisposeHelper.Dispose(ref LayoutPT);
			DisposeHelper.Dispose(ref E);
			DisposeHelper.Dispose(ref _b);
        }
	}

	public class EffectSpecialShadow : IEffectWrapper, IEffectMatricesWrapper {
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT, InputSignaturePNTG;
        public InputLayout LayoutPT, LayoutPNTG;

		public EffectReadyTechnique TechHorizontalShadowBlur, TechVerticalShadowBlur, TechAmbientShadow, TechResult, TechSimplest, TechAo, TechAoResult, TechAoGrow;

		public EffectOnlyMatrixVariable FxShadowViewProj, FxWorldViewProj, FxWorld, FxWorldInvTranspose;
		public EffectOnlyResourceVariable FxInputMap, FxDepthMap, FxAlphaMap, FxNormalMap;
		public EffectScalarVariable FxMultipler, FxGamma, FxCount, FxAmbient, FxPadding, FxFade, FxAlphaRef, FxNormalUvMult;
		public EffectVectorVariable FxSize, FxShadowSize, FxLightDir;

		EffectOnlyMatrixVariable IEffectMatricesWrapper.FxWorld => FxWorld;
		EffectOnlyMatrixVariable IEffectMatricesWrapper.FxWorldInvTranspose => FxWorldInvTranspose;
		EffectOnlyMatrixVariable IEffectMatricesWrapper.FxWorldViewProj => FxWorldViewProj;

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
			DisposeHelper.Dispose(ref InputSignaturePT);
			DisposeHelper.Dispose(ref LayoutPT);
			DisposeHelper.Dispose(ref InputSignaturePNTG);
			DisposeHelper.Dispose(ref LayoutPNTG);
			DisposeHelper.Dispose(ref E);
			DisposeHelper.Dispose(ref _b);
        }
	}

	public class EffectSpecialTrackMap : IEffectWrapper, IEffectScreenSizeWrapper {
		public const int Gblurradius = 3;
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignatureP, InputSignaturePT;
        public InputLayout LayoutP, LayoutPT;

		public EffectReadyTechnique TechMain, TechPp, TechFinal, TechFinalCheckers, TechPpHorizontalBlur, TechPpVerticalBlur;

		public EffectOnlyMatrixVariable FxWorldViewProj;
		public EffectOnlyResourceVariable FxInputMap;
		public EffectVectorVariable FxScreenSize;

		EffectVectorVariable IEffectScreenSizeWrapper.FxScreenSize => FxScreenSize;

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
			DisposeHelper.Dispose(ref InputSignatureP);
			DisposeHelper.Dispose(ref LayoutP);
			DisposeHelper.Dispose(ref InputSignaturePT);
			DisposeHelper.Dispose(ref LayoutPT);
			DisposeHelper.Dispose(ref E);
			DisposeHelper.Dispose(ref _b);
        }
	}

	public class EffectSpecialTrackOutline : IEffectWrapper, IEffectScreenSizeWrapper {
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT, InputSignatureP;
        public InputLayout LayoutPT, LayoutP;

		public EffectReadyTechnique TechFirstStep, TechExtraWidth, TechShadow, TechCombine, TechBlend, TechFinal, TechFinalBg, TechFinalCheckers, TechFirstStepObj;

		public EffectOnlyMatrixVariable FxMatrix, FxWorldViewProj;
		public EffectOnlyResourceVariable FxInputMap, FxBgMap;
		public EffectScalarVariable FxExtraWidth, FxDropShadowRadius;
		public EffectVectorVariable FxScreenSize, FxBlendColor;

		EffectVectorVariable IEffectScreenSizeWrapper.FxScreenSize => FxScreenSize;

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
			DisposeHelper.Dispose(ref InputSignaturePT);
			DisposeHelper.Dispose(ref LayoutPT);
			DisposeHelper.Dispose(ref InputSignatureP);
			DisposeHelper.Dispose(ref LayoutP);
			DisposeHelper.Dispose(ref E);
			DisposeHelper.Dispose(ref _b);
        }
	}

	public class EffectSpecialUv : IEffectWrapper {
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePNTG;
        public InputLayout LayoutPNTG;

		public EffectReadyTechnique TechMain;

		public EffectVectorVariable FxOffset;

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
			DisposeHelper.Dispose(ref InputSignaturePNTG);
			DisposeHelper.Dispose(ref LayoutPNTG);
			DisposeHelper.Dispose(ref E);
			DisposeHelper.Dispose(ref _b);
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
			DisposeHelper.Dispose(ref InputSignatureSpriteSpecific);
			DisposeHelper.Dispose(ref LayoutSpriteSpecific);
			DisposeHelper.Dispose(ref E);
			DisposeHelper.Dispose(ref _b);
        }
	}

	public class EffectTestingCube : IEffectWrapper {
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePC;
        public InputLayout LayoutPC;

		public EffectReadyTechnique TechCube;

		public EffectOnlyMatrixVariable FxWorldViewProj;

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
			DisposeHelper.Dispose(ref InputSignaturePC);
			DisposeHelper.Dispose(ref LayoutPC);
			DisposeHelper.Dispose(ref E);
			DisposeHelper.Dispose(ref _b);
        }
	}

	public class EffectTestingPnt : IEffectWrapper {
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePNT;
        public InputLayout LayoutPNT;

		public EffectReadyTechnique TechCube;

		public EffectOnlyMatrixVariable FxWorldViewProj;

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
			DisposeHelper.Dispose(ref InputSignaturePNT);
			DisposeHelper.Dispose(ref LayoutPNT);
			DisposeHelper.Dispose(ref E);
			DisposeHelper.Dispose(ref _b);
        }
	}

}
