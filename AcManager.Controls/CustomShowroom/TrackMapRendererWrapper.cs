using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using AcManager.Controls.Helpers;
using AcManager.Tools.Objects;
using AcTools.Kn5File;
using AcTools.Render.Kn5Specific;
using AcTools.Render.Kn5SpecificSpecial;
using AcTools.Render.Wrapper;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Windows.Controls;
using SlimDX;

namespace AcManager.Controls.CustomShowroom {
    public class TrackMapRendererWrapper : BaseKn5FormWrapper {
        private AttachedHelper _helper;

        protected class TrackMapCameraControlHelper : Kn5WrapperCameraControlHelper {
            public override void CameraMouseRotate(IKn5ObjectRenderer renderer, double dx, double dy, double height, double width) {
                var preparationRenderer = (TrackMapPreparationRenderer)renderer;
                var camera = preparationRenderer.CameraOrtho;
                if (camera != null) {
                    camera.Move(new Vector3((float)(dx * camera.Width / preparationRenderer.Width), 0f, (float)(-dy * camera.Height / preparationRenderer.Height)));
                    preparationRenderer.AutoResetCamera = false;
                    preparationRenderer.IsDirty = true;
                }
            }

            public override void CameraMousePan(IKn5ObjectRenderer renderer, double dx, double dy, double height, double width) {
                var preparationRenderer = (TrackMapPreparationRenderer)renderer;
                var camera = preparationRenderer.CameraOrtho;
                if (camera != null) {
                    camera.Move(new Vector3((float)(dx * camera.Width / preparationRenderer.Width), 0f, (float)(-dy * camera.Height / preparationRenderer.Height)));
                    preparationRenderer.AutoResetCamera = false;
                    preparationRenderer.IsDirty = true;
                }
            }

            public override void CameraMouseZoom(IKn5ObjectRenderer renderer, double dx, double dy, double height, double width) {
                var preparationRenderer = (TrackMapPreparationRenderer)renderer;
                preparationRenderer.AutoResetCamera = false;
                preparationRenderer.SetZoom((float)(preparationRenderer.Zoom * (1f + dy * 0.001f)));
            }
        }

        protected override Kn5WrapperCameraControlHelper GetHelper() {
            return new TrackMapCameraControlHelper();
        }

        public new TrackMapPreparationRenderer Renderer => (TrackMapPreparationRenderer)base.Renderer;

        public TrackMapRendererWrapper(TrackObjectBase track, TrackMapPreparationRenderer renderer) : base(renderer, track.Name, 680, 680) {
            _helper = new AttachedHelper(this, new TrackMapRendererTools(track, renderer), 240);
        }

        protected override void OnRender() {
            if (!Renderer.IsDirty) return;
            Renderer.Draw();
            InvokeFirstFrameCallback();
        }

        protected override void OnMouseWheel(object sender, MouseEventArgs e) {
            var value = e.Delta > 0 ? 100f : -100f;
            Helper.CameraMouseZoom(Kn5ObjectRenderer, 0f, value, Form.ClientSize.Height, Form.ClientSize.Width);
        }

        public static async Task Run(TrackObjectBase track) {
            var modelsFilename = track.ModelsFilename;
            string kn5Filename = null;
            if (!File.Exists(modelsFilename)) {
                modelsFilename = null;
                kn5Filename = Path.Combine(track.Location, track.Id + ".kn5");
                if (!File.Exists(kn5Filename)) {
                    ModernDialog.ShowMessage("Model not found");
                    return;
                }
            }

            Kn5 kn5;
            using (var waiting = new WaitingDialog()) {
                waiting.Report("Loading model…");
                kn5 = await Task.Run(() => modelsFilename != null ? Kn5.FromModelsIniFile(modelsFilename) : Kn5.FromFile(kn5Filename));
            }

            using (var renderer = new TrackMapPreparationRenderer(kn5)) {
                var wrapper = new TrackMapRendererWrapper(track, renderer);
                wrapper.Form.Icon = AppIconService.GetAppIcon();
                wrapper.Run();
            }
        }
    }
}