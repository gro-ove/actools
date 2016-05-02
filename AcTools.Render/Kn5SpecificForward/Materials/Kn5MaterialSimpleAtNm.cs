using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Kn5Specific.Textures;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5SpecificForward.Materials {
    public class Kn5MaterialSimpleAtNm : Kn5MaterialSimple {
        private IRenderableTexture _txNormal;

        public Kn5MaterialSimpleAtNm([NotNull] string kn5Filename, [NotNull] Kn5Material material) : base(kn5Filename, material) {}

        public override void Initialize(DeviceContextHolder contextHolder) {
            _txNormal = GetTexture("txNormal", contextHolder);
            base.Initialize(contextHolder);
        }

        public override bool Prepare(DeviceContextHolder contextHolder, SpecialRenderMode mode) {
            if (!base.Prepare(contextHolder, mode)) return false;

            Effect.FxNormalMap.SetResource(_txNormal);
            return true;
        }

        public override void Draw(DeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
            Effect.TechAtNm.DrawAllPasses(contextHolder.DeviceContext, indices);
        }
    }
}