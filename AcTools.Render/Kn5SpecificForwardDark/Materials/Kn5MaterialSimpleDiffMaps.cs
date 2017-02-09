using AcTools.Render.Base;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Kn5Specific.Textures;
using JetBrains.Annotations;

namespace ArcadeCorsa.Render.DarkRenderer.Materials {
    public class Kn5MaterialSimpleDiffMaps : Kn5MaterialSimpleReflective {
        private IRenderableTexture _txNormal;

        public Kn5MaterialSimpleDiffMaps([NotNull] Kn5MaterialDescription description) : base(description) { }

        public override void Draw(IDeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
            Effect.TechDiffMaps.DrawAllPasses(contextHolder.DeviceContext, indices);
        }

        public override void Initialize(IDeviceContextHolder contextHolder) {
            _txNormal = GetTexture("txNormal", contextHolder);
            base.Initialize(contextHolder);
        }

        public override bool Prepare(IDeviceContextHolder contextHolder, SpecialRenderMode mode) {
            if (!base.Prepare(contextHolder, mode)) return false;

            Effect.FxNormalMap.SetResource(_txNormal);
            return true;
        }
    }
}