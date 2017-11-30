using System;
using System.Threading;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.PostEffects;
using AcTools.Render.Base.TargetTextures;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;

namespace AcTools.Render.Kn5SpecificForwardDark {
    public partial class DarkKn5ObjectRenderer {
        private bool _useAccumulationDof;

        public bool UseAccumulationDof {
            get => _useAccumulationDof;
            set {
                if (Equals(value, _useAccumulationDof)) return;
                _useAccumulationDof = value;
                IsDirty = true;
                OnPropertyChanged();
            }
        }

        private int _accumulationDofIterations = 100;

        public int AccumulationDofIterations {
            get => _accumulationDofIterations;
            set {
                value = Math.Max(value, 2);
                if (Equals(value, _accumulationDofIterations)) return;
                _accumulationDofIterations = value;
                IsDirty = true;
                OnPropertyChanged();
                _realTimeAccumulationSize = 0;
            }
        }

        private float _accumulationDofApertureSize = 0.02f;

        public float AccumulationDofApertureSize {
            get => _accumulationDofApertureSize;
            set {
                if (Equals(value, _accumulationDofApertureSize)) return;
                _accumulationDofApertureSize = value;
                IsDirty = true;
                OnPropertyChanged();
                _realTimeAccumulationSize = 0;
            }
        }

        private bool _accumulationDofBokeh;

        public bool AccumulationDofBokeh {
            get => _accumulationDofBokeh;
            set {
                if (Equals(value, _accumulationDofBokeh)) return;
                _accumulationDofBokeh = value;
                OnPropertyChanged();
                _realTimeAccumulationSize = 0;
            }
        }

        protected override bool CanShotWithoutExtraTextures => base.CanShotWithoutExtraTextures && (!UseDof || !UseAccumulationDof);

        private CameraBase GetDofAccumulationCamera(CameraBase camera, float apertureMultipler) {
            var apertureSize = AccumulationDofApertureSize;

            Vector2 direction;
            if (apertureSize <= 0f) {
                direction = Vector2.Zero;
            } else {
                do {
                    direction = new Vector2(MathUtils.Random(-1f, 1f), MathUtils.Random(-1f, 1f));
                } while (direction.LengthSquared() > 1f);
                // direction.Normalize();
                // direction *= MathF.Pow(MathUtils.Random(0f, 1f), 0.4f);
            }

            var bokeh = camera.Right * direction.X + camera.Up * direction.Y;
            var positionOffset = AccumulationDofApertureSize * apertureMultipler * bokeh;

            var aaOffset = Matrix.Translation(MathUtils.Random(-1f, 1f) / Width, MathUtils.Random(-1f, 1f) / Height, 0f);
            var focusDistance = DofFocusPlane;

            var newCamera = new FpsCamera(camera.FovY) {
                CutProj = camera.CutProj.HasValue ? aaOffset * camera.CutProj : aaOffset
            };

            var newPosition = camera.Position + positionOffset;
            var lookAt = camera.Position + camera.Look * focusDistance;
            newCamera.LookAt(newPosition, lookAt, camera.Tilt);
            newCamera.SetLens(AspectRatio);
            newCamera.UpdateViewMatrix();
            return newCamera;
        }

        private IDisposable ReplaceCamera(CameraBase newCamera) {
            var camera = Camera;
            Camera = newCamera;
            return new ActionAsDisposable(() => { Camera = camera; });
        }

        private bool _accumulationDofShotInProcess;

        protected override void DrawShot(RenderTargetView target, IProgress<double> progress, CancellationToken cancellation) {
            if (UseDof && UseAccumulationDof && target != null) {
                var copy = DeviceContextHolder.GetHelper<CopyHelper>();

                _useDof = false;
                _accumulationDofShotInProcess = true;

                try {
                    if (IsDirty) {
                        _realTimeAccumulationSize = 0;
                    }

                    using (var summary = TargetResourceTexture.Create(Format.R32G32B32A32_Float))
                    using (var temporary = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm)) {
                        summary.Resize(DeviceContextHolder, ActualWidth, ActualHeight, null);
                        temporary.Resize(DeviceContextHolder, ActualWidth, ActualHeight, null);
                        DeviceContext.ClearRenderTargetView(summary.TargetView, default(Color4));
                        DeviceContext.ClearRenderTargetView(temporary.TargetView, default(Color4));

                        var iterations = AccumulationDofIterations;
                        for (var i = 0; i < iterations; i++) {
                            if (cancellation.IsCancellationRequested) return;

                            Vector2 direction;
                            do {
                                direction = new Vector2(MathUtils.Random(-1f, 1f), MathUtils.Random(-1f, 1f));
                            } while (direction.LengthSquared() > 1f);

                            using (ReplaceCamera(GetDofAccumulationCamera(Camera, 1f))) {
                                progress?.Report(0.05 + 0.9 * i / iterations);
                                base.DrawShot(temporary.TargetView, progress, cancellation);
                            }

                            DeviceContext.OutputMerger.BlendState = DeviceContextHolder.States.AddState;
                            copy.DrawSqr(DeviceContextHolder, temporary.View, summary.TargetView);
                            DeviceContext.OutputMerger.BlendState = null;
                        }

                        copy.AccumulateDivide(DeviceContextHolder, summary.View, target, iterations);
                    }

                } finally {
                    _useDof = true;
                    _accumulationDofShotInProcess = false;
                }

                return;
            }

            base.DrawShot(target, progress, cancellation);
        }

        public override bool AccumulationMode => UseDof && UseAccumulationDof && _realTimeAccumulationSize < AccumulationDofIterations;

        protected override void OnTickOverride(float dt) {
            base.OnTickOverride(dt);

            foreach (var light in _movingLights) {
                IsDirty |= light.Update();
            }

            if (IsDirty) {
                _realTimeAccumulationSize = 0;
            }
        }

        public override void Draw() {
            if (IsPaused) return;
            if (UseDof && UseAccumulationDof) {
                _realTimeAccumulationMode = true;
                if (IsDirty) {
                    _realTimeAccumulationSize = 0;
                }

                base.Draw();
            } else {
                if (_realTimeAccumulationMode) {
                    DisposeHelper.Dispose(ref _accumulationTexture);
                    DisposeHelper.Dispose(ref _accumulationMaxTexture);
                    DisposeHelper.Dispose(ref _accumulationTemporaryTexture);
                    DisposeHelper.Dispose(ref _accumulationBaseTexture);
                    _realTimeAccumulationSize = 0;
                    _realTimeAccumulationMode = false;
                }

                base.Draw();
            }
        }
    }
}