using System.ComponentModel;
using AcManager.Tools.Managers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Tools.Objects {
    [Localizable(false), JsonObject(MemberSerialization.OptIn)]
    public class UserChampionshipDriver : NotifyPropertyChanged {
        public const string PlayerName = "PLAYER";

        [CanBeNull, JsonProperty("name")]
        public string Name { get; }

        [JsonIgnore]
        public bool IsPlayer { get; }

        [CanBeNull, JsonProperty("car")]
        public string CarId { get; }

        [CanBeNull, JsonProperty("skin")]
        public string SkinId { get; }

        public static UserChampionshipDriver Default => new UserChampionshipDriver(PlayerName, "abarth500", "red_white");

        [JsonConstructor]
        public UserChampionshipDriver([CanBeNull] string name, [CanBeNull] string car, [CanBeNull] string skin) {
            Name = name?.Trim();
            CarId = car?.Trim();
            SkinId = skin?.Trim().ToLowerInvariant();
            IsPlayer = Name == PlayerName;
        }

        protected bool Equals(UserChampionshipDriver other) {
            return string.Equals(Name, other.Name) && IsPlayer == other.IsPlayer && string.Equals(CarId, other.CarId) && string.Equals(SkinId, other.SkinId);
        }

        public override bool Equals(object obj) {
            return !ReferenceEquals(null, obj) && (ReferenceEquals(this, obj) || obj.GetType() == GetType() && Equals((UserChampionshipDriver)obj));
        }

        public override int GetHashCode() {
            unchecked {
                var hashCode = Name?.GetHashCode() ?? 0;
                hashCode = (hashCode * 397) ^ IsPlayer.GetHashCode();
                hashCode = (hashCode * 397) ^ (CarId?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (SkinId?.GetHashCode() ?? 0);
                return hashCode;
            }
        }

        #region Progress
        private int _points;

        [JsonIgnore]
        public int Points {
            get { return _points; }
            set {
                if (Equals(value, _points)) return;
                _points = value;
                OnPropertyChanged();
            }
        }

        private int _takenPlace;

        public int TakenPlace {
            get { return _takenPlace; }
            set {
                if (value == _takenPlace) return;
                _takenPlace = value;
                OnPropertyChanged();
            }
        }
        #endregion

        #region Car and car skin objects (for UI only)
        private CarObject _car;

        [CanBeNull, JsonIgnore]
        public CarObject CarObject => _car != null || CarId == null ? _car : (_car = CarsManager.Instance.GetById(CarId));

        private CarSkinObject _skin;

        [CanBeNull, JsonIgnore]
        public CarSkinObject CarSkinObject => _skin != null || CarObject == null || SkinId == null ? _skin : (_skin = CarObject.GetSkinById(SkinId));
        #endregion

        #region Extra “baked” properties
        private double _aiLevel = 100;

        [JsonProperty("aiLevel", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate), DefaultValue(100d)]
        public double AiLevel {
            get { return _aiLevel; }
            set {
                if (Equals(value, _aiLevel)) return;
                _aiLevel = value;
                OnPropertyChanged();
            }
        }

        private string _nationality;

        [CanBeNull, JsonProperty("nationality", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string Nationality {
            get { return _nationality; }
            set {
                if (Equals(value, _nationality)) return;
                _nationality = value;
                OnPropertyChanged();
            }
        }
        #endregion
    }
}