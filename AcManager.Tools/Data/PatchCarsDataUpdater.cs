using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Tools.Data {
    [JsonObject(MemberSerialization.OptIn)]
    public class PatchCarDataEntry : PatchDataEntry {
        [CanBeNull, JsonProperty("features")]
        public string[] Features { get; private set; }

        [CanBeNull, JsonProperty("textures")]
        public string[] Textures { get; private set; }

        public string DisplayFeatures => Features.JoinToReadableString();

        public string DisplayTextures => Textures?.Select(x => PatchCarsTexturesDataUpdater.Instance.List.GetById(x).DisplayName)
                                                  .JoinToReadableString() ?? @"?";

        public bool HasTyresTextures => Features?.Contains(@"TyresTextures") ?? false;
        public bool HasTurnSignals => Features?.Contains(@"TurnSignals") ?? false;
        public bool HasExtraIndicators => Features?.Contains(@"ExtraIndicators") ?? false;
        public bool HasOdometer => Features?.Contains(@"Odometer") ?? false;
        public bool HasDeformingMesh => Features?.Contains(@"DeformingMesh") ?? false;
        public bool HasAdjustableWings => Features?.Contains(@"AdjustableWings") ?? false;

        [CanBeNull]
        public CarObject Car { get; private set; }

        protected override Task<Tuple<string, bool>> GetStateAsync() {
            Car = CarsManager.Instance.GetById(Id);
            return Task.FromResult(Car != null ? Tuple.Create(Car.DisplayName, true) : null);
        }

        protected override async Task InstallOrThrowAsync(bool force, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            var textures = Textures;
            if (textures?.Length > 0) {
                var length = textures.Length;
                var singlePiece = 1d / (1d + length);
                var split = progress?.Split(singlePiece);
                await base.InstallOrThrowAsync(force, split?.Item1, cancellation);
                await PatchCarsTexturesDataUpdater.Instance.EnsureLoadedAsync();
                for (var i = 0; i < textures.Length; i++) {
                    var texture = textures[i];
                    Logging.Debug("Installing texture pack: " + texture);
                    var name = PatchCarsTexturesDataUpdater.Instance.List.GetByIdOrDefault(texture)?.DisplayName ?? texture;
                    await PatchCarsTexturesDataUpdater.Instance.TriggerAutoLoadAsync(texture,
                            progress?.Subrange(singlePiece * (1 + i), singlePiece).StringConvert(s => $"Texture pack “{name}”: {s.ToSentenceMember()}"),
                            cancellation);
                }
            } else {
                await base.InstallOrThrowAsync(force, progress, cancellation);
            }
        }

        protected override bool IsToUnzip => true;

        protected override string DestinationExtension => ".ini";
    }

    public class PatchCarsDataUpdater : PatchBaseDataUpdater<PatchCarDataEntry> {
        public static PatchCarsDataUpdater Instance { get; } = new PatchCarsDataUpdater();

        protected override async Task Prepare() {
            await PatchCarsTexturesDataUpdater.Instance.EnsureLoadedAsync();
            await CarsManager.Instance.EnsureLoadedAsync();
        }

        public override string GetBaseUrl() {
            return "/patch/cars-configs/";
        }

        public override string GetCacheDirectoryName() {
            return "Cars Configs";
        }

        protected override string GetSourceUrl() {
            return @"tree/master/config/cars";
        }

        protected override string GetTitle() {
            return "Cars configs";
        }

        protected override string GetDescription() {
            return
                    "With all those cars, patch tries to guess various parameters, such as how to set up light sources, but carefully prepared config is much better. Plus, a lot of options patch doesn’t even try to guess to avoid messing things up.";
        }

        public override string GetDestinationDirectory() {
            return Path.Combine(PatchHelper.GetRootDirectory(), "config", "cars", "loaded");
        }
    }
}