using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Managers;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.SemiGui;
using AcTools.DataFile;
using AcTools.Processes;
using AcTools.Utils;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Pages.Settings {
    public partial class SettingsDev {
        private ViewModel Model => (ViewModel)DataContext;

        public SettingsDev() {
            DataContext = new ViewModel();
            InitializeComponent();
        }

        public class ViewModel : NotifyPropertyChanged {
            private ICommand _sendYearsCommand;

            public ICommand SendYearsCommand => _sendYearsCommand ?? (_sendYearsCommand = new AsyncCommand(async () => {
                try {
                    await CarsManager.Instance.EnsureLoadedAsync();
                    await TracksManager.Instance.EnsureLoadedAsync();
                    await ShowroomsManager.Instance.EnsureLoadedAsync();

                    await Task.Run(() => AppReporter.SendData("Years.json", new {
                        cars = CarsManager.Instance.LoadedOnly.Where(x => x.Year.HasValue).ToDictionary(x => x.Id, x => x.Year),
                        tracks = TracksManager.Instance.LoadedOnly.Where(x => x.Year.HasValue).ToDictionary(x => x.Id, x => x.Year),
                        showrooms = ShowroomsManager.Instance.LoadedOnly.Where(x => x.Year.HasValue).ToDictionary(x => x.Id, x => x.Year),
                    }));
                    Toast.Show("Data Sent", AppStrings.About_ReportAnIssue_Sent_Message);
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t send data", e);
                }
            }, TimeSpan.FromSeconds(3d)));

            private AsyncCommand _updateSidekickDatabaseCommand;

            public AsyncCommand UpdateSidekickDatabaseCommand
                => _updateSidekickDatabaseCommand ?? (_updateSidekickDatabaseCommand = new AsyncCommand(async () => {
                    using (var w = new WaitingDialog("Updating…")) {
                        var cancellation = w.CancellationToken;

                        w.Report("Loading cars…");
                        await CarsManager.Instance.EnsureLoadedAsync();

                        var list = CarsManager.Instance.EnabledOnly.ToList();
                        for (var i = 0; i < list.Count && !cancellation.IsCancellationRequested; i++) {
                            var car = list[i];
                            w.Report(new AsyncProgressEntry(car.DisplayName, i, list.Count));
                            await Task.Run(() => { SidekickHelper.UpdateSidekickDatabase(car.Id); });
                        }
                    }
                }, () => AcSettingsHolder.Python.IsActivated(SidekickHelper.SidekickAppId)));

            private AsyncCommand _startDirectCommand;

            public AsyncCommand StartDirectCommand => _startDirectCommand ?? (_startDirectCommand =
                    new AsyncCommand(() => GameWrapper.StartAsync(new Game.StartProperties {
                        PreparedConfig = new IniFile(FileUtils.GetRaceIniFilename())
                    })));
        }
    }
}