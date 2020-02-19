using System;
using System.IO;
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
using TimeZoneConverter;

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
                        cars = CarsManager.Instance.Loaded.Where(x => x.Year.HasValue).ToDictionary(x => x.Id, x => x.Year),
                        tracks = TracksManager.Instance.Loaded.Where(x => x.Year.HasValue).ToDictionary(x => x.Id, x => x.Year),
                        showrooms = ShowroomsManager.Instance.Loaded.Where(x => x.Year.HasValue).ToDictionary(x => x.Id, x => x.Year),
                    }, "Years"));
                    Toast.Show("Data sent", AppStrings.About_ReportAnIssue_Sent_Message);
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t send data", e);
                }
            }, TimeSpan.FromSeconds(3d)));

            private AsyncCommand _prepareTrackParamsCommand;

            public AsyncCommand PrepareTrackParamsCommand => _prepareTrackParamsCommand ?? (_prepareTrackParamsCommand = new AsyncCommand(async () => {
                using (var w = new WaitingDialog("Generating config…")) {
                    var cancellation = w.CancellationToken;

                    w.Report("Loading tracks…");
                    await TracksManager.Instance.EnsureLoadedAsync();

                    var list = TracksManager.Instance.Enabled.ToList();
                    var filename = Path.Combine(AcRootDirectory.Instance.RequireValue, @"extension\config\data_track_params.ini");
                    if (!File.Exists(filename)) return;

                    var result = new IniFile(filename);
                    for (var i = 0; i < list.Count && !cancellation.IsCancellationRequested; i++) {
                        var track = list[i];
                        result[track.Id].Set("NAME", track.DisplayNameWithoutCount);
                        w.Report(new AsyncProgressEntry(track.DisplayName, i, list.Count));

                        if (result[track.Id].GetNonEmpty("TIMEZONE") != null) continue;

                        var trackGeoTags = track.GeoTags;
                        if (trackGeoTags == null || trackGeoTags.IsEmptyOrInvalid) {
                            trackGeoTags = await TracksLocator.TryToLocateAsync(track);
                            if (cancellation.IsCancellationRequested) return;
                        }

                        if (trackGeoTags == null) {
                            result[track.Id].Set("LATITUDE", 40.0f);
                            result[track.Id].Set("LONGITUDE", 0.0f);
                            result[track.Id].Set("TIMEZONE", "");
                            continue;
                        }

                        result[track.Id].Set("LATITUDE", trackGeoTags.LatitudeValue ?? 40.0f);
                        result[track.Id].Set("LONGITUDE", trackGeoTags.LongitudeValue ?? 0.0f);

                        var timeZone = await TimeZoneDeterminer.TryToDetermineAsync(trackGeoTags);
                        if (timeZone != null) {
                            result[track.Id].Set("TIMEZONE", TZConvert.WindowsToIana(timeZone.Id));
                        } else {
                            result[track.Id].Set("TIMEZONE", "");
                        }

                        result.Save();
                        await Task.Delay(TimeSpan.FromSeconds(10d), w.CancellationToken);
                        if (w.CancellationToken.IsCancellationRequested) return;

                        result = new IniFile(filename);
                    }

                    result.Save();
                }
            }));

            private AsyncCommand _updateSidekickDatabaseCommand;

            public AsyncCommand UpdateSidekickDatabaseCommand
                => _updateSidekickDatabaseCommand ?? (_updateSidekickDatabaseCommand = new AsyncCommand(async () => {
                    using (var w = new WaitingDialog("Updating…")) {
                        var cancellation = w.CancellationToken;

                        w.Report("Loading cars…");
                        await CarsManager.Instance.EnsureLoadedAsync();

                        var list = CarsManager.Instance.Enabled.ToList();
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
                        PreparedConfig = new IniFile(AcPaths.GetRaceIniFilename())
                    })));
        }
    }
}