using System;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.TargetTextures;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Base.Shadows {
    public class ShadowsSpot : ShadowsBase {
        private readonly FpsCamera _camera;
        private readonly TargetResourceDepthTexture _buffer;

        public ShaderResourceView View => _buffer.View;

        public Matrix ShadowTransform { get; private set; }

        public ShadowsSpot(int mapSize) : base(mapSize) {
            _camera = new FpsCamera(1f) {
                NearZ = 0.02f
            };

            _buffer = TargetResourceDepthTexture.Create();
        }

        protected override void ResizeBuffers(DeviceContextHolder holder, int size) {
            _buffer.Resize(holder, size, size, null);
        }

        private bool _dirty;

        public bool Update(Vector3 position, Vector3 direction, float range, float angle) {
            var result = _dirty;
            _dirty = false;

            if (result || _camera.FovY != angle || _camera.FarZ != range) {
                _camera.FovY = angle;
                _camera.FarZ = range;
                _camera.SetLens(1f);
                result = true;
            }

            if (result || _camera.Position != position || Vector3.Dot(_camera.Look, direction) < 0.99) {
                _camera.LookAt(position, position + direction, 
                    Math.Abs(direction.Y - 1f) < 0.01f || Math.Abs(direction.Y - (-1f)) < 0.01f ? Vector3.UnitX : Vector3.UnitY);
                _camera.UpdateViewMatrix();
                result = true;
            }

            if (result) {
                ShadowTransform = _camera.ViewProj * new Matrix {
                    M11 = 0.5f,
                    M22 = -0.5f,
                    M33 = 1.0f,
                    M41 = 0.5f,
                    M42 = 0.5f,
                    M44 = 1.0f
                };
            }

            return result;
        }

        public override void Invalidate() {
            _dirty = true;
        }

        protected override void UpdateBuffers(DeviceContextHolder holder, IShadowsDraw draw) {
            holder.DeviceContext.OutputMerger.SetTargets(_buffer.DepthView);
            holder.DeviceContext.ClearDepthStencilView(_buffer.DepthView, DepthStencilClearFlags.Depth, 1f, 0);
            draw.DrawSceneForShadows(holder, _camera);
        }

        protected override void ClearBuffers(DeviceContextHolder holder) {
            holder.DeviceContext.ClearDepthStencilView(_buffer.DepthView, DepthStencilClearFlags.Depth, 1f, 0);
        }

        protected override void DisposeOverride() {
            _buffer.Dispose();
        }
    }
}