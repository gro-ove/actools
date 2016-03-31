using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AcTools.Render.Base;
using AcTools.Render.Base.Camera;
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

        private readonly Matrix _transform;
        private readonly IRenderableMaterial _material;

        public AmbientShadow(string filename, Matrix transform)
            : base(BaseVertices, BaseIndices) {
            _transform = transform;
            _material = new AmbientShadowMaterial(filename);
        }

        protected override void Initialize(DeviceContextHolder contextHolder) {
            base.Initialize(contextHolder);
            _material.Initialize(contextHolder);
        }

        protected override void DrawInner(DeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode) {
            if (mode != SpecialRenderMode.Default) return;

            _material.Prepare(contextHolder, mode);
            base.DrawInner(contextHolder, camera, mode);

            _material.SetMatrices(_transform*ParentMatrix, camera);
            _material.Draw(contextHolder, Indices.Length, mode);
        }

        public override void Dispose() {
            base.Dispose();
            _material.Dispose();
        }
    }
}
