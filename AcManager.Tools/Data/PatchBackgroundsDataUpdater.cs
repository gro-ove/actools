using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Serialization;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Tools.Data {
    [JsonObject(MemberSerialization.OptIn)]
    public class PatchBackgroundDataEntry : PatchDataEntry {
        [CanBeNull]
        public TrackObjectBase Track { get; private set; }

        protected override Task<Tuple<string, bool>> GetStateAsync() {
            var match = Regex.Match(Id, @"^(.+)_(\d+)$");
            if (!match.Success) {
                return Task.FromResult<Tuple<string, bool>>(null);
            }

            var postfix = $" #{match.Groups[2].Value}";
            var baseTrackId = match.Groups[1].Value;
            if (baseTrackId.Contains(@"__")) {
                var trackId = baseTrackId.Replace(@"__", @"/");
                Track = TracksManager.Instance.GetLayoutById(trackId);
                if (Track != null) {
                    return Task.FromResult(Tuple.Create(Track.LayoutName + postfix, true));
                }
            }

            var track = TracksManager.Instance.GetById(baseTrackId);
            Track = track;
            return Task.FromResult(track != null ? Tuple.Create(track.DisplayNameWithoutCount + postfix, true) : null);
        }

        protected override bool IsToUnzip => false;

        protected override string DestinationExtension => ".jpg";

        protected override string GetDestinationFilename() {
            var match = Regex.Match(Id, @"^(.+)_(\d+)$");
            if (!match.Success) {
                return base.GetDestinationFilename();
            }

            return Path.Combine(Parent.GetDestinationDirectory(),
                    $@"{match.Groups[1].Value}_{(match.Groups[2].Value.As(0) + 900)}{DestinationExtension}");
        }
    }

    public class PatchBackgroundDataUpdater : PatchBaseDataUpdater<PatchBackgroundDataEntry> {
        public static PatchBackgroundDataUpdater Instance { get; } = new PatchBackgroundDataUpdater();

        protected override Task Prepare() {
            return TracksManager.Instance.EnsureLoadedAsync();
        }

        protected override IEnumerable<string> AutoLoadSelector(string requested, IEnumerable<string> available) {
            if (!requested.Contains(@"/")) {
                var regex = new Regex($@"^{Regex.Escape(requested)}_\d+$");
                return available.Where(x => regex.IsMatch(x));
            } else {
                var regex = new Regex($@"^{Regex.Escape(requested.Replace(@"/", @"__"))}_\d+$");
                var availableList = available.ToList();
                if (availableList.Any(x => regex.IsMatch(x))) {
                    return availableList.Where(x => regex.IsMatch(x));
                }

                regex = new Regex($@"^{Regex.Escape(requested.Split('/')[0])}_\d+$");
                return availableList.Where(x => regex.IsMatch(x));
            }
        }

        public override string GetBaseUrl() {
            return "/patch/backgrounds/";
        }

        public override string GetCacheDirectoryName() {
            return "Backgrounds";
        }

        protected override string GetSourceUrl() {
            return @"tree/master/backgrounds";
        }

        protected override string GetTitle() {
            return "Backgrounds for loading screen";
        }

        protected override string GetDescription() {
            return
                    "To freshen up loading screen, patch has an option of showing an image of a track being loaded. It would look for your screenshots or favourite images (all optional), but, if nothing found, it would try to use one of images shared on GitHub repo.";
        }

        public override string GetDestinationDirectory() {
            return Path.Combine(PatchHelper.GetRootDirectory(), "backgrounds");
        }
    }
}