using System;
using System.Diagnostics;
using System.Drawing;
using AcTools.Render.Base.Utils;
using AcTools.Utils.Helpers;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;
using SpriteTextRenderer.SlimDX;
using Device = SlimDX.Direct3D11.Device;
using Resource = SlimDX.Direct3D11.Resource;
using Debug = System.Diagnostics.Debug;

namespace AcTools.Render.Base {
    public abstract class BaseRenderer : IDisposable {
        private DeviceContextHolder _deviceContextHolder;
        private SwapChainDescription _swapChainDescription;
        private SwapChain _swapChain;
        private DepthStencilState _normalDepthState, _readOnlyDepthState;
        private SampleDescription _sampleDescription;

        public Device Device => _deviceContextHolder.Device;

        public DeviceContext DeviceContext => _deviceContextHolder.DeviceContext;

        public DeviceContextHolder DeviceContextHolder => _deviceContextHolder;

        public SampleDescription SampleDescription => _sampleDescription;

        public DepthStencilState NormalDepthState => _normalDepthState;

        public DepthStencilState ReadOnlyDepthState => _readOnlyDepthState;

        private int _width;
        private int _height;
        private bool _resized = true;
        private SpriteRenderer _sprite;

        protected SpriteRenderer Sprite => _sprite ?? (_sprite = new SpriteRenderer(Device));

        public bool SpriteInitialized => _sprite != null;

        public int Width {
            get { return _width; }
            set {
                if (Equals(_width, value)) return;
                _width = value;
                _resized = true;
            }
        }

        public int Height {
            get { return _height; }
            set {
                if (Equals(_height, value)) return;
                _height = value;
                _resized = true;
            }
        }

        public float AspectRatio => (float)_width / _height;

        private readonly FrameMonitor _frameMonitor = new FrameMonitor();
        private readonly Stopwatch _stopwatch = new Stopwatch();

        public float FramesPerSecond => _frameMonitor.FramesPerSecond;

        public float Elapsed => _stopwatch.ElapsedMilliseconds / 1000f;
        private float _previousElapsed;

        protected BaseRenderer() {
            Configuration.EnableObjectTracking = false;
            Configuration.ThrowOnShaderCompileError = true;
        }

        private bool _initialized;

        protected virtual SampleDescription GetSampleDescription(int msaaQuality) {
            return new SampleDescription(4, msaaQuality - 1);
        }

        /// <summary>
        /// Get Device (could be temporary, could be not), set proper SampleDescription
        /// </summary>
        private Device InitializeDevice() {
            Debug.Assert(_initialized == false);

            var device = new Device(DriverType.Hardware, DeviceCreationFlags.None);
            if (device.FeatureLevel != FeatureLevel.Level_11_0) {
                throw new Exception("Direct3D Feature Level 11 unsupported");
            }

            var msaaQuality = device.CheckMultisampleQualityLevels(Format.R8G8B8A8_UNorm, 4);
            _sampleDescription = GetSampleDescription(msaaQuality);

            return device;
        }

        /// <summary>
        /// Initialize for out-screen rendering
        /// </summary>
        public void Initialize() {
            Debug.Assert(_initialized == false);

            _deviceContextHolder = new DeviceContextHolder(InitializeDevice());

            InitializeInner();
            _initialized = true;
        }

        /// <summary>
        /// Initialize for on-screen rendering
        /// </summary>
        public void Initialize(IntPtr outputHandle) {
            Debug.Assert(_initialized == false);

            InitializeDevice().Dispose();

            _swapChainDescription = new SwapChainDescription {
                BufferCount = 2,
                ModeDescription = new ModeDescription(0, 0, new Rational(60, 1), Format.R8G8B8A8_UNorm),
                IsWindowed = true,
                OutputHandle = outputHandle,
                SampleDescription = SampleDescription,
                SwapEffect = SwapEffect.Discard,
                Usage = Usage.RenderTargetOutput
            };

            Device device;
            Device.CreateWithSwapChain(DriverType.Hardware, DeviceCreationFlags.None, _swapChainDescription, out device, out _swapChain);
            _deviceContextHolder = new DeviceContextHolder(device);

            using (var factory = _swapChain.GetParent<Factory>()) {
                factory.SetWindowAssociation(outputHandle, WindowAssociationFlags.IgnoreAll);
            }

            InitializeInner();
            _initialized = true;
        }

        private Texture2D _renderBuffer;
        private RenderTargetView _renderView;
        private Texture2D _depthBuffer;
        private DepthStencilView _depthView;

        protected virtual void Resize() {
            if (_width == 0 || _height == 0) return;

            DisposeHelper.Dispose(ref _renderBuffer);
            DisposeHelper.Dispose(ref _renderView);
            DisposeHelper.Dispose(ref _depthBuffer);
            DisposeHelper.Dispose(ref _depthView);
            DisposeHelper.Dispose(ref _normalDepthState);

            if (_swapChain != null) {
                _swapChain.ResizeBuffers(_swapChainDescription.BufferCount, _width, _height,
                                        Format.Unknown, SwapChainFlags.None);
                _renderBuffer = Resource.FromSwapChain<Texture2D>(_swapChain, 0);
            } else {
                _renderBuffer = new Texture2D(Device, new Texture2DDescription {
                    Width = _width,
                    Height = _height,
                    MipLevels = 1,
                    ArraySize = 1,
                    Format = Format.R8G8B8A8_UNorm,
                    SampleDescription = SampleDescription,
                    Usage = ResourceUsage.Default,
                    BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget,
                    CpuAccessFlags = CpuAccessFlags.None,
                    OptionFlags = ResourceOptionFlags.None
                });
            }

            _renderView = new RenderTargetView(Device, _renderBuffer);
            _depthBuffer = new Texture2D(Device, new Texture2DDescription {
                Format = Format.D32_Float_S8X24_UInt,
                ArraySize = 1,
                MipLevels = 1,
                Width = _width,
                Height = _height,
                SampleDescription = SampleDescription,
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.DepthStencil,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None
            });

            _depthView = new DepthStencilView(Device, _depthBuffer);
            DeviceContext.Rasterizer.SetViewports(new Viewport(0, 0, _width, _height, 0.0f, 1.0f));
            ResetTargets();

            _normalDepthState = DepthStencilState.FromDescription(Device, new DepthStencilStateDescription {
                IsDepthEnabled = true,
                IsStencilEnabled = false,
                DepthWriteMask = DepthWriteMask.All,
                DepthComparison = Comparison.Less,
            });

            _readOnlyDepthState = DepthStencilState.FromDescription(Device, new DepthStencilStateDescription {
                IsDepthEnabled = true,
                IsStencilEnabled = false,
                DepthWriteMask = DepthWriteMask.Zero,
                DepthComparison = Comparison.Less,
            });

            DeviceContext.OutputMerger.DepthStencilState = NormalDepthState;
            ResizeInner();
        }

        protected void ResetTargets() {
            DeviceContext.OutputMerger.SetTargets(_depthView, _renderView);
        }

        protected DepthStencilView DepthStencilView => _depthView;

        protected RenderTargetView RenderTargetView => _renderView;

        public virtual void Draw() {
            Debug.Assert(_initialized);

            if (_resized) {
                Resize();
                _resized = false;
            }

            if (!_stopwatch.IsRunning) {
                _stopwatch.Start();
            }

            var elapsed = Elapsed;
            Update(elapsed - _previousElapsed);
            _previousElapsed = elapsed;

            _frameMonitor.Tick();

            DrawInner();
            if (SpriteInitialized) {
                DrawSprites();
            }

            _swapChain?.Present(SyncInterval ? 1 : 0, PresentFlags.None);
        }

        public bool SyncInterval = true;

        protected abstract void InitializeInner();

        protected abstract void ResizeInner();

        protected virtual void DrawInner() {
            DeviceContext.ClearDepthStencilView(_depthView, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1.0f, 0);
            DeviceContext.ClearRenderTargetView(_renderView, Color.DarkCyan);
        }

        protected virtual void DrawSprites() {
            _sprite?.Flush();
        }

        protected abstract void Update(float dt);

        public void ExitFullscreen() {
            if (_swapChain == null) return;
            _swapChain.IsFullScreen = false;
        }

        public void EnterFullscreen() {
            if (_swapChain == null) return;
            _swapChain.IsFullScreen = true;
        }

        public void ToggleFullscreen() {
            if (_swapChain == null) return;
            _swapChain.IsFullScreen = !_swapChain.IsFullScreen;
        }

        public virtual void Dispose() {
            DisposeHelper.Dispose(ref _sprite);
            DisposeHelper.Dispose(ref _renderBuffer);
            DisposeHelper.Dispose(ref _renderView);
            DisposeHelper.Dispose(ref _depthBuffer);
            DisposeHelper.Dispose(ref _depthView);
            DisposeHelper.Dispose(ref _normalDepthState);
            DisposeHelper.Dispose(ref _readOnlyDepthState);
            DisposeHelper.Dispose(ref _deviceContextHolder);
            DisposeHelper.Dispose(ref _swapChain);
        }
    }
}
