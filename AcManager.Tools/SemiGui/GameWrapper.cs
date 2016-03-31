using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Starters;
using AcTools.Processes;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.SemiGui {
    public interface IGameUiFactory {
        IGameUi Create();
    }

    public interface IGameUi {
        void Show(Game.StartProperties properties);

        void OnProgress(Game.ProgressState progress);

        void OnResult(Game.Result result);

        void OnError(Exception exception);

        CancellationToken CancellationToken { get; }
    }

    public class GameEndedArgs : CancelEventArgs {
        public readonly Game.StartProperties StartProperties;
        public readonly Game.Result Result;

        public GameEndedArgs(Game.StartProperties startProperties, Game.Result result) {
            StartProperties = startProperties;
            Result = result;
        }
    }

    public class GameFinishedArgs : EventArgs {
        public readonly Game.StartProperties StartProperties;
        public readonly Game.Result Result;

        public GameFinishedArgs(Game.StartProperties startProperties, Game.Result result) {
            StartProperties = startProperties;
            Result = result;
        }
    }

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

        public static async Task<Game.Result> StartAsync(Game.StartProperties properties) {
            if (SettingsHolder.Drive.ImmediateStart) {
                properties.SetAdditional(new ImmediateStart());
            }
            properties.SetAdditional(new DriverName());

            if (_factory == null) {
                return await Game.StartAsync(AcsStarterFactory.Create(), properties, null, CancellationToken.None);
            }

            var ui = _factory.Create();
            ui.Show(properties);

            Logging.Write("[GAMEWRAPPER] Started");
            try {
                var result = await Game.StartAsync(AcsStarterFactory.Create(), properties, new ProgressHandler(ui), ui.CancellationToken);
                Logging.Write("[GAMEWRAPPER] Finished: " + (result?.ToString() ?? "NULL"));

                if (result == null) {
                    var whatsGoingOn = AcLogHelper.TryToDetermineWhatsGoingOn();
                    if (whatsGoingOn != null) {
                        properties.SetAdditional(whatsGoingOn);
                    }
                }

                var param = new GameEndedArgs(properties, result);
                Ended?.Invoke(null, param);
                /* TODO: should I set result to null if param.Cancel is true? */
                (result == null || param.Cancel ? Cancelled : Finished)?.Invoke(null, new GameFinishedArgs(properties, result));

                ui.OnResult(result);

                return result;
            } catch(Exception e) {
                Logging.Warning("[GAMEWRAPPER] Exception: " + e);
                ui.OnError(e);
                return null;
            }
        }
    }
}