using AcTools.Render.Base;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Materials;
using JetBrains.Annotations;

namespace ArcadeCorsa.Render.DarkRenderer.Materials {
    public class Kn5MaterialSimpleGl : Kn5MaterialSimpleBase {
        public Kn5MaterialSimpleGl([NotNull] Kn5MaterialDescription description) : base(description) { }

        public override void Draw(IDeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
            Effect.TechGl.DrawAllPasses(contextHolder.DeviceContext, indices);
        }
    }
}