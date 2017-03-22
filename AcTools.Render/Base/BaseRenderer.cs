using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using AcTools.Render.Base.PostEffects;
using AcTools.Render.Base.Sprites;
using AcTools.Render.Base.TargetTextures;
using AcTools.Render.Base.Utils;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;
using Debug = System.Diagnostics.Debug;
using Device = SlimDX.Direct3D11.Device;
using Resource = SlimDX.Direct3D11.Resource;

namespace AcTools.Render.Base {
    public abstract class BaseRenderer : IDisposable, INotifyPropertyChanged {
        public static readonly Color4 ColorTransparent = new Color4(0f, 0f, 0f, 0f);

        private DeviceContextHolder _deviceContextHolder;

        private void SetDeviceContextHolder(DeviceContextHolder value) {
            _deviceContextHolder = value;
            value.UpdateRequired += (sender, args) => {
                IsDirty = true;
            };
        }

        private SwapChainDescription _swapChainDescription;
        private SwapChain _swapChain;

        public Device Device => _deviceContextHolder.Device;

        public DeviceContext DeviceContext => _deviceContextHolder.DeviceContext;

        public DeviceContextHolder DeviceContextHolder => _deviceContextHolder;

        public bool IsDirty { get; set; }

        private int _width, _height;
        private bool _resized = true;
        private SpriteRenderer _sprite;

        [CanBeNull]
        protected SpriteRenderer Sprite => !UseSprite || _sprite != null || FeatureLevel < FeatureLevel.Level_10_0 ? _sprite :
                (_sprite = new SpriteRenderer(DeviceContextHolder));

        public bool SpriteInitialized => _sprite != null;

        private double _resolutionMultiplier = 1d;
        private double _previousResolutionMultiplier = 2d;

        /// <summary>
        /// Be careful with this option! Don’t forget to call PrepareForFinalPass() if you’re
        /// using it and remember — Z-buffer will be in original (bigger) size, but output buffer
        /// will be smaller.
        /// </summary>
        public double ResolutionMultiplier {
            get { return _resolutionMultiplier; }
            set {
                if (value < 0d) {
                    _previousResolutionMultiplier = -value;
                    return;
                }

                if (Equals(value, _resolutionMultiplier)) return;
                _resolutionMultiplier = value;

                if (!Equals(value, 1d)) {
                    _previousResolutionMultiplier = value;
                }

                OnResolutionMultiplierChanged();
                IsDirty = true;
            }
        }

        protected virtual void OnResolutionMultiplierChanged() {
            UpdateSampleDescription();

            _resized = true;
            IsDirty = true;
            OnPropertyChanged(nameof(ResolutionMultiplier));
            OnPropertyChanged(nameof(Width));
            OnPropertyChanged(nameof(Height));
            OnPropertyChanged(nameof(OutputDownscaleMultiplier));
            OnPropertyChanged(nameof(UseSsaa));
        }

        public bool UseSsaa {
            get { return !Equals(1d, _resolutionMultiplier); }
            set { ResolutionMultiplier = value ? _previousResolutionMultiplier : 1d; }
        }

        public int ActualWidth => _width;

        public int Width {
            get { return (int)(_width * ResolutionMultiplier); }
            set {
                if (Equals(_width, value)) return;
                _width = value;
                _resized = true;
                IsDirty = true;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ActualWidth));
            }
        }

        public int ActualHeight => _height;

        public int Height {
            get { return (int)(_height * ResolutionMultiplier); }
            set {
                if (Equals(_height, value)) return;
                _height = value;
                _resized = true;
                IsDirty = true;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ActualHeight));
            }
        }

        public double OutputDownscaleMultiplier => 1 / ResolutionMultiplier;

        public float AspectRatio => (float)ActualWidth / ActualHeight;

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

        public void ResetDelta() {
            _previousElapsed = Elapsed;
        }

        protected BaseRenderer() {
#if DEBUG
            Configuration.EnableObjectTracking = true;
            Configuration.DetectDoubleDispose = true;
#else
            Configuration.EnableObjectTracking = false;
            Configuration.DetectDoubleDispose = false;
#endif
        }

        private SampleDescription GetMsaaDescription(Device device) {
            var msaaQuality = device.CheckMultisampleQualityLevels(Format.R8G8B8A8_UNorm, MsaaSampleCount);
            return new SampleDescription(MsaaSampleCount, msaaQuality - 1);
        }

        private bool _useMsaa;

        public bool UseMsaa {
            get { return _useMsaa; }
            set {
                if (value == _useMsaa) return;
                _useMsaa = value;
                OnPropertyChanged();
                UpdateSampleDescription();
                IsDirty = true;
            }
        }

        private int _msaaSampleCount = 4;

        public int MsaaSampleCount {
            get { return _msaaSampleCount; }
            set {
                if (Equals(value, _msaaSampleCount)) return;
                _msaaSampleCount = value;
                OnPropertyChanged();
                UpdateSampleDescription();
                IsDirty = true;
            }
        }

        private void UpdateSampleDescription() {
            if (_deviceContextHolder == null) return;

            var sampleDescription = UseMsaa ? GetMsaaDescription(Device) : new SampleDescription(1, 0);
            if (sampleDescription != SampleDescription) {
                SampleDescription = sampleDescription;
                if (_swapChain != null) {
                    RecreateSwapChain();
                } else {
                    _resized = true;
                }
            }
        }

        public bool WpfMode { get; set; }

        public bool UseSprite { get; set; } = true;

        protected bool Initialized { get; private set; }

        public SampleDescription SampleDescription { get; private set; } = new SampleDescription(1, 0);

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

            if (UseMsaa) {
                SampleDescription = GetMsaaDescription(device);
            }

            return device;
        }

        /// <summary>
        /// Initialize for out-screen rendering
        /// </summary>
        public void Initialize() {
            Debug.Assert(Initialized == false);

            SetDeviceContextHolder(new DeviceContextHolder(InitializeDevice()));
            InitializeInner();
            Initialized = true;
        }

        public IntPtr GetRenderTarget() {
            return _renderBuffer.ComPointer;
        }

        private bool _sharedHolder;

        /// <summary>
        /// Initialize for out-screen rendering using exising holder
        /// </summary>
        public void Initialize(DeviceContextHolder existingHolder) {
            Debug.Assert(Initialized == false);

            _sharedHolder = true;
            SetDeviceContextHolder(existingHolder);
            InitializeInner();
            Initialized = true;
        }

        /// <summary>
        /// Initialize for on-screen rendering
        /// </summary>
        public void Initialize(IntPtr outputHandle) {
            Debug.Assert(Initialized == false);

            if (UseMsaa) {
                InitializeDevice().Dispose();
            }

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
            SetDeviceContextHolder(new DeviceContextHolder(device));

            using (var factory = _swapChain.GetParent<Factory>()) {
                factory.SetWindowAssociation(outputHandle, WindowAssociationFlags.IgnoreAll);
            }
            
            InitializeInner();
            Initialized = true;
        }

        private void RecreateSwapChain() {
            _swapChainDescription = new SwapChainDescription {
                BufferCount = 2,
                ModeDescription = new ModeDescription(0, 0, new Rational(60, 1), Format.R8G8B8A8_UNorm),
                IsWindowed = true,
                OutputHandle = _swapChainDescription.OutputHandle,
                SampleDescription = SampleDescription,
                SwapEffect = SwapEffect.Discard,
                Usage = Usage.RenderTargetOutput
            };

            DisposeSwapChain();
            _swapChain = new SwapChain(Device.Factory, Device, _swapChainDescription);

            _resized = true;
        }

        protected abstract void InitializeInner();

        private Texture2D _renderBuffer;
        private RenderTargetView _renderView;
        private Texture2D _depthBuffer;
        private DepthStencilView _depthView;
        
        public Texture2D RenderBuffer => _renderBuffer;

        public Texture2D DepthBuffer => _depthBuffer;

        [CanBeNull]
        protected Tuple<Texture2D, DepthStencilView> CreateDepthBuffer(int width, int height) {
            var tex = new Texture2D(Device, new Texture2DDescription {
                Format = FeatureLevel < FeatureLevel.Level_10_0 ? Format.D16_UNorm : Format.D32_Float,
                ArraySize = 1,
                MipLevels = 1,
                Width = width,
                Height = height,
                SampleDescription = SampleDescription,
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.DepthStencil,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None
            });
            return Tuple.Create(tex, new DepthStencilView(Device, tex));
        }

        protected void Resize() {
            var width = Width;
            var height = Height;

            if (width == 0 || height == 0) return;

            DisposeHelper.Dispose(ref _renderBuffer);
            DisposeHelper.Dispose(ref _renderView);
            DisposeHelper.Dispose(ref _depthBuffer);
            DisposeHelper.Dispose(ref _depthView);

            if (_swapChain != null) {
                _swapChain.ResizeBuffers(_swapChainDescription.BufferCount, ActualWidth, ActualHeight,
                        Format.Unknown, SwapChainFlags.None);
                _renderBuffer = Resource.FromSwapChain<Texture2D>(_swapChain, 0);
            } else {
                _renderBuffer = new Texture2D(Device, new Texture2DDescription {
                    Width = ActualWidth,
                    Height = ActualHeight,
                    MipLevels = 1,
                    ArraySize = 1,
                    Format = WpfMode ? Format.B8G8R8A8_UNorm : Format.R8G8B8A8_UNorm,
                    SampleDescription = SampleDescription,
                    Usage = ResourceUsage.Default,
                    BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget,
                    CpuAccessFlags = CpuAccessFlags.None,
                    OptionFlags = WpfMode ? ResourceOptionFlags.Shared : ResourceOptionFlags.None
                });
            }

            _renderView = new RenderTargetView(Device, _renderBuffer);

            var depth = CreateDepthBuffer(width, height);
            _depthBuffer = depth?.Item1;
            _depthView = depth?.Item2;
            
            DeviceContext.Rasterizer.SetViewports(OutputViewport);
            Sprite?.RefreshViewport();

            DeviceContext.Rasterizer.SetViewports(Viewport);

            ResetTargets();
            DeviceContext.OutputMerger.DepthStencilState = null;

            ResizeInner();
            DeviceContextHolder.OnResize(width, height);

            InitiallyResized = true;
        }

        protected void PrepareForFinalPass() {
            DeviceContext.Rasterizer.SetViewports(OutputViewport);
        }

        protected bool InitiallyResized { get; private set; }

        public Viewport Viewport => new Viewport(0, 0, Width, Height, 0.0f, 1.0f);

        public Viewport OutputViewport => new Viewport(0, 0, ActualWidth, ActualHeight, 0.0f, 1.0f);

        protected abstract void ResizeInner();

        protected void ResetTargets() {
            DeviceContext.OutputMerger.SetTargets(_depthView, _renderView);
        }

        protected DepthStencilView DepthStencilView => _depthView;

        protected RenderTargetView RenderTargetView => _renderView;

        public bool Draw() {
            Debug.Assert(Initialized);
            IsDirty = false;

            var result = _resized;
            if (result) {
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

            DeviceContext.Rasterizer.SetViewports(Viewport);

            DrawInner();
            if (SpriteInitialized) {
                DrawSprites();
            }

            if (_swapChain != null) {
                _swapChain.Present(SyncInterval ? 1 : 0, PresentFlags.None);
            } else {
                DeviceContext.Flush();
            }

            return result;
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

        protected void DisposeSwapChain() {
            try {
                DisposeHelper.Dispose(ref _swapChain);
                Debug.WriteLine("SWAPCHAIN DISPOSED");
            } catch (ObjectDisposedException) {
                Debug.WriteLine("SWAPCHAIN ALREADY DISPOSED?");
            } catch (Exception) {
                Debug.WriteLine("WUUT?");
            }
        }

        private WeakReference<Form> _associatedWindow;

        [CanBeNull]
        public Form AssociatedWindow {
            get {
                if (_associatedWindow != null) {
                    Form result;
                    if (_associatedWindow.TryGetTarget(out result)) return result;

                    _associatedWindow = null;
                }

                return null;
            }
        }

        public void SetAssociatedWindow(Form window) {
            _associatedWindow = window == null ? null : new WeakReference<Form>(window);
        }

        public virtual void Dispose() {
            DisposeSwapChain();

            DisposeHelper.Dispose(ref _renderView);
            DisposeHelper.Dispose(ref _renderBuffer);

            DisposeHelper.Dispose(ref _sprite);

            DisposeHelper.Dispose(ref _shotRenderBuffer);
            DisposeHelper.Dispose(ref _shotMsaaTemporaryTexture);
            DisposeHelper.Dispose(ref _shotDownsampleTexture);

            DisposeHelper.Dispose(ref _depthBuffer);
            DisposeHelper.Dispose(ref _depthView);

            if (!_sharedHolder) {
                DisposeHelper.Dispose(ref _deviceContextHolder);
            }
        }

        private TargetResourceTexture _shotRenderBuffer, _shotMsaaTemporaryTexture, _shotDownsampleTexture;

        public virtual void Shot(double multiplier, double downscale, Stream outputStream, bool lossless) {
            var resolutionMultiplier = ResolutionMultiplier;
            var format = lossless ? ImageFileFormat.Png : ImageFileFormat.Jpg;

            ResolutionMultiplier = multiplier;

            if (Equals(downscale, 1d) && !UseMsaa) {
                // Simplest case: existing buffer will do just great, so let’s use it
                Draw();
                Texture2D.ToStream(DeviceContext, _renderBuffer, format, outputStream);
            } else {
                // More complicated situation: we need to temporary replace existing _renderBuffer
                // with a custom one

                // Preparing temporary replacement for _renderBuffer…
                if (_shotRenderBuffer == null) {
                    // Let’s keep all those buffers in memory between shots to make series shooting faster
                    _shotRenderBuffer = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm);
                }

                _shotRenderBuffer.Resize(DeviceContextHolder, Width, Height, SampleDescription);

                // If we won’t call Resize() here, it will be called in Draw(), and then swap chain will
                // be recreated in a wrong time
                if (_resized) {
                    Resize();
                    _resized = false;
                }

                // Destroying current swap chain if needed
                var swapChainMode = _swapChain != null;
                if (swapChainMode) {
                    DisposeHelper.Dispose(ref _swapChain);
                }

                // Replacing _renderBuffer…
                var renderView = _renderView;
                _renderView = _shotRenderBuffer.TargetView;

                // Calculating output width and height and, if needed, preparing downscaled buffer…
                var outputWidth = (Width * downscale).RoundToInt();
                var outputHeight = (Height * downscale).RoundToInt();

                if (!Equals(downscale, 1d)) {
                    if (_shotDownsampleTexture == null) {
                        _shotDownsampleTexture = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm);
                    }

                    _shotDownsampleTexture.Resize(DeviceContextHolder, outputWidth, outputHeight, null);
                }
                
                // Ready to draw!
                Draw();

                // For MSAA, we need to copy the result into a texture without MSAA enabled to save it later
                TargetResourceTexture result;
                if (UseMsaa) {
                    if (_shotMsaaTemporaryTexture == null) {
                        _shotMsaaTemporaryTexture = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm);
                    }

                    _shotMsaaTemporaryTexture.Resize(DeviceContextHolder, Width, Height, null);

                    DeviceContextHolder.GetHelper<CopyHelper>()
                                       .Draw(DeviceContextHolder, _shotRenderBuffer.View, _shotMsaaTemporaryTexture.TargetView);
                    result = _shotMsaaTemporaryTexture;
                } else {
                    result = _shotRenderBuffer;
                }

                if (Equals(downscale, 1d)) {
                    Texture2D.ToStream(DeviceContext, result.Texture, format, outputStream);
                } else {
                    DeviceContextHolder.GetHelper<DownsampleHelper>()
                                       .Draw(DeviceContextHolder, result, _shotDownsampleTexture, false);
                    Texture2D.ToStream(DeviceContext, _shotDownsampleTexture.Texture, format, outputStream);
                }

                // Restoring old stuff
                _renderView = renderView;

                if (swapChainMode) {
                    RecreateSwapChain();
                }
            }

            ResolutionMultiplier = resolutionMultiplier;
        }

        public Image Shot(double multiplier, double downscale, bool lossless) {
            using (var stream = new MemoryStream()) {
                Shot(multiplier, downscale, stream, lossless);
                stream.Position = 0;
                return Image.FromStream(stream);
            }
        }

        protected void SaveRenderBufferAsPng(string filename, float multipler) {
            using (var stream = new MemoryStream()) {
                Texture2D.ToStream(DeviceContext, RenderBuffer, ImageFileFormat.Png, stream);
                stream.Position = 0;

                using (var image = Image.FromStream(stream)) {
                    if (Equals(multipler, 1f)) {
                        image.Save(filename, ImageFormat.Png);
                    } else {
                        using (var bitmap = new Bitmap((int)(Width * multipler), (int)(Height * multipler)))
                        using (var graphics = Graphics.FromImage(bitmap)) {
                            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            graphics.SmoothingMode = SmoothingMode.HighQuality;
                            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            graphics.DrawImage(image, 0f, 0f, Width * multipler, Height * multipler);

                            bitmap.Save(filename, ImageFormat.Png);
                        }
                    }
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
