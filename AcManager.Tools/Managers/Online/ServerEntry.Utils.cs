using System;
using System.Text.RegularExpressions;
using AcManager.Tools.Helpers;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
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

        public bool CheckCars() {
            var cars = Cars;
            if (cars == null) return false;

            for (var i = cars.Count - 1; i >= 0; i--) {
                var car = cars[i];
                if (car.CarExists != (CarsManager.Instance.GetWrapperById(car.Id) != null)) {
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
    }
}
