using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AcManager.Controls.Helpers;
using AcManager.Internal;
using AcManager.Tools.Helpers;
using AcManager.Tools.Miscellaneous;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Pages.About {
    public partial class AboutPage {
        private int _clicks;

        public AboutPage() {
            DataContext = new ViewModel();
            InitializeComponent();
            FancyHints.DoYouKnowAboutAndroid.Trigger(TimeSpan.FromSeconds(1));
        }

        private void OnVersionClick(object sender, MouseButtonEventArgs e) {
            if (!SettingsHolder.Common.DeveloperMode && ++_clicks == 7 &&
                    ModernDialog.ShowMessage(AppStrings.About_DeveloperMode, AppStrings.About_DeveloperMode_Title, MessageBoxButton.YesNo) ==
                            MessageBoxResult.Yes) {
                SettingsHolder.Common.DeveloperMode = true;
            }
        }

        public class ViewModel : NotifyPropertyChanged {
            private ICommand _moreInformationCommand;

            public ICommand MoreInformationCommand => _moreInformationCommand ?? (_moreInformationCommand = new DelegateCommand(() => {
                WindowsHelper.ViewInBrowser($"{InternalUtils.MainApiDomain}/app");
            }));

            private ICommand _recentChangesCommand;

            public ICommand RecentChangesCommand => _recentChangesCommand ?? (_recentChangesCommand = new DelegateCommand(() => {
                WindowsHelper.ViewInBrowser($"{InternalUtils.MainApiDomain}/f/d/10-content-manager-detailed-changelog");
            }));

            private const string KeyLogsSentTime = "GeneralViewModel.KeyLogsSentTime";

            private ICommand _sendLogsCommand;

            public ICommand SendLogsCommand => _sendLogsCommand ?? (_sendLogsCommand = new AsyncCommand(async () => {
                try {
                    var message = Prompt.Show(
                            AppStrings.About_ReportAnIssue_Prompt,
                            AppStrings.About_ReportAnIssue_Title, placeholder: @"?", multiline: true);
                    if (message == null) return;
                    await Task.Run(() => AppReporter.SendLogs(message));
                    ValuesStorage.Set(KeyLogsSentTime, DateTime.Now);
                    Toast.Show(AppStrings.About_ReportAnIssue_Sent, AppStrings.About_ReportAnIssue_Sent_Message);
                } catch (Exception e) {
                    NonfatalError.Notify(AppStrings.About_ReportAnIssue_CannotSend, e);
                }
            }, () => (DateTime.Now - ValuesStorage.Get<DateTime>(KeyLogsSentTime)).TotalSeconds > 3d, TimeSpan.FromSeconds(3)));
        }
    }
}
