using System.Text;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Presets;
using AcTools.DataFile;
using AcTools.Processes;
using AcTools.Utils;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Controls.ViewModels {
    public class TrackStateViewModelBase : NotifyPropertyChanged {
        private const string DefaultKey = "TrackStateVM.sd";
        public static readonly PresetsCategory PresetableCategory = new PresetsCategory("Track States");

        #region Properties
        private bool _weatherDefined;

        public bool WeatherDefined {
            get => _weatherDefined;
            set => Apply(value, ref _weatherDefined);
        }

        private double _gripStart;

        public double GripStart {
            get => _gripStart;
            set {
                value = value.Clamp(0d, 2d);
                if (!SettingsHolder.Drive.AllowDecimalTrackState) {
                    value = value.Round(0.01);
                }
                if (Equals(value, _gripStart)) return;
                _gripStart = value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        private double _gripTransfer;

        public double GripTransfer {
            get => _gripTransfer;
            set {
                value = value.Clamp(0d, 2d);
                if (!SettingsHolder.Drive.AllowDecimalTrackState) {
                    value = value.Round(0.01);
                }
                if (Equals(value, _gripTransfer)) return;
                _gripTransfer = value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        private double _gripRandomness;

        public double GripRandomness {
            get => _gripRandomness;
            set {
                value = value.Clamp(0d, 2d);
                if (!SettingsHolder.Drive.AllowDecimalTrackState) {
                    value = value.Round(0.01);
                }
                if (Equals(value, _gripRandomness)) return;
                _gripRandomness = value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        private int _lapGain;

        public int LapGain {
            get => _lapGain;
            set {
                if (Equals(value, _lapGain)) return;
                _lapGain = value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        private string _description;

        [CanBeNull]
        public string Description {
            get => _description;
            set {
                if (Equals(value, _description)) return;
                _description = value;
                OnPropertyChanged();
                SaveLater();
            }
        }
        #endregion

        #region Saveable
        private class SaveableData {
            [JsonProperty("s")]
            public double GripStart = 95d;

            [JsonProperty("t")]
            public double GripTransfer = 90d;

            [JsonProperty("r")]
            public double GripRandomness = 2d;

            [JsonProperty("g")]
            public int LapGain = 132;

            [JsonProperty("d")]
            public string Description;

            [JsonProperty("w")]
            public bool WeatherDefined;
        }

        protected virtual void SaveLater() {
            Saveable.SaveLater();
        }

        protected readonly ISaveHelper Saveable;

        protected TrackStateViewModelBase(string key, bool fixedMode) {
            Saveable = new SaveHelper<SaveableData>(key ?? DefaultKey, () => fixedMode ? null : new SaveableData {
                GripStart = GripStart,
                GripTransfer = GripTransfer,
                GripRandomness = GripRandomness,
                LapGain = LapGain,
                Description = Description,
                WeatherDefined = WeatherDefined,
            }, o => {
                GripStart = o.GripStart;
                GripTransfer = o.GripTransfer;
                GripRandomness = o.GripRandomness;
                LapGain = o.LapGain;
                Description = o.Description;
                WeatherDefined = o.WeatherDefined;
            });
        }

        public byte[] ToBytes() {
            try {
                return Encoding.UTF8.GetBytes(Saveable.ToSerializedString() ?? @"{}");
            } catch {
                return Encoding.UTF8.GetBytes(@"{}");
            }
        }

        public Game.TrackProperties ToProperties() {
            return new Game.TrackProperties {
                Preset = null,
                LapGain = LapGain,
                Randomness = RoundIfNeeded(GripRandomness),
                SessionStart = RoundIfNeeded(GripStart),
                SessionTransfer = RoundIfNeeded(GripTransfer)
            };

            double RoundIfNeeded(double v) {
                return SettingsHolder.Drive.AllowDecimalTrackState ? v * 100d : (v * 100d).RoundToInt();
            }
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Full load-and-save mode. All changes will be saved automatically and loaded
        /// later (only with this constuctor).
        /// </summary>
        public TrackStateViewModelBase() : this(null, false) {
            Saveable.Initialize();
        }

        /// <summary>
        /// Create a new AssistsViewModel which will load data from serialized string, but won’t
        /// save any changes if they will occur.
        /// </summary>
        public static TrackStateViewModelBase CreateFixed([NotNull] string serializedData) {
            var result = new TrackStateViewModelBase(DefaultKey, true);
            result.Saveable.Reset();
            result.Saveable.FromSerializedString(serializedData);
            return result;
        }

        /// <summary>
        /// Create a new AssistsViewModel which will load data from serialized string, but won’t
        /// save any changes if they will occur.
        /// </summary>
        /// <param name="section">INI-file section.</param>
        public static TrackStateViewModelBase CreateBuiltIn([CanBeNull] IniFileSection section) {
            if (section == null) {
                return new TrackStateViewModelBase(null, false) {
                    WeatherDefined = true,
                    GripStart = 95d / 100d,
                    GripRandomness = 2d / 100d,
                    LapGain = 132,
                    Description = "Track state specified by weather, or Green, in case weather doesn’t specify track state"
                };
            }

            return new TrackStateViewModelBase(null, false) {
                GripStart = section.GetDouble("SESSION_START", 95d) / 100d,
                GripTransfer = section.GetDouble("SESSION_TRANSFER", 90d) / 100d,
                GripRandomness = section.GetDouble("RANDOMNESS", 2d) / 100d,
                LapGain = section.GetInt("LAP_GAIN", 132),
                Description = section.GetNonEmpty("DESCRIPTION")
            };
        }
        #endregion
    }
}