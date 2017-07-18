using System.Linq;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.TargetTextures;
using AcTools.Render.Base.Utils;
using AcTools.Utils.Helpers;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;

namespace AcTools.Render.Base.Shadows {
    /// <summary>
    /// Similar to ShadowsPoint, but uses 2D texture instead of cubemap to store shadows.
    /// </summary>
    public class ShadowsPointFlat : ShadowsBase {
        private readonly FpsCamera[] _cameras;
        private TargetResourceDepthTexture _buffer;

        public ShaderResourceView View => _buffer.View;

        public float FarZValue => _cameras[0].FarZValue;

        public float NearZValue => _cameras[0].NearZValue;

        public ShadowsPointFlat(int mapSize, float padding) : base(mapSize) {
            _cameras = Enumerable.Range(0, 6).Select(x => new FpsCamera(MathF.PI / 2) {
                NearZ = 0.1f,
                RhMode = false,
                CutProj = Matrix.Transformation2D(Vector2.Zero, 0f, new Vector2(padding), Vector2.Zero, 0f, Vector2.Zero)
            }).ToArray();
            _buffer = TargetResourceDepthTexture.Create(Format.R24G8_Typeless);
        }

        protected override RasterizerState GetRasterizerState(IDeviceContextHolder holder) {
            return holder.States.ShadowsPointState;
        }

        protected override void ResizeBuffers(DeviceContextHolder holder, int size) {
            if (size > 2048) size = 2048;
            _buffer.Resize(holder, size, size * 6, null);
        }

        private Vector3 _center;
        private float _radius;

        public bool Update(Vector3 center, float radius) {
            if (_center == center && _radius == radius) return false;

            _center = center;
            _radius = radius;

            var targets = new[] {
                new Vector3(center.X + 1, center.Y, center.Z),
                new Vector3(center.X - 1, center.Y, center.Z),
                new Vector3(center.X, center.Y + 1, center.Z),
                new Vector3(center.X, center.Y - 1, center.Z),
                new Vector3(center.X, center.Y, center.Z + 1),
                new Vector3(center.X, center.Y, center.Z - 1)
            };

            var ups = new[] {
                new Vector3(0, 1, 0),
                new Vector3(0, 1, 0),
                new Vector3(0, 0, -1),
                new Vector3(0, 0, 1),
                new Vector3(0, 1, 0),
                new Vector3(0, 1, 0),
            };

            for (var i = 0; i < 6; i++) {
                if (_cameras[i].FarZ != radius) {
                    _cameras[i].NearZ = 0.02f;
                    _cameras[i].FarZ = radius;
                    _cameras[i].SetLens(1f);
                }

                _cameras[i].LookAt(center, targets[i], ups[i]);
                _cameras[i].UpdateViewMatrix();
            }

            return true;
        }

        public override void Invalidate() {
            _center = default(Vector3);
            _radius = 0f;
        }

        protected override void UpdateBuffers(DeviceContextHolder holder, IShadowsDraw draw) {
            holder.DeviceContext.OutputMerger.SetTargets(_buffer.DepthView);
            holder.DeviceContext.ClearDepthStencilView(_buffer.DepthView, DepthStencilClearFlags.Depth, 1.0f, 0);

            for (var i = 0; i < 6; i++) {
                holder.DeviceContext.Rasterizer.SetViewports(new Viewport(0, i * _buffer.Width, _buffer.Width, _buffer.Width, 0f, 1f));
                draw.DrawSceneForShadows(holder, _cameras[i]);
            }
        }

        protected override void ClearBuffers(DeviceContextHolder holder) {
            holder.DeviceContext.ClearDepthStencilView(_buffer.DepthView, DepthStencilClearFlags.Depth, 1.0f, 0);
        }

        protected override void DisposeOverride() {
            DisposeHelper.Dispose(ref _buffer);
        }
    }
}
