using System.ComponentModel;
using AcManager.Tools;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Presentation;
using Newtonsoft.Json;

namespace AcManager.Controls.ViewModels {
    public class RaceGridMode : Displayable, IWithId {
        // Random category
        public static readonly RaceGridMode SameCar = new RaceGridMode("same_car", ToolsStrings.Drive_Grid_SameCar);
        public static readonly RaceGridMode SameGroup = new RaceGridMode("same_group", ToolsStrings.Drive_Grid_SameGroup);
        public static readonly RaceGridMode Filtered = new RaceGridMode("filtered", ToolsStrings.Drive_Grid_FilteredBy, false);
        public static readonly RaceGridMode Manual = new RaceGridMode("manual", ToolsStrings.Drive_Grid_Manual, false);

        // Custom category
        public static readonly RaceGridMode Custom = new RaceGridMode("custom", "Custom", false);

        [JsonProperty(PropertyName = @"id")]
        private readonly string _id;

        public string Id => _id;

        [JsonProperty(PropertyName = @"name")]
        private readonly string _displayName;

        public override string DisplayName => _displayName;

        [JsonProperty(PropertyName = @"filter")]
        private readonly string _filter;

        public string Filter => _filter;

        [JsonProperty(PropertyName = @"script")]
        private readonly string _script;

        public string Script => _script;

        [JsonProperty(PropertyName = @"test")]
        private readonly bool _test;

        public bool Test => _test;

        [JsonProperty(PropertyName = @"affectedByTrack")]
        private readonly bool _affectedByTrack;

        public bool AffectedByTrack => _affectedByTrack;

        [DefaultValue(true), JsonProperty(PropertyName = @"affectedByCar", DefaultValueHandling = DefaultValueHandling.Populate)]
        private readonly bool _affectedByCar;

        public bool AffectedByCar => _affectedByCar;

        public override string ToString() => Id;

        [JsonConstructor]
        // ReSharper disable once UnusedMember.Local
        private RaceGridMode() { }

        private RaceGridMode([Localizable(false)] string id, string displayName, bool affectedByCar = true) {
            _id = id;
            _displayName = displayName;
            _filter = "";
            _script = "";
            _test = false;
            _affectedByTrack = false;
            _affectedByCar = affectedByCar;
        }
    }
}