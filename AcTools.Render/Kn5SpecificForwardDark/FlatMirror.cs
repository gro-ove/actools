using System;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Structs;
using AcTools.Render.Shaders;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5SpecificForwardDark {
    public class FlatMirror : RenderableList {
        public class FlatMirrorObject : TrianglesRenderableObject<InputLayouts.VerticePT> {
            private static readonly InputLayouts.VerticePT[] BaseVertices;
            private static readonly ushort[] BaseIndices;

            static FlatMirrorObject() {
                BaseVertices = new InputLayouts.VerticePT[4];
                for (var i = 0; i < BaseVertices.Length; i++) {
                    BaseVertices[i] = new InputLayouts.VerticePT(
                            new Vector3(i < 2 ? 1 : -1, 0, i % 2 == 0 ? -1 : 1),
                            new Vector2(i < 2 ? 1 : 0, i % 2));
                }

                BaseIndices = new ushort[] { 0, 2, 1, 3, 1, 2 };
            }

            private IRenderableMaterial _material;

            public Matrix Transform;
            private readonly bool _opaqueMode;

            public FlatMirrorObject(Matrix transform, bool opaqueMode) : base(null, BaseVertices, BaseIndices) {
                Transform = transform;
                _opaqueMode = opaqueMode;
            }

            protected override void Initialize(IDeviceContextHolder contextHolder) {
                base.Initialize(contextHolder);

                _material = contextHolder.GetMaterial(_opaqueMode ? BasicMaterials.FlatGroundKey : BasicMaterials.FlatMirrorKey);
                _material.Initialize(contextHolder);
            }

            protected override void DrawOverride(IDeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode) {
                if (!_material.Prepare(contextHolder, mode)) return;
                base.DrawOverride(contextHolder, camera, mode);

                _material.SetMatrices(Transform * ParentMatrix, camera);
                _material.Draw(contextHolder, Indices.Length, mode);
            }

            public override void Dispose() {
                base.Dispose();
                _material?.Dispose();
            }
        }

        private readonly FlatMirrorObject _object;

        private FlatMirror([CanBeNull] IRenderableObject mirroredObject, Plane plane, bool opaqueMode) {
            LocalMatrix = Matrix.Reflection(plane);

            var point = plane.Normal * plane.D;
            var matrix = Matrix.Scaling(1000f, 1000f, 1000f) * Matrix.Translation(point);

            if (mirroredObject != null) {
                Add(mirroredObject.Clone());
                _object = new FlatMirrorObject(matrix, opaqueMode) { ParentMatrix = Matrix };
            } else {
                _object = new FlatMirrorObject(matrix, opaqueMode) { ParentMatrix = Matrix };
            }
        }

        public FlatMirror([NotNull] IRenderableObject mirroredObject, Plane plane) : this(mirroredObject, plane, false) {}

        public FlatMirror(Plane plane, bool opaqueMode) : this(null, plane, opaqueMode) {}

        private RasterizerState _rasterizerState;

        public override void Draw(IDeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode, Func<IRenderableObject, bool> filter = null) {
            if (mode != SpecialRenderMode.Simple && mode != SpecialRenderMode.SimpleTransparent) return;

            if (camera != null && camera.Position.Y > 0) {
                var state = contextHolder.DeviceContext.Rasterizer.State;
                try {
                    contextHolder.DeviceContext.Rasterizer.State = _rasterizerState ?? contextHolder.States.InvertedState;
                    contextHolder.GetEffect<EffectDarkMaterial>().FxFlatMirrored.Set(true);
                    base.Draw(contextHolder, camera, mode, filter);
                } finally {
                    contextHolder.DeviceContext.Rasterizer.State = state;
                    contextHolder.GetEffect<EffectDarkMaterial>().FxFlatMirrored.Set(false);
                }
            }

            _object.Draw(contextHolder, camera, mode, filter);
        }

        public void SetInvertedRasterizerState(RasterizerState state) {
            _rasterizerState = state;
        }
    }
}