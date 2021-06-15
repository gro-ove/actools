using System.Collections.Generic;
using System.IO;
using System.Windows;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Tools.SemiGui {
    public static class DataUpdateWarning {
        public static void BackupData(CarObject car) {
            var data = Path.Combine(car.Location, "data.acd");
            var backup = Path.Combine(car.Location, "data_backup_cm.acd");
            if (File.Exists(data) && !File.Exists(backup)) {
                File.Copy(data, backup);
            }
        }

        public static bool Warn(CarObject car) {
            if (ModernDialog.ShowMessage("Are you sure you want to modify car’s data? You won’t be able to use it online if server would use original version.",
                    "You’re about to modify car’s data", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return false;
            BackupData(car);
            return true;
        }

        public static bool Warn(IEnumerable<CarObject> cars) {
            if (ModernDialog.ShowMessage("Are you sure you want to modify car’s data? You won’t be able to use it online if server would use original version.",
                    "You’re about to modify car’s data", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return false;
            foreach (var car in cars) {
                BackupData(car);
            }
            return true;
        }
    }
}