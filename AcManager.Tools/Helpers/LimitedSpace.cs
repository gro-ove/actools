using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Helpers {
    public static class LimitedSpace {
        public const string SelectedSkin = ".SelectedSkin";
        public const string SelectedLayout = ".SelectedLayout";
        public const string SelectedEntry = ".SelectedEntry";
        public const string OnlineQuickFilter = ".QuickFilter";
        public const string OnlineSorting = ".OnlineSorting";

        public static void Initialize() {
            LimitedStorage.RegisterSpace(SelectedSkin, 25);
            LimitedStorage.RegisterSpace(SelectedLayout, 25);
            LimitedStorage.RegisterSpace(SelectedEntry, 25);
            LimitedStorage.RegisterSpace(OnlineQuickFilter, 25);
            LimitedStorage.RegisterSpace(OnlineSorting, 25);
        }
    }
}