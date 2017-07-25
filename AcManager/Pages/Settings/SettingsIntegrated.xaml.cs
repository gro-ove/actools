using System;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Settings {
    public partial class SettingsIntegrated {
        public SettingsIntegrated() {
            DataContext = new ViewModel();
            InitializeComponent();
        }

        private ViewModel Model => (ViewModel)DataContext;

        public class ViewModel : NotifyPropertyChanged {
            public SettingsHolder.IntegratedSettings Integrated => SettingsHolder.Integrated;
            public SettingsHolder.LiveSettings Live => SettingsHolder.Live;
            public SettingsHolder.DriveSettings Drive => SettingsHolder.Drive;

            private AsyncCommand _importStereoOdometerCommand;

            public AsyncCommand ImportStereoOdometerCommand =>
                    _importStereoOdometerCommand ?? (_importStereoOdometerCommand = new AsyncCommand(async () => {
                        try {
                            using (WaitingDialog.Create("Importing…")) {
                                await Task.Run(() => StereoOdometerHelper.ImportAll());
                            }
                        } catch (Exception e) {
                            NonfatalError.Notify("Can’t import", e);
                        }
                    }));

            private AsyncCommand _importSidekickOdometerCommand;

            public AsyncCommand ImportSidekickOdometerCommand =>
                    _importSidekickOdometerCommand ?? (_importSidekickOdometerCommand = new AsyncCommand(async () => {
                        try {
                            using (WaitingDialog.Create("Importing…")) {
                                await Task.Run(() => SidekickHelper.OdometerImportAll());
                            }
                        } catch (Exception e) {
                            NonfatalError.Notify("Can’t import", e);
                        }
                    }));
        }
    }
}
