using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
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
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
            string[] achievments;
            try {
                achievments = await Task.Run(() => SteamWebProvider.GetAchievments(CommonAcConsts.AppId, SteamIdHelper.Instance.Value), cancellation);
                if (cancellation.IsCancellationRequested) return;
            } catch (WebException e)
                    when (e.Status == WebExceptionStatus.ProtocolError && (e.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.InternalServerError) {
                throw new InformativeException("Can’t get challenges progress because of internal server error (500)", "Make sure Steam account isn’t private.");
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
            if (data == null || cancellation.IsCancellationRequested) return;

            foreach (var pair in JsonConvert.DeserializeObject<Dictionary<string, int>>(data)) {
                var eventObject = GetById(pair.Key);
                if (eventObject == null) continue;
                eventObject.TakenPlace = Math.Min(eventObject.TakenPlace, (eventObject.ConditionType == null ? 4 : 3) - pair.Value);
            }
        }

        public async Task UpdateProgressViaSidePassage(IProgress<string> progress, CancellationToken cancellation) {
            progress.Report("Getting data…");
            var data = await SidePassageStarter.GetAchievementsAsync(cancellation);
            if (data == null || cancellation.IsCancellationRequested) return;

            foreach (var pair in JObject.Parse(data)["achievements"].ToObject<Dictionary<string, string>[]>()) {
                var eventObject = GetById(pair.GetValueOrDefault("name") ?? "");
                if (eventObject == null) continue;
                eventObject.TakenPlace = Math.Min(eventObject.TakenPlace,
                        (eventObject.ConditionType == null ? 4 : 3) - pair.GetValueOrDefault("maxTier").AsInt(-1));
            }
        }

        public async Task UpdateProgressViaSteamStarter(IProgress<string> progress, CancellationToken cancellation) {
            progress.Report("Getting data…");
            var data = await Task.Run(() => SteamStarter.GetAchievements());
            if (data == null || cancellation.IsCancellationRequested) return;

            foreach (var pair in JObject.Parse(data)["achievements"].ToObject<Dictionary<string, string>[]>()) {
                var eventObject = GetById(pair.GetValueOrDefault("name") ?? "");
                if (eventObject == null) continue;
                eventObject.TakenPlace = Math.Min(eventObject.TakenPlace,
                        (eventObject.ConditionType == null ? 4 : 3) - pair.GetValueOrDefault("maxTier").AsInt(-1));
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