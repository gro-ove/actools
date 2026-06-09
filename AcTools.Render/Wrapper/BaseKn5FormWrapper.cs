using System.Windows.Forms;
using AcTools.Render.Base;
using AcTools.Render.Kn5Specific;
using AcTools.Windows;

namespace AcTools.Render.Wrapper {
    public class BaseKn5FormWrapper : FormWrapperMouseBase {
        public readonly IKn5ObjectRenderer Kn5ObjectRenderer;

        public bool AutoAdjustTargetOnReset = true;
        public bool InvertMouseButtons = false;

        private Kn5WrapperCameraControlHelper _helper;

        protected Kn5WrapperCameraControlHelper Helper => _helper ?? (_helper = GetHelper());

        protected virtual Kn5WrapperCameraControlHelper GetHelper() {
            return new Kn5WrapperCameraControlHelper();
        }

        public BaseKn5FormWrapper(BaseRenderer renderer, string title, int width, int height) : base(renderer, title, width, height) {
            Kn5ObjectRenderer = (IKn5ObjectRenderer)renderer;
        }

        protected override void OnClick() { }

        protected override void OnMouseMove(MouseButtons button, int dx, int dy) {
            if (button == (InvertMouseButtons ? MouseButtons.Left : MouseButtons.Middle) ||
                    button == MouseButtons.Left && User32.IsAsyncKeyPressed(Keys.Space)) {
                Helper.CameraMousePan(Kn5ObjectRenderer, dx, dy, Form.ClientSize.Width, Form.ClientSize.Height);
            } else if (button == (InvertMouseButtons ? MouseButtons.Right : MouseButtons.Left)) {
                Helper.CameraMouseRotate(Kn5ObjectRenderer, dx, dy, Form.ClientSize.Width, Form.ClientSize.Height);
            } else if (button == (InvertMouseButtons ? MouseButtons.Middle : MouseButtons.Right)
                    && (!Kn5ObjectRenderer.UseFpsCamera || User32.IsAsyncKeyPressed(Keys.LControlKey) || User32.IsAsyncKeyPressed(Keys.RControlKey))) {
                Helper.CameraMouseZoom(Kn5ObjectRenderer, dx, dy, Form.ClientSize.Width, Form.ClientSize.Height);
            }
        }

        protected override void OnMouseMove(object sender, MouseEventArgs e) {
            if (Kn5ObjectRenderer.LockCamera) return;
            base.OnMouseMove(sender, e);
        }

        protected override void OnMouseWheel(float value) {
            var useFpsCamera = Kn5ObjectRenderer.UseFpsCamera;
            var ctrlPressed = User32.IsAsyncKeyPressed(Keys.LControlKey) || User32.IsAsyncKeyPressed(Keys.RControlKey);
            if (!(useFpsCamera ^ ctrlPressed)) {
                Kn5ObjectRenderer.Camera.Zoom(value * (useFpsCamera ? -0.1f : -0.4f));
            } else if (!useFpsCamera) {
                var c = Kn5ObjectRenderer.CameraOrbit;
                if (c == null) return;
                Kn5ObjectRenderer.ChangeCameraFov(c.FovY - value * 0.05f);
            }
        }

        protected override void OnMouseWheel(object sender, MouseEventArgs e) {
            if (Kn5ObjectRenderer.LockCamera) return;
            base.OnMouseWheel(sender, e);
        }

        protected override void OnTick(object sender, TickEventArgs args) {
            base.OnTick(sender, args);
            if (Form.Focused) {
                Helper.OnTick(Kn5ObjectRenderer, args.DeltaTime);
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
                    if (!args.Control && !args.Alt && !args.Shift && !Kn5ObjectRenderer.LockCamera) {
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
                    if (!args.Shift) {
                        if (!args.Control) {
                            Kn5ObjectRenderer.CarLightsEnabled = !Kn5ObjectRenderer.CarLightsEnabled;
                            if (!args.Alt) {
                                Kn5ObjectRenderer.CarBrakeLightsEnabled = Kn5ObjectRenderer.CarLightsEnabled;
                            }
                        } else if (!args.Alt) {
                            Kn5ObjectRenderer.CarBrakeLightsEnabled = !Kn5ObjectRenderer.CarBrakeLightsEnabled;
                        }

                        Renderer.IsDirty = true;
                    }
                    break;

                case Keys.Space:
                    if (!args.Control && !args.Alt && !args.Shift && !Kn5ObjectRenderer.LockCamera) {
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