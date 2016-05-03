using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using AcManager.Tools.Objects;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Utils;
using SlimDX.Direct3D9;

namespace AcManager.Controls {
    public partial class Kn5CarRenderControl {
        public static readonly DependencyProperty CarProperty = DependencyProperty.Register(nameof(Car), typeof(CarObject),
                typeof(Kn5CarRenderControl));

        public CarObject Car {
            get { return (CarObject)GetValue(CarProperty); }
            set { SetValue(CarProperty, value); }
        }

        public Kn5CarRenderControl() {
            InitializeComponent();

            Width = 400;
            Height = 300;
        }

        //private Render _render;

        private void Run() {
            if (Car == null) return;

            var renderer = new ForwardKn5ObjectRenderer(FileUtils.GetMainCarFilename(Car.Location));

            var hwnd = new HwndSource(0, 0, 0, 0, 0, D3DimageControl.PixelWidth, D3DimageControl.PixelHeight, "Kn5CarRenderControl", IntPtr.Zero);
            // renderer.Initialize(hwnd.Handle);
            renderer.Initialize();
            renderer.Width = (int)Width;
            renderer.Height = (int)Height;

            //_render = new Render(FileUtils.GetMainCarFilename(_car.Location), 0);
            //_render.Control(hwnd.Handle, D3DimageControl.PixelWidth, D3DimageControl.PixelHeight);

            D3DimageControl.Lock();

            renderer.Draw();

            // D3DimageControl.SetBackBuffer(D3DResourceTypeEx., renderer.RenderBuffer.ComPointer);

            var rect = new System.Windows.Int32Rect(0, 0, (int)Width, (int)Height);

            D3DimageControl.AddDirtyRect(rect);

            D3DimageControl.Unlock();

            //CompositionTarget.Rendering += CompositionTarget_Rendering;
            //D3DimageControl.IsFrontBufferAvailableChanged += OnIsFrontBufferAvailableChanged;

            // var _backBufferSurface = renderer.Device.GetBackBuffer(0, 0);
            //D3DimageControl.SetBackBuffer(D3DResourceTypeEx.ID3D10Texture2D, _backBufferSurface.ComPointer);
        }

        void CompositionTarget_Rendering(object sender, EventArgs e) {
            D3DimageControl.Lock();
            //_render.Control_Render();
            D3DimageControl.Unlock();
        }

        private void ContentControl_SizeChanged(object sender, SizeChangedEventArgs e) {
            if (Car != null) {
                Run();
            }
        }
        /// <summary>
        /// Method used to invoke an Action that will catch DllNotFoundExceptions and display a warning dialog.
        /// </summary>
        /// <param name="action">The Action to invoke.</param>
        public static void InvokeWithDllProtection(Action action) {
            InvokeWithDllProtection(
                () => {
                    action.Invoke();
                    return 0;
                });
        }

        static bool errorHasDisplayed;

        /// <summary>
        /// Method used to invoke A Func that will catch DllNotFoundExceptions and display a warning dialog.
        /// </summary>
        /// <param name="func">The Func to invoke.</param>
        /// <returns>The return value of func, or default(T) if a DllNotFoundException was caught.</returns>
        /// <typeparam name="T">The return type of the func.</typeparam>
        public static T InvokeWithDllProtection<T>(Func<T> func) {
            try {
                return func.Invoke();
            } catch (DllNotFoundException e) {
                if (!errorHasDisplayed) {
                    MessageBox.Show("This sample requires:\nManual build of the D3DVisualization project, which requires installation of Windows 10 SDK or DirectX SDK.\n" +
                                    "Installation of the DirectX runtime on non-build machines.\n\n" +
                                    "Detailed exception message: " + e.Message, "WPF D3D11 Interop",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                    errorHasDisplayed = true;

                    if (Application.Current != null) {
                        Application.Current.Shutdown();
                    }
                }
            }

            return default(T);
        }
    }
}