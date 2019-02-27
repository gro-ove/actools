using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Controls.Dialogs;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers.Api;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Pages.ShadersPatch {
    public partial class ShadersInstalledDetails : UserControl {
        private ViewModel Model => (ViewModel)DataContext;

        public ShadersInstalledDetails() {
            DataContext = new ViewModel();
            InitializeComponent();
            LoadBackgroundAsync().Ignore();
        }

        private byte[] _imageData;
        private string _imageMessage;

        private async Task LoadBackgroundAsync() {
            var data = await CmApiProvider.GetStaticDataBytesAsync("patch_img_about", TimeSpan.MaxValue);
            if (data != null) {
                using (var stream = new MemoryStream(data))
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Read)) {
                    _imageData = zip.GetEntry(@"image.jpg")?.Open().ReadAsBytesAndDispose();
                    _imageMessage = zip.GetEntry(@"about.txt")?.Open().ReadAsBytesAndDispose().ToUtf8String();
                    BackgroundImageProgress.IsActive = false;
                    BackgroundImage.Source = _imageData;
                }
            }
        }

        private void OnImageMouseUp(object sender, MouseButtonEventArgs e) {
            if (_imageData == null) return;
            new ImageViewer(BetterImage.LoadBitmapSourceFromBytes(_imageData), x => _imageMessage) {
                MaxAreaWidth = 1920,
                MaxAreaHeight = 1080,
                MaxImageWidth = 1920,
                MaxImageHeight = 1080
            }.ShowDialog();
        }

        public class ViewModel : NotifyPropertyChanged {
            private string _installedVersion;

            public string InstalledVersion {
                get => _installedVersion;
                set => Apply(value, ref _installedVersion);
            }

            private string _installedVersionNumber;

            public string InstalledVersionNumber {
                get => _installedVersionNumber;
                set => Apply(value, ref _installedVersionNumber);
            }

            public ViewModel() {
                InstalledVersion = PatchHelper.GetInstalledVersion();
                InstalledVersionNumber = PatchHelper.GetInstalledBuild();
            }
        }
    }
}