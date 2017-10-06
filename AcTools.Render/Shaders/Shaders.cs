/* GENERATED AUTOMATICALLY */
/* DON’T MODIFY */

using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Base.Structs;
using AcTools.Render.Base.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.D3DCompiler;
using SlimDX.Direct3D11;
// ReSharper disable InconsistentNaming
// ReSharper disable LocalizableElement
// ReSharper disable NotNullMemberIsNotInitialized

namespace AcTools.Render.Shaders {
	internal static class ShadersResourceManager {
		internal static readonly ResourceManager Manager = new ResourceManager("AcTools.Render.Shaders", Assembly.GetExecutingAssembly());
	}

	public class EffectDarkMaterial : IEffectWrapper, IEffectMatricesWrapper, IEffectScreenSizeWrapper {
		public enum Mode { Main, FewerExtraShadows, FewerExtraShadowsNoPCSS, NoAreaLights, NoExtraShadows, NoPCSS, Simple, SimpleNoPCSS, SimpleNoShadows, WithoutLighting }

		[StructLayout(LayoutKind.Sequential)]
        public struct Light {
            public Vector3 PosW;
            public float Range;
            public Vector3 DirectionW;
            public float SpotlightCosMin;
            public Vector3 Color;
            public float SpotlightCosMax;
            public uint Type;
            public uint Flags;
            public uint ShadowId;
            public float Padding;
            public Vector4 Extra;

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

		[StructLayout(LayoutKind.Sequential)]
        public struct TyresMaterial {
            public float BlurLevel;
            public float DirtyLevel;

			public static readonly int Stride = Marshal.SizeOf(typeof(TyresMaterial));
        }

		public class EffectStructTyresMaterialVariable {
			private readonly EffectVariable _v;

			public EffectStructTyresMaterialVariable(EffectVariable v) {
				_v = v;
			}

			public void Set(TyresMaterial value){
				 SlimDxExtension.SetObject(_v, value, TyresMaterial.Stride);
			}
        }

		public const uint LightOff = 0;
		public const uint LightPoint = 1;
		public const uint LightSpot = 2;
		public const uint LightDirectional = 3;
		public const uint LightSphere = 4;
		public const uint LightTube = 5;
		public const uint LightLtcPlane = 6;
		public const uint LightLtcTube = 7;
		public const uint LightLtcSphere = 4;
		public const uint LightNoShadows = 1;
		public const uint LightSmoothShadows = 2;
		public const uint LightShadowsCube = 4;
		public const uint LightSpecular = 8;
		public const uint LightLtcPlaneDoubleSide = 16;
		public const uint LightLtcTubeWithCaps = 16;
		public const uint HasNormalMap = 1;
		public const uint NmObjectSpace = 2;
		public const uint UseNormalAlphaAsAlpha = 64;
		public const uint AlphaTest = 128;
		public const uint IsAdditive = 16;
		public const uint HasDetailsMap = 4;
		public const uint IsCarpaint = 32;
		public const bool DebugMode = false;
		public const float Pi = 3.141592653f;
		public const int ComplexLighting = 1;
		public const float CubemapPadding = 0.95f;
		public const float LutSize = 64.0f;
		public const int MaxNumSplits = 3;
		public const int MaxBones = 64;
		public const bool EnableShadows = true;
		public const bool EnablePcss = true;
		public const int MaxLighsAmount = 50;
		public const int MaxExtraShadows = 25;
		public const bool EnableAreaLights = true;
		public const bool EnableAdditionalAreaLights = false;
		public const int MaxExtraShadowsSmooth = 25;
		public const int MaxExtraShadowsFewer = 5;
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT, InputSignaturePNTG, InputSignaturePNTGW4B;
        public InputLayout LayoutPT, LayoutPNTG, LayoutPNTGW4B;

		public EffectReadyTechnique TechStandard, TechSky, TechAlpha, TechReflective, TechNm, TechNmUvMult, TechAtNm, TechMaps, TechSkinnedMaps, TechDiffMaps, TechTyres, TechGl, TechSkinnedGl, TechWindscreen, TechCollider, TechDebug, TechSkinnedDebug, TechDepthOnly, TechSkinnedDepthOnly, TechAmbientShadow, TechMirror, TechFlatMirror, TechTransparentGround, TechFlatTextureMirror, TechFlatBackgroundGround, TechFlatAmbientGround, TechGPass_Standard, TechGPass_Alpha, TechGPass_Reflective, TechGPass_Nm, TechGPass_NmUvMult, TechGPass_AtNm, TechGPass_Maps, TechGPass_SkinnedMaps, TechGPass_Tyres, TechGPass_Gl, TechGPass_SkinnedGl, TechGPass_FlatMirror, TechGPass_FlatMirror_SslrFix, TechGPass_Debug, TechGPass_SkinnedDebug;

		[NotNull]
		public EffectOnlyMatrixVariable FxWorld, FxWorldInvTranspose, FxWorldViewProj;
		[NotNull]
		public EffectOnlyMatrixArrayVariable FxExtraShadowViewProj, FxShadowViewProj, FxBoneTransforms;
		[NotNull]
		public EffectOnlyResourceVariable FxNoiseMap, FxLtcMap, FxLtcAmp, FxReflectionCubemap, FxDiffuseMap, FxNormalMap, FxMapsMap, FxDetailsMap, FxDetailsNormalMap, FxDiffuseBlurMap, FxNormalBlurMap, FxDirtyMap, FxAoMap;
		[NotNull]
		public EffectOnlyResourceArrayVariable FxExtraShadowMaps, FxShadowMaps;
		[NotNull]
		public EffectOnlyIntVariable FxNumSplits, FxFlatMirrorSide;
		[NotNull]
		public EffectOnlyFloatVariable FxGPassAlphaThreshold, FxReflectionPower, FxCubemapReflectionsOffset, FxCubemapAmbient, FxFlatMirrorPower;
		[NotNull]
		public EffectOnlyBoolVariable FxGPassTransparent, FxPcssEnabled, FxUseAo, FxCubemapReflections;
		[NotNull]
		public EffectOnlyVector2Variable FxShadowMapSize;
		[NotNull]
		public EffectOnlyVector3Variable FxLightDir, FxLightColor, FxEyePosW, FxAmbientDown, FxAmbientRange, FxBackgroundColor;
		[NotNull]
		public EffectOnlyVector4Variable FxScreenSize;
		[NotNull]
		public EffectOnlyVectorArrayVariable FxExtraShadowMapSize, FxExtraShadowNearFar, FxPcssScale;
		[NotNull]
		public EffectStructStandartMaterialVariable FxMaterial;
		[NotNull]
		public EffectStructReflectiveMaterialVariable FxReflectiveMaterial;
		[NotNull]
		public EffectStructMapsMaterialVariable FxMapsMaterial;
		[NotNull]
		public EffectStructAlphaMaterialVariable FxAlphaMaterial;
		[NotNull]
		public EffectStructNmUvMultMaterialVariable FxNmUvMultMaterial;
		[NotNull]
		public EffectStructTyresMaterialVariable FxTyresMaterial;
		[NotNull]
		public EffectStructLightArrayVariable FxLights;

		EffectOnlyVector4Variable IEffectScreenSizeWrapper.FxScreenSize => FxScreenSize;

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
			TechTyres = new EffectReadyTechnique(E.GetTechniqueByName("Tyres"));
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
			TechTransparentGround = new EffectReadyTechnique(E.GetTechniqueByName("TransparentGround"));
			TechFlatTextureMirror = new EffectReadyTechnique(E.GetTechniqueByName("FlatTextureMirror"));
			TechFlatBackgroundGround = new EffectReadyTechnique(E.GetTechniqueByName("FlatBackgroundGround"));
			TechFlatAmbientGround = new EffectReadyTechnique(E.GetTechniqueByName("FlatAmbientGround"));
			TechGPass_Standard = new EffectReadyTechnique(E.GetTechniqueByName("GPass_Standard"));
			TechGPass_Alpha = new EffectReadyTechnique(E.GetTechniqueByName("GPass_Alpha"));
			TechGPass_Reflective = new EffectReadyTechnique(E.GetTechniqueByName("GPass_Reflective"));
			TechGPass_Nm = new EffectReadyTechnique(E.GetTechniqueByName("GPass_Nm"));
			TechGPass_NmUvMult = new EffectReadyTechnique(E.GetTechniqueByName("GPass_NmUvMult"));
			TechGPass_AtNm = new EffectReadyTechnique(E.GetTechniqueByName("GPass_AtNm"));
			TechGPass_Maps = new EffectReadyTechnique(E.GetTechniqueByName("GPass_Maps"));
			TechGPass_SkinnedMaps = new EffectReadyTechnique(E.GetTechniqueByName("GPass_SkinnedMaps"));
			TechGPass_Tyres = new EffectReadyTechnique(E.GetTechniqueByName("GPass_Tyres"));
			TechGPass_Gl = new EffectReadyTechnique(E.GetTechniqueByName("GPass_Gl"));
			TechGPass_SkinnedGl = new EffectReadyTechnique(E.GetTechniqueByName("GPass_SkinnedGl"));
			TechGPass_FlatMirror = new EffectReadyTechnique(E.GetTechniqueByName("GPass_FlatMirror"));
			TechGPass_FlatMirror_SslrFix = new EffectReadyTechnique(E.GetTechniqueByName("GPass_FlatMirror_SslrFix"));
			TechGPass_Debug = new EffectReadyTechnique(E.GetTechniqueByName("GPass_Debug"));
			TechGPass_SkinnedDebug = new EffectReadyTechnique(E.GetTechniqueByName("GPass_SkinnedDebug"));

			for (var i = 0; i < TechAmbientShadow.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechAmbientShadow.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (DarkMaterial, PT, AmbientShadow) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);
			for (var i = 0; i < TechStandard.Description.PassCount && InputSignaturePNTG == null; i++) {
				InputSignaturePNTG = TechStandard.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePNTG == null) throw new System.Exception("input signature (DarkMaterial, PNTG, Standard) == null");
			LayoutPNTG = new InputLayout(device, InputSignaturePNTG, InputLayouts.VerticePNTG.InputElementsValue);
			for (var i = 0; i < TechSkinnedMaps.Description.PassCount && InputSignaturePNTGW4B == null; i++) {
				InputSignaturePNTGW4B = TechSkinnedMaps.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePNTGW4B == null) throw new System.Exception("input signature (DarkMaterial, PNTGW4B, SkinnedMaps) == null");
			LayoutPNTGW4B = new InputLayout(device, InputSignaturePNTGW4B, InputLayouts.VerticePNTGW4B.InputElementsValue);

			FxWorld = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorld"));
			FxWorldInvTranspose = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldInvTranspose"));
			FxWorldViewProj = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldViewProj"));
			FxExtraShadowViewProj = new EffectOnlyMatrixArrayVariable(E.GetVariableByName("gExtraShadowViewProj"));
			FxShadowViewProj = new EffectOnlyMatrixArrayVariable(E.GetVariableByName("gShadowViewProj"));
			FxBoneTransforms = new EffectOnlyMatrixArrayVariable(E.GetVariableByName("gBoneTransforms"));
			FxNoiseMap = new EffectOnlyResourceVariable(E.GetVariableByName("gNoiseMap"));
			FxLtcMap = new EffectOnlyResourceVariable(E.GetVariableByName("gLtcMap"));
			FxLtcAmp = new EffectOnlyResourceVariable(E.GetVariableByName("gLtcAmp"));
			FxReflectionCubemap = new EffectOnlyResourceVariable(E.GetVariableByName("gReflectionCubemap"));
			FxNoiseMap = new EffectOnlyResourceVariable(E.GetVariableByName("gNoiseMap"));
			FxDiffuseMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDiffuseMap"));
			FxNormalMap = new EffectOnlyResourceVariable(E.GetVariableByName("gNormalMap"));
			FxMapsMap = new EffectOnlyResourceVariable(E.GetVariableByName("gMapsMap"));
			FxDetailsMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDetailsMap"));
			FxDetailsNormalMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDetailsNormalMap"));
			FxDiffuseBlurMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDiffuseBlurMap"));
			FxNormalBlurMap = new EffectOnlyResourceVariable(E.GetVariableByName("gNormalBlurMap"));
			FxDirtyMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDirtyMap"));
			FxAoMap = new EffectOnlyResourceVariable(E.GetVariableByName("gAoMap"));
			FxExtraShadowMaps = new EffectOnlyResourceArrayVariable(E.GetVariableByName("gExtraShadowMaps"));
			FxShadowMaps = new EffectOnlyResourceArrayVariable(E.GetVariableByName("gShadowMaps"));
			FxNumSplits = new EffectOnlyIntVariable(E.GetVariableByName("gNumSplits"));
			FxFlatMirrorSide = new EffectOnlyIntVariable(E.GetVariableByName("gFlatMirrorSide"));
			FxGPassAlphaThreshold = new EffectOnlyFloatVariable(E.GetVariableByName("gGPassAlphaThreshold"));
			FxReflectionPower = new EffectOnlyFloatVariable(E.GetVariableByName("gReflectionPower"));
			FxCubemapReflectionsOffset = new EffectOnlyFloatVariable(E.GetVariableByName("gCubemapReflectionsOffset"));
			FxCubemapAmbient = new EffectOnlyFloatVariable(E.GetVariableByName("gCubemapAmbient"));
			FxFlatMirrorPower = new EffectOnlyFloatVariable(E.GetVariableByName("gFlatMirrorPower"));
			FxGPassTransparent = new EffectOnlyBoolVariable(E.GetVariableByName("gGPassTransparent"));
			FxPcssEnabled = new EffectOnlyBoolVariable(E.GetVariableByName("gPcssEnabled"));
			FxUseAo = new EffectOnlyBoolVariable(E.GetVariableByName("gUseAo"));
			FxCubemapReflections = new EffectOnlyBoolVariable(E.GetVariableByName("gCubemapReflections"));
			FxShadowMapSize = new EffectOnlyVector2Variable(E.GetVariableByName("gShadowMapSize"));
			FxLightDir = new EffectOnlyVector3Variable(E.GetVariableByName("gLightDir"));
			FxLightColor = new EffectOnlyVector3Variable(E.GetVariableByName("gLightColor"));
			FxEyePosW = new EffectOnlyVector3Variable(E.GetVariableByName("gEyePosW"));
			FxAmbientDown = new EffectOnlyVector3Variable(E.GetVariableByName("gAmbientDown"));
			FxAmbientRange = new EffectOnlyVector3Variable(E.GetVariableByName("gAmbientRange"));
			FxBackgroundColor = new EffectOnlyVector3Variable(E.GetVariableByName("gBackgroundColor"));
			FxScreenSize = new EffectOnlyVector4Variable(E.GetVariableByName("gScreenSize"));
			FxExtraShadowMapSize = new EffectOnlyVectorArrayVariable(E.GetVariableByName("gExtraShadowMapSize"));
			FxExtraShadowNearFar = new EffectOnlyVectorArrayVariable(E.GetVariableByName("gExtraShadowNearFar"));
			FxPcssScale = new EffectOnlyVectorArrayVariable(E.GetVariableByName("gPcssScale"));
			FxMaterial = new EffectStructStandartMaterialVariable(E.GetVariableByName("gMaterial"));
			FxReflectiveMaterial = new EffectStructReflectiveMaterialVariable(E.GetVariableByName("gReflectiveMaterial"));
			FxMapsMaterial = new EffectStructMapsMaterialVariable(E.GetVariableByName("gMapsMaterial"));
			FxAlphaMaterial = new EffectStructAlphaMaterialVariable(E.GetVariableByName("gAlphaMaterial"));
			FxNmUvMultMaterial = new EffectStructNmUvMultMaterialVariable(E.GetVariableByName("gNmUvMultMaterial"));
			FxTyresMaterial = new EffectStructTyresMaterialVariable(E.GetVariableByName("gTyresMaterial"));
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

		[NotNull]
		public EffectOnlyMatrixVariable FxWorld, FxWorldInvTranspose, FxWorldViewProj;
		[NotNull]
		public EffectOnlyResourceVariable FxDiffuseMap;
		[NotNull]
		public EffectOnlyVector3Variable FxEyePosW;
		[NotNull]
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

			FxWorld = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorld"));
			FxWorldInvTranspose = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldInvTranspose"));
			FxWorldViewProj = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldViewProj"));
			FxDiffuseMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDiffuseMap"));
			FxEyePosW = new EffectOnlyVector3Variable(E.GetVariableByName("gEyePosW"));
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

		public EffectReadyTechnique TechCopy, TechCopySqr, TechCopySqrt, TechCut, TechCopyNoAlpha, TechAccumulate, TechAccumulateDivide, TechAccumulateBokehDivide, TechOverlay, TechDepthToLinear, TechShadow, TechDepth, TechFxaa;

		[NotNull]
		public EffectOnlyResourceVariable FxInputMap, FxOverlayMap, FxDepthMap;
		[NotNull]
		public EffectOnlyFloatVariable FxSizeMultipler, FxMultipler;
		[NotNull]
		public EffectOnlyVector2Variable FxBokenMultipler;
		[NotNull]
		public EffectOnlyVector4Variable FxScreenSize;

		EffectOnlyVector4Variable IEffectScreenSizeWrapper.FxScreenSize => FxScreenSize;

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "PpBasic");
			E = new Effect(device, _b);

			TechCopy = new EffectReadyTechnique(E.GetTechniqueByName("Copy"));
			TechCopySqr = new EffectReadyTechnique(E.GetTechniqueByName("CopySqr"));
			TechCopySqrt = new EffectReadyTechnique(E.GetTechniqueByName("CopySqrt"));
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

			FxInputMap = new EffectOnlyResourceVariable(E.GetVariableByName("gInputMap"));
			FxOverlayMap = new EffectOnlyResourceVariable(E.GetVariableByName("gOverlayMap"));
			FxDepthMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDepthMap"));
			FxSizeMultipler = new EffectOnlyFloatVariable(E.GetVariableByName("gSizeMultipler"));
			FxMultipler = new EffectOnlyFloatVariable(E.GetVariableByName("gMultipler"));
			FxBokenMultipler = new EffectOnlyVector2Variable(E.GetVariableByName("gBokenMultipler"));
			FxScreenSize = new EffectOnlyVector4Variable(E.GetVariableByName("gScreenSize"));
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

		[NotNull]
		public EffectOnlyMatrixVariable FxWorldViewProjInv;
		[NotNull]
		public EffectOnlyResourceVariable FxInputMap, FxFlatMirrorDepthMap, FxFlatMirrorNormalsMap, FxMapsMap;
		[NotNull]
		public EffectOnlyFloatVariable FxPower;
		[NotNull]
		public EffectOnlyFloatArrayVariable FxSampleWeights;
		[NotNull]
		public EffectOnlyVector4Variable FxScreenSize;
		[NotNull]
		public EffectOnlyVectorArrayVariable FxSampleOffsets;

		EffectOnlyVector4Variable IEffectScreenSizeWrapper.FxScreenSize => FxScreenSize;

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

			FxWorldViewProjInv = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldViewProjInv"));
			FxInputMap = new EffectOnlyResourceVariable(E.GetVariableByName("gInputMap"));
			FxFlatMirrorDepthMap = new EffectOnlyResourceVariable(E.GetVariableByName("gFlatMirrorDepthMap"));
			FxFlatMirrorNormalsMap = new EffectOnlyResourceVariable(E.GetVariableByName("gFlatMirrorNormalsMap"));
			FxMapsMap = new EffectOnlyResourceVariable(E.GetVariableByName("gMapsMap"));
			FxPower = new EffectOnlyFloatVariable(E.GetVariableByName("gPower"));
			FxSampleWeights = new EffectOnlyFloatArrayVariable(E.GetVariableByName("gSampleWeights"));
			FxScreenSize = new EffectOnlyVector4Variable(E.GetVariableByName("gScreenSize"));
			FxSampleOffsets = new EffectOnlyVectorArrayVariable(E.GetVariableByName("gSampleOffsets"));
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

		[NotNull]
		public EffectOnlyMatrixVariable FxCameraProjInv, FxCameraProj, FxWorldViewProjInv, FxWorldViewProj;
		[NotNull]
		public EffectOnlyResourceVariable FxDiffuseMap, FxDepthMap, FxBaseReflectionMap, FxNormalMap, FxNoiseMap, FxFirstStepMap, FxDepthMapDown, FxDepthMapDownMore;
		[NotNull]
		public EffectOnlyFloatVariable FxStartFrom, FxFixMultiplier, FxOffset, FxGlowFix, FxDistanceThreshold;
		[NotNull]
		public EffectOnlyVector3Variable FxEyePosW;
		[NotNull]
		public EffectOnlyVector4Variable FxSize;

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

			FxCameraProjInv = new EffectOnlyMatrixVariable(E.GetVariableByName("gCameraProjInv"));
			FxCameraProj = new EffectOnlyMatrixVariable(E.GetVariableByName("gCameraProj"));
			FxWorldViewProjInv = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldViewProjInv"));
			FxWorldViewProj = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldViewProj"));
			FxDiffuseMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDiffuseMap"));
			FxDepthMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDepthMap"));
			FxBaseReflectionMap = new EffectOnlyResourceVariable(E.GetVariableByName("gBaseReflectionMap"));
			FxNormalMap = new EffectOnlyResourceVariable(E.GetVariableByName("gNormalMap"));
			FxNoiseMap = new EffectOnlyResourceVariable(E.GetVariableByName("gNoiseMap"));
			FxFirstStepMap = new EffectOnlyResourceVariable(E.GetVariableByName("gFirstStepMap"));
			FxDepthMapDown = new EffectOnlyResourceVariable(E.GetVariableByName("gDepthMapDown"));
			FxDepthMapDownMore = new EffectOnlyResourceVariable(E.GetVariableByName("gDepthMapDownMore"));
			FxStartFrom = new EffectOnlyFloatVariable(E.GetVariableByName("gStartFrom"));
			FxFixMultiplier = new EffectOnlyFloatVariable(E.GetVariableByName("gFixMultiplier"));
			FxOffset = new EffectOnlyFloatVariable(E.GetVariableByName("gOffset"));
			FxGlowFix = new EffectOnlyFloatVariable(E.GetVariableByName("gGlowFix"));
			FxDistanceThreshold = new EffectOnlyFloatVariable(E.GetVariableByName("gDistanceThreshold"));
			FxEyePosW = new EffectOnlyVector3Variable(E.GetVariableByName("gEyePosW"));
			FxSize = new EffectOnlyVector4Variable(E.GetVariableByName("gSize"));
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

		[NotNull]
		public EffectOnlyResourceVariable FxInputTexture, FxInputTextureBokenBase, FxInputTextureDepth, FxInputTextureBokeh, FxInputTextureDownscaledColor;
		[NotNull]
		public EffectOnlyFloatVariable FxZNear, FxZFar, FxFocusPlane, FxDofCoCScale, FxDebugBokeh, FxCoCLimit;
		[NotNull]
		public EffectOnlyVector4Variable FxScreenSize, FxScreenSizeHalfRes, FxCocScaleBias;

		EffectOnlyVector4Variable IEffectScreenSizeWrapper.FxScreenSize => FxScreenSize;

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

			FxInputTexture = new EffectOnlyResourceVariable(E.GetVariableByName("InputTexture"));
			FxInputTextureBokenBase = new EffectOnlyResourceVariable(E.GetVariableByName("InputTextureBokenBase"));
			FxInputTextureDepth = new EffectOnlyResourceVariable(E.GetVariableByName("InputTextureDepth"));
			FxInputTextureBokeh = new EffectOnlyResourceVariable(E.GetVariableByName("InputTextureBokeh"));
			FxInputTextureDownscaledColor = new EffectOnlyResourceVariable(E.GetVariableByName("InputTextureDownscaledColor"));
			FxZNear = new EffectOnlyFloatVariable(E.GetVariableByName("gZNear"));
			FxZFar = new EffectOnlyFloatVariable(E.GetVariableByName("gZFar"));
			FxFocusPlane = new EffectOnlyFloatVariable(E.GetVariableByName("gFocusPlane"));
			FxDofCoCScale = new EffectOnlyFloatVariable(E.GetVariableByName("gDofCoCScale"));
			FxDebugBokeh = new EffectOnlyFloatVariable(E.GetVariableByName("gDebugBokeh"));
			FxCoCLimit = new EffectOnlyFloatVariable(E.GetVariableByName("gCoCLimit"));
			FxScreenSize = new EffectOnlyVector4Variable(E.GetVariableByName("gScreenSize"));
			FxScreenSizeHalfRes = new EffectOnlyVector4Variable(E.GetVariableByName("gScreenSizeHalfRes"));
			FxCocScaleBias = new EffectOnlyVector4Variable(E.GetVariableByName("gCocScaleBias"));
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

		[NotNull]
		public EffectOnlyResourceVariable FxInputMap;
		[NotNull]
		public EffectOnlyVector2Variable FxMultipler;
		[NotNull]
		public EffectOnlyVector4Variable FxScreenSize;

		EffectOnlyVector4Variable IEffectScreenSizeWrapper.FxScreenSize => FxScreenSize;

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

			FxInputMap = new EffectOnlyResourceVariable(E.GetVariableByName("gInputMap"));
			FxMultipler = new EffectOnlyVector2Variable(E.GetVariableByName("gMultipler"));
			FxScreenSize = new EffectOnlyVector4Variable(E.GetVariableByName("gScreenSize"));
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

		public EffectReadyTechnique TechDownsampling, TechAdaptation, TechTonemap, TechCopy, TechColorGrading, TechCombine_ToneLumaBasedReinhard, TechCombine_ToneWhitePreservingLumaBasedReinhard, TechCombine_ToneUncharted2, TechCombine_ToneReinhard, TechCombine_ToneFilmic, TechCombine_ToneFilmicReinhard, TechCombine, TechBloom, TechBloomHighThreshold;

		[NotNull]
		public EffectOnlyResourceVariable FxInputMap, FxBrightnessMap, FxBloomMap, FxColorGradingMap;
		[NotNull]
		public EffectOnlyVector2Variable FxPixel, FxCropImage;
		[NotNull]
		public EffectOnlyVector4Variable FxParams;

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "PpHdr");
			E = new Effect(device, _b);

			TechDownsampling = new EffectReadyTechnique(E.GetTechniqueByName("Downsampling"));
			TechAdaptation = new EffectReadyTechnique(E.GetTechniqueByName("Adaptation"));
			TechTonemap = new EffectReadyTechnique(E.GetTechniqueByName("Tonemap"));
			TechCopy = new EffectReadyTechnique(E.GetTechniqueByName("Copy"));
			TechColorGrading = new EffectReadyTechnique(E.GetTechniqueByName("ColorGrading"));
			TechCombine_ToneLumaBasedReinhard = new EffectReadyTechnique(E.GetTechniqueByName("Combine_ToneLumaBasedReinhard"));
			TechCombine_ToneWhitePreservingLumaBasedReinhard = new EffectReadyTechnique(E.GetTechniqueByName("Combine_ToneWhitePreservingLumaBasedReinhard"));
			TechCombine_ToneUncharted2 = new EffectReadyTechnique(E.GetTechniqueByName("Combine_ToneUncharted2"));
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

			FxInputMap = new EffectOnlyResourceVariable(E.GetVariableByName("gInputMap"));
			FxBrightnessMap = new EffectOnlyResourceVariable(E.GetVariableByName("gBrightnessMap"));
			FxBloomMap = new EffectOnlyResourceVariable(E.GetVariableByName("gBloomMap"));
			FxColorGradingMap = new EffectOnlyResourceVariable(E.GetVariableByName("gColorGradingMap"));
			FxPixel = new EffectOnlyVector2Variable(E.GetVariableByName("gPixel"));
			FxCropImage = new EffectOnlyVector2Variable(E.GetVariableByName("gCropImage"));
			FxParams = new EffectOnlyVector4Variable(E.GetVariableByName("gParams"));
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

		[NotNull]
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

			FxInputMap = new EffectOnlyResourceVariable(E.GetVariableByName("gInputMap"));
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

		[NotNull]
		public EffectOnlyResourceVariable FxInputMap, FxDepthMap;
		[NotNull]
		public EffectOnlyVector4Variable FxScreenSize;

		EffectOnlyVector4Variable IEffectScreenSizeWrapper.FxScreenSize => FxScreenSize;

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "PpOutline");
			E = new Effect(device, _b);

			TechOutline = new EffectReadyTechnique(E.GetTechniqueByName("Outline"));

			for (var i = 0; i < TechOutline.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechOutline.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (PpOutline, PT, Outline) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);

			FxInputMap = new EffectOnlyResourceVariable(E.GetVariableByName("gInputMap"));
			FxDepthMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDepthMap"));
			FxScreenSize = new EffectOnlyVector4Variable(E.GetVariableByName("gScreenSize"));
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

		[NotNull]
		public EffectOnlyMatrixVariable FxViewProjInv, FxViewProj, FxShadowViewProj;
		[NotNull]
		public EffectOnlyResourceVariable FxDepthMap, FxShadowMap, FxNoiseMap;
		[NotNull]
		public EffectOnlyVector2Variable FxNoiseSize, FxShadowSize;
		[NotNull]
		public EffectOnlyVector3Variable FxShadowPosition;

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

			FxViewProjInv = new EffectOnlyMatrixVariable(E.GetVariableByName("gViewProjInv"));
			FxViewProj = new EffectOnlyMatrixVariable(E.GetVariableByName("gViewProj"));
			FxShadowViewProj = new EffectOnlyMatrixVariable(E.GetVariableByName("gShadowViewProj"));
			FxDepthMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDepthMap"));
			FxShadowMap = new EffectOnlyResourceVariable(E.GetVariableByName("gShadowMap"));
			FxNoiseMap = new EffectOnlyResourceVariable(E.GetVariableByName("gNoiseMap"));
			FxNoiseSize = new EffectOnlyVector2Variable(E.GetVariableByName("gNoiseSize"));
			FxShadowSize = new EffectOnlyVector2Variable(E.GetVariableByName("gShadowSize"));
			FxShadowPosition = new EffectOnlyVector3Variable(E.GetVariableByName("gShadowPosition"));
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

		[NotNull]
		public EffectOnlyResourceVariable FxDepthMap, FxNormalMap, FxFirstStepMap;
		[NotNull]
		public EffectOnlyFloatArrayVariable FxWeights;
		[NotNull]
		public EffectOnlyVector2Variable FxSourcePixel, FxNearFarValue;

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

			FxDepthMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDepthMap"));
			FxNormalMap = new EffectOnlyResourceVariable(E.GetVariableByName("gNormalMap"));
			FxFirstStepMap = new EffectOnlyResourceVariable(E.GetVariableByName("gFirstStepMap"));
			FxWeights = new EffectOnlyFloatArrayVariable(E.GetVariableByName("gWeights"));
			FxSourcePixel = new EffectOnlyVector2Variable(E.GetVariableByName("gSourcePixel"));
			FxNearFarValue = new EffectOnlyVector2Variable(E.GetVariableByName("gNearFarValue"));
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

		[NotNull]
		public EffectOnlyMatrixVariable FxWorldViewProjInv, FxWorldViewProj, FxProj, FxProjInv, FxNormalsToViewSpace;
		[NotNull]
		public EffectOnlyResourceVariable Fx_DepthSource, Fx_NormalmapSource, Fx_ViewspaceDepthSource, Fx_ViewspaceDepthSource1, Fx_ViewspaceDepthSource2, Fx_ViewspaceDepthSource3, Fx_ImportanceMap, Fx_LoadCounter, Fx_BlurInput, FxDepthMap, FxNormalMap, FxNoiseMap, FxDitherMap, FxFirstStepMap;
		[NotNull]
		public EffectOnlyVectorArrayVariable FxViewFrustumVectors;
		[NotNull]
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

			FxWorldViewProjInv = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldViewProjInv"));
			FxWorldViewProj = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldViewProj"));
			FxProj = new EffectOnlyMatrixVariable(E.GetVariableByName("gProj"));
			FxProjInv = new EffectOnlyMatrixVariable(E.GetVariableByName("gProjInv"));
			FxNormalsToViewSpace = new EffectOnlyMatrixVariable(E.GetVariableByName("gNormalsToViewSpace"));
			Fx_DepthSource = new EffectOnlyResourceVariable(E.GetVariableByName("g_DepthSource"));
			Fx_NormalmapSource = new EffectOnlyResourceVariable(E.GetVariableByName("g_NormalmapSource"));
			Fx_ViewspaceDepthSource = new EffectOnlyResourceVariable(E.GetVariableByName("g_ViewspaceDepthSource"));
			Fx_ViewspaceDepthSource1 = new EffectOnlyResourceVariable(E.GetVariableByName("g_ViewspaceDepthSource1"));
			Fx_ViewspaceDepthSource2 = new EffectOnlyResourceVariable(E.GetVariableByName("g_ViewspaceDepthSource2"));
			Fx_ViewspaceDepthSource3 = new EffectOnlyResourceVariable(E.GetVariableByName("g_ViewspaceDepthSource3"));
			Fx_ImportanceMap = new EffectOnlyResourceVariable(E.GetVariableByName("g_ImportanceMap"));
			Fx_LoadCounter = new EffectOnlyResourceVariable(E.GetVariableByName("g_LoadCounter"));
			Fx_BlurInput = new EffectOnlyResourceVariable(E.GetVariableByName("g_BlurInput"));
			FxDepthMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDepthMap"));
			FxNormalMap = new EffectOnlyResourceVariable(E.GetVariableByName("gNormalMap"));
			FxNoiseMap = new EffectOnlyResourceVariable(E.GetVariableByName("gNoiseMap"));
			FxDitherMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDitherMap"));
			FxFirstStepMap = new EffectOnlyResourceVariable(E.GetVariableByName("gFirstStepMap"));
			FxViewFrustumVectors = new EffectOnlyVectorArrayVariable(E.GetVariableByName("gViewFrustumVectors"));
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

		[NotNull]
		public EffectOnlyMatrixVariable FxWorldViewProjInv, FxView, FxProj, FxProjT, FxProjInv, FxNormalsToViewSpace, FxProjectionMatrix;
		[NotNull]
		public EffectOnlyResourceVariable FxDepthMap, FxNormalMap, FxNoiseMap, FxDitherMap, FxFirstStepMap;
		[NotNull]
		public EffectOnlyFloatVariable FxDitherScale;
		[NotNull]
		public EffectOnlyVector2Variable FxRenderTargetResolution;
		[NotNull]
		public EffectOnlyVectorArrayVariable FxViewFrustumVectors, FxSampleDirections;

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "PpHbao");
			E = new Effect(device, _b);

			TechHbao = new EffectReadyTechnique(E.GetTechniqueByName("Hbao"));

			for (var i = 0; i < TechHbao.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechHbao.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (PpHbao, PT, Hbao) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);

			FxWorldViewProjInv = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldViewProjInv"));
			FxView = new EffectOnlyMatrixVariable(E.GetVariableByName("gView"));
			FxProj = new EffectOnlyMatrixVariable(E.GetVariableByName("gProj"));
			FxProjT = new EffectOnlyMatrixVariable(E.GetVariableByName("gProjT"));
			FxProjInv = new EffectOnlyMatrixVariable(E.GetVariableByName("gProjInv"));
			FxNormalsToViewSpace = new EffectOnlyMatrixVariable(E.GetVariableByName("gNormalsToViewSpace"));
			FxProjectionMatrix = new EffectOnlyMatrixVariable(E.GetVariableByName("gProjectionMatrix"));
			FxDepthMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDepthMap"));
			FxNormalMap = new EffectOnlyResourceVariable(E.GetVariableByName("gNormalMap"));
			FxNoiseMap = new EffectOnlyResourceVariable(E.GetVariableByName("gNoiseMap"));
			FxDitherMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDitherMap"));
			FxFirstStepMap = new EffectOnlyResourceVariable(E.GetVariableByName("gFirstStepMap"));
			FxDitherScale = new EffectOnlyFloatVariable(E.GetVariableByName("gDitherScale"));
			FxRenderTargetResolution = new EffectOnlyVector2Variable(E.GetVariableByName("gRenderTargetResolution"));
			FxViewFrustumVectors = new EffectOnlyVectorArrayVariable(E.GetVariableByName("gViewFrustumVectors"));
			FxSampleDirections = new EffectOnlyVectorArrayVariable(E.GetVariableByName("gSampleDirections"));
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

		[NotNull]
		public EffectOnlyMatrixVariable FxCameraProjInv, FxCameraProj, FxWorldViewProjInv, FxWorldViewProj;
		[NotNull]
		public EffectOnlyResourceVariable FxDepthMap, FxNormalMap, FxNoiseMap, FxFirstStepMap;
		[NotNull]
		public EffectOnlyFloatVariable FxAoPower;
		[NotNull]
		public EffectOnlyFloatArrayVariable FxWeights;
		[NotNull]
		public EffectOnlyVector2Variable FxNoiseSize, FxSourcePixel, FxNearFarValue;
		[NotNull]
		public EffectOnlyVector3Variable FxEyePosW;
		[NotNull]
		public EffectOnlyVectorArrayVariable FxSamplesKernel;

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

			FxCameraProjInv = new EffectOnlyMatrixVariable(E.GetVariableByName("gCameraProjInv"));
			FxCameraProj = new EffectOnlyMatrixVariable(E.GetVariableByName("gCameraProj"));
			FxWorldViewProjInv = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldViewProjInv"));
			FxWorldViewProj = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldViewProj"));
			FxDepthMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDepthMap"));
			FxNormalMap = new EffectOnlyResourceVariable(E.GetVariableByName("gNormalMap"));
			FxNoiseMap = new EffectOnlyResourceVariable(E.GetVariableByName("gNoiseMap"));
			FxFirstStepMap = new EffectOnlyResourceVariable(E.GetVariableByName("gFirstStepMap"));
			FxAoPower = new EffectOnlyFloatVariable(E.GetVariableByName("gAoPower"));
			FxWeights = new EffectOnlyFloatArrayVariable(E.GetVariableByName("gWeights"));
			FxNoiseSize = new EffectOnlyVector2Variable(E.GetVariableByName("gNoiseSize"));
			FxSourcePixel = new EffectOnlyVector2Variable(E.GetVariableByName("gSourcePixel"));
			FxNearFarValue = new EffectOnlyVector2Variable(E.GetVariableByName("gNearFarValue"));
			FxEyePosW = new EffectOnlyVector3Variable(E.GetVariableByName("gEyePosW"));
			FxSamplesKernel = new EffectOnlyVectorArrayVariable(E.GetVariableByName("gSamplesKernel"));
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

		[NotNull]
		public EffectOnlyMatrixVariable FxViewProjInv, FxViewProj;
		[NotNull]
		public EffectOnlyResourceVariable FxDepthMap, FxNormalMap, FxNoiseMap;
		[NotNull]
		public EffectOnlyFloatVariable FxAoPower;
		[NotNull]
		public EffectOnlyVector2Variable FxNoiseSize;
		[NotNull]
		public EffectOnlyVectorArrayVariable FxSamplesKernel;

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "PpSsaoAlt");
			E = new Effect(device, _b);

			TechSsaoVs = new EffectReadyTechnique(E.GetTechniqueByName("SsaoVs"));

			for (var i = 0; i < TechSsaoVs.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechSsaoVs.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (PpSsaoAlt, PT, SsaoVs) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);

			FxViewProjInv = new EffectOnlyMatrixVariable(E.GetVariableByName("gViewProjInv"));
			FxViewProj = new EffectOnlyMatrixVariable(E.GetVariableByName("gViewProj"));
			FxDepthMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDepthMap"));
			FxNormalMap = new EffectOnlyResourceVariable(E.GetVariableByName("gNormalMap"));
			FxNoiseMap = new EffectOnlyResourceVariable(E.GetVariableByName("gNoiseMap"));
			FxAoPower = new EffectOnlyFloatVariable(E.GetVariableByName("gAoPower"));
			FxNoiseSize = new EffectOnlyVector2Variable(E.GetVariableByName("gNoiseSize"));
			FxSamplesKernel = new EffectOnlyVectorArrayVariable(E.GetVariableByName("gSamplesKernel"));
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

		[NotNull]
		public EffectOnlyMatrixVariable FxWorld, FxWorldInvTranspose, FxWorldViewProj;
		[NotNull]
		public EffectOnlyResourceVariable FxDiffuseMap, FxNormalMap, FxMapsMap, FxDetailsMap, FxDetailsNormalMap;
		[NotNull]
		public EffectOnlyVector3Variable FxEyePosW;
		[NotNull]
		public EffectStructStandartMaterialVariable FxMaterial;
		[NotNull]
		public EffectStructReflectiveMaterialVariable FxReflectiveMaterial;
		[NotNull]
		public EffectStructMapsMaterialVariable FxMapsMaterial;
		[NotNull]
		public EffectStructAlphaMaterialVariable FxAlphaMaterial;
		[NotNull]
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

			FxWorld = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorld"));
			FxWorldInvTranspose = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldInvTranspose"));
			FxWorldViewProj = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldViewProj"));
			FxDiffuseMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDiffuseMap"));
			FxNormalMap = new EffectOnlyResourceVariable(E.GetVariableByName("gNormalMap"));
			FxMapsMap = new EffectOnlyResourceVariable(E.GetVariableByName("gMapsMap"));
			FxDetailsMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDetailsMap"));
			FxDetailsNormalMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDetailsNormalMap"));
			FxEyePosW = new EffectOnlyVector3Variable(E.GetVariableByName("gEyePosW"));
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

	public class EffectSpecialAreaLights : IEffectWrapper {
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePC;
        public InputLayout LayoutPC;

		public EffectReadyTechnique TechMain, TechGPass;

		[NotNull]
		public EffectOnlyMatrixVariable FxWorld, FxWorldViewProj;
		[NotNull]
		public EffectOnlyIntVariable FxFlatMirrorSide;
		[NotNull]
		public EffectOnlyBoolVariable FxOverrideColor;
		[NotNull]
		public EffectOnlyVector4Variable FxCustomColor;

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "SpecialAreaLights");
			E = new Effect(device, _b);

			TechMain = new EffectReadyTechnique(E.GetTechniqueByName("Main"));
			TechGPass = new EffectReadyTechnique(E.GetTechniqueByName("GPass"));

			for (var i = 0; i < TechMain.Description.PassCount && InputSignaturePC == null; i++) {
				InputSignaturePC = TechMain.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePC == null) throw new System.Exception("input signature (SpecialAreaLights, PC, Main) == null");
			LayoutPC = new InputLayout(device, InputSignaturePC, InputLayouts.VerticePC.InputElementsValue);

			FxWorld = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorld"));
			FxWorldViewProj = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldViewProj"));
			FxFlatMirrorSide = new EffectOnlyIntVariable(E.GetVariableByName("gFlatMirrorSide"));
			FxOverrideColor = new EffectOnlyBoolVariable(E.GetVariableByName("gOverrideColor"));
			FxCustomColor = new EffectOnlyVector4Variable(E.GetVariableByName("gCustomColor"));
		}

        public void Dispose() {
			if (E == null) return;
			DisposeHelper.Dispose(ref InputSignaturePC);
			DisposeHelper.Dispose(ref LayoutPC);
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

		[NotNull]
		public EffectOnlyMatrixVariable FxWorldViewProj;
		[NotNull]
		public EffectOnlyBoolVariable FxOverrideColor;
		[NotNull]
		public EffectOnlyVector4Variable FxCustomColor;

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "SpecialDebugLines");
			E = new Effect(device, _b);

			TechMain = new EffectReadyTechnique(E.GetTechniqueByName("Main"));

			for (var i = 0; i < TechMain.Description.PassCount && InputSignaturePC == null; i++) {
				InputSignaturePC = TechMain.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePC == null) throw new System.Exception("input signature (SpecialDebugLines, PC, Main) == null");
			LayoutPC = new InputLayout(device, InputSignaturePC, InputLayouts.VerticePC.InputElementsValue);

			FxWorldViewProj = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldViewProj"));
			FxOverrideColor = new EffectOnlyBoolVariable(E.GetVariableByName("gOverrideColor"));
			FxCustomColor = new EffectOnlyVector4Variable(E.GetVariableByName("gCustomColor"));
		}

        public void Dispose() {
			if (E == null) return;
			DisposeHelper.Dispose(ref InputSignaturePC);
			DisposeHelper.Dispose(ref LayoutPC);
			DisposeHelper.Dispose(ref E);
			DisposeHelper.Dispose(ref _b);
        }
	}

	public class EffectSpecialDebugReflections : IEffectWrapper, IEffectMatricesWrapper {
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePNTG;
        public InputLayout LayoutPNTG;

		public EffectReadyTechnique TechMain;

		[NotNull]
		public EffectOnlyMatrixVariable FxWorld, FxWorldInvTranspose, FxWorldViewProj;
		[NotNull]
		public EffectOnlyResourceVariable FxReflectionCubemap;
		[NotNull]
		public EffectOnlyVector3Variable FxEyePosW;

		EffectOnlyMatrixVariable IEffectMatricesWrapper.FxWorld => FxWorld;
		EffectOnlyMatrixVariable IEffectMatricesWrapper.FxWorldInvTranspose => FxWorldInvTranspose;
		EffectOnlyMatrixVariable IEffectMatricesWrapper.FxWorldViewProj => FxWorldViewProj;

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "SpecialDebugReflections");
			E = new Effect(device, _b);

			TechMain = new EffectReadyTechnique(E.GetTechniqueByName("Main"));

			for (var i = 0; i < TechMain.Description.PassCount && InputSignaturePNTG == null; i++) {
				InputSignaturePNTG = TechMain.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePNTG == null) throw new System.Exception("input signature (SpecialDebugReflections, PNTG, Main) == null");
			LayoutPNTG = new InputLayout(device, InputSignaturePNTG, InputLayouts.VerticePNTG.InputElementsValue);

			FxWorld = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorld"));
			FxWorldInvTranspose = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldInvTranspose"));
			FxWorldViewProj = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldViewProj"));
			FxReflectionCubemap = new EffectOnlyResourceVariable(E.GetVariableByName("gReflectionCubemap"));
			FxEyePosW = new EffectOnlyVector3Variable(E.GetVariableByName("gEyePosW"));
		}

        public void Dispose() {
			if (E == null) return;
			DisposeHelper.Dispose(ref InputSignaturePNTG);
			DisposeHelper.Dispose(ref LayoutPNTG);
			DisposeHelper.Dispose(ref E);
			DisposeHelper.Dispose(ref _b);
        }
	}

	public class EffectSpecialPaintShop : IEffectWrapper {
		[StructLayout(LayoutKind.Sequential)]
        public struct ChannelsParams {
            public Vector4 Map;
            public Vector4 Add;
            public Vector4 Multiply;

			public static readonly int Stride = Marshal.SizeOf(typeof(ChannelsParams));
        }

		public class EffectStructChannelsParamsVariable {
			private readonly EffectVariable _v;

			public EffectStructChannelsParamsVariable(EffectVariable v) {
				_v = v;
			}

			public void Set(ChannelsParams value){
				 SlimDxExtension.SetObject(_v, value, ChannelsParams.Stride);
			}
        }

		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectReadyTechnique TechPiece, TechFill, TechFlakes, TechPattern, TechColorfulPattern, TechMask, TechReplacement, TechMaps, TechTint, TechTintMask, TechCombineChannels, TechFindLimitsFirstStep, TechFindLimits, TechNormalizeLimits, TechDesaturate;

		[NotNull]
		public EffectOnlyMatrixVariable FxTransform;
		[NotNull]
		public EffectOnlyResourceVariable FxNoiseMap, FxInputMap, FxAoMap, FxMaskMap, FxOverlayMap, FxUnderlayMap;
		[NotNull]
		public EffectOnlyFloatVariable FxNoiseMultipler, FxFlakes;
		[NotNull]
		public EffectOnlyBoolVariable FxUseMask;
		[NotNull]
		public EffectOnlyVector2Variable FxAlphaAdjustments;
		[NotNull]
		public EffectOnlyVector4Variable FxColor, FxSize;
		[NotNull]
		public EffectOnlyVectorArrayVariable FxColors;
		[NotNull]
		public EffectStructChannelsParamsVariable FxInputParams;
		[NotNull]
		public EffectStructChannelsParamsVariable FxAoParams;
		[NotNull]
		public EffectStructChannelsParamsVariable FxMaskParams;
		[NotNull]
		public EffectStructChannelsParamsVariable FxOverlayParams;
		[NotNull]
		public EffectStructChannelsParamsVariable FxUnderlayParams;

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "SpecialPaintShop");
			E = new Effect(device, _b);

			TechPiece = new EffectReadyTechnique(E.GetTechniqueByName("Piece"));
			TechFill = new EffectReadyTechnique(E.GetTechniqueByName("Fill"));
			TechFlakes = new EffectReadyTechnique(E.GetTechniqueByName("Flakes"));
			TechPattern = new EffectReadyTechnique(E.GetTechniqueByName("Pattern"));
			TechColorfulPattern = new EffectReadyTechnique(E.GetTechniqueByName("ColorfulPattern"));
			TechMask = new EffectReadyTechnique(E.GetTechniqueByName("Mask"));
			TechReplacement = new EffectReadyTechnique(E.GetTechniqueByName("Replacement"));
			TechMaps = new EffectReadyTechnique(E.GetTechniqueByName("Maps"));
			TechTint = new EffectReadyTechnique(E.GetTechniqueByName("Tint"));
			TechTintMask = new EffectReadyTechnique(E.GetTechniqueByName("TintMask"));
			TechCombineChannels = new EffectReadyTechnique(E.GetTechniqueByName("CombineChannels"));
			TechFindLimitsFirstStep = new EffectReadyTechnique(E.GetTechniqueByName("FindLimitsFirstStep"));
			TechFindLimits = new EffectReadyTechnique(E.GetTechniqueByName("FindLimits"));
			TechNormalizeLimits = new EffectReadyTechnique(E.GetTechniqueByName("NormalizeLimits"));
			TechDesaturate = new EffectReadyTechnique(E.GetTechniqueByName("Desaturate"));

			for (var i = 0; i < TechPiece.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechPiece.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (SpecialPaintShop, PT, Piece) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);

			FxTransform = new EffectOnlyMatrixVariable(E.GetVariableByName("gTransform"));
			FxNoiseMap = new EffectOnlyResourceVariable(E.GetVariableByName("gNoiseMap"));
			FxInputMap = new EffectOnlyResourceVariable(E.GetVariableByName("gInputMap"));
			FxAoMap = new EffectOnlyResourceVariable(E.GetVariableByName("gAoMap"));
			FxMaskMap = new EffectOnlyResourceVariable(E.GetVariableByName("gMaskMap"));
			FxOverlayMap = new EffectOnlyResourceVariable(E.GetVariableByName("gOverlayMap"));
			FxUnderlayMap = new EffectOnlyResourceVariable(E.GetVariableByName("gUnderlayMap"));
			FxNoiseMultipler = new EffectOnlyFloatVariable(E.GetVariableByName("gNoiseMultipler"));
			FxFlakes = new EffectOnlyFloatVariable(E.GetVariableByName("gFlakes"));
			FxUseMask = new EffectOnlyBoolVariable(E.GetVariableByName("gUseMask"));
			FxAlphaAdjustments = new EffectOnlyVector2Variable(E.GetVariableByName("gAlphaAdjustments"));
			FxColor = new EffectOnlyVector4Variable(E.GetVariableByName("gColor"));
			FxSize = new EffectOnlyVector4Variable(E.GetVariableByName("gSize"));
			FxColors = new EffectOnlyVectorArrayVariable(E.GetVariableByName("gColors"));
			FxInputParams = new EffectStructChannelsParamsVariable(E.GetVariableByName("gInputParams"));
			FxAoParams = new EffectStructChannelsParamsVariable(E.GetVariableByName("gAoParams"));
			FxMaskParams = new EffectStructChannelsParamsVariable(E.GetVariableByName("gMaskParams"));
			FxOverlayParams = new EffectStructChannelsParamsVariable(E.GetVariableByName("gOverlayParams"));
			FxUnderlayParams = new EffectStructChannelsParamsVariable(E.GetVariableByName("gUnderlayParams"));
		}

        public void Dispose() {
			if (E == null) return;
			DisposeHelper.Dispose(ref InputSignaturePT);
			DisposeHelper.Dispose(ref LayoutPT);
			DisposeHelper.Dispose(ref E);
			DisposeHelper.Dispose(ref _b);
        }
	}

	public class EffectSpecialPiecesBlender : IEffectWrapper {
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectReadyTechnique TechBlend;

		[NotNull]
		public EffectOnlyResourceArrayVariable FxInputMaps;
		[NotNull]
		public EffectOnlyVector2Variable FxPaddingSize, FxTexMultiplier;

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "SpecialPiecesBlender");
			E = new Effect(device, _b);

			TechBlend = new EffectReadyTechnique(E.GetTechniqueByName("Blend"));

			for (var i = 0; i < TechBlend.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechBlend.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (SpecialPiecesBlender, PT, Blend) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, InputLayouts.VerticePT.InputElementsValue);

			FxInputMaps = new EffectOnlyResourceArrayVariable(E.GetVariableByName("gInputMaps"));
			FxPaddingSize = new EffectOnlyVector2Variable(E.GetVariableByName("gPaddingSize"));
			FxTexMultiplier = new EffectOnlyVector2Variable(E.GetVariableByName("gTexMultiplier"));
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

		[NotNull]
		public EffectOnlyMatrixVariable FxShadowViewProj, FxWorldViewProj, FxWorld, FxWorldInvTranspose;
		[NotNull]
		public EffectOnlyResourceVariable FxInputMap, FxDepthMap, FxAlphaMap, FxNormalMap;
		[NotNull]
		public EffectOnlyFloatVariable FxMultipler, FxGamma, FxCount, FxAmbient, FxPadding, FxFade, FxAlphaRef, FxNormalUvMult;
		[NotNull]
		public EffectOnlyVector2Variable FxShadowSize, FxOffset;
		[NotNull]
		public EffectOnlyVector3Variable FxLightDir;
		[NotNull]
		public EffectOnlyVector4Variable FxSize;

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

			FxShadowViewProj = new EffectOnlyMatrixVariable(E.GetVariableByName("gShadowViewProj"));
			FxWorldViewProj = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldViewProj"));
			FxWorld = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorld"));
			FxWorldInvTranspose = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldInvTranspose"));
			FxInputMap = new EffectOnlyResourceVariable(E.GetVariableByName("gInputMap"));
			FxDepthMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDepthMap"));
			FxAlphaMap = new EffectOnlyResourceVariable(E.GetVariableByName("gAlphaMap"));
			FxNormalMap = new EffectOnlyResourceVariable(E.GetVariableByName("gNormalMap"));
			FxMultipler = new EffectOnlyFloatVariable(E.GetVariableByName("gMultipler"));
			FxGamma = new EffectOnlyFloatVariable(E.GetVariableByName("gGamma"));
			FxCount = new EffectOnlyFloatVariable(E.GetVariableByName("gCount"));
			FxAmbient = new EffectOnlyFloatVariable(E.GetVariableByName("gAmbient"));
			FxPadding = new EffectOnlyFloatVariable(E.GetVariableByName("gPadding"));
			FxFade = new EffectOnlyFloatVariable(E.GetVariableByName("gFade"));
			FxAlphaRef = new EffectOnlyFloatVariable(E.GetVariableByName("gAlphaRef"));
			FxNormalUvMult = new EffectOnlyFloatVariable(E.GetVariableByName("gNormalUvMult"));
			FxShadowSize = new EffectOnlyVector2Variable(E.GetVariableByName("gShadowSize"));
			FxOffset = new EffectOnlyVector2Variable(E.GetVariableByName("gOffset"));
			FxLightDir = new EffectOnlyVector3Variable(E.GetVariableByName("gLightDir"));
			FxSize = new EffectOnlyVector4Variable(E.GetVariableByName("gSize"));
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

		[NotNull]
		public EffectOnlyMatrixVariable FxWorldViewProj;
		[NotNull]
		public EffectOnlyResourceVariable FxInputMap;
		[NotNull]
		public EffectOnlyVector4Variable FxScreenSize;

		EffectOnlyVector4Variable IEffectScreenSizeWrapper.FxScreenSize => FxScreenSize;

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

			FxWorldViewProj = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldViewProj"));
			FxInputMap = new EffectOnlyResourceVariable(E.GetVariableByName("gInputMap"));
			FxScreenSize = new EffectOnlyVector4Variable(E.GetVariableByName("gScreenSize"));
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

		[NotNull]
		public EffectOnlyMatrixVariable FxMatrix, FxWorldViewProj;
		[NotNull]
		public EffectOnlyResourceVariable FxInputMap, FxBgMap;
		[NotNull]
		public EffectOnlyFloatVariable FxExtraWidth, FxDropShadowRadius;
		[NotNull]
		public EffectOnlyVector4Variable FxScreenSize, FxBlendColor;

		EffectOnlyVector4Variable IEffectScreenSizeWrapper.FxScreenSize => FxScreenSize;

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

			FxMatrix = new EffectOnlyMatrixVariable(E.GetVariableByName("gMatrix"));
			FxWorldViewProj = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldViewProj"));
			FxInputMap = new EffectOnlyResourceVariable(E.GetVariableByName("gInputMap"));
			FxBgMap = new EffectOnlyResourceVariable(E.GetVariableByName("gBgMap"));
			FxExtraWidth = new EffectOnlyFloatVariable(E.GetVariableByName("gExtraWidth"));
			FxDropShadowRadius = new EffectOnlyFloatVariable(E.GetVariableByName("gDropShadowRadius"));
			FxScreenSize = new EffectOnlyVector4Variable(E.GetVariableByName("gScreenSize"));
			FxBlendColor = new EffectOnlyVector4Variable(E.GetVariableByName("gBlendColor"));
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

		[NotNull]
		public EffectOnlyVector2Variable FxOffset;

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "SpecialUv");
			E = new Effect(device, _b);

			TechMain = new EffectReadyTechnique(E.GetTechniqueByName("Main"));

			for (var i = 0; i < TechMain.Description.PassCount && InputSignaturePNTG == null; i++) {
				InputSignaturePNTG = TechMain.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePNTG == null) throw new System.Exception("input signature (SpecialUv, PNTG, Main) == null");
			LayoutPNTG = new InputLayout(device, InputSignaturePNTG, InputLayouts.VerticePNTG.InputElementsValue);

			FxOffset = new EffectOnlyVector2Variable(E.GetVariableByName("gOffset"));
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

		[NotNull]
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

			FxTex = new EffectOnlyResourceVariable(E.GetVariableByName("Tex"));
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

		[NotNull]
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

			FxWorldViewProj = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldViewProj"));
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

		[NotNull]
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

			FxWorldViewProj = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldViewProj"));
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
