using System.Linq;
using System.Windows.Input;
using AcManager.Tools.Managers;
using AcTools;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Tools.Objects {
    public class UserChampionshipRoundExtended : NotifyPropertyChanged, IDraggable {
        private int _index;

        [JsonIgnore]
        public int Index {
            get { return _index; }
            set {
                if (Equals(value, _index)) return;
                _index = value;
                OnPropertyChanged();
            }
        }

        [JsonIgnore]
        public bool Deleted { get; private set; }

        private ICommand _deleteCommand;

        [JsonIgnore]
        public ICommand DeleteCommand => _deleteCommand ?? (_deleteCommand = new DelegateCommand(() => {
            Deleted = true;
            OnPropertyChanged(nameof(Deleted));
        }));

        private TrackObjectBase _track;

        [JsonIgnore, CanBeNull]
        public TrackObjectBase Track {
            get { return _track; }
            set {
                if (Equals(value, _track)) return;
                _track = value;

                if (value != null) {
                    TrackId = value.IdWithLayout;
                    OnPropertyChanged(nameof(TrackId));
                }

                OnPropertyChanged();

                if (Description == null) {
                    OnPropertyChanged(nameof(DisplayDescription));
                }
            }
        }

        [JsonProperty(@"track"), NotNull]
        public string TrackId { get; set; }

        [JsonIgnore, NotNull]
        public string KunosTrackId => TrackId.Replace('/', '-');

        private int _time = ((CommonAcConsts.TimeMinimum + CommonAcConsts.TimeMaximum) / 2).Round();

        [JsonProperty(@"time")]
        public int Time {
            get { return _time; }
            set {
                value = value.Clamp(CommonAcConsts.TimeMinimum, CommonAcConsts.TimeMaximum);
                if (Equals(value, _time)) return;
                _time = value;
                OnPropertyChanged();
            }
        }

        private double _temperature = ((CommonAcConsts.TemperatureMinimum + CommonAcConsts.TemperatureMaximum) / 2).Round();

        [JsonProperty(@"temperature")]
        public double Temperature {
            get { return _temperature; }
            set {
                value = value.Round(0.5).Clamp(CommonAcConsts.TemperatureMinimum, CommonAcConsts.TemperatureMaximum);
                if (Equals(value, _temperature)) return;
                _temperature = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RoadTemperature));
            }
        }

        [JsonIgnore]
        public double RoadTemperature => Game.ConditionProperties.GetRoadTemperature(Time, Temperature,
                Weather?.TemperatureCoefficient ?? 0d);

        private int _lapsCount = 5;

        [JsonProperty(@"laps")]
        public int LapsCount {
            get { return _lapsCount; }
            set {
                value = value.Clamp(1, 999);
                if (Equals(value, _lapsCount)) return;
                _lapsCount = value;
                OnPropertyChanged();
            }
        }

        private WeatherObject _weather;

        [JsonIgnore, CanBeNull]
        public WeatherObject Weather {
            get { return _weather; }
            set {
                if (Equals(value, _weather)) return;
                _weather = value;
                OnPropertyChanged();
            }
        }

        [JsonProperty(@"weather")]
        public string WeatherId {
            get { return Weather?.Id; }
            private set {
                // ignored
            }
        }

        private Game.TrackPropertiesPreset _trackProperties;

        [JsonIgnore]
        public Game.TrackPropertiesPreset TrackProperties {
            get { return _trackProperties; }
            set {
                if (Equals(value, _trackProperties)) return;
                _trackProperties = value;
                OnPropertyChanged();
            }
        }

        [JsonProperty(@"surface")]
        public int TrackPropertiesId => Game.DefaultTrackPropertiesPresets.IndexOf(_trackProperties);

        private string _description;

        [JsonProperty(@"description")]
        public string Description {
            get { return _description; }
            set {
                if (value != null) {
                    value = value.Trim();
                    if (value.Length == 0) {
                        value = null;
                    }
                }

                if (Equals(value, _description)) return;
                _description = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayDescription));
            }
        }

        [JsonIgnore]
        public string DisplayDescription {
            get {
                if (Description != null) {
                    return Description;
                }

                var d = Track?.Description;
                return string.IsNullOrEmpty(d) ? null : (char.IsLetterOrDigit(d[d.Length - 1]) ? d + @"." : d);
            }
        }

        public UserChampionshipRoundExtended(TrackObjectBase track) {
            TrackId = track.KunosIdWithLayout;
            Track = track;
        }

        [JsonConstructor]
        public UserChampionshipRoundExtended([CanBeNull] string track, [CanBeNull] string weather, int surface) {
            if (track == null) {
                Logging.Warning("Track=null!");
            }

            TrackId = track ?? TracksManager.Instance.GetDefault()?.IdWithLayout ?? @"imola";
            Track = TracksManager.Instance.GetLayoutById(TrackId);
            Weather = weather == null ? null : WeatherManager.Instance.GetById(weather);
            TrackProperties = Game.DefaultTrackPropertiesPresets.ElementAtOrDefault(surface) ?? Game.GetDefaultTrackPropertiesPreset();
        }

        #region Draggable
        public const string DraggableFormat = "Data-UserChampionshipRoundExtended";

        [JsonIgnore]
        string IDraggable.DraggableFormat => DraggableFormat;
        #endregion

        #region Progress
        private int _takenPlace;

        [JsonIgnore]
        public int TakenPlace {
            get { return _takenPlace; }
            set {
                if (Equals(value, _takenPlace)) return;
                _takenPlace = value;
                OnPropertyChanged();
            }
        }

        private bool _isAvailable;

        [JsonIgnore]
        public bool IsAvailable {
            get { return _isAvailable; }
            set {
                if (Equals(value, _isAvailable)) return;
                _isAvailable = value;
                OnPropertyChanged();
            }
        }

        private bool _isPassed;

        [JsonIgnore]
        public bool IsPassed {
            get { return _isPassed; }
            set {
                if (Equals(value, _isPassed)) return;
                _isPassed = value;
                OnPropertyChanged();
            }
        }
        #endregion
    }
}