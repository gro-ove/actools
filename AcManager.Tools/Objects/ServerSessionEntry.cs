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
            Load(IsEnabled ? config[_onKey] : config[_offKey]);
        }

        protected virtual void Load(IniFileSection section) {
            ConfigName = section.GetNonEmpty("NAME") ?? DisplayName;
            Time = TimeSpan.FromMinutes(section.GetDouble("TIME", 10d));
            IsOpen = section.GetBool("IS_OPEN", true);
        }

        public void Save(IniFile config) {
            if (IsEnabled) {
                Save(config[_onKey]);
                config.Remove(_offKey);
            } else {
                Save(config[_offKey]);
                config.Remove(_onKey);
            }
        }

        protected virtual void Save(IniFileSection section) {
            section.Set("NAME", string.IsNullOrWhiteSpace(ConfigName) ? DisplayName : ConfigName);
            section.Set("TIME", Time.TotalMinutes); // round?
            section.Set("IS_OPEN", IsOpen);
        }

        private string _configName;

        public string ConfigName {
            get { return _configName; }
            set {
                if (Equals(value, _configName)) return;
                _configName = value;
                OnPropertyChanged();
            }
        }

        private bool _isEnabled;

        public bool IsEnabled {
            get { return _isEnabled; }
            set {
                if (!IsAvailable) value = false;
                if (Equals(value, _isEnabled)) return;
                _isEnabled = value;
                OnPropertyChanged();
            }
        }

        private bool _isAvailable = true;

        public bool IsAvailable {
            get { return _isAvailable; }
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
            get { return _time; }
            set {
                value = value.Clamp(TimeSpan.Zero, TimeSpan.MaxValue);
                if (Equals(value, _time)) return;
                _time = value;
                OnPropertyChanged();
            }
        }

        private bool _isOpen;

        public bool IsOpen {
            get { return _isOpen; }
            set {
                if (Equals(value, _isOpen)) return;
                _isOpen = value;
                OnPropertyChanged();
            }
        }

        public bool IsClosable { get; }

        string IWithId.Id => _onKey;
    }

    public class ServerRaceSessionEntry : ServerSessionEntry {
        public ServerRaceSessionEntry([Localizable(false)] string key, string name, bool isClosable, bool enabledByDefault) : base(key, name, isClosable, enabledByDefault) { }

        protected override void Load(IniFileSection section) {
            base.Load(section);
            LapsCount = section.GetInt("LAPS", 5);
            WaitTime = TimeSpan.FromSeconds(section.GetInt("WAIT_TIME", 60));
            JoinType = section.GetIntEnum("IS_OPEN", ServerPresetRaceJoinType.CloseAtStart);
        }

        protected override void Save(IniFileSection section) {
            base.Save(section);
            section.Set("LAPS", LapsCount);
            section.Set("WAIT_TIME", WaitTime.TotalSeconds); // round?
            section.SetIntEnum("IS_OPEN", JoinType);
        }

        private int _lapsCount;

        public int LapsCount {
            get { return _lapsCount; }
            set {
                if (Equals(value, _lapsCount)) return;
                _lapsCount = value;
                OnPropertyChanged();
            }
        }

        private TimeSpan _waitTime;

        public TimeSpan WaitTime {
            get { return _waitTime; }
            set {
                if (Equals(value, _waitTime)) return;
                _waitTime = value;
                OnPropertyChanged();
            }
        }

        private ServerPresetRaceJoinType _joinType;

        public ServerPresetRaceJoinType JoinType {
            get { return _joinType; }
            set {
                if (Equals(value, _joinType)) return;
                _joinType = value;
                OnPropertyChanged();
            }
        }
    }
}