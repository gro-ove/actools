using System;
using System.ComponentModel;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Tools.Objects {
    public class ServerSessionEntry : Displayable, IWithId {
        private readonly string _onKey;
        private readonly string _offKey;
        private readonly bool _enabledByDefault;

        public sealed override string DisplayName { get; set; }

        public ServerSessionEntry([Localizable(false)] string key, string name, bool isClosable, bool enabledByDefault) {
            _onKey = key;
            _offKey = $@"__CM_{_onKey}_OFF";
            _enabledByDefault = enabledByDefault;

            DisplayName = name;
            IsClosable = isClosable;
        }

        public void Load(IniFile config) {
            var isEnabled = config.ContainsKey(_onKey);
            if (!isEnabled && !config.ContainsKey(_offKey)) {
                isEnabled = _enabledByDefault;
            }

            IsEnabled = isEnabled;
            Load(IsEnabled ? config[_onKey] : config[_offKey], config["SERVER"]);
        }

        protected virtual void Load(IniFileSection session, IniFileSection main) {
            ConfigName = session.GetNonEmpty("NAME") ?? DisplayName;
            Time = TimeSpan.FromMinutes(session.GetDouble("TIME", 10d));
            IsOpen = session.GetBool("IS_OPEN", true);
        }

        public void Save(IniFile config) {
            if (IsEnabled) {
                Save(config[_onKey], config["SERVER"]);
                config.Remove(_offKey);
            } else {
                Save(config[_offKey], config["SERVER"]);
                config.Remove(_onKey);
            }
        }

        protected virtual void Save(IniFileSection session, IniFileSection main) {
            session.Set("NAME", string.IsNullOrWhiteSpace(ConfigName) ? DisplayName : ConfigName);
            session.Set("TIME", Time.TotalMinutes); // round?
            session.Set("IS_OPEN", IsOpen);
        }

        private string _configName;

        public string ConfigName {
            get => _configName;
            set => Apply(value, ref _configName);
        }

        private bool _isEnabled;

        public bool IsEnabled {
            get => _isEnabled;
            set {
                if (!IsAvailable) value = false;
                if (Equals(value, _isEnabled)) return;
                _isEnabled = value;
                OnPropertyChanged();
            }
        }

        private bool _isAvailable = true;

        public bool IsAvailable {
            get => _isAvailable;
            set {
                if (Equals(value, _isAvailable)) return;
                _isAvailable = value;
                OnPropertyChanged();

                if (!IsAvailable) {
                    IsEnabled = false;
                }
            }
        }

        private TimeSpan _time;

        public TimeSpan Time {
            get => _time;
            set {
                value = value.Clamp(TimeSpan.Zero, TimeSpan.MaxValue);
                if (Equals(value, _time)) return;
                _time = value;
                OnPropertyChanged();
            }
        }

        private bool _isOpen;

        public bool IsOpen {
            get => _isOpen;
            set => Apply(value, ref _isOpen);
        }

        public bool IsClosable { get; }

        string IWithId<string>.Id => _onKey;
    }

    public class ServerQualificationSessionEntry : ServerSessionEntry {
        public ServerQualificationSessionEntry([Localizable(false)] string key, string name, bool isClosable, bool enabledByDefault) : base(key, name, isClosable, enabledByDefault) { }

        protected override void Load(IniFileSection session, IniFileSection main) {
            base.Load(session, main);
            QualifyLimitPercentage = main.GetInt("QUALIFY_MAX_WAIT_PERC", 120);
        }

        protected override void Save(IniFileSection session, IniFileSection main) {
            base.Save(session, main);
            main.Set("QUALIFY_MAX_WAIT_PERC", QualifyLimitPercentage);
        }

        private int _qualifyLimitPercentage;

        public int QualifyLimitPercentage {
            get => _qualifyLimitPercentage;
            set {
                value = value.Clamp(1, 65535);
                if (Equals(value, _qualifyLimitPercentage)) return;
                _qualifyLimitPercentage = value;
                OnPropertyChanged();
            }
        }
    }

    public class ServerRaceSessionEntry : ServerSessionEntry {
        public ServerRaceSessionEntry([Localizable(false)] string key, string name, bool isClosable, bool enabledByDefault) : base(key, name, isClosable, enabledByDefault) { }

        protected override void Load(IniFileSection session, IniFileSection main) {
            base.Load(session, main);
            ExtraLap = main.GetBool("RACE_EXTRA_LAP", false);
            ReversedGridRacePositions = main.GetInt("REVERSED_GRID_RACE_POSITIONS", 0);
            WaitTime = TimeSpan.FromSeconds(session.GetInt("WAIT_TIME", 60));
            JoinType = session.GetIntEnum("IS_OPEN", ServerPresetRaceJoinType.CloseAtStart);
            RaceOverTime = TimeSpan.FromSeconds(main.GetDouble("RACE_OVER_TIME", 60d));
            ResultScreenTime = TimeSpan.FromSeconds(main.GetDouble("RESULT_SCREEN_TIME", 60d));

            var lapsCount = session.GetInt("LAPS", 5);
            LimitByTime = lapsCount == 0;
            if (LimitByTime) {
                LapsCount = session.GetInt("__CM_LAPS_OFF", 5);
            } else {
                Time = TimeSpan.FromMinutes(session.GetDouble("__CM_TIME_OFF", 10d));
                LapsCount = lapsCount;
            }

            var mandatoryPitFrom = main.GetInt("RACE_PIT_WINDOW_START", 0);
            var mandatoryPitTo = main.GetInt("RACE_PIT_WINDOW_END", 0);
            MandatoryPit = mandatoryPitFrom > 0 || mandatoryPitTo > 0;
            if (MandatoryPit) {
                MandatoryPitFrom = mandatoryPitFrom;
                MandatoryPitTo = mandatoryPitTo;
            } else {
                MandatoryPitFrom = main.GetInt("__CM_RACE_PIT_WINDOW_START_OFF", 0);
                MandatoryPitTo = main.GetInt("__CM_RACE_PIT_WINDOW_END_OFF", 0);
            }

        }

        protected override void Save(IniFileSection session, IniFileSection main) {
            base.Save(session, main);
            main.Set("RACE_EXTRA_LAP", ExtraLap);
            main.Set("REVERSED_GRID_RACE_POSITIONS", ReversedGridRacePositions);
            session.Set("WAIT_TIME", WaitTime.TotalSeconds); // round?
            session.SetIntEnum("IS_OPEN", JoinType);

            main.Set("RACE_OVER_TIME", RaceOverTime.TotalSeconds.RoundToInt());
            main.Set("RESULT_SCREEN_TIME", ResultScreenTime.TotalSeconds.RoundToInt());

            if (LimitByTime) {
                // TIME is set in base implementation
                session.Set("LAPS", 0);
                session.Set("__CM_LAPS_OFF", LapsCount);
                session.Remove("__CM_TIME_OFF");
            } else {
                session.Set("LAPS", LapsCount);
                session.Set("TIME", 0);
                session.Set("__CM_TIME_OFF", Time.TotalMinutes);
                session.Remove("__CM_LAPS_OFF");
            }

            if (MandatoryPit) {
                session.Set("RACE_PIT_WINDOW_START", MandatoryPitFrom);
                session.Set("RACE_PIT_WINDOW_END", MandatoryPitTo);
                session.Remove("__CM_RACE_PIT_WINDOW_START_OFF");
                session.Remove("__CM_RACE_PIT_WINDOW_END_OFF");
            } else {
                session.Set("__CM_RACE_PIT_WINDOW_START_OFF", MandatoryPitFrom);
                session.Set("__CM_RACE_PIT_WINDOW_END_OFF", MandatoryPitTo);
                session.Remove("RACE_PIT_WINDOW_START");
                session.Remove("RACE_PIT_WINDOW_END");
            }
        }

        private bool _limitByTime;

        public bool LimitByTime {
            get => _limitByTime;
            set => Apply(value, ref _limitByTime);
        }

        private bool _extraLap;

        public bool ExtraLap {
            get => _extraLap;
            set => Apply(value, ref _extraLap);
        }

        private bool _mandatoryPit;

        public bool MandatoryPit {
            get => _mandatoryPit;
            set => Apply(value, ref _mandatoryPit);
        }

        private int _mandatoryPitFrom;

        public int MandatoryPitFrom {
            get => _mandatoryPitFrom;
            set {
                if (Equals(value, _mandatoryPitFrom)) return;
                _mandatoryPitFrom = value;
                OnPropertyChanged();

                if (_mandatoryPitTo < value) {
                    MandatoryPitTo = value;
                }
            }
        }

        private int _mandatoryPitTo;

        public int MandatoryPitTo {
            get => _mandatoryPitTo;
            set {
                if (Equals(value, _mandatoryPitTo)) return;
                _mandatoryPitTo = value;
                OnPropertyChanged();

                if (_mandatoryPitFrom > value) {
                    MandatoryPitFrom = value;
                }
            }
        }

        private int _reversedGridRacePositions;

        public int ReversedGridRacePositions {
            get => _reversedGridRacePositions;
            set => Apply(value, ref _reversedGridRacePositions);
        }

        private int _lapsCount;

        public int LapsCount {
            get => _lapsCount;
            set => Apply(value, ref _lapsCount);
        }

        private TimeSpan _waitTime;

        public TimeSpan WaitTime {
            get => _waitTime;
            set => Apply(value, ref _waitTime);
        }

        private ServerPresetRaceJoinType _joinType;

        public ServerPresetRaceJoinType JoinType {
            get => _joinType;
            set => Apply(value, ref _joinType);
        }

        private TimeSpan _resultScreenTime;

        public TimeSpan ResultScreenTime {
            get => _resultScreenTime;
            set => Apply(value, ref _resultScreenTime);
        }

        private TimeSpan _raceOverTime;

        public TimeSpan RaceOverTime {
            get => _raceOverTime;
            set => Apply(value, ref _raceOverTime);
        }
    }
}