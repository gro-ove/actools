using System;

namespace AcTools.WheelAngles.Implementations.Options {
    public class LogitechOptions : WheelOptionsBase {
        #region FFB settings
        private bool _detectSettingsAutomatically = true;

        public bool DetectSettingsAutomatically {
            get => _detectSettingsAutomatically;
            set => Apply(value, ref _detectSettingsAutomatically);
        }

        private bool _forceFeedbackEnable = true;

        public bool ForceFeedbackEnable {
            get => _forceFeedbackEnable;
            set => Apply(value, ref _forceFeedbackEnable);
        }

        private int _overallGainPercentage = 100;

        public int OverallGainPercentage {
            get => _overallGainPercentage;
            set => Apply(value, ref _overallGainPercentage);
        }

        private int _springGainPercentage;

        public int SpringGainPercentage {
            get => _springGainPercentage;
            set => Apply(value, ref _springGainPercentage);
        }

        private int _damperGainPercentage;

        public int DamperGainPercentage {
            get => _damperGainPercentage;
            set => Apply(value, ref _damperGainPercentage);
        }

        private bool _persistentSpringEnable = true;

        public bool PersistentSpringEnable {
            get => _persistentSpringEnable;
            set => Apply(value, ref _persistentSpringEnable);
        }

        private int _defaultSpringGainPercentage;

        public int DefaultSpringGainPercentage {
            get => _defaultSpringGainPercentage;
            set => Apply(value, ref _defaultSpringGainPercentage);
        }

        private bool _combinedPedalsEnable;

        public bool CombinedPedalsEnable {
            get => _combinedPedalsEnable;
            set => Apply(value, ref _combinedPedalsEnable);
        }

        private bool _gameSettingsEnable;

        public bool GameSettingsEnable {
            get => _gameSettingsEnable;
            set => Apply(value, ref _gameSettingsEnable);
        }

        private bool _allowGameSettings;

        public bool AllowGameSettings {
            get => _allowGameSettings;
            set => Apply(value, ref _allowGameSettings);
        }
        #endregion

        private bool _useOwnHandle;

        public bool UseOwnHandle {
            get => _useOwnHandle;
            set => Apply(value, ref _useOwnHandle);
        }

        public bool SpecifyHandle => true;

        private readonly Action _gameStartedCallback;

        public LogitechOptions(Action gameStartedCallback) {
            _gameStartedCallback = gameStartedCallback;
        }

        public void OnGameStarted() {
            _gameStartedCallback?.Invoke();
        }
    }
}