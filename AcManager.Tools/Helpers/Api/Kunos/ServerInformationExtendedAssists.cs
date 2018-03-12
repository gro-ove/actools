using System.ComponentModel;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Presentation;
using Newtonsoft.Json;

namespace AcManager.Tools.Helpers.Api.Kunos {
    [Localizable(false)]
    public class ServerInformationExtendedAssists : NotifyPropertyChanged {
        [JsonProperty(PropertyName = "absState")]
        public ServerPresetAssistState AbsState { get; set; } = ServerPresetAssistState.Factory;

        [JsonProperty(PropertyName = "tcState")]
        public ServerPresetAssistState TractionControlState { get; set; } = ServerPresetAssistState.Factory;

        [JsonProperty(PropertyName = "fuelRate")]
        public int FuelRate { get; set; } = 100;

        [JsonProperty(PropertyName = "damageMultiplier")]
        public int DamageMultiplier { get; set; } = 100;

        [JsonProperty(PropertyName = "tyreWearRate")]
        public int TyreWearRate { get; set; } = 100;

        [JsonProperty(PropertyName = "allowedTyresOut")]
        public int AllowedTyresOut { get; set; } = 2;

        [JsonProperty(PropertyName = "stabilityAllowed")]
        public bool StabilityAllowed { get; set; }

        [JsonProperty(PropertyName = "autoclutchAllowed")]
        public bool AutoclutchAllowed { get; set; }

        [JsonProperty(PropertyName = "tyreBlanketsAllowed")]
        public bool TyreBlankets { get; set; }

        [JsonProperty(PropertyName = "forceVirtualMirror")]
        public bool ForceVirtualMirror { get; set; }
    }
}