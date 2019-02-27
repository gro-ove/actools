using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Tools.Data {
    [JsonObject(MemberSerialization.OptIn)]
    public class PatchTrackDataEntry : PatchDataEntry {
        [CanBeNull, JsonProperty("lightsCount")]
        public string LightsCount { get; private set; }

        [CanBeNull]
        public TrackObjectBase Track { get; private set; }

        protected override Task<Tuple<string, bool>> GetStateAsync() {
            if (Id.Contains(@"__")) {
                var trackId = Id.Replace(@"__", @"/");
                Track = TracksManager.Instance.GetLayoutById(trackId);
                if (Track != null) {
                    return Task.FromResult(Tuple.Create(Track.LayoutName, true));
                }
            }

            var track = TracksManager.Instance.GetById(Id);
            Track = track;
            return Task.FromResult(track != null ? Tuple.Create(track.DisplayNameWithoutCount, true) : null);
        }

        protected override bool IsToUnzip => true;
        protected override string DestinationExtension => ".ini";
    }

    public class PatchTracksDataUpdater : PatchBaseDataUpdater<PatchTrackDataEntry> {
        public static PatchTracksDataUpdater Instance { get; } = new PatchTracksDataUpdater();

        protected override Task Prepare() {
            return TracksManager.Instance.EnsureLoadedAsync();
        }

        protected override IEnumerable<string> AutoLoadSelector(string requested, IEnumerable<string> available) {
            if (!requested.Contains(@"/")) {
                return base.AutoLoadSelector(requested, available);
            }

            return new[] {
                // If data for this layout is not found, use main one
                available.Contains(requested.Replace(@"/", @"__"))
                        ? requested.Replace(@"/", @"__")
                        : requested.Split('/')[0]
            };
        }

        public override string GetBaseUrl() {
            return "/patch/tracks-configs/";
        }

        public override string GetCacheDirectoryName() {
            return "Tracks Configs";
        }

        protected override string GetSourceUrl() {
            return @"tree/master/config/tracks";
        }

        protected override string GetTitle() {
            return "Tracks configs";
        }

        protected override string GetDescription() {
            return
                    "Carefully prepared by incredible community, track configs not only specify where to position various light sources, but also add new objects like lamp posts, and adjust materials to give tracks more realistic (often meaning properly saturated) look.";
        }

        public override string GetDestinationDirectory() {
            return Path.Combine(PatchHelper.GetRootDirectory(), "config", "tracks", "loaded");
        }
    }
}