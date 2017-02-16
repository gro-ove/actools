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

                DisposeHelper.Dispose(ref _bufferSmaa);
                if (value) {
                    _bufferSmaa = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm);
                }

                if (!InitiallyResized) return;
                ResizeBuffers();

                IsDirty = true;
                OnPropertyChanged();
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

                DisposeHelper.Dispose(ref _buffer);
                DisposeHelper.Dispose(ref _buffer1);
                DisposeHelper.Dispose(ref _buffer2);
                DisposeHelper.Dispose(ref _buffer3);

                if (_useBloom) {
                    _buffer = TargetResourceTexture.Create(Format.R16G16B16A16_Float);
                    _buffer1 = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm);
                    _buffer2 = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm);
                    _buffer3 = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm);
                } else {
                    _buffer = TargetResourceTexture.Create(Format.R16G16B16A16_Float);
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

        protected override FeatureLevel FeatureLevel => FeatureLevel.Level_10_0;

        private TargetResourceTexture _buffer, _buffer1, _buffer2, _buffer3, _bufferSmaa;

        protected TargetResourceTexture InnerBuffer => _buffer;

        protected override void InitializeInner() {
            UseBloom = true;
        }

        private void ResizeBuffers() {
            _buffer.Resize(DeviceContextHolder, Width, Height, SampleDescription);
            _buffer1?.Resize(DeviceContextHolder, ActualWidth / 2, ActualHeight / 2, null);
            _buffer3?.Resize(DeviceContextHolder, ActualWidth / 2, ActualHeight / 2, null);
            _buffer2?.Resize(DeviceContextHolder, ActualWidth, ActualHeight, null);
            _bufferSmaa?.Resize(DeviceContextHolder, ActualWidth, ActualHeight, null);
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
                DeviceContext.Rasterizer.SetViewports(_buffer1.Viewport);
                DeviceContext.OutputMerger.SetTargets(_buffer1.TargetView);
                DeviceContext.ClearRenderTargetView(_buffer1.TargetView, ColorTransparent);

                DeviceContextHolder.PrepareQuad(_hdr.LayoutPT);
                _hdr.FxInputMap.SetResource(_buffer.View);
                _hdr.TechBloomHighThreshold.DrawAllPasses(DeviceContext, 6);


                DeviceContext.Rasterizer.SetViewports(_buffer3.Viewport);
                DeviceContext.OutputMerger.SetTargets(_buffer3.TargetView);
                DeviceContext.ClearRenderTargetView(_buffer3.TargetView, ColorTransparent);

                DeviceContextHolder.PrepareQuad(_lensFlares.LayoutPT);
                _lensFlares.FxInputMap.SetResource(_buffer1.View);
                _lensFlares.TechGhosts.DrawAllPasses(DeviceContext, 6);

                // blur bright areas from buffer #1 to itself using downscaled buffer #3 as a temporary one 
                _blur.Blur(DeviceContextHolder, _buffer3, _buffer1, 2f, 2);

                // combine original buffer and buffer #1 with blurred bright areas to buffer #2
                DeviceContext.Rasterizer.SetViewports(_buffer2.Viewport);
                DeviceContext.OutputMerger.SetTargets(_buffer2.TargetView);

                _hdr.FxInputMap.SetResource(_buffer.View);
                _hdr.FxBloomMap.SetResource(_buffer3.View);
                _hdr.TechCombine.DrawAllPasses(DeviceContext, 6);

                // buffer #2 is ready
                result = _buffer2.View;
            } else if (UseBloom) {
                // prepare effects
                if (_hdr == null) {
                    _hdr = DeviceContextHolder.GetEffect<EffectPpHdr>();
                    _blur = DeviceContextHolder.GetHelper<BlurHelper>();
                }

                // filter bright areas by high threshold to downscaled buffer #1
                DeviceContext.Rasterizer.SetViewports(_buffer1.Viewport);
                DeviceContext.OutputMerger.SetTargets(_buffer1.TargetView);
                DeviceContext.ClearRenderTargetView(_buffer1.TargetView, ColorTransparent);

                DeviceContextHolder.PrepareQuad(_hdr.LayoutPT);
                _hdr.FxInputMap.SetResource(_buffer.View);
                _hdr.TechBloomHighThreshold.DrawAllPasses(DeviceContext, 6);

                // blur bright areas from buffer #1 to itself using downscaled buffer #3 as a temporary one
                _blur.Blur(DeviceContextHolder, _buffer1, _buffer3, 0.5f * BloomRadiusMultipler, 2);

                // combine original buffer and buffer #1 with blurred bright areas to buffer #2
                DeviceContext.Rasterizer.SetViewports(_buffer2.Viewport);
                DeviceContext.OutputMerger.SetTargets(_buffer2.TargetView);

                _hdr.FxInputMap.SetResource(_buffer.View);
                _hdr.FxBloomMap.SetResource(_buffer1.View);
                _hdr.TechCombine.DrawAllPasses(DeviceContext, 6);

                // buffer #2 is ready
                result = _buffer2.View;
            } else {
                result = _buffer.View;
            }

            PrepareForFinalPass();
            DeviceContext.ClearRenderTargetView(RenderTargetView, ColorTransparent);
            if (UseSsaa) {
                if (UseFxaa) {
                    DeviceContextHolder.GetHelper<FxaaHelper>().Draw(DeviceContextHolder, result, RenderTargetView);
                } else {
                    DeviceContextHolder.GetHelper<DownsampleHelper>().Draw(DeviceContextHolder, result, new Vector2(Width, Height),
                            RenderTargetView, new Vector2(ActualWidth, ActualHeight), TemporaryFlag);
                }
            } else if (UseFxaa) {
                DeviceContextHolder.GetHelper<FxaaHelper>().Draw(DeviceContextHolder, result, RenderTargetView);
            } else if (IsSmaaAvailable && UseSmaa) {
                var temporary = result == _buffer.View ? _buffer2 : _buffer;
                DeviceContextHolder.GetHelper<SmaaHelper>().Draw(DeviceContextHolder, result, RenderTargetView, temporary, _bufferSmaa);
            } else { 
                DeviceContextHolder.GetHelper<CopyHelper>().Draw(DeviceContextHolder, result, RenderTargetView);
            }
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
            DisposeHelper.Dispose(ref _buffer);
            DisposeHelper.Dispose(ref _buffer1);
            DisposeHelper.Dispose(ref _buffer2);
            DisposeHelper.Dispose(ref _buffer3);
            DisposeHelper.Dispose(ref _bufferSmaa);
            base.Dispose();
        }
    }
}
