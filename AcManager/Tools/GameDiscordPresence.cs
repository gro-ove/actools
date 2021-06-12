using System;
using System.Diagnostics;
using System.Threading.Tasks;
using AcManager.DiscordRpc;
using AcManager.Pages.Drive;
using AcManager.Tools.GameProperties;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Managers;
using AcManager.Tools.SemiGui;
using AcTools;
using AcTools.Processes;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Converters;

namespace AcManager.Tools {
    public class GameDiscordPresence : Game.GameHandler {
        public override IDisposable Set(Process process) {
            var appId = process != null ? DiscordConnector.Instance?.SetAppId(process.Id) : null;
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
            switch (mode) {
                case GameMode.Replay:
                    _presence = new DiscordRichPresence(1000, "Preparing to race", "Watching replay");
                    break;
                case GameMode.Benchmark:
                    _presence = new DiscordRichPresence(1000, "Preparing to race", "Running benchmark");
                    break;
                case GameMode.Race:
                    if (properties.GetAdditional<RsrMark>() != null) {
                        _presence = new DiscordRichPresence(1000, "RSR", "Hotlap");
                    } else if (properties.GetAdditional<SrsMark>() != null) {
                        _presence = new DiscordRichPresence(1000, "SRS", "In a race");
                    } else if (properties.GetAdditional<WorldSimSeriesMark>() != null) {
                        _presence = new DiscordRichPresence(1000, "WSS", "In a race");
                    } else if (properties.ModeProperties is Game.OnlineProperties online) {
                        _presence = new DiscordRichPresence(1000, "Online", "In a race");
                        WatchForOnlineDetails(online).Ignore();
                    } else if (properties.GetAdditional<SpecialEventsManager.EventProperties>()?.EventId is string challengeId) {
                        var challenge = SpecialEventsManager.Instance.GetById(challengeId);
                        _presence = new DiscordRichPresence(1000, "Driving Solo", challenge != null ? $"Challenge | {challenge.DisplayName}" : "Challenge");
                    } else {
                        _presence = new DiscordRichPresence(1000, "Driving Solo", GetSessionName(properties.ModeProperties));
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }

            _presence.Now()
                     .Car(properties.BasicProperties?.CarId)
                     .Track(properties.BasicProperties?.TrackId, properties.BasicProperties?.TrackConfigurationId);
        }

        private async Task WatchForOnlineDetails(Game.OnlineProperties online) {
            if (online.ServerHttpPort == null) return;
            var httpPort = online.ServerHttpPort.Value;

            string joinSecret = null, matchSecret = null;
            while (_presence?.IsDisposed == false) {
                try {
                    var data = await KunosApiProvider.GetInformationDirectAsync(online.ServerIp, online.ServerHttpPort.Value);
                    var currentSession = data.SessionTypes?.ArrayElementAtOrDefault(data.Session);
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

                    _presence.ForceUpdate();
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