using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AcManager.Controls.Helpers;
using AcManager.Controls.Pages.Dialogs;
using AcManager.Tools.Helpers;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.SemiGui;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Pages.About {
    /// <summary>
    /// Interaction logic for About.xaml
    /// </summary>
    public partial class AboutPage {
        private int _clicks;

        public AboutPage() {
            DataContext = new AboutPageViewModel();
            InitializeComponent();
        }

        private void Version_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            if (!SettingsHolder.Common.DeveloperMode && ++_clicks == 10 &&
                    ModernDialog.ShowMessage("Enable developer mode? Using it might cause data corruption.", "Developer Mode", MessageBoxButton.YesNo) ==
                            MessageBoxResult.Yes) {
                SettingsHolder.Common.DeveloperMode = true;
            }
        }

        private AboutPageViewModel Model => (AboutPageViewModel)DataContext;

        public class AboutPageViewModel : NotifyPropertyChanged {
            public AboutPageViewModel() { }

            private RelayCommand _moreInformationCommand;

            public RelayCommand MoreInformationCommand => _moreInformationCommand ?? (_moreInformationCommand = new RelayCommand(o => {
                Process.Start("http://acstuff.ru/app");
            }));

            private const string KeyLogsSentTime = "GeneralViewModel.KeyLogsSentTime";

            private AsyncCommand _sendLogsCommand;

            public AsyncCommand SendLogsCommand => _sendLogsCommand ?? (_sendLogsCommand = new AsyncCommand(async o => {
                try {
                    var message = Prompt.Show("Please, describe the issue.", "What’s The Issue?", watermark: "?", multiline: true);
                    if (message == null) return;
                    await Task.Run(() => AppReporter.SendLogs(message));
                    ValuesStorage.Set(KeyLogsSentTime, DateTime.Now);
                    Toast.Show("Logs Sent", "Thank you for the help!");
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t send logs", e);
                }
            }, o => DateTime.Now - ValuesStorage.GetDateTimeOrEpochTime(KeyLogsSentTime) > TimeSpan.FromHours(0.0001), 3000));
        }
    }
}
