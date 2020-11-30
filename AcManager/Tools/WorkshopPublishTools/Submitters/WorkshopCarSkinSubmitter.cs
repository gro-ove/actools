using System;
using System.IO;
using System.Threading.Tasks;
using AcManager.CustomShowroom;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Presets;
using AcManager.Tools.Objects;
using AcTools.Render.Kn5SpecificForwardDark;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Serialization;
using Newtonsoft.Json.Linq;
using SharpCompress.Writers;

namespace AcManager.Tools.WorkshopPublishTools.Submitters {
    public class WorkshopCarSkinSubmittable : WorkshopBaseSubmittable<CarSkinObject> {
        public static DarkPreviewsOptions PreviewsOptions() {
            return CmPreviewsSettings.GetSerializedSavedOptions(PresetsManager.Instance.GetBuiltInPresetData(@"Custom Previews", @"Kunos"));
        }

        public string PreviewImageFilename;
        public string IconUrl;
        public string PreviewUrl;

        private async Task UpdatePreviewAsync() {
            PreviewImageFilename = Path.Combine(TemporaryLocation, "preview.jpg");
            using (var op = Log?.BeginParallel("Generating previews with default look", @"Updating skin")) {
                await CmPreviewsTools.UpdatePreviewAsync(new[] {
                    new ToUpdatePreview(await CarsManager.Instance.GetByIdAsync(Target.CarId) ?? throw new Exception(), Target)
                }, PreviewsOptions(), @"Kunos", destinationOverrideCallback: skin => PreviewImageFilename, progress: op);
            }
        }

        private async Task<string> PackMainAsync() {
            var packedFilename = Path.Combine(TemporaryLocation, "skin.zip");
            using (var op = Params.Log?.BeginParallel($"Packing skin {Target.Id}", @"Packing:")) {
                await Target.TryToPack(new CarSkinObject.CarSkinPackerParams {
                    Destination = packedFilename,
                    ShowInExplorer = false,
                    Override = key => {
                        if (Path.GetFileName(key) == "preview.jpg") {
                            return (w, k) => w.Write(k, PreviewImageFilename);
                        }
                        return null;
                    },
                    Progress = op
                });
            }
            return packedFilename;
        }

        protected override async Task PrepareOverrideAsync() {
            if (PreviewImageFilename == null) {
                await UpdatePreviewAsync();
            }

            await TaskExtension.MakeList(
                    async () => {
                        IconUrl = await UploadFileAsync("skin icon", "icon.png", Target.LiveryImage);
                        PreviewUrl = await UploadFileAsync("skin preview", "preview.jpg", PreviewImageFilename);
                    },
                    async () => await PrepareMainPackageAsync(await PackMainAsync()))
                    .WhenAll();
        }
    }

    public class WorkshopCarSkinSubmitter : WorkshopBaseSubmitter<CarSkinObject, WorkshopCarSkinSubmittable> {
        public WorkshopCarSkinSubmitter(CarSkinObject obj, bool isChildObject, WorkshopSubmitterParams submitterParams)
                : base(obj, isChildObject, submitterParams) { }

        public override JObject BuildPayload() {
            var number = Target.SkinNumber.As(0);
            var ret = base.BuildPayload();
            ret.Merge(new JObject {
                ["skinIcon"] = Data.IconUrl,
                ["previewImage"] = Data.PreviewUrl,
                ["number"] = number > 0 ? (int?)number : null,
                ["driver"] = Target.DriverName.Or(null),
                ["team"] = Target.Team.Or(null),
                ["country"] = Target.Country.Or(null),
            });
            return ret;
        }
    }
}