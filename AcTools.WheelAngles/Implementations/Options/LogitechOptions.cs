using System;

namespace AcTools.WheelAngles.Implementations.Options {
    public class LogitechOptions : WheelOptionsBase {
        #region FFB settings
        private bool _detectSettingsAutomatically = true;

        public bool DetectSettingsAutomatically {
            get => _detectSettingsAutomatically;
            set {
                if (Equals(value, _detectSettingsAutomatically)) return;
                _detectSettingsAutomatically = value;
                OnPropertyChanged();
            }
        }

        private bool _forceFeedbackEnable = true;

        public bool ForceFeedbackEnable {
            get => _forceFeedbackEnable;
            set {
                if (Equals(value, _forceFeedbackEnable)) return;
                _forceFeedbackEnable = value;
                OnPropertyChanged();
            }
        }

        private int _overallGainPercentage = 100;

        public int OverallGainPercentage {
            get => _overallGainPercentage;
            set {
                if (Equals(value, _overallGainPercentage)) return;
                _overallGainPercentage = value;
                OnPropertyChanged();
            }
        }

        private int _springGainPercentage;

        public int SpringGainPercentage {
            get => _springGainPercentage;
            set {
                if (Equals(value, _springGainPercentage)) return;
                _springGainPercentage = value;
                OnPropertyChanged();
            }
        }

        private int _damperGainPercentage;

        public int DamperGainPercentage {
            get => _damperGainPercentage;
            set {
                if (Equals(value, _damperGainPercentage)) return;
                _damperGainPercentage = value;
                OnPropertyChanged();
            }
        }

        private bool _persistentSpringEnable = true;

        public bool PersistentSpringEnable {
            get => _persistentSpringEnable;
            set {
                if (Equals(value, _persistentSpringEnable)) return;
                _persistentSpringEnable = value;
                OnPropertyChanged();
            }
        }

        private int _defaultSpringGainPercentage;

        public int DefaultSpringGainPercentage {
            get => _defaultSpringGainPercentage;
            set {
                if (Equals(value, _defaultSpringGainPercentage)) return;
                _defaultSpringGainPercentage = value;
                OnPropertyChanged();
            }
        }

        private bool _combinedPedalsEnable;

        public bool CombinedPedalsEnable {
            get => _combinedPedalsEnable;
            set {
                if (Equals(value, _combinedPedalsEnable)) return;
                _combinedPedalsEnable = value;
                OnPropertyChanged();
            }
        }

        private bool _gameSettingsEnable;

        public bool GameSettingsEnable {
            get => _gameSettingsEnable;
            set {
                if (Equals(value, _gameSettingsEnable)) return;
                _gameSettingsEnable = value;
                OnPropertyChanged();
            }
        }

        private bool _allowGameSettings;

        public bool AllowGameSettings {
            get => _allowGameSettings;
            set {
                if (Equals(value, _allowGameSettings)) return;
                _allowGameSettings = value;
                OnPropertyChanged();
            }
        }
        #endregion

        private bool _useOwnHandle;

        public bool UseOwnHandle {
            get => _useOwnHandle;
            set {
                if (Equals(value, _useOwnHandle)) return;
                _useOwnHandle = value;
                OnPropertyChanged();
            }
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