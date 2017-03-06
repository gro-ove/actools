using System;
using System.Collections.Generic;
using System.Linq;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.TargetTextures;
using AcTools.Utils.Helpers;
using SlimDX;
using SlimDX.Direct3D11;
using Viewport = SlimDX.Direct3D11.Viewport;

namespace AcTools.Render.Base.Shadows {
    public class ShadowsDirectional : IDisposable {
        public sealed class CameraOrthoShadow : CameraOrtho {
            private readonly CameraOrtho _innerCamera;

            public CameraOrthoShadow SmallerCamera;

            public CameraOrthoShadow() {
                _innerCamera = new CameraOrtho();
                _innerCamera.SetLens(1f);
            }

            public override void LookAt(Vector3 pos, Vector3 target, Vector3 up) {
                base.LookAt(pos, target, up);

                if (!Equals(_innerCamera.FarZ, FarZ)) {
                    _innerCamera.Aspect = Aspect;
                    _innerCamera.NearZ = NearZ;
                    _innerCamera.FarZ = FarZ;
                    _innerCamera.Width = Width * 0.95f;
                    _innerCamera.Height = Height * 0.95f;
                    _innerCamera.SetLens(1f);
                }

                _innerCamera.LookAt(pos, target, up);
            }

            public override void UpdateViewMatrix() {
                base.UpdateViewMatrix();
                _innerCamera.UpdateViewMatrix();
            }

            public override bool Visible(BoundingBox box) {
                return Frustum.Intersect(box) > 0 && SmallerCamera?._innerCamera.Intersect(box) != FrustrumIntersectionType.Inside;
            }
        }

        public class Split : IDisposable {
            internal float Size { get; private set; }

            internal float ClipDistance { get; private set; }

            internal CameraOrthoShadow Camera { get; private set; }

            public readonly TargetResourceDepthTexture Buffer;

            public Split(float size, float clipDistance) {
                Buffer = TargetResourceDepthTexture.Create();
                Update(size, clipDistance);
            }

            public void Update(float size, float clipDistance) {
                if (Camera != null && Equals(size, Size) && Equals(ClipDistance, clipDistance)) return;

                Size = size;
                ClipDistance = clipDistance;

                Camera = new CameraOrthoShadow {
                    NearZ = 1f,
                    FarZ = ClipDistance * 2f,
                    Width = size,
                    Height = size
                };

                Camera.SetLens(1f);
            }

            public void LookAt(Vector3 direction, Vector3 lookAt) {
                Camera.LookAt(lookAt - ClipDistance * Vector3.Normalize(direction), lookAt, direction.X == 0f && direction.Z == 0f ? Vector3.UnitX : Vector3.UnitY);
                Camera.UpdateViewMatrix();
                ShadowTransform = Camera.ViewProj * new Matrix {
                    M11 = 0.5f,
                    M22 = -0.5f,
                    M33 = 1.0f,
                    M41 = 0.5f,
                    M42 = 0.5f,
                    M44 = 1.0f
                };
            }

            public Matrix ShadowTransform { get; private set; }

            public ShaderResourceView View => Buffer.View;

            public void Dispose() {
                Buffer.Dispose();
            }
        }

        public Split[] Splits { get; private set; }

        public int MapSize { get; private set; }
        private Viewport _viewport;

        private RasterizerState _rasterizerState;
        private DepthStencilState _depthStencilState;

        public ShadowsDirectional(int mapSize, IEnumerable<float> splits, float clipDistance = 50f) {
            MapSize = mapSize;
            _viewport = new Viewport(0, 0, MapSize, MapSize, 0, 1.0f);

            SetSplits(splits, clipDistance);
        }

        private void SetSplits(IEnumerable<float> splits, float clipDistance = 50f) {
            var splitsValues = splits.ToArrayIfItIsNot();
            if (Splits != null && splitsValues.Length == Splits.Length) {
                for (var i = 0; i < Splits.Length; i++) {
                    Splits[i].Update(splitsValues[i], clipDistance);
                }
            } else {
                Splits?.DisposeEverything();
                Splits = splitsValues.Select(x => new Split(x, clipDistance)).ToArray();
                for (var i = 1; i < Splits.Length; i++) {
                    Splits[i].Camera.SmallerCamera = Splits[i - 1].Camera;
                }
            }
        }

        public void SetSplits(DeviceContextHolder holder, IEnumerable<float> splits, float clipDistance = 50f) {
            SetSplits(splits, clipDistance);
            foreach (var split in Splits) {
                split.Buffer.Resize(holder, MapSize, MapSize, null);
            }
        }

        public void SetMapSize(DeviceContextHolder holder, int value) {
            if (Equals(value, MapSize)) return;
            MapSize = value;
            _viewport = new Viewport(0, 0, MapSize, MapSize, 0, 1.0f);
            foreach (var split in Splits) {
                split.Buffer.Resize(holder, MapSize, MapSize, null);
            }
        }

        public ShadowsDirectional(int mapSize, float clipDistance = 50f) : this(mapSize, new []{ 15f, 50f, 200f }, clipDistance) {}

        public void Initialize(DeviceContextHolder holder) {
            foreach (var split in Splits) {
                split.Buffer.Resize(holder, MapSize, MapSize, null);
            }

            _rasterizerState = RasterizerState.FromDescription(holder.Device, new RasterizerStateDescription {
                CullMode = CullMode.Front,
                FillMode = FillMode.Solid,
                IsAntialiasedLineEnabled = false,
                IsDepthClipEnabled = true,
                DepthBias = 100,
                DepthBiasClamp = 0.0f,
                SlopeScaledDepthBias = 1f
            });

            _depthStencilState = DepthStencilState.FromDescription(holder.Device, new DepthStencilStateDescription {
                DepthWriteMask = DepthWriteMask.All,
                DepthComparison = Comparison.Greater,
                IsDepthEnabled = true,
                IsStencilEnabled = false
            });
        }

        public void Update(Vector3 direction, BaseCamera camera) {
            foreach (var split in Splits) {
                split.LookAt(direction, camera.Position + camera.Look * split.Size / 2);
            }
        }

        public void Update(Vector3 direction, Vector3 target) {
            foreach (var split in Splits) {
                split.LookAt(direction, target);
            }
        }

        public void DrawScene(DeviceContextHolder holder, IShadowsDraw draw) {
            holder.SaveRenderTargetAndViewport();

            holder.DeviceContext.Rasterizer.SetViewports(_viewport);
            holder.DeviceContext.OutputMerger.DepthStencilState = null;
            holder.DeviceContext.OutputMerger.BlendState = null;
            holder.DeviceContext.Rasterizer.State = _rasterizerState;

            foreach (var split in Splits) {
                holder.DeviceContext.OutputMerger.SetTargets(split.Buffer.DepthView);
                holder.DeviceContext.ClearDepthStencilView(split.Buffer.DepthView, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1f, 0);
                draw.DrawSceneForShadows(holder, split.Camera);
                holder.DeviceContext.GenerateMips(split.Buffer.View);
            }

            holder.DeviceContext.Rasterizer.State = null;
            holder.DeviceContext.OutputMerger.DepthStencilState = null;
            holder.RestoreRenderTargetAndViewport();
        }

        public void Clear(DeviceContextHolder holder) {
            holder.SaveRenderTargetAndViewport();

            holder.DeviceContext.Rasterizer.SetViewports(_viewport);
            holder.DeviceContext.OutputMerger.DepthStencilState = null;
            holder.DeviceContext.OutputMerger.BlendState = null;
            holder.DeviceContext.Rasterizer.State = _rasterizerState;

            foreach (var split in Splits) {
                holder.DeviceContext.OutputMerger.SetTargets(split.Buffer.DepthView);
                holder.DeviceContext.ClearDepthStencilView(split.Buffer.DepthView, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1f, 0);
            }

            holder.DeviceContext.Rasterizer.State = null;
            holder.DeviceContext.OutputMerger.DepthStencilState = null;
            holder.RestoreRenderTargetAndViewport();
        }

        public void Dispose() {
            Splits.DisposeEverything();
            DisposeHelper.Dispose(ref _rasterizerState);
            DisposeHelper.Dispose(ref _depthStencilState);
        }
    }
}
