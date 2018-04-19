using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcTools.AiFile;
using AcTools.Kn5File;
using AcTools.Render.Kn5Specific;
using AcTools.Render.Kn5SpecificSpecial;
using AcTools.Render.Wrapper;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using SlimDX;

namespace AcManager.CustomShowroom {
    public class TrackMapRendererWrapper : BaseKn5FormWrapper {
        private AttachedHelper _helper;

        private class TrackMapCameraControlHelper : Kn5WrapperCameraControlHelper {
            public override void CameraMouseRotate(IKn5ObjectRenderer renderer, double dx, double dy, double height, double width) {
                var preparationRenderer = (TrackMapPreparationRenderer)renderer;
                var camera = preparationRenderer.CameraOrtho;
                if (camera != null) {
                    camera.Move(new Vector3((float)(-dx * camera.Width / preparationRenderer.Width), 0f,
                            (float)(-dy * camera.Height / preparationRenderer.Height)));
                    preparationRenderer.AutoResetCamera = false;
                    preparationRenderer.IsDirty = true;
                }
            }

            public override void CameraMousePan(IKn5ObjectRenderer renderer, double dx, double dy, double height, double width) {
                var preparationRenderer = (TrackMapPreparationRenderer)renderer;
                var camera = preparationRenderer.CameraOrtho;
                if (camera != null) {
                    camera.Move(new Vector3((float)(dx * camera.Width / preparationRenderer.Width), 0f,
                            (float)(-dy * camera.Height / preparationRenderer.Height)));
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

        public TrackMapRendererWrapper(TrackObjectBase track, TrackMapPreparationRenderer renderer) : base(renderer, track.Name, 680, 680) {
            _helper = new AttachedHelper(this, new TrackMapRendererTools(track, renderer), 240);
        }

        protected override void OnMouseWheel(object sender, MouseEventArgs e) {
            var value = e.Delta > 0 ? 100f : -100f;
            Helper.CameraMouseZoom(Kn5ObjectRenderer, 0f, value, Form.ClientSize.Height, Form.ClientSize.Width);
        }

        public static Task Run(TrackObjectBase track, bool aiLane) {
            try {
                return RunInner(track, aiLane);
            } catch (Exception e) {
                VisualCppTool.OnException(e, "Can’t update map");
                return Task.Delay(0);
            }
        }

        private static async Task RunInner(TrackObjectBase track, bool aiLane) {
            string modelsFilename = null, kn5Filename = null, aiLaneFilename = null;

            if (!aiLane) {
                modelsFilename = track.ModelsFilename;
                if (!File.Exists(modelsFilename)) {
                    modelsFilename = null;
                    kn5Filename = Path.Combine(track.Location, track.Id + ".kn5");
                    if (!File.Exists(kn5Filename)) {
                        ModernDialog.ShowMessage("Model not found");
                        return;
                    }
                }
            } else {
                aiLaneFilename = track.AiLaneFastFilename;
                if (!File.Exists(aiLaneFilename)) {
                    ModernDialog.ShowMessage("AI lane not found");
                    return;
                }
            }

            await PrepareAsync();

            TrackMapPreparationRenderer renderer = null;
            try {
                using (WaitingDialog.Create("Loading model…")) {
                    renderer = aiLaneFilename == null ?
                            modelsFilename == null ?
                                    new TrackMapPreparationRenderer(await Task.Run(() => Kn5.FromFile(kn5Filename))) :
                                    new TrackMapPreparationRenderer(await Task.Run(() => TrackComplexModelDescription.CreateLoaded(modelsFilename))) :
                            new TrackMapPreparationRenderer(await Task.Run(() => AiSpline.FromFile(aiLaneFilename)));
                }

                var wrapper = new TrackMapRendererWrapper(track, renderer);
                wrapper.Form.Icon = AppIconService.GetAppIcon();
                wrapper.Run();
            } catch (Exception e) {
                NonfatalError.Notify("Can’t update map", e);
            } finally {
                renderer?.Dispose();
            }
        }
    }
}