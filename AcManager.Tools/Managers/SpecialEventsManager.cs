using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Managers.Directories;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcManager.Tools.Starters;
using AcTools;
using AcTools.Processes;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Tools.Managers {
    public class SpecialEventsManager : AcManagerNew<SpecialEventObject> {
        public static IStorage ProgressStorage { get; }

        static SpecialEventsManager() {
            ProgressStorage = new Storage(FilesStorage.Instance.GetFilename("Progress", "Special Challenges.data"));
        }

        private static SpecialEventsManager _instance;

        public static SpecialEventsManager Instance => _instance ?? (_instance = new SpecialEventsManager());

        private SpecialEventsManager() {
            GameWrapper.Finished += GameWrapper_Finished;
        }

        internal class EventProperties {
            public string EventId;
        }

        private async void GameWrapper_Finished(object sender, GameFinishedArgs e) {
            var extra = e.Result?.GetExtraByType<Game.ResultExtraSpecialEvent>();
            if (extra == null || extra.Tier == -1 || string.IsNullOrWhiteSpace(extra.Guid)) return;

            await EnsureLoadedAsync();
            var eventObject = GetByGuid(extra.Guid);
            if (eventObject == null) return;

            eventObject.TakenPlace = (eventObject.ConditionType == null ? 4 : 3) - extra.Tier;

            // throw new NotImplementedException();
        }

        [CanBeNull]
        public SpecialEventObject GetByGuid(string guid) {
            return LoadedOnly.FirstOrDefault(x => x.Guid == guid);
        }

        public async Task UpdateProgress(IProgress<string> progress, CancellationToken cancellation) {
            if (SteamIdHelper.Instance.Value == null) {
                throw new InformativeException("Can’t get challenges progress", "Steam ID is missing.");    
            }

            progress.Report("Finishing preparing…");
            await EnsureLoadedAsync();
            if (cancellation.IsCancellationRequested) return;

            progress.Report("Loading stats…");
            var achievments = await Task.Run(() => SteamWebProvider.TryToGetAchievments(CommonAcConsts.AppId, SteamIdHelper.Instance.Value), cancellation);
            if (cancellation.IsCancellationRequested) return;

            if (achievments == null) {
                throw new InformativeException("Can’t get challenges progress", "Make sure Steam account isn’t private.");
            }

            foreach (var eventObject in LoadedOnly) {
                eventObject.TakenPlace = 5;
            }
            
            foreach (var achievment in achievments.Where(x => x.StartsWith(@"SPECIAL_EVENT_"))) {
                var id = achievment.Substring(0, achievment.Length - 2);
                var place = FlexibleParser.TryParseInt(achievment.Substring(achievment.Length - 1));

                var eventObject = GetById(id);
                if (eventObject == null || !place.HasValue) continue;

                eventObject.TakenPlace = Math.Min(eventObject.TakenPlace, (eventObject.ConditionType == null ? 4 : 3) - place.Value);
            }
        }

        public async Task UpdateProgressViaModule(IProgress<string> progress, CancellationToken cancellation) {
            progress.Report("Getting data…");
            var data = await ModuleStarter.GetDataAsync("achievments", cancellation);

            foreach (var pair in JsonConvert.DeserializeObject<Dictionary<string, int>>(data)) {
                var eventObject = GetById(pair.Key);
                if (eventObject == null) continue;
                eventObject.TakenPlace = Math.Min(eventObject.TakenPlace, (eventObject.ConditionType == null ? 4 : 3) - pair.Value);
            }
        }

        private static readonly string[] WatchedFiles = {
            @"preview.png",
            @"event.ini"
        };

        protected override bool ShouldSkipFile(string objectLocation, string filename) {
            if (base.ShouldSkipFile(objectLocation, filename)) return true;
            var inner = filename.SubstringExt(objectLocation.Length + 1);
            return !WatchedFiles.Contains(inner.ToLowerInvariant());
        }

        public override IAcDirectories Directories { get; } = new AcDirectories(Path.Combine(AcRootDirectory.Instance.RequireValue, @"content", @"specialevents"), null);

        protected override SpecialEventObject CreateAcObject(string id, bool enabled) {
            return new SpecialEventObject(this, id, enabled);
        }
    }
}