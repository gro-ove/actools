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
                IsDirty = true;
                OnPropertyChanged();
                OnBackgroundColorChanged();
            }
        }

        private float _backgroundBrightness = 1f;

        public float BackgroundBrightness {
            get { return _backgroundBrightness; }
            set {
                if (Equals(value, _backgroundBrightness)) return;
                _backgroundBrightness = value;
                IsDirty = true;
                OnPropertyChanged();
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
        private TargetResourceTexture _bufferA, _bufferAColorGrading;
        private TargetResourceTexture _bufferH1, _bufferH2;

        protected override void InitializeInner() {
            _bufferF = TargetResourceTexture.Create(Format.R16G16B16A16_Float);
            _bufferA = TargetResourceTexture.Create(Format.R16G16B16A16_Float);
            UseBloom = true;
        }

        private byte[] _colorGradingData;

        public byte[] ColorGradingData {
            get { return _colorGradingData; }
            set {
                if (Equals(value, _colorGradingData)) return;
                _colorGradingData = value;
                IsDirty = true;
                OnPropertyChanged();
            }
        }

        private bool _useColorGrading;

        public bool UseColorGrading {
            get { return _useColorGrading; }
            set {
                if (Equals(value, _useColorGrading)) return;
                _useColorGrading = value;
                IsDirty = true;
                OnPropertyChanged();

                if (value) {
                    _bufferAColorGrading = TargetResourceTexture.Create(Format.R16G16B16A16_Float);
                    if (InitiallyResized) {
                        _bufferAColorGrading.Resize(DeviceContextHolder, ActualWidth, ActualHeight, null);
                    }
                } else {
                    DisposeHelper.Dispose(ref _bufferAColorGrading);
                }
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

        protected override void OnResolutionMultiplierChanged() {
            base.OnResolutionMultiplierChanged();
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
            _bufferAColorGrading?.Resize(DeviceContextHolder, ActualWidth, ActualHeight, null);

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

        public RasterizerState GetRasterizerState() {
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

        private float _bloomRadiusMultiplier = 1f;

        public float BloomRadiusMultiplier {
            get { return _bloomRadiusMultiplier; }
            set {
                if (value.Equals(_bloomRadiusMultiplier)) return;
                _bloomRadiusMultiplier = value;
                IsDirty = true;
                OnPropertyChanged();
            }
        }

        protected virtual void DrawSceneToBuffer() {
            DrawPrepare();

            DeviceContext.ClearRenderTargetView(InnerBuffer.TargetView, (Color4)BackgroundColor * BackgroundBrightness);

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


        private ToneMappingFn _toneMapping;

        public ToneMappingFn ToneMapping {
            get { return _toneMapping; }
            set {
                if (Equals(value, _toneMapping)) return;
                _toneMapping = value;
                IsDirty = true;
                OnPropertyChanged();
            }
        }

        private EffectTechnique GetHdrTechnique() {
            if (ToneMapping != ToneMappingFn.None) {
                _hdr.FxParams.Set(new Vector4(ToneGamma, ToneExposure, ToneWhitePoint, 0f));

                switch (ToneMapping) {
                    case ToneMappingFn.Reinhard:
                        return _hdr.TechCombine_ToneReinhard;
                    case ToneMappingFn.Filmic:
                        return _hdr.TechCombine_ToneFilmic;
                    case ToneMappingFn.FilmicReinhard:
                        return _hdr.TechCombine_ToneFilmicReinhard;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            return _hdr.TechCombine;
        }

        private float _toneGamma = 1.0f;

        public float ToneGamma {
            get { return _toneGamma; }
            set {
                if (Equals(value, _toneGamma)) return;
                _toneGamma = value;
                if (ToneMapping != ToneMappingFn.None) {
                    IsDirty = true;
                }
                OnPropertyChanged();
            }
        }

        private float _toneExposure = 0.8f;

        public float ToneExposure {
            get { return _toneExposure; }
            set {
                if (Equals(value, _toneExposure)) return;
                _toneExposure = value;
                if (ToneMapping != ToneMappingFn.None) {
                    IsDirty = true;
                }
                OnPropertyChanged();
            }
        }

        private float _toneWhitePoint = 1.66f;

        public float ToneWhitePoint {
            get { return _toneWhitePoint; }
            set {
                if (Equals(value, _toneWhitePoint)) return;
                _toneWhitePoint = value;
                if (ToneMapping != ToneMappingFn.None) {
                    IsDirty = true;
                }
                OnPropertyChanged();
            }
        }

        protected bool HdrPass(ShaderResourceView input, RenderTargetView output, Viewport viewport) {
            if (UseLensFlares) {
                // prepare effects
                if (_lensFlares == null) {
                    _lensFlares = DeviceContextHolder.GetEffect<EffectPpLensFlares>();
                }

                if (_hdr == null) {
                    _hdr = DeviceContextHolder.GetEffect<EffectPpHdr>();
                }

                if (_blur == null) {
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
                DeviceContext.Rasterizer.SetViewports(viewport);
                DeviceContext.OutputMerger.SetTargets(output);

                _hdr.FxInputMap.SetResource(input);
                _hdr.FxBloomMap.SetResource(_bufferH2.View);
                GetHdrTechnique().DrawAllPasses(DeviceContext, 6);

                return true;
            }

            if (UseBloom) {
                // prepare effects
                if (_hdr == null) {
                    _hdr = DeviceContextHolder.GetEffect<EffectPpHdr>();
                }

                if (_blur == null) {
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
                _blur.Blur(DeviceContextHolder, _bufferH1, _bufferH2, 0.5f * BloomRadiusMultiplier, 2);

                // combine original buffer and buffer #1 with blurred bright areas to buffer #2
                DeviceContext.Rasterizer.SetViewports(viewport);
                DeviceContext.OutputMerger.SetTargets(output);

                _hdr.FxInputMap.SetResource(input);
                _hdr.FxBloomMap.SetResource(_bufferH1.View);
                GetHdrTechnique().DrawAllPasses(DeviceContext, 6);

                return true;
            }

            if (ToneMapping != ToneMappingFn.None) {
                if (_hdr == null) {
                    _hdr = DeviceContextHolder.GetEffect<EffectPpHdr>();
                }

                DeviceContext.Rasterizer.SetViewports(viewport);
                DeviceContext.OutputMerger.SetTargets(output);
                DeviceContextHolder.PrepareQuad(_hdr.LayoutPT);

                _hdr.FxInputMap.SetResource(input);
                _hdr.FxBloomMap.SetResource(null);
                GetHdrTechnique().DrawAllPasses(DeviceContext, 6);
                return true;
            }

            return false;
        }

        private bool _colorGradingSet;
        private Texture3D _colorGradingTexture;
        private ShaderResourceView _colorGradingView;

        /// <summary>
        /// Throws an exception if something goes wrong.
        /// </summary>
        public void LoadColorGradingData() {
            DisposeHelper.Dispose(ref _colorGradingView);
            DisposeHelper.Dispose(ref _colorGradingTexture);

            try {
                _colorGradingTexture = Texture3D.FromMemory(Device, ColorGradingData);
                _colorGradingView = new ShaderResourceView(Device, _colorGradingTexture);
            } catch (Exception) {
                DisposeHelper.Dispose(ref _colorGradingView);
                DisposeHelper.Dispose(ref _colorGradingTexture);
                throw;
            }
        }

        private bool ColorGradingPass(ShaderResourceView input, RenderTargetView output, Viewport viewport) {
            if (ColorGradingData == null) return false;

            if (!_colorGradingSet) {
                _colorGradingSet = true;

                try {
                    LoadColorGradingData();
                } catch (Exception e) {
                    AcToolsLogging.Write(e);
                }
            }

            if (_colorGradingView == null) return false;

            if (_hdr == null) {
                _hdr = DeviceContextHolder.GetEffect<EffectPpHdr>();
            }

            DeviceContext.Rasterizer.SetViewports(viewport);
            DeviceContext.OutputMerger.SetTargets(output);

            _hdr.FxInputMap.SetResource(input);
            _hdr.FxColorGradingMap.SetResource(_colorGradingView);
            _hdr.TechColorGrading.DrawAllPasses(DeviceContext, 6);
            return true;
        }
        
        private bool AaPass(ShaderResourceView input, RenderTargetView output) {
            if (IsSmaaAvailable && UseSmaa) {
                throw new NotImplementedException();
            }

            if (UseSsaa) {
                if (UseFxaa) {
                    DeviceContextHolder.GetHelper<FxaaHelper>().Draw(DeviceContextHolder, input, _bufferFSsaaFxaa.TargetView, new Vector2(Width, Height));
                    input = _bufferFSsaaFxaa.View;
                }

                DeviceContextHolder.GetHelper<DownsampleHelper>().Draw(DeviceContextHolder, input, new Vector2(Width, Height),
                        output, new Vector2(ActualWidth, ActualHeight), TemporaryFlag);
                return true;
            }

            if (UseFxaa) {
                DeviceContextHolder.GetHelper<FxaaHelper>().Draw(DeviceContextHolder, input, output);
                return true;
            }
            
            return false;
        }

        private void AaThenBloom() {
            var aaView = AaPass(_bufferF.View, _bufferA.TargetView) ? _bufferA.View : _bufferF.View;

            if (UseColorGrading) {
                var hdrView = HdrPass(aaView, _bufferAColorGrading.TargetView, _bufferAColorGrading.Viewport) ? _bufferAColorGrading.View : aaView;
                if (!ColorGradingPass(hdrView, RenderTargetView, OutputViewport)) {
                    // nothing happened in color grading stage
                    DeviceContextHolder.GetHelper<CopyHelper>().Draw(DeviceContextHolder, hdrView, RenderTargetView);
                }
            } else {
                if (!HdrPass(aaView, RenderTargetView, OutputViewport)) {
                    // HDR stage didn’t move AA buffer to RTV
                    DeviceContextHolder.GetHelper<CopyHelper>().Draw(DeviceContextHolder, aaView, RenderTargetView);
                }
            }
        }

        private void BloomThenAa() {
            var bloomView = HdrPass(_bufferF.View, _bufferA.TargetView, OutputViewport) ? _bufferA.View : _bufferF.View;
            var aa = AaPass(bloomView, RenderTargetView);

            if (!aa) {
                // AA stage didn’t move bloomed buffer to RTV
                DeviceContextHolder.GetHelper<CopyHelper>().Draw(DeviceContextHolder, bloomView, RenderTargetView);
            }
        }

        protected override void DrawInner() {
            DrawSceneToBuffer();
            AaThenBloom();
        }

        public bool TemporaryFlag { get; set; }

        public bool KeepFxaaWhileShooting;

        public override void Shot(double multiplier, double downscale, Stream outputStream, bool lossless) {
            if (KeepFxaaWhileShooting || Equals(multiplier, 1d) && Equals(downscale, 1d)) {
                base.Shot(multiplier, downscale, outputStream, lossless);
            } else {
                var useFxaa = UseFxaa;
                UseFxaa = false;

                try {
                    base.Shot(multiplier, downscale, outputStream, lossless);
                } finally {
                    UseFxaa = useFxaa;
                }
            }
        }

        protected sealed override void DrawSprites() {
            var sprite = Sprite;
            if (sprite == null) return;
            DrawSpritesInner();
            sprite.Flush();
        }

        protected virtual void DrawSpritesInner() {}

        public override void Dispose() {
            DisposeHelper.Dispose(ref _bufferF);
            DisposeHelper.Dispose(ref _bufferFSsaaFxaa);

            DisposeHelper.Dispose(ref _bufferA);
            DisposeHelper.Dispose(ref _bufferAColorGrading);

            DisposeHelper.Dispose(ref _bufferH1);
            DisposeHelper.Dispose(ref _bufferH2);

            DisposeHelper.Dispose(ref _colorGradingView);
            DisposeHelper.Dispose(ref _colorGradingTexture);

            base.Dispose();
        }
    }
}
