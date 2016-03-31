/* GENERATED AUTOMATICALLY */
/* DON'T MODIFY */

using SlimDX.D3DCompiler;
using SlimDX.Direct3D11;
using Device = SlimDX.Direct3D11.Device;
// ReSharper disable InconsistentNaming

namespace AcTools.Kn5Render.Kn5Render.Effects {
	public static class EffectUtils {
		internal static ShaderBytecode Compile(byte[] data, string name = "") {
            try {
                return ShaderBytecode.Compile(data, "Render", "fx_5_0", ShaderFlags.None, EffectFlags.None);
            } catch (System.Exception e) {
                System.Windows.Forms.MessageBox.Show("Shader " + (name ?? "?") + " compilation failed:\n\n" + e.Message);
                throw;
            }
        }
	}

	public class EffectScreen : System.IDisposable {
        public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectTechnique TechCopy, TechHorzBlur, TechVertBlur;

		public EffectResourceVariable FxInputImage;
		public EffectVectorVariable FxTexel;

		public EffectScreen(Device device) {
            using (var bc = EffectUtils.Compile(Properties.Resources.EffectScreen, "Screen")){
                E = new Effect(device, bc);
			}

			TechCopy = E.GetTechniqueByName("Copy");
			TechHorzBlur = E.GetTechniqueByName("HorzBlur");
			TechVertBlur = E.GetTechniqueByName("VertBlur");

			for (var i = 0; i < TechCopy.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechCopy.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (Screen, PT, Copy) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, VerticePT.InputElements);

			FxInputImage = E.GetVariableByName("gInputImage").AsResource();
			FxTexel = E.GetVariableByName("gTexel").AsVector();
		}

        public void Dispose() {
            E.Dispose();
			InputSignaturePT.Dispose();
            LayoutPT.Dispose();
        }

        public void Dispose(bool b) {
            if (b) Dispose();
        }
	}

	public class EffectShotBodyShadow : System.IDisposable {
        public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectTechnique TechCreateBodyShadow, TechHorzBodyShadowBlur, TechVertBodyShadowBlur;

		public EffectResourceVariable FxInputImage;
		public EffectVectorVariable FxTexel;

		public EffectShotBodyShadow(Device device) {
            using (var bc = EffectUtils.Compile(Properties.Resources.EffectShotBodyShadow, "ShotBodyShadow")){
                E = new Effect(device, bc);
			}

			TechCreateBodyShadow = E.GetTechniqueByName("CreateBodyShadow");
			TechHorzBodyShadowBlur = E.GetTechniqueByName("HorzBodyShadowBlur");
			TechVertBodyShadowBlur = E.GetTechniqueByName("VertBodyShadowBlur");

			for (var i = 0; i < TechCreateBodyShadow.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechCreateBodyShadow.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (ShotBodyShadow, PT, CreateBodyShadow) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, VerticePT.InputElements);

			FxInputImage = E.GetVariableByName("gInputImage").AsResource();
			FxTexel = E.GetVariableByName("gTexel").AsVector();
		}

        public void Dispose() {
            E.Dispose();
			InputSignaturePT.Dispose();
            LayoutPT.Dispose();
        }

        public void Dispose(bool b) {
            if (b) Dispose();
        }
	}

	public class EffectShotTrackMap : System.IDisposable {
        public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectTechnique TechTrackMap;

		public EffectResourceVariable FxInputImage;

		public EffectShotTrackMap(Device device) {
            using (var bc = EffectUtils.Compile(Properties.Resources.EffectShotTrackMap, "ShotTrackMap")){
                E = new Effect(device, bc);
			}

			TechTrackMap = E.GetTechniqueByName("TrackMap");

			for (var i = 0; i < TechTrackMap.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechTrackMap.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (ShotTrackMap, PT, TrackMap) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, VerticePT.InputElements);

			FxInputImage = E.GetVariableByName("gInputImage").AsResource();
		}

        public void Dispose() {
            E.Dispose();
			InputSignaturePT.Dispose();
            LayoutPT.Dispose();
        }

        public void Dispose(bool b) {
            if (b) Dispose();
        }
	}

	public class EffectShotWheelShadow : System.IDisposable {
        public Effect E;

        public ShaderSignature InputSignaturePT;
        public InputLayout LayoutPT;

		public EffectTechnique TechCreateWheelShadow, TechHorzWheelShadowBlur, TechVertWheelShadowBlur;

		public EffectResourceVariable FxInputImage;
		public EffectVectorVariable FxTexel;

		public EffectShotWheelShadow(Device device) {
            using (var bc = EffectUtils.Compile(Properties.Resources.EffectShotWheelShadow, "ShotWheelShadow")){
                E = new Effect(device, bc);
			}

			TechCreateWheelShadow = E.GetTechniqueByName("CreateWheelShadow");
			TechHorzWheelShadowBlur = E.GetTechniqueByName("HorzWheelShadowBlur");
			TechVertWheelShadowBlur = E.GetTechniqueByName("VertWheelShadowBlur");

			for (var i = 0; i < TechCreateWheelShadow.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechCreateWheelShadow.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (ShotWheelShadow, PT, CreateWheelShadow) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, VerticePT.InputElements);

			FxInputImage = E.GetVariableByName("gInputImage").AsResource();
			FxTexel = E.GetVariableByName("gTexel").AsVector();
		}

        public void Dispose() {
            E.Dispose();
			InputSignaturePT.Dispose();
            LayoutPT.Dispose();
        }

        public void Dispose(bool b) {
            if (b) Dispose();
        }
	}

	public class EffectTest : System.IDisposable {
        public Effect E;

        public ShaderSignature InputSignaturePNT, InputSignaturePT;
        public InputLayout LayoutPNT, LayoutPT;

		public EffectTechnique TechCar, TechSimple, TechShadow;

		public EffectMatrixVariable FxWorld, FxWorldInvTranspose, FxWorldViewProj;
		public EffectResourceVariable FxDiffuseMap, FxDetailsMap, FxNormalMap, FxMapMap, FxCubeMap;
		public EffectVectorVariable FxEyePosW;
		public EffectVariable FxMaterial, FxDirLight;

		public EffectTest(Device device) {
            using (var bc = EffectUtils.Compile(Properties.Resources.EffectTest, "Test")){
                E = new Effect(device, bc);
			}

			TechCar = E.GetTechniqueByName("Car");
			TechSimple = E.GetTechniqueByName("Simple");
			TechShadow = E.GetTechniqueByName("Shadow");

			for (var i = 0; i < TechCar.Description.PassCount && InputSignaturePNT == null; i++) {
				InputSignaturePNT = TechCar.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePNT == null) throw new System.Exception("input signature (Test, PNT, Car) == null");
			LayoutPNT = new InputLayout(device, InputSignaturePNT, VerticePNT.InputElements);
			for (var i = 0; i < TechSimple.Description.PassCount && InputSignaturePT == null; i++) {
				InputSignaturePT = TechSimple.GetPassByIndex(i).Description.Signature;
			}
			if (InputSignaturePT == null) throw new System.Exception("input signature (Test, PT, Simple) == null");
			LayoutPT = new InputLayout(device, InputSignaturePT, VerticePT.InputElements);

			FxWorld = E.GetVariableByName("gWorld").AsMatrix();
			FxWorldInvTranspose = E.GetVariableByName("gWorldInvTranspose").AsMatrix();
			FxWorldViewProj = E.GetVariableByName("gWorldViewProj").AsMatrix();
			FxDiffuseMap = E.GetVariableByName("gDiffuseMap").AsResource();
			FxDetailsMap = E.GetVariableByName("gDetailsMap").AsResource();
			FxNormalMap = E.GetVariableByName("gNormalMap").AsResource();
			FxMapMap = E.GetVariableByName("gMapMap").AsResource();
			FxCubeMap = E.GetVariableByName("gCubeMap").AsResource();
			FxEyePosW = E.GetVariableByName("gEyePosW").AsVector();
			FxMaterial = E.GetVariableByName("gMaterial");
			FxDirLight = E.GetVariableByName("gDirLight");
		}

        public void Dispose() {
            E.Dispose();
			InputSignaturePNT.Dispose();
            LayoutPNT.Dispose();
			InputSignaturePT.Dispose();
            LayoutPT.Dispose();
        }

        public void Dispose(bool b) {
            if (b) Dispose();
        }
	}


}
