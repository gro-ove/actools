using System;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.GameProperties;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Starters;
using AcTools.Processes;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.SemiGui {
    public static class GameWrapper {
        private static IGameUiFactory _factory;

        public static void RegisterFactory(IGameUiFactory factory) {
            _factory = factory;
        }

        public static event EventHandler<GameEndedArgs> Ended;
        public static event EventHandler<GameFinishedArgs> Finished;
        public static event EventHandler<GameFinishedArgs> Cancelled;

        private class ProgressHandler : IProgress<Game.ProgressState> {
            private readonly IGameUi _ui;

            public ProgressHandler(IGameUi ui) {
                _ui = ui;
            }

            public void Report(Game.ProgressState value) {
                Logging.Write("[GAMEWRAPPER] Progress: " + value);
                _ui.OnProgress(value);
            }
        }

        public static Task StartReplayAsync(Game.StartProperties properties) {
            return StartAsync(properties, false);
        }

        public static Task<Game.Result> StartAsync(Game.StartProperties properties) {
            return StartAsync(properties, true);
        }

        private static void PrepareRaceModeImmediateStart(Game.StartProperties properties) {
            if (!SettingsHolder.Drive.ImmediateStart) return;
            properties.SetAdditional(new ImmediateStart());
        }

        private static void PrepareRaceModeRsr(Game.StartProperties properties) {
            if (!SettingsHolder.LiveTiming.RsrEnabled) return;
            if (SettingsHolder.LiveTiming.RsrDisableAppAutomatically) {
                var rsrMode = properties.GetAdditional<RsrMark>() != null;
                var form = AcSettingsHolder.Forms.Entries.GetByIdOrDefault(RsrMark.FormId);
                if (form != null) {
                    form.IsVisible = rsrMode;
                    AcSettingsHolder.Forms.SaveImmediately();
                }
            }
        }

        private static async Task<Game.Result> StartAsync(Game.StartProperties properties, bool raceMode) {
            if (SettingsHolder.Common.FixResolutionAutomatically) {
                AcSettingsHolder.Video.EnsureResolutionIsCorrect();
            }

            if (raceMode) {
                PrepareRaceModeImmediateStart(properties);
                PrepareRaceModeRsr(properties);
                properties.SetAdditional(new DriverName());
            }

            if (_factory == null) {
                using (ReplaysExtensionSetter.OnlyNewIfEnabled()) {
                    if (raceMode) {
                        properties.SetAdditional(new RaceCommandExecutor(properties));
                    } else {
                        properties.SetAdditional(new ReplayCommandExecutor(properties));
                    }

                    return await Game.StartAsync(AcsStarterFactory.Create(), properties, null, CancellationToken.None);
                }
            }

            using (var ui = _factory.Create()) {
                ui.Show(properties);
                
                try {
                    Game.Result result;
                    using (ReplaysExtensionSetter.OnlyNewIfEnabled()) {
                        if (raceMode) {
                            properties.SetAdditional(new RaceCommandExecutor(properties));
                        } else {
                            properties.SetAdditional(new ReplayCommandExecutor(properties));
                        }

                        result = await Game.StartAsync(AcsStarterFactory.Create(), properties, new ProgressHandler(ui), ui.CancellationToken);
                    }

                    if (ui.CancellationToken.IsCancellationRequested) {
                        ui.OnError(new UserCancelledException());
                        return null;
                    }

                    if (raceMode) {
                        if (result == null) {
                            var whatsGoingOn = AcLogHelper.TryToDetermineWhatsGoingOn();
                            if (whatsGoingOn != null) {
                                properties.SetAdditional(whatsGoingOn);
                            }
                        }

                        var param = new GameEndedArgs(properties, result);
                        Ended?.Invoke(null, param);
                        /* TODO: should set result to null if param.Cancel is true? */

                        var replayHelper = new ReplayHelper(properties, result);
                        (result == null || param.Cancel ? Cancelled : Finished)?.Invoke(null, new GameFinishedArgs(properties, result));

                        ui.OnResult(result, replayHelper);
                    }

                    return result;
                } catch (Exception e) {
                    ui.OnError(e);
                    return null;
                }
            }
        }
    }
}