using System;
using System.IO;
using System.Threading.Tasks;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Tools.Data {
    [JsonObject(MemberSerialization.OptIn)]
    public class PatchCarVaoDataEntry : PatchDataEntry {
        [CanBeNull]
        public CarObject Car { get; private set; }

        protected override Task<Tuple<string, bool>> GetStateAsync() {
            Car = CarsManager.Instance.GetById(Id);
            return Task.FromResult(Car != null ? Tuple.Create(Car.DisplayName, true) : null);
        }

        protected override bool IsToUnzip => false;
        protected override string DestinationExtension => ".vao-patch";
    }

    public class PatchCarsVaoDataUpdater : PatchBaseDataUpdater<PatchCarVaoDataEntry> {
        public static PatchCarsVaoDataUpdater Instance { get; } = new PatchCarsVaoDataUpdater();

        protected override Task Prepare() {
            return CarsManager.Instance.EnsureLoadedAsync();
        }

        public override string GetBaseUrl() {
            return "/patch/cars-vao/";
        }

        public override string GetCacheDirectoryName() {
            return "Cars VAO";
        }

        protected override string GetSourceUrl() {
            return "https://github.com/ac-custom-shaders-patch/acc-extension-cars-vao/tree/master";
        }

        protected override string GetTitle() {
            return "Vertex AO patches for cars";
        }

        protected override string GetDescription() {
            return "With per-vertex ambient occlusion data, cars get deeper looking exteriors interiors.";
        }

        public override string GetDestinationDirectory() {
            return Path.Combine(PatchHelper.GetRootDirectory(), "vao-patches-cars");
        }
    }
}