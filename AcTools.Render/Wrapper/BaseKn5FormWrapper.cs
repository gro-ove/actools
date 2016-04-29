using System;
using System.Drawing;
using System.Windows.Forms;
using AcTools.Render.Base;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific;
using AcTools.Render.Kn5SpecificDeferred;

namespace AcTools.Render.Wrapper {
    public class BaseKn5FormWrapper : SimpleFormWrapper {
        public readonly IKn5ObjectRenderer Kn5ObjectRenderer;

        public BaseKn5FormWrapper(BaseRenderer renderer, string title, int width, int height) : base(renderer, title, width, height) {
            Kn5ObjectRenderer = (IKn5ObjectRenderer)renderer;

            var lastMousePos = Point.Empty;
            Form.MouseMove += (o, e) => {
                var size = 180.0f / Math.Min(Form.Height, Form.Width);
                var dx = MathF.ToRadians(size * (e.X - lastMousePos.X));
                var dy = MathF.ToRadians(size * (e.Y - lastMousePos.Y));
                switch (e.Button) {
                    case MouseButtons.Left:
                        Kn5ObjectRenderer.CameraOrbit.Pitch(dy);
                        Kn5ObjectRenderer.CameraOrbit.Yaw(-dx);
                        Kn5ObjectRenderer.AutoRotate = false;
                        break;

                    case MouseButtons.Middle:
                        Kn5ObjectRenderer.CameraOrbit.Target += dy * Kn5ObjectRenderer.CameraOrbit.Up - dx * Kn5ObjectRenderer.CameraOrbit.Right;
                        break;

                    case MouseButtons.Right:
                        Kn5ObjectRenderer.CameraOrbit.Zoom(dy * 3.0f);
                        break;
                }

                lastMousePos = e.Location;
            };

            Form.MouseWheel += (o, e) => {
                Kn5ObjectRenderer.CameraOrbit.Zoom(e.Delta > 0 ? -0.4f : 0.4f);
            };
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
                case Keys.Tab:
                    Renderer.SyncInterval = !Renderer.SyncInterval;
                    break;

                case Keys.Space:
                    if (!args.Control && !args.Alt && !args.Shift) {
                        Kn5ObjectRenderer.AutoRotate = !Kn5ObjectRenderer.AutoRotate;
                    }
                    break;
            }
        }
    }
}