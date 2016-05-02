using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Utils;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5SpecificForward.Materials {
    public class Kn5MaterialSimpleDiffMaps : Kn5MaterialSimpleReflective {
        public Kn5MaterialSimpleDiffMaps([NotNull] string kn5Filename, [NotNull] Kn5Material material) : base(kn5Filename, material) { }

        public override void Draw(DeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
            Effect.TechDiffMaps.DrawAllPasses(contextHolder.DeviceContext, indices);
        }
    }
}