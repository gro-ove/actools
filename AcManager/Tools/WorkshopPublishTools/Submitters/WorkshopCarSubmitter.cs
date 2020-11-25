using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AcManager.CustomShowroom;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Kn5File;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using Newtonsoft.Json.Linq;
using SharpCompress.Writers;

namespace AcManager.Tools.WorkshopPublishTools.Submitters {
    public class WorkshopCarSubmittable : WorkshopBaseSubmittable<CarObject> {
        public List<CarSkinObject> Skins;

        public Dictionary<string, string> CarPreviews;
        public Dictionary<string, WorkshopCarSkinSubmitter> SkinSubmitters;
        public string CarDownloadUrl;
        public string ShowroomUrl;
        public string UpgradeIconUrl;

        public override void EnsureInitialized() {
            Skins = Target.EnabledOnlySkins.ApartFrom(Params.DisabledObjects).ToList();
        }

        private async Task PreparePreviewsAsync() {
            CarPreviews = Skins.ToDictionary(x => x.Id, x => Path.Combine(TemporaryLocation, $"preview_{x.Id}.jpg"));

            await CmPreviewsTools.UpdatePreviewAsync(new[] {
                new ToUpdatePreview(Target, Skins.Where(x => !File.Exists(CarPreviews[x.Id])).ToList())
            }, WorkshopCarSkinSubmittable.PreviewsOptions(), @"Kunos",
                    destinationOverrideCallback: skin => CarPreviews[skin.Id],
                    progress: Log?.Progress(@"Updating skin"));
        }

        private class CarShowroomPacker : AcCommonObject.AcCommonObjectPacker<CarObject> {
            private readonly List<CarSkinObject> _skins;

            public CarShowroomPacker(IEnumerable<CarSkinObject> skins) {
                _skins = skins.ToList();
            }

            protected override string GetBasePath(CarObject t) {
                return "";
            }

            protected override IEnumerable PackOverride(CarObject t) {
                yield return Add("data/ambient_shadows.ini", "data/cameras.ini", "data/dash_cam.ini", "data/driver3d.ini",
                        "data/extra_animations.ini", "data/lights.ini", "data/mirrors.ini");
                yield return Add("animations/*.ksanim");
                yield return Add("body_shadow.png", "tyre_*_shadow.png");

                var mainModel = AcPaths.GetMainCarFilename(t.Location, false);
                if (mainModel == null) {
                    throw new Exception("Failed to find main car model");
                }
                yield return AddFile("model.kn5", mainModel);

                var textureNames = Kn5.FromFile(AcPaths.GetMainCarFilename(t.Location, t.AcdData, false) ?? throw new Exception(),
                        SkippingTextureLoader.Instance, SkippingMaterialLoader.Instance, SkippingNodeLoader.Instance).TexturesData.Keys.ToList();
                foreach (var skin in _skins) {
                    foreach (var name in textureNames) {
                        yield return Add($"skins/{skin.Id}/{name}");
                    }
                }
            }

            protected override PackedDescription GetDescriptionOverride(CarObject t) {
                return null;
            }
        }

        private async Task<string> PackShowroomAsync() {
            var packed = Path.Combine(TemporaryLocation, "packed.zip");
            using (var op = Log?.BeginParallel("Packing car", @"Packing:")) {
                await Target.TryToPack(new CarObject.CarPackerParams {
                    Destination = packed,
                    ShowInExplorer = false,
                    Override = key => {
                        if (Path.GetFileName(key) == "preview.jpg") {
                            var skinId = Path.GetFileName(Path.GetDirectoryName(key));
                            var filename = Path.Combine(packed, $"preview_{Target.Id}_{skinId}.jpg");
                            if (File.Exists(filename)) {
                                return (w, k) => w.Write(k, filename);
                            }
                        }
                        return null;
                    },
                    Progress = op
                }, new CarShowroomPacker(Skins.Take(2)));
            }
            return packed;
        }

        private async Task<string> PackMainAsync() {
            var packed = Path.Combine(TemporaryLocation, "packed.zip");
            using (var op = Log?.BeginParallel("Packing car", @"Packing:")) {
                await Target.TryToPack(new CarObject.CarPackerParams {
                    Destination = packed,
                    ShowInExplorer = false,
                    Override = key => {
                        if (Path.GetFileName(key) == "preview.jpg") {
                            var skinId = Path.GetFileName(Path.GetDirectoryName(key));
                            var filename = Path.Combine(packed, $"preview_{Target.Id}_{skinId}.jpg");
                            if (File.Exists(filename)) {
                                return (w, k) => w.Write(k, filename);
                            }
                        }
                        return null;
                    },
                    Progress = op
                });
            }
            return packed;
        }

        private async Task UploadSkinsAsync() {
            using (Log?.Begin("Uploading skins")) {
                SkinSubmitters = new Dictionary<string, WorkshopCarSkinSubmitter>();
                await Skins.Select(async skin => {
                    SkinSubmitters[skin.Id] = new WorkshopCarSkinSubmitter(skin, true, Params) {
                        Data = { PreviewImageFilename = CarPreviews[skin.Id] }
                    };
                    await SkinSubmitters[skin.Id].PrepareAsync();
                }).WhenAll(10);
            }
        }

        public IEnumerable<JObject> BuildSkinsPayloads() {
            if (SkinSubmitters != null) {
                return SkinSubmitters.Values.Select(x => x.BuildPayload());
            }
            return Skins.Select(x => new WorkshopCarSkinSubmitter(x, true, Params).BuildPayload());
        }

        protected override async Task PrepareOverrideAsync() {
            await PreparePreviewsAsync();

            CarDownloadUrl = await UploadFileAsync("main package", $"{Target.Id}.zip", await PackMainAsync());
            try {
                ShowroomUrl = await UploadFileAsync("showroom package", $"{Target.Id}.zip", await PackShowroomAsync());
            } catch (Exception e) {
                Logging.Warning(e);
            }
            UpgradeIconUrl = File.Exists(Target.UpgradeIcon)
                    ? await UploadFileAsync("upgrade icon", "upgrade.png", Target.UpgradeIcon) : null;
            ShowroomUrl = await UploadFileAsync("showroom package", "upgrade.png", Target.UpgradeIcon);

            await UploadSkinsAsync();
        }
    }

    public class WorkshopCarSubmitter : WorkshopBaseSubmitter<CarObject, WorkshopCarSubmittable> {
        public WorkshopCarSubmitter(CarObject obj, bool isChildObject, WorkshopSubmitterParams submitterParams) : base(obj, isChildObject, submitterParams) { }

        public override JObject BuildPayload() {
            var drivetrainIni = Target.AcdData?.GetIniFile("drivetrain.ini");
            var electronicsIni = Target.AcdData?.GetIniFile("electronics.ini");
            var tyresIni = Target.AcdData?.GetIniFile("tyres.ini");

            var ret = base.BuildPayload();
            ret.Merge(new JObject {
                ["carBrand"] = Target.Brand,
                ["carClass"] = Target.CarClass,
                ["country"] = Target.Country,
                ["year"] = Target.Year,
                ["parentID"] = Target.ParentId,
                ["specs"] = new JObject {
                    ["gears"] = drivetrainIni?["GEARS"].GetIntNullable("COUNT"),
                    ["tyresVersion"] = tyresIni?["HEADER"].GetIntNullable("VERSION"),
                    ["audioSourceID"] = Target.SoundDonorId == "tatuusfa1" ? null : Target.SoundDonorId,
                    ["features"] = JArray.FromObject(new[] {
                        electronicsIni?["ABS"].GetBoolNullable("PRESENT") == true ? "abs" : null,
                        electronicsIni?["TRACTION_CONTROL"].GetBoolNullable("PRESENT") == true ? "tc" : null,
                        electronicsIni?["EDL"].GetBoolNullable("PRESENT") == true ? "edl" : null,
                    }.NonNull()),
                    ["ui"] = new JObject {
                        ["weight"] = Target.SpecsWeight,
                        ["bhp"] = Target.SpecsBhp,
                        ["torque"] = Target.SpecsTorque,
                        ["topspeed"] = Target.SpecsTopSpeed,
                        ["acceleration"] = Target.SpecsAcceleration,
                        ["pwratio"] = Target.SpecsPwRatio
                    },
                    ["curves"] = new JObject {
                        ["power"] = Target.SpecsPowerCurve?.ToJArray(),
                        ["torque"] = Target.SpecsTorqueCurve?.ToJArray(),
                    },
                },
                ["skins"] = JArray.FromObject(Data.BuildSkinsPayloads()),
                ["upgradeIcon"] = Data.UpgradeIconUrl
            });
            return ret;
        }
    }
}