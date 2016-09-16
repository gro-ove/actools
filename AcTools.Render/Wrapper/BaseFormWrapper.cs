using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using AcTools.Render.Base;
using AcTools.Render.Temporary;
using AcTools.Windows;
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

    public class BaseFormWrapper {
        private readonly string _title;

        public readonly BaseRenderer Renderer;
        public readonly RenderForm Form;

        protected bool Paused { get; private set; }

        public BaseFormWrapper(BaseRenderer renderer, string title, int width, int height) {
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
            Form.KeyDown += OnKeyDown;
            Form.KeyUp += OnKeyUp;

            Form.GotFocus += OnGotFocus;
            Form.LostFocus += OnLostFocus;

            renderer.Tick += OnTick;
        }

        protected virtual void OnTick(object sender, TickEventArgs args) {}

        protected virtual void OnGotFocus(object sender, EventArgs e) {
            Paused = false;
        }

        protected virtual void OnLostFocus(object sender, EventArgs e) {
            Paused = true;
        }

        private Action _firstFrame;

        public void Run(Action firstFrame = null) {
            try {
                _firstFrame = firstFrame;
                MessagePump.Run(Form, OnRender);
            } catch (InvalidOperationException e) {
                Logging.Warning("MessagePump exception: " + e);
            }
        }

        protected virtual void OnResize(object sender, EventArgs eventArgs) {
            Renderer.Width = Form.ClientSize.Width;
            Renderer.Height = Form.ClientSize.Height;
        }

        protected virtual void OnKeyDown(object sender, KeyEventArgs args) {}

        public bool IsPressed(Keys key) {
            return Form.Focused && User32.IsKeyPressed(key);
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
                OnFullscreenChanged();
            }
        }

        protected virtual void OnFullscreenChanged() {
            if (_fullscreenEnabled) {
                Form.FormBorderStyle = FormBorderStyle.None;
                Form.WindowState = FormWindowState.Maximized;
            } else {
                Form.FormBorderStyle = FormBorderStyle.Sizable;
                Form.WindowState = FormWindowState.Normal;
            }
        }

        public void ToggleFullscreen() {
            FullscreenEnabled = !FullscreenEnabled;
        }

        protected virtual void OnRender() {
            Form.Text = $"{_title} (FPS: {Renderer.FramesPerSecond:F0})";
            if (Paused && !Renderer.IsDirty) return;
            Renderer.Draw();

            if (_firstFrame != null) {
                _firstFrame.Invoke();
                _firstFrame = null;
            }
        }

        private Timer _toastTimer;

        public void Toast(string message) {
            if (Form == null) return;

            Action action = delegate {
                if (_toastTimer == null) {
                    _toastTimer = new Timer { Interval = 3000 };
                    _toastTimer.Tick += ToastTimer_Tick;
                }

                Form.Text = Form.Text == _title ? message : Form.Text + ", " + message;
                _toastTimer.Enabled = false;
                _toastTimer.Enabled = true;
            };

            Form.Invoke(action);
        }

        void ToastTimer_Tick(object sender, EventArgs e) {
            if (Form == null) return;
            Form.Text = _title;
            _toastTimer.Enabled = false;
        }

        private bool _closed;

        public void Stop() {
            if (_closed) return;
            _closed = true;
            try {
                Form.Close();
            } catch (Exception) {
                // ignored
            }
        }
    }
}
