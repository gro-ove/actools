using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
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

            public CarEntry(string carId) : this(carId, CarsManager.Instance.GetWrapperById(carId)) { }

            public CarEntry([NotNull] AcItemWrapper carObjectWrapper) : this(carObjectWrapper.Id, carObjectWrapper) { }

            [CanBeNull]
            public CarSkinObject AvailableSkin {
                get { return _availableSkin; }
                set {
                    if (Equals(value, _availableSkin))
                        return;
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
                    if (value == _total)
                        return;
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
                    if (value == _available)
                        return;
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
                return Equals(_availableSkin, other._availableSkin) && Id.Equals(other.Id, StringComparison.OrdinalIgnoreCase) && Total == other.Total &&
                        Available == other.Available;
            }

            public override bool Equals(object obj) {
                return !ReferenceEquals(null, obj) && (ReferenceEquals(this, obj) || obj.GetType() == GetType() && Equals((CarEntry)obj));
            }

            public override int GetHashCode() {
                unchecked {
                    var hashCode = _availableSkin?.GetHashCode() ?? 0;
                    hashCode = (hashCode * 397) ^ Id.GetHashCode();
                    hashCode = (hashCode * 397) ^ Total;
                    hashCode = (hashCode * 397) ^ Available;
                    return hashCode;
                }
            }
        }

        public class CurrentDriver {
            public string Name { get; set; }

            public string Team { get; set; }

            public string CarId { get; set; }

            public string CarSkinId { get; set; }

            public CarObject Car => _car ?? (_car = CarsManager.Instance.GetById(CarId));
            private CarObject _car;

            public CarSkinObject CarSkin => _carSkin ?? (_carSkin = CarSkinId != null ? Car?.GetSkinById(CarSkinId) : Car?.GetFirstSkinOrNull());
            private CarSkinObject _carSkin;

            protected bool Equals(CurrentDriver other) {
                return string.Equals(Name, other.Name) && string.Equals(Team, other.Team) && string.Equals(CarId, other.CarId) && string.Equals(CarSkinId, other.CarSkinId);
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
                    return hashCode;
                }
            }
        }
    }
}
