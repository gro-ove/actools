using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AcManager.DiscordRpc;
using AcManager.Pages.Drive;
using AcManager.Tools.GameProperties;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Managers;
using AcManager.Tools.SemiGui;
using AcTools.Processes;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Converters;
using Newtonsoft.Json;

namespace AcManager.Tools {
    public class GameDiscordPresence : Game.GameHandler {
        private static string[] _knownCarBrands;
        private static string[] _knownTracks;

        private static void InitializeKnownIds() {
            if (_knownCarBrands != null) return;
            _knownCarBrands = ReadIds("CarBrands");
            _knownTracks = ReadIds("Tracks");

            string[] ReadIds(string name) {
                var file = FilesStorage.Instance.GetContentFile("Discord", $"{name}.json");
                if (file.Exists) {
                    try {
                        return JsonConvert.DeserializeObject<string[]>(File.ReadAllText(file.Filename));
                    } catch (Exception e) {
                        Logging.Error(e);
                    }
                }

                return new string[0];
            }
        }

        public override IDisposable Set(Process process) {
            var appId = process != null ? DiscordConnector.Instance.SetAppId(process.Id) : null;
            if (appId != null) {
                _presence.ForceUpdate();
            }

            return new ActionAsDisposable(() => {
                appId?.Dispose();
                _presence?.Dispose();
            });
        }

        private readonly DiscordRichPresence _presence;

        public GameDiscordPresence(Game.StartProperties properties, GameMode mode) {
            InitializeKnownIds();

            switch (mode) {
                case GameMode.Replay:
                    _presence = new DiscordRichPresence(1000, "Preparing to race", "Watching replay");
                    return;
                case GameMode.Benchmark:
                    _presence = new DiscordRichPresence(1000, "Preparing to race", "Running benchmark");
                    return;
            }

            if (properties.GetAdditional<RsrMark>() != null) {
                _presence = new DiscordRichPresence(1000, "RSR", "Hotlap");
            } else if (properties.GetAdditional<SrsMark>() != null) {
                _presence = new DiscordRichPresence(1000, "SRS", "In a race");
            } else if (properties.ModeProperties is Game.OnlineProperties online) {
                _presence = new DiscordRichPresence(1000, "Online", "In a race");
                WatchForOnlineDetails(online).Forget();
            } else if (properties.GetAdditional<SpecialEventsManager.EventProperties>()?.EventId is string challengeId) {
                var challenge = SpecialEventsManager.Instance.GetById(challengeId);
                _presence = new DiscordRichPresence(1000, "Driving Solo", challenge != null ? $"Challenge | {challenge.DisplayName}" : "Challenge");
            } else {
                _presence = new DiscordRichPresence(1000, "Driving Solo", GetSessionName(properties.ModeProperties));
            }

            _presence.Start = DateTime.Now;

            var car = CarsManager.Instance.GetById(properties.BasicProperties?.CarId ?? "");
            if (car != null) {
                var carBrand = car.Brand?.ToLowerInvariant().Replace(" ", "_");
                if (!_knownCarBrands.Contains(carBrand)) {
                    carBrand = "various";
                }

                _presence.SmallImage = new DiscordImage($@"car_{carBrand}", car.Name ?? car.Id);
            }

            var track = TracksManager.Instance.GetLayoutById(properties.BasicProperties?.TrackId ?? "",
                    properties.BasicProperties?.TrackConfigurationId ?? "");
            if (track != null) {
                var trackId = track.MainTrackObject.Id;
                if (!_knownTracks.Contains(trackId)) {
                    trackId = "unknown";
                }

                _presence.LargeImage = new DiscordImage($@"track_{trackId}", track.Name ?? track.Id);
            }
        }

        private async Task WatchForOnlineDetails(Game.OnlineProperties online) {
            if (online.ServerHttpPort == null) return;
            var httpPort = online.ServerHttpPort.Value;

            string joinSecret = null, matchSecret = null;
            while (_presence?.IsDisposed == false) {
                try {
                    var data = await KunosApiProvider.GetInformationDirectAsync(online.ServerIp, online.ServerHttpPort.Value);
                    var currentSession = data.SessionTypes?.ElementAtOrDefault(data.Session);
                    if (currentSession > 0) {
                        _presence.Details = ((Game.SessionType)currentSession).GetDescription();
                    }

                    if (joinSecret == null) {
                        var password = data.Password && OnlineServer.IncludePasswordToInviteLink ? online.Password : null;
                        joinSecret = DiscordHandler.GetJoinSecret(online.ServerIp, httpPort, password);
                        matchSecret = DiscordHandler.GetMatchSecret(online.ServerIp, httpPort, password);
                    }

                    _presence.End = DateTime.Now + TimeSpan.FromSeconds(data.TimeLeft - Math.Round(data.Timestamp / 1000d));
                    _presence.Party = new DiscordParty(data.Id) {
                        Capacity = data.Capacity,
                        Size = data.Clients,
                        JoinSecret = joinSecret,
                        MatchSecret = matchSecret,
                    };
                } catch (Exception e) {
                    Logging.Warning(e.Message);
                }

                await Task.Delay(BuildInformation.IsDebugConfiguration ? 3000 : 15000);
            }
        }

        private static string GetSessionName(Game.BaseModeProperties properties) {
            switch (properties) {
                case Game.TrackdayProperties _:
                    return "Track day";
                case Game.HotlapProperties _:
                    return "Hotlap";
                case Game.TimeAttackProperties _:
                    return "Time attack";
                case Game.PracticeProperties _:
                    return "Practice";
                case Game.WeekendProperties _:
                    return "Weekend";
                case Game.DriftProperties _:
                    return "Drift";
                case Game.DragProperties drag:
                    return $"Drag race | {PluralizingConverter.PluralizeExt(drag.MatchesCount, "{0} run")}";
                case Game.RaceProperties race:
                    return $"Quick race | {PluralizingConverter.PluralizeExt(race.RaceLaps, "{0} lap")}";
                default:
                    return "Race";
            }
        }
    }
}