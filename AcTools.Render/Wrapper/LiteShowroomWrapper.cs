using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Utils;
using AcTools.Utils.Helpers;

namespace AcTools.Render.Wrapper {
    public class LiteShowroomWrapper : BaseKn5FormWrapper {
        private readonly ForwardKn5ObjectRenderer _renderer;

        private bool _editMode;

        public bool EditMode {
            get { return _editMode; }
            set {
                if (Equals(value, _editMode)) return;
                _editMode = value;

                if (value) {
                    GoToToolMode();
                } else {
                    GoToNormalMode();
                }
            }
        }

        public LiteShowroomWrapper(ForwardKn5ObjectRenderer renderer, string title = "Lite Showroom", int width = 1600, int height = 900) : base(renderer, title, width, height) {
            _renderer = renderer;
            Form.MouseDoubleClick += OnMouseDoubleClick;
        }

        private void OnMouseDoubleClick(object sender, MouseEventArgs e) {
            _renderer.AutoAdjustTarget = true;
        }

        protected virtual void GoToNormalMode() {
            Form.FormBorderStyle = FormBorderStyle.Sizable;
            Form.Width = 1600;
            Form.Height = 900;
            Form.Left = (Screen.PrimaryScreen.WorkingArea.Width - Form.Width) / 2;
            Form.Top = (Screen.PrimaryScreen.WorkingArea.Height - Form.Height) / 2;
            Form.TopMost = false;
            Kn5ObjectRenderer.VisibleUi = true;

            Renderer.Width = Form.ClientSize.Width;
            Renderer.Height = Form.ClientSize.Height;
        }

        protected virtual void GoToToolMode() {
            Form.FormBorderStyle = FormBorderStyle.SizableToolWindow;
            Form.Width = 400;
            Form.Height = 240;
            Form.Top = Screen.PrimaryScreen.WorkingArea.Height - 300;
            Form.Left = 80;
            Form.TopMost = true;
            Kn5ObjectRenderer.VisibleUi = false;

            Renderer.Width = Form.ClientSize.Width;
            Renderer.Height = Form.ClientSize.Height;
        }

        protected override void OnKeyUp(object sender, KeyEventArgs args) {
            if (args.KeyCode == Keys.F2 && !args.Control && !args.Alt && !args.Shift || args.KeyCode == Keys.Escape && EditMode) {
                EditMode = !EditMode;
                return;
            }

            if (args.KeyCode == Keys.Space && !args.Control && !args.Alt && !args.Shift) {
                return;
            }

            base.OnKeyUp(sender, args);
            if (args.Handled) return;

            switch (args.KeyCode) {
                case Keys.Space:
                    if (!args.Control && args.Alt && !args.Shift) {
                        Kn5ObjectRenderer.AutoRotate = !Kn5ObjectRenderer.AutoRotate;
                    }
                    break;

                case Keys.End:
                    _renderer.AutoAdjustTarget = true;
                    break;

                case Keys.F7:
                    if (args.Alt) {
                        _renderer.UseInterpolationCamera = !_renderer.UseInterpolationCamera;
                    } else {
                        _renderer.UseFpsCamera = !_renderer.UseFpsCamera;
                    }
                    break;

                case Keys.F8:
                    if (!_renderer.UseMsaa) {
                        var multipler = 2;
                        var image = _renderer.Shot(multipler);
                        var filename = Path.Combine(FileUtils.GetDocumentsScreensDirectory(), "__custom_showroom_" + DateTime.Now.ToUnixTimestamp() + ".jpg");
                        image.HighQualityResize(new Size(image.Width / multipler, image.Height / multipler)).Save(filename);
                    }
                    break;

                case Keys.F:
                    _renderer.UseFxaa = !_renderer.UseFxaa;
                    break;

                case Keys.W:
                    _renderer.ShowWireframe = !_renderer.ShowWireframe;
                    break;

                case Keys.B:
                    _renderer.UseBloom = !_renderer.UseBloom;
                    break;
            }
        }
    }
}