using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Helpers {
    public static class LimitedSpace {
        public const string SelectedSkin = ".SelectedSkin";
        public const string SelectedLayout = ".SelectedLayout";
        public const string SelectedEntry = ".SelectedEntry";
        public const string OnlineQuickFilter = ".QuickFilter";
        public const string OnlineSelected = ".OnlineSelected";
        public const string OnlineSelectedCar = ".OnlineSelectedCar";
        public const string OnlineSorting = ".OnlineSorting";
        public const string LapTimesSortingColumn = ".LapTimesSortingColumn";
        public const string LapTimesSortingDescending = ".LapTimesSortingDescending";

        public static void Initialize() {
            LimitedStorage.RegisterSpace(SelectedSkin, 1000);
            LimitedStorage.RegisterSpace(SelectedLayout, 1000);
            LimitedStorage.RegisterSpace(SelectedEntry, 100);
            LimitedStorage.RegisterSpace(OnlineQuickFilter, 100);
            LimitedStorage.RegisterSpace(OnlineSelected, 100);
            LimitedStorage.RegisterSpace(OnlineSelectedCar, 1000);
            LimitedStorage.RegisterSpace(OnlineSorting, 100);
            LimitedStorage.RegisterSpace(LapTimesSortingColumn, 100);
            LimitedStorage.RegisterSpace(LapTimesSortingDescending, 100);
        }
    }
}