using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using AcManager.Tools.Objects;
using AcTools.Kn5Render.Kn5Render;
using AcTools.Utils;

namespace AcManager.Controls {
    public partial class Kn5CarRenderControl {
        private CarObject _car;

        public CarObject Car {
            get { return _car; }
            set {
                if (value == _car) return;
                _car = value;
                // Run();
            }
        }

        public Kn5CarRenderControl() {
            InitializeComponent();

            Width = 400;
            Height = 300;
        }

        private Render _render;

        private void Run() {

           // try {
                var hwnd = new HwndSource(0, 0, 0, 0, 0, D3DimageControl.PixelWidth, D3DimageControl.PixelHeight, "Kn5CarRenderControl", IntPtr.Zero);
                _render = new Render(FileUtils.GetMainCarFilename(_car.Location), 0);
                //_render.Control(hwnd.Handle, D3DimageControl.PixelWidth, D3DimageControl.PixelHeight);

                CompositionTarget.Rendering += CompositionTarget_Rendering;
                //D3DimageControl.IsFrontBufferAvailableChanged += OnIsFrontBufferAvailableChanged;

                //_backBufferSurface = _render.CurrentDevice.GetBackBuffer(0, 0);
                //D3DimageControl.SetBackBuffer(D3DResourceType.IDirect3DSurface9, _backBufferSurface.ComPointer);
         /*   } catch (Direct3D9Exception ex) {
                MessageBox.Show(ex.Message, "Caught Direct3D9Exception in SlimDXControl.Initialize()");     // BUG: remove this
                throw;
            } catch (Exception ex) {
                MessageBox.Show(ex.Message, "Caught Exception in SlimDXControl.Initialize()");     // BUG: remove this
                throw;
            }*/
        }

        void CompositionTarget_Rendering(object sender, EventArgs e) {
            D3DimageControl.Lock();
            //_render.Control_Render();
            D3DimageControl.Unlock();
        }

        private void ContentControl_SizeChanged(object sender, SizeChangedEventArgs e) {
            if (_car != null) {
                Run();
            }
        }
    }
}