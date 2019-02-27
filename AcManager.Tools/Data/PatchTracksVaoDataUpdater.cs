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
    public class PatchTrackVaoDataEntry : PatchDataEntry {
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

        protected override bool IsToUnzip => false;
        protected override string DestinationExtension => ".vao-patch";
    }

    public class PatchTracksVaoDataUpdater : PatchBaseDataUpdater<PatchTrackVaoDataEntry> {
        public static PatchTracksVaoDataUpdater Instance { get; } = new PatchTracksVaoDataUpdater();

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

        protected override Task Prepare() {
            return TracksManager.Instance.EnsureLoadedAsync();
        }

        public override string GetBaseUrl() {
            return "/patch/tracks-vao/";
        }

        public override string GetCacheDirectoryName() {
            return "Tracks VAO";
        }

        protected override string GetSourceUrl() {
            return @"tree/master/vao-patches";
        }

        protected override string GetTitle() {
            return "Vertex AO patches for tracks";
        }

        protected override string GetDescription() {
            return "With per-vertex ambient occlusion data, tracks get some of those smooth ambient light shadows for practically nothing.";
        }

        public override string GetDestinationDirectory() {
            return Path.Combine(PatchHelper.GetRootDirectory(), "vao-patches");
        }
    }
}