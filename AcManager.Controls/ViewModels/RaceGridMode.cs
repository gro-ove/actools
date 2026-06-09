using System.ComponentModel;
using System.Text.RegularExpressions;
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

        private BuiltInGridMode([Localizable(false)] string id, string displayName, bool candidatesMode = true, bool? filterable = null,
                bool affectedByCar = false, bool affectedByTrack = false) {
            Id = id;
            CandidatesMode = candidatesMode;
            Filterable = filterable ?? candidatesMode;
            AffectedByCar = affectedByCar;
            AffectedByTrack = affectedByTrack;
            DisplayName = displayName;
        }
    }

    public sealed class CandidatesGridMode : Displayable, IRaceGridMode {
        public bool CandidatesMode => true;
        public bool Filterable => true;

        public string Id { get; }

        [JsonProperty(PropertyName = @"filter")]
        public string Filter { get; internal set; }

        [JsonProperty(PropertyName = @"script")]
        public string Script { get; internal set; }

        [JsonProperty(PropertyName = @"test")]
        public bool Test { get; internal set; }

        [JsonProperty(PropertyName = @"affectedByTrack")]
        public bool AffectedByTrack { get; internal set; }

        [DefaultValue(true), JsonProperty(PropertyName = @"affectedByCar", DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool AffectedByCar { get; internal set; }

        private static string _namespace;

        public static void SetNamespace(string space) {
            _namespace = space == null ? null : IdFromName(space);
        }

        [NotNull]
        private static string IdFromName([NotNull] string name) {
            return Regex.Replace(name, @"[/\\]+", "_").ToLowerInvariant();
        }

        [JsonConstructor, UsedImplicitly]
        private CandidatesGridMode(string id = null, string name = null) {
            Id = id ?? (_namespace ?? @".") + @"/" + IdFromName(name ?? @".");
            DisplayName = name;
        }
    }
}