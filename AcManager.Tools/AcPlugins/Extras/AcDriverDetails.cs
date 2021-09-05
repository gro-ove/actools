using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Tools.AcPlugins.Extras {
    [JsonObject(MemberSerialization.OptIn)]
    public class AcDriverDetails : NotifyPropertyChanged {
        [JsonConstructor]
        private AcDriverDetails() { }

        public AcDriverDetails(string guid, string name, string carId, string skinId) {
            Guid = guid;
            DriverName = name;
            CarId = carId;

            var i = skinId.IndexOf('/');
            CarSkinId = i != -1 ? skinId.Substring(0, i) : skinId;
        }

        [JsonProperty("car")]
        public string CarId { get; private set; }

        [JsonProperty("skin")]
        public string CarSkinId { get; private set; }

        [JsonProperty("guid")]
        public string Guid { get; private set; }

        [JsonProperty("name")]
        public string DriverName { get; private set; }

        [CanBeNull]
        public string CarName => Car?.DisplayName ?? CarId;

        [CanBeNull]
        public CarObject Car => CarsManager.Instance.GetById(CarId);

        [CanBeNull]
        public CarSkinObject CarSkin => Car?.GetSkinById(CarSkinId);
    }
}