using System;
using System.Drawing;
using System.Windows.Forms;
using AcTools.Render.Base;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific;
using SlimDX;

namespace AcTools.Render.Wrapper {
    public class BaseKn5FormWrapper : BaseFormWrapper {
        public readonly IKn5ObjectRenderer Kn5ObjectRenderer;

        public bool AutoAdjustTargetOnReset = true;
        public bool InvertMouseButtons = false;

        public bool FormMoving;

        public BaseKn5FormWrapper(BaseRenderer renderer, string title, int width, int height) : base(renderer, title, width, height) {
            Kn5ObjectRenderer = (IKn5ObjectRenderer)renderer;
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

        protected virtual void OnClick() {}

        public Point MousePosition { get; private set; }
        private Point _startMousePos;
        private Point _lastMousePos;

        private bool _moved, _moving, _down;

        protected virtual void CameraMousePan(float dx, float dy) {
            var size = 4.0f / Math.Min(Form.Height, Form.Width);
            dx *= size;
            dy *= size;

            var c = Kn5ObjectRenderer.CameraOrbit;
            if (c != null) {
                c.Target += dy * Vector3.Cross(c.Look, c.Right) - dx * c.Right;
                Kn5ObjectRenderer.AutoRotate = false;
                Kn5ObjectRenderer.AutoAdjustTarget = false;
                Renderer.IsDirty = true;
            } else {
                var f = Kn5ObjectRenderer.FpsCamera;
                if (f != null) {
                    f.Position += dy * Vector3.Cross(f.Look, f.Right) - dx * f.Right;
                    Renderer.IsDirty = true;
                }
            }
        }

        protected virtual void CameraMouseRotate(float dx, float dy) {
            var size = 180.0f / Math.Min(Form.Height, Form.Width);
            dx *= size;
            dy *= size;

            Kn5ObjectRenderer.Camera.Pitch(MathF.ToRadians(dy));
            Kn5ObjectRenderer.Camera.Yaw(MathF.ToRadians(Kn5ObjectRenderer.UseFpsCamera ? dx : -dx));
            Kn5ObjectRenderer.AutoRotate = false;
            Renderer.IsDirty = true;
        }

        protected virtual void CameraMouseZoom(float dx, float dy) {
            var size = 9.0f / Math.Min(Form.Height, Form.Width);
            dy *= size;

            Kn5ObjectRenderer.Camera.Zoom(dy);
            Kn5ObjectRenderer.AutoRotate = false;
            Renderer.IsDirty = true;
        }

        protected void OnMouseMove(object sender, MouseEventArgs e) {
            if (!Form.Focused) {
                _moving = false;
                return;
            }

            MousePosition = e.Location;

            if (Math.Abs(e.X - _startMousePos.X) > 2 || Math.Abs(e.Y - _startMousePos.Y) > 2) {
                _moved = true;
            }

            if (_moving && !_down) {
                var dx = e.X - _lastMousePos.X;
                var dy = e.Y - _lastMousePos.Y;

                if (e.Button == (InvertMouseButtons ? MouseButtons.Left : MouseButtons.Middle) || e.Button == MouseButtons.Left && IsPressed(Keys.Space)) {
                    CameraMousePan(dx, dy);
                } else if (e.Button == (InvertMouseButtons ? MouseButtons.Right : MouseButtons.Left)) {
                    if (FormMoving) {
                        Form.Left += e.X - _lastMousePos.X;
                        Form.Top += e.Y - _lastMousePos.Y;
                        _lastMousePos = e.Location;
                        return;
                    }

                    CameraMouseRotate(dx, dy);
                } else if (e.Button == (InvertMouseButtons ? MouseButtons.Middle : MouseButtons.Right)) {
                    CameraMouseZoom(dx, dy);
                }
            }

            _down = false;
            _lastMousePos = e.Location;
        }

        protected virtual void OnMouseWheel(object sender, MouseEventArgs e) {
            var value = e.Delta > 0 ? 1f : -1f;

            if (Kn5ObjectRenderer.UseFpsCamera || !IsPressed(Keys.LControlKey) && !IsPressed(Keys.RControlKey)) {
                Kn5ObjectRenderer.Camera.Zoom(value * (Kn5ObjectRenderer.UseFpsCamera ? -0.1f : -0.4f));
            } else {
                var c = Kn5ObjectRenderer.CameraOrbit;
                if (c == null) return;
                c.FovY = MathF.Clamp(c.FovY - value * 0.1f, MathF.PI * 0.05f, MathF.PI * 0.8f);
                c.SetLens(c.Aspect);
                c.Zoom(value * 0.4f);
            }

            Renderer.IsDirty = true;
        }

        protected override void OnTick(object sender, TickEventArgs args) {
            base.OnTick(sender, args);

            if (IsPressed(Keys.LMenu) || IsPressed(Keys.RMenu)) return;

            var speed = 0.1f;
            if (IsPressed(Keys.LShiftKey) || IsPressed(Keys.RShiftKey)) speed *= 0.2f;
            if (IsPressed(Keys.LControlKey) || IsPressed(Keys.RControlKey)) speed = 5.0f;

            if (IsPressed(Keys.Up)) {
                Kn5ObjectRenderer.Camera.Walk(speed);
                Kn5ObjectRenderer.AutoRotate = false;
                Kn5ObjectRenderer.AutoAdjustTarget = false;
                Renderer.IsDirty = true;
            }

            if (IsPressed(Keys.Down)) {
                Kn5ObjectRenderer.Camera.Walk(-speed);
                Kn5ObjectRenderer.AutoRotate = false;
                Kn5ObjectRenderer.AutoAdjustTarget = false;
                Renderer.IsDirty = true;
            }

            if (IsPressed(Keys.Left)) {
                Kn5ObjectRenderer.Camera.Strafe(-speed);
                Kn5ObjectRenderer.AutoRotate = false;
                Kn5ObjectRenderer.AutoAdjustTarget = false;
                Renderer.IsDirty = true;
            }

            if (IsPressed(Keys.Right)) {
                Kn5ObjectRenderer.Camera.Strafe(speed);
                Kn5ObjectRenderer.AutoRotate = false;
                Kn5ObjectRenderer.AutoAdjustTarget = false;
                Renderer.IsDirty = true;
            }
        }

        protected override void OnKeyUp(object sender, KeyEventArgs args) {
            base.OnKeyUp(sender, args);
            if (args.Handled) return;

            if (args.Alt && args.KeyCode == Keys.Enter || args.KeyCode == Keys.F11) {
                args.Handled = true;
                ToggleFullscreen();
                return;
            }

            switch (args.KeyCode) {
                case Keys.Home:
                    if (!args.Control && !args.Alt && !args.Shift) {
                        Kn5ObjectRenderer.ResetCamera();
                        if (AutoAdjustTargetOnReset) {
                            Kn5ObjectRenderer.AutoAdjustTarget = true;
                        }
                    }
                    break;

                case Keys.Tab:
                    if (!args.Control && !args.Alt && !args.Shift) {
                        Renderer.SyncInterval = !Renderer.SyncInterval;
                    }
                    break;

                case Keys.L:
                    if (!args.Control && !args.Alt && !args.Shift) {
                        Kn5ObjectRenderer.CarLightsEnabled = !Kn5ObjectRenderer.CarLightsEnabled;
                        Renderer.IsDirty = true;
                    }
                    break;

                case Keys.Space:
                    if (!args.Control && !args.Alt && !args.Shift) {
                        Kn5ObjectRenderer.AutoRotate = !Kn5ObjectRenderer.AutoRotate;
                    }
                    break;

                case Keys.H:
                    if (args.Control && !args.Alt && !args.Shift) {
                        Kn5ObjectRenderer.VisibleUi = !Kn5ObjectRenderer.VisibleUi;
                        Renderer.IsDirty = true;
                    }
                    break;

                case Keys.PageUp:
                    if (!args.Control && !args.Alt && !args.Shift) {
                        Kn5ObjectRenderer.SelectPreviousSkin();
                    }
                    break;

                case Keys.PageDown:
                    if (!args.Control && !args.Alt && !args.Shift) {
                        Kn5ObjectRenderer.SelectNextSkin();
                    }
                    break;
            }
        }
    }
}