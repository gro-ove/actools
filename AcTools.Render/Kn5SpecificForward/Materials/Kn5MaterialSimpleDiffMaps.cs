using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Materials;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5SpecificForward.Materials {
    public class Kn5MaterialSimpleDiffMaps : Kn5MaterialSimpleReflective {
        public Kn5MaterialSimpleDiffMaps([NotNull] Kn5MaterialDescription description) : base(description) { }

        public override void Draw(IDeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
            Effect.TechDiffMaps.DrawAllPasses(contextHolder.DeviceContext, indices);
        }
    }
}