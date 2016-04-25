using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using AcTools.Render.Base;
using SlimDX.Windows;

namespace AcTools.Render.Wrapper {
    public class SimpleFormWrapper {
        private readonly string _title;

        public readonly BaseRenderer Renderer;
        public readonly RenderForm Form;

        public SimpleFormWrapper(BaseRenderer renderer, string title, int width, int height) {
            _title = title;

            Form = new RenderForm(title) {
                Width = width,
                Height = height,
                StartPosition = FormStartPosition.CenterScreen
            };

            Renderer = renderer;
            Renderer.Initialize(Form.Handle);

            Renderer.Width = Form.ClientSize.Width;
            Renderer.Height = Form.ClientSize.Height;

            Form.UserResized += OnResize;
            Form.KeyUp += OnKeyUp;
        }

        public void Run() {
            MessagePump.Run(Form, OnRender);
        }

        private void OnResize(object sender, EventArgs eventArgs) {
            Renderer.Width = Form.ClientSize.Width;
            Renderer.Height = Form.ClientSize.Height;
        }

        private void OnKeyUp(object sender, KeyEventArgs args) {
            switch (args.KeyCode) {
                /* case Keys.F5:
                    renderer.SwapChain.SetFullscreenState(true, null);
                    break;

                case Keys.F4:
                    renderer.SwapChain.SetFullscreenState(false, null);
                    break;*/

                case Keys.Escape:
                    Form.Close();
                    break;
            }
        }

        private bool _fullscreenEnabled;

        public bool FullscreenEnabled {
            get { return _fullscreenEnabled; }
            set {
                if (Equals(value, _fullscreenEnabled)) return;
                _fullscreenEnabled = value;

                if (_fullscreenEnabled) {
                    Form.FormBorderStyle = FormBorderStyle.None;
                    Form.WindowState = FormWindowState.Maximized;
                } else {
                    Form.FormBorderStyle = FormBorderStyle.Sizable;
                    Form.WindowState = FormWindowState.Normal;
                }
            }
        }

        public void ToggleFullscreen() {
            FullscreenEnabled = !FullscreenEnabled;
        }

        private void OnRender() {
            Form.Text = $"{_title} (FPS: {Renderer.FramesPerSecond:F0})";
            Renderer.Draw();
        }
    }
}
