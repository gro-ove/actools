using System;
using System.Linq;
using System.Windows.Controls;
using AcManager.Tools.Helpers;
using AcManager.Tools.Profile;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using StringBasedFilter;

namespace AcManager.Pages.Miscellaneous {
    public partial class LapTimes_Sources : IParametrizedUriContent {
        private ViewModel Model => (ViewModel)DataContext;

        public LapTimes_Sources() {
            DataContext = new ViewModel();
            InitializeComponent();
        }

        public void OnUri(Uri uri) {
            var filterValue = uri.GetQueryParam("Filter");
            var filter = string.IsNullOrWhiteSpace(filterValue) ? null : Filter.Create(StringTester.Instance, filterValue);
            List.ItemsSource = (filter == null ? LapTimesManager.Instance.Sources :
                    LapTimesManager.Instance.Sources.Where(x => filter.Test(x.DisplayName))).OrderBy(x => x.DisplayName).ToList();
        }

        public class ViewModel : NotifyPropertyChanged {
            public SettingsHolder.LapTimesSettings LapTimes => SettingsHolder.LapTimes;

            private DelegateCommand _clearCacheCommand;

            public DelegateCommand ClearCacheCommand => _clearCacheCommand ?? (_clearCacheCommand = new DelegateCommand(() => {
                LapTimesManager.Instance.ClearCache();
            }));
        }
    }
}
