using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AcManager.Controls.Dialogs;
using AcManager.Controls.Helpers;
using AcManager.Tools.Helpers;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.SemiGui;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Pages.About {
    public partial class AboutPage {
        private int _clicks;

        public AboutPage() {
            DataContext = new AboutPageViewModel();
            InitializeComponent();
        }

        private void Version_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            if (!SettingsHolder.Common.DeveloperMode && ++_clicks == 10 &&
                    ModernDialog.ShowMessage("Enable developer mode?", "Developer Mode", MessageBoxButton.YesNo) ==
                            MessageBoxResult.Yes) {
                SettingsHolder.Common.DeveloperMode = true;
            }
        }

        private void ContentElement_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            if (Keyboard.Modifiers != (ModifierKeys.Alt | ModifierKeys.Control)) _clicks += 11;
            if (!SettingsHolder.Common.MsMode && (_clicks += 99) == 990 &&
                    ModernDialog.ShowMessage("Enable most secret mode? Using it might cause data corruption.", "Most Secret Mode", MessageBoxButton.YesNo) ==
                            MessageBoxResult.Yes) {
                SettingsHolder.Common.MsMode = true;
            }
        }

        public class AboutPageViewModel : NotifyPropertyChanged {
            private RelayCommand _moreInformationCommand;

            public RelayCommand MoreInformationCommand => _moreInformationCommand ?? (_moreInformationCommand = new RelayCommand(o => {
                Process.Start("http://acstuff.ru/app");
            }));

            private const string KeyLogsSentTime = "GeneralViewModel.KeyLogsSentTime";

            private AsyncCommand _sendLogsCommand;

            public AsyncCommand SendLogsCommand => _sendLogsCommand ?? (_sendLogsCommand = new AsyncCommand(async o => {
                try {
                    var message = Prompt.Show(
                            "Describe the issue. And, please, leave some contacts if you want to get a response (also, it could help to resolve the issue).",
                            "What’s The Issue?", watermark: "?", multiline: true);
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
