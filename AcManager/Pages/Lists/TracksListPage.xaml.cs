using System;
using System.Windows;
using AcManager.Controls.Helpers;
using AcManager.Controls.ViewModels;
using AcManager.Internal;
using AcManager.Pages.Drive;
using AcManager.Pages.Windows;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Filters.Testers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using StringBasedFilter;

namespace AcManager.Pages.Lists {
    public partial class TracksListPage : IParametrizedUriContent {
        public void OnUri(Uri uri) {
            var filter = uri.GetQueryParam("Filter");
            DataContext = new ViewModel(string.IsNullOrEmpty(filter) ? null : Filter.Create(TrackObjectTester.Instance, filter));
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            ((ViewModel)DataContext).Load();
            FancyHints.DoubleClickToQuickDrive.Trigger();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            ((ViewModel)DataContext).Unload();
        }

        private class ViewModel : AcListPageViewModel<TrackObject> {
            public ViewModel(IFilter<TrackObject> listFilter)
                : base(TracksManager.Instance, listFilter) {
            }

            protected override string GetSubject() {
                return AppStrings.List_Tracks;
            }

            protected override string LoadCurrentId() {
                if (_selectNextTrack != null) {
                    var value = _selectNextTrack;
                    SaveCurrentKey(value);
                    _selectNextTrack = null;
                    return value;
                }

                return base.LoadCurrentId();
            }
        }

        public static void Show(TrackObjectBase track) {
            // TODO
            if (!AppKeyHolder.IsAllRight) return;

            var mainWindow = Application.Current?.MainWindow as MainWindow;
            if (mainWindow == null) return;

            _selectNextTrack = track.Id;
            _selectNextTrackLayoutId = track.LayoutId;

            NavigateToPage();
        }

        public static void NavigateToPage() {
            (Application.Current?.MainWindow as MainWindow)?.NavigateTo(new Uri("/Pages/Lists/TracksListPage.xaml", UriKind.Relative));
        }

        private static string _selectNextTrack;
        private static string _selectNextTrackLayoutId; // TODO

        protected override void OnItemDoubleClick(AcObjectNew obj) {
            var track = obj as TrackObjectBase;
            if (track == null) return;
            QuickDrive.Show(track: track);
        }
    }
}
