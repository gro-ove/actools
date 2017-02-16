using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Utils;
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

        public override void Draw(IDeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
            Effect.TechCollider.DrawAllPasses(contextHolder.DeviceContext, indices);
            contextHolder.DeviceContext.OutputMerger.BlendState = null;
            contextHolder.DeviceContext.OutputMerger.DepthStencilState = null;
        }
    }
}