using System;
using System.ComponentModel;
using AcTools.Utils.Helpers;

namespace AcTools.WheelAngles.Implementations.Options {
    public enum LogitechG29HandleOptions {
        [Description("Don’t specify handle")]
        NoHandle = 0,

        [Description("Use Assetto Corsa window handle")]
        AcHandle = 1,

        [Description("Use main CM’s window handle")]
        MainHandle = 2,

        [Description("Use fake CM’s window handle")]
        FakeHandle = 3
    }

    public class LogitechG29Options : WheelOptionsBase, IGameWaitingWheelOptions {
        public LogitechG29HandleOptions[] HandleOptions { get; } = EnumExtension.GetValues<LogitechG29HandleOptions>();

        private LogitechG29HandleOptions _handle = LogitechG29HandleOptions.MainHandle;

        public LogitechG29HandleOptions Handle {
            get => _handle;
            set => Apply(value, ref _handle);
        }

        private readonly Action _gameStartedCallback;

        public LogitechG29Options(Action gameStartedCallback) {
            _gameStartedCallback = gameStartedCallback;
        }

        public void OnGameStarted() {
            _gameStartedCallback?.Invoke();
        }
    }
}