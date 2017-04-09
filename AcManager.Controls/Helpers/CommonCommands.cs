using System;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcManager.Tools.Profile;
using AcTools.LapTimes;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Controls.Helpers {
    public interface ISomeCommonCommandsHelper {
        void SetupHotlap(CarObject car, TrackObjectBase track);

        void RunHotlap(CarObject car, TrackObjectBase track);
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
                _helper.RunHotlap(p.Item1, p.Item2);
            }
        }, ids => _helper != null));

        private static AsyncCommand<LapTimeEntry> _removeLapTimeEntryCommand;

        public static AsyncCommand<LapTimeEntry> RemoveLapTimeEntryCommand
            => _removeLapTimeEntryCommand ?? (_removeLapTimeEntryCommand = new AsyncCommand<LapTimeEntry>(e => LapTimesManager.Instance.RemoveEntryAsync(e)));
    }
}