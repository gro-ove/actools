using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcTools;
using AcTools.Render.Kn5SpecificSpecial;
using AcTools.Render.Wrapper;
using AcTools.Utils;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.CustomShowroom {
    public class TrackOutlineRendererWrapper : FormWrapperMouseBase {
        private AttachedHelper _helper;

        public new TrackOutlineRenderer Renderer => (TrackOutlineRenderer)base.Renderer;

        public TrackOutlineRendererWrapper(TrackObjectBase track, TrackOutlineRenderer renderer)
                : base(renderer, track.Name, CommonAcConsts.TrackOutlineWidth, CommonAcConsts.TrackOutlineHeight) {
            _helper = new AttachedHelper(this, new TrackOutlineRendererTools(track, renderer), 244, -80, false);
            Form.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            Form.MaximizeBox = false;
        }

        protected override void UpdateSize() {
            Renderer.Height = CommonAcConsts.TrackOutlineHeight;
            Renderer.Width = CommonAcConsts.TrackOutlineWidth;

            if (Form.ClientSize.Width != CommonAcConsts.TrackOutlineWidth || Form.ClientSize.Height != CommonAcConsts.TrackOutlineHeight) {
                Form.ClientSize = new Size(CommonAcConsts.TrackOutlineWidth, CommonAcConsts.TrackOutlineHeight);
            }
        }

        protected override void OnMouseMove(MouseButtons button, int dx, int dy) {
            if (button == MouseButtons.Left) {
                Renderer.OffsetX -= (float)(dx * Renderer.ResolutionMultiplier);
                Renderer.OffsetY -= (float)(dy * Renderer.ResolutionMultiplier);
            }
        }

        protected override void OnMouseWheel(float value) {
            Renderer.Scale *= 1f + value / 10f;
        }

        public static Task Run(TrackObjectBase track) {
            try {
                return RunInner(track);
            } catch (Exception e) {
                VisualCppTool.OnException(e, "Can’t update outline");
                return Task.Delay(0);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static async Task RunInner(TrackObjectBase track) {
            if (!File.Exists(track.MapImage)) {
                ModernDialog.ShowMessage("Map not found");
                return;
            }

            await PrepareAsync();

            try {
                var maps = track.MainTrackObject.MultiLayouts?.Select(x => x.MapImage).ToArray() ?? new[] { track.MapImage };
                using (var renderer = new TrackOutlineRenderer(maps, track.MapImage, track.PreviewImage)) {
                    var wrapper = new TrackOutlineRendererWrapper(track, renderer);
                    wrapper.Form.Icon = AppIconService.GetAppIcon();
                    wrapper.Run();
                }
            } catch (Exception e) {
                NonfatalError.Notify("Can’t update outline", e);
            }
        }

        public static Task UpdateAsync(TrackObjectBase track) {
            try {
                return UpdateAsyncInner(track);
            } catch (Exception e) {
                VisualCppTool.OnException(e, "Can’t update outline");
                return Task.Delay(0);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static async Task UpdateAsyncInner(TrackObjectBase track) {
            if (!File.Exists(track.MapImage)) {
                ModernDialog.ShowMessage("Map not found");
                return;
            }

            try {
                using (WaitingDialog.Create("Saving…")) {
                    var maps = track.MainTrackObject.MultiLayouts?.Select(x => x.MapImage).ToArray() ?? new[] { track.MapImage };
                    await Task.Run(() => {
                        using (var renderer = new TrackOutlineRenderer(maps, track.MapImage, track.PreviewImage) {
                            LoadPreview = false
                        }) {
                            renderer.Initialize();
                            renderer.Width = CommonAcConsts.TrackOutlineWidth;
                            renderer.Height = CommonAcConsts.TrackOutlineHeight;

                            TrackOutlineRendererTools.LoadSettings(track, renderer);
                            using (var holder = FileUtils.RecycleOriginal(track.OutlineImage)) {
                                renderer.Shot(holder.Filename);
                            }
                        }
                    });
                }
            } catch (Exception e) {
                NonfatalError.Notify("Can’t update outline", e);
            }
        }
    }
}