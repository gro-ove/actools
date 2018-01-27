using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AcTools.Render.Base;
using JetBrains.Annotations;
using SlimDX.Windows;

namespace AcTools.Render.Wrapper {
    public class FormWrapperBase {
        private readonly string _title;

        public readonly BaseRenderer Renderer;
        public readonly RenderForm Form;

        protected bool Paused { get; private set; }

        private static FormWrapperBase _current;

        public static bool StopCurrent() {
            if (_current != null && !_current.Renderer.Disposed && !_current._closed) {
                _current.Stop();
                return true;
            }

            return false;
        }

        public static Task PrepareAsync() {
            /* after stopping previous MessagePump (it’s the only thing keeping us from
             * using several wrappers at once), wait a little to make sure it actually
             * finished */
            return Task.Delay(StopCurrent() ? 200 : 0);
        }

        protected FormWrapperBase(BaseRenderer renderer, string title, int width, int height) {
            if (StopCurrent()) {
                throw new Exception("Can’t have two renderers running at the same time");
            }

            _current = this;
            _title = title;

            Form = new RenderForm(title) {
                Text = _title,
                ClientSize = new Size(width, height),
                StartPosition = FormStartPosition.CenterScreen
            };

            Renderer = renderer;
            Renderer.Initialize(Form.Handle);
            Renderer.SetAssociatedWindow(Form);

            UpdateSize();

            Form.UserResized += OnResize;
            Form.KeyDown += OnKeyDown;
            Form.KeyUp += OnKeyUp;

            Form.GotFocus += OnGotFocus;
            Form.LostFocus += OnLostFocus;

            renderer.Tick += OnTick;
        }

        private bool _updatingSize;

        protected virtual async void UpdateSize() {
            if (_updatingSize) return;
            _updatingSize = true;

            try {
                await Task.Yield();
                Renderer.Width = Form.ClientSize.Width;
                Renderer.Height = Form.ClientSize.Height;
            } finally {
                _updatingSize = false;
            }
        }

        protected virtual void OnTick(object sender, TickEventArgs args) {}

        protected virtual void OnGotFocus(object sender, EventArgs e) {
            Paused = false;
        }

        protected virtual void OnLostFocus(object sender, EventArgs e) {
            Paused = true;
        }

        [CanBeNull]
        private Action _firstFrame;

        public void Run(Action firstFrame = null) {
            try {
                _firstFrame = firstFrame;
                MessagePump.Run(Form, OnRender);
            } catch (InvalidOperationException e) {
                AcToolsLogging.Write(e);
            }
        }

        protected virtual void OnResize(object sender, EventArgs eventArgs) {
            UpdateSize();
        }

        protected virtual void OnKeyDown(object sender, KeyEventArgs args) {}

        protected virtual void OnKeyUp(object sender, KeyEventArgs args) {
            switch (args.KeyCode) {
                case Keys.Escape:
                    args.Handled = true;
                    Form.Close();
                    break;
            }
        }

        public void OnKeyUp(KeyEventArgs args) {
            OnKeyUp(Form, args);
        }

        private bool _fullscreenEnabled;

        public bool FullscreenEnabled {
            get => _fullscreenEnabled;
            set {
                if (Equals(value, _fullscreenEnabled)) return;
                _fullscreenEnabled = value;
                OnFullscreenChanged();
            }
        }

        public event EventHandler FullscreenChanged;

        protected virtual void OnFullscreenChanged() {
            FullscreenChanged?.Invoke(this, EventArgs.Empty);
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

        protected void InvokeFirstFrameCallback() {
            if (_firstFrame != null) {
                _firstFrame.Invoke();
                _firstFrame = null;
            }
        }

        protected virtual bool SleepMode => !Renderer.IsDirty && !Renderer.AccumulationMode;

        private void Draw() {
            Renderer.Draw();
        }

        private void OnRender() {
            if (_closed) return;

            try {
                Form.Text = $@"{_title} (FPS: {Renderer.FramesPerSecond:F0})";

                if (SleepMode) {
                    Thread.Sleep(20);
                    return;
                }

                Draw();
                InvokeFirstFrameCallback();
                Thread.Sleep(1);
            } catch (Exception e) {
                AcToolsLogging.NonFatalErrorNotify("Custom Showroom unhandled exception", null, e);
                Stop();
            }
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
