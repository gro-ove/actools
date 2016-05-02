using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using AcTools.Render.Base;
using SlimDX.Windows;

namespace AcTools.Render.Wrapper {
    public static class ImageExtension {
        public static Image HighQualityResize(this Image img, Size size) {
            var percent = Math.Min(size.Width / (float)img.Width, size.Height / (float)img.Height);
            var width = (int)(img.Width * percent);
            var height = (int)(img.Height * percent);

            var b = new Bitmap(width, height);
            using (var g = Graphics.FromImage(b)) {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(img, 0, 0, width, height);
            }

            return b;
        }
    }

    public class SimpleFormWrapper {
        private readonly string _title;

        public readonly BaseRenderer Renderer;
        public readonly RenderForm Form;

        private bool _paused;

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

            Form.GotFocus += OnGotFocus;
            Form.LostFocus += OnLostFocus;
        }

        private void OnGotFocus(object sender, EventArgs e) {
            _paused = false;
        }

        private void OnLostFocus(object sender, EventArgs e) {
            _paused = true;
        }

        public void Run() {
            MessagePump.Run(Form, OnRender);
        }

        protected virtual void OnResize(object sender, EventArgs eventArgs) {
            Renderer.Width = Form.ClientSize.Width;
            Renderer.Height = Form.ClientSize.Height;
        }

        protected virtual void OnKeyUp(object sender, KeyEventArgs args) {
            switch (args.KeyCode) { 
                case Keys.Escape:
                    args.Handled = true;
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
            if (_paused && !Renderer.IsDirty) return;
            Form.Text = $"{_title} (FPS: {Renderer.FramesPerSecond:F0})";
            Renderer.Draw();
        }
    }
}
