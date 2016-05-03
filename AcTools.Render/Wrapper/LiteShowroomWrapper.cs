using System.Windows.Forms;
using AcTools.Render.Kn5SpecificForward;

namespace AcTools.Render.Wrapper {
    public class LiteShowroomWrapper : BaseKn5FormWrapper {
        private readonly ForwardKn5ObjectRenderer _renderer;

        public bool EditMode;

        public LiteShowroomWrapper(ForwardKn5ObjectRenderer renderer) : base(renderer, "Lite Showroom", 1600, 900) {
            _renderer = renderer;
        }

        public void ToogleEditMode() {
            if (EditMode) {
                Form.FormBorderStyle = FormBorderStyle.Sizable;
                Form.Width = 1600;
                Form.Height = 900;
                Form.Left = (Screen.PrimaryScreen.WorkingArea.Width - Form.Width) / 2;
                Form.Top = (Screen.PrimaryScreen.WorkingArea.Height - Form.Height) / 2;
                Form.TopMost = false;
                Kn5ObjectRenderer.VisibleUi = true;
            } else {
                Form.FormBorderStyle = FormBorderStyle.None;
                Form.Width = 400;
                Form.Height = 240;
                Form.Top = Screen.PrimaryScreen.WorkingArea.Height - 300;
                Form.Left = 80;
                Form.TopMost = true;
                Kn5ObjectRenderer.VisibleUi = false;
            }

            Renderer.Width = Form.ClientSize.Width;
            Renderer.Height = Form.ClientSize.Height;

            EditMode = !EditMode;
        }

        protected override void OnKeyUp(object sender, KeyEventArgs args) {
            if (args.KeyCode == Keys.F2 || args.KeyCode == Keys.Escape && EditMode) {
                ToogleEditMode();
                return;
            }

            base.OnKeyUp(sender, args);
            if (args.Handled) return;

            switch (args.KeyCode) {
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