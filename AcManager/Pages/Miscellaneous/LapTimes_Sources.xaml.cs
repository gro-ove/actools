using System;
using System.Windows.Controls;
using AcManager.Tools.Helpers;
using AcManager.Tools.Profile;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Miscellaneous {
    public partial class LapTimes_Sources {
        private ViewModel Model => (ViewModel)DataContext;

        public LapTimes_Sources() {
            DataContext = new ViewModel();
            InitializeComponent();
        }

        public class ViewModel : NotifyPropertyChanged {
            public SettingsHolder.LapTimesSettings LapTimes => SettingsHolder.LapTimes;

            private DelegateCommand _clearCacheCommand;

            public DelegateCommand ClearCacheCommand => _clearCacheCommand ?? (_clearCacheCommand = new DelegateCommand(() => {
                LapTimesManager.Instance.ClearCache();
            }));

            private AsyncCommand<string> _exportLapTimesCommand;

            public AsyncCommand<string> ExportLapTimesCommand => _exportLapTimesCommand ?? (_exportLapTimesCommand = new AsyncCommand<string>(async key => {
                try {
                    using (var waiting = WaitingDialog.Create("Loading lap times…")) {
                        await LapTimesManager.Instance.UpdateAsync();
                        waiting.Report("Exporting…");
                        await LapTimesManager.Instance.ExportAsync(key);
                    }
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t export lap times", e);
                }
            }));
        }
    }
}
