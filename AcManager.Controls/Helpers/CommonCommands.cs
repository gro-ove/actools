using System;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcManager.Tools.Profile;
using AcTools.LapTimes;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Controls.Helpers {
    public interface ISomeCommonCommandsHelper {
        void SetupHotlap(CarObject car, TrackObjectBase track);

        Task<bool> RunHotlapAsync(CarObject car, TrackObjectBase track);
    }

    public static class CommonCommands {
        private static ISomeCommonCommandsHelper _helper;

        public static void SetHelper(ISomeCommonCommandsHelper helper) {
            _helper = helper;
            _runHotlapCommand?.RaiseCanExecuteChanged();
            _setupHotlapCommand?.RaiseCanExecuteChanged();
        }

        private static Tuple<CarObject, TrackObjectBase> ParseParameter(object[] ids) {
            if (ids.Length != 2) {
                Logging.Unexpected();
                return null;
            }

            var carId = ids[0] as string;
            var trackId = ids[1] as string;
            if (carId == null || trackId == null) {
                Logging.Unexpected();
                return null;
            }

            var car = CarsManager.Instance.GetById(carId);
            var track = TracksManager.Instance.GetLayoutByKunosId(trackId);
            if (car == null || track == null) {
                Logging.Warning($"Car or track ({carId}, {trackId}) not found");
                return null;
            }

            return Tuple.Create(car, track);
        }

        private static DelegateCommand<object[]> _setupHotlapCommand;

        public static DelegateCommand<object[]> SetupHotlapCommand => _setupHotlapCommand ?? (_setupHotlapCommand = new DelegateCommand<object[]>(ids => {
            var p = ParseParameter(ids);
            if (p != null) {
                _helper.SetupHotlap(p.Item1, p.Item2);
            }
        }, ids => _helper != null));

        private static DelegateCommand<object[]> _runHotlapCommand;

        public static DelegateCommand<object[]> RunHotlapCommand => _runHotlapCommand ?? (_runHotlapCommand = new DelegateCommand<object[]>(ids => {
            var p = ParseParameter(ids);
            if (p != null) {
                _helper.RunHotlapAsync(p.Item1, p.Item2);
            }
        }, ids => _helper != null));

        private static AsyncCommand<LapTimeEntry> _removeLapTimeEntryCommand;

        public static AsyncCommand<LapTimeEntry> RemoveLapTimeEntryCommand
            => _removeLapTimeEntryCommand ?? (_removeLapTimeEntryCommand = new AsyncCommand<LapTimeEntry>(e => LapTimesManager.Instance.RemoveEntryAsync(e)));

        private static AsyncCommand<LapTimeEntry> _shareLapTimeEntryCommand;

        public static AsyncCommand<LapTimeEntry> ShareLapTimeEntryCommand
            => _shareLapTimeEntryCommand ?? (_shareLapTimeEntryCommand = new AsyncCommand<LapTimeEntry>(e => {
                var driverName = SettingsHolder.Drive.DifferentPlayerNameOnline
                        ? SettingsHolder.Drive.PlayerNameOnline.Or(SettingsHolder.Drive.PlayerName)
                        : SettingsHolder.Drive.PlayerName;
                var steamId = SteamIdHelper.Instance.Value;
                if (steamId != null && steamId.Length > 4) {
                    driverName += $" (…{steamId.Substring(steamId.Length - 4)})";
                }

                return SharingUiHelper.ShareAsync(SharedEntryType.Results,
                        "Lap time", null,
                        $@"Lap time set by {driverName} at {e.EntryDate:dd/MM/yyyy hh:mm tt}:
• Car: {CarsManager.Instance.GetById(e.CarId)?.DisplayName ?? e.CarId};
• Track: {TracksManager.Instance.GetLayoutByKunosId(e.TrackId)?.DisplayName ?? e.TrackId};
• Time: {e.LapTime.ToMillisecondsString()}");
            }));
    }
}