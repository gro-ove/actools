using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.CompilerServices;
using AcTools.Render.Base.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;
using SpriteTextRenderer.SlimDX;
using Device = SlimDX.Direct3D11.Device;
using Resource = SlimDX.Direct3D11.Resource;
using Debug = System.Diagnostics.Debug;

namespace AcTools.Render.Base {
    public abstract class BaseRenderer : IDisposable, INotifyPropertyChanged {
        public static readonly Color4 ColorTransparent = new Color4(0f, 0f, 0f, 0f);

        private DeviceContextHolder _deviceContextHolder;
        private SwapChainDescription _swapChainDescription;
        private SwapChain _swapChain;

        public Device Device => _deviceContextHolder.Device;

        public DeviceContext DeviceContext => _deviceContextHolder.DeviceContext;

        public DeviceContextHolder DeviceContextHolder => _deviceContextHolder;

        public bool IsDirty { get; set; }

        private int _width;
        private int _height;
        private bool _resized = true;
        private SpriteRenderer _sprite;

        [CanBeNull]
        protected SpriteRenderer Sprite => _sprite != null || FeatureLevel < FeatureLevel.Level_10_0 ? _sprite :
                (_sprite = new SpriteRenderer(Device));

        public bool SpriteInitialized => _sprite != null;

        public int Width {
            get { return _width; }
            set {
                if (Equals(_width, value)) return;
                _width = value;
                _resized = true;
                OnPropertyChanged();
            }
        }

        public int Height {
            get { return _height; }
            set {
                if (Equals(_height, value)) return;
                _height = value;
                _resized = true;
                OnPropertyChanged();
            }
        }

        public float AspectRatio => (float)_width / _height;

        private readonly FrameMonitor _frameMonitor = new FrameMonitor();
        private readonly Stopwatch _stopwatch = new Stopwatch();

        private float _previousFramesPerSecond;

        public float FramesPerSecond {
            get {
                var current = _frameMonitor.FramesPerSecond;
                if (Math.Abs(_previousFramesPerSecond - current) > 0.0001f) {
                    _previousFramesPerSecond = current;
                    OnPropertyChanged(nameof(FramesPerSecond));
                }
                return current;
            }
        }

        public float Elapsed => _stopwatch.ElapsedMilliseconds / 1000f;
        private float _previousElapsed;

        protected BaseRenderer() {
            Configuration.EnableObjectTracking = false;
            Configuration.ThrowOnShaderCompileError = true;
        }

        protected bool Initialized { get; private set; }

        protected int MsaaQuality { get; private set; }

        protected virtual SampleDescription SampleDescription => new SampleDescription(1, 0);

        protected abstract FeatureLevel FeatureLevel { get; }
   
        /// <summary>
        /// Get Device (could be temporary, could be not), set proper SampleDescription
        /// </summary>
        private Device InitializeDevice() {
            Debug.Assert(Initialized == false);

            var device = new Device(DriverType.Hardware, DeviceCreationFlags.None);
            if (device.FeatureLevel < FeatureLevel) {
                throw new Exception($"Direct3D Feature {FeatureLevel} unsupported");
            }

            MsaaQuality = device.CheckMultisampleQualityLevels(Format.R8G8B8A8_UNorm, 4);
            return device;
        }

        /// <summary>
        /// Initialize for out-screen rendering
        /// </summary>
        public void Initialize() {
            Debug.Assert(Initialized == false);

            _deviceContextHolder = new DeviceContextHolder(InitializeDevice());
            InitializeInner();

            Initialized = true;
        }

        private bool _sharedHolder;

        /// <summary>
        /// Initialize for out-screen rendering using exising holder
        /// </summary>
        public void Initialize(DeviceContextHolder existingHolder) {
            Debug.Assert(Initialized == false);

            _sharedHolder = true;
            _deviceContextHolder = existingHolder;
            InitializeInner();

            Initialized = true;
        }

        /// <summary>
        /// Initialize for on-screen rendering
        /// </summary>
        public void Initialize(IntPtr outputHandle) {
            Debug.Assert(Initialized == false);
            
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

            Initialized = true;
        }

        protected abstract void InitializeInner();

        private Texture2D _renderBuffer;
        private RenderTargetView _renderView;
        private Texture2D _depthBuffer;
        private DepthStencilView _depthView;
        
        public Texture2D RenderBuffer => _renderBuffer;

        public Texture2D DepthBuffer => _depthBuffer;

        protected virtual void Resize() {
            if (_width == 0 || _height == 0) return;

            DisposeHelper.Dispose(ref _renderBuffer);
            DisposeHelper.Dispose(ref _renderView);
            DisposeHelper.Dispose(ref _depthBuffer);
            DisposeHelper.Dispose(ref _depthView);

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
                Format = FeatureLevel < FeatureLevel.Level_10_0 ? Format.D16_UNorm : Format.D32_Float,
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
            DeviceContext.Rasterizer.SetViewports(Viewport);
            ResetTargets();

            DeviceContext.OutputMerger.DepthStencilState = null;
            Sprite?.RefreshViewport();

            ResizeInner();
            DeviceContextHolder.OnResize(Width, Height);

            InitiallyResized = true;
        }

        protected bool InitiallyResized { get; private set; }

        public Viewport Viewport => new Viewport(0, 0, _width, _height, 0.0f, 1.0f);

        protected abstract void ResizeInner();

        protected void ResetTargets() {
            DeviceContext.OutputMerger.SetTargets(_depthView, _renderView);
        }

        protected DepthStencilView DepthStencilView => _depthView;

        protected RenderTargetView RenderTargetView => _renderView;

        public virtual void Draw() {
            Debug.Assert(Initialized);
            IsDirty = false;

            if (_resized) {
                Resize();
                _resized = false;
            }

            if (!_stopwatch.IsRunning) {
                _stopwatch.Start();
            }

            var elapsed = Elapsed;
            OnTick(elapsed - _previousElapsed);
            Tick?.Invoke(this, new TickEventArgs(elapsed - _previousElapsed));
            _previousElapsed = elapsed;

            if (_frameMonitor.Tick()) {
                OnPropertyChanged(nameof(FramesPerSecond));
            }

            DrawInner();
            if (SpriteInitialized) {
                DrawSprites();
            }

            _swapChain?.Present(SyncInterval ? 1 : 0, PresentFlags.None);
        }

        private bool _syncInterval = true;

        public bool SyncInterval {
            get { return _syncInterval; }
            set {
                if (Equals(value, _syncInterval)) return;
                _syncInterval = value;
                OnPropertyChanged();
            }
        }

        protected virtual void DrawInner() {
            DeviceContext.ClearDepthStencilView(_depthView, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1.0f, 0);
            DeviceContext.ClearRenderTargetView(_renderView, Color.DarkCyan);
        }

        protected virtual void DrawSprites() {
            _sprite?.Flush();
        }

        public event TickEventHandler Tick;

        protected abstract void OnTick(float dt);

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

            if (!_sharedHolder) {
                DisposeHelper.Dispose(ref _deviceContextHolder);
            }

            DisposeHelper.Dispose(ref _swapChain);
        }

        public virtual Image Shot(int multipler) {
            var width = Width;
            var height = Height;

            Width = width * multipler;
            Height = height * multipler;
            Draw();

            Width = width;
            Height = height;

            var stream = new System.IO.MemoryStream();
            Texture2D.ToStream(DeviceContext, _renderBuffer, ImageFileFormat.Jpg, stream);
            stream.Position = 0;
            return Image.FromStream(stream);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
