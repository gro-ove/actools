using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.GameProperties;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.AcLog;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Managers;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Starters;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using StringBasedFilter;

namespace AcManager.Tools.SemiGui {
    public static class GameWrapper {
        private static IAnyFactory<IGameUi> _uiFactory;

        public static void RegisterFactory(IAnyFactory<IGameUi> factory) {
            _uiFactory = factory;
        }

        private static IAnyFactory<Game.AssistsProperties> _defaultAssistsFactory;

        public static void RegisterFactory(IAnyFactory<Game.AssistsProperties> factory) {
            _defaultAssistsFactory = factory;
        }

        public static event EventHandler<GameStartedArgs> Started;
        public static event EventHandler<GameEndedArgs> Ended;
        public static event EventHandler<GameFinishedArgs> Finished;
        public static event EventHandler<GameFinishedArgs> Cancelled;

        public static bool IsInGame { get; private set; }

        private class ProgressHandler : IProgress<Game.ProgressState> {
            private readonly IGameUi _ui;

            public ProgressHandler(IGameUi ui) {
                _ui = ui;
            }

            public void Report(Game.ProgressState value) {
                _ui.OnProgress(value);
            }
        }

        public static Task StartBenchmarkAsync(Game.StartProperties properties) {
            return StartAsync(properties, GameMode.Benchmark);
        }

        public static Task StartReplayAsync(Game.StartProperties properties) {
            return StartAsync(properties, GameMode.Replay);
        }

        public static Task<Game.Result> StartAsync(Game.StartProperties properties) {
            return StartAsync(properties, GameMode.Race);
        }

        private static void PrepareRaceModeImmediateStart(Game.StartProperties properties) {
            if (!SettingsHolder.Drive.ImmediateStart) return;
            properties.SetAdditional(new ImmediateStart());
        }

        private static void PrepareRaceModeRsr(Game.StartProperties properties) {
            var rsrMode = properties.GetAdditional<RsrMark>() != null;
            var form = AcSettingsHolder.Forms.Entries.GetByIdOrDefault(RsrMark.FormId);
            if (form != null) {
                form.SetVisibility(rsrMode);
                AcSettingsHolder.Forms.SaveImmediately();
            }
        }

        private class StringTester : ITester<string> {
            public string ParameterFromKey(string key) {
                return null;
            }

            public bool Test(string obj, string key, ITestEntry value) {
                return key == null && value.Test(obj);
            }
        }

        private static void PrepareRaceDriverName(Game.StartProperties properties) {
            if (properties.HasAdditional<SrsMark>()) return;

            if (properties.BasicProperties?.DriverName != null) {
                properties.SetAdditional(new DriverName(properties.BasicProperties.DriverName, properties.BasicProperties.DriverNationality));
                return;
            }

            var online = properties.ModeProperties as Game.OnlineProperties;
            if (online != null && SettingsHolder.Live.SrsEnabled && SettingsHolder.Live.SrsAutoMode) {
                var filter = Filter.Create(new StringTester(), SettingsHolder.Live.SrsAutoMask, true);
                if (filter.Test(online.ServerName ?? "")) {
                    Logging.Write("Looks like this is a SRS server, let’s use SRS name");
                    properties.SetAdditional(new SrsMark {
                        Name = SrsMark.GetName(),
                        Nationality = "",
                        Team = ""
                    });
                    return;
                }
            }

            properties.SetAdditional(new DriverName());
        }

        /*private class FocusHelper : Game.GameHandler {
            public override IDisposable Set(Process process) {
                try {
                    User32.BringProcessWindowToFront(process);
                } catch (Exception e) {
                    Logging.Warning(e);
                }

                return null;
            }
        }*/

        private static bool _nationCodesProviderSet;

        private static void StartAsync_AdjustProperties(Game.StartProperties properties) {
            if (SettingsHolder.Integrated.RsrLimitTemperature && properties.ConditionProperties != null &&
                    (properties.ConditionProperties.AmbientTemperature < 10d || properties.ConditionProperties.RoadTemperature < 10d) &&
                    AcSettingsHolder.Python.IsActivated("RsrLiveTime")) {
                Toast.Show("Temperature Set To 10 °C", "RSR is active, and according to its rules, you are not allowed to use temperatures lower than 10 °C");
                properties.ConditionProperties.AmbientTemperature = 10d;
                properties.ConditionProperties.RoadTemperature = 10d;
            }
        }

        private static void StartAsync_Prepare(Game.StartProperties properties) {
            if (!_nationCodesProviderSet) {
                _nationCodesProviderSet = true;
                try {
                    Game.NationCodeProvider = NationCodeProvider.Instance;
                } catch (Exception e) {
                    Logging.Unexpected(e);
                }
            }

            AcSettingsHolder.Graphics.FixShadowMapBias();
            CarCustomDataHelper.Revert();

            if (SettingsHolder.Drive.CopyFilterToSystemForOculus && AcSettingsHolder.Video.CameraMode.Id == "OCULUS") {
                properties.SetAdditional(new CopyFilterToSystemForOculusHelper());
            }

            if (SettingsHolder.Common.FixResolutionAutomatically) {
                Logging.Debug("Trying to fix resolution just in case…");
                AcSettingsHolder.Video.EnsureResolutionIsCorrect();
            }

            if (SettingsHolder.Drive.WeatherSpecificClouds) {
                properties.SetAdditional(new WeatherSpecificCloudsHelper());
            }

            if (SettingsHolder.Drive.WeatherSpecificTyreSmoke) {
                properties.SetAdditional(new WeatherSpecificTyreSmokeHelper());
            }

            if (SettingsHolder.Live.RsrEnabled && SettingsHolder.Live.RsrDisableAppAutomatically) {
                PrepareRaceModeRsr(properties);
            }

            var carId = properties.BasicProperties?.CarId;
            if (carId != null) {
                if (SettingsHolder.Drive.SidekickIntegration) {
                    SidekickHelper.UpdateSidekickDatabase(carId);
                }

                if (SettingsHolder.Drive.SidekickOdometerExportValues) {
                    SidekickHelper.OdometerExport(carId);
                }

                if (SettingsHolder.Drive.RaceEssentialsIntegration) {
                    RaceEssentialsHelper.UpdateRaceEssentialsDatabase(carId, false);
                }

                if (SettingsHolder.Drive.StereoOdometerExportValues) {
                    StereoOdometerHelper.Export(carId);
                }
            }

            properties.SetAdditional(new ModeSpecificPresetsHelper());
            properties.SetAdditional(new WeatherSpecificVideoSettingsHelper());
            properties.SetAdditional(new CarSpecificControlsPresetHelper());
        }

        private static void StartAsync_PrepareRace(Game.StartProperties properties) {
            if (properties.AssistsProperties == null) {
                properties.AssistsProperties = _defaultAssistsFactory?.Create();
            }

            PrepareRaceModeImmediateStart(properties);
            PrepareRaceDriverName(properties);

            Logging.Write("Assists: " + properties.AssistsProperties?.GetDescription());
        }

        private static async Task<Game.Result> StartAsync_NoUi(Game.StartProperties properties, GameMode mode) {
            using (ReplaysExtensionSetter.OnlyNewIfEnabled())
            using (ScreenshotsConverter.OnlyNewIfEnabled()) {
                if (mode == GameMode.Race) {
                    properties.SetAdditional(new RaceCommandExecutor(properties));
                } else if (mode == GameMode.Replay) {
                    properties.SetAdditional(new ReplayCommandExecutor(properties));
                }

                return await Game.StartAsync(AcsStarterFactory.Create(), properties, null, CancellationToken.None);
            }
        }

        private static async Task PrepareReplay(Game.StartProperties properties, IGameUi ui, CancellationToken cancellation) {
            var replayProperties = properties.ReplayProperties;
            if (replayProperties != null) {
                var replayFilename = replayProperties.Filename ?? Path.Combine(FileUtils.GetReplaysDirectory(), replayProperties.Name);
                ui.OnProgress("Checking replay for fake cars…");

                var fakes = await FakeCarsHelper.GetFakeCarsIds(replayFilename);
                if (fakes.Count > 0) {
                    Logging.Debug("Fakes found: " + fakes.Select(x => $"{x.FakeId} ({x.SourceId})").JoinToString(", "));
                    foreach (var fake in fakes) {
                        var car = CarsManager.Instance.GetById(fake.SourceId);
                        if (car != null) {
                            FakeCarsHelper.CreateFakeCar(car, fake.FakeId, null);
                        } else {
                            Logging.Warning("Original not found: " + fake.SourceId);
                        }
                    }
                }
            }
        }

        private static async Task<Game.Result> StartAsync_Ui(Game.StartProperties properties, GameMode mode) {
            using (var ui = _uiFactory.Create()) {
                Logging.Write($"Starting game: {properties.GetDescription()}");
                ui.Show(properties, mode);

                CancellationTokenSource linked = null;
                IsInGame = true;

                try {
                    Game.Result result;
                    using (ReplaysExtensionSetter.OnlyNewIfEnabled())
                    using (ScreenshotsConverter.OnlyNewIfEnabled()) {
                        if (mode == GameMode.Race) {
                            properties.SetAdditional(new RaceCommandExecutor(properties));
                            Started?.Invoke(null, new GameStartedArgs(properties));

                            if (SettingsHolder.Drive.ContinueOnEscape) {
                                properties.SetAdditional(new ContinueRaceHelper());
                            }
                        } else if (mode == GameMode.Replay) {
                            properties.SetAdditional(new ReplayCommandExecutor(properties));
                        }

                        var cancellationToken = ui.CancellationToken;
                        if (SettingsHolder.Drive.ImmediateCancel) {
                            var cancelHelper = new ImmediateCancelHelper();
                            linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cancelHelper.GetCancellationToken());
                            cancellationToken = linked.Token;
                            properties.SetAdditional(cancelHelper);
                            properties.SetKeyboardListener = true;
                        }

                        if (mode == GameMode.Replay) {
                            await PrepareReplay(properties, ui, cancellationToken);
                        }

                        result = await Game.StartAsync(AcsStarterFactory.Create(), properties, new ProgressHandler(ui), cancellationToken);
                    }

                    Logging.Write($"Result: {result?.GetDescription() ?? @"<NULL>"}");
                    if (ui.CancellationToken.IsCancellationRequested) {
                        ui.OnError(new UserCancelledException());
                        return null;
                    }

                    var whatsGoingOn = mode != GameMode.Race || result == null ? AcLogHelper.TryToDetermineWhatsGoingOn() : null;
                    if (whatsGoingOn != null) {
                        properties.SetAdditional(whatsGoingOn);
                    }

                    if (mode == GameMode.Race) {
                        var param = new GameEndedArgs(properties, result);
                        Ended?.Invoke(null, param);
                        /* TODO: should set result to null if param.Cancel is true? */

                        var replayHelper = new ReplayHelper(properties, result);
                        (result == null || param.Cancel ? Cancelled : Finished)?.Invoke(null, new GameFinishedArgs(properties, result));

                        ui.OnResult(result, replayHelper);
                    } else {
                        ui.OnResult(null, null);
                    }

                    return result;
                } catch (Exception e) when (e.IsCanceled()) {
                    // ui.OnError(new UserCancelledException());
                    ui.OnResult(null, null);
                    return null;
                } catch (Exception e) {
                    Logging.Warning(e);
                    ui.OnError(e);
                    return null;
                } finally {
                    linked?.Dispose();
                    IsInGame = false;
                }
            }
        }

        private static Task<Game.Result> StartAsync(Game.StartProperties properties, GameMode mode) {
            StartAsync_AdjustProperties(properties);
            StartAsync_Prepare(properties);
            // properties.SetAdditional(new FocusHelper());

            if (mode == GameMode.Race) {
                StartAsync_PrepareRace(properties);
            }

            return _uiFactory == null ? StartAsync_NoUi(properties, mode) : StartAsync_Ui(properties, mode);
        }
    }
}