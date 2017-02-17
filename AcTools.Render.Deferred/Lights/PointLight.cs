using System.Linq;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Structs;
using AcTools.Render.Base.Utils;
using AcTools.Render.Deferred.Shaders;
using AcTools.Render.Shaders;
using AcTools.Utils.Helpers;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Deferred.Lights {
    public class PointLight : BaseLight {
        public Vector3 Position;
        public Vector3 Color;
        public float Radius;
        public bool Specular = true;
        public bool Debug = false;

        private EffectDeferredLight _effect;

        private class SphereObject : TrianglesRenderableObject<InputLayouts.VerticeP> {
            private SphereObject(InputLayouts.VerticeP[] vertices, ushort[] indices) : base(null, vertices, indices) { }

            public static SphereObject Create(float radius) {
                var mesh = GeometryGenerator.CreateSphere(radius, 10, 10);
                return new SphereObject(mesh.Vertices.Select(x => new InputLayouts.VerticeP(x.Position)).ToArray(),
                        mesh.Indices.Select(x => (ushort)x).ToArray());
            }
        }

        SphereObject _sphere;
        DepthStencilState _depth;
        RasterizerState _rasterizer;

        public override void OnInitialize(DeviceContextHolder holder) {
            _effect = holder.GetEffect<EffectDeferredLight>();
            _sphere = SphereObject.Create(Radius);
            _rasterizer = RasterizerState.FromDescription(holder.Device, new RasterizerStateDescription {
                CullMode = CullMode.Front,
                FillMode = FillMode.Solid,
                IsDepthClipEnabled = true,
                IsAntialiasedLineEnabled = false
            });
            _depth = DepthStencilState.FromDescription(holder.Device, new DepthStencilStateDescription {
                DepthComparison = Comparison.Greater,
                IsDepthEnabled = true,
                IsStencilEnabled = false,
                DepthWriteMask = DepthWriteMask.Zero
            });
        }

        public override void DrawInner(DeviceContextHolder holder, ICamera camera, SpecialLightMode mode) {
            //holder.DeviceContext.OutputMerger.DepthStencilState = null;

            _effect.FxPointLightRadius.Set(Radius * Radius);
            _effect.FxPointLightPosition.Set(Position);
            _effect.FxLightColor.Set(Color);

            _sphere.Draw(holder, camera, SpecialRenderMode.Shadow);
            holder.DeviceContext.Rasterizer.State = _rasterizer;
            //holder.DeviceContext.OutputMerger.DepthStencilState = _depth;

            var matrix = Matrix.Translation(Position);
            _effect.FxWorldViewProj.SetMatrix(matrix * camera.ViewProj);
            _effect.FxWorldInvTranspose.SetMatrix(Matrix.Invert(Matrix.Transpose(matrix)));
            _effect.FxWorld.SetMatrix(matrix);
            (Debug ? _effect.TechPointLight_Debug : Specular ? _effect.TechPointLight : _effect.TechPointLight_NoSpec)
                    .DrawAllPasses(holder.DeviceContext, _sphere.IndicesCount);

            holder.DeviceContext.Rasterizer.State = null;
            //holder.DeviceContext.OutputMerger.DepthStencilState = null;
        }

        public override void Dispose() {
            _effect = null;
            DisposeHelper.Dispose(ref _sphere);
            DisposeHelper.Dispose(ref _rasterizer);
            DisposeHelper.Dispose(ref _depth);
        }
    }
}
