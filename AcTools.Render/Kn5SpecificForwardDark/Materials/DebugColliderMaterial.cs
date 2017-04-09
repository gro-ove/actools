using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Kn5Specific.Materials;

namespace AcTools.Render.Kn5SpecificForwardDark.Materials {
    public class DebugColliderMaterial : Kn5MaterialSimpleBase {
        public DebugColliderMaterial() : base(new Kn5MaterialDescription(new Kn5Material {
            BlendMode = Kn5MaterialBlendMode.AlphaBlend,
            DepthMode = Kn5MaterialDepthMode.DepthOff
        })) {}

        public override bool Prepare(IDeviceContextHolder contextHolder, SpecialRenderMode mode) {
            if (!base.Prepare(contextHolder, mode)) return false;
            contextHolder.DeviceContext.OutputMerger.BlendState = contextHolder.States.TransparentBlendState;
            contextHolder.DeviceContext.OutputMerger.DepthStencilState = contextHolder.States.DisabledDepthState;
            return true;
        }

        protected override EffectReadyTechnique GetTechnique() {
            return Effect.TechCollider;
        }

        public override void Draw(IDeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
            base.Draw(contextHolder, indices, mode);
            contextHolder.DeviceContext.OutputMerger.BlendState = null;
            contextHolder.DeviceContext.OutputMerger.DepthStencilState = null;
        }
    }
}