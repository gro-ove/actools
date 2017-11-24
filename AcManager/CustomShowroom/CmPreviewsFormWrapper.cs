using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AcManager.Tools.Objects;
using AcTools;
using AcTools.Kn5File;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Kn5SpecificForwardDark;
using AcTools.Utils;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.CustomShowroom {
    public class CmPreviewsFormWrapper : LiteShowroomFormWrapperWithUiShots {
        private AttachedHelper _helper;
        private readonly CmPreviewsTools _tools;

        public new DarkKn5ObjectRenderer Renderer => (DarkKn5ObjectRenderer)base.Renderer;

        private CmPreviewsFormWrapper(CarObject car, DarkKn5ObjectRenderer renderer, string skinId, string presetFilename)
                : base(renderer, "Previews Params", CommonAcConsts.PreviewWidth, CommonAcConsts.PreviewHeight) {
            _tools = new CmPreviewsTools(renderer, car, skinId, presetFilename);
            _helper = new AttachedHelper(this, _tools);
        }

        public void SetToUpdate([NotNull] IReadOnlyList<ToUpdatePreview> list) {
            _tools.Model.ToUpdate = list;
        }

        public IReadOnlyList<UpdatePreviewError> GetErrors() {
            return _tools.Model.GetErrors();
        }

        protected override bool SleepMode => base.SleepMode && !_helper.IsActive;

        [ItemCanBeNull]
        private static async Task<IReadOnlyList<UpdatePreviewError>> Run([NotNull] CarObject car, [CanBeNull] string skinId,
                [CanBeNull] IReadOnlyList<ToUpdatePreview> toUpdate, [CanBeNull] string presetFilename) {
            var carKn5 = AcPaths.GetMainCarFilename(car.Location, car.AcdData);
            if (!File.Exists(carKn5)) {
                ModernDialog.ShowMessage("Model not found");
                return null;
            }

            await PrepareAsync();

            Kn5 kn5;
            using (var waiting = new WaitingDialog()) {
                waiting.Report("Loading model…");
                kn5 = await Task.Run(() => Kn5.FromFile(carKn5));
            }

            using (var renderer = new DarkKn5ObjectRenderer(CarDescription.FromKn5(kn5, car.Location, car.AcdData)) {
                AutoRotate = false,
                AutoAdjustTarget = false,
                AsyncTexturesLoading = true,
                AsyncOverridesLoading = true
            }) {
                var wrapper = new CmPreviewsFormWrapper(car, renderer, skinId, presetFilename);

                if (toUpdate != null) {
                    wrapper.SetToUpdate(toUpdate);
                }

                wrapper.Form.Icon = AppIconService.GetAppIcon();
                wrapper.Run();

                return wrapper.GetErrors();
            }
        }

        public static Task<IReadOnlyList<UpdatePreviewError>> Run([NotNull] IReadOnlyList<ToUpdatePreview> toUpdate, [CanBeNull] string presetFilename) {
            if (toUpdate.Count == 0) return Task.FromResult<IReadOnlyList<UpdatePreviewError>>(new UpdatePreviewError[0]);
            return Run(toUpdate[0].Car, toUpdate[0].Skins?.FirstOrDefault()?.Id, toUpdate, presetFilename);
        }
    }
}