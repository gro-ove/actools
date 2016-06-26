using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AcManager.Controls.Dialogs;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Converters;

namespace AcManager.Pages.Settings {
    public partial class SettingsDev {
        public SettingsDev() {
            InitializeComponent();
            DataContext = new DevViewModel();
        }

        public class DevViewModel : NotifyPropertyChanged {
            private AsyncCommand _magickNetMemoryLeakingCommand;

            public AsyncCommand MagickNetMemoryLeakingCommand => _magickNetMemoryLeakingCommand ?? (_magickNetMemoryLeakingCommand = new AsyncCommand(async o => {
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
