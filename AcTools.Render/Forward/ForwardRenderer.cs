using System;
using System.Drawing;
using System.IO;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.PostEffects;
using AcTools.Render.Base.TargetTextures;
using AcTools.Render.Base.Utils;
using AcTools.Render.Shaders;
using AcTools.Utils.Helpers;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;

namespace AcTools.Render.Forward {
    public abstract class ForwardRenderer : SceneRenderer {
        private Color _backgroundColor = Color.Gray;

        public Color BackgroundColor {
            get { return _backgroundColor; }
            set {
                if (Equals(value, _backgroundColor)) return;
                _backgroundColor = value;
                OnBackgroundColorChanged();
            }
        }

        protected virtual void OnBackgroundColorChanged() {}

        private bool _useInterpolationCamera;

        public bool UseInterpolationCamera {
            get { return _useInterpolationCamera; }
            set {
                if (Equals(value, _useInterpolationCamera)) return;
                _useInterpolationCamera = value;
                OnPropertyChanged();
            }
        }

        protected override FeatureLevel FeatureLevel => FeatureLevel.Level_10_0;

        // F: full size, A: actual size, H: half size
        private TargetResourceTexture _bufferF, _bufferFSsaaFxaa;
        private TargetResourceTexture _bufferA;
        private TargetResourceTexture _bufferH1, _bufferH2;

        protected override void InitializeInner() {
            _bufferF = TargetResourceTexture.Create(Format.R16G16B16A16_Float);
            _bufferA = TargetResourceTexture.Create(Format.R16G16B16A16_Float);
            UseBloom = true;
        }

        private bool _useFxaa = true;

        public bool UseFxaa {
            get { return _useFxaa; }
            set {
                if (Equals(_useFxaa, value)) return;
                _useFxaa = value;
                IsDirty = true;
                OnPropertyChanged();
                UpdateSsaaFxaaBuffer();
            }
        }

        private bool _isFxaaAvailable;

        public bool IsFxaaAvailable {
            get { return _isFxaaAvailable; }
            set {
                if (Equals(value, _isFxaaAvailable)) return;
                _isFxaaAvailable = value;
                OnPropertyChanged();
            }
        }

        public bool IsSmaaAvailable => false;

        private bool _useSmaa;

        public bool UseSmaa {
            get { return _useSmaa; }
            set {
                if (!IsSmaaAvailable || Equals(value, _useSmaa)) return;
                _useSmaa = value;

                if (!InitiallyResized) return;
                ResizeBuffers();

                IsDirty = true;
                OnPropertyChanged();
            }
        }

        protected override void OnResolutionMultiplerChanged() {
            base.OnResolutionMultiplerChanged();
            UpdateSsaaFxaaBuffer();
        }

        private void UpdateSsaaFxaaBuffer() {
            if (!UseSsaa || !UseFxaa) {
                DisposeHelper.Dispose(ref _bufferFSsaaFxaa);
            } else if (_bufferFSsaaFxaa == null) {
                _bufferFSsaaFxaa = TargetResourceTexture.Create(Format.R16G16B16A16_Float);
                ResizeBuffers();
            }
        }

        private bool _showWireframe;

        public virtual bool ShowWireframe {
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
                
                DisposeHelper.Dispose(ref _bufferH1);
                DisposeHelper.Dispose(ref _bufferH2);

                if (_useBloom) {
                    _bufferH1 = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm);
                    _bufferH2 = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm);
                }

                if (!InitiallyResized) return;
                ResizeBuffers();

                IsDirty = true;
                OnPropertyChanged();
            }
        }

        private bool _useLensFlares;

        public bool UseLensFlares {
            get { return _useLensFlares; }
            set {
                if (Equals(value, _useLensFlares)) return;
                _useLensFlares = value;
                if (value) {
                    UseBloom = true;
                }
                
                //if (!InitiallyResized) return;
                //ResizeBuffers();

                IsDirty = true;
                OnPropertyChanged();
            }
        }
        
        protected TargetResourceTexture InnerBuffer => _bufferOverride ?? _bufferF;
        private TargetResourceTexture _bufferOverride;

        protected void SetInnerBuffer(TargetResourceTexture texture) {
            _bufferOverride = texture;
        }

        private void ResizeBuffers() {
            _bufferF.Resize(DeviceContextHolder, Width, Height, SampleDescription);
            _bufferFSsaaFxaa?.Resize(DeviceContextHolder, Width, Height, null);

            _bufferA?.Resize(DeviceContextHolder, ActualWidth, ActualHeight, null);

            _bufferH1?.Resize(DeviceContextHolder, ActualWidth / 2, ActualHeight / 2, null);
            _bufferH2?.Resize(DeviceContextHolder, ActualWidth / 2, ActualHeight / 2, null);
        }

        protected override void ResizeInner() {
            base.ResizeInner();
            ResizeBuffers();
        }

        protected virtual void DrawAfter() { }

        protected ICamera ActualCamera => UseInterpolationCamera ? (ICamera)_interpolationCamera : Camera;

        private readonly InterpolationCamera _interpolationCamera = new InterpolationCamera(5f);

        protected override void OnTick(float dt) {
            IsFxaaAvailable = !UseMsaa || UseSsaa;

            if (UseInterpolationCamera) {
                _interpolationCamera.Update(Camera, dt);
            }
        }

        public virtual RasterizerState GetRasterizerState() {
            return ShowWireframe ? DeviceContextHolder.States.WireframeState : null;
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

        private EffectPpHdr _hdr;
        private BlurHelper _blur;
        private EffectPpLensFlares _lensFlares;

        public float BloomRadiusMultipler = 1f;

        protected virtual void DrawSceneToBuffer() {
            DrawPrepare();

            DeviceContext.ClearRenderTargetView(InnerBuffer.TargetView, BackgroundColor);

            if (DepthStencilView != null) {
                DeviceContext.ClearDepthStencilView(DepthStencilView, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1f, 0);
                DeviceContext.OutputMerger.SetTargets(DepthStencilView, InnerBuffer.TargetView);
            } else {
                DeviceContext.OutputMerger.SetTargets(InnerBuffer.TargetView);
            }

            DrawScene();
            DrawAfter();

            DeviceContext.OutputMerger.DepthStencilState = null;
            DeviceContext.OutputMerger.BlendState = null;
            DeviceContext.Rasterizer.State = null;
        }

        protected void FinalPpPass(ShaderResourceView input) {
            if (UseLensFlares) {
                // prepare effects
                if (_lensFlares == null) {
                    _lensFlares = DeviceContextHolder.GetEffect<EffectPpLensFlares>();
                }

                if (_hdr == null) {
                    _hdr = DeviceContextHolder.GetEffect<EffectPpHdr>();
                    _blur = DeviceContextHolder.GetHelper<BlurHelper>();
                }

                // filter bright areas by high threshold to downscaled buffer #1
                DeviceContext.Rasterizer.SetViewports(_bufferH1.Viewport);
                DeviceContext.OutputMerger.SetTargets(_bufferH1.TargetView);
                DeviceContext.ClearRenderTargetView(_bufferH1.TargetView, ColorTransparent);

                DeviceContextHolder.PrepareQuad(_hdr.LayoutPT);
                _hdr.FxInputMap.SetResource(input);
                _hdr.TechBloomHighThreshold.DrawAllPasses(DeviceContext, 6);

                DeviceContext.Rasterizer.SetViewports(_bufferH2.Viewport);
                DeviceContext.OutputMerger.SetTargets(_bufferH2.TargetView);
                DeviceContext.ClearRenderTargetView(_bufferH2.TargetView, ColorTransparent);

                DeviceContextHolder.PrepareQuad(_lensFlares.LayoutPT);
                _lensFlares.FxInputMap.SetResource(_bufferH1.View);
                _lensFlares.TechGhosts.DrawAllPasses(DeviceContext, 6);

                // blur bright areas from buffer #1 to itself using downscaled buffer #3 as a temporary one 
                _blur.Blur(DeviceContextHolder, _bufferH2, _bufferH1, 2f, 2);

                // combine original buffer and buffer #1 with blurred bright areas to buffer #2
                DeviceContext.Rasterizer.SetViewports(OutputViewport);
                DeviceContext.OutputMerger.SetTargets(RenderTargetView);

                _hdr.FxInputMap.SetResource(input);
                _hdr.FxBloomMap.SetResource(_bufferH2.View);
                _hdr.TechCombine.DrawAllPasses(DeviceContext, 6);
            } else if (UseBloom) {
                // prepare effects
                if (_hdr == null) {
                    _hdr = DeviceContextHolder.GetEffect<EffectPpHdr>();
                    _blur = DeviceContextHolder.GetHelper<BlurHelper>();
                }

                // filter bright areas by high threshold to downscaled buffer #1
                DeviceContext.Rasterizer.SetViewports(_bufferH1.Viewport);
                DeviceContext.OutputMerger.SetTargets(_bufferH1.TargetView);
                DeviceContext.ClearRenderTargetView(_bufferH1.TargetView, ColorTransparent);

                DeviceContextHolder.PrepareQuad(_hdr.LayoutPT);
                _hdr.FxInputMap.SetResource(input);
                _hdr.TechBloomHighThreshold.DrawAllPasses(DeviceContext, 6);

                // blur bright areas from buffer #1 to itself using downscaled buffer #3 as a temporary one
                _blur.Blur(DeviceContextHolder, _bufferH1, _bufferH2, 0.5f * BloomRadiusMultipler, 2);

                // combine original buffer and buffer #1 with blurred bright areas to buffer #2
                DeviceContext.Rasterizer.SetViewports(OutputViewport);
                DeviceContext.OutputMerger.SetTargets(RenderTargetView);

                _hdr.FxInputMap.SetResource(input);
                _hdr.FxBloomMap.SetResource(_bufferH1.View);
                _hdr.TechCombine.DrawAllPasses(DeviceContext, 6);
            } else {
                DeviceContextHolder.GetHelper<CopyHelper>().Draw(DeviceContextHolder, input, RenderTargetView);
            }
        }

        private ShaderResourceView AaPass(ShaderResourceView input) {
            if (IsSmaaAvailable && UseSmaa) {
                throw new NotImplementedException();
            }

            if (UseSsaa) {
                if (UseFxaa) {
                    DeviceContextHolder.GetHelper<FxaaHelper>().Draw(DeviceContextHolder, input, _bufferFSsaaFxaa.TargetView, new Vector2(Width, Height));
                    input = _bufferFSsaaFxaa.View;
                }

                DeviceContextHolder.GetHelper<DownsampleHelper>().Draw(DeviceContextHolder, input, new Vector2(Width, Height),
                        _bufferA.TargetView, new Vector2(ActualWidth, ActualHeight), TemporaryFlag);

                return _bufferA.View;
            }

            if (UseFxaa) {
                DeviceContextHolder.GetHelper<FxaaHelper>().Draw(DeviceContextHolder, input, _bufferA.TargetView);
                return _bufferA.View;
            }
            
            return input;
        }

        protected override void DrawInner() {
            DrawSceneToBuffer();
            FinalPpPass(AaPass(_bufferF.View));
        }

        public bool TemporaryFlag { get; set; }

        public bool KeepFxaaWhileShooting;

        public override void Shot(double multipler, double downscale, Stream outputStream, bool lossless) {
            if (KeepFxaaWhileShooting || Equals(multipler, 1d) && Equals(downscale, 1d)) {
                base.Shot(multipler, downscale, outputStream, lossless);
            } else {
                var useFxaa = UseFxaa;
                UseFxaa = false;

                try {
                    base.Shot(multipler, downscale, outputStream, lossless);
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
            DisposeHelper.Dispose(ref _bufferF);
            DisposeHelper.Dispose(ref _bufferFSsaaFxaa);

            DisposeHelper.Dispose(ref _bufferA);

            DisposeHelper.Dispose(ref _bufferH1);
            DisposeHelper.Dispose(ref _bufferH2);
            base.Dispose();
        }
    }
}
