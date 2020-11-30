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
        public string ShowroomUrl;
        public string UpgradeIconUrl;

        public override void EnsureInitialized() {
            Skins = Target.EnabledOnlySkins.ApartFrom(Params.DisabledObjects).ToList();
        }

        private async Task PreparePreviewsAsync() {
            CarPreviews = Skins.ToDictionary(x => x.Id, x => Path.Combine(TemporaryLocation, $"preview_{x.Id}.jpg"));
            using (var op = Log?.BeginParallel("Generating previews with default look", @"Updating skin")) {
                await CmPreviewsTools.UpdatePreviewAsync(new[] {
                    new ToUpdatePreview(Target, Skins.Where(x => !File.Exists(CarPreviews[x.Id])).ToList())
                }, WorkshopCarSkinSubmittable.PreviewsOptions(), @"Kunos", destinationOverrideCallback: skin => CarPreviews[skin.Id], progress: op);
            }
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
            var packed = Path.Combine(TemporaryLocation, "showroom.zip");
            using (var op = Log?.BeginParallel("Packing showroom package", @"Packing:")) {
                await Target.TryToPack(new CarObject.CarPackerParams {
                    Destination = packed,
                    ShowInExplorer = false,
                    Progress = op
                }, new CarShowroomPacker(Skins.Take(2)));
            }
            return packed;
        }

        private async Task<string> PackMainAsync() {
            var packed = Path.Combine(TemporaryLocation, "packed.zip");
            using (var op = Log?.BeginParallel("Packing main package", @"Packing:")) {
                await Target.TryToPack(new CarObject.CarPackerParams {
                    Destination = packed,
                    ShowInExplorer = false,
                    Override = key => {
                        if (Path.GetFileName(key) == "preview.jpg") {
                            var skinId = Path.GetFileName(Path.GetDirectoryName(key)) ?? string.Empty;
                            return (w, k) => w.Write(k, CarPreviews[skinId]);
                        }
                        return null;
                    },
                    Progress = op
                });
            }
            return packed;
        }

        private async Task UploadSkinsAsync() {
            SkinSubmitters = new Dictionary<string, WorkshopCarSkinSubmitter>();
            await Skins.Select(async skin => {
                SkinSubmitters[skin.Id] = new WorkshopCarSkinSubmitter(skin, true, Params) {
                    Data = { PreviewImageFilename = CarPreviews[skin.Id] }
                };
                await SkinSubmitters[skin.Id].PrepareAsync();
            }).WhenAll(8);
        }

        public IEnumerable<JObject> BuildSkinsPayloads() {
            if (SkinSubmitters != null) {
                return SkinSubmitters.Values.Select(x => x.BuildPayload());
            }
            return Skins.Select(x => new WorkshopCarSkinSubmitter(x, true, Params).BuildPayload());
        }

        private async Task UploadStuffWithPreviewsTask() {
            await PreparePreviewsAsync();
            await TaskExtension.MakeList(async () => await PrepareMainPackageAsync(await PackMainAsync()), UploadSkinsAsync).WhenAll();
        }

        private async Task UploadStuffWithoutPreviewsTask() {
            await TaskExtension.MakeList(
                    async () => {
                        try {
                            var showroomPackage = await PackShowroomAsync();
                            ShowroomUrl = await UploadFileAsync("showroom package", "showroom.zip", showroomPackage);
                        } catch (Exception e) {
                            Logging.Warning(e);
                        }
                    },
                    async () => {
                        UpgradeIconUrl = File.Exists(Target.UpgradeIcon)
                                ? await UploadFileAsync("upgrade icon", "upgrade.png", Target.UpgradeIcon)
                                : null;
                    })
                    .WhenAll();
        }

        protected override async Task PrepareOverrideAsync() {
            await TaskExtension.MakeList(UploadStuffWithPreviewsTask, UploadStuffWithoutPreviewsTask).WhenAll();
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
                ["upgradeIcon"] = Data.UpgradeIconUrl,
                ["showroomURL"] = Data.ShowroomUrl,
            });
            return ret;
        }
    }
}