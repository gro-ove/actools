/* GENERATED AUTOMATICALLY */
/* DON’T MODIFY */

// ReSharper disable RedundantUsingDirective
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

namespace CustomTracksBakery.Shaders {
	internal static class ShadersResourceManager {
		internal static readonly ResourceManager Manager = new ResourceManager("CustomTracksBakery.Shaders", Assembly.GetExecutingAssembly());
	}

	public class EffectBakeryShaders : IEffectWrapper {
		private ShaderBytecode _b;
		public Effect E;

        public ShaderSignature InputSignaturePNTG;
        public InputLayout LayoutPNTG;

		public EffectReadyTechnique TechPerPixel, TechMultiLayer, TechPerPixel_SecondPass, TechMultiLayer_SecondPass;

		[NotNull]
		public EffectOnlyMatrixVariable FxWorldViewProj;
		[NotNull]
		public EffectOnlyResourceVariable FxDiffuseMap, FxMaskMap, FxDetailRMap, FxDetailGMap, FxDetailBMap, FxDetailAMap, FxAlphaMap;
		[NotNull]
		public EffectOnlyFloatVariable FxKsDiffuse, FxAlphaRef, FxMagicMult, FxSecondPassMode;
		[NotNull]
		public EffectOnlyVector4Variable FxMultRGBA;

		public void Initialize(Device device) {
			_b = EffectUtils.Load(ShadersResourceManager.Manager, "BakeryShaders");
			E = new Effect(device, _b);

			TechPerPixel = new EffectReadyTechnique(E.GetTechniqueByName("PerPixel"));
			TechMultiLayer = new EffectReadyTechnique(E.GetTechniqueByName("MultiLayer"));
			TechPerPixel_SecondPass = new EffectReadyTechnique(E.GetTechniqueByName("PerPixel_SecondPass"));
			TechMultiLayer_SecondPass = new EffectReadyTechnique(E.GetTechniqueByName("MultiLayer_SecondPass"));

			for (var i = 0; i < TechPerPixel.Description.PassCount && InputSignaturePNTG == null; i++) {
				InputSignaturePNTG = TechPerPixel.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePNTG == null) throw new System.Exception("input signature (BakeryShaders, PNTG, PerPixel) == null");
			LayoutPNTG = new InputLayout(device, InputSignaturePNTG, InputLayouts.VerticePNTG.InputElementsValue);

			FxWorldViewProj = new EffectOnlyMatrixVariable(E.GetVariableByName("gWorldViewProj"));
			FxDiffuseMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDiffuseMap"));
			FxMaskMap = new EffectOnlyResourceVariable(E.GetVariableByName("gMaskMap"));
			FxDetailRMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDetailRMap"));
			FxDetailGMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDetailGMap"));
			FxDetailBMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDetailBMap"));
			FxDetailAMap = new EffectOnlyResourceVariable(E.GetVariableByName("gDetailAMap"));
			FxAlphaMap = new EffectOnlyResourceVariable(E.GetVariableByName("gAlphaMap"));
			FxKsDiffuse = new EffectOnlyFloatVariable(E.GetVariableByName("gKsDiffuse"));
			FxAlphaRef = new EffectOnlyFloatVariable(E.GetVariableByName("gAlphaRef"));
			FxMagicMult = new EffectOnlyFloatVariable(E.GetVariableByName("gMagicMult"));
			FxSecondPassMode = new EffectOnlyFloatVariable(E.GetVariableByName("gSecondPassMode"));
			FxMultRGBA = new EffectOnlyVector4Variable(E.GetVariableByName("gMultRGBA"));
		}

        public void Dispose() {
			if (E == null) return;
			DisposeHelper.Dispose(ref InputSignaturePNTG);
			DisposeHelper.Dispose(ref LayoutPNTG);
			DisposeHelper.Dispose(ref E);
			DisposeHelper.Dispose(ref _b);
        }
	}

}
