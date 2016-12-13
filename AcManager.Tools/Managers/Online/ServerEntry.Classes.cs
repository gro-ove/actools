using System;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api.Kunos;
using AcManager.Tools.Objects;
using AcTools.Processes;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.Online {
    public partial class ServerEntry {
        public class Session {
            public bool IsActive { get; set; }

            /// <summary>
            /// Seconds.
            /// </summary>
            public long Duration { get; set; }

            public Game.SessionType Type { get; set; }

            public string DisplayType => Type.GetDescription() ?? Type.ToString();

            public string DisplayTypeShort => DisplayType.Substring(0, 1);

            public string DisplayDuration => Type == Game.SessionType.Race ?
                    PluralizingConverter.PluralizeExt((int)Duration, ToolsStrings.Online_Session_LapsDuration) :
                    Duration.ToReadableTime();

            protected bool Equals(Session other) {
                return IsActive == other.IsActive && Duration == other.Duration && Type == other.Type;
            }

            public override bool Equals(object obj) {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((Session)obj);
            }

            public override int GetHashCode() {
                unchecked {
                    var hashCode = IsActive.GetHashCode();
                    hashCode = (hashCode * 397) ^ Duration.GetHashCode();
                    hashCode = (hashCode * 397) ^ (int)Type;
                    return hashCode;
                }
            }
        }

        public class CarEntry : Displayable, IWithId {
            public string Id { get; }

            [CanBeNull]
            public AcItemWrapper CarObjectWrapper { get; }

            public bool CarExists { get; }

            [CanBeNull]
            public CarObject CarObject => (CarObject)CarObjectWrapper?.Loaded();

            private CarSkinObject _availableSkin;

            private CarEntry(string carId, AcItemWrapper carObjectWrapper) {
                Id = carId;
                CarObjectWrapper = carObjectWrapper;
                CarExists = carObjectWrapper != null;
            }

            public CarEntry(string carId) : this(carId, CarsManager.Instance.GetWrapperById(carId)) {}

            public CarEntry([NotNull] AcItemWrapper carObjectWrapper) : this(carObjectWrapper.Id, carObjectWrapper) {}

            [CanBeNull]
            public CarSkinObject AvailableSkin {
                get { return _availableSkin; }
                set {
                    if (Equals(value, _availableSkin)) return;
                    _availableSkin = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PreviewImage));

                    if (Total == 0 && value != null && CarObject != null) {
                        CarObject.SelectedSkin = value;
                    }
                }
            }

            public string PreviewImage => AvailableSkin?.PreviewImage ?? CarObject?.SelectedSkin?.PreviewImage;

            private int _total;

            public int Total {
                get { return _total; }
                set {
                    if (value == _total) return;
                    _total = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsAvailable));
                    OnPropertyChanged(nameof(DisplayName));
                }
            }

            private int _available;

            public int Available {
                get { return _available; }
                set {
                    if (value == _available) return;
                    _available = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsAvailable));
                    OnPropertyChanged(nameof(DisplayName));
                }
            }

            public bool IsAvailable => Total == 0 || Available > 0;

            public override string DisplayName {
                get {
                    var name = CarObjectWrapper?.Value.DisplayName ?? Id;
                    return Total == 0 ? name : $@"{name} ({Available}/{Total})";
                }
                set { }
            }

            protected bool Equals(CarEntry other) {
                return Id.Equals(other.Id, StringComparison.OrdinalIgnoreCase);
            }

            public override bool Equals(object obj) {
                return !ReferenceEquals(null, obj) && (ReferenceEquals(this, obj) || obj.GetType() == GetType() && Equals((CarEntry)obj));
            }

            public override int GetHashCode() {
                return Id.GetHashCode();
            }

            public override string ToString() {
                return DisplayName;
            }
        }

        public class CurrentDriver {
            public string Name { get; }

            public string Team { get; }

            public string CarId { get; }

            public string CarSkinId { get; }

            public bool IsConnected { get; }

            public bool IsBookedForPlayer { get; }

            public CurrentDriver(ServerActualCarInformation x) {
                Name = x.DriverName;
                Team = x.DriverTeam;
                CarId = x.CarId;
                CarSkinId = x.CarSkinId;
                IsConnected = x.IsConnected;
                IsBookedForPlayer = x.IsRequestedGuid;
            }

            public CarObject Car => _car ?? (_car = CarsManager.Instance.GetById(CarId));
            private CarObject _car;

            public CarSkinObject CarSkin => _carSkin ?? (_carSkin = CarSkinId != null ? Car?.GetSkinById(CarSkinId) : Car?.GetFirstSkinOrNull());
            private CarSkinObject _carSkin;

            protected bool Equals(CurrentDriver other) {
                return string.Equals(Name, other.Name) && string.Equals(Team, other.Team) && string.Equals(CarId, other.CarId) &&
                        string.Equals(CarSkinId, other.CarSkinId) && IsConnected == other.IsConnected && IsBookedForPlayer == other.IsBookedForPlayer;
            }

            public override bool Equals(object obj) {
                return !ReferenceEquals(null, obj) && (ReferenceEquals(this, obj) || obj.GetType() == GetType() && Equals((CurrentDriver)obj));
            }

            public override int GetHashCode() {
                unchecked {
                    var hashCode = Name?.GetHashCode() ?? 0;
                    hashCode = (hashCode * 397) ^ (Team?.GetHashCode() ?? 0);
                    hashCode = (hashCode * 397) ^ (CarId?.GetHashCode() ?? 0);
                    hashCode = (hashCode * 397) ^ (CarSkinId?.GetHashCode() ?? 0);
                    hashCode = (hashCode * 397) ^ IsConnected.GetHashCode();
                    hashCode = (hashCode * 397) ^ IsBookedForPlayer.GetHashCode();
                    return hashCode;
                }
            }
        }
    }
}
