// uncomment this to render each and every frame, as opposed to only when the user signals an update is needed
//#define USE_PERFRAME_MODE

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using SlimDX;
using SlimDX.Direct3D9;

namespace AcManager.Controls {
    public interface IRenderEngine {
        void OnDeviceCreated(object sender, EventArgs e);
        void OnDeviceDestroyed(object sender, EventArgs e);
        void OnDeviceLost(object sender, EventArgs e);
        void OnDeviceReset(object sender, EventArgs e);
        void OnMainLoop(object sender, EventArgs e);
    }

    public partial class SlimDxControl : ContentControl {
        private IRenderEngine _renderEngine;

        public IRenderEngine RenderEngine {
            private set {
                _renderEngine = value;

                DeviceCreated += _renderEngine.OnDeviceCreated;
                DeviceDestroyed += _renderEngine.OnDeviceDestroyed;
                DeviceLost += _renderEngine.OnDeviceLost;
                DeviceReset += _renderEngine.OnDeviceReset;
                MainLoop += _renderEngine.OnMainLoop;
            }
            get { return _renderEngine; }
        }

        public SlimDxControl() {
            InitializeComponent();

#if USE_PERFRAME_MODE
            AlwaysRender = true;
#else
            AlwaysRender = false;
#endif
        }

        public void Setup(IRenderEngine renderEngine) {
            Initialize(true);
            RenderEngine = renderEngine;
        }

        // we use it for 3D
        private Direct3D _direct3D;
        private Direct3DEx _direct3DEx;
        private Device _device;
        private DeviceEx _deviceEx;
        private Surface _backBufferSurface;

        // device settings
        Format _adapterFormat = Format.X8R8G8B8;
        Format _backbufferFormat = Format.A8R8G8B8;
        Format _depthStencilFormat = Format.D16;
        CreateFlags _createFlags = CreateFlags.Multithreaded;

        private PresentParameters _pp;

        private bool _startThread;
        private bool _sizeChanged;

        public Image Image {
            get { return ImageControl; }
        }

        #region Properties
        public bool UseDeviceEx { get; private set; }

        public Direct3D Direct3D {
            get { return UseDeviceEx ? _direct3DEx : _direct3D; }
        }

        public Device Device {
            get { return UseDeviceEx ? _deviceEx : _device; }
        }

        public bool Available { get; private set; }
        #endregion

        #region Events

        public event EventHandler MainLoop;
        public event EventHandler DeviceCreated;
        public event EventHandler DeviceDestroyed;
        public event EventHandler DeviceLost;
        public event EventHandler DeviceReset;

        //can't figure out how to access remote session status through .NET
        [DllImport("user32")]
        static extern int GetSystemMetrics(int smIndex);
        const int SmRemoteSession = 0x1000;

        protected virtual void OnInitialize() {
        }

        protected virtual void OnMainLoop(EventArgs e) {
            if (MainLoop != null) {
                MainLoop(this, e);
            }
        }

        protected virtual void OnDeviceCreated(EventArgs e) {
            if (DeviceCreated != null) {
                DeviceCreated(this, e);
            }
            ForceRendering();
        }

        protected virtual void OnDeviceDestroyed(EventArgs e) {
            if (DeviceDestroyed != null) {
                DeviceDestroyed(this, e);
            }
        }

        protected virtual void OnDeviceLost(EventArgs e) {
            if (DeviceLost != null) {
                DeviceLost(this, e);
            }
        }

        protected virtual void OnDeviceReset(EventArgs e) {
            if (DeviceReset != null)
                DeviceReset(this, e);
        }

        #endregion

        protected override void OnInitialized(EventArgs e) {
            base.OnInitialized(e);
            InitializeDirect3D();
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo) {
            base.OnRenderSizeChanged(sizeInfo);
            _sizeChanged = true;
        }

        void InitializeDirect3D() {
            Available = false;
            // assume that we can't run at all under terminal services
            if (GetSystemMetrics(SmRemoteSession) != 0) return;

            try {
                _direct3DEx = new Direct3DEx();
                UseDeviceEx = true;
            } catch {
                _direct3D = new Direct3D();
                UseDeviceEx = false;
            }

            var result = Direct3D.CheckDeviceType(0, DeviceType.Hardware, _adapterFormat, _backbufferFormat, true);
            if (!result)
                return;

            result = Direct3D.CheckDepthStencilMatch(0, DeviceType.Hardware, _adapterFormat, _backbufferFormat, _depthStencilFormat);
            if (!result)
                return;

            var deviceCaps = Direct3D.GetDeviceCaps(0, DeviceType.Hardware);
            if ((deviceCaps.DeviceCaps & DeviceCaps.HWTransformAndLight) != 0)
                _createFlags |= CreateFlags.HardwareVertexProcessing;
            else
                _createFlags |= CreateFlags.SoftwareVertexProcessing;

            Available = true;
        }

        /// <summary>
        /// Initializes the various Direct3D objects we'll be using.
        /// </summary>
        private void Initialize(bool startThread) {
            if (!Available)
                return;

            try {
                _startThread = startThread;

                ReleaseDevice();
                var hwnd = new HwndSource(0, 0, 0, 0, 0, D3DimageControl.PixelWidth, D3DimageControl.PixelHeight, "SlimDXControl", IntPtr.Zero);

                _pp = new PresentParameters {
                    SwapEffect = SwapEffect.Copy,
                    DeviceWindowHandle = hwnd.Handle,
                    Windowed = true,
                    BackBufferWidth = ((int) Width < 0) ? 1 : (int) Width,
                    BackBufferHeight = ((int) Height < 0) ? 1 : (int) Height,
                    BackBufferFormat = _backbufferFormat,
                    AutoDepthStencilFormat = _depthStencilFormat
                };

                if (UseDeviceEx) {
                    // under remote desktop (Windows Server 2008, x86 mode), this will throw
                    _deviceEx = new DeviceEx((Direct3DEx)Direct3D, 0,
                       DeviceType.Hardware,
                       hwnd.Handle,
                       _createFlags,
                       _pp);
                } else {
                    _device = new Device(Direct3D, 0,
                       DeviceType.Hardware,
                       hwnd.Handle,
                       _createFlags,
                       _pp);
                }

                // call the users one
                OnDeviceCreated(EventArgs.Empty);
                OnDeviceReset(EventArgs.Empty);

                // only if startThread is true
                if (_startThread) {
                    CompositionTarget.Rendering += OnRendering;     // BUG: we don't want to render every frame, only when something has changed!
                    D3DimageControl.IsFrontBufferAvailableChanged += OnIsFrontBufferAvailableChanged;
                }
                D3DimageControl.Lock();
                _backBufferSurface = Device.GetBackBuffer(0, 0);
                D3DimageControl.SetBackBuffer(D3DResourceType.IDirect3DSurface9, _backBufferSurface.ComPointer);
                D3DimageControl.Unlock();
            } catch (Direct3D9Exception ex) {
                MessageBox.Show(ex.Message, "Caught Direct3D9Exception in SlimDXControl.Initialize()");     // BUG: remove this
                throw;
            } catch (Exception ex) {
                MessageBox.Show(ex.Message, "Caught Exception in SlimDXControl.Initialize()");     // BUG: remove this
                throw;
            }
        }

        #region Shutdown and Release
        public void Shutdown() {
            ReleaseDevice();
            ReleaseDirect3D();
        }

        private void ReleaseDevice() {
            if (_device != null) {
                if (!_device.Disposed) {
                    _device.Dispose();
                    _device = null;
                    OnDeviceDestroyed(EventArgs.Empty);
                }
            }

            if (_deviceEx != null) {
                if (!_deviceEx.Disposed) {
                    _deviceEx.Dispose();
                    _device = null;
                    OnDeviceDestroyed(EventArgs.Empty);
                }
            }

            D3DimageControl.Lock();
            D3DimageControl.SetBackBuffer(D3DResourceType.IDirect3DSurface9, IntPtr.Zero);
            D3DimageControl.Unlock();

            ReleaseBackBuffer();
        }

        private void ReleaseDirect3D() {
            if (_direct3D != null && !_direct3D.Disposed) {
                _direct3D.Dispose();
                _direct3D = null;
            }

            if (_direct3DEx == null || _direct3DEx.Disposed) return;

            _direct3DEx.Dispose();
            _direct3DEx = null;
        }
        #endregion

        #region Support for controlling on-demand vs. per-frame rendering
        private static object _locker = new object();

        // set this to true when something has changed, to signal that the rendering loop should not be skipped
        private bool _needsRendering;
        public void ForceRendering() {
            lock (_locker) {
                _needsRendering = true;
            }
        }

        public bool ResetForceRendering() {
            bool ret;
            lock (_locker) {
                ret = _needsRendering;
                _needsRendering = false;
            }
            return ret;
        }

        // set this to true to always render every frame, i.e. ignore the NeedsRendering flag
        public bool AlwaysRender { get; set; }
        #endregion

        public void ForceResize(double width, double height) {
            Width = width;
            Height = height;
            _sizeChanged = true;
        }

        private void OnRendering(object sender, EventArgs e) {
            Debug.Assert(D3DimageControl.IsFrontBufferAvailable);
            if (!Available)
                return;

            try {
                if (Device == null) {
                    Initialize(_startThread);
                    ForceRendering();
                }

                if (Device == null) return;

                if (_sizeChanged) {
                    _pp.BackBufferWidth = (int)Width;
                    _pp.BackBufferHeight = (int)Height;
                    ReleaseBackBuffer();
                    OnDeviceLost(EventArgs.Empty);
                    Device.Reset(_pp);               // BUG: under WinXP mode, this throws when doing a window maximize/resize
                    OnDeviceReset(EventArgs.Empty);
                    Debug.WriteLine("resize");
                    _sizeChanged = false;
                    ForceRendering();
                }

                var needsRendering = ResetForceRendering();
                D3DimageControl.Lock();
                if (D3DimageControl.IsFrontBufferAvailable && (needsRendering || AlwaysRender)) {
                    var result = Device.TestCooperativeLevel();
                    if (result.IsFailure) {
                        throw new Direct3D9Exception("Device.TestCooperativeLevel() failed");
                    }

                    Device.Clear(ClearFlags.Target | ClearFlags.ZBuffer, new Color4(System.Drawing.Color.Black), 0, 0);
                    Device.BeginScene();
                    // call the users method
                    OnMainLoop(EventArgs.Empty);

                    Device.EndScene();
                    Device.Present();

                }
                _backBufferSurface = Device.GetBackBuffer(0, 0);
                D3DimageControl.SetBackBuffer(D3DResourceType.IDirect3DSurface9, _backBufferSurface.ComPointer);
                D3DimageControl.AddDirtyRect(new Int32Rect(0, 0, D3DimageControl.PixelWidth, D3DimageControl.PixelHeight));
                D3DimageControl.Unlock();
            } catch (Direct3D9Exception ex) {
                MessageBox.Show(ex.Message, "Caught Direct3D9Exception in SlimDXControl.OnRendering()");     // BUG: remove this
                Initialize(_startThread);
            } catch (Exception ex) {
                MessageBox.Show(ex.Message, "Caught Exception in SlimDXControl.OnRendering()");     // BUG: remove this
                throw;
            }
        }

        #region front & back buffer management
        private void ReleaseBackBuffer() {
            if (_backBufferSurface == null || _backBufferSurface.Disposed) return;

            _backBufferSurface.Dispose();
            _backBufferSurface = null;

            D3DimageControl.Lock();
            D3DimageControl.SetBackBuffer(D3DResourceType.IDirect3DSurface9, IntPtr.Zero);
            D3DimageControl.Unlock();
        }

        void OnIsFrontBufferAvailableChanged(object sender, DependencyPropertyChangedEventArgs e) {
            try {
                if (D3DimageControl.IsFrontBufferAvailable) {
                    Initialize(_startThread);
                } else {
                    // we reach here under remote desktop (Windows Server 2008, x64 mode)
                    CompositionTarget.Rendering -= OnRendering;
                    throw new Direct3D9Exception("_d3dimage.IsFrontBufferAvailable is false");
                }
            } catch (Exception ex) {
                MessageBox.Show(ex.Message, "Caught Exception in SlimDXControl.OnIsFrontBufferAvailableChanged()");     // BUG: remove this
                throw;
            }
        }
        #endregion

        private void ContentControl_SizeChanged(object sender, SizeChangedEventArgs e) {
            ForceResize(e.NewSize.Width, e.NewSize.Height);
        }
    }
}