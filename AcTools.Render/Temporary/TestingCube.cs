using System.Linq;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Structs;
using AcTools.Render.Base.Utils;
using AcTools.Render.Shaders;
using SlimDX;

namespace AcTools.Render.Temporary {
    public class TestingCube : TrianglesRenderableObject<InputLayouts.VerticePC> {
        private EffectTestingCube _effectMiniCube;

        public TestingCube() : base(null, new[] {
            new InputLayouts.VerticePC(new Vector3(-1.0f, -1.0f, -1.0f), new Vector4(1.0f, 0.0f, 0.0f, 1.0f)),
            new InputLayouts.VerticePC(new Vector3(-1.0f, 1.0f, -1.0f), new Vector4(1.0f, 0.0f, 0.0f, 1.0f)),
            new InputLayouts.VerticePC(new Vector3(1.0f, 1.0f, -1.0f), new Vector4(1.0f, 0.0f, 0.0f, 1.0f)),
            new InputLayouts.VerticePC(new Vector3(-1.0f, -1.0f, -1.0f), new Vector4(1.0f, 0.0f, 0.0f, 1.0f)),
            new InputLayouts.VerticePC(new Vector3(1.0f, 1.0f, -1.0f), new Vector4(1.0f, 0.0f, 0.0f, 1.0f)),
            new InputLayouts.VerticePC(new Vector3(1.0f, -1.0f, -1.0f), new Vector4(1.0f, 0.0f, 0.0f, 1.0f)),

            new InputLayouts.VerticePC(new Vector3(-1.0f, -1.0f, 1.0f), new Vector4(0.0f, 1.0f, 0.0f, 1.0f)),
            new InputLayouts.VerticePC(new Vector3(1.0f, 1.0f, 1.0f), new Vector4(0.0f, 1.0f, 0.0f, 1.0f)),
            new InputLayouts.VerticePC(new Vector3(-1.0f, 1.0f, 1.0f), new Vector4(0.0f, 1.0f, 0.0f, 1.0f)),
            new InputLayouts.VerticePC(new Vector3(-1.0f, -1.0f, 1.0f), new Vector4(0.0f, 1.0f, 0.0f, 1.0f)),
            new InputLayouts.VerticePC(new Vector3(1.0f, -1.0f, 1.0f), new Vector4(0.0f, 1.0f, 0.0f, 1.0f)),
            new InputLayouts.VerticePC(new Vector3(1.0f, 1.0f, 1.0f), new Vector4(0.0f, 1.0f, 0.0f, 1.0f)),

            new InputLayouts.VerticePC(new Vector3(-1.0f, 1.0f, -1.0f), new Vector4(0.0f, 0.0f, 1.0f, 1.0f)),
            new InputLayouts.VerticePC(new Vector3(-1.0f, 1.0f, 1.0f), new Vector4(0.0f, 0.0f, 1.0f, 1.0f)),
            new InputLayouts.VerticePC(new Vector3(1.0f, 1.0f, 1.0f), new Vector4(0.0f, 0.0f, 1.0f, 1.0f)),
            new InputLayouts.VerticePC(new Vector3(-1.0f, 1.0f, -1.0f), new Vector4(0.0f, 0.0f, 1.0f, 1.0f)),
            new InputLayouts.VerticePC(new Vector3(1.0f, 1.0f, 1.0f), new Vector4(0.0f, 0.0f, 1.0f, 1.0f)),
            new InputLayouts.VerticePC(new Vector3(1.0f, 1.0f, -1.0f), new Vector4(0.0f, 0.0f, 1.0f, 1.0f)),

            new InputLayouts.VerticePC(new Vector3(-1.0f, -1.0f, -1.0f), new Vector4(1.0f, 1.0f, 0.0f, 1.0f)),
            new InputLayouts.VerticePC(new Vector3(1.0f, -1.0f, 1.0f), new Vector4(1.0f, 1.0f, 0.0f, 1.0f)),
            new InputLayouts.VerticePC(new Vector3(-1.0f, -1.0f, 1.0f), new Vector4(1.0f, 1.0f, 0.0f, 1.0f)),
            new InputLayouts.VerticePC(new Vector3(-1.0f, -1.0f, -1.0f), new Vector4(1.0f, 1.0f, 0.0f, 1.0f)),
            new InputLayouts.VerticePC(new Vector3(1.0f, -1.0f, -1.0f), new Vector4(1.0f, 1.0f, 0.0f, 1.0f)),
            new InputLayouts.VerticePC(new Vector3(1.0f, -1.0f, 1.0f), new Vector4(1.0f, 1.0f, 0.0f, 1.0f)),

            new InputLayouts.VerticePC(new Vector3(-1.0f, -1.0f, -1.0f), new Vector4(1.0f, 0.0f, 1.0f, 1.0f)),
            new InputLayouts.VerticePC(new Vector3(-1.0f, -1.0f, 1.0f), new Vector4(1.0f, 0.0f, 1.0f, 1.0f)),
            new InputLayouts.VerticePC(new Vector3(-1.0f, 1.0f, 1.0f), new Vector4(1.0f, 0.0f, 1.0f, 1.0f)),
            new InputLayouts.VerticePC(new Vector3(-1.0f, -1.0f, -1.0f), new Vector4(1.0f, 0.0f, 1.0f, 1.0f)),
            new InputLayouts.VerticePC(new Vector3(-1.0f, 1.0f, 1.0f), new Vector4(1.0f, 0.0f, 1.0f, 1.0f)),
            new InputLayouts.VerticePC(new Vector3(-1.0f, 1.0f, -1.0f), new Vector4(1.0f, 0.0f, 1.0f, 1.0f)),

            new InputLayouts.VerticePC(new Vector3(1.0f, -1.0f, -1.0f), new Vector4(0.0f, 1.0f, 1.0f, 1.0f)),
            new InputLayouts.VerticePC(new Vector3(1.0f, 1.0f, 1.0f), new Vector4(0.0f, 1.0f, 1.0f, 1.0f)),
            new InputLayouts.VerticePC(new Vector3(1.0f, -1.0f, 1.0f), new Vector4(0.0f, 1.0f, 1.0f, 1.0f)),
            new InputLayouts.VerticePC(new Vector3(1.0f, -1.0f, -1.0f), new Vector4(0.0f, 1.0f, 1.0f, 1.0f)),
            new InputLayouts.VerticePC(new Vector3(1.0f, 1.0f, -1.0f), new Vector4(0.0f, 1.0f, 1.0f, 1.0f)),
            new InputLayouts.VerticePC(new Vector3(1.0f, 1.0f, 1.0f), new Vector4(0.0f, 1.0f, 1.0f, 1.0f)),
        }, Enumerable.Range(0, 36).Select(x => (ushort)x).ToArray()) {}

        protected override void Initialize(DeviceContextHolder contextHolder) {
            base.Initialize(contextHolder);
            _effectMiniCube = contextHolder.GetEffect<EffectTestingCube>();
        }

        protected override void DrawInner(DeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode) {
            if (mode != SpecialRenderMode.Simple) return;

            contextHolder.DeviceContext.InputAssembler.InputLayout = _effectMiniCube.LayoutPC;
            base.DrawInner(contextHolder, camera, mode);

            _effectMiniCube.FxWorldViewProj.SetMatrix(ParentMatrix * camera.ViewProj);
            _effectMiniCube.TechCube.DrawAllPasses(contextHolder.DeviceContext, Indices.Length);
        }

        public override void Dispose() {
            base.Dispose();
            _effectMiniCube?.Dispose();
        }
    }
}