using System.Linq;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Structs;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Kn5Specific.Textures;
using SlimDX;

namespace AcTools.Render.Kn5Specific.Objects {
    public class SkyObject : TrianglesRenderableObject<InputLayouts.VerticeP> {
        private IRenderableMaterial _material;

        private SkyObject(InputLayouts.VerticeP[] vertices, ushort[] indices) : base(vertices, indices) {
            BoundingBox = new BoundingBox(new Vector3(-9e9f), new Vector3(9e9f));
        }

        public static SkyObject Create(float radius) {
            var mesh = GeometryGenerator.CreateSphere(radius, 30, 30);
            return new SkyObject(mesh.Vertices.Select(x => new InputLayouts.VerticeP(x.Position)).ToArray(),
                    mesh.Indices.Select(x => (ushort)x).ToArray());
        }

        protected override void Initialize(DeviceContextHolder contextHolder) {
            base.Initialize(contextHolder);

            var materialsProvider = contextHolder.Get<Kn5MaterialsProvider>();
            _material = materialsProvider.GetSkyMaterial();
            _material.Initialize(contextHolder);
        }

        protected override void DrawInner(DeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode) {
            if (!_material.Prepare(contextHolder, mode)) return;
            base.DrawInner(contextHolder, camera, mode);

            _material.SetMatrices(Matrix.Identity, camera);
            _material.Draw(contextHolder, Indices.Length, mode);
        }

        public override void Dispose() {
            base.Dispose();
            _material.Dispose();
        }
    }
}
