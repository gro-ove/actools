using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using AcManager.Controls.Helpers;
using AcManager.Tools.Objects;
using AcTools.Kn5File;
using AcTools.Render.Kn5SpecificSpecial;
using AcTools.Render.Wrapper;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using SlimDX;

namespace AcManager.Controls.CustomShowroom {
    public class TrackMapRendererWrapper : BaseKn5FormWrapper {
        private AttachedHelper _helper;

        public new TrackMapPreparationRenderer Renderer => (TrackMapPreparationRenderer)base.Renderer;

        public TrackMapRendererWrapper(TrackObjectBase track, TrackMapPreparationRenderer renderer) : base(renderer, track.Name, 680, 680) {
            _helper = new AttachedHelper(this, new TrackMapRendererTools(track, renderer), 240);
        }

        protected override void OnRender() {
            if (!Renderer.IsDirty) return;
            Renderer.Draw();
            InvokeFirstFrameCallback();
        }

        protected override void CameraMouseRotate(float dx, float dy) {
            var camera = Renderer.CameraOrtho;
            if (camera != null) {
                camera.Move(new Vector3(dx * camera.Width / Renderer.Width, 0f, -dy * camera.Height / Renderer.Height));
                Renderer.AutoResetCamera = false;
                Renderer.IsDirty = true;
            }
        }

        protected override void CameraMousePan(float dx, float dy) {
            var camera = Renderer.CameraOrtho;
            if (camera != null) {
                camera.Move(new Vector3(dx * camera.Width / Renderer.Width, 0f, -dy * camera.Height / Renderer.Height));
                Renderer.AutoResetCamera = false;
                Renderer.IsDirty = true;
            }
        }

        protected override void CameraMouseZoom(float dx, float dy) {
            Renderer.AutoResetCamera = false;
            Renderer.SetZoom(Renderer.Zoom * (1f + dy * 0.001f));
        }

        protected override void OnMouseWheel(object sender, MouseEventArgs e) {
            var value = e.Delta > 0 ? 100f : -100f;
            CameraMouseZoom(0f, value);
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