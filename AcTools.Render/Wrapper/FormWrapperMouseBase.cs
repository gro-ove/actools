using System;
using System.Drawing;
using System.Windows.Forms;
using AcTools.Render.Base;

namespace AcTools.Render.Wrapper {
    public abstract class FormWrapperMouseBase : FormWrapperBase {
        public bool FormMoving;

        protected FormWrapperMouseBase(BaseRenderer renderer, string title, int width, int height) : base(renderer, title, width, height) {
            Form.MouseMove += OnMouseMove;
            Form.MouseDown += OnMouseDown;
            Form.MouseUp += OnMouseUp;
            Form.MouseWheel += OnMouseWheel;
        }

        protected override void OnLostFocus(object sender, EventArgs e) {
            base.OnLostFocus(sender, e);
            // _moving = false;
        }

        private void OnMouseDown(object sender, MouseEventArgs e) {
            _moved = false;
            _moving = true;
            _down = true;
            _startMousePos = MousePosition;
        }

        private void OnMouseUp(object sender, MouseEventArgs e) {
            if (!_moved) {
                OnClick();
            }
        }

        protected virtual void OnClick() { }

        public Point MousePosition { get; private set; }
        private Point _startMousePos;
        private Point _lastMousePos;

        private bool _moved, _moving, _down;

        protected abstract void OnMouseMove(MouseButtons button, int dx, int dy);

        protected virtual void OnMouseMove(object sender, MouseEventArgs e) {
            if (!Form.Focused) {
                _moving = false;
                return;
            }

            MousePosition = e.Location;

            if (Math.Abs(e.X - _startMousePos.X) > 2 || Math.Abs(e.Y - _startMousePos.Y) > 2) {
                _moved = true;
            }

            if (_moving && !_down) {
                if (e.Button == MouseButtons.Left && FormMoving) {
                    Form.Left += e.X - _lastMousePos.X;
                    Form.Top += e.Y - _lastMousePos.Y;
                    _lastMousePos = e.Location;
                    return;
                }

                OnMouseMove(e.Button, e.X - _lastMousePos.X, e.Y - _lastMousePos.Y);
            }

            _down = false;
            _lastMousePos = e.Location;
        }

        protected abstract void OnMouseWheel(float value);

        protected virtual void OnMouseWheel(object sender, MouseEventArgs e) {
            OnMouseWheel(e.Delta > 0 ? 1f : -1f);
            Renderer.IsDirty = true;
        }
    }
}