using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Kn5Specific.Textures;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5SpecificForward.Materials {
    public class Kn5MaterialSimpleNm : Kn5MaterialSimpleReflective {
        private IRenderableTexture _txNormal;

        public Kn5MaterialSimpleNm([NotNull] string kn5Filename, [NotNull] Kn5Material material) : base(kn5Filename, material) { }

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
            Effect.TechNm.DrawAllPasses(contextHolder.DeviceContext, indices);
        }
    }
}