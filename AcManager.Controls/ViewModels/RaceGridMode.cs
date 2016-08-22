using System.ComponentModel;
using AcManager.Tools;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Controls.ViewModels {
    public interface IRaceGridMode : INotifyPropertyChanged, IWithId {
        string DisplayName { get; }

        bool CandidatesMode { get; }

        bool Filterable { get; }

        bool AffectedByCar { get; }

        bool AffectedByTrack { get; }
    }

    public sealed class BuiltInGridMode : Displayable, IRaceGridMode {
        // Same car
        public static readonly IRaceGridMode SameCar = new BuiltInGridMode("same_car", ToolsStrings.Drive_Grid_SameCar, filterable: false, affectedByCar: true);

        // Random category
        public static readonly IRaceGridMode CandidatesSameGroup = new BuiltInGridMode("same_group", ToolsStrings.Drive_Grid_SameGroup, affectedByCar: true);
        public static readonly IRaceGridMode CandidatesFiltered = new BuiltInGridMode("filtered", ToolsStrings.Drive_Grid_FilteredBy);
        public static readonly IRaceGridMode CandidatesManual = new BuiltInGridMode("manual", ToolsStrings.Drive_Grid_Manual);

        // Custom category
        public static readonly IRaceGridMode Custom = new BuiltInGridMode("custom", "Custom", false);

        public string Id { get; }

        public bool CandidatesMode { get; }

        public bool Filterable { get; }

        public bool AffectedByCar { get; }

        public bool AffectedByTrack { get; }

        private BuiltInGridMode([Localizable(false)] string id, string displayName, bool candidatesMode = true, bool? filterable = null, bool affectedByCar = false,
                bool affectedByTrack = false) {
            Id = id;
            CandidatesMode = candidatesMode;
            Filterable = filterable ?? candidatesMode;
            AffectedByCar = affectedByCar;
            AffectedByTrack = affectedByTrack;
            DisplayName = displayName;
        }
    }

    public class CandidatesGridMode : Displayable, IRaceGridMode {
        public bool CandidatesMode => true;

        public bool Filterable => true;

        [JsonProperty(PropertyName = @"id")]
        public string Id { get; protected set; }

        [JsonProperty(PropertyName = @"name")]
        public override string DisplayName { get; set; }

        [JsonProperty(PropertyName = @"filter")]
        public string Filter { get; protected set; }

        [JsonProperty(PropertyName = @"script")]
        public string Script { get; protected set; }

        [JsonProperty(PropertyName = @"test")]
        public bool Test { get; protected set; }

        [JsonProperty(PropertyName = @"affectedByTrack")]
        public bool AffectedByTrack { get; protected set; }

        [DefaultValue(true), JsonProperty(PropertyName = @"affectedByCar", DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool AffectedByCar { get; protected set; }

        [JsonConstructor, UsedImplicitly]
        private CandidatesGridMode() {}
    }
}