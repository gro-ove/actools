using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Structs;
using AcTools.Render.Kn5Specific.Materials;
using SlimDX;

namespace AcTools.Render.Kn5Specific.Objects {
    public class AmbientShadow : TrianglesRenderableObject<InputLayouts.VerticePT> {
        private static readonly InputLayouts.VerticePT[] BaseVertices;
        private static readonly ushort[] BaseIndices;

        static AmbientShadow() {
            BaseVertices = new InputLayouts.VerticePT[4];
            for (var i = 0; i < BaseVertices.Length; i++) {
                BaseVertices[i] = new InputLayouts.VerticePT(
                    new Vector3(i < 2 ? 1 : -1, 0, i % 2 == 0 ? -1 : 1),
                    new Vector2(i < 2 ? 1 : 0, i % 2)
                );
            }

            BaseIndices = new ushort[] { 0, 2, 1, 3, 1, 2 };
        }

        private readonly string _filename;
        private IRenderableMaterial _material;

        public Matrix Transform;

        public AmbientShadow(string filename, Matrix transform)
                : base(BaseVertices, BaseIndices) {
            _filename = filename;
            Transform = transform;
        }

        protected override void Initialize(DeviceContextHolder contextHolder) {
            base.Initialize(contextHolder);

            _material = contextHolder.Get<Kn5MaterialsProvider>().GetAmbientShadowMaterial(_filename);
            _material.Initialize(contextHolder);
        }

        protected override void DrawInner(DeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode) {
            if (!_material.Prepare(contextHolder, mode)) return;
            base.DrawInner(contextHolder, camera, mode);

            _material.SetMatrices(Transform * ParentMatrix, camera);
            _material.Draw(contextHolder, Indices.Length, mode);
        }

        public override void Dispose() {
            base.Dispose();
            _material?.Dispose();
        }
    }
}
