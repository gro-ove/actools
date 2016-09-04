using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using AcManager.Controls.Dialogs;
using AcManager.Controls.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Miscellaneous;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Converters;
using WaitingDialog = FirstFloor.ModernUI.Dialogs.WaitingDialog;

namespace AcManager.Pages.Settings {
    [Localizable(false)]
    public partial class SettingsDev {
        public SettingsDev() {
            InitializeComponent();
            DataContext = new ViewModel();
        }

        public class ViewModel : NotifyPropertyChanged {
            private ICommand _sendYearsCommand;

            public ICommand SendYearsCommand => _sendYearsCommand ?? (_sendYearsCommand = new AsyncCommand(async o => {
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
            }, 3000));

            private ICommand _decryptHelperCommand;

            public ICommand DecryptHelperCommand => _decryptHelperCommand ?? (_decryptHelperCommand = new RelayCommand(o => {
                var m = Prompt.Show("DH:", "DH", watermark: "<key>=<value>");
                if (m == null) return;

                var s = m.Split(new[] { '=' }, 2);
                if (s.Length == 2) {
                    ModernDialog.ShowMessage("d: " + ValuesStorage.Storage.Decrypt(s[0], s[1]));
                }
            }));

            private ICommand _magickNetMemoryLeakingCommand;

            public ICommand MagickNetMemoryLeakingCommand => _magickNetMemoryLeakingCommand ?? (_magickNetMemoryLeakingCommand = new AsyncCommand(async o => {
                var image = FileUtils.GetDocumentsScreensDirectory();
                var filename = new DirectoryInfo(image).GetFiles("*.bmp")
                                                       .OrderByDescending(f => f.LastWriteTime)
                                                       .FirstOrDefault();
                if (filename == null) {
                    ModernDialog.ShowMessage("Can’t start. At least one bmp-file in screens directory required.");
                    return;
                }

                using (var w = new WaitingDialog($"Copying {filename.Name}…")) {
                    Logging.Write("TESTING MEMORY LEAKS!");

                    var from = filename.FullName;
                    var to = filename.FullName + ".jpg";
                    Logging.Write("FROM: " + from);
                    Logging.Write("TO: " + to);

                    var i = 0;
                    while (!w.CancellationToken.IsCancellationRequested) {
                        await Task.Run(() => {
                            ImageUtils.ApplyPreviewImageMagick(from, to, 1022, 575);
                        });
                        w.Report(PluralizingConverter.PluralizeExt(++i, "Copied {0} time"));
                        await Task.Delay(10);
                    }
                }
            }));
        }
    }
}
