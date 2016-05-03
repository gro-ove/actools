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

        public bool FormMoving;

        public BaseKn5FormWrapper(BaseRenderer renderer, string title, int width, int height) : base(renderer, title, width, height) {
            Kn5ObjectRenderer = (IKn5ObjectRenderer)renderer;
            Form.MouseMove += OnMouseMove;
            Form.MouseWheel += OnMouseWheel;
        }

        private Point _lastMousePos;

        protected virtual void OnMouseMove(object sender, MouseEventArgs e) {
            if (e.Button == MouseButtons.Middle || e.Button == MouseButtons.Left && IsPressed(Keys.Space)) {
                var size = 180.0f / Math.Min(Form.Height, Form.Width);
                var dx = MathF.ToRadians(size * (e.X - _lastMousePos.X));
                var dy = MathF.ToRadians(size * (e.Y - _lastMousePos.Y));

                var c = Kn5ObjectRenderer.CameraOrbit;
                if (c != null) {
                    c.Target += dy * Vector3.Cross(c.Look, c.Right) - dx * c.Right;
                    Kn5ObjectRenderer.AutoRotate = false;
                    Kn5ObjectRenderer.AutoAdjustTarget = false;
                }
            } else if (e.Button == MouseButtons.Left) {
                if (FormMoving) {
                    Form.Left += e.X - _lastMousePos.X;
                    Form.Top += e.Y - _lastMousePos.Y;
                    _lastMousePos = new Point(e.X - e.X + _lastMousePos.X, e.Y - e.Y + _lastMousePos.Y);
                    return;
                }

                var size = 180.0f / Math.Min(Form.Height, Form.Width);
                var dx = MathF.ToRadians(size * (e.X - _lastMousePos.X));
                var dy = MathF.ToRadians(size * (e.Y - _lastMousePos.Y));

                Kn5ObjectRenderer.Camera.Pitch(dy);
                Kn5ObjectRenderer.Camera.Yaw(-dx);
                Kn5ObjectRenderer.AutoRotate = false;
            } else if (e.Button == MouseButtons.Right) {
                var size = 180.0f / Math.Min(Form.Height, Form.Width);
                var dy = MathF.ToRadians(size * (e.Y - _lastMousePos.Y));
                Kn5ObjectRenderer.Camera.Zoom(dy * 3.0f);
                Kn5ObjectRenderer.AutoRotate = false;
            }

            _lastMousePos = e.Location;
        }

        protected virtual void OnMouseWheel(object sender, MouseEventArgs e) {
            Kn5ObjectRenderer.Camera.Zoom(e.Delta > 0 ? -0.4f : 0.4f);
            Kn5ObjectRenderer.AutoRotate = false;
            Kn5ObjectRenderer.AutoAdjustTarget = false;
        }

        protected override void OnTick(object sender, TickEventArgs args) {
            base.OnTick(sender, args);

            if (IsPressed(Keys.RControlKey)) return;

            if (IsPressed(Keys.Up)) {
                Kn5ObjectRenderer.Camera.Walk(0.1f);
                Kn5ObjectRenderer.AutoRotate = false;
                Kn5ObjectRenderer.AutoAdjustTarget = false;
            }

            if (IsPressed(Keys.Down)) {
                Kn5ObjectRenderer.Camera.Walk(-0.1f);
                Kn5ObjectRenderer.AutoRotate = false;
                Kn5ObjectRenderer.AutoAdjustTarget = false;
            }

            if (IsPressed(Keys.Left)) {
                Kn5ObjectRenderer.Camera.Strafe(-0.1f);
                Kn5ObjectRenderer.AutoRotate = false;
                Kn5ObjectRenderer.AutoAdjustTarget = false;
            }

            if (IsPressed(Keys.Right)) {
                Kn5ObjectRenderer.Camera.Strafe(0.1f);
                Kn5ObjectRenderer.AutoRotate = false;
                Kn5ObjectRenderer.AutoAdjustTarget = false;
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