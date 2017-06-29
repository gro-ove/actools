using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AcManager.Controls.Dialogs;
using AcManager.Controls.Helpers;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Managers;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.SemiGui;
using AcTools.DataFile;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Converters;
using WaitingDialog = FirstFloor.ModernUI.Dialogs.WaitingDialog;

namespace AcManager.Pages.Settings {
    [Localizable(false)]
    public partial class SettingsDebug {
        public SettingsDebug() {
            InitializeComponent();
            DataContext = new ViewModel();
        }

        public class ViewModel : NotifyPropertyChanged {
            private ICommand _decryptHelperCommand;

            public ICommand DecryptHelperCommand {
                get {
                    Logging.Here();
                    return _decryptHelperCommand ?? (_decryptHelperCommand = new DelegateCommand(() => {
                        var m = Prompt.Show("DH:", "DH", watermark: "<key>=<value>");
                        if (m == null) return;

                        var s = m.Split(new[] { '=' }, 2);
                        if (s.Length == 2) {
                            ModernDialog.ShowMessage("d: " + ValuesStorage.Storage.Decrypt(s[0], s[1]));
                        }
                    }));
                }
            }

            private ICommand _magickNetMemoryLeakingCommand;

            public ICommand MagickNetMemoryLeakingCommand => _magickNetMemoryLeakingCommand ?? (_magickNetMemoryLeakingCommand = new AsyncCommand(async () => {
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
                            ImageUtils.ApplyPreviewImageMagick(from, to, 1022, 575, new AcPreviewImageInformation());
                        });
                        w.Report(PluralizingConverter.PluralizeExt(++i, "Copied {0} time"));
                        await Task.Delay(10);
                    }
                }
            }));

            private static BitmapImage LoadBitmapImage(string filename) {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.UriSource = new Uri(filename);
                bi.EndInit();
                bi.Freeze();
                return bi;
            }

            private void MakeCollage(IEnumerable<string> imageFilenames, string outputFilename) {
                var collage = new Grid();
                foreach (var filename in imageFilenames) {
                    collage.Children.Add(new Image {
                        Source = LoadBitmapImage(filename),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                    });
                }

                collage.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                collage.Arrange(new Rect(0d, 0d, collage.DesiredSize.Width, collage.DesiredSize.Height));

                var bitmap = new RenderTargetBitmap((int)collage.DesiredSize.Width, (int)collage.DesiredSize.Height,
                        96, 96, PixelFormats.Default);
                bitmap.Render(collage);

                bitmap.SaveAsPng(outputFilename);
            }

            private DelegateCommand _testCommand;

            public DelegateCommand TestCommand => _testCommand ?? (_testCommand = new DelegateCommand(() => {
                ModernDialog.ShowMessage(SuggestionLists.CarSkinDriverNamesList.JoinToString("\n"));
                ModernDialog.ShowMessage(SuggestionLists.CarSkinTeamsList.JoinToString("\n"));

                //MakeCollage(Directory.GetFiles(@"C:\Users\Carrot\Desktop\Temp\0", "ic_*.png"), @"C:\Users\Carrot\Desktop\Temp\0\comb.png");
            }));
        }
    }
}
