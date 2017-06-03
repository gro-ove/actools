using System;
using System.Linq;
using System.Text.RegularExpressions;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.Online {
    public partial class ServerEntry {
        private static readonly Regex SpacesCollapseRegex = new Regex(@"\s+", RegexOptions.Compiled);
        private static readonly Regex SortingCheatsRegex = new Regex(@"^(?:AA+|[ !-]+|A(?![b-zB-Z0-9])+)+| ?-$", RegexOptions.Compiled);
        private static readonly Regex SimpleCleanUpRegex = new Regex(@"^AA+\s*", RegexOptions.Compiled);

        private static string CleanUp(string name, [CanBeNull] string oldName, out int? extPort) {
            const string mark = "🛈";
            var specialIndex = name.IndexOf(mark, StringComparison.InvariantCulture);
            if (specialIndex != -1) {
                extPort = FlexibleParser.TryParseInt(name.Substring(specialIndex + mark.Length));
                name = name.Substring(0, specialIndex).Trim();
            } else {
                extPort = null;
                name = name.Trim();
            }

            name = SpacesCollapseRegex.Replace(name, " ");
            if (SettingsHolder.Online.FixNames) {
                name = SortingCheatsRegex.Replace(name, "");
            } else if (oldName != null && SimpleCleanUpRegex.IsMatch(name) && !SimpleCleanUpRegex.IsMatch(oldName)) {
                name = SimpleCleanUpRegex.Replace(name, "");
            }
            return name;
        }

        private void CheckPostUpdate() {
            UpdateMissingContent();

            var missingSomething = _missingCarsError != null || _missingTrackError != null;
            if (PortExtended != null) {
                missingSomething |= UpdateMissingContentExtended(missingSomething) == ServerStatus.MissingContent;
            }

            if (Status == ServerStatus.Ready || Status == ServerStatus.MissingContent) {
                Status = missingSomething ? ServerStatus.MissingContent : ServerStatus.Ready;
            }

            UpdateErrorsList();
            AvailableUpdate();
        }

        public bool CheckCars() {
            var cars = Cars;
            if (cars == null) return false;

            if (SettingsHolder.Online.LoadServersWithMissingContent) {
                // In this mode, data is loaded no matter if data is here or not, so we can just update
                // entries without reloading whole thing.

                if (cars.Aggregate(false, (current, car) => current | car.UpdateCarObject())) {
                    if (CurrentDrivers != null) {
                        foreach (var currentDriver in CurrentDrivers) {
                            currentDriver.ResetCar();
                        }
                    }

                    CheckPostUpdate();

                    // Specially for OnlineItem to update list of cars.
                    OnPropertyChanged(nameof(Cars));
                }

                return false;
            }

            for (var i = cars.Count - 1; i >= 0; i--) {
                var car = cars[i];
                if (car.CarObjectWrapper != CarsManager.Instance.GetWrapperById(car.Id)) {
                    goto Dirty;
                }
            }

            return false;

            Dirty:

            Cars = null;
            SetSelectedCarEntry(null);

            if (CurrentDrivers != null) {
                foreach (var currentDriver in CurrentDrivers) {
                    currentDriver.ResetCar();
                }
            }

            Status = ServerStatus.Unloaded;
            return true;
        }

        public void CheckCarSkins(string carId){
            if (Cars?.GetByIdOrDefault(carId) != null) {
                CheckPostUpdate();
            }
        }

        private bool _carVersionIsWrong;
        public void OnCarVersionChanged(CarObject car) {
            if (!_carVersionIsWrong || !PortExtended.HasValue ||
                    Cars?.Any(x => x.CarObjectWrapper?.Value == car) != true) return;
            CheckPostUpdate();
        }

        public bool CheckTrack() {
            if (TrackId == null) return false;

            var track = GetTrack(TrackId);
            if (track == Track) return false;

            Track = track;

            if (SettingsHolder.Online.LoadServersWithMissingContent) {
                // In this mode, data is loaded no matter if data is here or not, so we can just update
                // entries without reloading whole thing.
                CheckPostUpdate();
                return false;
            }

            Status = ServerStatus.Unloaded;
            return true;
        }

        private bool _trackVersionIsWrong;
        public void OnTrackVersionChanged(TrackObjectBase track) {
            if (!_trackVersionIsWrong || !PortExtended.HasValue || Track != track) return;
            CheckPostUpdate();
        }

        public bool CheckWeather() {
            if (!_weatherObjectSet || WeatherId == null) return false;

            var weather = WeatherManager.Instance.GetById(WeatherId);
            if (weather == _weatherObject) return false;

            if (SettingsHolder.Online.LoadServersWithMissingContent) {
                // In this mode, data is loaded no matter if data is here or not, so we can just update
                // entries without reloading whole thing.
                _weatherObject = weather;
                CheckPostUpdate();
                return false;
            }

            Status = ServerStatus.Unloaded;
            return true;
        }
    }
}
