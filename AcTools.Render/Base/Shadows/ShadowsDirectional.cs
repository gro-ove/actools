using System;
using System.Collections.Generic;
using System.Linq;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.TargetTextures;
using AcTools.Utils.Helpers;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Base.Shadows {
    public class ShadowsDirectional : ShadowsBase {
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

            public bool LookAt(Vector3 direction, Vector3 lookAt) {
                Camera.LookAt(lookAt - ClipDistance * Vector3.Normalize(direction), lookAt,
                        direction.X == 0f && direction.Z == 0f ? Vector3.UnitX : Vector3.UnitY);
                Camera.UpdateViewMatrix();
                var shadowTransform = Camera.ViewProj * new Matrix {
                    M11 = 0.5f,
                    M22 = -0.5f,
                    M33 = 1.0f,
                    M41 = 0.5f,
                    M42 = 0.5f,
                    M44 = 1.0f
                };

                if (ShadowTransform != shadowTransform) {
                    ShadowTransform = shadowTransform;
                    return true;
                }
                
                return false;
            }

            public Matrix ShadowTransform { get; private set; }

            public ShaderResourceView View => Buffer.View;

            public void Dispose() {
                Buffer.Dispose();
            }
        }

        public Split[] Splits { get; private set; }

        public ShadowsDirectional(int mapSize, IEnumerable<float> splits, float clipDistance = 100f) : base(mapSize) {
            SetSplits(splits, clipDistance);
        }

        public ShadowsDirectional(int mapSize, float clipDistance = 50f) : this(mapSize, new[] { 15f, 50f, 200f }, clipDistance) { }

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

        public void SetSplits(DeviceContextHolder holder, IEnumerable<float> splits, float clipDistance = 100f) {
            SetSplits(splits, clipDistance);
            foreach (var split in Splits) {
                split.Buffer.Resize(holder, MapSize, MapSize, null);
            }
        }

        protected override void ResizeBuffers(DeviceContextHolder holder, int size) {
            foreach (var split in Splits) {
                split.Buffer.Resize(holder, size, size, null);
            }
        }

        public void Update(Vector3 direction, BaseCamera camera) {
            foreach (var split in Splits) {
                split.LookAt(direction, camera.Position + camera.Look * split.Size / 2);
            }
        }

        private bool _dirty;

        public bool Update(Vector3 direction, Vector3 target) {
            var result = _dirty;
            _dirty = false;

            for (var i = Splits.Length - 1; i >= 0; i--) {
                result |= Splits[i].LookAt(direction, target);
            }
            return result;
        }

        public override void Invalidate() {
            _dirty = true;
        }

        protected override void UpdateBuffers(DeviceContextHolder holder, IShadowsDraw draw) {
            foreach (var split in Splits) {
                holder.DeviceContext.OutputMerger.SetTargets(split.Buffer.DepthView);
                holder.DeviceContext.ClearDepthStencilView(split.Buffer.DepthView, DepthStencilClearFlags.Depth, 1f, 0);
                draw.DrawSceneForShadows(holder, split.Camera);
            }
        }

        protected override void ClearBuffers(DeviceContextHolder holder) {
            foreach (var split in Splits) {
                holder.DeviceContext.ClearDepthStencilView(split.Buffer.DepthView, DepthStencilClearFlags.Depth, 1f, 0);
            }
        }

        protected override void DisposeOverride() {
            Splits.DisposeEverything();
        }
    }
}
