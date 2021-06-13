using System.Collections.Generic;
using AcManager.Tools.Helpers;
using AcManager.Tools.Profile;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Pages.Miscellaneous {
    public partial class MostSpeed {
        protected override void Initialize(ViewModel viewModel) {
            DataContext = viewModel;
            InitializeComponent();
        }

        protected override double GetCarValue(string carId, IStorage storage) {
            return PlayerStatsManager.Instance.GetMaxSpeedByCar(carId, storage);
        }

        protected override double GetTrackValue(string trackId, IStorage storage) {
            return PlayerStatsManager.Instance.GetMaxSpeedAtTrack(trackId, storage);
        }

        protected override string GetDisplayValue(double value) {
            return string.Format(SettingsHolder.CommonSettings.SpeedFormat, value * SettingsHolder.CommonSettings.DistanceMultiplier);
        }

        protected override double CalculateTotalTrackValue(IEnumerable<double> value) {
            return value.MaxOr(0d);
        }
    }
}