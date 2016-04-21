using System.Linq;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Camera;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Structs;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Materials;

namespace AcTools.Render.Kn5Specific.Objects {
    public class Kn5RenderableObject : TrianglesRenderableObject<InputLayouts.VerticePNTG> {
        public readonly Kn5Node OriginalNode;
        private readonly IRenderableMaterial _material;

        public Kn5RenderableObject(Kn5Node node)
            : base(node.Vertices.Select(x => new InputLayouts.VerticePNTG(
                                                 x.Co.ToVector3FixX(),
                                                 x.Normal.ToVector3FixX(),
                                                 x.Uv.ToVector2(),
                                                 x.Tangent.ToVector3FixX()
                                                 )).ToArray(),
                   node.Indices.ToIndicesFixX()) {
            OriginalNode = node;
            _material = Kn5MaterialsProvider.GetMaterial(OriginalNode.MaterialId);
        }

        protected override void Initialize(DeviceContextHolder contextHolder) {
            base.Initialize(contextHolder);
            _material.Initialize(contextHolder);
        }

        protected override void DrawInner(DeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode) {
            if (!OriginalNode.Active || !OriginalNode.IsVisible || !OriginalNode.IsRenderable || (OriginalNode.IsTransparent && mode != SpecialRenderMode.Transparent && _material.)) return;

            _material.Prepare(contextHolder, mode);
            base.DrawInner(contextHolder, camera, mode);

            _material.SetMatrices(ParentMatrix, camera);
            _material.Draw(contextHolder, Indices.Length, mode);
        }

        public override void Dispose() {
            base.Dispose();
            _material.Dispose();
        }
    }
}
