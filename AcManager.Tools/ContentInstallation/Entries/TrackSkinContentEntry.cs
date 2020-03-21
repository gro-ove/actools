using System;
using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Utils;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation.Entries {
    public class TrackSkinContentEntry : ContentEntryBase<TrackSkinObject> {
        public override double Priority => 40d;

        [NotNull]
        private readonly TrackObject _track;

        public TrackSkinContentEntry([NotNull] string path, [NotNull] string id, [NotNull] string trackId, string name, string version, byte[] iconData = null)
                : base(path, id, name, version, iconData) {
            _track = TracksManager.Instance.GetById(trackId) ?? throw new Exception($"Track “{trackId}” for the skin not found");
            NewFormat = string.Format(ToolsStrings.ContentInstallation_CarSkinNew, "{0}", _track.DisplayName);
            ExistingFormat = string.Format(ToolsStrings.ContentInstallation_CarSkinExisting, "{0}", _track.DisplayName);
        }

        public override string GenericModTypeName => "Track skin";
        public override string NewFormat { get; }
        public override string ExistingFormat { get; }

        public override FileAcManager<TrackSkinObject> GetManager() {
            return _track.SkinsManager;
        }

        protected override IEnumerable<UpdateOption> GetUpdateOptions() {
            bool UiFilter(string x) {
                return !FileUtils.ArePathsEqual(x, @"ui_track_skin.json");
            }

            bool PreviewFilter(string x) {
                return !FileUtils.ArePathsEqual(x, @"preview.png");
            }

            return base.GetUpdateOptions().Union(new[] {
                new UpdateOption(ToolsStrings.ContentInstallation_KeepUiInformation, false) { Filter = UiFilter },
                new UpdateOption("Update over existing files, keep preview", false) { Filter = PreviewFilter },
                new UpdateOption("Update over existing files, keep UI information & preview", false) { Filter = x => UiFilter(x) && PreviewFilter(x) }
            });
        }
    }
}