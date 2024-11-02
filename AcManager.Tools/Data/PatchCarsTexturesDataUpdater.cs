using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Tools.Data {
    public class PatchCarTexturesDataEntry : PatchDataEntry {
        [JsonProperty("name")]
        public string Name { get; private set; }

        [CanBeNull, JsonProperty("usedBy")]
        public string[] UsedBy { get; private set; }

        private string _displayUsedBy;

        public string DisplayUsedBy {
            get {
                if (_displayUsedBy == null) {
                    var s = UsedBy?.Select(x => CarsManager.Instance.GetById(x)?.DisplayName).NonNull().JoinToReadableString();
                    _displayUsedBy = string.IsNullOrWhiteSpace(s) ? "none" : s;
                }
                return _displayUsedBy;
            }
        }

        protected override Task<Tuple<string, bool>> GetStateAsync() {
            return Task.FromResult(Tuple.Create(Name ?? AcStringValues.NameFromId(Id), true));
        }

        protected override bool IsToUnzip => false;
        protected override string DestinationExtension => ".zip";
    }

    public class PatchCarsTexturesDataUpdater : PatchBaseDataUpdater<PatchCarTexturesDataEntry> {
        public static PatchCarsTexturesDataUpdater Instance { get; } = new PatchCarsTexturesDataUpdater();

        protected override Task Prepare() {
            return CarsManager.Instance.EnsureLoadedAsync();
        }

        public override string GetBaseUrl() {
            return "/cars-textures";
        }

        public override string GetCacheDirectoryName() {
            return "Cars Textures";
        }

        protected override string GetSourceUrl() {
            return @"tree/master/textures/cars";
        }

        protected override string GetTitle() {
            return "Cars textures";
        }

        protected override string GetDescription() {
            return
                    "Some car configs refer to extra textures packs. Those won’t take a lot of time, and could improve experince. For example, tyres would change textures depending on what is selected in pits.";
        }

        public override string GetDestinationDirectory() {
            return Path.Combine(PatchHelper.RequireRootDirectory(), "textures", "cars");
        }
    }
}