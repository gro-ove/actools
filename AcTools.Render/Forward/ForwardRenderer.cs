using System.Drawing;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.PostEffects;
using AcTools.Render.Base.TargetTextures;
using AcTools.Render.Base.Utils;
using AcTools.Render.Shaders;
using AcTools.Utils.Helpers;
using SlimDX.Direct3D11;
using SlimDX.DXGI;

namespace AcTools.Render.Forward {
    public abstract class ForwardRenderer : SceneRenderer {
        private bool _useInterpolationCamera;

        public bool UseInterpolationCamera {
            get { return _useInterpolationCamera; }
            set {
                if (Equals(value, _useInterpolationCamera)) return;
                _useInterpolationCamera = value;
                OnPropertyChanged();
            }
        }

        private bool _useFxaa = true;

        public bool UseFxaa {
            get { return _useFxaa; }
            set {
                if (Equals(_useFxaa, value)) return;
                _useFxaa = value;
                IsDirty = true;
                OnPropertyChanged();
            }
        }

        private bool _showWireframe;

        public bool ShowWireframe {
            get { return _showWireframe; }
            set {
                if (Equals(_showWireframe, value)) return;
                _showWireframe = value;
                IsDirty = true;
                OnPropertyChanged();
            }
        }

        private bool _useBloom;

        public bool UseBloom {
            get { return _useBloom; }
            set {
                if (Equals(value, _useBloom)) return;

                _useBloom = value;

                DisposeHelper.Dispose(ref _buffer);
                DisposeHelper.Dispose(ref _buffer1);
                DisposeHelper.Dispose(ref _buffer2);

                if (_useBloom) {
                    _buffer = TargetResourceTexture.Create(Format.R16G16B16A16_Float, SampleDescription);
                    _buffer1 = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm, SampleDescription);
                    _buffer2 = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm, SampleDescription);
                } else {
                    _buffer = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm, SampleDescription);
                }

                if (!InitiallyResized) return;
                _buffer.Resize(DeviceContextHolder, Width, Height);
                _buffer1?.Resize(DeviceContextHolder, Width, Height);
                _buffer2?.Resize(DeviceContextHolder, Width, Height);

                IsDirty = true;
                OnPropertyChanged();
            }
        }

        protected override FeatureLevel FeatureLevel => FeatureLevel.Level_10_0;

        private TargetResourceTexture _buffer, _buffer1, _buffer2;
        private RasterizerState _wireframeRasterizerState;

        protected TargetResourceTexture InnerBuffer => _buffer;

        protected override void InitializeInner() {
            UseBloom = true;

            _wireframeRasterizerState = RasterizerState.FromDescription(Device, new RasterizerStateDescription {
                FillMode = FillMode.Wireframe,
                CullMode = CullMode.Back,
                IsFrontCounterclockwise = false,
                IsAntialiasedLineEnabled = false,
                IsDepthClipEnabled = true
            });
        }

        protected override void ResizeInner() {
            base.ResizeInner();
            
            _buffer.Resize(DeviceContextHolder, Width, Height);
            _buffer1?.Resize(DeviceContextHolder, Width, Height);
            _buffer2?.Resize(DeviceContextHolder, Width, Height);
        }

        private EffectPpHdr _hdr;
        private BlurHelper _blur;

        protected virtual void DrawAfter() { }

        protected ICamera ActualCamera => UseInterpolationCamera ? (ICamera)_interpolationCamera : Camera;

        private readonly InterpolationCamera _interpolationCamera = new InterpolationCamera(5f);

        protected override void OnTick(float dt) {
            if (UseInterpolationCamera) {
                _interpolationCamera.Update(Camera, dt);
            }
        }

        public Color BackgroundColor { get; set; } = Color.Gray;

        public virtual RasterizerState GetRasterizerState() {
            return ShowWireframe ? _wireframeRasterizerState : null;
        }

        protected virtual void DrawScene() {
            DeviceContext.OutputMerger.DepthStencilState = null;
            DeviceContext.OutputMerger.BlendState = null;
            DeviceContext.Rasterizer.State = GetRasterizerState();

            DeviceContext.OutputMerger.DepthStencilState = DeviceContextHolder.States.LessEqualDepthState;
            Scene.Draw(DeviceContextHolder, ActualCamera, SpecialRenderMode.Simple);

            DeviceContext.OutputMerger.DepthStencilState = DeviceContextHolder.States.ReadOnlyDepthState;
            Scene.Draw(DeviceContextHolder, ActualCamera, SpecialRenderMode.SimpleTransparent);
        }

        protected override void DrawInner() {
            DrawPrepare();

            DeviceContext.ClearRenderTargetView(_buffer.TargetView, BackgroundColor);
            DeviceContext.ClearDepthStencilView(DepthStencilView, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1f, 0);
            DeviceContext.OutputMerger.SetTargets(DepthStencilView, _buffer.TargetView);

            DrawScene();
            DrawAfter();

            DeviceContext.OutputMerger.DepthStencilState = null;
            DeviceContext.OutputMerger.BlendState = null;
            DeviceContext.Rasterizer.State = null;

            ShaderResourceView result;
            if (UseBloom) {
                if (_hdr == null) {
                    _hdr = DeviceContextHolder.GetEffect<EffectPpHdr>();
                    _blur = DeviceContextHolder.GetHelper<BlurHelper>();
                }

                DeviceContext.OutputMerger.SetTargets(_buffer1.TargetView);
                DeviceContext.ClearRenderTargetView(_buffer1.TargetView, ColorTransparent);

                DeviceContextHolder.PrepareQuad(_hdr.LayoutPT);
                _hdr.FxInputMap.SetResource(_buffer.View);
                _hdr.TechBloom.DrawAllPasses(DeviceContext, 6);
                
                _blur.Blur(DeviceContextHolder, _buffer1, _buffer2, 1f, 2);

                DeviceContext.OutputMerger.SetTargets(_buffer2.TargetView);

                _hdr.FxInputMap.SetResource(_buffer.View);
                _hdr.FxBloomMap.SetResource(_buffer1.View);
                _hdr.TechCombine.DrawAllPasses(DeviceContext, 6);

                result = _buffer2.View;
            } else {
                result = _buffer.View;
            }

            DeviceContext.ClearRenderTargetView(RenderTargetView, ColorTransparent);
            if (UseFxaa) {
                DeviceContextHolder.GetHelper<FxaaHelper>().Draw(DeviceContextHolder, result, RenderTargetView);
            } else {
                DeviceContextHolder.GetHelper<CopyHelper>().Draw(DeviceContextHolder, result, RenderTargetView);
            }
        }

        public bool KeepFxaaWhileShooting;

        public override Image Shot(int multipler) {
            if (KeepFxaaWhileShooting) {
                return base.Shot(multipler);
            } else {
                var useFxaa = UseFxaa;
                UseFxaa = false;

                try {
                    return base.Shot(multipler);
                } finally {
                    UseFxaa = useFxaa;
                }
            }
        }

        protected sealed override void DrawSprites() {
            if (Sprite == null) return;
            DrawSpritesInner();
            Sprite.Flush();
        }

        protected virtual void DrawSpritesInner() {}

        public override void Dispose() {
            DisposeHelper.Dispose(ref _wireframeRasterizerState);
            DisposeHelper.Dispose(ref _buffer);
            DisposeHelper.Dispose(ref _buffer1);
            DisposeHelper.Dispose(ref _buffer2);
            base.Dispose();
        }
    }
}
